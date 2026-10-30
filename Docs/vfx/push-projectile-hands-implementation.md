# 如来神掌特效：实现笔记

设计见 [push-projectile-hands.md](push-projectile-hands.md) 与 [README.md](README.md)；本文只记实现了什么、和设计的偏离、实施中的新发现与未决项。2026-10-28 起，分支 `refactor/vfx-refactor`。同日第一次画面评审（设计文档第 12 节，提交 `20f37d88`）后按修正决定重做了材质与贴图，本文已按重做后的状态改写；第一版（半透明墨 + Fresnel + 程序化贴图）只在第 2 节末尾留了一行记录。

> **2026-10-30 过时说明**：第 1 节表里的 shader 族（`FX/Mesh_Energy`、`FX/Decal`、`FX/Skin`）、粒子皮、`VFX_Palm` / `VFX_Palm_Anti`、`Mesh_FX_Palm`、`VFX_Template`、程序化贴图、预览台 `PalmVfxPreview` 和特效调试场 `VFX_Sandbox` 都已删除（[retired.md](retired.md) 第 6 节）；现役的是插槽管线（`VFX_Palm_Release` 开火动画 + `VFX_Palm_Bullet` 墨手，见 [slots.md](slots.md) 与 [palm-mesh-tripo.md](palm-mesh-tripo.md)）。还在用的只有阶段控制器、VFX 表、技能接入和 `VFX_Palm_Buff`（及其 `FX/Additive_Glow`、`FX/Alpha_Smoke` 材质）。本文其余部分按历史记录保留。

## 1. 实现了什么

