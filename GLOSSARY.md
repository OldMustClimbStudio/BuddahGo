# BuddahGo

佛陀竞速中的技能和风险术语补充。比赛、参赛者与 AI 驾驶术语沿用 [CONTEXT.md](CONTEXT.md)；中文文档里的训练模式即 Practice，训练木桩即 Training Dummy。玩法规则和“已定 / 建议”的区分以 [Docs/gameplay/README.md](Docs/gameplay/README.md) 为准。

## Language

### 技能与施法

**技能配装（Skill Loadout）**：
参赛者赛前配置的三个技能槽，技能在比赛中按各自冷却重复使用。配装在比赛开始前固定，本场能触发哪些缘起也由它决定。
_Avoid_: 道具库存、弹药

**技能键（Skill Key）**：
真人玩家单键施法用的键：槽 0/1/2 同时绑定 Q/E/R 和 1/2/3（`InputSystem_Actions` 的 `SkillSlot1`–`SkillSlot3`），按下即请求施放该槽。手部朝向跟随光标投到地面的点（转手上限 180°/s），鼠标左键推左手、右键推右手；←/→ 转手只在没有鼠标时作后备。W 是前进键（见“推进”），W/↑ 推掌已删除（2026-10-30）。
_Avoid_: 搓招键、快捷栏

**搓招（Combo Input）**：
已退役（A7）。曾指在规定时间窗内按顺序完成的 W/↑ 手部输入组合，用来触发对应技能槽。现在真人用技能键，AI 直接调用 `SkillExecutor.CastSlotServer`（和 owner RPC 同一个施法门）；`ComboSkillInput` 和配表 `Skill/ComboInput`、`Skill/ComboBinding` 已删除，W/↑ 推掌的绑定也在 2026-10-30 删除。本词条只为读旧文档保留。
_Avoid_: 瞬间施法、仅播放前摇、用它描述真人施法

**推进（Drive）**：
让 Buddah 向前的推力开关（2026-10-30 起前进不再自动）。真人按住 W 推进、松开停止推进：松开只撤掉推进力，不刹车，靠地面摩擦滑行。AI 和训练木桩不按键，经转向覆盖（`ISteeringOverride`）的 `drive` 写同一个开关：AI 一直推进到完赛，木桩恒为关。开关进预测输入（`BuddahPredictedInputData.Throttle`，`BuddahDriveInput.ResolveThrottle`），倒计时等移动门关闭时一律为关。推进力和限速来自 `Movement` 表。
_Avoid_: 油门恒为 1、自动前进、刹车（现在没有刹车）

**施法串行（Cast Serialization）**：
技能之间没有全局施法锁，每个槽只受自己的冷却限制，所以两个技能可以同时生效；但任何时刻最多一个待确认施法：前一招还在确认前摇里时，请求任何槽都被拒绝（`CastStage.Rejected`），前一招执行、执念入账后才接受下一招。服务器按“待确认施法 → 反噬锁 → 禁锢 → 槽冷却”的顺序判定（`SkillCastRules.EvaluateGate`）。
_Avoid_: 全局施法锁、施法队列、castLock

**待确认施法（Pending Cast）**：
服务器已接受、正处在 1 秒确认前摇里的施法（`SkillExecutor.HasPendingCast`）。前摇是全场可见的读招窗口，结束时技能执行；已判定反噬的则改为开始惩罚。普通施法被禁锢打断时取消，并退还本槽冷却；已判定反噬的待确认施法不会被取消或打断，只有施法组件停止、或 AI 已不在比赛中时才会丢掉（被禁锢时不丢为已定（2026-10-26）；AI 放弃施法、复活、传送等普通取消也不丢，是建议（实现采用），见 [Docs/gameplay/README.md](Docs/gameplay/README.md) §6.2）。
_Avoid_: 排队施法、施法中

**禁锢（Freeze）**：
#2 禅足冰封施加给施法者半径内其他所有 Buddah（包括 AI 和训练木桩）的状态，代码和 HUD 里叫冻结（`Freeze` 预测修饰命令；HUD 显示“冻结”，字体缺字时显示 FROZEN）。禁锢期间推力和速度上限归零、转向锁死，不能施法、不能推掌，手部不转，动画定格。施加时会打断正在确认前摇的普通施法并退还本槽冷却；已判定的反噬照常进入惩罚。
_Avoid_: 定身（定身只清零推力和速度上限，不锁转向和动作）

