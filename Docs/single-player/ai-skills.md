# AI 技能使用设计

**A7 AI 适配已完成（2026-10-28，`feat/mechanics-update` ac3d5fe，PR #63）。** 共享技能规则以 [玩法深化](../gameplay/README.md) 为准（各表“状态”列区分用户已定和实现采用的建议），训练模式实测见 [训练模式验证记录](../gameplay/practice-verification.md)。本文的决策管线、难度映射和接入接缝已按 A7 之后的代码改写；2026-10-03 的设计讨论原文保留在各节里，涉及搓招的句子已标注退役。A7 之前“仍按旧规则”的前提清单和要改的清单被下表取代。

A7 的结果（代码位置按 ac3d5fe；“已定”是玩法深化里用户定的规则，“建议（实现采用）”是本次实现采用、用户没裁定的数值或做法）：

| 位置 | A7 之后 | 状态 |
|---|---|---|
| 施法路径 | `AISkillCaster.Decide` 选定槽位 → `AISkillCommitment`（Decided → 下一 tick 像真人按键一样请求 → WaitingConfirm → Resolved，没有按键步骤）→ `SkillExecutor.CastSlotServer`。和 owner RPC 同一个施法门：施法串行、本槽冷却、同招加价、统一反噬都在 `SkillExecutor` 里，AI 没有自己的规则 | 已定 |
| 已接受的施法 | 机会过期（`opportunity-expired`）不再撤回已被服务器接受、正在前摇的施法（真人也撤不回，撤回又不退冷却）；完赛、离开比赛阶段、`CastingEnabled` 关闭、开场交接、效果重置这些原因照旧取消待确认的普通施法；已判定的反噬谁也取消不了 | 建议（实现采用） |
| 拒绝原因 | 服务器拒绝请求时承诺取消，原因带上拒绝它的那道门：`rejected-pending` / `rejected-backfirelocked` / `rejected-frozen` / `rejected-cooldown`（`SkillExecutor.ResolveCastBlockServer`）；门是开的却被拒，是 AI 专用的入口检查（同类全局效果已可见），记 `rejected-global-visible` | 建议（实现采用） |
| 反噬风险 | （2026-10-31：反噬是该技能自己的 `_anti` 变体 + 5 秒惩罚；用户定先保持下面的统一代价，不把变体代价加回来，试玩后再调。）统一反噬后代价与技能无关：`u = v × 缘起倍率 − riskWeight × P(反噬) × backfireCost`，`P(反噬) ≥ stopProbability` 直接不放（所有技能一个阈值）。`AI/SkillTuning.backfireCost` 1；`AI/SkillDifficulty.stopProbability` Easy 1.01（从不停手）/ Normal 0.7 / Hard 0.45，沿用原 `severeStopProbability`；`backlashSeverity`、`mildStopProbability`、`severeSeverityThreshold` 删除 | 建议（实现采用） |
| `AISkillWorld` 快照 | `AISkillWorld.Classify`：反噬惩罚的 ×0.85 限速（`MaxSpeedPenaltyUntilTick`）记为 `Slowed` 并计入受损秒数；#4 突进（`BurstUntilTick`）记为 `Accelerating`；缩小不再算受损（统一反噬后没有缩小来源） | 建议（实现采用） |
| 缘起决策 | `AIYuanqiPlanner`，见下文“8. 缘起决策”。只用已实现的三对（#1、#2、#4），读 `YuanqiRegistry.Registered`（服务器按配装登记的同一份 `SkillDatabase.GetYuanqis()` + `YuanqiCatalog.ForLoadout`）。规则：先放会被消耗的那招，在它结束前至少 1 秒请求另一招，两招执念合计估风险（已定）；具体倍率、窗口、节奏是建议值 | 已定（规则）/ 建议（实现采用）（数值） |
| 掌形瞄准 | 仍经 `BuddahHandControl.InjectServerRotation`（180°/s，`Push.basic.rotationSpeedDegPerSec`）；发射改为直接 `InjectServerPush(true)`（不再注入搓招键），间隔按 `ServerPushCooldown`。瞄准容差 ±20° / ±12° / ±6° 没有重调：真人改鼠标瞄准后命中率的变化要用户试玩数据，见玩法深化 §8 | 待定（容差） |
| 反噬矩阵 | `AISkillEffectHarness` 的反噬用例按统一反噬：目标摆放和普通用例相同，样本多记 `BackfireLeft`、`MaxSpeedPenalty`；`Tools/ai/analyze_skill_matrix.py` 六个技能的反噬期望都是 `{"backfire"}`，不许出现技能自身效果，掌形反噬 0 发射，全局技能反噬不影响任何人，18 s 内恢复 | 已定 |
| 退役 | `ComboSkillInput`（类、prefab 组件、`SkillExecutor` 的订阅）、`Skill/ComboInput`、`Skill/ComboBinding`、`__enums__` 的 `ComboToken`、`AI/SkillDifficulty.keyMistakeProbability`、`AI/SkillTuning.keyMinSeconds / keyMaxSeconds / maxComboRetries / severeSeverityThreshold / scaleShrunkThreshold`、`AI/SkillOpportunity.backlashSeverity`、校验规则 `KeyMaxBelowComboWindow`、`AISkillTelemetry.Keys`、harness 结果里的 `ComboFailures / Keys` | 已定 |
| 新增参数 | `AI/SkillTuning`：`backfireCost` 1、`yuanqiSetupMultiplier` 1.2、`yuanqiFollowUpScore` 0.8、`yuanqiFollowUpMultiplier` 1.5、`yuanqiFollowUpTempoSeconds` 0.3、`yuanqiLateWindowSeconds` 0.6；`AI/Personality.yuanqiAffinity` 五个人格都是 1（0 = 不做缘起决策）。`Assets/Resources/AI/SkillPersonalities.json` 同步，只作无表时的兜底 | 建议（实现采用） |
| 验证 | 无窗口 EditMode `BuddahGo.Tests` 577/577（A7 前 559）；无窗口导表 0 错误；单人赛（5 个 AI，`AISkillRaceHarness`）见本文末尾“A7 单人赛验证” | — |

