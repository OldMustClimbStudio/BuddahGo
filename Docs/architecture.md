# 架构与运行流程

| 系统 | 主要代码位置（相对 `Assets/Scripts/`） | 状态负责人 |
|---|---|---|
| 连接与房间 | `Network/Core/`、`Network/Room/` | `GameNetworkManager`、`RoomStateManager` |
| 大厅与选择 | `Network/Lobby/`、`Network/Session/PropertySelection/` | `SteamLobbyManager`、`PropertiesSelectionManager` |
| 预测运动 | `New_Buddah/` | `BuddahPredictedMotor`、reconcile 数据与相关 bridge |
| 技能 | `Buddah/ComboSkill/` | `SkillExecutor` |
| 配置 | `Config/` | `ProjectConfigRuntime` 和 repositories |
| 比赛与结算 | `RaceIntro/`、`Network/Gameplay/`、`Network/Session/Results/` | 入场、进度、终点和投票各自的 controller/manager |
| 表现 | `UI/`、预测表现与相机 bridge | 读取游戏状态，更新 UI、VFX、相机 |
| 会话与比赛契约 | `Match/`、`Match/Contracts/`、`Match/UI/` | `SessionLauncher` 启停会话；`MatchRules`、`MatchServices`、`RacerDirectory` 为每局静态入口，由 `SessionLauncher.ResetMatchGlobals` 统一清理 |
| AI 对手 | `AI/`；诊断 harness 在 `AI/Diagnostics/`，仅 Editor/Development 编译 | `AIRacerDriver` 通过 `ISteeringOverride` 驾驭，`AISkillCaster` 通过共享 `SkillExecutor` 施法，`AISkillWorld` 提供每局共享快照 |

## 关键边界

- 服务器决定游戏状态；客户端预测，表现层读取状态，不建立第二套游戏权威。
- 预测模式下，技能、碰撞和复活通过现有预测桥接处理运动，避免在表现层直接改刚体。
- `SkillExecutor` 负责技能分发与 base/anti 配对；合法技能、配装和属性来自配置仓库。
- `RoomStateManager` 负责多人场景流转。`ResultDecisionManager` 负责投票结果，再请求房间系统切场景。
- 控制权交接、复活和模式切换需要同时处理运动限制、队列与表现状态，避免遗留旧状态。

## 程序集与测试

- `BuddahGo.Runtime` 覆盖 `Assets/Scripts` 下的游戏代码；`BuddahGo.Editor` 覆盖其中的 `Editor` 目录。UI 也在 Runtime 中。
- `FishyFacepunch` 独立引用 FishNet.Runtime，Steamworks DLL 继续由插件的平台导入设置选择。Feel 已归入 MoreMountains.Tools，不另建 MMFeedbacks 程序集。
- `Assets/Editor` 留在默认 Editor 程序集；Scripts 以外的场景/示例脚本保持原程序集。不要把它们的 UnityEvent 类型名一并替换。
- `Assets/Tests/EditMode` 中的 `BuddahGo.Tests` 仅在 Editor 编译，通过 friend assembly 访问纯逻辑 helper，不进入 Player。打开 Unity 的 Test Runner，选择 EditMode 下该程序集运行；也可用 Unity MCP 的 `run_tests`，指定 `mode=EditMode` 和 `assembly_names=["BuddahGo.Tests"]`。
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

单机对局（Solo Match，设计中）复用同一条链路：由 SessionLauncher 在 Yak 上启动本机 host 取代大厅，单人房间自动进入选择场景，结算以"再来一局 / 返回"按钮取代投票。设计与实现入口见 [single-player/HANDOFF.md](single-player/HANDOFF.md)。

网络细节见 [networking.md](networking.md)，运动设计见 [prediction-design.md](prediction-design.md)。