### 房间

**房间（Room）**：
联机比赛前参赛者聚集的地方，由一名玩家（房主）创建、其他玩家加入；在菜单场景里表现为主殿内部，主殿门前是创建或加入房间的入口。
_Avoid_: 大厅、频道、Lobby（Lobby 只指 Steam 大厅）

**房主（Room Host）**：
创建房间的那名真人玩家，总坐在 1 号座位；房主离开即房间解散，所有人回到主菜单，不转给别人。
_Avoid_: Host、服务器、Owner

**座位（Seat）**：
房间里一名参赛者所在的固定位置，有自己的朝向；主殿内共六个，房主总在同一个座位上。
_Avoid_: 槽、Slot、站位
### 执念与反噬

**执念（Obsession）**：
决定反噬概率的累积值（`ObsessionFigure`）。技能执行时加上该技能的执念增量（遇到同招加价再乘倍率），平时每秒自然下降，落后越多下降越快；反噬惩罚期间冻结、不下降。反噬概率按执念走 sigmoid 曲线，HUD 把当前概率作为主要数字显示。
_Avoid_: 法力、技能余额

**反噬（Backfire）**：
按当前执念掷骰得到的统一惩罚，掷骰在服务器接受施法请求的那一刻。被判定反噬的施法照常播放 1 秒确认前摇（统一的反噬表现：`IsAnti` 动画触发和 `Anti_Reflection_Shared` / `_Local` 反馈事件），本槽照常进冷却，但技能不生效、不加执念、不计入同招加价。前摇结束时开始 10 秒惩罚：所有技能锁住，执念冻结，极速 ×0.85；结束时执念 ×0.8。复活和传送不解除惩罚，身上已有的 buff 和正在进行的缘起不受影响。参数在配表 `Skill/Obsession`（`backfireLockSeconds`、`backfireMaxSpeedMultiplier`、`backfireEndKeepRatio`）。代码里施法结果仍沿用旧名 `CastEvent.IsBacklash`。用户提到的“反作用”（反噬时身上 buff 有概率反转）待确认，没有实现。
_Avoid_: 反噬变体、`*_anti` 技能、技能拒绝（那是 `CastStage.Rejected`）

**同招加价（Same-skill Surcharge）**：
同一技能在自己冷却结束后 5 秒内再次施放时，本次执念增量 ×1.15，连续时复利叠加（×1.15 → ×1.3225 → ×1.52），与中间放没放别的技能无关。超过 5 秒才放、或发生反噬，层数清零；被取消和反噬的施法不计层数。参数在配表 `Skill/Obsession` 的 `repeatWindowSeconds`、`repeatGainMultiplier`；HUD 在冷却结束后的窗口里显示下一次的倍率和剩余秒数。
_Avoid_: 连发惩罚、冷却惩罚

**开局静默期（Opening Quiet Period）**：
比赛开始后执念从初始值自然下降、任何施法都是高风险的阶段；是共享规则的结果，不是 AI 规则。2026-10-04 初始值由 1000 调为 0 后该阶段不再出现，术语保留用于解读此前的记录。
_Avoid_: 禁放时间、开局保护

### 缘起

**缘起（Yuanqi）**：
两个技能的效果重叠时产生的组合效果。一个技能正在生效时，另一个与它组成缘起对的技能也进入生效（确认前摇结束、执行的那一刻），就触发这一对；没有顺序规则。“生效中”按施法者自己的效果窗口算（配表 `Skill.effectDurationSeconds`；一念光明为展开 1 秒后的保持期 10 秒，逆姻缘为反转的 10 秒）。判定只在服务器做。现有三对：#1 金身巨掌（法相金身 + 如来神掌）、#2 禅足冰封（神足通 + 禅定缓流）、#4 金足破阵（神足通 + 法相金身）。
_Avoid_: 连招、combo（易与搓招混淆）、三连

**缘起对（Yuanqi Pair）**：
组成一个缘起的两个技能（无序，表里记为 `skillA` / `skillB`）及其模式和消耗标志。每对是技能之外的第三方模块：一个 `Yuanqi_*` 类、一个 `.asset`、配表 `Skill/Yuanqi` 的一行，有参数时再加一张 `Skill/YuanqiEffect<对>` 家族表。开局按配装登记：只为两招都在本配装里的对建实例，配装里没有的组合不检测。一次重叠只触发一次，同一对要再触发，必须其中一招重新生效、形成新的重叠。
_Avoid_: 组合槽

