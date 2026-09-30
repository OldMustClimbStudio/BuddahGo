# 架构与运行流程

| 系统 | 主要代码位置（相对 `Assets/Scripts/`） | 状态负责人 |
|---|---|---|
| 连接与房间 | `Network/Core/`、`Network/Room/` | `GameNetworkManager`、`ConnectionManager`、`RoomStateManager` |
| 大厅与选择 | `Network/Lobby/`、`Network/Session/PropertySelection/` | `SteamLobbyManager`、`PropertiesSelectionManager` |
| 预测运动 | `New_Buddah/` | `BuddahPredictedMotor`、reconcile 数据与相关 bridge |
| 技能 | `Buddah/ComboSkill/` | `SkillExecutor` |
| 配置 | `Config/` | `ProjectConfigRuntime` 和 repositories |
| 比赛与结算 | `RaceIntro/`、`Network/Gameplay/`、`Network/Session/Results/` | 入场、进度、终点和投票各自的 controller/manager |
| 表现 | `UI/`、预测表现与相机 bridge | 读取游戏状态，更新 UI、VFX、相机 |

## 关键边界

- 服务器决定游戏状态；客户端预测，表现层读取状态，不建立第二套游戏权威。
- 预测模式下，技能、碰撞和复活通过现有预测桥接处理运动，避免在表现层直接改刚体。
- `SkillExecutor` 负责技能分发与 base/anti 配对；合法技能、配装和属性来自配置仓库。
- `RoomStateManager` 负责多人场景流转。`ResultDecisionManager` 负责投票结果，再请求房间系统切场景。
- 控制权交接、复活和模式切换需要同时处理运动限制、队列与表现状态，避免遗留旧状态。

## 一场比赛的链路

```text
SteamLobbyManager 创建/加入大厅
  → ConnectionManager 启动连接，GameNetworkManager 绑定网络管理器
  → RoomStateManager 进入选择场景
  → PropertiesSelectionManager 校验属性、配装与 ready 状态
  → RoomStateManager 加载赛道，IntroSequenceManager 协调开场
  → 预测运动与 SkillExecutor 驱动比赛
  → RaceCompletionTracker / RaceFinishManager 记录进度与完成顺序
  → 结算展示，ResultDecisionManager 收集下一局/返回投票
  → RoomStateManager 执行场景转换
```

网络细节见 [networking.md](networking.md)，运动设计见 [prediction-design.md](prediction-design.md)。
