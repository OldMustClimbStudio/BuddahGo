# 玩法深化：缘起、执念与操作改版

日期 2026-10-25。状态（`feat/mechanics-update`，PR #63，2026-10-31 中途 review 与退役清理后）：阶段 A 的 A1–A8、阶段 B 和阶段 C 第一、二级已完成：训练模式自动验证 2026-10-26 13/13 通过，A8 之后 2026-10-30 重跑 14/14（[训练模式验证记录](practice-verification.md)），2026-10-28 A7 AI 适配完成并在单人赛 harness 里跑过（见 §7.1 A7 的验证一行）；阶段 C 第三级（联机）和用户试玩还没做。2026-10-25/26 用户答复了第 8 节的大部分待定项，结论已改成“已定”。2026-10-30 前进改为按住 W 推进，AI 同步适配（§3、§7.1 A8）。2026-10-31 用户推翻“六种 `_anti` 变体由统一惩罚取代”：反噬改为统一惩罚 + 必定放出该技能自己的 `_anti` 变体，变体按 dev 原样恢复（§6.2）。

标记：“已定”是用户在讨论中确认的规则；“建议”是待用户确认的提案；“建议（实现采用）”是用户没有裁定、代码已按它实现的做法；“待确认”是还要问用户的地方。技能数值按配表现值（`Assets/Design/Tables/_Export`）；修改顺序是先改机制、再对接配表，见第 7 节。代码层面的构筑见 [缘起架构设计](yuanqi-architecture.md)。

## 1. 为什么改

改版前（dev 5f91e51）的代码与配表让玩法深度受这几处限制：

- **同一时间只有一个技能。** 施法锁是全槽共享的，时长约等于效果时长；冷却只比锁多 2 秒（加速 5/3、缓流 8/6、反转 12/10、黑幕 16/14）。三个槽的冷却实际不用管理，也不存在自己技能之间的配合。
- **执念只是惩罚。** 唯一的决策是“现在放还是等 20–30 秒”。六种反噬的代价差别很大（缓流反噬定身 1 秒，反转反噬自反 10 秒），执念价格却几乎相同。
- **搓招和推掌共用 W/↑。** 两只手的冷却是分开的，近身时左右交替推掌（间隔 < 0.35 秒），第三下就凑成 `W↑W` 或 `↑W↑`，误放出槽 0/1 的技能（`ComboSkillInput.FindExactMatchOnSuffix` 按后缀匹配）。
- **瞄准卡手。** 手部朝向靠 ←/→ 以 180°/s 转动，蓄力掌很难打准。

还有几处不在本次范围内，见第 9 节。

## 2. 核心循环

跑线 → 读局势、选组合 → 叠放技能触发缘起 → 承担执念风险（反噬 = 10 秒哑火）。

深度从“手指”转移到“决策”：单招输入变简单，复杂度留给“放哪几招、按什么顺序、什么时候兑现”。

## 3. 操作

| 项 | 规则 | 状态 |
|---|---|---|
| 技能释放 | 取消搓招，单键释放 | 已定 |
| 前进 | 不再自动前进：按住 W 向前推进，松开就停止推进。松开只撤掉推进力，不刹车，靠地面摩擦自然减速；推进力和限速沿用配表（`Movement.forwardForce` 50、`maxSpeed` 80 m/s），没有新参数 | 已定（2026-10-30） |
| 手部朝向 | 鼠标控制；转手有速度上限，鼠标拉动带拖拽感，避免左右晃鼠标时手部抽搐 | 已定 |
| 起跑交接 | 完全物理丢出去：GO 交接那一 tick 把开场的速度（RaceMap 上约 60 m/s）一次性交给刚体，之后不再锁速度、不混合、不屏蔽输入，摩擦立刻生效，W / A/D 立刻可用。什么都不按就带着这个速度沿惯性滑行，靠地面摩擦慢慢停下。`Movement.launchInheritSeconds`、`launchBlendSeconds` 由 0.2 / 0.32 秒改为 0，交接后屏蔽转向的 0.15 秒改为 0 | 已定（2026-10-30） |
| 键位 | 左手 A/D 转向、W 前进；右手鼠标瞄准，鼠标左/右键推掌（左键左手、右键右手）；技能槽 0/1/2 同时绑 Q/E/R 和 1/2/3。W/↑ 推掌已删除（2026-10-30），见本节末尾 | 已定 |
| 瞄准方式 | 光标投到地面取方向（赛车镜头在动，比鼠标横移更稳） | 已定 |
| 转手上限 | 先沿用 `Push.basic.rotationSpeedDegPerSec` 180°/s 加平滑，试玩再调；命中率上升后蓄力掌冲量和 buff 时长需要重调 | 已定 |

鼠标只换了输入来源：手部 yaw 仍由 owner 经 `BuddahHandControl.TrySendYawToServer` 同步给服务器，网络结构没变。没有鼠标、或光标射线打不到瞄准平面时，退回 ←/→ 转手（`HandRotation`）。鼠标只适用于 PC，以后支持手柄改用右摇杆。

前进的开关在预测输入里，不是客户端表现：owner 每个 tick 读 W（`PlayerBuddahInputSource.GetThrottle`，按住为 1、松开为 0），`BuddahDriveInput.ResolveThrottle` 把它写进 `BuddahPredictedInputData.Throttle`，服务器按同一份输入模拟，预测和 reconcile 的路径都没变。AI 和训练木桩不走按键，经 `ISteeringOverride` 的 `drive` 写同一个字段：AI 一直推进到完赛（已定，2026-10-30），木桩恒为 0。其余状态沿用原规则（已定，2026-10-30）：倒计时期间移动门关闭，W 无效；起跑交接见上表（完全物理，交接后立刻读输入）；复活后速度清零，要按住 W 才走；禁锢时推进力和限速归零，W 无效；逆姻缘只反转左右，W 仍是前进；UI 挡输入时 W 和 A/D 一起失效。

`HandPush` 动作是 PassThrough 类型：每个键单独上报，只在按下时推掌。按住一个键再按另一个键，两只手都会推（Button 类型会做冲突裁决，第二个键会被吞掉）。光标停在可交互的 UI 上时，点击不推掌。

**废弃内容：W/↑ 推掌已删除（2026-10-30）。** 2026-10-25 起，W/↑ 推掌（W 左手、↑ 右手）按用户要求作为废弃内容暂留（“留尾巴”），主键位改成鼠标左/右键。2026-10-30 W 改为前进键，用户决定 W 和 ↑ 两条推掌一起删除：`InputSystem_Actions` 的 `HandPush` 只剩鼠标左/右键，`BuddahHandControl.TryResolvePushHand` 的 W/↑ 分支和标着“废弃内容”的注释已删（`ComboSkillInput` 里的那处已随 A7 删除），配表 `Global/InputBinding` 删掉这两行。↑ 现在不绑定任何动作。

## 4. 施法节奏

| 项 | 规则 | 状态 |
|---|---|---|
| 全局施法锁 | 不同技能之间取消，可以同时生效 | 已定 |
| 确认前摇 | 保留 1 秒及其特效；它是全场可见的读招窗口。缘起在第二招执行时判定，所以第二招要提前 1 秒请求（见 5.1） | 已定 |
| 单技能冷却 | 保留。去掉全局锁后，三个槽各自的冷却才第一次需要管理 | 已定 |
| 施法串行 | 同一时间只允许一个待确认的施法，后一招要等前一招执行、执念入账后才能请求；前摇期间的请求直接拒绝 | 已定（见 7.1 的 A3） |

## 5. 缘起（技能组合）

两个技能互相配合产生的效果叫**缘起**（已定）。本节记录机制规则和接入方式；每一对缘起的具体内容以 [缘起技能设计](yuanqi.md) 为准（最初的原文在 Notion“Combo 缘起机制”页）。

### 5.1 规则

| 项 | 规则 | 状态 |
|---|---|---|
| 触发 | 一个技能正在生效时，另一个与它互有缘起的技能也进入生效（前摇结束、执行的那一刻），两招重叠的一瞬间触发 | 已定 |
| 顺序 | 没有顺序规则，先放哪招都能触发 | 已定 |
| 效果与收尾 | 取决于每个缘起自己的描述：触发时出现什么效果（瞬发一次，或持续一段）；之后哪招被消耗（剩余效果当场结束），哪招照常持续到自己结束。描述里没写消耗的，两招都照常持续 | 已定 |
| 全局技能的“生效中” | 一念光明和逆姻缘的效果不在施法者身上，按施法者自己的效果窗口算：一念光明为黑幕保持期 10 秒（展开和淡出不算），逆姻缘为反转的 10 秒 | 已定 |
| 参数写法 | 描述里的单个字母（N、M）是可调参数，每段描述的变量各自独立；阶段 B 每个缘起在配表里有自己的一组参数列 | 已定 |
| 覆盖 | 按需加入：合适的对才做，不合适的就不做，不追求 15 对全覆盖。一套配装可能有 0–3 个缘起，选技能界面显示本套配装能触发哪些 | 已定 |
| 三连 | 搁置，见 5.3 | 已定 |
| 触发次数 | 一次重叠只触发一次；同一对要再触发，必须形成新的重叠（例如其中一招结束后重新施放） | 已定 |
| 被消耗的技能 | 冷却和执念在施放时已经付过，被消耗不返还 | 已定 |
| 三招同时生效 | 设计上从根本避免三个技能两两互有效果。两个缘起都是持续型、又很可能同时出现时，允许它们同时生效；这类情况在设计各对内容时控制 | 已定 |

