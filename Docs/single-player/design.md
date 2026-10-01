# 单机模式设计

- 术语以根目录 [CONTEXT.md](../../CONTEXT.md) 为准。
- 不可轻易回退的决策见 [Docs/adr/](../adr/)。
- 分阶段目标见 [phases.md](phases.md)；执行入口见 [HANDOFF.md](HANDOFF.md)。
- **事实基线**：`dev` 已合入架构重构（PR #47–#58）。本文行号以合并后的代码为准，仅作定位提示，以符号名为准。

## 1. 目标与功能清单

**Solo Match**：一名 Human Player 对 0–5 名 AI Racer，完全离线（Steam 可以不运行），一局的流程与 Online Match 一样完整。AI 数量为 0 时称为 **Practice**。

| # | 功能 | 归属阶段 |
|---|---|---|
| F1 | 没有 Steam 也能启动游戏并进入主菜单；联机入口提示"Steam 不可用" | S1 |
| F2 | 主菜单"单人游戏" → 单机设置（AI 数量 0–5、难度）→ 选择 → 比赛 → 结算 | S1 |
| F3 | 选择阶段没有超时，跳过地图投票 | S1 |
| F4 | 单机下全员完赛立即结算；Practice 中玩家冲线即结算 | S1 |
| F5 | 结算：总用时、每圈 Lap Time、名次（Practice 不显示名次）；按钮为"再来一局"和"返回" | S1 / S2 |
| F6 | Esc 弹出"退出到主菜单？"，确认后结束对局 | S1 |
| F7 | AI 作为 Racer 完整参赛：出发、计圈、完赛、名次、观战、排行榜名字 | S2 / S3a |
| F8 | AI 往前推演转向，表现保留滑稽感；三档难度，Fumble 单独调节 | S1.5 / S4 |
| F9 | AI 卡住时自动恢复（Stuck Recovery） | S3a |
| F10 | AI 按规则施放技能，执念值和反噬规则与玩家一致 | S3b / S5 |
| F11 | 玩家冲线后可以跳过观战（Skip Spectating） | S6 |
| F12 | 其他 Racer 显示头顶名字，小地图显示对手（占位美术） | S6 |
| F13 | 暂停菜单 | S7（后续） |

## 2. 决策汇总

| 主题 | 结论 |
|---|---|
| 形态 | 对战 AI，派对向；Practice 是 0 个 AI 的 Solo Match |
| 技术路线 | 用 Yak 起离线 host，复用全部服务器权威代码（ADR 0001）。纯 host 会话里没有 reconcile 和回放，联机远端那类预测问题不会出现 |
| Steam | 修改 FishyFacepunch，让它在初始化时容忍 Steam 缺失（ADR 0004） |
| 身份 | 比赛数据按 RacerId 区分：真人 = ClientId，AI = 10000 + 序号（ADR 0002） |
| 流程 | 全部保留；去掉投票、选择超时、地图投票；再来一局回到选择场景，保留 AI 数量、难度和名字；返回回到主菜单首页 |
| 结束 | 沿用"第一名冲线后 15 秒"；单机下全员完赛立即结算；跳过观战等同于倒计时立即到期 |
| 计时 | 服务器用 Match Clock 为每个 Racer 记录开赛、每次过线和完赛的时刻 |
| AI 数量和难度 | AI 0–5 个，默认 3 个；难度分简单、普通、困难，只决定规划精度，不设圈速目标 |
| AI 驾驶 | 与玩家使用同一个预测运动组件；只输出 -1/0/+1；往前推演做转向规划；决策层可替换（ADR 0003）；路线用赛道样条线 |
| AI 不完美 | 五个旋钮：反应延迟、推演时长、重新规划间隔、候选分辨率（这四个由难度决定），加 Fumble（独立）；另有走线偏移。追赶机制只调决策层，不改物理 |
| AI 技能 | 配装复用 `LoadoutRules`，在合法候选中随机选；每个技能一条施放时机规则；遮挡视野会降低 AI 的规划精度 |
| AI 卡住 | 服务器侧检测：样条线进度连续 N 秒没有前进，或逆行超过 M 秒，就复活到赛道上（阈值可配置） |
| 观战 | 沿用现有规则（跟随排名最高、还没完赛的 Racer），改为按 RacerId 查找；不提供切换 |
| UI | 头顶名字、小地图对手标记由 Match Rules 控制：单机开启，联机保持现状 |
| 美术 | 4–6 人开场布局、AI 皮肤区分、头顶名字、小地图标记都先用占位符 |
| 暂停 | 本期预留 Match Clock 和 Esc 确认框；暂停菜单放到 S7 |
| 不做 | 存档、Steam 邀请处理之外的联机交互、手绘赛车线、联机房间 AI 补位、改名、经验公式规划器 |

