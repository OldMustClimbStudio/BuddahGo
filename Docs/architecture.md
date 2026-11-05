# 架构与运行流程

| 系统 | 主要代码位置（相对 `Assets/Scripts/`） | 状态负责人 |
|---|---|---|
| 连接与房间 | `Network/Core/`、`Network/Room/` | `GameNetworkManager`、`RoomStateManager` |
| 大厅与选择 | `Network/Lobby/`、`Network/Session/PropertySelection/` | `SteamLobbyManager`、`PropertiesSelectionManager` |
| 预测运动 | `New_Buddah/` | `BuddahPredictedMotor`、reconcile 数据与相关 bridge |
| 技能 | `Buddah/ComboSkill/`、`GameData/ObsessionFigure.cs` | `SkillExecutor` 负责施法入口、施法门与统一反噬；`ObsessionFigure` 负责执念与反噬惩罚 |
| 配置 | `Config/` | `ProjectConfigRuntime` 和 repositories |
| 比赛与结算 | `RaceIntro/`、`Network/Gameplay/`、`Network/Session/Results/` | 入场、进度、终点和投票各自的 controller/manager |
| 表现 | `UI/`、预测表现与相机 bridge、`Buddah/RacerSkillPresentationState.cs` | 读取游戏状态和表现事件，更新 UI、VFX、相机 |
| 会话与比赛契约 | `Match/`、`Match/Contracts/`、`Match/UI/` | `SessionLauncher` 启停会话；`MatchRules`、`MatchServices`、`RacerDirectory` 为每局静态入口，由 `SessionLauncher.ResetMatchGlobals` 统一清理；参赛者身份见[下文](#参赛者身份与权限) |
| 训练木桩 | `Match/TrainingDummy/`，由 `Network/Match/MatchSpawnManager` 生成 | `TrainingDummy` 原地停车与复位（仅服务器），`TrainingDummyReadout` 显示头顶读数 |
| AI 对手 | `AI/`；诊断 harness 在 `AI/Diagnostics/`，仅 Editor/Development 编译 | `AIRacerDriver` 通过 `ISteeringOverride` 驾驭；`AISkillCaster` 直接调用共享的 `SkillExecutor.CastSlotServer`（和 owner RPC 同一个施法门，A7），掌形瞄准经 `BuddahHandControl.InjectServerRotation` / `InjectServerPush`；`AISkillWorld` 提供每局共享快照 |

## 关键边界

- 服务器决定游戏状态；客户端预测，表现层读取状态，不建立第二套游戏权威。
- 预测模式下，技能（包括冻结、突进和反噬减速）、碰撞和复活通过现有预测桥接处理运动，避免在表现层直接改刚体。
- `SkillExecutor` 负责施法入口、施法门与统一反噬，见[技能施法链路](#技能施法链路)；合法技能、配装和属性来自配置仓库。
- `RoomStateManager` 负责多人场景流转。`ResultDecisionManager` 负责投票结果，再请求房间系统切场景。
- 控制权交接、复活和模式切换需要同时处理运动限制、队列与表现状态，避免遗留旧状态。
- 前进不是自动的（2026-10-30）：预测输入的 `Throttle` 由 `BuddahDriveInput.ResolveThrottle` 决定，移动门关闭时为 0；有 `ISteeringOverride`（AI、训练木桩）时取它的 `drive`；否则取 owner 的 W（`PlayerBuddahInputSource.GetThrottle`，按住 1、松开 0，不刹车）。真人和 AI 写同一个字段，服务器按同一份输入模拟。

## 技能施法链路

```text
真人 owner：Q/E/R 或 1/2/3（InputSystem_Actions.Player.SkillSlot1–3）
  → SkillExecutor.RequestCast → CastSlotServerRpc（RequireOwnership）
服务器 AI：AISkillCaster.Decide（AISkillCommitment：决定后下一 tick 请求）→ AISkillCaster.Request
  → 两条路进入同一个 SkillExecutor.CastSlotServer
  → 施法门 → 在请求这一刻掷反噬骰 → QueueCast：本槽进冷却，播放确认前摇
  → 前摇结束：正常 → ExecuteServer、执念入账、CastObserversRpc
              反噬 → 技能不执行，开始统一反噬惩罚
```

- 搓招已退役（A7）：`ComboSkillInput` 已删除，真人用技能键，AI 直接调用 `CastSlotServer`，两者经过同一个施法门。`HandPush` 只绑鼠标左/右键，只用来推掌（W/↑ 推掌 2026-10-30 已删除，W 改为前进），见[玩法深化 §3](gameplay/README.md#3-操作)。手部朝向取光标在地面上的投影（`BuddahHandControl`，纯数学在 `Buddah/Input/HandAimMath.cs`），yaw 仍经 `BuddahHandControl.TrySendYawToServer` 同步。
- 施法门 `SkillCastRules.EvaluateGate` 由 `TryResolveCast`（每次请求）和 `IsSlotReadyServer`（AI 就绪判断）共用，依次检查：已有待确认的施法、反噬锁、冻结、本槽冷却。没有全局施法锁。同一时刻最多一个待确认的施法，前摇期间的请求直接 `Rejected`；不同技能可以同时生效。
- 冷却和确认前摇按所请求技能取值（前摇为 `Skill.castConfirmDelaySeconds`，没有配表行时 1 秒）。反噬同样花掉本槽冷却。
- 同招加价（`SkillRepeatSurcharge`）：同一技能在自己冷却结束后 `repeatWindowSeconds` 内重放，本次执念增量乘以 `repeatGainMultiplier` 的层数次方。层数在请求时算出，只在正常执行时提交；反噬时全部清零。
- 统一反噬（`ExecuteBackfireServer`）：不调用 `ExecuteServer`，不加执念。`ObsessionFigure.BeginBackfireServer` 在 `backfireLockSeconds` 内锁住全部技能并冻结执念，结束时执念乘以 `backfireEndKeepRatio`；同时 `ApplyMaxSpeedMultiplierToOwner` 施加 `backfireMaxSpeedMultiplier` 的极速预测修饰。施法以 `CastStage.Executed` 加 `IsBacklash` 结束。惩罚不是技能效果，复活和技能重置都不解除它。已判定反噬的施法不会被普通取消、复活或冻结打断（`SkillCastRules.DropsPendingCast`），只有 AI 离开比赛（`ResolveConfirmOutcome`）和执行器停止会丢掉它。上面这些参数都是 `ObsessionFigure` 的字段，数值来自 `Skill/Obsession` 表。
- 冻结（`SkillExecutor.ApplyFreezeToOwner`）会取消正在确认的普通施法并返还本槽冷却。冻结期间施法门关闭，不能推掌，手部不转。
- 反噬对所有技能统一处理，没有按技能的反噬变体（`*_anti`），`SkillAction` 也没有 `antiSkillId`；决策见 [ADR 0007](adr/0007-unified-backfire-replaces-per-skill-anti-variants.md)。

### 缘起

缘起（技能组合）在叠在本 PR 之上的单独 PR（分支 `feat/yuanqi`）里设计和实现，本分支不含。缘起要用的预测修饰已在本分支：冻结 `Freeze`（`SkillExecutor.ApplyFreezeToOwner`）、突进 `Burst`（`ApplyBurstToOwner`）和提前结束修饰的 `EndModifier`（`EndAccelerationForOwner` / `EndScaleForOwner`），规则见[玩法深化 A5](gameplay/README.md#a5-缘起)。

### 表现接口

新状态只提供表现接口，不加新特效（用户决定，2026-10-26）。美术可以挂接的入口：

- `ObsessionFigure.BackfireStateChanged`：反噬开始和结束，每个端都会触发。
- `RacerSkillPresentationState`：每个端给每个 racer 运行时挂上，提供 `FrozenChanged` / `BurstChanged` 与剩余时间。
- `BuddahHandControl.TryGetAimGroundPoint`：owner 端当前帧的光标地面点；`IsInsideMouseAimDeadRadius` 判断它是否在死区内，`BuddahHandControl.LocalOwner` 是本机 owner 的手部组件。占位图标 `AimGroundIndicator`（`UI/InGameUI/`，由 `RaceMapUIBootstrap` 运行时挂到 HUD）读这三个入口，在瞄准点画圆环加十字，等手部朝向指引设计后替换，见[玩法深化 A6](gameplay/README.md#a6-hud)。

不需要美术的动作层已经实现：冻结期间角色 Animator 定格，手部停转。

## 参赛者身份与权限

- `RacerIdentity`（`Match/Contracts/`）在 spawn 前分配 racer id：真人等于 client id，AI 为 `RacerId.AIBase`（10000）加序号。id 已分配的 racer 在 `OnStartNetwork` 注册进 `RacerDirectory`。
- 训练木桩用同一个 racer prefab，由 `AssignTrainingDummyBeforeSpawn` 标记 `IsTrainingDummy`，id 保持未分配（−1），所以不进 `RacerDirectory`、排名、完赛、结算、落后补偿和开场布局。按场上 `SkillExecutor` 找目标的技能（如逆姻缘）仍会命中它。
- `RacerAuthority` 回答“哪个进程驱动这个 racer”：

| 判断 | 何时为真 | 用于 |
|---|---|---|
| `IsServerAI` | 服务器上，id 已分配且为 AI | AI 施法路径与 AI 专用的施法复查 |
| `IsServerDummy` | 服务器上，没有有效 owner、`IsTrainingDummy` 且 id 未分配 | 木桩自身的服务器逻辑 |
| `IsServerControlled` | `IsServerAI` 或 `IsServerDummy` | 没有 owner 连接、由服务器本地执行的部分：技能重置、坠落复活、teleport 后的清理 |
| `HasLocalControl` | owner，或服务器控制 | 运动输入写入、刚体物理、开场时间线 |
| `IsProgressAuthority` | owner，或服务器 AI；不含木桩 | 圈数、检查点、计时、进度上报与逆行纠正 |

## 程序集与测试

- `BuddahGo.Runtime` 覆盖 `Assets/Scripts` 下的游戏代码；`BuddahGo.Editor` 覆盖其中的 `Editor` 目录。UI 也在 Runtime 中。
- `FishyFacepunch` 独立引用 FishNet.Runtime，Steamworks DLL 继续由插件的平台导入设置选择。Feel 已归入 MoreMountains.Tools，不另建 MMFeedbacks 程序集。
- `Assets/Editor` 留在默认 Editor 程序集；Scripts 以外的场景/示例脚本保持原程序集。不要把它们的 UnityEvent 类型名一并替换。
- `Assets/Tests/EditMode` 中的 `BuddahGo.Tests` 仅在 Editor 编译，通过 friend assembly 访问纯逻辑 helper，不进入 Player。打开 Unity 的 Test Runner，选择 EditMode 下该程序集运行；也可用 Unity MCP 的 `run_tests`，指定 `mode=EditMode` 和 `assembly_names=["BuddahGo.Tests"]`。编辑器失焦（例如锁屏）时 `run_tests` 会停住，改用 headless 命令行，见 [Unity MCP](unity-mcp.md#测试与验证入口)。
- 玩法机制的训练模式验证由 `MechanicsPracticeHarness`（`AI/Diagnostics/`）在 play mode 里驱动真实 Practice 流程，重跑方法见[训练模式验证记录](gameplay/practice-verification.md#重跑方法)。
- 游戏场景的 UnityEvent 持久目标使用 `BuddahGo.Runtime`。新增程序集边界或迁移类型时，需要同时复核场景/预制体中的程序集限定名和实际按钮、Timeline 回调。

房间名单、握手、配装和身份 helper 的职责、调用约束及测试范围见 [会话辅助规则](session-helpers.md)。

## 一场比赛的链路

```text
SteamLobbyManager 创建/加入大厅
  → GameNetworkManager 启动连接并绑定网络管理器
  → RoomStateManager 进入选择场景
  → PropertiesSelectionManager 校验属性、配装与 ready 状态
  → RoomStateManager 加载赛道，IntroSequenceManager 协调开场
  → 预测运动与 SkillExecutor 驱动比赛
  → RaceCompletionTracker / RaceFinishManager 记录进度与完成顺序
  → 结算展示，ResultDecisionManager 收集下一局/返回投票
  → RoomStateManager 执行场景转换
```

单机对局（Solo）复用同一条链路：`SessionLauncher` 在 Yak（本机 loopback transport）上启动本机 host 取代大厅，单人房间自动进入选择场景，结算以“再来一局 / 返回”按钮取代投票。设计与实现入口见 [single-player/HANDOFF.md](single-player/HANDOFF.md)。

训练模式（Practice）是 0 个 AI 的 Solo：`SoloMatchRules` 改读 `Match/ModeRule` 的 Practice 行，玩家完赛即结束，`SpawnTrainingDummies` 为真。`MatchSpawnManager` 在起跑后的第一段直道上生成 3 个木桩（数量、间距、偏移和复位秒数来自 Practice 行；RaceMap 上在起跑线后约 53、68、83 米，间隔 15 米，左、中、右各一）。木桩不驾驶、不施法；被推离生成点后，最后一次受到技能效果、冲量或与运动物体接触 3 秒后传回。主菜单没有单独的训练模式入口：单人面板按 `Match/ModeRule` Solo 行的 `aiCount`（现为 5）开局，该值为 0 时开出的就是训练模式；不改表时用代码 `SessionControl.Current.StartSoloHost(new SoloMatchSettings(0, …))` 启动，`MechanicsPracticeHarness` 就是这样做的。

网络细节见 [networking.md](networking.md)，运动设计见 [prediction-design.md](prediction-design.md)。