**实施授权与最新决定（2026-10-03）：** 本规格现已进入完整实现；下文“仅文档／暂不实现”描述的是设计讨论时点，已由用户本次开始指令覆盖。用户确认当前单机必定六人，五个人格全部出场、固定三槽，重开沿用。显示名仍暂用稳定 ID 对应的 P1–P5 行为占位名。普通档风险及距离、节奏等数值仍是配置化建议默认值；初始执念、恢复和反转增益保持现值。实际进度与验证以 [实施记录](ai-skill-implementation-2026-10-03.md) 为准。

状态：已记录本轮玩法决策，并于 2026-10-03 按运行时代码复核修订；按用户要求仅文档落盘，暂不实现。复核发现与依据见 [设计复核](ai-skill-design-review-2026-10-03.md)。

本次讨论以 PR #59 的 `e95c63c` 为源码基线，在独立工作树与 `docs/ai-skill-personalities` 分支进行。此处仅承载 AI 技能与人格设计；排名／结算／执念的 RacerId 迁移已在 feat/single-player-mode 提交为 `f9c56d8`（未合并 dev，本工作树不含），实现 AI 技能前须先以该提交为基线。旧文档中"始终使用单机原工作树"的历史安排不再约束本次文档讨论。

玩法术语见 [GLOSSARY](../../GLOSSARY.md)，五种人格的具体提案见 [人格设计](ai-personalities.md)，阶段与实际比赛验证边界见 [AI 测试计划](ai-testing.md)。

设计取向（用户 2026-10-03 要求）：**追求可实现的最优，不追求完美模拟人的行为。** AI 可以读取服务器权威状态，但要带延迟、噪声和感知受损；行为要让玩家看得懂，而不是让玩家误以为是真人。

## 已确认的本次决策

- **Q1，使用风格**：情境判断加概率。根据自身状态、赛道和附近对手识别有意义的施法机会，再独立概率决策；不要求复杂行为树或全面战术规划。
- **Q2，难度与公平性**：简单、普通、困难遵守玩家相同的技能配装、冷却、执念与反噬规则，仅调整判断时机、反应和失误等决策水平。不赋予额外技能资源或反噬豁免。
- **Q3，按难度考虑反噬风险**：困难模式中，当前反噬概率越高，施法决策越谨慎；简单模式不额外根据反噬风险收缩施法意愿，仍按情境机会与概率出手。普通模式的风险敏感程度用户尚未确认；下文给出建议默认值（风险权重 0.5）供调校，不视为已确认。
- **Q4，攻击对象**：所有对手一视同仁；真人身份不增加攻击倾向，AI 之间也按相同机会规则互相干扰。机会判断不改变技能本身的作用范围。单机下黑幕天然只影响真人屏幕，要满足 Q4 必须实现 AI 感知受损状态（见"后续控制器"）。
- **Q5，转向反转适应**：反转开始后先有短暂误操作，再适应反向控制；效果结束时也有重新适应过程。难度影响适应速度，困难 AI 恢复更快；不以直接改动物理或提前解除效果实现。实现位置是 AI 驾驶层的"认知转向符号"，见"后续控制器"。