## 3. 模块架构

### 3.1 分层与依赖规则

```text
┌──────────────── Presentation（UI / 相机 / 占位表现）────────────────┐
│ SoloSetupPanel · SoloResultsView · QuitConfirmDialog · SkipSpectateButton │
│ RacerNameTag · MiniMapRacerMarkers                                  │
└───────────────▲ 只读：订阅事件或查询 ─────────────────────────────────┘
┌──────────────── Match（对局编排）────────────────────────────────────┐
│ SessionLauncher · IMatchRules(Online/Solo) · SoloMatchSettings      │
│ MatchClock · RaceTiming · RaceEndPolicy                             │
└───────────────▲ ─────────────────────────────────────────────────────┘
┌──────────────── Racers（参赛者）─────────────────────────────────────┐
│ RacerRegistry · RacerIdentity · ServerProgressReporter              │
└───────────────▲ ─────────────────────────────────────────────────────┘
┌──────────────── AI（只在服务器运行）────────────────────────────────┐
│ AIRacerDriver(组件) → ISteeringPlanner(ForwardSimPlanner)           │
│   ↳ BuddahMotionModel · IRacingLine(SplineRacingLine) · AIDifficultyProfile │
│ AISkillCaster → IAISkillRule[] · StuckDetector                      │
└───────────────▲ ─────────────────────────────────────────────────────┘
┌──────────── 现有 Gameplay / Prediction / Session（重构后）─────────────┐
│ BuddahPredictedMotor · SkillExecutor · RoomStateManager · LeaderboardManager … │
└──────────────────────────────────────────────────────────────────────┘
```

**依赖规则**

1. 依赖只能自上而下。现有系统通过本文定义的**接缝**（§3.3）获取 AI 输入、规则和 Racer 信息，不直接引用 AI 或 Solo 的具体类。
2. **纯逻辑与 Unity/网络分离**：规划器、运动模型、技能规则判定、卡死判定、计时计算、Match Rules 都是不依赖 NetworkBehaviour 的普通类，放进 `BuddahGo.Tests` 写 EditMode 测试。
3. **AI 只在服务器运行**：AI 组件在 `!IsServerInitialized` 时保持禁用，客户端上不执行任何 AI 逻辑。
4. **不在运行时添加 NetworkBehaviour**（见 `Docs/networking.md`）：AI 组件是普通 MonoBehaviour，预先放在 `Buddah.prefab` 上，默认禁用，由服务器在指定 AI 角色时启用。
5. **联机行为不变**：所有单机差异都通过 `IMatchRules` 查询，联机实现返回现有行为。

**程序集与目录**

所有模块都在 `BuddahGo.Runtime` 程序集内，按目录和命名空间划分：

| 目录 | 命名空间 |
|---|---|
| `Assets/Scripts/Match/` | `BuddahGo.Match` |
| `Assets/Scripts/Racers/` | `BuddahGo.Racers` |
| `Assets/Scripts/AI/` | `BuddahGo.AI` |
| `Assets/Scripts/UI/Solo/` | 沿用 UI 现有命名空间 |

AI 模块只依赖下层，因此以后可以拆成独立程序集；本期不拆。

### 3.2 模块清单

