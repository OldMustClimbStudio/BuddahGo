# 禅足冰封（缘起 #2）特效

> - **这是什么**：缘起「禅足冰封」（神足通 + 禅定缓流，`Yuanqi_FrostLock`）的特效设计记录：调研、项目现有素材、冰体材质的四轮小样，以及用户的选择（冰体材质先定 v1.5，第四轮去“糊”后换成 v4，第 4 节）。2026-11-06 特效设计会话。2026-11-06 做了第一版正式特效并接上缘起，替换了程序化占位（第 6、8 节）；细节由用户在 VFXtests 里自己调。
> - **机制以哪里为准**：玩法见 [../gameplay/yuanqi.md](../gameplay/yuanqi.md)（#2），构筑和表现接口见 [../gameplay/yuanqi-architecture.md](../gameplay/yuanqi-architecture.md) 第 4 节。本文只写画面。
> - **Notion**：「技能 Combo 缘起机制」里这一条的视觉是“瞬间生成周围地面冰洁效果，受到影响的敌方单位将会冻结为冰块，禁锢结束后恢复”，前面还有一句意象“加速减速相符而成 — 将周围的空间瞬时膨胀”。

## 1. 画面要服从的玩法事实

| 事实 | 现值 / 出处 | 对特效的意思 |
|---|---|---|
| 禁锢 | N = 1.5 秒，半径 R = 12 米（缓流区 7.5 米），施法者以外全部（真人、AI、木桩） | 冻住到解冻只有 1.5 秒；地面范围直径 24 米，会跨出赛道 |
| 时机 | 服务器触发那一刻就冻住；`RacerSkillPresentationState.FrozenChanged` 开始立即报、结束晚 0.1 秒报 | 冰块跟 `FrozenChanged` 出现，不等地面波 |
| 冻结期间 | 速度归零、推不动、Animator 停、双手不转；HUD 从 `FreezeSecondsLeft` 倒计时 | 冰块是静止的；解冻预警可以读剩余时间 |
| 两招都被消耗 | 神足通拖尾淡出，缓流墨池停止（余韵 1 秒） | 地面效果可以接上缓流墨池收掉的那一下 |
| 表现接口 | `SkillExecutor.YuanqiObserved`（载荷：中心、半径、秒数、目标 id）；每个 racer 的 `FrozenChanged` | 各端都有，不需要新 RPC |
| 佛身 | 浮空的一个头加两只手，没有身体；包围盒约 7.2 × 7.9 × 11.2 米（体型 ×1） | 冰要包住的是“头 + 双手”这个散开的形状 |

## 2. 渲染上的限制（调研结论）

- **读画面颜色做折射的冰，会把冰里的人“折射没了”。** 佛身（layer 6）在 AfterRenderingTransparents 由 `RenderBuddah` 画（[../rendering/ARCHITECTURE.md](../rendering/ARCHITECTURE.md) 帧顺序第 6 步），不在不透明纹理里。Shader Graph 官方冰样例、`Piloto Warp` 都读这张纹理，透过冰只能看到地面。要看到冰里的人，冰只能是 Alpha 混合叠在佛身上；真正的折射在现在的渲染顺序下做不到，只能用视差模拟。
- **档位**：只有 High Fidelity 开深度 / 不透明纹理；Performant 关 HDR（高于 1 的白会被截断，泛光变弱）。冰要靠轮廓和明暗读出来，不能只靠发光。
- **受光照的冰会被黄昏染成琥珀金**（第 4 节小样 2），容易和法相金身混淆。冰要读成“冷”，靠不受光照的白和墨。
- **`UberFXSG` 的能力**：三色映射、Fresnel（可以反转）、细节噪声扭曲、溶解（阈值读 UV0 的自定义通道，遮罩是 Detail Noise）、顶点偏移；不受光照，没有视差和法线。用 `GroundCrack_RadialPostExplody`（中心亮、往外暗）做溶解遮罩，就是“裂纹从中心往外长”，不用新 shader。
- **地面贴合**：工程里没有 URP Decal renderer feature，现有特效都是平面网格；12 米范围在坡上会穿插或悬空。
- **平面反射**：镜像渲染器不画 layer 6，冰块不会有倒影（和现有赛车手一致）。

## 3. 参考