## 运行时事实（设计必须服从的量级）

以下取自 prefab、技能资产与配表，技能、执念和输入部分已按 `0dbc551` 核对；数值可调，设计只依赖量级关系。

**执念与反噬**（`ObsessionFigure`，数值来自配表 `Skill/Obsession`，prefab 值只作兜底）：范围 0–1000，初始 0（2026-10-04 由用户从 1000 调为 0），基础恢复 10/s，追赶恢复 `0.01 × gap²`（gap 为与领跑者整场完成度之差，0–100），反噬概率为 Pmax 95%、k 0.008、中点 500 的 S 曲线。执念 300 时约 16%，500 时 47.5%，600 时约 66%，800 时约 87%。执念增益：加速／缓流／掌形 +300，巨大化／黑幕／反转 +200（反转原为 0，2026-10-03 起按平衡实验改为 200）。反噬是统一惩罚，另有同招加价，见顶部注。

由此得到的节奏：初始值为 0 时开局即可低风险施法（旧值 1000 时存在约 50–70 s 的开局静默期，术语表保留该词以解读旧记录）；一次 +300 需 30 s 基础恢复抵消，困难 AI 的真实节奏约每 30–45 s 一次、每场 6–9 次；冷却一好就重放同一技能还要叠同招加价，神足通（+300、冷却 5 s）连续重放到第 3 次请求时反噬约 56%，第 4 次约 91%（[玩法深化 §6.1](../gameplay/README.md)）。落后恢复只有在落后三分之一圈以上时才明显（gap 10 仅 +1/s）。

**技能**（配表 `Skill/Skill` 与各 `SkillEffect*`、`Push` 表，`0dbc551`；配表优先于资产）：

| 技能 ID | 冷却 | 效果与时长 | 作用对象 |
|---|---|---|---|
| `acceleration` | 5 | +50 前向力、+40 速度上限，3 s | 自身 |
| `slowtrap` | 8 | 挂在施法者身上的拖尾接触区 6 s；进入者减速 2 s，每目标 0.5 s 可重施 | 进入区域的任何其他参赛者 |
| `giant` | 8 | 4× 体型、3× 质量、3.5× 前向力，6 s | 自身，碰撞影响他人 |
| `push_projectile_hands` | 12 | 15 s 掌形 buff：每次推掌变为蓄力掌（蓄力 1 s、动作延迟 0.9 s、冷却 1 s、速度 600、寿命 3 s、冲量 500） | 发射方向上的任何参赛者 |
| `blackcurtain` | 16 | 展开 1 s、保持 10 s、淡出 2 s 的全屏遮挡；施法者可见边线 | 仅本地屏幕（单机 = 真人） |
| `reverseturn` | 12 | 场上所有其他参赛者转向反转 10 s，不分距离 | 全场 |

冷却从请求时刻加 1 s 前摇起算，反噬也照常进本行冷却。没有全局施法锁：一招执行后即可请求另一个冷却已好的槽，两个技能可以同时生效，成对的技能会触发缘起（AI 的缘起决策见下文）。掌形 buff 期间每次推掌都是一次蓄力发射。

**输入**：玩家单键施法（槽 0/1/2 = Q/E/R 与 1/2/3），手部朝向跟随光标的地面投影，鼠标左/右键推左/右手；按住 W 前进、松开停止推进（2026-10-30，W/↑ 推掌已删除）。AI 不按键：前进经 `AIRacerDriver.TryGetOverride` 的 `drive`，和真人的 W 写进同一个预测输入字段（`BuddahDriveInput.ResolveThrottle`），一直推进到完赛（已定，2026-10-30），驾驶规划 `BuddahMotionModel` 也按满推进力建模，所以这次改动不改变 AI 的驾驶。AI 不再搓招（A7）：决定后下一 tick 直接调用 `SkillExecutor.CastSlotServer`，再经 1 s 施法确认，从决定到效果出现约 1 s + 1 tick（决策前另有难度给定的反应延迟）。掌形 buff 期间 AI 的发射是直接的服务器推掌（`BuddahHandControl.InjectServerPush`，普通推掌冷却 0.4 s）。AI 的手部朝向经 `BuddahHandControl.InjectServerRotation` 以 180°/s 旋转，掌形发射方向 = 车头方向 + 手部 yaw。

**驾驶规划器**：`ThrustVectorPlanner` 不感知任何其他参赛者，没有车车碰撞建模；它直接读取真实转向符号 `FinalSteeringSign`。三档驾驶参数：简单 反应 8 tick + 抖动 5、次优失误 25%、角度噪声 10°；普通 3+3、10%、5°；困难 0、3%、2°（`Tools/ai/difficulty-*.json`）。