| 模块 | 类型 | 职责 | 主要接口 | 复用场景 |
|---|---|---|---|---|
| `SessionLauncher` | 普通类，由 GameNetworkManager 持有 | 选择传输层（Multipass：FishyFacepunch / Yak），启动和停止联机 host、联机 client、单机 host；单机返回主菜单时完整关闭网络 | `StartOnlineHost()`、`StartSoloHost(SoloMatchSettings)`、`StopSession()` | 以后的本地测试、LAN 模式 |
| `IMatchRules` + `OnlineMatchRules` / `SoloMatchRules` | 普通类 | 集中描述两种模式的差异：投票、选择超时、地图投票、提前结算、跳过观战、Esc 退出、暂停、头顶名字、小地图对手 | 一组只读布尔/数值属性 | 新增模式时只加一个实现 |
| `SoloMatchSettings` | 不可变数据 | AI 数量、难度、AI 名字列表（含"再来一局"时沿用） | — | 调参场景、自动化测试 |
| `MatchClock` | 服务器权威的普通类加只读同步 | 比赛内唯一的计时源；可暂停（S7 启用） | `Now`、`IsPaused`、`Pause()`、`Resume()` | 暂停、所有比赛计时器 |
| `RaceTiming` | 服务器侧，按 RacerId 记录 | 记录开赛时刻、每次过线时刻、完赛时刻，计算 Lap Time 和总用时 | `OnRaceStarted()`、`OnLapCompleted(racerId, lap)`、`GetLapTimes(racerId)` | 联机结算以后也可以显示圈速 |
| `RaceEndPolicy` | 普通类 | 根据 `IMatchRules` 和 Racer 状态，决定何时结算：15 秒、全员完赛、跳过观战 | `ShouldEndNow(state)` | — |
| `RacerRegistry` | 服务器权威，客户端只读 | RacerId 到（Buddah 对象、显示名、类型 Human/AI、所属连接）的映射 | `Register`、`TryGet(racerId)`、`All`、`GetByObject` | 一切按 Racer 查询的系统、联机 AI 补位 |
| `RacerIdentity` | 挂在 Buddah 上的同步字段 | 让每台 Buddah 知道自己的 RacerId（客户端也能查到） | `RacerId`、`IsAI` | UI、观战、排行榜 |
| `ServerProgressReporter` | 服务器侧 | 为 AI 在服务器上计算样条线进度和计圈，并调用现有的进度/完赛登记入口；真人仍走 owner 上报 | `Tick()` | 以后服务器权威计圈 |
| `AIRacerDriver` | MonoBehaviour，在 prefab 上，默认禁用 | 每 tick 从 `ISteeringPlanner` 取得 -1/0/+1，交给 motor 的转向接缝 | `CurrentSteering` | 自动驾驶测试（S1.5）、联机 AI 补位 |
| `ISteeringPlanner` / `ForwardSimPlanner` | 纯逻辑 | 往前推演候选按键序列，选出评分最高的序列（ADR 0003） | `Plan(MotionState, IRacingLine, Profile) → int` | 将来可加经验公式实现 |
| `BuddahMotionModel` | 纯逻辑 | 与 motor 一致的平面运动公式（推力、力矩、惯量、松键衰减、速度上限），复用 `BuddahLocomotionStep.Compute` | `Step(state, steering, stats, dt)` | 规划器、调参工具、测试 |
| `IRacingLine` / `SplineRacingLine` | 纯逻辑包装 | 给出前方目标点、切线和偏离距离；内置可配置的走线偏移 | `Sample(distance)`、`Project(pos)` | 将来的手绘赛车线 |
| `AIDifficultyProfile` | ScriptableObject | 三档难度的精度旋钮、Fumble、走线偏移、追赶系数、卡死阈值 | — | 调参场景 |
| `AISkillCaster` + `IAISkillRule` | 服务器侧组件加纯逻辑规则 | 按技能 id 查规则，判定时机后调用服务器施法入口 | `IAISkillRule.ShouldCast(context)` | 联机 AI 补位 |
| `StuckDetector` | 纯逻辑 | 判断进度停滞或长时间逆行 | `Update(progress, forwardDot, dt) → bool` | 也可以给真人用 |
| `AITuningHarness` | 调参场景和工具 | 让 AI 自动跑 N 圈、统计数据，并能施加干扰 | — | 回归和性能测量 |
| Solo UI | Presentation | 设置面板、结算视图、退出确认、跳过观战按钮、头顶名字、小地图标记（占位） | — | 头顶名字和小地图标记联机也能开启 |