外部：《死神》露琪亚「初の舞・月白」（先圈地、再升冰柱、碎裂，结构最接近）；《斩·赤红之瞳》艾斯德斯「摩诃钵特摩」（冻住时间和空间，对应 Notion 的“空间瞬时膨胀”）；迪士尼《冰雪奇缘》跺脚成雪花再分叉生长；英雄联盟 Lissandra 的黑冰；原神冻结 / 碎冰；无主之地冷冻死亡（冻住后部件碎掉）；《黑神话：悟空》定身法（画面没查到文字描述）；哥窑开片 / 金丝铁线（明代起比作冰裂，先大纹后细纹）；Shader Graph 14 官方冰样例（三层视差 + Fresnel，受光照、读画面颜色）。

项目现有、可借的：包里的 Shield（白墨球壳，最接近冰壳）、Floor Shockwave（0.3 秒墨池炸开，地面瞬冻的底子）、Nova Shockwave（环形外扩，“空间膨胀”的波）、Vertical Rune（地面池 + 光柱，冰柱封印的结构）；贴图 `CrystalNoise`（晶面）、`GroundCrack_RadialPostExplody`（放射白裂纹）、`Frosty_ground_Radial`（灰，可直接进墨白色板）、`Noise_TilingSoftWebbing`（脉络）、`Flare_Cross`（闪点）。包里 `Materials/Ice/` 大多不是冰色；包里没有冰晶或冰块模型。

## 4. 冰体材质小样

场景：RaceMap 起跑区黄昏，High Fidelity；比赛视角（约 97 米、FOV 40）+ 近景 + 环绕。冰的外形是临时程序生成的多面冰晶（一个大块包住头和双手 + 九根斜刺），只用来比材质，不是最终造型。图在 [yuanqi-frost-lock/](yuanqi-frost-lock/)。

**第一轮（三选一）**：

| 小样 | 做法 | 结果 |
|---|---|---|
| 1 特效冰 | `UberFXSG`，白晶面 + 灰中间调 + 白亮边 | 比赛视角最醒目，能透出佛身；晶面纹偏碎 |
| 2 物体冰 | Shader Graph 官方冰样例（受光照、不透明） | 侧面受光变琥珀金、俯看深蓝灰，像大理石或金块；佛身完全看不见 |
| 3 墨冰 | `UberFXSG`，墨壳 + 反转 Fresnel 内透光 | 像黑曜石，画风最统一，远看不够跳 |

**用户选 1**，要更多冰块感：加内折射和一点自发光。

**第二轮（v1 / v2 / v2+）**：在 1 的基础上加四层，全部 `UberFXSG`、不读画面颜色：

| 层 | 做法 | 作用 |
|---|---|---|
| 内壁 | 冰晶翻成里朝外，冷灰，晶面纹 0.3、错开偏移 | 转视角时内外晶面相对移动，模拟内折射和厚度 |
| 内部裂纹 | 0.9 内壳，`Noise_TilingSoftWebbing`，轻微 UV 扭曲 | 冰里的裂纹 |
| 内核微光 | 0.8 内壳，叠加；主贴图用白图（透明度恒为 1），反转 Fresnel | 中间亮的自发光。用脉络贴图时透明度来自 R 通道，几乎全透明，看不见 |
| 外表面 | 本体透明度 0.12–0.35，边缘亮度 2.2 | 硬边更亮，泛光抓得到 |
| 闪点 | 最高 6 个尖角各一个 `Flare_Cross`，朝向摄像机 | 冰面闪点（正式做要闪烁） |

v2 的微光 Fresnel 是 0.45 × 1.0，v2+ 是 1.1 × 1.3。结果：v2 有很强的“冻在冰里”的感觉，但在比赛视角下里面的人基本看不清；v2+ 几乎盖住佛身。

**用户定（2026-11-06）：取 v1 和 v2 之间**：保留内壁视差和闪点，微光比 v2 再弱，让冻住的是谁仍然认得出。

**第三轮（v1 / v1.5 / v2）**：v1.5 用 v2 的五层，只把里面调淡：

| 层 | v2 | v1.5 |
|---|---|---|
| 内壁 | 三色映射透明度 0.30 / 0.45 / 0.65 | 0.18 / 0.30 / 0.45 |
| 内部裂纹 | 中间调 / 高光透明度 0.15 / 0.85 | 0.08 / 0.5 |
| 内核微光 | 底色 0.06，Fresnel 0.45（scale 1.0，power 1.5，反转） | 底色 0.03，Fresnel 0.22（同） |
| 外表面 | 本体透明度 0.12 / 0.35 / 0.8，边缘 2.2（scale 1.8，power 3.5） | 0.15 / 0.4 / 0.85，边缘 2.0（同） |
| 闪点 | 6 个 `Flare_Cross` | 同 |