## 设计原则

1. **读权威、不读未来、不读隐藏。** AI 可读服务器上所有参赛者的位置、赛道进度、速度、状态标志（定身、反转、缩放、推掌保护等）与自己的执念／反噬概率；不读他人的冷却、反噬抽签结果或任何未来信息。
2. **延迟与噪声是难度，不是物理。** 观察到决定之间有反应延迟，决策有噪声，瞄准有容差，黑幕带来感知受损（A7 之前还有搓招的按键失误，已随搓招退役）。这些都只作用于 AI 的输入与决策，运动规则与技能效果不变。
3. **人格与难度正交。** 难度拥有风险权重、停手阈值、反应延迟、瞄准容差、适应时间、感知受损参数；人格拥有配装、槽位、价值权重、耐心阈值、局势偏置、目标偏好、掌形发射上限、缘起倾向（`yuanqiAffinity`）。两者相乘，不互相覆盖。
4. **机会要有有效期。** 每个机会谓词同时给出"预计还成立多久"，低于决定到生效的延迟就不算机会。
5. **不改驾驶、不改作用范围、不针对真人。** 人格不改线追人、不故意落后套利；全局技能仍是全局技能。
6. **可见即可用。** 已经在生效的全局效果（反转、黑幕）屏幕上看得见，AI 可以据此不叠加；这不属于隐藏信息。

## 决策管线（建议，可实现）

所有 AI 共用同一条管线，人格与难度只是参数。每一级都可以单独写 EditMode 测试。

### 1. 共享决策快照

服务器每隔一个固定间隔（建议 0.1–0.2 s，用 tick 计）为全部参赛者建一次快照，五个 AI 共用，不各自扫描：

- 每位参赛者：RacerId、赛道进度 `s`（沿样条距离）、圈数、速度、横向偏移、是否完赛／复活中／起步交接中、状态标志（`IsRooted`、`IsInvertTurnActive`、`IsSteeringSuppressed`、`ScaleMultiplier`、当前加速修饰符正负、是否处于缓流区）、上一帧速度（用于"刚减速"判断）。
- 赛道：从 `SplineRacingLine` 预计算的曲率数组 `κ(s)` 与"前方 T 秒直道比例"查表，比赛开始时算一次。
- 全局：当前可见的全局效果（反转／黑幕）剩余时间；每位 AI 最近一次施法时间（用于交错）。

进度来源复用 `PlayerProgressReporter`／`LeaderboardManager` 已有的样条进度，不另算一套。

### 2. 门控

以下任一不满足则本轮不决策：比赛进行中且未完赛；不在复活、起步交接、结果区；没有进行中的承诺（已决定或正在等待施法确认）；自己没有被定身；目标槽可施放（`SkillExecutor.IsSlotReadyServer`：没有待确认施法、不在反噬惩罚或禁锢中、本槽冷却已好）；上一轮决策距今 ≥ 决策间隔；反应延迟已过（观察时间 + 延迟 ≤ 现在）。

### 3. 技能机会谓词

每个技能一个纯函数，输入快照与自身状态，输出机会分 `o ∈ [0,1]`、目标（若有）与有效期。谓词定义见 [人格设计](ai-personalities.md) 的"共享机会谓词"。谓词只回答"现在放有没有意义"，不回答"放不放"。

### 4. 价值与风险

- 价值 `v = w[人格][技能] × o × 局势偏置[人格][领先/落后/车群]`。
- 风险 `r = P(反噬) × backfireCost`，`P(反噬)` 直接调用 `ObsessionFigure.GetBackfireProbabilityPercent(当前执念)`，就是真实规则。统一反噬（A7）后代价与技能无关：`AI/SkillTuning.backfireCost`，建议（实现采用）1。A7 之前每个技能一个严重度（加速 1.0、反转 0.9、巨大化 0.5、黑幕 0.4、掌形 0.3、缓流 0.1），已删除。
- 净值 `u = v × 缘起倍率 − λ[难度] × r`（缘起倍率见“8. 缘起决策”，普通施法为 1）。停手阈值只剩一个：`P(反噬) ≥ stopProbability[难度]` 直接不放（Easy 1.01 从不停手、Normal 0.7、Hard 0.45，沿用原来的严重技能阈值）。

### 5. 节奏抽样

不是"净值过阈值就立刻放"，而是每轮决策以 `p = 1 − exp(−max(0, u − θ[人格]) × Δt / τ[人格])` 的概率出手。`θ` 是耐心阈值，`τ` 是节奏常数。这样同一机会持续存在时出手时间自然分散，冷却一到就放和长期不放都不会发生；五个 AI 的决策相位按索引交错。缘起跟进例外：窗口不到一秒，抽样用 `yuanqiFollowUpTempoSeconds`（0.3 s）和耐心 0，见“8. 缘起决策”。

