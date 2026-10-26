# 玩法深化：缘起、执念与操作改版

日期 2026-10-25。状态（`feat/mechanics-update` 0dbc551，PR #63）：阶段 A 的 A1–A6、阶段 B 和阶段 C 第一级已完成，2026-10-26 训练模式自动验证 13/13 通过（[训练模式验证记录](practice-verification.md)）；A7 AI 适配、阶段 C 第二、三级（单人赛、联机）和用户试玩还没做。2026-10-25/26 用户答复了第 8 节的大部分待定项，结论已改成“已定”。

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
| 手部朝向 | 鼠标控制；转手有速度上限，鼠标拉动带拖拽感，避免左右晃鼠标时手部抽搐 | 已定 |
| 键位 | 左手 A/D 转向；右手鼠标瞄准，鼠标左/右键推掌（左键左手、右键右手）；技能槽 0/1/2 同时绑 Q/E/R 和 1/2/3。W/↑ 推掌保留但已废弃，见本节末尾 | 已定 |
| 瞄准方式 | 光标投到地面取方向（赛车镜头在动，比鼠标横移更稳） | 已定 |
| 转手上限 | 先沿用 `Push.basic.rotationSpeedDegPerSec` 180°/s 加平滑，试玩再调；命中率上升后蓄力掌冲量和 buff 时长需要重调 | 已定 |

鼠标只换了输入来源：手部 yaw 仍由 owner 经 `BuddahHandControl.TrySendYawToServer` 同步给服务器，网络结构没变。没有鼠标、或光标射线打不到瞄准平面时，退回 ←/→ 转手（`HandRotation`）。鼠标只适用于 PC，以后支持手柄改用右摇杆。

`HandPush` 动作是 PassThrough 类型：每个键单独上报，只在按下时推掌。按住一个键再按另一个键，两只手都会推（Button 类型会做冲突裁决，第二个键会被吞掉）。光标停在可交互的 UI 上时，点击不推掌。

**废弃内容（2026-10-25，用户要求留尾巴）。** W/↑ 推掌（W 左手、↑ 右手）的代码和按键绑定暂时保留，但已不是主键位，主键位是鼠标左/右键；它也不再参与搓招。这部分属于废弃内容，后续可能再调整或删除。代码里有三处同样的注释：`BuddahHandControl.Awake` 里 `HandPush` 绑定的说明、`BuddahHandControl.TryResolvePushHand` 的 W/↑ 分支、`ComboSkillInput.OnHandPushPerformed` 的 W/↑ 映射（这个方法已不接线）。配表 `Global/InputBinding` 的 `status` 列也把这两条标为“废弃内容，后续可能再调整”。`InputSystem_Actions` 的 `HandPush` 绑定本身没法加注释：`.inputactions` 是 JSON，生成的 `InputSystem_Actions.cs` 也要和生成器输出一致。

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
| 反作用 | 用户提到反噬“有些许可能出现反作用”。理解为：反噬时，身上已有 buff 有一定概率反转（比如金身变缩小，巨掌跟着变小掌）。统一反噬后金身的缩小变体退役，这是唯一的“变小”来源 | 待确认理解与概率 |
| 六种 `_anti` 变体 | 由统一惩罚取代 | 已定 |

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