### 3.3 与现有代码的接缝

只在下列位置改动现有系统，每处都只是"加一个接缝"，不重写原逻辑：

| 接缝 | 位置 | 做法 |
|---|---|---|
| 转向来源 | `BuddahPredictedMotor.BuildReplicateData`（`BuddahPredictedMotor.cs:284`，读转向在 `:292`） | 当 `!Owner.IsValid && IsServerInitialized` 且 `AIRacerDriver` 启用时，改读 AI 给出的转向值，否则走原来的 owner 输入桥。油门本来就固定为 1 |
| 生成 | `MatchSpawnManager`（`:97`、`:117`、`:123`） | 真人生成后，按 `SoloMatchSettings` 生成 AI：无 owner、分配 RacerId、出生点序号接在真人之后、调用 `SkillLoadout.SetSlotsServer`（`:42`） |
| 出发交接 | `RaceBodyIntroStateController`（`:301`、`:353` 会把非本地 owner 的车设为 kinematic）；`TryApplyServerAuthoritativeLaunchHandoff`（`BuddahPredictedMotor.Events.cs:175`） | 无 owner 的车在服务器上走权威交接路径，不设 kinematic |
| 进度与完赛 | `PlayerProgressReporter`（`:28` 有 IsOwner 门槛，ServerRpc 在 `:74`）、`RaceCompletionTracker`（`:61`、`:248`）、`LapProgress`（只在 owner 端计圈） | AI 由 `ServerProgressReporter` 在服务器上计算进度和计圈，再调用同一套登记入口 |
| 排行榜 | `LeaderboardManager._progressByClientId`（`:21`）、`RankEntry`（`:350`） | 键改为 RacerId；`RankEntry.ClientId` 改名为 `RacerId`（线上格式不变）；显示名取自 RacerRegistry，不再用 `gameObject.name #OwnerId`（`PlayerProgressReporter.cs:204`） |
| 完赛 | `RaceFinishManager._finishedClientIds`（`:27`、`:120-127`）、15 秒倒计时（`:76-84`） | 键改为 RacerId；结算时机交给 `RaceEndPolicy` |
| 结算演出 | `MatchResultPresentationCoordinator`（`:47`、`:119`、`:248-254`） | 键改为 RacerId |
| 观战 | `RaceSpectatorTargetResolver`（`:45-51`）、`CinemachineLocalPlayerFollower`（`:60-72`） | 按 RacerId 查找目标，规则不变 |
| 开场排序 | `IntroSequenceManager` 的排序比较函数（`:491-500`） | 按 RacerId 排序，真人在前 |
| 施法 | `SkillExecutor.CastSlotServerRpc`（`:176`）已经拆成 `TryResolveCast`（`:185`）、`ResolveCastVariant`（`:224`）、`QueueCast`（`:261`） | 新增服务器侧入口 `CastSlotServer(slot)`，复用这三步，冷却、执念值、反噬判定与 RPC 路径完全一致 |
| owner 表现 | `Apply*ToOwner` 的 TargetRpc（`SkillExecutor :350-417`，只判断 `conn == null`，EmptyConnection 仍会发送） | 无 owner 时不发 TargetRpc；拖尾、缩放、Feel 改为由 observers 路径在 host 上播放 |
| 复活 | `BuddahRespawn`（`IsLocalOwner` 在 `:226-231`，门槛在 `:85`、`:132`）；服务器传送 `TryApplyServerAuthoritativeTeleport`（`Events.cs:256-270`）；`RequestResetActiveSkillEffectsForRespawn` 有 IsOwner 门槛（`SkillExecutor :114`） | AI 的复活和 Stuck Recovery 在服务器上直接调用权威传送和 `ResetActiveSkillEffectsForOwner`（`:139`） |
| 执念值追赶 | `ObsessionFigure`（`:114` 用 `entry.ClientId == OwnerId`） | 改为按 RacerId 查找 |
| 名字 | `RoomStateManager.SubmitLocalDisplayName`（`:606`，Steam 无效时直接 return）、`PlayerIdentity.ResolvePlayerName`（回退为 `Player {id}`） | 单机里真人用 Steam 名，没有就用回退名；AI 名字来自配置列表 |
| 选择阶段 | `PropertiesSelectionManager`（阶段在 `:563-568`，超时 60 秒） | 单机下跳过地图阶段，取消超时；皮肤阶段保留（目前本来就只是占位） |
| 结算决策 | `ResultDecisionManager`（参与者取自 `RoomStateManager.Players`，`:191`） | 单机下用"再来一局 / 返回"按钮替代投票 |
| 菜单 | `MainMenuUI`（`:215-224` 按 IsInLobby 切换面板） | 首页新增"单人游戏"入口 |