### 6. 动作承诺状态机

`Idle → Decided(槽, 截止时间) → WaitingConfirm → Resolved(普通/反噬) → Idle`（`AISkillCommitment`，A7 之后没有按键步骤）。

- Decided 后的下一 tick，`AISkillCaster.Request` 调用 `SkillExecutor.CastSlotServer(槽)`，像真人按下技能键；被接受就进入 WaitingConfirm，等服务器的 Executed（普通或反噬）、Cancelled 或 Rejected 事件。请求前到了截止时间（机会有效期）就取消，记 `opportunity-expired`。
- 等待确认期间不重新选择技能、不瞄准；复活（效果重置）、完赛、离开比赛阶段、开场交接、施法关闭时取消并撤回待确认的普通施法（冷却不退，和真人一样）；机会过期不撤回已接受的施法。已判定的反噬谁也撤不回，照常进入惩罚。
- 进入 Resolved 后记录结果（是否反噬）供日志与 A3 统计；人格不据此“学习”，只写入统计。
- A7 之前：Decided 之后是 Combo 阶段，按 0.2–0.3 s 间隔注入 W／上 搓招键，难度给每键失误率，失误重搓最多一次。随 `ComboSkillInput` 退役。

### 7. 后续控制器

三个与技能绑定的小控制器，各自是独立的 tick 级状态机：

- **掌形瞄准器**：buff 期间选取谓词给出的目标，计算所需 yaw，以 180°/s 的真实速率转手（`BuddahHandControl.InjectServerRotation`），进入难度给定的容差后在推掌冷却允许时直接推一次左手（`InjectServerPush(true)`，A7 之前是注入搓招键 W）；每次 buff 的发射上限由人格决定。瞄准进行中不做新的施法决策。
- **反转适应**（Q5）：`AIRacerDriver` 维护一个"认知转向符号"。真实符号翻转后，认知符号保持旧值 `adaptTicks[难度]` 再翻转；效果结束时同样延迟 `readaptTicks[难度]`。规划器用认知符号而不是 `FinalSteeringSign`。期间 AI 实际按错方向键，是真实误操作。
- **感知受损**（黑幕）：服务器为每个 AI 维护"视野受损至 tick"。黑幕由所有非施法者获得（`SkillPerceptionState.ApplyCurtain`；2026-10-26 起没有反噬黑幕），持续时间与屏幕效果一致。受损期间对驾驶 profile 施加临时乘数（前视时间、次优失误率、反应 tick）并延长技能决策的反应延迟。这样黑幕对 AI 与对真人都有意义，Q4 才成立。

### 8. 缘起决策（A7，建议（实现采用））

规则是已定的（玩法深化 §5.1、§7.1 A7）：没有顺序规则，但先放会被消耗的那招能先享受完它；第二招有 1 s 前摇，要在先放那招结束前至少 1 s 请求；两招的执念合计要按反噬概率估风险。`AIYuanqiPlanner` 是纯逻辑（EditMode 测试 `AIYuanqiPlannerTests`），`AISkillCaster.Decide` 每轮对每个槽问它：

- **配装有哪些对**：`Build(配装, YuanqiRegistry.Registered, 主效果时长)`。五个预设里守位型（缓流、加速、巨大化）有 #2 禅足冰封和 #4 金足破阵，车群型（巨大化、掌形、缓流）有 #1 金身巨掌，追猎、干扰、机会型没有。
- **先后**：只消耗一招的对（#4），被消耗的神足通先放；两招都消耗（#2）或都不消耗（#1）时，主效果长的先放（#2 缓流 6 s 先于神足通 3 s，#1 掌形 15 s 先于金身 6 s，重叠最长），相同则 `skillA` 先。
- **跟进**：某对的另一招在状态板上生效中、剩余 ≥ 前摇 + 1 tick，这个槽就是跟进候选，有效期 = 剩余时间（请求下一 tick 发出、前摇后执行，仍落在窗口里）。先放的那招会被这一对消耗时（#2、#4 的神足通）只在剩余 ≤ 前摇 + `yuanqiLateWindowSeconds`（1 + 0.6 s）时才跟进（晚打，先享受加速）；不消耗时（#1）立刻跟进。跟进候选的机会分取 `max(自身谓词, yuanqiFollowUpScore 0.8)`，价值乘 `yuanqiFollowUpMultiplier 1.5 × yuanqiAffinity`，抽样用 `yuanqiFollowUpTempoSeconds 0.3` 和耐心 0。
- **起手**：这个槽是某对先放的那招，且另一招的冷却会在它效果结束前至少 1 s 就绪（冷却剩余按 Accepted 事件估算）；再估第二招的反噬概率 `P2 = P(当前执念 + 第一招的 obsessionGain)`，`P2 < stopProbability` 才给起手加成（价值乘 `yuanqiSetupMultiplier 1.2 × yuanqiAffinity`），否则按普通施法评估（日志 `yuanqi setup-risk`）。
- **不做的**：不为没设计的对补内容；不改任何谓词的作用范围；`yuanqiAffinity` 0 就完全不做缘起决策。
- 日志：`Telemetry.Emit` kind `yuanqi`，reason `setup` / `follow-up` / `setup-risk`；单人赛 harness 按 racer 记缘起触发次数（`SkillExecutor.YuanqiObserved`）。