**先后带来的决策（已定）。** 没有顺序规则，但“谁被消耗”让先后有了差别。以神足通 + 法相金身为例（神足通被消耗，金身持续到结束）：

- 先放金身、再放神足通：神足通一生效就触发缘起并被消耗，只换来一次突进。
- 先放神足通、再放金身：先享受神足通的加速，在它快结束时让金身生效，加速和缘起都吃到。

重叠在第二招**执行**时判定，而第二招有 1 秒前摇，所以第二招要在先放那招结束前至少 1 秒请求。按神足通现值 3 秒算，神足通生效后 2 秒内请求金身。

两招能同时生效的最长时间（施法串行 + 1 秒前摇，先放长的那招，配表现值）：

| 组合 | 最长重叠 |
|---|---|
| 含神足通的 5 对 | 3 秒 |
| 缓流 + 金身 | 5 秒 |
| 缓流 / 金身 × 光明 / 神掌 / 逆姻缘 | 6 秒 |
| 光明 + 逆姻缘 | 9 秒 |
| 光明 / 逆姻缘 + 神掌 | 10 秒 |

### 5.2 粘合：缘起怎么接进技能

缘起是两招之间的“粘合”（已定）：

- **开局即知。** 配装在比赛开始前就固定，系统在生成时就知道每个玩家能触发哪些缘起。按配装预先登记本场可能出现的缘起（类似 `#define` 的预先编排），配装里没有的组合不检测。
- **交给第三方。** 检测到两招同时生效时，把执行交给这一对的缘起模块。缘起模块是两招之外的第三方，可以是独立的类，也可以按架构再拆。
- **原技能复用并兼容。** 两招的原有代码照常复用；涉及缘起的技能要能接受第三方中途消耗或改写它（例如神足通被消耗时，停止加速并清理运动修饰）。

实现（建议（实现采用）；目录、依赖方向、句柄、配表扩展与 #2 的构筑见 [缘起架构设计](yuanqi-architecture.md)）：

| 部件 | 职责 |
|---|---|
| 技能状态板 `SkillStateBoard`（每个 Buddah 一个，服务器独有，不同步） | 每次正常执行后由 `SkillExecutor` 统一登记一条：技能 id、槽位、生效窗口、句柄。窗口和句柄由技能自己的 `SkillAction` 提供，实时参数（如当前体型）从句柄读，别人只读。到期、被缘起消耗、同一招再次施放（旧的一条先结束）或复位时结束；开始和结束各发一次事件 |
| 开局登记 `YuanqiRegistry` | 每次定槽（`SkillLoadout.ServerSlotsChanged`）都按配装重新登记：从缘起列表里挑出两招都在本配装里、且启用的对，为这个 Racer 各建一个缘起实例；木桩不施法，没有状态板，也不登记 |
| 触发 | 某个技能在状态板上变为生效时，只查本 Racer 已登记、且包含这个技能的缘起；另一招也在生效、且这两条还没触发过，就触发。不每帧轮询 |
| 技能兼容接口 | **消耗**：`ISkillRuntimeHandle.Consume`，提前结束剩余效果，清理修饰和表现（神足通、缓流、金身、神掌有）；**参数改写**：按需的小接口，目前只有 #1 的 `IHandsImpulseScale`（蓄力弹冲量跟随体型）。缘起模块只通过这些接口动技能；消耗哪招由配表的 `consumeA / consumeB` 决定 |
| 持续型缘起 | 缘起效果需要持续时（如巨掌随体型变化），缘起实例在两招都生效期间存在，任一招结束就撤掉改写 |
| 网络 | 判定只在服务器做；改运动的效果走预测修饰命令（服务器一份 + owner TargetRpc）；画面只读观察者事件和同步状态，表现层这一轮只做接口（见 A6） |

### 5.3 三连（搁置）

缘起生效期间再放第三招打出终式，这次不做（已定）。原因：效果过强；技能本来就不多，三连会让选卡套路固定；要实现的内容按组合数指数增长。原先“前两招成型、第三招收束”的方案和三连风险测算一并撤下，需要时从提交 29af5cf 的版本找回。

## 6. 执念与反噬

### 6.1 执念

| 项 | 规则 | 状态 |
|---|---|---|
| 基础曲线 | 不变：每次施法加 `Skill.obsessionGain`（200/300），每秒自然下降 10，反噬概率是 sigmoid（Pmax 95%、k 0.008、中点 500） | 已定 |
| 同招加价 | 同一技能在自己冷却结束后 5 秒内再放，本次执念增量 ×1.15；连续时按复利叠加（×1.15 → ×1.32 → ×1.52）；与中间是否放过别的技能无关 | 已定 |
| 加价层数清零 | 冷却结束超过 5 秒才放就清零；反噬时也清零 | 已定 |
| 落后补偿 | 本次不改，保持现值 `catchupRecoveryA` 0.01；以后试玩再在配表调（原提议是调大，让车群外的追赶更可感） | 已定 |

反噬概率参考：

| 执念 | 0 | 100 | 200 | 300 | 400 | 500 | 600 | 700 | 800 | 900 | 1000 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 反噬 % | 1.7 | 3.7 | 7.9 | 16 | 30 | 48 | 66 | 79 | 87 | 91 | 95 |

神足通（增量 300，冷却 5 秒）每次冷却一好就重放：

| 第几次 | 请求时执念 | 反噬 % | 本次增量 |
|---|---|---|---|
| 1 | 0 | 1.7 | 300 |
| 2 | ≈250 | 11 | 345 |
| 3 | ≈545 | 56 | 397 |
| 4 | ≈890 | 91 | 456 |

正常打法最多叠两层；同招加价主要是一个明确的信号，不会让数值失控。

### 6.2 反噬

| 项 | 规则 | 状态 |
|---|---|---|
| 本次技能 | 失效；不加执念，也不计入同招加价的次数 | 已定 |
| 本次技能的冷却 | 照常进冷却（按原技能的冷却） | 已定 |
| 惩罚开始的时刻 | 掷骰仍在按键那一刻；1 秒确认前摇照常播放，播的是统一的反噬表现（`IsAnti` 动画和 `Anti_Reflection` 特效），前摇结束时惩罚开始 | 已定（2026-10-26） |
| 惩罚期间复活 | 惩罚继续：技能锁、执念冻结、极速 ×0.85 都持续到 10 秒结束，结束时照常 ×0.8 | 已定 |
| 前摇中途取消 | 已判定反噬的施法在 1 秒前摇里不会被取消，前摇结束照常进入惩罚，AI 和人类玩家一样：AI 放弃施法（时机过期、同类全局效果已经可见等）、复活或传送都不会让它失效。只有 AI 离开比赛和组件销毁会丢掉它（`SkillCastRules.ResolveConfirmOutcome` / `DropsPendingCast`） | 建议（实现采用） |
| 前摇中被禁锢 | 已判定反噬的施法被禁锢时照常进入惩罚：本槽冷却照常花掉，前摇结束时开始 10 秒惩罚；禁锢只打断普通施法（取消并返还冷却） | 已定（2026-10-26） |
| 惩罚 | 10 秒内：所有技能锁住；执念条冻结、不自然下降（落后补偿的额外下降也暂停）；极速 ×0.85 | 已定 |
| 结束时 | 执念 ×0.8（降低当前持有值的 20%） | 已定（暂定值） |
| 身上已有的 buff | 不打断；已生效的缘起照常按各自规则继续；锁住期间不能放技能，所以无法触发新缘起 | 已定 |
| 反作用 | 用户提到反噬“有些许可能出现反作用”。理解为：反噬时，身上已有 buff 有一定概率反转（比如金身变缩小，巨掌跟着变小掌）。2026-10-31 变体恢复后，它和变体是什么关系（被变体取代，还是另外保留）待用户答复 | 待确认 |
| 六种 `_anti` 变体 | **统一惩罚 + 必定变体**：反噬时照常施加上面的统一惩罚（本次技能失效、不加执念、本槽照常进冷却、三槽锁 10 秒、极速 ×0.85、执念冻结、结束 ×0.8），同时必定放出这个技能自己的 `_anti` 反效果。变体内容按改版前 dev 原样恢复（神足通反噬自身减速、缓流反噬定身后加速、一念光明反噬只遮自己、金身反噬缩小、神掌反噬向后的反向弹幕、逆姻缘反噬反转自己）。这一行推翻了 2026-10-25 起记的“由统一惩罚取代（已定）”。实现见 [退役记录](retired.md) 第 8 节 | 已定（2026-10-31） |
| 变体与惩罚的边界 | 变体按自己的时长独立结束，和 10 秒锁、×0.85 互不影响；反噬判定后被禁锢，变体照常放出；变体不进技能状态板，不能触发也不能参与缘起；复活时变体效果按普通技能效果的复位规则清掉（惩罚照旧持续）；变体不加执念、不另算冷却 | 建议（实现采用），待用户答复（2026-10-31 已提问） |

执念条冻结后，解锁时的执念就是反噬时的值 × 0.8：

| 反噬时执念 | 解锁时执念 | 解锁时反噬 % | 降到 300（≈16%）还要等 |
|---|---|---|---|
| 500 | 400 | 30 | 10 秒 |
| 700 | 560 | 59 | 26 秒 |
| 900 | 720 | 81 | 42 秒 |