结果：近景能透过薄微光看清佛脸和双手；比赛视角下仍是一整块白冰，里面的头和手比 v2 好认得多。图：`round3_*`。

**用户定（2026-11-06）：冰体材质用 v1.5。** 精确数值在 [yuanqi-frost-lock/probes/frost_build_v3.cs](yuanqi-frost-lock/probes/frost_build_v3.cs)：`backMid`、`veinsMid`、`coreMid`、`surfMid` 复制自 v2 的 `back` / `veins` / `core` / `surf`，表里没列的参数（贴图、平铺、偏移、扭曲、渲染队列）和 v2 相同；闪点用同一个 `glint` 材质。

**第四轮：去“糊”（v1.5 / v4 / v4 + 棱线）**。用户看第一版后觉得冰“很糊，不完全像冰体”。原因：`CrystalNoise` 本身是软的、又被放大；四层中间灰半透明叠成一层乳白雾；每个晶面亮度差不多，没有明暗交界；外表面和裂纹层的 UV 扭曲、超过 1 的边缘亮度被泛光晕开。v4 不换 shader、不新做贴图，只改这几处：

| 层 | v1.5 | v4 |
|---|---|---|
| 晶面明暗 | 网格顶点色全白 | 外表面和内壁各用一份按假光着色的网格：光方向（物体空间）(-0.45, 0.75, 0.5) 归一化，`d = n·L × 0.5 + 0.5`，分三档 `d < 0.4` / `< 0.72` / 其余 → 暗、中、亮，每个面平涂一个灰度（外表面 0.18 → 1.0，内壁 0.35 → 0.8；颜色 `(v, 1.01v, 1.03v)`） |
| 外表面 | 三色映射 (0.12, 0.14, 0.16, 0.15) / (0.70, 0.75, 0.80, 0.40) / (1.8, 1.85, 1.9, 0.85)，分界 0.45 / 0.78；Fresnel 2.0（scale 1.8，power 3.5）；扭曲 0.05 | (0.80, 0.84, 0.88, 0.48) / (0.88, 0.91, 0.94, 0.58) / (1, 1, 1, 0.7)，分界 0.45 / 0.85（三色几乎一样，一面一个色调，明暗交给顶点色）；Fresnel 1.3（scale 1.2，power 4）；**无扭曲** |
| 内壁 | (0.16, 0.19, 0.22, 0.18) / (0.48, 0.55, 0.60, 0.30) / (1.1, 1.15, 1.2, 0.45)；Fresnel 0.5（scale 0.8，power 3） | (0.45, 0.5, 0.55, 0.18) / (0.55, 0.6, 0.65, 0.24) / (0.7, 0.75, 0.8, 0.3)，分界 0.45 / 0.85；**无 Fresnel**；平铺 0.3、偏移 (0.37, 0.61) 不变 |
| 内部裂纹 | `Noise_TilingSoftWebbing`，平铺 1.4，扭曲 0.12 | **`GroundCrack_RadialPostExplody`**（已 vendor），平铺 0.35，(0, 0, 0, 0) / (0.9, 0.92, 0.95, 0) / (1.1, 1.12, 1.15, 0.5)，分界 0.7 / 0.88；无 Fresnel、**无扭曲**（平铺 0.9 时放射纹会排成星形斑点） |
| 内核微光 | 发光底色 0.03 + 反转 Fresnel 0.22（power 1.5） | **底色 0**（三色全黑），只留反转 Fresnel (0.2, 0.21, 0.22)，scale 1.0，power 2.2 |
| 闪点 | 6 个 `Flare_Cross` | 同 |

结果：v4 的乳白雾基本没了，每个晶面一个平的灰白色调，佛脸和双手更清楚，冰里有几道硬的细裂纹，近看像干净的冰 / 玻璃；比赛视角和 v1.5 接近、略清透。v4 + 棱线（每条夹角大于约 12° 的晶面折线加一条宽约佛身高度 0.45% 的白线，叠加 0.55）最“脆”、远看最像冰晶，但小冰刺上的线太多，近看像线框。图：`round4_*`。

**用户定（2026-11-06）：冰体材质换成 v4（不加棱线）。** 精确数值在 [yuanqi-frost-lock/probes/frost_build_v4.cs](yuanqi-frost-lock/probes/frost_build_v4.cs)：`surf4`、`back4`、`veins4`、`core4` 和着色函数 `shaded`。已按上表改进 `FrostLockProto`（2026-11-06，第 8 节）：四个材质换成 v4，外表面用着色网格 `Mesh_FX_FrostLock_CrystalShaded`，内壁 `Mesh_FX_FrostLock_CrystalBack` 直接带着色（只有内壁用它）；`SkillVfxMeshAlpha` 只改三色映射和 Fresnel 的 alpha，不碰顶点色，所以不冲突。