## 难度映射（建议默认值，待调校）

| 参数 | 简单 | 普通 | 困难 |
|---|---|---|---|
| 风险权重 λ | 0 | 0.5 | 1.0 |
| 停手阈值 `stopProbability` | 无（1.01） | P ≥70% | P ≥45%（A7 之前按严重度分两档，统一反噬后只剩一个） |
| 决策间隔 | 0.3 s | 0.25 s | 0.2 s |
| 观察→决定反应延迟 | 0.6–1.0 s | 0.3–0.5 s | 0.1–0.2 s |
| 瞄准容差 | ±20° | ±12° | ±6° |
| 反转适应 / 回切 | 2.0 s / 1.0 s | 1.0 s / 0.6 s | 0.4 s / 0.3 s |
| 黑幕受损（前视×、失误+、反应+tick） | ×0.5、+0.20、+10 | ×0.6、+0.10、+6 | ×0.75、+0.05、+3 |

A7 之前的“每键搓招失误率”15% / 6% / 1% 已随搓招退役。简单档 λ=0 对应 Q3"不额外顾虑反噬"；简单 AI 连续施放会很快把执念推高并大量反噬，这是共享规则的自然结果，不另加保护。普通档的 0.5 是对未定项的建议默认，不是用户确认值。

## 优化层

决策成本必须远低于现有驾驶规划（五 AI 中位 5.8–7.2 ms/帧，Development 构建）。目标：五个 AI 的技能决策合计 ≤0.2 ms/帧，用 `AI.Skill` 剖析标记按帧汇总，和 `AI.Plan` 分开报告。

- **一次快照、多处使用。** 快照 O(N)，N ≤6；谓词只做进度差、曲率查表与标志判断，不做物理查询、不调用 `FindObjectsByType`（`Skill_ReverseTurn.ExecuteServer` 每次施法调用一次可接受，决策层不调用）。
- **交错。** 五个 AI 的决策 tick 相位错开，同一帧最多一个 AI 决策；快照更新与决策不在同一帧。
- **预计算。** 曲率与直道比例沿赛车线预计算为数组，比赛开始时一次；目标 yaw 的计算只在瞄准器激活时进行。
- **零分配。** 快照、候选、日志结构复用数组与结构体；每个 AI 的 `System.Random` 由 `NoiseSeed` 派生并固定，保证可复现。
- **tick 定时。** 决策间隔、请求时刻、瞄准、适应、感知受损全部用服务器 tick，为 S7 Pause 预留；不新增 `Time.time` 依赖。
- **退化路径。** 若预算超出，先拉长决策间隔与快照间隔，不降低门控与合法性校验，不绕过施法门。
- **测量方法。** `AITuningHarness --ai-profiles` 增加施法开关与同场对照（施法关／开），报告每帧 `AI.Skill`、每 AI 施法次数、反噬次数、取消次数、缘起触发次数、按情境的分布；无观察者时不产生分配。
- **玩法层面的浪费控制。** 有效期不足不放；可见全局效果生效中不叠加同类；自己处于定身／复活／起步交接时不开始新决策（已接受的施法照常完成，与玩家一致）；掌形瞄准进行中不决策。

## 接入接缝