执念越高时反噬，越吃亏：在 900 反噬，一共约 52 秒放不了安全的技能，差不多半圈。掷骰发生在按键那一刻，玩家看得到概率，所以不是被迫的死循环，但 HUD 必须把当前反噬概率显示清楚。

## 7. 修改方案

### 7.0 总则

| 项 | 规则 | 状态 |
|---|---|---|
| 顺序 | 先改游戏机制（阶段 A），机制改完后再做配表对接（阶段 B），最后逐级验证（阶段 C）。配表只是机制的对接层 | 已定 |
| 数值 | 技能数值按配表现值，阶段 A 不改数值。本文里引用的技能数字都是配表现值 | 已定 |
| 新增参数 | 阶段 A 新增的参数（如同招加价的 5 秒、×1.15）先写成组件或 SO 上的序列化字段，带默认值；阶段 B 再接进配表，默认值就是表里的初值 | 已定 |
| 配表结构 | 阶段 A 不改 xlsx 的表头和 sheet；退役的行和列先留在表里，代码不再读取，阶段 B 再删。这样阶段 A 中途导表和校验不会坏 | 已定 |

每一步写清：改哪里、新增、退役、网络与预测、训练模式怎么验证、完成标准。A1–A6 按实现写（0dbc551），A7 按实现写（ac3d5fe），A8 按实现写（d6fc63b1、a5dc8808）。验证结果引用 [训练模式验证记录](practice-verification.md) 的场景编号 S0–S13。这次机制更新退役的代码、资产、配表和文档见 [退役记录](retired.md)。

### 7.1 阶段 A：机制

步骤顺序和依赖：

| 步 | 内容 | 依赖 | 状态 |
|---|---|---|---|
| A1 | 训练木桩 | 无；后面每一步都靠它验证作用于他人的效果 | 已完成（S0、S7、S12） |
| A2 | 操作：单键释放、鼠标瞄准 | 无 | 已完成（S1、S2） |
| A3 | 施法节奏：去掉全局锁、施法串行 | 无 | 已完成（S3） |
| A4 | 执念与反噬：统一反噬、同招加价 | A3 | 已完成（S4、S5、S6） |
| A5 | 缘起：技能状态板、开局登记、缘起模块、技能兼容接口 | A3；木桩来自 A1 | 已完成：#1、#2、#4（S8、S9、S10） |
| A6 | HUD | A2–A5 | 已完成，最小可用版（S11） |
| A7 | AI 适配，并最终退役搓招 | A2–A5；在单人赛验证 | 已完成（ac3d5fe，单人赛 harness 验证见下） |
| A8 | 操作：W 推进（2026-10-30 追加），AI 走同一个推进开关 | A2、A7 | 已完成（S1、S13；单人赛 harness 验证见下） |

#### A1 训练木桩

规则（已定）：固定位置生成、不驾驶、不施法的 Buddah，只在训练模式生成，用来验证命中、减速、反转和冲量。

| 项 | 规则 | 状态 |
|---|---|---|
| 数量与位置 | 3 个，放在起跑后第一段直道上，沿赛车线间隔 15 米，横向分别偏左、居中、偏右 | 已定 |
| 物理 | 正常受力，能被神掌推走、被金身撞开，能吃减速和反转；油门为 0，原地停车 | 已定 |
| 复位 | 离开生成点后 3 秒没再受到技能或碰撞，就传送回生成点；掉出赛道时走现有复活 | 已定 |
| 静止接触与滑行 | 静止贴着别的物体（另一个木桩、停着的赛车）不算碰撞：接触的相对速度或对方自身的水平速度超过 0.5 m/s 才算（`TrainingDummy.disturbedContactSpeed`），否则两个被推到一起的木桩会互相刷新计时，永远不复位。木桩被撞飞后自己的滑行也不算受击，所以最后一次受击 3 秒后就传送回去，不会滑出几百米再停（训练模式实测：300 冲量能把木桩送出约 500 米） | 建议（实现采用） |
| 身份 | 不是参赛者：不进排名、结算、小地图，不影响落后补偿的 gap；技能找目标时要把它算进去 | 已定 |
| 反馈 | 头顶显示当前受到的效果和剩余时间（减速、反转、受击冲量） | 已定 |

| 项 | 内容 |
|---|---|
| 改哪里 | `MatchSpawnManager.SpawnTrainingDummies`（在 `SpawnSoloAI` 之后调用），只在 `IMatchRules.SpawnTrainingDummies` 为真时生成：`SoloMatchRules` 在 AI 数为 0（训练模式）时为真，`OnlineMatchRules` 恒为假。用同一个 Racer prefab，无 owner，不调用 `AIRacerDriver.AdoptProfile` 和 `AISkillCaster.Configure`。位置：人类起跑点投影到赛道后，沿赛道 90 米起放第一个，间隔 15 米；左右两个横向偏 10 米，按两侧墙距收窄并探测地面（RaceMap 上落在起跑线后约 53、68、83 米）。数量、间距、起点偏移、横向偏移、复位时间在 `ModeRule` 的 Practice 行（阶段 B） |
| 新增 | `Assets/Scripts/Match/TrainingDummy/`：`TrainingDummy`（`ISteeringOverride` 原地停车、复位计时 `TrainingDummyResetClock`）、`TrainingDummyReadout` 与 `TrainingDummyReadoutText`（头顶读数）、`TrainingDummyLayout`（摆放计算）；身份 `RacerIdentity.IsTrainingDummy` / `AssignTrainingDummyBeforeSpawn`，`RacerAuthority.IsServerDummy` / `IsServerControlled` |
| 身份要点 | 排名、完赛和结算都按 `PlayerProgressReporter` 枚举（`LeaderboardManager`、`RaceFinishManager`、`MatchResultPresentationCoordinator`）。实现时没有关掉木桩的 `PlayerProgressReporter`：木桩的 racer id 保持未分配（−1），这三处都跳过无效 id；`RacerAuthority.IsProgressAuthority` 也不包含木桩，所以木桩不跑圈数、计时和逆行逻辑，训练模式的 `endWhenAllRacersFinished` 不会等木桩完赛。开场演出（`IntroSequenceManager.FindIntroBodies`）也排除木桩，训练模式仍按 1P 布局。木桩要保留 `SkillExecutor`，因为逆姻缘按场上所有 `SkillExecutor` 找目标（`Skill_ReverseTurn.ExecuteServer`） |
| 网络与预测 | 木桩没有 owner，技能效果只在服务器生效，和 AI 一样（`ApplyXToOwner` 没有 owner 时跳过 TargetRpc）。木桩的身份是“服务器控制、但不是参赛者”：服务器侧的清理和本地控制判断走 `RacerAuthority.IsServerControlled`（服务器 AI 或木桩）；`IsServerAI` 仍要求 `RacerIdentity.IsAI`，木桩不冒充 AI，`AISkillWorld` 按 `RacerDirectory` 取对手，木桩不在里面 |
| 训练模式验证 | 缓流让木桩减速、逆姻缘让它反转、冲量推开它，头顶读数和效果一致；最后一次受击 3 秒后复位；比赛能正常结束，排名和结算里没有木桩。结果见 S0、S7、S12（金身撞开木桩见 S8） |
| 完成标准 | 以上全部通过；单人赛和联机不生成木桩 |

#### A2 操作：单键释放、鼠标瞄准

| 项 | 内容 |
|---|---|
| 改哪里 | 技能键：`SkillExecutor` 的 owner 绑定 `InputSystem_Actions.Player.SkillSlot1–3`，`OnSkillKeyPressed → RequestCast`；玩家不再经过 `ComboSkillInput.OnSkillSlotTriggered`（当时只剩服务器 AI 订阅它，A7 已连组件一起删除）。瞄准与推掌：`BuddahHandControl`，光标射线投到过 racer 高度的水平面取方向，`HandAimMath` 先平滑、再按 `rotationSpeedDegPerSec` 限速；`HandPush` 加鼠标左/右键。`PlayerBuddahInputSource` 没有改 |
| 新增 | `InputSystem_Actions` 的 `SkillSlot1–3`（Q/E/R 与 1/2/3，`.inputactions` 和生成的 `.cs`）和 `HandPush` 的 `<Mouse>/leftButton`、`<Mouse>/rightButton`；`BuddahHandControl` 的 `useMouseAim`、`mouseAimSmoothingSeconds`（0.06 秒）、`mouseAimDeadRadius`（1.5 米，随体型放大）、`mouseAimPlaneHeightOffset`，这几个值是建议（实现采用），试玩再调；转手上限沿用 `Push.basic.rotationSpeedDegPerSec` 180°/s |
| 退役 | 玩家侧的搓招：`ComboSkillInput` 的 owner 不再启用输入、不再监听 `HandPush`；HUD 搓招进度（槽位的 W/↑ token 先在运行时隐藏，2026-10-31 连图一起删除）。`ComboSkillInput` 当时保留给 AI 逐键搓招，A7 已连同两张搓招表一起删除 |
| 网络与预测 | 手部 yaw 仍经 `BuddahHandControl.TrySendYawToServer` 同步，网络结构不变；施法请求仍走 `RequestCast → CastSlotServerRpc` |
| 训练模式验证 | 结果见 S1、S2：Q / 2 / R 放出槽 0、1、2，W 和 `W↑W` 都不施法；手部朝向到达光标目标 ±3°，最大转速 180°/s；左键只推左手、右键只推右手。“鼠标转手不抽搐”和“对木桩的神掌命中率比方向键明显提高”要用户试玩确认 |
| 完成标准 | 玩家侧不再经过搓招就能施法；单人赛里的 AI 仍能照常施法（A7 起 AI 直接调用 `CastSlotServer`） |