## 4. 关键流程

### 4.1 开始一局 Solo Match

```text
主菜单「单人游戏」→ SoloSetupPanel（AI 数量、难度；默认取上次的选择）
  → SessionLauncher.StartSoloHost(settings)：Multipass 选 Yak，启动 server+client
  → RoomStateManager 以单人房间直接开始（不需要 ready，不需要大厅）
  → 选择场景：技能配装 → 皮肤（占位）；没有超时，跳过地图投票
  → RaceMap：MatchSpawnManager 生成真人和 N 个 AI（RacerRegistry 登记）
  → Intro：开场布局按 Racer 数量选取（4–6 人用占位布局）；服务器权威交接
  → GO：MatchClock 开始计时，RaceTiming 记录开赛时刻
```

### 4.2 比赛中，每个服务器 tick

```text
AIRacerDriver：读取运动状态 → ForwardSimPlanner.Plan → 写入转向 (-1/0/+1)
BuddahPredictedMotor：在 BuildReplicateData 里取得 AI 转向 → 正常模拟（与玩家同一套物理）
ServerProgressReporter：更新 AI 的进度；检测到过线时调用 RaceTiming.OnLapCompleted
StuckDetector：判定卡死 → 服务器权威传送，并重置技能效果
AISkillCaster：按规则判定 → SkillExecutor.CastSlotServer
真人：照旧由 owner 上报进度；服务器观察到圈数增加时调用 RaceTiming.OnLapCompleted（精度约 ±0.1 秒，取决于上报间隔）
```

### 4.3 结束、跳过观战、退出、再来一局

```text
RaceEndPolicy：
  联机 → 第一名冲线后 15 秒
  单机 → 15 秒，或者全员完赛（取先到者）；Practice 中玩家冲线即结束
  跳过观战 → 立即结束，未完赛的 Racer 按"倒计时到期"的规则排名
结算：SoloResultsView 显示名次（Practice 不显示）、总用时、每圈 Lap Time
  再来一局 → 回到选择场景，SoloMatchSettings 和 AI 名字沿用，AI 配装重新随机
  返回     → SessionLauncher.StopSession() → 主菜单首页
Esc → QuitConfirmDialog → 确认后同"返回"（不结算）
```

## 5. 各部分设计要点

### 5.1 离线启动（S1）

**N3 现状**
- `FishyFacepunch.Initialize` 里的 `SteamClient.Init` 会抛出 `NoSteamClient`（`FishyFacepunch.cs:98-101`）。
- 异常发生在 `NetworkManager.InitializeComponents` 的 TransportManager 一步（`:361`），之后的 Client/Server/Scene/Observer/PredictionManager 都不会初始化。
- `Multipass.Initialize` 会逐个初始化它下面的所有传输层（`:150-161`）。

**要求**
- 必须先完成 ADR 0004 的容错，Yak 才能用。
- 容错后，`SteamClient.IsValid == false` 时：联机入口禁用并给出提示；单机正常运行。

**单人房间与身份**
- 单人房间的 host 身份已由 #48（N1）修正：没有大厅时，"本地连接就是 host"。
- 名字用 `PlayerIdentity` 的回退值。