| 位置 | 现状 | 需要的服务器等价路径 |
|---|---|---|
| `ComboSkillInput` | 已删除（A7）。2026-10-03 时曾加服务器侧按键注入入口给 AI 搓招 | 不再需要：AI 直接调用 `SkillExecutor.CastSlotServer` |
| `BuddahHandControl` | 推掌与朝向都经 owner 的 ServerRpc | 服务器本地的推掌与 yaw 设置路径，复用同一冷却与投射物生成；AI 手部动作在 host 画面可见 |
| `SkillExecutor` | `CastSlotServerRpc(RequireOwnership)`；`Apply*ToOwner` 在 `Owner == null` 时跳过 TargetRpc | 服务器本地的 `CastSlotServer` 入口（AI 现在直接调用它），沿用 `TryResolveCast → ResolveCastVariant → QueueCast`，不放宽真人 RPC；`ResolveCastBlockServer` 只读报告哪道门会拒绝 |
| `SkillLoadout` | AI 当前退回默认配装 | `SpawnSoloAI` 按人格 ID 解析三槽，经 `LoadoutRules` 对 Selectable 池校验后 `SetSlotsServer` |
| `AIRacerDriver` / 规划器 | 只有转向，读真实转向符号 | 认知转向符号、感知受损乘数、技能决策器挂接点 |
| 复活与清理 | `ResetActiveSkillEffectsServer` 依赖 Owner 连接；motor 事件已调用 `ResetActiveSkillEffectsForOwner` | 核对无 owner 时服务器侧修饰符、掌形 buff、缓流区、承诺状态机都被取消 |
| 身份与恢复 | 本基线按 OwnerId | 以 `f9c56d8` 为基线，确认 AI 的排名、追赶恢复与结算 |

## 实施与验证草案

以下是后续实现草案，不是本轮执行任务。用户再次要求开始实现后的顺序：先合入 `f9c56d8` 基线 → 人格配装解析与校验 → 共享快照与谓词（EditMode）→ 服务器按键注入与承诺状态机 → 公共权威施法 → 掌形瞄准器 → 反转适应与感知受损 → 五 AI 实战与可试玩构建。

每次决策写一条 Verbose 日志：tick、RacerId、人格 ID、难度、候选技能与机会分、目标、执念与 P(反噬)、净值、是否出手、未出手原因（门控／阈值／抽样／有效期）。A3 报告按人格汇总：施法次数、反噬次数、取消次数、缘起触发次数、按情境（领先／落后／车群）与目标状态的分布、`AI.Skill` 耗时（搓招失败次数已随搓招退役）。

验证分别记录：规则与独立状态测试；六种技能普通／反噬的适用对象与实际效果；受扰后恢复和生命周期清理；五个 AI 带技能的自然比赛及实际完赛／DNF 原因；可试玩构建。施法请求、接受、实际执行、投射物命中和效果恢复分开记录。尚未发生的用户试玩保持未验证。

## A7 单人赛验证（2026-10-28）

`AISkillRaceHarness.Begin(dir, SoloDifficulty.Normal)`，编辑器 Play 模式从 MainMenu 启动，五个产品 AI、真人不操作；原始数据在 `Logs/a7/solo-1`（`Logs/` 不入库），下面只记汇总。它是 A3 式的自然比赛观察，不是用户试玩，手感和数值一律未验证。

| 看的点 | 结果 |
|---|---|
| 比赛能跑完并结算 | 结束原因 `natural-race-results`，0 运行错误；trickster 316.6 s、opportunist 327.2 s 自然完赛，其余三个 AI 在完赛倒计时截止时在第 3 圈；真人停在起点（`stalled-at-cutoff`） |
| AI 不再叠技能 | 63 次请求全部被接受、0 次 Rejected；没有一次请求发生在前一招待确认期间；两招最短执行间隔 1.1 s（前一招执行后立刻请求下一招，是缘起跟进） |
| 施法频率 | keeper 10、hunter 11、brawler 11、trickster 13、opportunist 17 次执行（1.8–3.1 次/分钟）；黑幕 17 次（trickster 10、opportunist 7，约每 20 s 一次全屏黑幕）要用户判断是否过频 |
| 反噬会触发并按新惩罚执行 | 7 次反噬（keeper 1、hunter 1、brawler 2、opportunist 3；trickster 0），掷骰时执念 296–618、概率 16%–68%；每次反噬后 10 s 内三个槽的候选全部是 `cooldown-or-lock`，下一次执行都在 ≥ 11.1 s 后 |
| AI 能触发缘起 | 8 次：keeper #2 禅足冰封 ×4（缓流先、神足通后），brawler #1 金身巨掌 ×4；#4 金足破阵 0 次：keeper 这场一次都没放金身（金身谓词要 35 m 内有接触，权重 0.8），起手加成（`yuanqi setup` 1705 次候选）大多落在缓流和神足通上 |
| 风险模型 | `risk-stop` 候选 276 次（Normal 停手 70%）；最高概率的出手是 hunter 在执念 607（67%）放反转并反噬 |
| 性能 | `AI.Skill` 每帧中位 0.034 ms、P95 0.149 ms（五个 AI 合计，含观察器），帧时中位 16.3 ms |
| 未验证 | 真人参与时的体验、黑幕频率是否合适、#2 禁锢对车群是否过分（玩法深化 §9 的受害者保护）、#4 在自然比赛里能否触发、Easy / Hard 两档、联机 |