#### A3 施法节奏：去掉全局锁、施法串行

| 项 | 内容 |
|---|---|
| 改哪里 | `SkillExecutor` 删掉全局锁 `_castLockedUntil`；请求（`TryResolveCast`）和 AI 的就绪判断（`IsSlotReadyServer`）共用一个闸门 `SkillCastRules.EvaluateGate`，依次检查待确认施法、反噬锁、冻结、本槽冷却 `_nextReadyTime`（单技能冷却保留，第 4 节已定）；被挡的请求发 `CastStage.Rejected`。冷却从执行时刻算起（请求时刻 + 前摇 + 冷却） |
| 施法串行 | 改版前真人路径 `CastSlotServer → TryResolveCast` 只检查全局锁和冷却，没有检查 `HasPendingCast`，是全局锁顺带挡住了第二次请求；去掉全局锁后，前摇期间的第二次请求会再起一个协程、覆盖 `_pendingCast`，而且掷骰时上一招的执念还没入账（原来记为“掷骰时机漏洞”）。现在闸门的第一项就是 `HasPendingCast`：前一招执行、执念入账后才接受下一招 |
| 退役 | 全局锁的代码路径；`SkillAction.castLockSeconds` 字段和 `Skill.castLockSeconds` 列（阶段 B 已删，16121ed）；`LocalSkillUiEvent.CastLockSeconds` 和 HUD 的 2 秒假锁（A6 删） |
| 网络与预测 | 没有新的同步 |
| 训练模式验证 | 结果见 S3：前摇期间第二个请求被拒绝，第一招执行后第二招被接受，两个技能同时在状态板上生效，每槽只执行一次 |
| 完成标准 | 两个技能能同时生效；任何时刻最多一个待确认的施法 |

#### A4 执念与反噬：统一反噬、同招加价

规则见 6.1、6.2（同招加价、加价清零、反噬惩罚、结束 ×0.8 已定；反作用待确认，没有实现）。2026-10-31 起反噬还必定放出该技能的 `_anti` 变体（6.2），下表的“退役”一行随之大部分作废，见表后说明。

| 项 | 内容 |
|---|---|
| 改哪里 | `SkillExecutor`：`ResolveCastVariant` 只掷骰，不再换成 `_anti` 的 SkillAction；`QueueCast` 按请求的技能写冷却、算同招层数，前摇播普通或统一反噬表现（`PlayQueuedCastFeedbackObserversRpc`）；前摇结束时 `SkillCastRules.ResolveConfirmOutcome` 决定执行、反噬或取消。反噬走 `ExecuteBackfireServer`：不调用 `ExecuteServer`、不加执念、不计同招层数并清空全部层数，开始惩罚，owner 经 `BackfireSkillUiTargetRpc` 得知本槽冷却，最后发 `Executed`（`IsBacklash = true`） |
| 新增 | 反噬惩罚放在 `ObsessionFigure` 里，没有单独组件：同步的结束 tick（SyncVar）、`IsBackfireActive`、`BackfireSecondsLeft`、`BeginBackfireServer`、`MultiplyServer`、各端的 `BackfireStateChanged`；纯计算在 `ObsessionBackfireRules`。极速 ×0.85 是新的预测修饰 `MaxSpeedMultiplier`（`ApplyMaxSpeedMultiplierToOwner`）。同招加价 `SkillRepeatSurcharge`（`SkillCastRules.cs`）按技能 id 记层数，不按槽：锚点是这个技能上次正常执行的冷却结束时刻，请求时算层数，正常执行后才记账。参数 `repeatWindowSeconds` 5、`repeatGainMultiplier` 1.15、`backfireLockSeconds` 10、`backfireMaxSpeedMultiplier` 0.85、`backfireEndKeepRatio` 0.8 是 `ObsessionFigure` 的序列化字段，阶段 B 进 `Obsession` 表 |
| 退役 | 6 个 `Skill_*_Anti` 类和资产、`SkillAction.antiSkillId`、`ExecuteObservers` / `ExecuteLocal` 的 `isAnti` 参数、`BuddahHandControl.FireChargedProjectileBurstReplicated`、各技能运行时组件里的反噬分支和各自的反噬特效。缓流反噬的定身效果 `MovementRootThenAccelerationEffect` 保留，作为预测关闭时禁锢的兜底。配表里的 `_anti` 行已在阶段 B 删除（16121ed） |
| 网络与预测 | 极速 ×0.85 和其他运动修饰一样走预测桥接（服务器 + owner TargetRpc），技能重置不清（`BuddahPredictedModifierStateOps.ClearActiveSkillModifiers` 不动 `MaxSpeedPenaltyUntilTick`；`Movement/ModifierRule` 的 `resetOnSkillReset = false` 只是文档记录），复活 teleport 由 `PreservePersistentOnReset` 保留；执念和惩罚结束 tick 都是 `SyncVar`，各端按自己的时钟算剩余时间 |
| 训练模式验证 | 结果见 S4、S5、S6：增量 300 → 345 → 396.75，冷却结束超过 5 秒再放回到 300；反噬时技能不生效、执念不涨，三个槽都拒绝，极速 80 → 68，执念不动，中途复活惩罚仍在，正好 10 秒，结束时 ×0.8；已判定的反噬被禁锢时照常进入惩罚，冷却不退 |
| 完成标准 | 反噬是统一惩罚加该技能自己的变体（2026-10-31 起；A4 当时是只剩统一惩罚一种）；观察指标：反噬后多久能放出第一招安全技能，超过 30 秒就考虑把 0.8 往下调（配表 `Obsession.backfireEndKeepRatio`），要试玩数据 |

**2026-10-31 变体恢复。** 用户推翻了“由统一惩罚取代”。上表“退役”一行里的 6 个 `Skill_*_Anti` 类和资产、`SkillAction.antiSkillId`、`FireChargedProjectileBurstReplicated` 和配表的反噬行都按 dev 原样恢复。`ExecuteObservers` / `ExecuteLocal` 不恢复 `isAnti` 参数：变体类自己知道是变体，只有黑幕的 `BlackCurtainPresentation.TryPlay` 和 `SkillPerceptionState.ApplyCurtain` 重新带一个反噬标志。反噬走 `ExecuteBackfireServer`：先开始统一惩罚，再按 dev 的顺序解析变体（表 `antiSkillId` → SO `antiSkillId` → `<id>_anti`），在服务器执行一次，`BackfireVariantObserversRpc` 播表现。提交 7a078eed（配表）、6f8fdb74（弹幕方法）、69e80978（黑幕标志）、7a15e7e5（类与资产）、45eadb63（校验）、b688b5e5（接线）、c369d584（测试）。

#### A5 缘起

结构见 5.2：技能状态板、开局登记、缘起模块、技能兼容接口；代码层面见 [缘起架构设计](yuanqi-architecture.md)。

| 项 | 内容 |
|---|---|
| 改哪里 | 状态板由 `SkillExecutor` 统一写：每次正常执行后（`CastObserversRpc` 之后）`BeginSkillStateServer`，窗口和句柄来自 `SkillAction` 的两个虚方法。`GetActiveWindow` 默认取表 `Skill.effectDurationSeconds`；一念光明覆盖为展开 1 秒之后的保持期 10 秒；逆姻缘用默认（施法者窗口 = 反转的 10 秒，反转组件挂在受害者身上）。`CreateRuntimeHandle` 由神足通、缓流、金身、神掌覆盖。开局登记不在 `MatchSpawnManager`：`SkillExecutor.OnStartServer` 给每个 racer（木桩除外）挂上状态板和登记，`SkillLoadout.ServerSlotsChanged` 每次定槽都重新登记，玩家和 AI 同一条路 |
| 新增 | `Assets/Scripts/Buddah/ComboSkill/Yuanqi/`：`SkillStateBoard`、`YuanqiRegistry`、`YuanqiAction`、`YuanqiContext`、`YuanqiCatalog`、`YuanqiTargeting`、`BuddahBodyImpact` 等；三对在 `Yuanqi/Pairs/`，资产在 `Assets/Skills_Assets/Yuanqi/`（列在 `Skill Database.asset` 的 `yuanqis`）；技能兼容接口 `ISkillRuntimeHandle`（消耗）、`IScaleRuntimeHandle`（实时体型）、`IHandsImpulseScale`（#1 的改写） |
| 技能兼容 | 神足通的消耗是 `SkillExecutor.EndAccelerationForOwner`（预测修饰 `EndModifier(Acceleration)`，预测关闭时 `MovementAccelerationEffect.CancelAndRestore`）加各端的收尾表现；缓流的消耗是区域组件自己的 `Consume`（销毁区域、停特效）；#4 的撞击不是金身开放的接口，而是 `Yuanqi_GiantCharge` 挂到施法者身上的 `BuddahBodyImpact`；神掌的改写是 `BuddahHandControl` 实现的 `IHandsImpulseScale.ImpulseFollowsScale`（#1） |
| 实现顺序 | 状态板 → 开局登记 → 三对（#2、#4、#1）同一批实现。其余各对等内容定了再加 |
| 可复用 | 禁锢没有复用缓流反噬的定身，而是新的预测修饰 `Freeze`：定身的限速清零与停止步，加转向扭矩归零、转向抑制、施法和推掌锁；预测关闭时才用 `MovementRootThenAccelerationEffect` 兜底。突进没有用受击宽限期，而是新的预测修饰 `Burst`：命令落地时把水平速度沿车头设到新上限，额外限速按 增量 × 剩余比例² 回落；同一个人一次突进只撞一次。M 的基准是 `BuddahHandControl.BasicPushImpulseStrength`（`Push.basic.impulseStrength` 100） |
| 清理 | `SkillExecutor.ResetYuanqiStateServer`（清状态板、结束持续型缘起、忘掉已触发的重叠）挂在所有服务器侧复位上：服务器控制的 racer（AI、木桩）的复位、真人的 `ResetActiveSkillEffectsServer`、真人复活 teleport 在服务器上被消费时、`OnStopServer` |
| 网络与预测 | 判定只在服务器做；禁锢、突进、取消加速、取消体型都是新的预测修饰，撞击冲量走 `BuddahPredictionRouter.RouteImpulse`；表现只做接口（见 A6） |
| 训练模式验证 | 结果见 S8、S9、S10：#4 两种先后都对，突进上限 140 → 80 用时 0.8 秒，每个木桩只撞一次，冲量 300，沿施法者→木桩的水平方向，神足通被消耗、金身继续；#2 半径内冻结 1.5 秒、半径外不冻、两招都被消耗、单放神足通不再触发，重新形成重叠再次触发；#1 两招都在时蓄力掌冲量 500 × 4，任一招结束恢复。没有覆盖：#2 先神足通、后缓流只验证了触发和再次冻结（S9 重新触发那次），没查消耗；复活后再触发；一念光明和逆姻缘的窗口（目前没有含它们的缘起） |
| 完成标准 | 加一对缘起只需要新增缘起模块、资产和表行（必要时加兼容接口），不改其他缘起；配装里没有的缘起不检测 |