每一步写清：改哪里、新增、退役、网络与预测、训练模式怎么验证、完成标准。A1–A6 按实现写（0dbc551），A7 还是计划（代码位置按 0dbc551）。验证结果引用 [训练模式验证记录](practice-verification.md) 的场景编号 S0–S12。

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
| A7 | AI 适配，并最终退役搓招 | A2–A5；在单人赛验证 | 未做 |

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
| 改哪里 | 技能键：`SkillExecutor` 的 owner 绑定 `InputSystem_Actions.Player.SkillSlot1–3`，`OnSkillKeyPressed → RequestCast`；玩家不再经过 `ComboSkillInput.OnSkillSlotTriggered`（只有服务器 AI 还订阅它）。瞄准与推掌：`BuddahHandControl`，光标射线投到过 racer 高度的水平面取方向，`HandAimMath` 先平滑、再按 `rotationSpeedDegPerSec` 限速；`HandPush` 加鼠标左/右键。`PlayerBuddahInputSource` 没有改 |
| 新增 | `InputSystem_Actions` 的 `SkillSlot1–3`（Q/E/R 与 1/2/3，`.inputactions` 和生成的 `.cs`）和 `HandPush` 的 `<Mouse>/leftButton`、`<Mouse>/rightButton`；`BuddahHandControl` 的 `useMouseAim`、`mouseAimSmoothingSeconds`（0.06 秒）、`mouseAimDeadRadius`（1.5 米，随体型放大）、`mouseAimPlaneHeightOffset`，这几个值是建议（实现采用），试玩再调；转手上限沿用 `Push.basic.rotationSpeedDegPerSec` 180°/s |
| 退役 | 玩家侧的搓招：`ComboSkillInput` 的 owner 不再启用输入、不再监听 `HandPush`；HUD 搓招进度（槽位的 W/↑ token 运行时隐藏）。`ComboSkillInput` 本身保留：AI 还在用它逐键搓招（`AISkillCaster` / `AISkillCommitment` 经 `InjectServerKey`），等 A7 改完 AI 再删 |
| 网络与预测 | 手部 yaw 仍经 `BuddahHandControl.TrySendYawToServer` 同步，网络结构不变；施法请求仍走 `RequestCast → CastSlotServerRpc` |
| 训练模式验证 | 结果见 S1、S2：Q / 2 / R 放出槽 0、1、2，W 和 `W↑W` 都不施法；手部朝向到达光标目标 ±3°，最大转速 180°/s；左键只推左手、右键只推右手。“鼠标转手不抽搐”和“对木桩的神掌命中率比方向键明显提高”要用户试玩确认 |
| 完成标准 | 玩家侧不再经过搓招就能施法；单人赛里的 AI 仍能照常施法（AI 仍走搓招注入，单人赛还没验证） |

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

规则见 6.1、6.2（同招加价、加价清零、反噬惩罚、结束 ×0.8 已定；反作用待确认，没有实现）。

| 项 | 内容 |
|---|---|
| 改哪里 | `SkillExecutor`：`ResolveCastVariant` 只掷骰，不再换成 `_anti` 的 SkillAction；`QueueCast` 按请求的技能写冷却、算同招层数，前摇播普通或统一反噬表现（`PlayQueuedCastFeedbackObserversRpc`）；前摇结束时 `SkillCastRules.ResolveConfirmOutcome` 决定执行、反噬或取消。反噬走 `ExecuteBackfireServer`：不调用 `ExecuteServer`、不加执念、不计同招层数并清空全部层数，开始惩罚，owner 经 `BackfireSkillUiTargetRpc` 得知本槽冷却，最后发 `Executed`（`IsBacklash = true`） |
| 新增 | 反噬惩罚放在 `ObsessionFigure` 里，没有单独组件：同步的结束 tick（SyncVar）、`IsBackfireActive`、`BackfireSecondsLeft`、`BeginBackfireServer`、`MultiplyServer`、各端的 `BackfireStateChanged`；纯计算在 `ObsessionBackfireRules`。极速 ×0.85 是新的预测修饰 `MaxSpeedMultiplier`（`ApplyMaxSpeedMultiplierToOwner`）。同招加价 `SkillRepeatSurcharge`（`SkillCastRules.cs`）按技能 id 记层数，不按槽：锚点是这个技能上次正常执行的冷却结束时刻，请求时算层数，正常执行后才记账。参数 `repeatWindowSeconds` 5、`repeatGainMultiplier` 1.15、`backfireLockSeconds` 10、`backfireMaxSpeedMultiplier` 0.85、`backfireEndKeepRatio` 0.8 是 `ObsessionFigure` 的序列化字段，阶段 B 进 `Obsession` 表 |
| 退役 | 6 个 `Skill_*_Anti` 类和资产、`SkillAction.antiSkillId`、`ExecuteObservers` / `ExecuteLocal` 的 `isAnti` 参数、`BuddahHandControl.FireChargedProjectileBurstReplicated`、各技能运行时组件里的反噬分支和各自的反噬特效。缓流反噬的定身效果 `MovementRootThenAccelerationEffect` 保留，作为预测关闭时禁锢的兜底。配表里的 `_anti` 行已在阶段 B 删除（16121ed） |
| 网络与预测 | 极速 ×0.85 和其他运动修饰一样走预测桥接（服务器 + owner TargetRpc），技能重置不清（`BuddahPredictedModifierStateOps.ClearActiveSkillModifiers` 不动 `MaxSpeedPenaltyUntilTick`；`Movement/ModifierRule` 的 `resetOnSkillReset = false` 只是文档记录），复活 teleport 由 `PreservePersistentOnReset` 保留；执念和惩罚结束 tick 都是 `SyncVar`，各端按自己的时钟算剩余时间 |
| 训练模式验证 | 结果见 S4、S5、S6：增量 300 → 345 → 396.75，冷却结束超过 5 秒再放回到 300；反噬时技能不生效、执念不涨，三个槽都拒绝，极速 80 → 68，执念不动，中途复活惩罚仍在，正好 10 秒，结束时 ×0.8；已判定的反噬被禁锢时照常进入惩罚，冷却不退 |
| 完成标准 | 反噬只剩统一惩罚一种；观察指标：反噬后多久能放出第一招安全技能，超过 30 秒就考虑把 0.8 往下调（配表 `Obsession.backfireEndKeepRatio`），要试玩数据 |

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
| 退役 | 搓招进度（槽位的 W/↑ token 运行时隐藏）、全局施法锁的 UI（`LocalSkillUiEvent.CastLockSeconds`、2 秒假锁 `fallbackCastLockSeconds`）；`UI/UiTiming` 里 `SkillSlotUI` 的三行一并删除 |
| 计时口径 | 冷却和同招加价窗口按服务器执行时的 tick 定锚（`CastObserversRpc` / `BackfireSkillUiTargetRpc` 带 `serverTick`，`LocalSkillUiEvent.SecondsSinceServerEvent` = 本端估算的服务器 tick 减去它），不按 RPC 到达时刻，否则远端客户端会晚一个完整 RTT。和反噬、冻结计时一样以服务器时钟为准，远端只剩上行延迟（约 RTT/2）的偏差，方向安全：显示就绪后按下，请求到达服务器时冷却已经结束（本端估算服务器 tick 的误差只有一两个 tick，小于上行延迟）；窗口最后约半个 RTT 显示的加价实际不会收（对玩家有利）。host 与训练模式没有偏差 |
| 文字 | 中文只用 SoloCJK 字体子集里有的字（`InGameHudLabels`，`InGameHudTests` 检查）；“冻”“起”不在子集里，“冻结”显示 FROZEN，缘起名显示英文或 id，等子集重烘（第 8 节） |
| 训练模式验证 | 结果见 S11、S5、S6：槽位显示 Q/1、E/2、R/3，冷却 4.5s，“反噬 47%”（执念 495），惩罚中三个槽“反噬 9.6s”，冻结时 FROZEN |
| 完成标准 | 不看日志也能读出执念、反噬概率、加价窗口，以及本套配装的缘起；位置、字体和可读性要用户试玩确认 |