| 部分 | 位置 | 说明 |
|---|---|---|
| shader 族 | `Assets/Rendering/Shaders/FX/` | 手写 URP 14 HLSL，SRP Batcher 兼容，不读深度或不透明纹理，共用 `FXCommon.hlsl`。`FX/Mesh_Energy`：不透明深色剪影 + 金 matcap（只有 matcap 的亮半边变金，暗半边留墨，得到黑金），硬边 cutout 显形 / 溶解，沿飞行方向位置遮罩的前导高光，脉冲模式（同网格加色叠一遍 matcap × 脉冲色）。`FX/Skin`：粒子皮。`FX/Decal`、`FX/Alpha_Smoke`：共用 `FXInk.hlsl` 的实心墨 / 印章 cutout，沿笔画画出、从笔根断开、画出前沿一道金边，`_FxAlpha` 做侵蚀而不是淡出。`FX/Additive_Glow`：经文环与金点 |
| 粒子皮 | `FX/Skin`、`Assets/Editor/VFX/FxSkinMeshBaker.cs` → `Assets/VFX/如来神掌/Mesh_FX_Palm_Skin.asset` | 在主体表面按面积采样 6000 个点（30 % 略沉入内部），烘成一张点云网格（每点四个顶点），一次绘制；顶点里展开成朝相机的软圆点，蓄力时沿法线从外侧聚拢、尖端先落，飞行时微颤闪烁，消散时向前散开；颜色取同一张金 matcap 并设下限（背光的点不会变成黑灰）。`SkillVfxLayer.density`（再乘档位发射倍率）控制画多少点：反噬 0.5、出掌溅出那层 0.25、Low 档 40 % |
| 阶段控制器 | `Assets/Scripts/VFX/SkillVfxPhaseController.cs`、`SkillVfxLayer.cs`、`SkillVfxTypes.cs` | 每个元素一个 `SkillVfxLayer`（阶段、层级、功能、优先级、玩法信息、时间窗、曲线）；控制器按阶段事件启停各层、按 VFX 表换算时长、按 `VfxQuality.Current` 省略层；纯规则在 `SkillVfxRules`，有单测 |
| VFX 表 | `Skill.xlsx` / `VfxRegistry`（改为导出表）、`SkillVfxTable.cs` | README 第 5 节的列（驼峰命名）；行 `palm`、`palm_anti`（`antiOf = palm`）、`palm_buf`；prefab 引用仍在 `SkillVfxDatabase.asset`，测试校验两边一致 |
| 技能接入 | `ChargedHandProjectileRuntime`（只改视觉分支）、`Skill_PushProjectileHands(_Anti)`、`BuddahHandControl`、`SkillExecutor` | 技能资产只写 vfx id；视觉副本按现有时序报 Chg 进度、Imp（= 出手）、Prj、Dis（= 到期，原地溶解后由控制器销毁）；buff 状态层在各端挂到施法者身上 |
| 贴图 | `Assets/VFX/Shared/Textures/` | 形状类来自真实素材：`T_FX_Brush_Stroke`、`T_FX_Enso_Mask` 由 `Tools/vfx/cut_brush.py` 从克利夫兰美术馆开放库（CC0）月山道宗《行草书》2003.354 里的「一」字裁出、转正、清边（A = 形，R = 沿笔画位置；墨圆是同一笔沿圆弧弯成）；`T_FX_Sutra_Glyph` 由 `Tools/vfx/bake_sutra.py` 用 Noto Serif SC（OFL，Windows 11 自带）烘出《心经》开头 32 字。程序化的只剩噪声、软点、金 matcap（`T_FX_Gold_Matcap`）和从网格正投影的硬边印章 `T_FX_PalmPrint(_Broken)_Mask`（`Tools/BuddahGo/VFX/Generate FX Textures`）；`T_FX_Ink_Flow` 已删 |
| 资产 | `Assets/VFX/如来神掌/`：`VFX_Palm`、`VFX_Palm_Anti`、`VFX_Palm_Buf`、`Mesh_FX_Palm`、`Mesh_FX_Palm_Skin`、`Materials/`；`Assets/VFX/Shared/`：贴图与 `VFX_Template.prefab` | 由编辑器脚本生成，可重跑：`Tools/BuddahGo/VFX/` 下 Bake Palm Mesh → Generate FX Textures → Bake Palm Skin → Build Palm VFX → Register Palm VFX |
| 登记 | `SkillVfxDatabase.asset`、`PreloadShader.shadervariants`、RaceMap 的 `VfxStartupWarmup`（新字段 `skillVfxDatabase`，预热表里 `warmup = true` 的条目）、`Buddah.prefab` 的 `SkillFeelRouter` | Feel 事件 `palm_launch_local`、`palm_anti_launch_local`：`技能 / 反噬Reflection_Local` 的副本，去掉 1 s 延迟，缩短到 0.15 s，用单独的 `ImpulseSource_Palm`（幅度 0.3），共用的 `ImpluseSource.prefab` 不动 |
| 预览台 | `Assets/Editor/VFX/PalmVfxPreview.cs`、`Assets/Scripts/VFX/SkillVfxPreviewDriver.cs`（仅 Editor 编译） | 空场景 + 8 单位高的施法者胶囊 + RaceMap 的后期 profile + 施法者 / 侧面 / 受害者三个机位；按真实时序播放，可冻结到指定时刻截图 |
| 特效调试场 | `Assets/VFX/Sandbox/VFX_Sandbox.unity`、`Assets/Scripts/VFX/SkillVfxSandbox.cs`（仅 Editor 编译）、`Assets/Editor/VFX/SkillVfxSandboxBuilder.cs` | `Tools/BuddahGo/VFX/Open VFX Sandbox` 打开，进 Play 用左上角面板：列出 `SkillVfxDatabase` 全部条目和 `Assets/VFX` 下其余 prefab（旧版对比），一键播放；投射类按 Push 表的蓄力 / 寿命 / 速度 / 碰撞盒 / 弹幕数时序，带控制器的挂到施法者身上当状态层，其余在施法者处生成（先去掉 FishNet 组件）；时间倍率、暂停、单帧、施放后冻结、四个机位（含右键环绕的自由机位）、特效质量档、命中占位、实时粒子数 / 帧耗时 / Batches。快捷键 1–9 播放、R 重播、Space 暂停、`.` 单帧、C 清空、Tab 换机位。新加特效后用 `Rebuild VFX Sandbox` 重建 |

没有改任何 RPC 签名、服务器碰撞体、命中、冲量路由或预测逻辑。碰撞盒尺寸由各观察端从本地技能资产（已被配表覆盖）取，缩放倍率沿用 RPC 里已有的 `scaledVisualSize`。

## 2. 与设计的偏离

