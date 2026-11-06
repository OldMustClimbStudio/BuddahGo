# 禅足冰封（缘起 #2）特效

> - **这是什么**：缘起「禅足冰封」（神足通 + 禅定缓流，`Yuanqi_FrostLock`）的特效设计记录：调研、项目现有素材、冰体材质的两轮小样，以及用户的选择。2026-11-06 特效设计会话。**还在设计阶段，没有正式实现**；现在比赛里跑的是程序化占位（第 6 节）。
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

## 5. 还没定

- 地面：四条思路（球壳改冰壳、冰柱封印、墨凝开片、空间膨胀）里冰块走了“特效冰”，地面和整体节奏还没选。
- 颜色：冰要不要破例加一点淡青；受害者身上的冰是否服从“受击用红”（[README.md](README.md) 第 2 节）。目前小样全是中性白。
- 冰块出现、解冻预警（裂纹按剩余时间生长）、碎裂或融化，都还没做。
- 冰的造型：现在是测试形；正式造型要围绕“头 + 双手”。
- Balanced / Performant 档、巨大化 ×4 / 反噬 ×0.3 体型下都还没看。
- 正式特效是否走缘起 SO 上的 vfx_id + `SkillVfxReplicator`（金足破阵的做法，[yuanqi-giant-charge.md](yuanqi-giant-charge.md)）。

## 6. 现在比赛里的占位和挂接点

占位在 `feat/yuanqi` 7ad81a9c（实现会话做的，不走插槽）：

- 地面：`Yuanqi_FrostLock.PlayObservers` 调 `FrostGroundPlaceholder.Spawn(payload.Center, Radius, Seconds)`。
- 冰块：`FrozenIceShellPlaceholder`，由 `SkillExecutor.AttachPresentationState` 挂到每个 racer，订阅 `FrozenChanged`。
- 开关：`FrostPlaceholder.Enabled`。

换成正式特效时：在这两个位置挂正式特效，删掉这三个占位文件，并在 [retired.md](retired.md) 记一笔。

## 7. 小样怎么重跑

[yuanqi-frost-lock/probes/](yuanqi-frost-lock/probes/) 里是编辑器探针（`execute_code` 片段，C# 6）和出图脚本：RaceMap 只在内存里打开、物体 `DontSave`、做完回到 MainMenu。`frost_build*.cs` 搭建，`frost_capture*.cs` 截图，`frost_cleanup.cs` 清理，`frost_present*.py` 加标注、拼 GIF。第一轮的小样 2 需要先把 Shader Graph 冰样例拷到 `Assets/_FrostSwatchLocal/Ice`（做完删掉）。