#### A6 HUD

最小可用版，全部是运行时创建的 TMP 文字，没有改 prefab。

| 项 | 内容 |
|---|---|
| 改哪里 | `SkillSlotUI`（状态在 `SkillSlotHudTimeline`）：每槽冷却、键位（Q/1、E/2、R/3）、同招加价窗口（冷却结束后 5 秒内显示下一次的执念倍率和窗口剩余秒数）、反噬锁和冻结时三个槽的倒计时；`ObsessionUI`：当前反噬概率是主数字，惩罚期间显示剩余秒数；`YuanqiToastUI`（`RaceMapUIBootstrap` 运行时挂到 HUD 画布）：施法者自己的缘起提示（`LocalYuanqiTriggered`）；选技能界面 `PropertiesSelectorUI` 在技能阶段列出本套配装能触发的缘起（`YuanqiCatalog.ForLoadout`，和服务器登记同一个答案） |
| 退役 | 搓招进度（槽位的 W/↑ token 运行时隐藏，2026-10-31 连图一起删除）、全局施法锁的 UI（`LocalSkillUiEvent.CastLockSeconds`、2 秒假锁 `fallbackCastLockSeconds`）；`UI/UiTiming` 里 `SkillSlotUI` 的三行一并删除 |
| 计时口径 | 冷却和同招加价窗口按服务器执行时的 tick 定锚（`CastObserversRpc` / `BackfireSkillUiTargetRpc` 带 `serverTick`，`LocalSkillUiEvent.SecondsSinceServerEvent` = 本端估算的服务器 tick 减去它），不按 RPC 到达时刻，否则远端客户端会晚一个完整 RTT。和反噬、冻结计时一样以服务器时钟为准，远端只剩上行延迟（约 RTT/2）的偏差，方向安全：显示就绪后按下，请求到达服务器时冷却已经结束（本端估算服务器 tick 的误差只有一两个 tick，小于上行延迟）；窗口最后约半个 RTT 显示的加价实际不会收（对玩家有利）。host 与训练模式没有偏差 |
| 文字 | 中文只用 SoloCJK 字体子集里有的字（`InGameHudLabels`，`InGameHudTests` 检查）；“冻”“起”不在子集里，“冻结”显示 FROZEN，缘起名显示英文或 id，等子集重烘（第 8 节） |
| 训练模式验证 | 结果见 S11、S5、S6：槽位显示 Q/1、E/2、R/3，冷却 4.5s，“反噬 47%”（执念 495），惩罚中三个槽“反噬 9.6s”，冻结时 FROZEN |
| 完成标准 | 不看日志也能读出执念、反噬概率、加价窗口，以及本套配装的缘起；位置、字体和可读性要用户试玩确认 |

**表现层（已定，2026-10-26）。** 这一轮只做接口、不做新特效：每个新状态和事件在各端都能读到，美术以后从接口挂特效。接口有 `SkillExecutor.YuanqiObserved`（缘起触发，载荷含中心、半径、秒数、方向、目标 id）、`SkillExecutor.YuanqiImpactObserved`（#4 每次撞击）、`RacerSkillPresentationState.FrozenChanged / BurstChanged`、`ObsessionFigure.BackfireStateChanged`、`BuddahHandControl.TryGetAimGroundPoint`（准星地面点，2026-10-31 起有占位图标，见下）。不需要美术的“动作层”直接实现：冻结时该 racer 的 Animator 停住、双手不转；#4 突进时施法者相机有一次短 FOV 抖动（8 度，待试玩调）。细节见 [缘起架构设计 §4.7](yuanqi-architecture.md)。

**瞄准点占位图标（已定：占位已做，2026-10-31；手部朝向指引的设计待定）。** 用户要“一个做鼠标位置指示的图标占位符，这个是为了未来进一步处理手部朝向指引的”。它是以后手部朝向指引表现的落脚点，这次只做占位、不做美术。用户确认的四项（2026-10-31）：

| 项 | 规则 | 状态 |
|---|---|---|
| 画在哪 | 世界空间：光标射线投到瞄准平面（过 racer 高度的水平面，和手部瞄准同一个点）上，平放一个圆环加十字；图标总是正好在光标下面 | 已定 |
| 画什么 | 只画瞄准点，不画角色到瞄准点的指向线 | 已定 |
| 何时显示 | 只给本机 owner 显示：比赛进行中、手能瞄准时显示；倒计时、开场演出、UI 挡输入（退出确认框）、没有鼠标或相机时隐藏。禁锢（冻结）期间照常显示，手不转但能看到光标落点。复活是瞬移，没有单独的隐藏 | 已定 |
| 死区 | 光标在死区内（离 racer 水平距离 ≤ `mouseAimDeadRadius` 1.5 米 × 体型倍率）时隐藏，那里手保持原目标、不跟光标；和瞄准用同一条判断 `HandAimMath.IsInsideDeadRadius` | 已定 |

| 项 | 内容 |
|---|---|
| 改哪里 | 新增 `AimGroundIndicator`（`UI/InGameUI/`，`RaceMapUIBootstrap.EnsureRuntimeHud` 运行时挂到 HUD 画布，和 `YuanqiToastUI` 同一个入口，没有改 prefab；图标本身是单独的场景物体，不挂在画布下）和纯逻辑 `AimGroundIndicatorMath`（显示判断、抬高、圆环十字网格）。`BuddahHandControl` 只加了只读入口：`LocalOwner`（owner 端 `OnStartClient` 设、停止时清，HUD 不用每帧搜场景）和 `IsInsideMouseAimDeadRadius(point)`；`HandAimMath.TryGetTargetYawOffset` 的死区判断抽成 `IsInsideDeadRadius`，瞄准行为不变 |
| 占位参数 | `AimGroundIndicator` 的序列化字段，代码注释标“占位，等手部朝向指引设计”：圆环半径 1.0 米、线宽 0.15 米、十字半长 0.35 米（编辑器里第一版 0.5 米在赛车镜头下直径只有约 22 像素，放大一倍）、48 段、颜色 (1, 0.85, 0.3, 0.9)、离瞄准平面抬高 0.05 米、死区内隐藏 = 是。图形是程序化网格加内置 `Sprites/Default` 材质（在 Always Included Shaders 里，打包可用），没有新美术资源。尺寸不随体型缩放 |
| 网络与预测 | 没有。只读 owner 本地的光标和瞄准平面，不发 RPC，不进预测；AI、木桩和别人的 racer 都不画 |
| 已知限制 | 角色自己的头和手会挡住图标：RaceMap 渲染器的 RenderObjects 特性 `RenderBuddah`（`URP-HighFidelity-Renderer` 等，`Buddah` 层）在透明物体之后单独画角色，地图边缘（`RenderMapEdge`）同理，所以图标即使关掉深度测试也会被盖住；赛车镜头在身后偏高，光标落在角色前方几米时常被佛头挡住（编辑器实测：右前方 35°、9 米处被挡）。试过关深度测试（自建着色器、`UI/Default` 的 `unity_GUIZTestMode`），只对坡道地面有用，而且各画质档不一致，已撤回。指引设计时要么给它在 `RenderBuddah` 之后单独一个 RenderObjects 阶段，要么改成屏幕空间。其他：瞄准平面是水平面，地面有坡时图标会浮在地面上方或沉到地面下（它标的是手实际瞄准的点，不是地表）；光标靠近地平线时瞄准点很远、图标很小；光标采样和相机更新的先后可能差一帧 |
| 以后接手部朝向指引 | 读侧不动：`BuddahHandControl.LocalOwner`、`TryGetAimGroundPoint`、`IsInsideMouseAimDeadRadius`。换表现只改 `AimGroundIndicator` 的 `BuildVisual` / `LateUpdate` 绘制部分（或换成 prefab 字段）；要画手的实际朝向（平滑和限速之后）而不是光标点，需要在 `BuddahHandControl` 再加一个只读的当前手部 yaw（现在只有 `ServerHandYaw`，owner 本地的 `handYawOffsetDeg` 是私有的） |
| 配表 | 这次不加数值。占位参数属于表现和 HUD 布局，按第 8 节的范围线留在代码；等指引设计定了再看要不要进表 |
| 验证 | 无窗口 EditMode `BuddahGo.Tests` 604/604（基线 597，新增 `AimGroundIndicatorTests` 7 个：显示判断、抬高、死区与瞄准一致、网格形状）。编辑器训练模式（2026-10-31，虚拟鼠标、真实鼠标停用）：开场演出和倒计时期间不画；比赛中光标摆到离 racer 9 米处，图标就在 `TryGetAimGroundPoint` 的点上（差 0.000 米），投回屏幕和光标差 0.1 像素；光标在 0.81 米处（死区内）隐藏，回到 9 米外重新显示；禁锢 4 秒期间照常显示；打开退出确认框隐藏、关闭后恢复；场上 4 个 racer（本机 1 个、木桩 3 个）只有 1 个图标。没验证：坡道上的遮挡、单人赛和联机客户端、试玩手感 |