**消耗（Consume）**：
缘起触发后提前结束某一招的剩余效果（连同它的运动修饰和特效）。消耗哪招由配表 `consumeA` / `consumeB` 决定，缘起模块只能通过技能提供的句柄（`ISkillRuntimeHandle.Consume`）做这件事。被消耗的技能不返还冷却和执念。#2 两招都消耗，#4 只消耗神足通，#1 都不消耗。
_Avoid_: 取消技能、打断

**持续型缘起（Sustained Yuanqi）**：
在两招都生效期间一直存在、任一招结束就撤掉改写的缘起（`YuanqiMode.Sustained`），不消耗任何一招；与之相对的瞬发型（`Instant`）只在触发那一刻生效一次。#1 金身巨掌是持续型：两招都在时，蓄力掌的冲量跟随当前体型（`IHandsImpulseScale.ImpulseFollowsScale`）。
_Avoid_: buff 型缘起、被动

**技能状态板（Skill State Board）**：
每个参赛者一个、只在服务器上的记录（`SkillStateBoard`），列出本参赛者正在生效的技能：技能 id、槽位、生效窗口和句柄。技能执行后由 `SkillExecutor` 统一登记，窗口到期、被消耗或复位时结束；缘起登记（`YuanqiRegistry`）订阅它的生效和结束事件来判定触发，不每帧轮询。状态板不同步给客户端；训练木桩不施法，没有状态板，但可以被缘起命中。
_Avoid_: buff 列表、技能队列

### AI 技能决策

**技能机会（Skill Opportunity）**：
当前自身状态、赛道或对手位置使某项技能可能有用的情境；出现机会不代表一定施放。
_Avoid_: 冷却结束必放

**技能人格（Skill Personality）**：
具名 AI 在固定三技能配装和施放偏好上形成的风格；驾驶能力由比赛难度另行决定。
_Avoid_: 驾驶人格、难度档位

**决策快照（Decision Snapshot）**：
服务器按固定间隔为全部参赛者建立的一份只读状态（进度、速度、状态标志、赛道曲率、可见的全局效果），供所有 AI 的技能决策共用。
_Avoid_: 每个 AI 自己扫描、每帧重算

**机会谓词（Opportunity Predicate）**：
每个技能一个的纯函数，根据决策快照给出机会分、目标与有效期；只回答"现在放有没有意义"，不回答"放不放"。
_Avoid_: 施放条件、触发器

**风险权重（Risk Weight）**：
难度决定的系数，乘以"当前反噬概率 × 统一反噬代价"后从价值中扣除（`AISkillDecision.Utility`：`riskWeight × P(反噬) × AI/SkillTuning.backfireCost`）；简单为 0。统一反噬后各技能的代价相同，所以每个难度只有一个停手阈值（`AI/SkillDifficulty.stopProbability`，P ≥ 阈值直接不放，对所有技能一样）；不再有按技能的反噬严重度。`backfireCost` 默认 1 是建议（实现采用）。
_Avoid_: 谨慎度、反噬惩罚

**动作承诺（Commitment）**：
AI 选定技能后，到施法结果为止不再更换技能的状态：选定（Decided）→ 下一 tick 像真人按键一样请求 `SkillExecutor.CastSlotServer` 并等待结果（WaitingConfirm）→ 结束（Resolved）；没有按键步骤。请求被拒绝、请求前时机过期、技能效果重置（复活）、完赛或离开比赛阶段、开场交接期间都会取消它；已被接受的待确认施法不因时机过期撤回（真人也不能撤回，取消也不退冷却）；被定身只阻止新的决策，不取消已有承诺；禁锢会取消 AI 待确认的普通施法（`CastStage.Cancelled`，退还本槽冷却），承诺随之取消。已判定反噬的待确认施法不随承诺取消，也不被禁锢打断（禁锢一条已定；普通取消不丢反噬是建议（实现采用），见 [Docs/gameplay/README.md](Docs/gameplay/README.md) §6.2）。
_Avoid_: 施法队列、锁定目标

**感知受损（Vision Impaired）**：
AI 因黑幕获得的服务器侧状态，表现为驾驶规划参数退化与决策反应延迟增加；对应真人屏幕被遮挡。
_Avoid_: AI 黑幕免疫、AI 看不见
