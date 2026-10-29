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
- 贴图 `T_FX_<描述>_<类型>`（类型：Mask / Noise / Flow / Flipbook / Glyph / Packed）；材质 `Mat_FX_<技能>_<元素>`；shader `FX/<功能层>`；prefab `VFX_<技能>`、`VFX_<技能>_Anti`；vfx_id 小写蛇形 `palm`、`palm_anti`。
- 资产目录：`Assets/VFX/<技能中文名>/` 沿用现有目录；共用资源放 `Assets/VFX/Shared/`（贴图、材质、模板 prefab），shader 放 `Assets/Rendering/Shaders/FX/`（与已有的 `BuddahGo/Lit` 同处）。

## 7. Shader、贴图与模板（2026-10-28 晚第三次改写）

- **路线：直接用包的 shader、材质、贴图、网格，本项目只拼装。** 包的全部材质走一个 Shader Graph `UberFXSG`（Main Texture + Alpha Override 遮罩 + Detail Noise 按通道做乘 / 加 / 溶解 / 扭曲 / 顶点偏移 + **Color Ramping 三色映射** + Fresnel + 软粒子 + UV 平移），屏幕扭曲走 `Piloto Warp`。黑白风格就是三色映射设成黑 / 深灰 / 白（墨）或白 / 灰 / 黑（光）。之前"Shader Graph 退出特效制作、手写 HLSL 族"的决定作废：手写复刻它的三色映射没有意义，已写的 `FX/*` HLSL 保留文件但不再用于新元素。VFX Graph 仍不用。
- **素材**：包按 Single Entity 授权在本项目内使用，贴图、材质、网格、shader 都可以直接引用，不限于它的 prefab；导入到 `Assets/Piloto Studio/`（保持原 GUID，便于日后升级），不改包内文件，本项目的材质副本放 `Assets/VFX/Shared/Materials/`。本项目**只新建三张贴图**：掌印符图 `T_FX_PalmSymbol`（白底、墨边晕染、软边，照 `yingyang_1` 的做法画，2048²）、反噬破印 `T_FX_PalmSymbol_Broken`（黑底白边、带裂口）、朱砂印 `T_FX_PalmSeal`（印章式硬边）；经文环沿用 Noto Serif CJK 烘的真实《心经》（已有）。**不再程序化生成任何形状贴图**，噪声与渐变也直接用包里的。
- **材质定义**：
  - 墨 = Alpha 混合，三色映射 Lows 黑 / Middles 深灰 / Highs 白（白是描边与高光），Detail Noise 的扭曲通道给液体感；墨池、墨浪用它的扭曲网格（`yy_Shockwave_3_Distorted_still`、`Ink_Shockwave_1_Distorted`、`Cone_LongAOE`），墨溅、墨片、墨烟用它的序列帧与涂抹贴图（`yy_Liquid_Splat`、`Ink_flakes_1`、`Smoke_screen_cloud_sharp` 黑版）。
  - 光 = Add 混合：`FuzzAdd_Soft`（软雾）、`Flare` / `LightFlash_Soft`（爆闪 0.15–0.3 s）、`Flecks_Shiny_Additive`（火星）、`Ring_Halo_faded_add_soft`（光环）；白色软雾 `FuzzAlpha_Soft` 用 Alpha。
  - 符图 = Alpha soft，三色映射白 / 灰 / 黑，Detail Noise 的扭曲通道给轻微晃动，溶解通道做入场 / 消散。
  - 亮度顺序：爆闪 > 白雾核心 > 符图白底 > 火星 > 烟灰 > 墨黑。面积最大的是墨池与白雾，最亮的是爆闪。
- **模板 prefab**：`Assets/VFX/Shared/VFX_Template.prefab` 改为从包的 `Ying Yang Orb` + `Ying Yang Frontal` + `Ying Yang Buff` 派生的子系统集合，根上挂阶段控制器与六个阶段空槽；新特效从它起步，只换符图、尺寸与时序。**尺寸**：包按 1 单位 ≈ 1 米做，本项目角色 8 单位高、掌 60 × 90，所有尺寸乘 8（或根缩放 8）后再按碰撞盒贴合。
- **贴花层**：墨池、经文环都是包里的平面网格或面片（`RadialUV`、`High_Poly_flat_ring`），沿地面摆放，不改渲染器。
- **屏幕后期层**：施法者本地的后期脉冲仍用 Feel（`SkillFeelRouter`）；包的 `Ying Yang Screen Space FX`（全屏阴阳扭曲）只作参考，不接。
- **管线限制**：`UberFXSG` 的软粒子与 `Piloto Warp` 的扭曲需要深度 / 不透明纹理，本项目 Balanced / Performant 管线资产没开（见 [push-projectile-hands.md 第 1 节](push-projectile-hands.md#1-玩法事实特效必须服从数值由配表管本轮不改)）；这两项只在 High Fidelity 生效，其他管线自动退化为硬边与无扭曲，设计上不能依赖它们传达玩法信息。

## 8. 预算与档位（2026-10-28 晚修订）

平台只有 PC Steam。包的每个效果用 14–52 个粒子系统（空根与占位在内），本项目按它的做法拼装但收紧：

| 项 | 上限 |
|---|---|
| 单次施放全部特效 GPU | 目标 ≤ 0.3 ms，拼装小样实测后定，硬上限 0.5 ms |
| 同屏 6 个参赛者同时施放 | ≤ 1.5 ms |
| 每次施放实例化的粒子系统 | ≤ 10 个（含空根；包的 Orb 是 18 个） |
| 总粒子 | ≤ 600（包的做法是每个系统 1–3 个大面片，火星类几十个） |
| 大面片 overdraw | 白雾 / 墨池一类半屏面片每次施放 ≤ 6 张同屏 |
| 实时光 | 不用（包没有实时光，全靠白雾与爆闪） |
| 阴影 | 特效不投不接 |
| 残留层 | ≤ 1.5 s，纯视觉，不参与玩法 |

档位：现有 `vfx_quality` 三档（Low / Medium / High，钩子 `VfxQuality.Current` 已有读者）对应优先级：Low 只留优先级 1 且发射量 40%，Medium 留 1–2 且 70%，High 全量。用户计划之后另开一档"特效质量"，越低省略越多层，规则相同。**距离分级**：离相机远于 `far_threshold_m` 的别人的特效自动省略 Accent、墨池与贴花，只保留携带玩法信息的主体与拖尾；本地玩家自己的永远全量。阈值与档位联动，开"特效质量"档时一并实现。

验证方法：编辑器内用现有的 `RaceMapRenderCost` 一类测量（同机同会话对比），Rendering Debugger 看 overdraw；数字写进各技能文档。

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