## A8 单人赛验证（2026-10-30，W 推进 + 完全物理交接）

AI 的推进开关一直为开（已定），代码只在 `AIRacerDriver.TryGetOverride` 补了注释。`AISkillRaceHarness` Normal 一场（2026-10-30，`Logs/a8/solo-20261030-231507`，不入库；W 推进和完全物理交接都已生效）：自然结算（`natural-race-results`），0 运行错误；trickster 320.5 s 自然完赛，其余四个 AI 在完赛倒计时截止时在第 3 圈；单圈 102–167 s（keeper 126.1 / 102.2，hunter 110.0 / 124.5，brawler 167.3 / 104.0，trickster 107.7 / 109.2 / 103.6，opportunist 129.0 / 104.2），和 A7 那场（101–139 s）同一量级，brawler 第一圈 167 s 偏慢；每个 AI 执行 10–18 次施法，71 次请求接受 69 次（opportunist 2 次被 A7 的 `rejected-global-visible` 挡下：已有黑幕可见时不再放黑幕），反噬 14 次，缘起 8 次（keeper #4 ×1、#2 ×2，brawler #1 ×5）。真人不按键：交接时 59.97 m/s，从第一个 tick 起只按摩擦减速（1.08 s 后 57.38 m/s，约 2.4 m/s²），之后被 AI 蓄力掌打中 10 次，靠冲量加滑行在 235.6 s 过了第 1 圈，没有任何 W 式的持续加速。同一天更早的一场（改交接之前）在 68 s 被手动停止（`operator-disabled`），期间编辑器里按过一次真实的 W，不作数。

2026-10-31 PR #63 中途 review 与退役清理之后，`AISkillRaceHarness` Normal 再跑一场（`Logs/review/solo-20261031`，不入库）：自然结算（`natural-race-results`）；trickster 313.4 s、keeper 322.1 s、opportunist 322.4 s、hunter 326.5 s 自然完赛，brawler 在完赛倒计时截止时在第 3 圈；单圈 93.4–138.8 s；72 次请求接受 71 次（1 次 `rejected-global-visible`），执行 71 次（每个 AI 13–17 次），反噬 12 次，缘起 8 次（brawler #1 ×4，keeper #2 ×3、#4 ×1）。控制台只有结算演出传送完赛车辆时的“kinematic 刚体不能设速度”报错，dev 上就有，与本分支无关。

2026-10-31 反噬变体恢复之后（反噬 = 统一惩罚 + 必定变体），`AISkillRaceHarness` Normal 再跑一场（`Logs/review/solo-anti-20261031`，不入库）：自然结算；trickster 335.1 s、opportunist 343.7 s 自然完赛，其余三个在完赛倒计时截止时在第 3 圈；单圈 99.4–174.1 s，比变体恢复前那场（93.4–138.8 s）慢，反噬变体会让施法者自己减速、定身、缩小或反转；68 次请求全部接受，执行 67 次、取消 1 次，反噬 13 次（每次都放出变体），缘起 5 次（brawler #1 ×3，keeper #2 ×2）。AI 风险仍按统一 `backfireCost`（用户 2026-10-31：先保持）。控制台只有结算演出传送完赛车辆时的 kinematic 报错（dev 已有）。

## 后续交接与未定项

- 本轮明确要求"先进行文档落盘，暂时不用直接开始 implement"。不自动进入代码、资源修改、Unity 实播、测试或构建；等待用户后续开始指令。
- 需用户决定：`reverseturn` 执念增益是否保留 200（原为 0，2026-10-03 起按平衡实验改为 200）；黑幕的 AI 感知受损是否纳入 S5 必要范围；五个预设配装、槽位与命名。
- 已定：追赶恢复系数本次不改（`Skill/Obsession.catchupRecoveryA` 0.01，2026-10-25 用户已定，以后试玩再在配表调，见 [玩法深化 §6.1](../gameplay/README.md)）；初始执念 2026-10-04 定为 0；Rematch 沿用人格与配装（2026-10-03，“重开沿用”，见本文顶部）。
- 上表所有数值为建议默认值，进入调校后按实测修正，不把任何具体数值记为已确认。
- 保留现有排名／结算提交、私有工具和原始验证证据。文档记录不代表这些改动已合并或已通过验证。

本文记录设计进展，不把提案、历史测试或未合并工作计为本次实现与验证。