#### A7 AI 适配

完成于 2026-10-28（ac3d5fe）。AI 和真人走同一条施法路径，搓招从代码和配表里退役。AI 侧的决策设计、风险模型和缘起决策的细节在 [AI 技能](../single-player/ai-skills.md)（顶部 A7 结果表、“8. 缘起决策”）和 [人格设计](../single-player/ai-personalities.md)。

| 项 | 内容 |
|---|---|
| 改哪里 | `AISkillCaster` 不再注入搓招键：`Decide` 选定槽位后 `AISkillCommitment` 记为 Decided，下一 tick 像真人按键一样调用 `SkillExecutor.CastSlotServer(slot)`（和 owner RPC 同一个入口，施法串行、同招加价、统一反噬都在 `SkillExecutor` 里），然后等 Executed / Cancelled / Rejected。瞄准仍经 `BuddahHandControl.InjectServerRotation`，掌形发射改为直接 `InjectServerPush`；容差 ±20° / ±12° / ±6° 没有重调（见第 8 节） |
| 新增 | 缘起决策 `AIYuanqiPlanner`（`Assets/Scripts/AI/`，纯逻辑）：按本配装登记的对（`YuanqiRegistry.Registered`，与服务器同一份 `SkillDatabase.GetYuanqis()` + `YuanqiCatalog.ForLoadout`）决定先后（会被消耗的那招先放；两招都消耗或都不消耗时主效果长的先放）、跟进（另一招生效中、剩余 ≥ 前摇 + 1 tick；先放那招会被消耗时只在剩余 ≤ 前摇 + 0.6 s 时跟进，否则立刻）和起手（另一招的冷却来得及、第二招的反噬概率 `P(当前执念 + 第一招增量)` 低于停手阈值才给加成）。只用已实现的三对，没设计的对不补。统一反噬风险：`u = v × 缘起倍率 − riskWeight × P(反噬) × backfireCost`，每个难度一个停手阈值 `stopProbability`；`AISkillWorld.Classify` 把反噬的 ×0.85 记为减速受损、突进记为加速。参数：`AI/SkillTuning` 的 `backfireCost` 1、`yuanqiSetupMultiplier` 1.2、`yuanqiFollowUpScore` 0.8、`yuanqiFollowUpMultiplier` 1.5、`yuanqiFollowUpTempoSeconds` 0.3、`yuanqiLateWindowSeconds` 0.6，`AI/SkillDifficulty.stopProbability` 1.01 / 0.7 / 0.45，`AI/Personality.yuanqiAffinity` 1；JSON 兜底 `SkillPersonalities.json` 同步。这些数值是建议（实现采用） |
| 退役 | （2026-10-31：反噬变体恢复后，AI 是否要把按技能的反噬代价加回来待用户答复，现在仍按统一 `backfireCost`。）`ComboSkillInput`（类、prefab 组件、`SkillExecutor` 的订阅与 `ClearCombo`）、`Skill/ComboInput`、`Skill/ComboBinding`、`__enums__` 的 `ComboToken`、`AISkillCommitment` 的按键步骤、AI 表的搓招列（`keyMistakeProbability`、`keyMinSeconds`、`keyMaxSeconds`、`maxComboRetries`）、按技能的 `backlashSeverity` 与 `mildStopProbability` / `severeSeverityThreshold`（`severeStopProbability` 改名 `stopProbability`）、校验规则 `KeyMaxBelowComboWindow`。`AISkillEffectHarness` 和 `Tools/ai/analyze_skill_matrix.py` 的反噬用例改成统一反噬 |
| 网络与预测 | 没有新的同步；`SkillExecutor` 只多了只读的 `ResolveCastBlockServer`（AI 记录被哪道门拒绝） |
| 验证 | 无窗口 EditMode `BuddahGo.Tests` 577/577（A7 前 559）；无窗口导表 0 错误；单人赛（5 个 AI，`AISkillRaceHarness`，编辑器 Play）：2026-10-28 Normal 难度一场（`Logs/a7/solo-1`，不入库）：自然结算（`natural-race-results`），0 运行错误；五个 AI 各执行 10–17 次施法（1.8–3.1 次/分钟），63 次请求全部被接受，没有一次在前一招待确认时再请求（施法串行成立），没有 Rejected；7 次反噬（keeper 1、hunter 1、brawler 2、trickster 0、opportunist 3）之后下一次执行都在 ≥ 11.1 s 后，惩罚 10 s 内三个槽的候选全部是 `cooldown-or-lock`；缘起触发 8 次：keeper #2 禅足冰封 ×4，brawler #1 金身巨掌 ×4，#4 金足破阵 0 次（keeper 这场没放过金身）；两个 AI 自然完赛（316.6 s、327.2 s），三个在完赛倒计时截止时在第 3 圈，真人不操作、停在起点。要用户试玩判断的：黑幕的频率（trickster 10 次、opportunist 7 次，每约 20 s 一次全屏黑幕）、#2 禁锢对车群的影响、圈速（带技能的 Normal AI 单圈 101–139 s，之前无技能基准 92.8–94.4 s） |
| 完成标准 | 代码和配表里不再有搓招；AI 和真人走同一条施法路径；AI 能在单人赛里施法、打出缘起并吃到反噬惩罚（都已验证）。节奏、停手阈值和缘起参数的手感要用户试玩 |

#### A8 前进：按住 W 推进

用户决定（已定，2026-10-30）：前进不再自动，按住 W 推进、松开停止推进，AI 同步适配。细则见第 3 节：松开只撤推进力、不刹车；W/↑ 推掌两条都删；倒计时、起跑交接、复活、禁锢、反转、UI 挡输入沿用原规则；推进力沿用配表；AI 一直推进到完赛。

