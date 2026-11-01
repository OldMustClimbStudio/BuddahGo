# 技能特效（VFX）重做：通用规范

分支 `refactor/vfx-refactor` · Unity 2022.3.55f1c1 · URP 14.0.11 · Shuriken + VFX Graph 14 + Feel · 2026-10-28 与用户探讨定稿；同日第一次画面评审后修订第 2、3、7、9、11、12 节（诊断见 [push-projectile-hands.md 第 12 节](push-projectile-hands.md#12-2026-10-28-第一次画面评审诊断与修正)）；同日晚第二次评审按《绝区零》仪玄的分层再次改写第 2、3、7、9、11、12 节（见 [push-projectile-hands.md 第 13 节](push-projectile-hands.md#13-2026-10-28-第二次评审实体与外层水墨分家) 与 [vfx-design-research.md 第 8 节](vfx-design-research.md#8-参考板绝区零仪玄的实体--外层水墨2026-10-28-晚用户指定)）。同日深夜第三次改写第 2、7、8、9 节：用户指定 Piloto Studio「Realistic Ying Yang VFX」为唯一美术基准，素材与 shader 全部改用该包（见 [push-projectile-hands.md 第 14 节](push-projectile-hands.md#14-2026-10-28-晚第三次改写完全按-pilotorealistic-ying-yang-vfx) 与 [vfx-design-research.md 第 10 节](vfx-design-research.md#10-拆解piloto-studiorealistic-ying-yang-vfx2026-10-28-晚用户指定为唯一美术基准)）。

目标：把六个技能（正常 + 反噬共 12 个特效）按"多层结构"逐个重做。现有特效几乎都是单层，没有预备 / 释放 / 命中 / 消散的时间轴，也没有贴花、光、主体、闪光、粒子、后期的空间分层。本文是所有技能共用的规范与流程；每个技能一份专题文档记录现状、拆解表、决定与验收。第一个技能是[如来神掌](push-projectile-hands.md)。

| 文档 | 内容 |
|---|---|
| 本文 | 流程、分层概念、拆解表模板、参数与配表、命名、shader 与贴图、预算与档位、验收与交付、技能顺序、未决问题 |
| [push-projectile-hands.md](push-projectile-hands.md) | 如来神掌：现状问题、目标、拆解表、反噬变体、验收记录、废弃资产 |
| [vfx-design-research.md](vfx-design-research.md) | 2026-10-28 调研：方法论、题材、可读性、意象、URP 技法、性能，附三个候选方向与参考板链接 |

本文只记录设计与决定，不描述已实现的代码；实现进度以各技能文档的验收记录为准。

## 1. 流程（五步，全组共用）

用户 2026-10-28 给出的工业化流程，本项目按以下方式落地：

| 步骤 | 一般做法 | 本项目 |
|---|---|---|
| 1 | 主美对齐参考图与关键词 | 由 [调研文档](vfx-design-research.md) 与参考板承担，关键词与方向见第 2 节；用户签字 |
| 2 | 做 5–10 个标杆特效当全组的尺子 | 六个技能的正常 + 反噬共 12 个特效就是标杆集；如来神掌是第一把尺子 |
| 3 | 写参数表和命名规范 | 第 5 节参数三层与 VFX 表、第 6 节命名 |
| 4 | 搭共用的贴图、材质、模板 | 第 7 节 shader 族、贴图库、模板 prefab，随如来神掌一起建起来 |
| 5 | 新特效从模板起步，每个都过评审 | 每个技能先填拆解表、用户纸面确认一次，出三张材质小样确认一次（2026-10-28 评审后加的唯一中间评审），再一口气做完全部阶段、反噬与档位，整体给用户看一次（用户 2026-10-28：不分多次原型评审） |

本轮分工：由 Claude 通过 Unity MCP 与编辑器脚本搭建资产和 shader，用户只做看图 / 试玩验收（用户自述手调效果一般）。本分支的文档会话只写文档，不改代码或资产。

## 2. 视觉方向（已定，2026-10-28 晚第三次改写）

- **配色原则：正向用仪玄色板，反作用用红（用户 2026-10-29 定的设计原则，优先于本节后面的色板和"反噬色板"两条）**。判断依据是这个效果对谁有利：
  - **正向**：施法者对自己或对别人的正常释放（神足通加速、禅定缓流、如来神掌出掌、巨大化、一念光明等）。色板按 [yixuan-palette.md](yixuan-palette.md)：墨是底、金是光、白是峰值；外黑内亮（光从墨壳里面透出来，不把金贴在墨的外缘）；大面积低饱和。
  - **反作用**：被别人的技能命中的受击效果（被反转等），以及反噬（释放失败、效果落回施法者自己身上的 `_Anti` 版本）。统一用红色系：纯黑墨底，红是 Alpha 混合的雾和烟（不发光），白只做加色的小点。参考值取自用户手调的 `VFX_Reverse_Hit`：红雾 `#FF1010`（Alpha、α≤0.55）、血红烟 `#950000` / `#6F0000`、墨黑 `#000000`、白 `#FFFFFF`。完整色板、逐层取值和比赛截图实测见 [anti-palette.md](anti-palette.md)。
  - 一个插槽只属于一边，不要在同一个插槽里混用金和红。新插槽开工前先确定它是正向还是反作用。

- **地图基调**：枯山水、禅院、谧静；日落下的大理石与沙地，ACES + 轻 bloom。技能特效允许比场景大胆、更有冲击力，但不是另一种美术。
- **方向：黑白湿墨 + 白光，完全照 Piloto Studio「Realistic Ying Yang VFX」的画风做**（用户 2026-10-28 晚："完全参考对方的美术风格，跟那个来"；包的拆解见 [vfx-design-research.md 第 10 节](vfx-design-research.md#10-拆解piloto-studiorealistic-ying-yang-vfx2026-10-28-晚用户指定为唯一美术基准)）：
  - **只有黑白两色。** 黑是**有光泽的湿墨**：墨池、墨浪、墨滴、墨烟，边缘带白描边与液体高光（扭曲网格 + 涂抹贴图 + 三色映射），不是平涂黑块；白是**光**：白色软雾、爆闪、火星、光环。灰只出现在烟和过渡里。
  - **主体是画好的符图面片**，像它的阴阳符：白底、墨边晕染、软边的一张 2048² 图贴在面片上，外面裹黑色扭曲壳和白雾；不是实心网格、不是金属、不是发光金。
  - **唯一的彩色是点缀**（它是竹叶的绿，面积不到 2%）：本项目只保留朱砂印记一处彩色，面积同样 ≤ 2%。金色整体废弃。
  - 题材元素借它的**墨竹**（竹影从地面升起、竹叶散落）与我们的禅院相通；加上本项目自己的掌印符、经文、朱砂印。
- **前三版全部废弃**（留作教训）：半透明网格 + Fresnel 墨线；深色剪影 + 金 matcap + 粒子皮；仪玄式自发光金能量体 + 外层烟墨。共同点是自己造材质和形；这次材质、贴图、网格、shader 全部来自包，本项目只做拼装、尺寸、时序和三张自己的符图。
- **色板（全技能共用）**：墨黑 `#000000`（永远带白描边或白高光，所以不是洞）、白 `#FFFFFF`（光，HDR 强度 1–30 由 shader 三色映射给出）、烟灰 `#808080`（只在烟与过渡）、朱砂 `#C0392B`（仅印记，≤ 2% 面积）。每个技能一个母题、一个剪影；禁止多层火焰烟雾堆叠。
- **反噬色板：黑白互换（阴阳反转）**。正常版符图白底墨边，反噬版黑底白边；白光压暗到 60%；朱砂印记换成灰白"破印"。全技能的反噬都按这条执行，不另做造型。
- 关键词：阴阳、湿墨、白光、墨竹、印、经文。
- 禁止：任何金色或其他彩色（印记除外）、没有白描边的平涂黑块、金属质感、matcap、单独出现的半透明灰雾、程序化生成的形状贴图。

## 3. 分层概念（采用用户提供的《游戏特效分层拆解规范》）

### 3.1 视觉层级

| 层级 | 作用 | 注意力占比（经验值） | 画质优先级 |
|---|---|---|---|
| Core 核心层 | "这是什么技能"：轮廓与主色 | ~60% | 1，任何档位保留 |
| Secondary 次级层 | 体积、动势、环境反馈 | ~30% | 2，Medium 以上保留 |
| Accent 点缀层 | 精致度与节奏感 | ~10% | 3，仅 High |

先做 Core 并确认能单独读懂，再加 Secondary，最后加 Accent。层次看结果不看组件数量：一个粒子系统如果已经分出层次，不必拆成多个。

### 3.2 功能层

glow 辉光（Add）、flash 闪光（Add，1–3 帧）、distortion 扭曲（屏幕扭曲，首版不做）、smoke 烟雾 / 笔触（外层墨，Alpha，核心不透明只羽化边缘）、sparks 火花 / 碎点（金粒：碎羽 / 流线与火星，Add）、decal 贴花（Alpha，淡出要慢）、mesh 主体（画好的符图面片 + 扭曲壳网格，本项目新增；2026-10-28 晚起按 Piloto 包做）、light 实时光（本项目新增，见第 8 节）。一个元素 = 一个视觉层级 + 一个功能层。

### 3.3 时间阶段

| 代码 | 阶段 | 目的 | 时长参考 |
|---|---|---|---|
| `Buf` | 状态持续（本项目新增） | 告诉对手"这个人现在有 X 状态"，权重必须低于任何一次释放 | = 技能 buff 时长 |
| `Chg` | 蓄力 / 预备 | 预告、吸引视线、指明方向 | = 技能蓄力时长 |
| `Prj` | 飞行 / 持续 | "这是什么、往哪飞、有多大、来自谁" | = 投射物寿命或效果时长 |
| `Imp` | 释放 / 命中 | 最强反馈，峰值集中在前 2–4 帧 | 0.05–0.2 s |
| `Dis` | 消散 | 能量衰减，画面让回给比赛 | 0.3–0.8 s |
| `Lin` | 残留 | 环境痕迹、余韵 | ≤ 1.5 s（本项目上限，见第 8 节） |

节奏：蓄力慢、释放快、消散中、残留短。每个技能至少覆盖三个阶段。

### 3.4 拆解表模板

| 元素 | 层级 | 功能 | 阶段 | 起止(s) | 颜色 | 混合 | 优先级 | 备注 |
|---|---|---|---|---|---|---|---|---|
| | Core / Secondary / Accent | glow / flash / distortion / smoke / sparks / decal / mesh / light | Buf / Chg / Prj / Imp / Dis / Lin | 相对阶段起点 | 主色 #RRGGBB | Emissive / Cutout / Add / Alpha / Distort / Decal | 1–3 | 降级策略、玩法信息、参考 |

起止时间写成"相对该阶段起点的秒数"，而阶段的绝对起点来自玩法数值（第 5 节），这样改玩法时长不用改表。携带玩法信息的元素（主体、前导、方向提示）在备注里标 **玩法信息**，任何档位和任何距离都不省略。

## 4. 运行时结构（设计，待实现）

- **阶段控制器**：一个全技能共用的运行时组件（建议名 `SkillVfxPhaseController`），挂在特效 prefab 根上。技能代码只在正确时机报告阶段事件（Buf 开始 / Chg 开始并持续给进度 / Imp / Prj 开始 / Dis / 命中 / Buf 结束）并传入玩法数值；控制器按阶段启停各层、按 VFX 表缩放时长、按档位与距离省略层。每层在 prefab 上标注自己的阶段、层级、优先级。现状对比：`ChargedHandProjectileRuntime` 自己 `SetFloat("Progress")`、自己实例化发射特效、自己算寿命，每个技能一套私有逻辑。
- **特效只在表现层**：现状已经是服务器只跑不可见碰撞体、视觉由 ObserversRpc 让客户端本地实例化，不走 NetworkObject；保持不变。`vfx_quality` 与未来的"特效质量"档位都是客户端本地的，携带玩法信息的层不依赖它们。
- **技能资产统一引用 vfx_id**：现状加速、缓流走 `SkillVfxDatabase` 的 vfx_id，如来神掌直接引用两个 prefab，巨大化与反转的 vfx_id（`accel_anti_vfx`、`reverse_turn_trap_vfx`、`reverse_turn_trap_anti_vfx`）没有条目。改为所有技能只写 vfx_id，VFX 表登记 id 到 prefab 的映射与视觉数值，预热列表从表生成，悬空 id 清掉。
- **反噬配对**：反噬特效与正常特效在表里显式配对（`<id>_anti` 命名，并有一列 `anti_of`），共用同一套层结构，只换反噬色板。
- **视觉尺寸跟随玩法尺寸**：主体网格的外接盒按碰撞盒尺寸自动缩放，随 `PlayerScaleEffect`（巨大化 / 缩小）一起缩；拖尾长度按速度 × 寿命推导。用户以后改数值不用再手调特效。
- **特效插槽（2026-10-29 用户确定）**：每个技能的特效是一组插槽 prefab，技能触发时直接实例化对应插槽播放，不再用挂在玩家身上的特效。插槽有两种：释放（`<技能>/Release/VFX_<技能>_Release`，挂在施法者身上）和受击（`<技能>/Hit/VFX_<技能>_Hit`，挂在被命中的玩家身上；目前只有转向反转有，表现"被反转"）。现有内容都是正向版，反噬版另开插槽。插槽里的层用三种锚点：`CasterRelative` 在出手时摆到施法者身上的固定点后留在原地（如来神掌的出掌爆发）；`CasterAttached` 在窗口内每帧跟着施法者，位置和朝向都跟（buff、拖尾）；`CasterFollow` 每帧只跟位置和体型，朝向停在出手时（贴地区域，禅定缓流；2026-10-30 加）。偏移按施法者坐标系、施法者体型 1 倍计，跟施法者缩放，不跟碰撞盒。插槽在 `Assets/Scenes/VFXtests.unity`（本地测试场景）里手调，用菜单 `Tools/BuddahGo/VFX/Proto/Release Placeholders/` 写回 prefab 和预览。
- **玩家身上特效已全部退休（2026-10-29，用户确定并实施）**：`Buddah.prefab` 的 `VisualRoot/技能Particle`、`反噬Particle`、`Trails`（`AccelerationTrail`、`BlackCurtainTrail`）已删除；`FeelVFX` 两个 `Reflection_Shared` 播放器已连同 `SkillFeelRouter` 条目一起删除（2026-10-30，[retired.md](retired.md) 第 6 节；相机震动、后期等 Local 反馈不变）；`VisualRoot/沙砾` 保留。运行时接法：技能照旧按 vfx_id 走 `SkillVfxReplicator`；特效库里 prefab 带 `SkillVfxPhaseController` 时按插槽播放（生成在该玩家处、把该玩家交给控制器、开 `Imp`、窗口结束后自动销毁）。现用 vfx_id：`palm`→如来神掌 Frontal 爆发（`ChargedHandProjectileRuntime` 路径）、`accel`→神足通释放、`slow`→禅定缓流释放、`reverse_hit`→转向反转受击（服务器给每个被反转的玩家各播一次，`Skill_ReverseTurn.hitVfxId`）、`curtain_trail`→一念光明期间每个玩家身上的路线拖尾（`BlackCurtainViewController` 在本地对每个玩家 `PlaySlotLocal`，拖尾时长随窗口拉长，保留原来"黑幕里只看得见拖尾"的玩法信息）。旧 id `accel_back_vfx`、`slowtrap_vfx` 的表行、库条目和 prefab 已删（2026-10-30）；`palm_buf`、`palm_anti` 仍指向实现会话第五轮的旧 prefab，等对应插槽做好再换。复活或瞬移时，跟随施法者的插槽会清空自己的拖尾（一帧跳超过 60 单位）。
- **跟随与卡顿（2026-10-29）**：角色根节点按物理 / 网络 tick 走（刚体没开插值），模型 `VisualRoot` 由 FishNet 平滑或单机时间线每帧插值。所以施法者锚点的层位置和朝向都跟 `VisualRoot`（经 `BuddahPredictionVisualRootBridge`），否则会对着模型和相机一顿一顿；体型仍取根节点（`PlayerScaleEffect` 缩放根节点）。`CasterAttached` 层的窗口结束后虽然停止发射，残留粒子仍跟着施法者，直到插槽销毁，不会留在赛道上。
- **跟随与收尾（2026-10-30，用户在技能调整里定）**：
  - 只跟随：施法者转向时整片特效不该跟着转的层（贴地区域）用 `CasterFollow`。转向反转受击 `reverse_hit` 仍是 `CasterAttached`，它看着"只跟随"是因为内容几乎全是朝相机的面片；贴地网格（Local 对齐）用 `CasterAttached` 会整片旋转。
  - 自然消失：层勾 `particlesDieNaturally` 时，窗口结束（= 技能时长结束）那一刻层下所有粒子系统同时停止发射，已有粒子按各自寿命播完（Stretch 时寿命 × k），插槽等最后一个粒子死完才销毁（`SkillVfxPhaseController.DestroyWhenFinished` 取 max(固定尾巴 1.5 s × k, 最长寿命)）。不勾时仍是窗口后固定 1.5 s × k 销毁，寿命更长的粒子会被瞬间切掉。特效本身的播放逻辑（循环、寿命、延迟）不改，只决定何时停止发射。测试：`SkillVfxAnchorTests.CasterFollowLayerFollowsThePositionButNotTheFacing`、`SkillVfxTimingTests.ParticlesDieNaturallyKeepsTheSlotUntilTheLastParticleDies`。
  - 跟技能一起收（用户定：符在技能结束前 1 秒开始消失，消失约 1 秒，和其他特效同步结束）：寿命长的粒子系统（转向反转的阴阳符）自然消失会在技能结束后还挂着，看着像技能没结束。把它拖进层的 `endWithWindow`，在窗口结束前 `endWithWindowSeconds`（真实秒，默认 1）它停止发射，每个现存粒子跳到它自己颜色渐变最后一段（淡出段）上透明度相同的位置，再把寿命设成正好用这几秒播完淡出段：已经全亮的从淡出开头播，刚出生还在淡入的从同样透明度处往下播，已经在淡出的不跳；本来就会更早死的不动。所以窗口结束（其余层停止发射）那一刻它正好消失，不另加淡出，也不会突然变亮或变暗。窗口比这几秒还短、或被提前结束时，在 `Deactivate` 里补播同样的结尾。插槽销毁时间对这些系统只算 `endWithWindowSeconds`。只在勾了 `particlesDieNaturally` 时生效。实现：`SkillVfxLayer.BeginEnding`、`SkillVfxRules.EndingAge`。测试：`SkillVfxTimingTests.EndWithWindowCapsTheLongLivedSystemsTail`、`EndingAgeKeepsTheParticlesAlpha`。
  - 贴地高度：比赛赛道在角色 pivot（`VisualRoot`）下约 0.30（体型 1 倍），VFXtests 的 `Floor` 在 pivot 下 4.25。在 VFXtests 里调的贴地内容如果低于 pivot 0.3，比赛里会埋进赛道被深度挡住；用层的 `casterOffset.y` 整层抬高，层内相对位置不动（禅定缓流 0.62）。
- **渲染顺序（2026-10-29，用户确定"同层即可"）**：为了让角色在一念光明的黑幕上可见，`URP-HighFidelity-Renderer` 的 `RenderBuddah` 在透明物之后又画一遍 Buddah 层，这会把角色盖在所有特效上面。现在技能特效和角色同层：`SkillVfxPhaseController.Configure` 把特效里 Default 层的物体改到 `Buddah` 层（已有专用层的保留，如一念光明的 `BlackCurtainTrail`）。渲染器最后加了 `RenderSkillVfx`（透明队列、Buddah 层、透明物之后），普通透明 pass 不再画 Buddah 层，所以特效只画一次，并且和角色做深度遮挡：在角色前面的挡住角色，在角色后面的被角色挡住。VFX Graph 的输出也走这个 pass（通用前摇曾临时复用旧 Graph，比赛里截图验证过）。编辑器里同一条规则：10 个插槽 prefab 本身已在 `Buddah` 层（菜单 `Tools/BuddahGo/VFX/Move Slot Prefabs To Character Layer`），`SkillVfxCharacterLayer` 在非 Play 时、层级变化后把 VFXtests 和 prefab 模式里插槽下新加的 Default 层物体改到 `Buddah` 层，所以预览和比赛的遮挡一致（2026-10-29 用户要求修 VFXtests 的渲染层）。只有 HighFidelity 渲染器有这套 pass，其他档位的渲染器层遮罩是全开的，本来就没有这个问题。
- **时长缩放（2026-10-29）**：技能时长来自配表，插槽按比例适配。控制器 `referenceSeconds` 写的是"这个特效按多长调的"（例如 `reverse_hit` 的 Imp 是 10 秒）。层窗口用 `PhaseFraction`（相位的百分比，例如 0–0.3 表示前 30%），配表改了时长，窗口自动跟着走。窗口里的内容由每层的 `timeScaling` 决定，k = 实际窗口 ÷ 参考窗口：`Fixed` 内容按原样播放，只移动开始和停止发射的时刻；`Stretch` 整体变速（每个粒子系统按 1/k 速度模拟，延迟、寿命、运动一起等比例缩放，看起来一样，只是变慢或变快）；`Rhythm` 只缩放节奏（开始延迟、发射时长、burst 时间乘 k，粒子本身的寿命和速度不变；例如 10% 处的延迟始终在 10%）。没写参考长度的相位不缩放，`Seconds` 窗口也不缩放。插槽 Inspector 底部有 "Timing preview"：输入一个时长（或点 ×0.5/×1/×2），列出每层窗口和每个粒子系统从百分之几开始发射、到百分之几停止、到百分之几最后一个粒子消失。旁边的按钮把该相位的 `Seconds` 窗口换算成参考长度下的比例。（2026-10-29 早些时候的状态：`reverse_hit` 10 秒（0–100%）、`slow` 11 秒（0–55%）、`accel` 10 秒（0–30%）、`curtain_trail` 13 秒（0–100%）。四个都还是 `Fixed`，当前时长下表现不变。下一条起按"时长同步"改过。）
- **时长同步（2026-10-29）**：用户要靠配表改前摇和持续时间来调强度，特效必须跟着走。规则：
  1. **长度只有一个来源**：特效播多久 = 配表给的玩法时长。前摇用 `Skill.castConfirmDelaySeconds`（通用前摇 `cast` / `cast_anti`；反噬行固定 2 s，是必需的惩罚设计，不要改回 1，2026-10-31 用户定。`cast_anti` 是例外：层窗口按秒 0–7 s，内容不随前摇缩短，前摇结束后余波照常播完，见 [slots.md](slots.md)），持续用 `Skill.effectDurationSeconds` 写进的技能主时长字段（一念光明是展开 + 保持 + 淡出）。每个技能在 `SkillAction.CollectVfxSlots` 里列出它播的特效和长度，播放用的是同一套字段，所以下面的窗口显示的就是比赛里的长度。技能 SO 上的 `vfxDurationSeconds` 保持 0；填了正数就是写死，不再跟配表（窗口会标出来）。
  2. **插槽按参考时长自动变速**：`referenceSeconds` 的 Imp 写"按多长调的"。所有已接线的释放 / 受击插槽都是 `Stretch`，窗口用 `PhaseFraction`，参考时长 = 现在配表的值，所以当前表现不变；配表改短一半，特效整体 2 倍速播完，改长就放慢。某层不想变速可以在 Inspector 改成 `Rhythm`（只缩放节奏）或 `Fixed`（内容原速、只移动窗口）。`curtain_trail` 是玩法拖尾，保持 `Fixed`。
  3. **核对工具**：`Tools/BuddahGo/VFX/Skill Timing Sync (特效时长同步)` 按当前配表列出每个技能的前摇和每个特效：比赛里的时长、参考时长、每层的倍速和状态。变速超过一倍（慢于 0.5 倍或快于 2 倍）标橙，建议在 VFXtests 按新时长重调；重调好以后点"设为参考"，参考时长改成新值，倍速回到 1。插槽 Inspector 的 Timing preview 多了 "Game length (配表)" 按钮，点一下就用比赛里的时长预览。
  4. **测试**：`SkillVfxTimingSyncTests` 检查每个技能播的特效都在 `SkillVfxDatabase` 里、每个插槽都有参考时长，以及 giant / reverse / reverse_hit / slow 的长度等于配表。

- **自动校验**（配表导入规则 + EditMode 测试）：每个技能引用的 vfx_id 必须有条目；特效总时长不得超过施法锁；拆解表声明的阶段在 prefab 上都有对应层；新 shader 登记进 `Assets/VFX/PreloadShader.shadervariants`，新 prefab 进预热列表。

## 5. 参数三层与 VFX 表

| 层 | 内容 | 存放 |
|---|---|---|
| 玩法数值 | 蓄力时长、投射物寿命、buff 时长、碰撞盒尺寸、发射数量与间距、速度 | 已有的技能配表（`Assets/Design/Tables/Skill/Skill.xlsx`）。特效不另存一份，运行时直接推导 |
| 视觉专属数值 | 各层相对时长比例、尺寸倍率、强度、拖尾长度系数、消散时长、点光寿命、远距省略阈值 | 新的 VFX 表（配表清单早已建议的 `VFX_Table`，按 [design-tables/README.md](../design-tables/README.md) 的规则建表） |
| 颜色、贴图、曲线、材质 | | prefab 与材质，不进表 |

VFX 表建议列：`vfx_id`、`anti_of`、`prefab_path`、`chg_ratio`（蓄力层占蓄力时长的比例）、`imp_frames`、`dis_seconds`、`lin_seconds`、`buf_fade_seconds`、`trail_seconds`（拖尾长度 = 速度 × 该值）、`scale_multiplier`、`light_seconds`、`light_intensity`、`far_threshold_m`（远距省略阈值）、`post_pulse_seconds`、`warmup`。时序初始值（用户 2026-10-28 确认，之后随时改）：预备 = 蓄力时长；释放闪光 2–4 帧；飞行 = 投射物寿命；未命中到期的消散 0.3–0.5 s；命中后的消散 ≤ 1.5 s；buff 状态标识淡入淡出各 0.3 s；施法者本地后期脉冲 ≤ 0.15 s。

## 6. 命名

- 元素 / 发射器：`FX_<技能>_<阶段>_<层级>_<功能>_<序号>`，阶段用 `Buf / Chg / Prj / Imp / Dis / Lin`，层级用 `C / S / A`。例：`FX_Palm_Imp_C_Flash_01`、`FX_Palm_Prj_S_Smoke_01`。
- 技能短名：`Palm`（如来神掌）、`Accel`（神足通）、`Slow`（禅定缓流）、`Curtain`（一念光明）、`Giant`（巨大化）、`Reverse`（转向反转）；反噬加 `Anti`：`FX_PalmAnti_Imp_C_Flash_01`。
- 贴图 `T_FX_<描述>_<类型>`（类型：Mask / Noise / Flow / Flipbook / Glyph / Packed）；材质 `Mat_FX_<技能>_<元素>`；shader `FX/<功能层>`；prefab 和 vfx_id 见下一条。
- **插槽 prefab 与 vfx_id（用户 2026-10-30 定，所有技能统一）**：
  - 插槽类型五种：`Cast`（通用前摇）、`Release`（施法者身上的释放 / 开火动画）、`Bullet`（飞出去的子弹，随判定盒走）、`Hit`（被命中的玩家身上）、`Buff`（施法者身上的状态）。
  - vfx_id = `<技能短名小写>_<插槽小写>`，反噬加 `_anti`：`palm_release`、`palm_release_anti`。表里 `antiOf` 填正向 id，测试 `VfxTableRowsPairAntiEffectsAndPointAtTheirPrefabs` 检查 `<antiOf>_anti`。通用前摇是例外，只有 `cast` / `cast_anti`。
  - prefab = `Assets/VFX/<技能中文名>/<插槽>/VFX_<技能短名>_<插槽>[_Anti].prefab`；通用前摇的短名是 `Common`。反噬版优先做正向的 prefab 变体（只覆盖颜色），改正向会同步过去。
  - 插槽控制器的 `SkillVfxPhaseController.vfxId` 和 prefab 根节点名都按这两条走。
  - `Assets/Scenes/VFXtests.unity` 里每个特效实例都用它的 prefab 名（例如 `VFX_Slow_Release`，不再叫 `ReleasePlaceholder_Slow`）；`SkillReleaseProto` 和 `PalmFrontalProto` 按这个名字找实例。

  | 技能 | vfx_id | prefab |
  |---|---|---|
  | 通用前摇 | `cast`、`cast_anti` | `通用前摇/Cast/VFX_Common_Cast`、`VFX_Common_Cast_Anti` |
  | 如来神掌 | `palm_release`、`palm_release_anti` | `如来神掌/Release/VFX_Palm_Release`、`VFX_Palm_Release_Anti` |
  | | `palm_bullet`、`palm_bullet_anti` | `如来神掌/Bullet/VFX_Palm_Bullet`、`VFX_Palm_Bullet_Anti` |
  | | `palm_buff` | `如来神掌/Buff/VFX_Palm_Buff` |
  | 神足通 | `accel_release` | `神足通/Release/VFX_Accel_Release` |
  | 禅定缓流 | `slow_release` | `禅定缓流/Release/VFX_Slow_Release` |
  | 转向反转 | `reverse_release`、`reverse_hit` | `转向反转/Release/VFX_Reverse_Release`、`转向反转/Hit/VFX_Reverse_Hit` |
  | 一念光明 | `curtain_release`、`curtain_hit` | `一念光明/Release/VFX_Curtain_Release`、`一念光明/Hit/VFX_Curtain_Hit` |
  | 巨大化 | `giant_release` | `巨大化/Release/VFX_Giant_Release` |

  2026-10-30 之前的旧名（`palm`、`palm_hand`、`palm_buf`、`accel`、`slow`、`reverse`、`curtain`、`curtain_trail`、`giant`，以及 `Proto/Release`、`Proto/Hit`、`Proto/Frontal` 目录）只留在各文档带日期的历史记录里。
- 资产目录：`Assets/VFX/<技能中文名>/` 沿用现有目录；共用资源放 `Assets/VFX/Shared/`（贴图、材质、模板 prefab），shader 放 `Assets/Rendering/Shaders/FX/`（与已有的 `BuddahGo/Lit` 同处）。

## 7. Shader、贴图与模板（2026-10-28 晚第三次改写）

- **路线：直接用包的 shader、材质、贴图、网格，本项目只拼装。** 包的全部材质走一个 Shader Graph `UberFXSG`（Main Texture + Alpha Override 遮罩 + Detail Noise 按通道做乘 / 加 / 溶解 / 扭曲 / 顶点偏移 + **Color Ramping 三色映射** + Fresnel + 软粒子 + UV 平移），屏幕扭曲走 `Piloto Warp`。黑白风格就是三色映射设成黑 / 深灰 / 白（墨）或白 / 灰 / 黑（光）。之前"Shader Graph 退出特效制作、手写 HLSL 族"的决定作废：手写复刻它的三色映射没有意义，已写的 `FX/*` HLSL 保留文件但不再用于新元素。VFX Graph 仍不用。
- **素材**：包按 Single Entity 授权在本项目内使用，贴图、材质、网格、shader 都可以直接引用，不限于它的 prefab；导入到 `Assets/Piloto Studio/`（保持原 GUID，便于日后升级），不改包内文件。本项目**只新建三张贴图**：掌印符图 `T_FX_PalmSymbol`（白底、墨边晕染、软边，照 `yingyang_1` 的做法画，2048²）、反噬破印 `T_FX_PalmSymbol_Broken`（黑底白边、带裂口）、朱砂印 `T_FX_PalmSeal`（印章式硬边）；经文环沿用 Noto Serif CJK 烘的真实《心经》（已有）。**不再程序化生成任何形状贴图**，噪声与渐变也直接用包里的。
- **资源管理规则（用户 2026-10-29："合理管理所有特效资源"）**：
  - **一个技能一个目录** `Assets/VFX/<技能中文名>/`。插槽 prefab 只放在 `Proto/Release/`、`Proto/Hit/`（如来神掌出掌在 `Proto/Frontal/`）；全技能共用的插槽放 `Assets/VFX/通用前摇/`。插槽总表 [slots.md](slots.md) 和插槽一起更新。
  - **从包里拷来的材质、贴图、网格只放** `Assets/VFX/Shared/Piloto/<Materials|Textures|Meshes|Shaders>/`，由 `VfxPackVendor` 管，不手动拷、不手动改名；来源表 [asset-sources.md](asset-sources.md) 由工具重写。用户手调的预选 prefab 放 `Assets/VFX/Shared/预选/`。
  - **包目录 `Assets/Piloto Studio/` 只读**：不在里面建或改文件，入库资产不得引用它。
  - **测试场景** 只用 `Assets/Scenes/VFXtests.unity`，不入库；旧的 `Assets/VFX/Sandbox/` 已退休。
  - **新插槽登记四处**：`SkillReleaseProto.Skills`、`Skill.xlsx` 的 VfxRegistry（导入后自动进配表数据库）、`SkillVfxDatabase`、`VfxPackVendor.EffectPrefabs`。少一处，入库检查或运行时就会找不到。
  - **退休的东西不立刻删**：先记进 [retired.md](retired.md)（内容、引用、替代、状态），全部特效做完后按表一次清除。替换或删除任何特效、Feel 播放器、特效相关脚本时，同一次改动里更新这张表。
  - **备份**：改用户调过的插槽前，先把 prefab 备份到会话 scratchpad（不进工程）。
- **第三方包只拷用到的（用户 2026-10-28 决定，2026-10-29 跑通）**：包的源目录只留在本地、写进 `.gitignore`，不进仓库；实验时可以随意直接用原件。特效 prefab 要入库前，跑菜单 `Tools/BuddahGo/VFX/Copy Used Pack Assets Into Project`（`VfxPackVendor`）：它解包拖进插槽的包 prefab，把这些特效还在用的材质、贴图、网格拷到 `Assets/VFX/Shared/Piloto/<类型>/`（已拷过的复用），把引用改指副本，并重写来源表 [asset-sources.md](asset-sources.md)。新特效 prefab 加进它的 `EffectPrefabs` 列表。入库的资产不得引用 `Assets/Piloto Studio/`；包里和旧 `Assets/Plugins/Piloto Studio/` 同 GUID 的共享文件（`UberFXSG` 等）仍从已入库的 Plugins 目录引用。你自己在包目录里建的 prefab 要先挪出来（预选放 `Assets/VFX/Shared/预选/`）。
- **材质定义**：
  - 墨 = Alpha 混合，三色映射 Lows 黑 / Middles 深灰 / Highs 白（白是描边与高光），Detail Noise 的扭曲通道给液体感；墨池、墨浪用它的扭曲网格（`yy_Shockwave_3_Distorted_still`、`Ink_Shockwave_1_Distorted`、`Cone_LongAOE`），墨溅、墨片、墨烟用它的序列帧与涂抹贴图（`yy_Liquid_Splat`、`Ink_flakes_1`、`Smoke_screen_cloud_sharp` 黑版）。
  - 光 = Add 混合：`FuzzAdd_Soft`（软雾）、`Flare` / `LightFlash_Soft`（爆闪 0.15–0.3 s）、`Flecks_Shiny_Additive`（火星）、`Ring_Halo_faded_add_soft`（光环）；白色软雾 `FuzzAlpha_Soft` 用 Alpha。
  - 符图 = Alpha soft，三色映射白 / 灰 / 黑，Detail Noise 的扭曲通道给轻微晃动，溶解通道做入场 / 消散。
  - 亮度顺序：爆闪 > 白雾核心 > 符图白底 > 火星 > 烟灰 > 墨黑。面积最大的是墨池与白雾，最亮的是爆闪。
- **模板 prefab**：（`VFX_Template.prefab` 已于 2026-10-30 删除，新插槽由 `SkillReleaseProto` 生成；下面是原设想）从包的 `Ying Yang Orb` + `Ying Yang Frontal` + `Ying Yang Buff` 派生的子系统集合，根上挂阶段控制器与六个阶段空槽；新特效从它起步，只换符图、尺寸与时序。**尺寸**：包按 1 单位 ≈ 1 米做，本项目角色 8 单位高、掌 60 × 90，所有尺寸乘 8（或根缩放 8）后再按碰撞盒贴合。
- **贴花层**：墨池、经文环都是包里的平面网格或面片（`RadialUV`、`High_Poly_flat_ring`），沿地面摆放，不改渲染器。
- **屏幕后期层**：施法者本地的后期脉冲仍用 Feel（`SkillFeelRouter`）；包的 `Ying Yang Screen Space FX`（全屏阴阳扭曲）只作参考，不接。
- **管线限制**：`UberFXSG` 的软粒子与 `Piloto Warp` 的扭曲需要深度 / 不透明纹理，本项目 Balanced / Performant 管线资产没开（见 [push-projectile-hands.md 第 1 节](push-projectile-hands.md#1-玩法事实特效必须服从数值由配表管本轮不改)）；这两项只在 High Fidelity 生效，其他管线自动退化为硬边与无扭曲，设计上不能依赖它们传达玩法信息。

## 8. 预算与档位（2026-10-28 晚修订）

平台只有 PC Steam。包的每个效果用 14–52 个粒子系统（空根与占位在内），本项目按它的做法拼装但收紧：

| 项 | 上限 |
|---|---|
| 单次施放全部特效 GPU | 目标 ≤ 0.3 ms，拼装小样实测后定，硬上限 0.5 ms |
| 同屏 6 个参赛者同时施放 | ≤ 1.5 ms |
| 每次施放实例化的粒子系统 | 原定 ≤ 10 个（含空根；包的 Orb 是 18 个）。**2026-10-31 已调整**：改为按插槽 prefab 计的上限，见表下"粒子系统上限" |
| 总粒子 | ≤ 600（包的做法是每个系统 1–3 个大面片，火星类几十个） |
| 大面片 overdraw | 白雾 / 墨池一类半屏面片每次施放 ≤ 6 张同屏 |
| 实时光 | 不用（包没有实时光，全靠白雾与爆闪） |
| 阴影 | 特效不投不接 |
| 残留层 | ≤ 1.5 s，纯视觉，不参与玩法 |

**粒子系统上限（2026-10-31，用户定"按实际放宽预算"）**：10 个原本是给如来神掌定的。2026-10-31 盘点 `SkillVfxDatabase` 的 14 个插槽，有 6 个超过 10 个；它们是用户按 Piloto 包效果手调定稿的，所以上限改成按插槽 prefab 计，取各自现在的实际数（含空根和不发射的系统），只防以后无意间变重。这些插槽的开销比原预算高，是用户预期之内、接受的（"这个特效本身 more cost 属于意料之内"），不算问题项；性能以后续实测为准，不以系统个数为准。减负时删掉不发射的空壳后，数会变少，上限不用跟着改。

| 插槽 | 粒子系统上限 | 性能实测 |
|---|---|---|
| `cast_anti` | 47 | 未测 |
| `palm_release` / `palm_release_anti` | 25 | 测过（如来神掌，[push-projectile-hands.md 第 15.1 节](push-projectile-hands.md)） |
| `cast` | 21 | 未测 |
| `reverse_hit` | 19 | 未测 |
| `accel_release` | 15 | 测过（神足通，同上第 15 节末） |
| 其余插槽（`slow_release` 现在正好 10，禅定缓流测过；其余为 0） | 10 | — |

一次施放还要加上通用前摇（`SkillExecutor` 每次施放都播 `cast` / `cast_anti`），所以按施放算：如来神掌 46、反噬 72、神足通 36、禅定缓流 31、转向反转 40 起（每个被反转的玩家再加一份 `reverse_hit` 的 19）。新增插槽默认 10，要超就在上表登记。测试：`SkillVfxTests.EverySlotStaysWithinItsParticleSystemCeilingAndUsesNoRealtimeLight`（14 个插槽全覆盖，也检查没有实时光）。

同次盘点的另两项待用户定：所有粒子系统的 `maxParticles` 都还是默认 1000，按 maxParticles 加总每个有粒子的插槽都过 600，按发射量粗估实际峰值每个插槽都不到 50；37 个渲染器勾着接收阴影、6 个勾着投影（`palm_bullet` 的根（已禁用）和 `Volume`、神足通的两条拖尾、一念光明的 `BlackCurtainTrail`）。这两项测试暂时只查 `palm_buff`。

档位：现有 `vfx_quality` 三档（Low / Medium / High，钩子 `VfxQuality.Current` 已有读者）对应优先级：Low 只留优先级 1 且发射量 40%，Medium 留 1–2 且 70%，High 全量。用户计划之后另开一档"特效质量"，越低省略越多层，规则相同。**距离分级**：离相机远于 `far_threshold_m` 的别人的特效自动省略 Accent、墨池与贴花，只保留携带玩法信息的主体与拖尾；本地玩家自己的永远全量。阈值与档位联动，开"特效质量"档时一并实现。

验证方法：编辑器内用现有的 `RaceMapRenderCost` 一类测量（同机同会话对比），Rendering Debugger 看 overdraw；数字写进各技能文档。

### 8.1 性能测量流程（2026-10-30，可对任一技能复用）

脚本在 `Tools/vfxperf/`（Python 3，直连 Unity MCP 的 HTTP 端，见 [Docs/unity-mcp.md](../unity-mcp.md)）；Unity 要先开着目标 worktree，MCP 服务在 `http://127.0.0.1:8080`，实例名用环境变量 `UMCP_INSTANCE` 覆盖（默认 `vfx-refactor@20ffbfa46a852d00`）。编辑器同一时间只能一个人驱动。

| 脚本 | 用法 | 做什么 |
|---|---|---|
| `vfxperf.py` | `python -X utf8 vfxperf.py <skillId> [次数=6] [间隔 s=0.2] [标签] [每帧 C# 旋钮]` | 没在比赛里就先 `racestart.py`（MainMenu → Play → `AITuningHarness.Begin`，1 玩家 + 2 AI，等发车）；装一个 `EditorApplication.update` 记录器，0–2 s 基线，然后本地玩家直接调技能 SO 的 `ExecuteServer` + `ExecuteObservers` 连放 N 次（如来神掌再反射调 `TryPushServer` 并清冷却）；最后一次后再记 9 s。输出 `vfxperf-<标签>.out`（每帧：时刻、未缩放帧时间 ms、在空中的墨手数、模拟中的流体数、显卡驱动占用 MB、流体显存 MB）并按阶段打印中位 / p95 / max |
| `palmperf24.py` | `python -X utf8 palmperf24.py <标签> [预算] [掌数=24] [旋钮]` | 如来神掌专用：24 掌齐飞 + 稳态每秒 6 掌，`预算` 每帧强写 `InkFluidVolume.simulationBudget`；旋钮是对每个 `InkFluidVolume f` 每帧执行的 C#（比如 `f.cellSize = 3f; f.resolution = new UnityEngine.Vector3Int(56, 56, 128);`，或关掉某个渲染器做对照） |
| `palmshot.py` | `python -X utf8 palmshot.py <标签> [旋钮]` | 出一掌，在 +0.4 / 0.9 / 1.5 / 2.4 s 截 Game 视图到 `shots/` |
| `recompile.py` | `python -X utf8 recompile.py` | 停 Play、`AssetDatabase.Refresh()`、等编译完、打印 Console 错误 |
| `umcp.py` / `shots.py` / `racestart.py` | 底层 | HTTP MCP 客户端（会话 id 存 `umcp_session.txt`，domain reload 后失效就删掉）；`code()` / `is_playing()`；开比赛 |

读数的规矩：同一台机器、同一会话内对比；基线（0 个特效）每次都记，比赛进程不同基线会差 1–2 ms；p95 比中位更能反映卡顿；"六人同时放"= 连放 6 次；如来神掌的最坏情况是每客户端 24 掌。做"对照"时用旋钮关掉某一层（渲染器 `enabled = false`、预算 0 等），差值就是该层的开销。实测记进各技能文档（如来神掌：[push-projectile-hands.md 第 15.1 节](push-projectile-hands.md)）。编辑器数字只能做相对比较，不是 build 的绝对值。

## 9. 验收与交付（2026-10-28 晚修订）

每个技能一次纸面确认（拆解表 + 方向），**两轮拼装小样**，然后做完全部阶段、反噬与档位省略，整体给用户看一次：Practice 模式施放正常与反噬各 3 次，录短视频或固定视角连拍（沿用渲染文档的 JPEG 对比做法），用户在 Editor 里看过后回"过 / 改哪一层"。交付清单：

- [ ] 拆解表（第 3.4 节模板）
- [ ] 特效资产 + 预览视频（含慢放）
- [ ] 各档位截图（优先级 1 / 2 / 3 分别验证）
- [ ] 性能数据：粒子数、overdraw、draw call、GPU 时间
- [ ] 命名符合第 6 节，无未用资源；废弃资产按 [CONTRIBUTING.md](../../CONTRIBUTING.md) 的运行时引用检查后删除

**拼装小样（取代前两版的"材质小样 / 基准小样"）**：
1. 第一轮：把包的 `Ying Yang Orb`、`Ying Yang Frontal`、`Ying Yang Buff` **原样**放进我们的亮赛道（根缩放 8），在施法者 / 侧 45° / 受害者三个机位各截一张，只回答一个问题：这个画风在亮沙地 + 蓝天前成不成立。用户回"成 / 不成"。
2. 第二轮：换上掌印符图，按 palm 文档第 4 节拼出飞行、出掌、蓄力三张静帧。用户回"过 / 改哪层"后再做完全部阶段。
3. 小样里出现任何彩色（印记除外）、没有白描边的黑块、金属高光，就算不过。

本轮不做受击反馈（用户 2026-10-28：先优化特效本身），命中层在拆解表里只作占位；音频项目里完全没有，本轮只预留事件钩子。

## 10. 技能顺序（暂定，模板完成后可调）

| 序 | 技能 | 现状 | 专题文档 |
|---|---|---|---|
| 1 | 如来神掌 `push_projectile_hands` | 单层点云手 + 8 层 Piloto 枪口焰，用户评为最难看 | [push-projectile-hands.md](push-projectile-hands.md) |
| 2 | 巨大化 `giant` | 完全没有特效 | |
| 3 | 转向反转 `reverseturn` | vfx_id 悬空，没有特效 | |
| 4 | 禅定缓流 `slowtrap` | 有拖尾区特效，材质槽有丢失引用 | |
| 5 | 一念光明 `blackcurtain` | 全屏遮挡 shader 可用 | |
| 6 | 神足通 `acceleration` | 用户认为看着还行，最后统一色板微调 | |

## 11. 未决问题

- `佛手Projectile.fbx`（1.7 MB，大概率高模）的面数与手势：实施时在 Unity 里确认，决定减面复用还是新建低模。
- 受击反馈（受害者屏幕边缘闪光、冲击、命中印记）的具体设计：用户决定后续再做。
- "特效质量"独立档位与 `vfx_quality` 的关系：用户计划另开一档，届时决定是替代还是并存。
- 音频：从零建表，键是技能 id 还是 Feel eventId，待用户决定（见 [design-tables/inventory-assets.md](../design-tables/inventory-assets.md)）。
- 配表清单里的三个悬空 vfx_id 与两个 Feel eventId 随第 4 节的统一一起清理，具体在实施时核对。
- 黑墨池在亮沙地上是否过重：第一轮拼装小样决定，上限是角色直径 × 3、≤ 蓄力时长 + 0.5 s。
- 包的 `UberFXSG` / `Piloto Warp` 在 Unity 2022.3.55 + URP 14.0.11 下的兼容性，以及 Balanced / Performant 无深度纹理时软粒子与扭曲的退化表现：导入时核对。

## 12. 术语

- **分层表 / 拆解表（Layer Sheet）**：第 3.4 节的表，每个技能必填，验收按它逐层对照。
- **阶段控制器（Phase Controller）**：第 4 节的共用运行时组件。
- **标杆特效（Benchmark Effect）**：12 个技能特效，作为全组的尺子。
- **玩法信息层**：携带"是什么、往哪、多大、来自谁"的元素，任何档位与距离都不省略。
- **反噬色板**：去金换紫红的替换规则。
- **符图（Symbol）**：画好的白底墨边图贴在面片上，技能的主体；照包里的阴阳符图。
- **墨池 / 墨浪 / 墨溅 / 墨片（Ink Pool / Wave / Splat / Flakes）**：包里四类液体形的黑墨元素，都带白描边或高光。
- **白雾 / 爆闪（Fuzz / Flare）**：包里的两类白光元素，承担亮度。
- **拼装小样（Assembly Stills）**：第 9 节的两轮静帧，全量搭建前的中间评审。
- **实体 / 外层墨 / 金粒 / 粒子皮 / 实心墨形**：前三版的已废弃概念，见 [push-projectile-hands.md 第 12–14 节](push-projectile-hands.md#14-2026-10-28-晚第三次改写完全按-pilotorealistic-ying-yang-vfx)。