**表现层（已定，2026-10-26）。** 这一轮只做接口、不做新特效：每个新状态和事件在各端都能读到，美术以后从接口挂特效。接口有 `SkillExecutor.YuanqiObserved`（缘起触发，载荷含中心、半径、秒数、方向、目标 id）、`SkillExecutor.YuanqiImpactObserved`（#4 每次撞击）、`RacerSkillPresentationState.FrozenChanged / BurstChanged`、`ObsessionFigure.BackfireStateChanged`、`BuddahHandControl.TryGetAimGroundPoint`（准星地面点，不画准星）。不需要美术的“动作层”直接实现：冻结时该 racer 的 Animator 停住、双手不转；#4 突进时施法者相机有一次短 FOV 抖动（8 度，待试玩调）。细节见 [缘起架构设计 §4.7](yuanqi-architecture.md)。

#### A7 AI 适配（未做）

| 项 | 内容 |
|---|---|
| 现状 | AI 仍按 `ComboSkillInput` 逐键搓招，施法进的是和真人同一个闸门，所以已经受新规则约束：可以叠放技能、施法更频繁，反噬走统一惩罚。还没跟上的地方：`AISkillCaster` 收到任何 `Rejected` 都取消承诺；`AISkillTuning.BacklashSeverity` 还按六种反噬分别估代价；`AISkillWorld` 看不到反噬的 ×0.85 和突进；AI 不做缘起决策，配装里有成对技能时只会偶然触发；诊断 `AISkillEffectHarness` 的反噬用例还按旧的反噬效果写 |
| 改哪里 | `AISkillCaster` 不再按 `ComboSkillInput` 的按键序列逐键搓招（`AISkillCommitment`、`InjectServerKey`），直接调用 `SkillExecutor.CastSlotServer`；瞄准仍经 `BuddahHandControl.InjectServerRotation`，精度按真人用鼠标的水平重调 |
| 新增 | 缘起决策：本配装有哪些缘起（`YuanqiCatalog.ForLoadout`，读同一份 `SkillDatabase.GetYuanqis()`）；先放会被消耗的那招，在它结束前至少 1 秒请求另一招；两招的执念合计要按反噬概率估算风险 |
| 退役 | `ComboSkillInput` 和 `Skill/ComboBinding`、`Skill/ComboInput` 的读取方；`AISkillCommitment` 的按键步骤 |
| 文档 | `Docs/single-player/ai-skills.md` 和 `ai-personalities.md` 顶部已列出作废的前提（“任何两个技能不会同时生效”“逐键搓招”）；A7 时改写正文，补上缘起决策 |
| 验证 | 在单人赛验证（训练模式没有 AI）：五个 AI 能施法、能打出缘起、反噬次数合理 |
| 完成标准 | 代码里不再有搓招；AI 和真人走同一条施法路径 |