## 5. 还没定

第一版（第 8 节）对下面几项先给了默认值，用户在 VFXtests 里看过再定（和金足破阵一起汇总在 [../gameplay/yuanqi-open-items.md](../gameplay/yuanqi-open-items.md) 第 1 节）：

- 地面：四条思路（球壳改冰壳、冰柱封印、墨凝开片、空间膨胀）里冰块走了“特效冰”（材质已从 v1.5 换成 v4，插槽已按 v4 重建，0b2c7d2e）。第一版地面是“冰面 + 放射裂纹 + R 处的环”，环外扩一下算“空间膨胀”；整体节奏没有和用户对过。
- 颜色：冰要不要破例加一点淡青；受害者身上的冰是否服从“受击用红”（[README.md](README.md) 第 2 节）。第一版全是中性白。
- 冰块出现、解冻：第一版是 0.09 秒从 88% 长到全尺寸，最后 0.22 秒化掉，化的同时掉一把碎冰。解冻预警（裂纹按剩余时间生长）没做。
- 冰的造型：仍是小样的测试形（围住身体包围盒的大晶块加九根斜刺）；正式造型要围绕“头 + 双手”。
- Balanced / Performant 档、反噬 ×0.3 体型下没看；巨大化 ×4 的冰块在 VFXtests 看过（整块 ×4）。
- 走插槽：已定（第 8 节），和金足破阵一样走缘起 SO 的 vfx_id。

## 6. 挂接点

- 地面：`Yuanqi_FrostLock.PlayObservers` → `PlayVfxSlotLocal(caster, payload.Seconds)`，播 `vfxId = frost_lock_release`（每一端本地播，没有新 RPC）。
- 冰块：`FrozenIceShell`，由 `SkillExecutor.AttachPresentationState` 挂到每个 racer，订阅 `FrozenChanged`：开始时用 `FreezeSecondsLeft` 播 `frost_lock_hit`；冻结提前结束（重置、复活）时 `StopSlotLocal`，冰块当场清掉。
- `Yuanqi_FrostLock.CollectVfxSlots` 列出这两个插槽和长度 N，给时长同步检查用。

2026-11-06 之前这里是程序化占位（7ad81a9c 的 `FrostPlaceholder`、`FrostGroundPlaceholder`、`FrozenIceShellPlaceholder`），已删，记在 [retired.md](retired.md) 第 6.6 节。

## 7. 小样怎么重跑

[yuanqi-frost-lock/probes/](yuanqi-frost-lock/probes/) 里是编辑器探针（`execute_code` 片段，C# 6）和出图脚本：RaceMap 只在内存里打开、物体 `DontSave`、做完回到空场景（合了 dev 以后没有 `MainMenu.unity`，用 `frost_cleanup_empty.cs`；`frost_cleanup.cs` 是之前回 MainMenu 的版本）。`frost_build*.cs` 搭建（无后缀 = 第一轮，`_v2` = 第二轮，`_v3` = 第三轮 v1.5，`_v4` = 第四轮 v4），`frost_capture*.cs` 截图，`frost_present*.py` 加标注、拼 GIF。第一轮的小样 2 需要先把 Shader Graph 冰样例拷到 `Assets/_FrostSwatchLocal/Ice`（做完删掉）。

## 8. 第一版（2026-11-06，实现会话）

照金足破阵的习惯：交付默认值，在 VFXtests 做好预览，细节用户自己调。

**插槽**（README 第 6 节命名，`禅足冰封/` 目录，短名 `FrostLock`）：