| 设计 | 实现 | 原因 |
|---|---|---|
| 主体外接盒贴合碰撞盒（README 4、拆解表 4.4） | 主体保持比例等比缩放，装进碰撞盒（缩放 = 三轴比值的最小值）；正常掌碰撞盒正好是参考尺寸，结果不变 | 反噬碰撞盒 60×50×20，逐轴贴合会把手压扁成爪子；等比得到约 0.56 倍，正好是文档第 5 节的"小手约 0.6" |
| 前导金边是单独一行元素 | 做在 `FX/Mesh_Energy` 里，与主体同一网格、同一次绘制 | 少一次绘制；仍是 Core、玩法信息 |
| 粒子皮用 VFX Graph（README 第 7 节、拆解表 4.4，第 8 节第 4 项 `VFX_Palm_Skin.vfx`） | 烘好的点云网格 + `FX/Skin`，一次绘制，由阶段控制器已有的 `_FxProgress` / `_FxDissolve` 驱动 | 项目里没有可复用的 VFX Graph（旧 `如来神掌.vfx` 点云写死，只暴露 Progress / SpawnParticles / TargetPos）；用代码新建 VFX Graph 只能走 VFX 包的内部 API。观感同为致密软点，开销可预测。用户 2026-10-28 定为"点云网格"（经总控转达），设计文档第 7 节由设计会话改 |
| 出掌闪光做在 `FX/Mesh_Energy` 的自发光项里 | 同一网格再画一遍加色的脉冲（`Mat_FX_Palm_Pulse`，2–4 帧） | 阶段控制器按层驱动属性，主体三段（Chg / Prj / Dis）各是一层；单独一层最简单，只在闪光的几帧多一次绘制。颜色是 matcap × 脉冲色，保留金属明暗，不会成为平的剪影 |
| 笔触拖尾用粒子 Ribbon | `TrailRenderer`，长度 = `trail_seconds` | 不占每次施放 4 个粒子系统的名额；Dis 行"拖尾断尾"由它的 0.3 s 尾部侵蚀承担 |
| 劲风是粒子 | 一张笔触面片，沿飞行轴躺在手后，绕该轴转 35° | 只要一笔；面片在前 0.15 s 沿笔画画出、后 0.15 s 从笔根断开；转 35° 让施法者背后和侧面都看得到 |
| 印记位置"出手点空中" | 出手点后方 14（朝施法者一侧），16 × 16 | 原来放在掌前 12、30 × 34，被手挡住，只从指缝露出几块红色：这就是评审第 13 条"指尖附近的红色杂点"的来源 |
| prefab 命名只有 `VFX_Palm` / `VFX_Palm_Anti` | 另有 `VFX_Palm_Buf`（vfx id `palm_buf`） | buff 状态层挂在施法者身上，生命周期与掌不同 |
| `far_threshold_m`、`post_pulse_seconds` | 列已建，标 `doc`（无运行时读者）；脉冲时长由测试校验 Feel 预制体 ≤ 0.15 s | README 第 8 节：远距省略随"特效质量"档一起实现 |
| 第一版（评审前） | 半透明墨 + Fresnel 墨线 + 滚动 Voronoi 墨晕 + 程序化笔触 / 伪字经文 + 白色掌印闪光面片 | 评审判为脏乱，原因与修正见设计文档第 12 节；对应代码与贴图已删除或改写 |
| 佛手"五指微张" | 复用 `佛手Projectile.fbx`：切掉底座（底部 8.5 %），四指绕指根张开 8°，二次误差减面到 2799 面 | 纸面确认 Q7；项目里没有 Blender，减面器 `FxMeshSimplifier` 写在编辑器里 |

## 3. 新发现（已核对代码与配表）