### 7.2 阶段 B：配表对接

阶段 A 稳定后做，已完成（16121ed），A7 的表改动随 A7 做。每项都按 [design-tables README](../design-tables/README.md) 的流程：改 xlsx 表头（`##var`、`##type`、`##comment`）→ `python Tools/design-tables/gen_records.py .` 重新生成记录类和 `export-map.json` → 组件里接 `DesignTableOverlay.Apply` / `ApplyMapped` → 补跨表校验规则 → 导入 → 更新 `Docs/design-tables` 对应的 inventory 文档 → 跑 `DesignTableTests`、`DesignTableRuntimeTests`。阶段 A 的序列化字段默认值就是表里的初值；接上后表是数值来源，组件和 SO 上的值只在没有表时兜底（design-tables 决定 14：覆盖而非重构）。退役的字段和 sheet 从表里删掉，不留无效列。

| 来自 | 配表与映射 | 状态 |
|---|---|---|
| A1 木桩 | 并进 `Match/ModeRule`：五列 `trainingDummyCount`、`trainingDummySpacingMeters`、`trainingDummyStartOffsetMeters`、`trainingDummyLateralOffsetMeters`、`trainingDummyResetDelaySeconds`（Practice 行 3 / 15 / 90 / 10 / 3，Online / Solo 的数量填 0）。`MatchSpawnManager` 只读 Practice 行（`TrainingDummyModeRuleMap`）；是否生成仍由代码里的 `IMatchRules.SpawnTrainingDummies` 决定 | 已完成 |
| A2 操作 | `Push.rotationSpeedDegPerSec` 的 `##comment` 改成“鼠标转手速度上限”（方向键回退和 AI 也用它）；键位不做成可配，文档表 `Global/InputBinding` 列出 Q/E/R + 1/2/3、鼠标左/右键推掌、←/→ 回退，W/↑ 标为废弃 | 已完成 |
| A3 节奏 | 删除 `Skill.castLockSeconds`（没有改成全局短间隔） | 已完成 |
| A4 反噬 | 删除 `Skill` 表 6 行 `_anti` 和 `antiSkillId` 列；删掉 `SkillEffect*` 的反噬行与反噬列、`Push` 的 `charged_anti` 行和它专用的列、`SkillConstant`、`SkillSelectionRule`（Hidden）、`SkillPresentation`、`FeelEvent` 的反噬行；保留 `Anti_Reflection_*` 表现、`VfxRegistry` 的反噬 id、`__enums__` 里 `SkillId` 的反噬值和 `UI/Text` 的反噬文案 key。`Obsession` 新增 `repeatWindowSeconds` 5、`repeatGainMultiplier` 1.15、`backfireLockSeconds` 10、`backfireMaxSpeedMultiplier` 0.85、`backfireEndKeepRatio` 0.8。依赖 `antiSkillId` 的校验规则一起改 | 已完成 |
| A5 缘起 | 新增 `Skill/Yuanqi`（缘起 id、两招、`consumeA / consumeB`、`mode`、`retriggerPolicy`、`isEnabled`、文案 key）和家族表 `YuanqiEffectFrostLock`（N、R、是否冻木桩）、`YuanqiEffectGiantCharge`（突进限速、回落秒数、M）；`__enums__` 加 `YuanqiId`、`YuanqiMode`、`YuanqiRetrigger`；`UI/Text` 加 `yuanqi.<id>.name / .desc`。#1 没有参数，不建家族表。表结构见 [缘起架构设计 §3](yuanqi-architecture.md) | 已完成 |
| 预测修饰 | `ModifierCommandType` 加 `Freeze`、`EndModifier`、`Burst`、`MaxSpeedMultiplier`，`ModifierRule` 加四行，`StackPolicy` 加 `Restart`；`StructuralConstant.prediction_protocol_version` = 2 | 已完成 |
| A6 HUD | `UI/UiTiming` 删掉 `SkillSlotUI` 的 `successHoldSeconds`、`fallbackCastLockSeconds`、`previewTimeoutSeconds` | 已完成 |
| A7 AI | 删除 `Skill/ComboBinding`、`Skill/ComboInput`；AI 人格表里和搓招有关的列（如每键失误率）删除，补缘起决策参数 | 未做（随 A7） |