| 插槽 | 谁播、播多久 | 层 | 内容 |
|---|---|---|---|
| `frost_lock_release` → `Release/VFX_FrostLock_Release` | `Yuanqi_FrostLock.PlayObservers`，施法者处，N（参考 1.5 s，`Stretch`） | `FX_FrostLock_Imp_C_Decal_01`（玩法层）、`FX_FrostLock_Imp_A_Sparks_02`（点缀，优先级 2）；都是 `CasterRelative` + `casterScaling = None`，贴地 `casterOffset.y = -0.3` | 冰面（`CrystalNoise` 晶面放进圆形软遮罩）、放射裂纹（`GroundCrack_RadialPostExplody`）、R 处的环（`FX_Ring_01_T`：外扩时亮一下，之后留作冰区边缘），0.12 s 铺开，最后 25% 淡出；地面闪点 |
| `frost_lock_hit` → `Hit/VFX_FrostLock_Hit` | `FrozenIceShell`，每个被冻的人身上，冻结剩余秒数 | `FX_FrostLock_Imp_C_Mesh_01`（玩法层）、`FX_FrostLock_Imp_A_Sparks_01`（点缀）；都是 `CasterAttached` + `Whole`（巨人的冰块整块 ×4） | v4 的四层（内壁、内部裂纹、内核微光、外表面，数值照 `frost_build_v4.cs`，外表面和内壁用按假光着色的网格，每个晶面一个平的色调）和小样一样是 `MeshRenderer`；`SkillVfxMeshAlpha` 把层的 alpha 曲线换成 `UberFXSG` 的三色映射透明度和 Fresnel 颜色，并让冰块从 88% 长到全尺寸、融化时缩回；闪点 `Flare_Cross` 每秒 6 个、从最高的 6 个尖角随机冒；解冻时 24 颗碎冰亮点（`Fleks_Shiny`）从冰面飞出、下落 |

粒子系统：地面 4 个，冰块 2 个（另有 4 个网格），都在默认上限 10 以内。材质 `Materials/Mat_FX_FrostLock_*`（全是 `UberFXSG`，不读不透明纹理）；网格 `Meshes/Mesh_FX_FrostLock_*`（小样的晶块、它的着色版、翻面并着色的内壁、闪点位置）。用到的两张包贴图已 vendor。

**怎么改、怎么看**：菜单 `Tools/BuddahGo/VFX/Proto/禅足冰封/Rebuild Slots (ice block + frozen ground)`（`FrostLockProto`，默认值在文件头）重建两个插槽、材质和网格（覆盖写入，GUID 不变；会冲掉手调，手调后要保留就别再跑它）；`.../Place Preview In VFXtests` 在 VFXtests 放施法者、三个被冻的人（一个 ×4）、`Cam_Race`（比赛镜头规则：跟随偏移 ×体型、FOV 30），由 `SkillVfxPreviewFrostLock` 循环触发，同时给场景套上 RaceMap 的观感（`Tools/BuddahGo/VFX/Proto/Apply RaceMap Look To VFXtests`：天空、环境光、雾、太阳、`RaceMap_Dusk_Profile` 后期、FXAA、赛道材质的地板；没有烘焙光照、禅砂和平面反射）。

**做的时候踩到的**（下次做贴地特效先看）：

- **`UberFXSG` 的贴图按线性读**：包的遮罩和裂纹贴图中间灰读出来只有约 0.2，三色映射的阈值和“主贴图 R 通道当透明度”会把它们切掉，只剩中心一小块。做法：阈值压低（0.02 / 0.12），通道权重给到 3–10（`_MainAlphaChannel`、`_AlphaOverrideChannel` 是点乘，可以大于 1），面片按“实际看得见的比例”放大（冰面 0.5、裂纹 0.45、环 0.93，常量在 `FrostLockProto`）。
- **大面片被 `maxParticleSize` 截掉**：粒子渲染器默认最大只到视口的一半，25–50 m 的地面面片在近景里会缩小。这里设成 10。
- **冰块不能用网格模式粒子**：同样的 v1.5 材质（当时的版本）放在“只发一个粒子”的网格系统上，比赛和 VFXtests 里都只剩一层很淡的灰影（×1 的人几乎看不见），同一帧旁边放一个 `MeshRenderer` 版就是小样那块白冰。原因没查到底（顶点流、GPU instancing 都试过，没有区别）。所以冰块用 `MeshRenderer` + `SkillVfxMeshAlpha`；粒子渲染器仍抄 `accel_release` 的顶点流。
- **VFXtests 的替身要放在场景根上**：FishNet 会给装着替身的父物体加 `NetworkObject`，进 Play 时没有 NetworkManager 就把它关掉；预览组件在 `Start` 里重新打开替身（金足破阵的 `SkillVfxPreviewDash` 同样做法）。

**没验证的**：联机（各端由 `PlayObservers` 和 `FrozenChanged` 本地播，没有新 RPC）；Balanced / Performant 档；反噬 ×0.3 体型；坡道上的地面（平面面片，坡上会悬空或穿插）；AI 触发冰封时的画面。改了 R（配表 `YuanqiEffectFrostLock.radiusMeters`）要重跑 `FrostLockProto`，`SkillVfxTests.FrostLockGroundRimStandsAtTheFreezeRadius` 会提醒。