**关闭会话**
- 单机返回主菜单时，必须停止 server 和 client，并清理 `RoomStateManager`（它是 global 对象，跨场景存在）、`SoloMatchSettings` 和静态缓存。
- 之后再开单机或联机，都不能残留状态（V8）。

**Steam 邀请**
- 单机进行中收到的 Steam 邀请先挂起，回到主菜单后再提示。

### 5.2 Racer 与计时（S2）

- **RacerRegistry 必须新建**。现有的 `PlayerRegistry` 只是 `BuddahMovement` 的列表，没有 id 索引。
- **计时统一用 Match Clock**。现状是完赛时间用 `Time.unscaledTimeAsDouble`（`RaceFinishManager.cs:125`），而 GO 时间用 tick 换算出的网络时间（`IntroSequenceManager :201`、`IntroTimeUtility.cs:39-47`），两者不能相减。
  - 开赛、过线、完赛统一用 Match Clock 记录。
  - `RankEntry.FinishServerTime` 的含义保持不变，联机界面照旧使用它。
- **联机兼容**：`RankEntry` 的字段只改名，不改类型和顺序。新增的计时数据放在 RaceTiming 自己的同步结构里，不改动 `RankEntry` 的布局。

### 5.3 AI 驾驶（S1.5 / S4）

**运动事实（规划器必须遵守）**

| 参数 | 值 | 位置 |
|---|---|---|
| 质量 | 2 | `Buddah.prefab:572` |
| 线性阻力 | 0，横向速度不会衰减 | `Buddah.prefab:573` |
| 角阻力 | 0.05 | `Buddah.prefab:574` |
| 刚体约束 | 锁 X、Z 旋转 | `Buddah.prefab:589` |
| 推进 | 沿水平朝向施加 `FinalForwardForce × throttle` | `BuddahLocomotionStep.Compute`（`:37`），motor 在 `:460` 调用 |
| 转向 | 按住时 `AddTorque(steering × FinalTurnTorque)` | motor `:476` |
| 松键衰减 | 角速度按 `TurnDecayPerSecond` 线性衰减 | motor `:481-485`，`Mathf.MoveTowards` |
| 速度上限 | `ClampPlanarSpeed(FinalMaxSpeed + 推人宽限期 6)` | motor `:429`，定义在 `:671` |
| 默认配置 | 50 / 30 / 3 / 80 | `BuddahPredictedMotorConfig.cs:9-12`，prefab `:606-609` |
| 玩家输入 | 数字量，A/D 只能给出 -1、0、+1 | `PlayerBuddahInputSource.GetSteering :34` |

**规划方法**
- 每隔"重新规划间隔"个 tick，用 `BuddahMotionModel` 把一组候选按键序列向前推演"推演时长"个 tick，候选形如"左 / 不按 / 右，保持 k tick 后松开"。
- 评分项：
  - 与走线的横向偏离
  - 速度方向与切线的夹角
  - 前方目标点的进度
  - 与赛道边缘的接近程度
- 推演不含碰撞，撞墙或被推之后靠下一次重新规划来修正。
- 规划器读取当前的 `Final*` 数值，所以技能改变推力、质量或转向后，推演会自动跟着变。

**精度、Fumble 与追赶**
- 难度只决定精度：反应延迟（使用 N tick 前的状态）、推演时长、重新规划间隔、候选分辨率。
- Fumble 在输出端以一定概率替换按键。
- 遮挡视野期间：推演时长缩短，反应延迟增加。
- 追赶：落后的 AI 降低 Fumble、提高精度；领先的 AI 反过来。

**S1.5 可行性验证**
- 在 Practice 中让 `AIRacerDriver` 接管**玩家自己那台 Buddah** 的转向（仅调试开关）。
- 这样不需要 Racer，也不需要生成 AI，就能验证规划器能否稳定跑完 3 圈。

### 5.4 AI 技能（S3b / S5）