| 项 | 内容 |
|---|---|
| 原来 | 自动前进来自预测马达：`BuddahPredictedMotor.BuildReplicateData` 里 `throttle = movementAllowed ? 1 : 0`，只有 `ISteeringOverride`（AI、木桩、harness 的停车）能改成 0；预测关闭时的 `BuddahMovement` 读 `PlayerBuddahInputSource.GetThrottle`，它恒为 1 |
| 改哪里 | `InputSystem_Actions` 的 `Movement` 加 `<Keyboard>/w`（`.inputactions` 和生成的 `.cs` 一致），`HandPush` 删 `<Keyboard>/w`、`<Keyboard>/upArrow`；`PlayerBuddahInputSource.GetThrottle` 读 W；`BuddahPredictionOwnerInputBridge.ReadThrottle`（UI 挡输入时为 0，和 `ReadSteering` 一样）；`BuddahPredictedMotor.BuildReplicateData` 改用 `BuddahDriveInput.ResolveThrottle`（移动门关闭 → 0；有转向覆盖 → 覆盖的 `drive`；否则 → owner 的 W）；`BuddahHandControl.TryResolvePushHand` 删 W/↑ 分支 |
| AI | 不按键：`AIRacerDriver.TryGetOverride` 的 `drive` 本来就是写进同一个 `Throttle` 字段的推进开关，一直为 true，完赛后为 false。AI 的运动模型 `BuddahMotionModel` 本来就按满推进力规划，所以 AI 的代码只补了注释，行为不变 |
| 新增 | `BuddahDriveInput`（`New_Buddah/Core/`，纯函数）；`BuddahPredictionDebugState.throttleInput`（诊断用）；EditMode `DriveInputTests`（W 绑定在资产和包装类里一致、W 按住为 1 松开为 0、A/D / ↑ / 空格不推进、移动门和转向覆盖的优先级）；训练模式场景 S13 |
| 退役 | W/↑ 推掌的绑定和代码分支；`MechanicsPracticeHarness` S1 里“W 仍推左手”的记录改成“W 和 ↑ 都不推掌”的检查；`AISkillEffectHarness` 让真人推掌的虚拟按键从 W 改成虚拟鼠标左键 |
| 网络与预测 | 没有新的同步，`BuddahPredictedInputData` 结构不变（`Throttle` 原本就在里面，只是真人一直写 1），预测协议版本不用改。服务器用 owner 发来的输入模拟，推进开关和转向一样是服务器权威 |
| 起跑交接 | 用户追加（已定，2026-10-30）：“完全的物理丢出去模式”。原来交接后 0.2 秒把速度钉在开场速度、油门和转向清零，再 0.32 秒把速度和输入线性混合回来，另有 0.15 秒屏蔽转向（`RaceBodyIntroStateController` 传给 `BeginLaunchHandoff` 的常量）。现在 `Movement.launchInheritSeconds` = `launchBlendSeconds` = 0（配表），`RaceBodyIntroStateController.LaunchSuppressTurnSeconds` = 0（代码常量，配表 doc 列 `launchSuppressTurnSeconds` 同步为 0）：`ConsumePendingLaunchHandoffEvent` 照旧在交接那一 tick 落地并设 `rb.velocity` = 开场速度，`BuddahPredictedLaunchHandoffResolver.Advance` 看到 0 时长直接进 Normal，之后只有物理。owner 和服务器走同一条路径，交接数据结构不变 |
| 训练模式验证 | 结果见 S1、S13：W ↑ W 不施法、不推掌；不按 W 时油门 0、静止；按住 W 0.6 秒油门 1、速度上升；松开后油门 0、速度不再上升，滑行减速 约 1.95–1.97 m/s²（和改动前按地面摩擦 0.2 × 9.81 的估算一致）。改成完全物理交接后完整重跑 14/14 通过 |
| 单人赛验证 | `AISkillRaceHarness` Normal 一场（2026-10-30，`Logs/a8/solo-20261030-231507`，不入库；W 推进和完全物理交接都已生效）：自然结算（`natural-race-results`），0 运行错误；trickster 320.5 s 自然完赛，其余四个 AI 在完赛倒计时截止时在第 3 圈；单圈 102–167 s（keeper 126.1 / 102.2，hunter 110.0 / 124.5，brawler 167.3 / 104.0，trickster 107.7 / 109.2 / 103.6，opportunist 129.0 / 104.2），和 A7 那场（101–139 s）同一量级，brawler 第一圈 167 s 偏慢；每个 AI 执行 10–18 次施法，71 次请求接受 69 次（opportunist 2 次被 A7 的 `rejected-global-visible` 挡下：已有黑幕可见时不再放黑幕），反噬 14 次，缘起 8 次（keeper #4 ×1、#2 ×2，brawler #1 ×5）。真人不按键：交接时 59.97 m/s，从第一个 tick 起只按摩擦减速（1.08 s 后 57.38 m/s，约 2.4 m/s²），之后被 AI 蓄力掌打中 10 次，靠冲量加滑行在 235.6 s 过了第 1 圈，没有任何 W 式的持续加速。同一天更早的一场（改交接之前）在 68 s 被手动停止（`operator-disabled`），期间编辑器里按过一次真实的 W，不作数 |
| 完成标准 | 真人不按 W 不前进；AI 照常跑完比赛，圈速和改动前在同一量级；手感（滑行距离、松开后要不要减速）要用户试玩 |

### 7.2 阶段 B：配表对接

阶段 A 稳定后做，已完成（16121ed），A7 的表改动已随 A7 做（ac3d5fe）。每项都按 [design-tables README](../design-tables/README.md) 的流程：改 xlsx 表头（`##var`、`##type`、`##comment`）→ `python Tools/design-tables/gen_records.py .` 重新生成记录类和 `export-map.json` → 组件里接 `DesignTableOverlay.Apply` / `ApplyMapped` → 补跨表校验规则 → 导入 → 更新 `Docs/design-tables` 对应的 inventory 文档 → 跑 `DesignTableTests`、`DesignTableRuntimeTests`。阶段 A 的序列化字段默认值就是表里的初值；接上后表是数值来源，组件和 SO 上的值只在没有表时兜底（design-tables 决定 14：覆盖而非重构）。退役的字段和 sheet 从表里删掉，不留无效列。

| 来自 | 配表与映射 | 状态 |
|---|---|---|
| A1 木桩 | 并进 `Match/ModeRule`：五列 `trainingDummyCount`、`trainingDummySpacingMeters`、`trainingDummyStartOffsetMeters`、`trainingDummyLateralOffsetMeters`、`trainingDummyResetDelaySeconds`（Practice 行 3 / 15 / 90 / 10 / 3，Online / Solo 的数量填 0）。`MatchSpawnManager` 只读 Practice 行（`TrainingDummyModeRuleMap`）；是否生成仍由代码里的 `IMatchRules.SpawnTrainingDummies` 决定 | 已完成 |
| A2 操作 | `Push.rotationSpeedDegPerSec` 的 `##comment` 改成“鼠标转手速度上限”（方向键回退和 AI 也用它）；键位不做成可配，文档表 `Global/InputBinding` 列出 Q/E/R + 1/2/3、鼠标左/右键推掌、←/→ 回退，W/↑ 标为废弃 | 已完成 |
| A3 节奏 | 删除 `Skill.castLockSeconds`（没有改成全局短间隔） | 已完成 |
| A4 反噬 | 删除 `Skill` 表 6 行 `_anti` 和 `antiSkillId` 列；删掉 `SkillEffect*` 的反噬行与反噬列、`Push` 的 `charged_anti` 行和它专用的列、`SkillConstant`、`SkillSelectionRule`（Hidden）、`SkillPresentation`、`FeelEvent` 的反噬行；保留 `Anti_Reflection_*` 表现（统一反噬的前摇在用）；`VfxRegistry` 的反噬 id、`__enums__` 里 `SkillId` 的反噬值和 `UI/Text` 的反噬文案 key 阶段 B 按“建议（实现采用）”保留，2026-10-31 退役清理时因无读取方删除，保留删除已定（2026-10-31，见[退役记录](retired.md) 4.1，`SkillId` 编号 6–11 不复用）。**2026-10-31 推翻**：用户决定恢复反噬变体，上面删掉的反噬行与列（不含 `castLockSeconds`）、`antiSkillId`、`SkillId` 6–11、12 个文案 key 和两个 VFX id 都按 dev 原样恢复（7a078eed），“6–11 不复用”作废。`Obsession` 新增 `repeatWindowSeconds` 5、`repeatGainMultiplier` 1.15、`backfireLockSeconds` 10、`backfireMaxSpeedMultiplier` 0.85、`backfireEndKeepRatio` 0.8。依赖 `antiSkillId` 的校验规则一起改 | 已完成 |
| A5 缘起 | 新增 `Skill/Yuanqi`（缘起 id、两招、`consumeA / consumeB`、`mode`、`retriggerPolicy`、`isEnabled`、文案 key）和家族表 `YuanqiEffectFrostLock`（N、R、是否冻木桩）、`YuanqiEffectGiantCharge`（突进限速、回落秒数、M）；`__enums__` 加 `YuanqiId`、`YuanqiMode`、`YuanqiRetrigger`；`UI/Text` 加 `yuanqi.<id>.name / .desc`。#1 没有参数，不建家族表。表结构见 [缘起架构设计 §3](yuanqi-architecture.md) | 已完成 |
| 预测修饰 | `ModifierCommandType` 加 `Freeze`、`EndModifier`、`Burst`、`MaxSpeedMultiplier`，`ModifierRule` 加四行，`StackPolicy` 加 `Restart`；`StructuralConstant.prediction_protocol_version` = 2 | 已完成 |
| A6 HUD | `UI/UiTiming` 删掉 `SkillSlotUI` 的 `successHoldSeconds`、`fallbackCastLockSeconds`、`previewTimeoutSeconds` | 已完成 |
| A8 前进 | `Global/InputBinding` 加 `Movement` 的 `<Keyboard>/w`（主键位），删掉 `HandPush` 的 W、↑ 两行，A/D 与空格的说明改写；`Movement.autoThrottle`（doc 列）的注释改成“前进不再自动”，值仍为 1（按住时的油门）。没有新参数 | 已完成 |
| A7 AI | 删除 `Skill/ComboBinding`、`Skill/ComboInput` 和 `__enums__` 的 `ComboToken`；AI 表删掉搓招列（`SkillDifficulty.keyMistakeProbability`，`SkillTuning.keyMinSeconds / keyMaxSeconds / maxComboRetries`）和按技能的 `SkillOpportunity.backlashSeverity`、`SkillTuning.severeSeverityThreshold`、`SkillDifficulty.mildStopProbability`（`severeStopProbability` 改名 `stopProbability`）；补 `SkillTuning.backfireCost` 与五个 `yuanqi*` 列、`Personality.yuanqiAffinity`。导入 0 错误 | 已完成（ac3d5fe） |

完成标准：表是数值来源，阶段 A 的序列化默认值只作兜底；导入后 `_Reports/validation.md` 没有错误（2026-10-26 import 模式 0 错误，3 条警告是 dev 就有的 `HudObsession` 场景对象路径）；两组配表测试通过（在 EditMode 559/559 里）。

### 7.3 阶段 C：验证

按训练模式 → 单人赛 → 联机逐级推进，前一级通过再进下一级（训练模式优先，已定）。