完成标准：表是数值来源，阶段 A 的序列化默认值只作兜底；导入后 `_Reports/validation.md` 没有错误（2026-10-26 import 模式 0 错误，3 条警告是 dev 就有的 `HudObsession` 场景对象路径）；两组配表测试通过（在 EditMode 559/559 里）。

### 7.3 阶段 C：验证

按训练模式 → 单人赛 → 联机逐级推进，前一级通过再进下一级（训练模式优先，已定）。

| 级别 | 能验证 | 验证不了 | 状态 |
|---|---|---|---|
| 训练模式（0 个 AI，有木桩） | 单键释放、鼠标瞄准、施法串行、执念、反噬、同招加价；状态板、缘起触发与消耗、两种先后顺序；一念光明和逆姻缘的窗口；借助木桩验证命中、减速、反转和冲量 | AI 的缘起决策；远端客户端的预测表现（训练模式是本机 host）；黑幕落在别人屏幕上的效果 | 自动验证 13/13 通过（2026-10-26，[训练模式验证记录](practice-verification.md)）；那里列出了没覆盖的点 |
| 单人赛（5 个 AI） | AI 施法和缘起决策；AI 受到缘起后的表现；控制叠加是否过分 | 远端客户端的预测表现 | 未做（等 A7） |
| 联机（两端） | 远端客户端的预测和 reconcile（冻结、突进、反噬限速）；观察者事件；缘起画面在各端是否一致；黑幕在对方屏幕上的效果 | — | 未做 |

每一级分开记录：施法请求、接受、执行、命中、效果恢复。训练模式的自动验证是受控夹具（反射改参数、传送、虚拟输入），不是自然比赛；用户亲自试玩之前，手感、数值和画面一律记为“未验证”。

## 8. 待定清单

还要用户答复或安排的：

- [ ] 反噬“反作用”：理解对不对，概率多少（6.2；没有实现）
- [ ] 缘起效果（如 #2 的禁锢）算不算控制，是否计入受害者控制上限（第 9 节的受害者保护）
- [ ] 神足通被敌人的缓流覆盖共享加速槽之后，#2 / #4 “消耗神足通”对移动没有作用，#4 从减速后的上限起步：是否接受（[缘起架构设计](yuanqi-architecture.md) Q15）
- [ ] SoloCJK 字体子集重烘：“冻”“起”等字缺失，“冻结”和缘起名现在显示英文或 id（A6）
- [ ] 缘起特效（#2 结冰与冰块、#4 突进与撞击、#1 巨掌）：表现接口已留，美术未做
- [ ] 占位值和新参数的试玩调整：#2 的 N、R，#4 的突进与 M，转手平滑，突进 FOV，反噬结束的 ×0.8（全部在配表或组件字段上）
- [ ] 是否入表（design-tables 决定 4，需用户确认）：鼠标瞄准平滑 `BuddahHandControl.mouseAimSmoothingSeconds` 0.06 秒和死区 `mouseAimDeadRadius` 1.5 米目前只是代码序列化默认值，按 7.2 的完成标准（代码里不再有阶段 A 的默认值作为数值来源）应进 `Skill/Push` basic 行（建议作 adv 列）；木桩复位阈值（`TrainingDummy.disturbedContactSpeed` 0.5 m/s 等）和表现、HUD 布局参数按范围线可以留在代码

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
- [x] 键位与瞄准方式（已定；W/↑ 推掌暂留为废弃内容）
- [x] 阶段 A 新增参数先用序列化字段带默认值；阶段 A 不改配表结构（已定）
- [x] 训练木桩的数量、位置、物理、复位、身份、反馈（已定）
- [x] 反噬时本槽照常进冷却；惩罚期间复活，惩罚继续（已定）
- [x] 落后补偿本次不改（已定）
- [x] 反噬惩罚从 1 秒确认前摇结束时开始（已定）
- [x] 反噬前摇中被禁锢：照常进入惩罚，冷却不返还（已定）
- [x] 表现层这一轮只做接口和不需要美术的动作层，不做新特效（已定，2026-10-26）

## 9. 不在本次范围

- 驾驶仍只有转向：油门恒为 1（`PlayerBuddahInputSource.GetThrottle`），没有刹车或漂移。`space` 已绑定在 Movement 上但代码不读，可以留作以后的驾驶动作。
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