- **配装**：先打乱 `LoadoutRules` 的候选顺序，再用 `FindNextAutoFillSkillId` 逐槽填充，最后通过 `SkillLoadout.SetSlotsServer` 写入。这和玩家走同一套合法性规则；默认设置不允许重复。
- **施法规则**：

| 技能 | 普通版施放时机 |
|---|---|
| 加速 | 前方是直道 |
| 减速陷阱 | 身后近处有 Racer |
| 反转转向 | 前方有 Racer 正在过弯 |
| 遮挡视野 | 附近有对手 |
| 推手 / 弹丸 | 侧前方有 Racer |
| 巨大化 | 周围 Racer 多 |

- **难度**只影响规则判断的精度（看多远、多久扫描一次）。
- **反噬**概率照常由执念值决定。
- **技能池**以配置仓库为准。

### 5.5 Stuck Recovery（S3a）

- 现有的贴地复活（`RespawnFloor` 持续接触 2 秒）和逆行修正（`RaceCompletionTracker.cs:181-213`）都只在 owner 端运行。对 AI，要在服务器侧复用同样的判定。
- 另外新增 `StuckDetector`：样条线进度连续 N 秒没有前进（默认 3 秒），或逆行超过 M 秒，就复活到当前进度处的赛道上方，朝向对齐切线（与现有复活一致）。
- 阈值放在 `AIDifficultyProfile` 里。

### 5.6 Match Clock（S1 预留，S7 启用）

**接入范围**：比赛内的计时器都改为读取 Match Clock，并保持现有的数值和顺序：

| 计时器 | 位置 |
|---|---|
| 第一名冲线后 15 秒 | `RaceFinishManager :78/:125` |
| 开赛倒计时 | `RoomStateManager :859` |
| 技能冷却与施法延迟 | `SkillExecutor :213/:271/:287` |
| 执念值衰减 | `ObsessionFigure :83` |
| 复活 | `BuddahRespawn :63/:76/:203` |
| 推人判定框 | `PushHitbox :51/:105` |
| 完赛宽限期 | `RaceCompletionTracker :72` |
| 结算倒计时 | `MatchResultPresentationCoordinator :268`、`ResultDecisionManager :212` |
| 选择阶段倒计时 | `PropertiesSelectionManager :596` |

**暂停（S7）时的处理**
- FishNet 的 tick 按 `unscaledDeltaTime` 累加（`TimeManager.cs:700`），motor 的 modifier 按 tick 计时，所以暂停时 tick 也必须停下来。具体方案在 S7 设计。
- S1 只要求：所有比赛计时器只有一个时间来源，而且行为与原来一致。

### 5.7 可用性与占位美术

**可用性**
- 设置面板的默认值是上一次的选择（用 PlayerPrefs 记住），首次默认为 3 个 AI、普通难度。
- 单机中所有确认框都可以用键盘操作。
- 结算界面上，"再来一局"是默认焦点。

**占位美术**

| 内容 | 占位做法 |
|---|---|
| 4–6 人开场布局 | 复制现有布局，按间距平移 |
| AI 区分 | 给 AI 的材质加一个颜色偏移 |
| 头顶名字 | TMP 文字，朝向相机 |
| 小地图对手 | 彩色圆点 |

所有占位资源都放在 `Assets/Placeholder/`，方便以后统一替换。

## 6. 风险

| 风险 | 应对 |
|---|---|
| 规划器无法稳定驾驭这套物理 | S1.5 提前验证；ADR 0003 保留了经验公式作为备选 |
| 改 RacerId 时不小心改变了联机行为 | S2 单独成阶段，必须跑联机回归（V2） |
| AI 卡在 owner 门槛上：出发被设成 kinematic、进度不更新、复活不触发 | §3.3 已逐个列出接缝，S3a 的完成标准逐项覆盖 |
| 单机和联机之间状态残留 | `SessionLauncher.StopSession` 统一清理；V8 交替运行验证 |
| 视觉平滑层（TickSmoother）在 host 上抖动 | V7 专项观察；不要改预测逻辑 |
| 5 个 AI 的规划开销 | V9 预算：发布构建中每帧总耗时 < 1ms |