- 运行时冷却 / 施法锁是 12 / 10 s（配表胜过技能资产的 18 / 15）。buff 15 s 超过施法锁；`Buf` 层按 buff 时长，"不超过施法锁"只校验单次施放的 Chg + Prj + Dis（纸面确认 Q1，测试 `OneCastOfTheEffectFitsInTheCastLock`）。
- 反噬是一字横排 5 掌，生成在施法者前方 107 × 缩放处，向后飞、穿过施法者，可打到自己；碰撞盒 60×50×20。视觉跟随实际位置；共享元素（墨迹、破印闪光、劲风、点光、后期脉冲）只放在 0 号掌上，并移到横排中心。
- 掌在命中后不销毁、可多次命中；客户端收不到命中事件；蓄力掌里的 `ignoreSolidWorld` 是死代码（会穿墙）。命中层只留了 `NotifyHit` 接口和 prefab 上的占位节点。
- 施法者本地 buff 到期时，原代码会清空本地视觉 prefab，之后才到达的掌在施法者屏幕上不显示。现在每次生成时按 vfx id 解析，这个问题已一并消除。
- 掌的尺寸是 60×90×20，而角色只有约 8 个单位高：这是一只巨掌，出手时把施法者整个罩住。施法者相关的元素（脚下墨迹、胸前光轮）按角色尺寸做，不跟碰撞盒缩放。
- Balanced 和 Performant 管线资产不开 Depth Texture，Performant 还关了附加光：FX shader 不依赖深度，点光在 Performant 下自然无效。
- 本地施法者沿用现有约定 `IsOwner`。AI 在 host 上也是 owner，所以 host 会在 AI 出掌时播本地后期脉冲；现有的 `Skill_Reflection_Local` 和 FOV 也是同样情况，本轮没改。
- `Buddah.prefab` 存盘时，Unity 清掉了几项代码里早已不存在的旧字段（`directHeading*`、`anchor`、`panelSize`），属于无害改动。
- Unity 打开工程时会重新序列化 `Assets/Settings/URP-PlanarReflection-Renderer.asset`，这个改动不提交。

## 4. 验收记录

### 材质小样（2026-10-28，待用户回复）

预览台（空场景、8 单位高的施法者胶囊、RaceMap 后期 profile）按真实时序播放后冻结截图，正常版本，High 档。图在共享笔记 `palm-vfx-shots/material-stills/`（不进仓库）：

| README 第 9 节要求 | 图 |
|---|---|
| 飞行中主体侧 45°（剪影 + 粒子皮 + 前导） | `1_flight_45_front.png`（掌心一侧）、`1_flight_45_back.png`（手背一侧，带一笔拖尾） |
| 出掌瞬间（自发光脉冲 + 朱砂印记） | `2_launch_caster.png`（施法者机位）、`2_launch_three_quarter.png` |
| 蓄力中（半大的手 + 一笔墨圆 + 可辨经文） | `3_charge_caster.png`、`3_charge_ground.png`（俯看墨圆与《心经》环） |

调到第四轮的过程：第一轮出掌闪光仍是一块平的白色手形（脉冲直接输出颜色）、印章 40 × 40 盖住画面中央、主体整体偏棕；第二轮掌心前导把半只手抬成奶白、拖尾宽成一堵黑墙、显形前沿是一条粗黄边；第三、四轮收窄前导（深度 0.8 以后、亮度 1.1）、拖尾宽 12 收尖、前沿宽 0.02，粒子皮的颜色设下限、未落定的点不显示。反噬五掌未出小样（设计只要求正常版本）。

## 4b. 全量实施前的案头核对（对照 2fa473cb，2026-10-28 晚）

设计文档改为完全按 Piloto「Realistic Ying Yang VFX」后，实施前先核对了拆解表和包的实际内容。数据来自编辑器读出的 prefab（[yingyang/systems.md](yingyang/systems.md)）与 `UberFXSG.shadergraph` 的节点。