| 级别 | 能验证 | 验证不了 | 状态 |
|---|---|---|---|
| 训练模式（0 个 AI，有木桩） | 单键释放、鼠标瞄准、施法串行、执念、反噬、同招加价；状态板、缘起触发与消耗、两种先后顺序；一念光明和逆姻缘的窗口；借助木桩验证命中、减速、反转和冲量 | AI 的缘起决策；远端客户端的预测表现（训练模式是本机 host）；黑幕落在别人屏幕上的效果 | 自动验证 13/13 通过（2026-10-26），A8 后重跑 14/14（2026-10-30），中途 review 与退役清理后再跑 14/14（2026-10-31，[训练模式验证记录](practice-verification.md)）；那里列出了没覆盖的点。2026-10-31 用户在编辑器里亲自试玩（fb22b0f9），没有发现问题；入口是单人设置面板的 “Practice (0 AI)” 按钮 |
| 单人赛（5 个 AI） | AI 施法和缘起决策；AI 受到缘起后的表现；控制叠加是否过分 | 远端客户端的预测表现 | harness 自动验证通过（2026-10-28，2026-10-28 Normal 一场：5 个 AI 施法 10–17 次、反噬 7 次、缘起 8 次、自然结算、0 运行错误，见 AI 技能文档末尾；2026-10-30 A8 后和 2026-10-31 中途 review 后各重跑一场，都自然结算）；“控制叠加是否过分”和手感要用户试玩 |
| 联机（两端） | 远端客户端的预测和 reconcile（冻结、突进、反噬限速）；观察者事件；缘起画面在各端是否一致；黑幕在对方屏幕上的效果 | — | 未做 |

每一级分开记录：施法请求、接受、执行、命中、效果恢复。训练模式的自动验证是受控夹具（反射改参数、传送、虚拟输入），不是自然比赛；用户亲自试玩之前，手感、数值和画面一律记为“未验证”。

## 8. 待定清单

还要用户答复或安排的：

- [ ] 反噬“反作用”：理解对不对，概率多少；2026-10-31 变体恢复后，它是被变体取代还是另外保留（6.2；没有实现）
- [ ] 反噬变体与惩罚的边界（6.2“变体与惩罚的边界”，现在按建议实现）：变体时长和 10 秒锁各自计时；被禁锢时变体照常放出；变体不进状态板、不参与缘起；复活时变体效果按普通复位清掉；变体不加执念、不另算冷却
- [ ] AI 的反噬风险要不要把变体代价加回来（A7 删掉了按技能的 `backlashSeverity` 和两档停手阈值）；现在仍是统一 `backfireCost`（建议先保持）
- [ ] 缘起效果（如 #2 的禁锢）算不算控制，是否计入受害者控制上限（第 9 节的受害者保护）
- [ ] 神足通被敌人的缓流覆盖共享加速槽之后，#2 / #4 “消耗神足通”对移动没有作用，#4 从减速后的上限起步：是否接受（[缘起架构设计](yuanqi-architecture.md) Q15）
- [ ] SoloCJK 字体子集重烘：“冻”“起”等字缺失，“冻结”和缘起名现在显示英文或 id（A6）。2026-10-31 补充：单人设置面板现有文字也缺 9 个字（三 五 六 四 固 格 种 装 零），“六人单人赛”“五种技能人格 · 固定配装”显示成方块；训练模式按钮缺“训练模式”，暂用英文 “Practice (0 AI)”
- [ ] 缘起特效（#2 结冰与冰块、#4 突进与撞击、#1 巨掌）：表现接口已留，美术未做
- [ ] 手部朝向指引的设计：瞄准点占位图标已做（A6，2026-10-31，圆环加十字，只给 owner），指引画什么（光标点还是手的实际朝向、要不要指向线）、美术和参数待定
- [ ] 占位值和新参数的试玩调整：#2 的 N、R，#4 的突进与 M，转手平滑，突进 FOV，反噬结束的 ×0.8（全部在配表或组件字段上）
- [ ] AI 瞄准容差（`AI/SkillDifficulty.aimToleranceDegrees` ±20° / ±12° / ±6°）要不要按真人改用鼠标后的命中水平重调：A7 没有改，要用户试玩数据（建议先保持）
- [ ] AI 的统一反噬代价和缘起决策参数（`AI/SkillTuning.backfireCost` 1、`yuanqiSetupMultiplier` 1.2、`yuanqiFollowUpScore` 0.8、`yuanqiFollowUpMultiplier` 1.5、`yuanqiFollowUpTempoSeconds` 0.3、`yuanqiLateWindowSeconds` 0.6，`SkillDifficulty.stopProbability` 1.01 / 0.7 / 0.45，`Personality.yuanqiAffinity` 1）都是建议（实现采用），按单人赛试玩再定
- [ ] AI 已接受的施法在机会过期时不撤回（真人也撤不回，撤回不退冷却）是建议（实现采用），用户没裁定
- [ ] 松开 W 后的滑行：没有刹车，只靠地面摩擦减速（训练模式实测 约 1.95–1.97 m/s²），按这个减速度从 80 m/s 滑到停约 41 秒、约 1.6 千米；要不要加松开减速或刹车，等试玩再定（建议先保持，已定的是“只撤推进力”）
- [ ] 是否入表（design-tables 决定 4，需用户确认）：鼠标瞄准平滑 `BuddahHandControl.mouseAimSmoothingSeconds` 0.06 秒和死区 `mouseAimDeadRadius` 1.5 米目前只是代码序列化默认值，按 7.2 的完成标准（代码里不再有阶段 A 的默认值作为数值来源）应进 `Skill/Push` basic 行（建议作 adv 列）；木桩复位阈值（`TrainingDummy.disturbedContactSpeed` 0.5 m/s 等）和表现、HUD 布局参数按范围线可以留在代码
- [ ] 神足通、缓流、神掌 SO 资产上的时长和冷却（10 / 12、10 / 12、冷却 18）和配表（3 / 5、6 / 8、12）不一致；运行时以表为准，只在无表时生效。改成表值，还是按设计表 README 的遗留清单删掉这些被表接管的字段（2026-10-31 退役清理提出，见 [退役记录](retired.md) 第 6 节）

已答复：

- [x] 缘起没有顺序规则；先后通过“谁被消耗”影响收益（已定）
- [x] 缘起在两招重叠的一瞬间触发，效果与收尾按各自描述（已定）
- [x] 缘起是开局登记的第三方粘合，原技能兼容被消耗和被改写（已定）
- [x] 缘起按需加入，不要求 15 对全覆盖（已定）
- [x] 一念光明、逆姻缘按施法者效果窗口判定“生效中”（已定）
- [x] 三连搁置（已定）
- [x] 一次重叠只触发一次；被消耗的技能不返还冷却和执念（已定）
- [x] 三招同时生效：设计上避免三招两两互有效果；两个持续型缘起可以同时生效（已定）
- [x] 各对缘起的措辞细节（名称、禁锢、突进、弹开方向）：[缘起技能设计](yuanqi.md) 的 Q1–Q9 于 2026-10-25 答复（已定）
- [x] 加价层数的清零规则：超过 5 秒或反噬时清零（已定）
- [x] 保留单技能冷却（已定）
- [x] 神足通时长：先按配表现值 3 秒（已定）
- [x] 键位与瞄准方式（已定；W/↑ 推掌暂留为废弃内容，2026-10-30 两条都删除）
- [x] 起跑交接改成完全物理：交接时把开场速度一次性交给刚体，之后不锁速度、不混合、不屏蔽输入（已定，2026-10-30）
- [x] 前进：不再自动，按住 W 推进、松开只撤推进力（不刹车）；起跑、倒计时、复活、禁锢、反转、UI 挡输入沿用原规则，推进力和限速沿用配表；AI 一直推进到完赛（已定，2026-10-30）
- [x] 阶段 A 新增参数先用序列化字段带默认值；阶段 A 不改配表结构（已定）
- [x] 训练木桩的数量、位置、物理、复位、身份、反馈（已定）
- [x] 训练模式入口：主菜单“单人游戏”的设置面板加第三个按钮 “Practice (0 AI)”，在“开始单人赛”和“返回”之间，按 0 个 AI 开局，走 `Match/ModeRule` 的 Practice 行（3 个木桩、玩家完赛即结束）；难度按钮对它没有作用（已定，2026-10-31，用户试玩后要求）
- [x] 反噬时本槽照常进冷却；惩罚期间复活，惩罚继续（已定）
- [x] 落后补偿本次不改（已定）
- [x] 反噬惩罚从 1 秒确认前摇结束时开始（已定）
- [x] 反噬前摇中被禁锢：照常进入惩罚，冷却不返还（已定）
- [x] 表现层这一轮只做接口和不需要美术的动作层，不做新特效（已定，2026-10-26）

## 9. 不在本次范围

- 驾驶只有 W 推进和 A/D 转向（2026-10-30 起前进不再自动），没有刹车、倒车或漂移。`space` 已绑定在 Movement 上但代码不读，可以留作以后的驾驶动作。
- 全局技能（逆姻缘、一念光明）对领跑者同样有利。
- 没有防御或反制手段；1 秒前摇的预警目前没有对应的反制机制。
- 缘起让控制更容易叠加（如 #2 的禁锢），受害者保护（控制上限、短暂免控）仍然需要设计。
- 三连（缘起生效期间放第三招打出终式）搁置，见 5.3。

## 附：缘起表

每一对的内容见 [缘起技能设计](yuanqi.md)，这里只列已定的对，避免两处不同步。

| # | 技能对 | 名称 | 收尾 | 缘起 id |
|---|---|---|---|---|
| 1 | 法相金身 + 如来神掌 | 金身巨掌 | 两招照常持续 | `giant_palm` |
| 2 | 神足通 + 禅定缓流 | 禅足冰封 | 两招都被消耗 | `frost_lock` |
| 4 | 神足通 + 法相金身 | 金足破阵 | 神足通被消耗，金身持续到结束 | `giant_charge` |