- **包材质名**：第 4 节点名的 22 个材质全部存在。
- **网格**：`Cone_LongAOE`（`.asset`）、`ConeTapered.fbx`（子网格 `ConeTapered_Wide`）、`SimpleSphere_VFX_UV.fbx` 存在；`UV_Distorted_PolarCords` 是 `UV_Distorted_PolarCord.fbx` 里的子网格。
- **编译**：`UberFXSG`、`Piloto Warp` 在 2022.3.55 + URP 14.0.11 下编译通过；172 个材质的 shader 都可用，预览台里实际渲染正常。
- **溶解不能用 MaterialPropertyBlock 驱动**（第 8 节第 5 项要改）：`UberFXSG` 没有"溶解量"材质参数。侵蚀只读每个粒子的 `Custom1.y`（UV0.w），亮度倍率读 `Custom1.x`（UV0.z）；材质上只有 `_Disolve_Fade_Range`（默认 −0.25..1 的映射范围）、`_Detail_Disolve_Channel`（噪声通道）和 `_DISABLE_EROSION` 开关。所以"符图随 Progress 显形 / 溶解"要由 `SkillVfxLayer` 每帧用 `ParticleSystem.SetCustomParticleData` 写 Custom1：x = 亮度，y = 侵蚀，由阶段进度或溶解曲线算出。另一种做法是让粒子寿命等于阶段窗口，再用 Custom Data 模块的曲线。前者能跟配表时长走，推荐前者，约 20 行改动。
- **自己发射的粒子必须开 Custom Vertex Streams**（UV、Custom1.xyzw），否则 `Custom1.x` = 0，整片发黑或看不见。
- **文档里几处出处 / 参数和包不符**：
  - 4.2 / 4.4 "白雾核心 `FuzzAdd_Soft` 照 Orb"：Orb 里没有 `FuzzAdd_Soft`，Orb 的核心是黑色的 `FuzzAlpha_Soft` / `FuzzAlpha`（尺寸 3）。白雾的出处应是 Beam（`FuzzAdd_Soft` 尺寸 3，寿命 6）或 Frontal / Flash AoE（尺寸 12–15）。
  - 4.4 墨壳"`_v_speed -1.2` 的流线"：`UberFXSG` 没有 `_v_speed` 参数。流线靠 `_DetailNoisePanning` 的平移（Orb 的墨环是 (0, −0.05)）和 `_MainTexturePanning`；Orb 的墨环网格是 `UV_Distorted_PolarCords` 转 270°，不是锥。
  - 4.2 墨竹"size ≈ 20"：包里 `bamboo_yy_1/2/3` 的尺寸是 7.65 / 9 / 4，×8 是 32–72。≈ 20 是新定的值，不是照搬。
  - 4.4 火星尾"照 Beam 的 Flecks rate 50"：Beam 的 Flecks 是 rate 100，另一组是 25 颗 × 10 次的循环爆发。
- **没有深度 / 不透明纹理时的退化**（Balanced、Performant）：
  - 扭曲（`Piloto Warp`，SceneColor）：URP 下包里的 `DistortionGrabber` 在 `Awake` 里自我禁用（它只服务 Built-in 管线）；不透明纹理关闭时 `_CameraOpaqueTexture` 没人绑定，会采到黑或旧画面。实施时这层要按 `UniversalRenderPipeline.asset.supportsCameraOpaqueTexture` 开关，不能只按特效质量档。
  - 软粒子（`Soft Particle` 子图，SceneDepth）：深度纹理关闭时 `_CameraDepthTexture` 不绑定。D3D 的反向 Z 下读到 0 会被当成远平面，淡出系数为 1，粒子照常显示，只是和地面相交处变成硬边；如果读到 1 会整片消失。**需在 Balanced 档实测一次。**兜底：启动时按管线是否有深度纹理，统一关掉我们那几份材质副本的软粒子关键字。也可以只在主相机上开深度纹理，但这会多一次深度预渲染。

## 5. 未决项

- 材质小样待用户回"过 / 去墨 / 改哪层"；之后再做完全部阶段、反噬与档位的整体验收（性能实测、各档位截图、Practice 施放各 3 次）。
- Realistic Ying Yang VFX 的入库方式（用户 2026-10-28 原话"不进，只拷用到的"，随全量实施一起做）：
  - `Assets/Piloto Studio/`（该包导入的目录）加进 `.gitignore`，不进仓库。
  - 实际用到的贴图、网格、材质、序列帧拷到 `Assets/VFX/` 下，按 README 第 6 节命名；建议放 `Assets/VFX/Shared/Piloto/` 或各技能目录。仓库只跟踪这些拷贝。
  - 我们的 prefab 和材质只引用拷贝，不引用 `Assets/Piloto Studio/` 下的原件。
  - 它依赖的 `UberFXSG` 等 54 个共用资产已在仓库的 `Assets/Plugins/Piloto Studio/` 下跟踪，可直接引用。
  - 在本文加一张"拷贝来源表"（拷贝路径 ← 包内原路径），以后升级包时对照。
- 旧资产（`如来神掌VFXGraph.prefab`、`如来神掌.vfx`、三份 pcache、`射击特效.prefab`、`HandProjectile.prefab`、`如来神掌.prefab`）本轮只解除引用，用户验收通过后再按 CONTRIBUTING 删除。
- 联机两端实测（Steam 双端）未做；本轮只在 Editor 里验证。
