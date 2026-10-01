# 交接：单机模式实现

你负责在分支 `feat/single-player-mode` 上实现 Solo Match：一个 Human Player 对 0–5 个 AI Racer，完全离线，流程与 Online Match 一样完整。

| 你需要什么 | 看哪里 |
|---|---|
| 术语（讨论、命名、注释都用这套词） | [CONTEXT.md](../../CONTEXT.md) |
| 功能清单、模块架构、接缝、流程、坑 | [design.md](design.md) |
| 各阶段的目标、完成标准、验证项 | [phases.md](phases.md) |
| 为什么这样定 | [Docs/adr/](../adr/) |
| 进度、调参记录、决策、新发现 | [progress.md](progress.md) |

仓库通用规则以 [harness.md](../../harness.md) 与 [CONTRIBUTING.md](../../CONTRIBUTING.md) 为准；重构后的 helper 约定见 [Docs/session-helpers.md](../session-helpers.md)。

## 开工条件

已满足：重构（#47–#58）已经合入 `dev`，本分支已同步，S0 已完成。从 S1 开始。

## 每次开工

1. 进入 worktree，没有就创建：`git worktree add .worktree/single-player-mode feat/single-player-mode`。
2. 执行 `git lfs pull`，再执行 `git merge origin/dev`。
3. 打开 progress.md，从第一个不是 `done` 的阶段继续。

## 每个阶段的做法

1. 在 progress.md 中把该阶段标为 `doing`。
2. 读完 phases.md 里该阶段的全部内容，以及它引用的 design.md 章节。
3. 先读受影响的代码，再动手。design.md §3.3 的接缝表是起点，不是完整清单。
4. 写代码时遵守 design.md §3.1 的依赖规则。其中最常被违反的几条：
   - 现有代码只通过契约层（`Assets/Scripts/Match/Contracts/`）访问新模块。
   - 纯逻辑写成普通类，并配 EditMode 测试。
   - 不在运行时添加 NetworkBehaviour：`RacerIdentity`、AI 组件都预先放在 prefab 上。
5. 按 CONTRIBUTING 的格式小步提交，例如 `feat: [S3a] spawn owner-less AI racers`。
6. 跑该阶段要求的 V 项，并在 progress.md 记录结果。当前环境跑不了的项，写"未执行 + 原因"，不能写成通过。
7. 完成标准逐条满足后，标为 `done`，向 `dev` 开 PR。PR 的 Validation 一节列出各 V 项的结果。

**阶段完成的含义**：完成标准逐条满足，要求的 V 项都有实际结果，progress.md 已更新。

## 需要停下来找团队的情况

- 需要改变 CONTEXT.md 中某个术语的含义，或者需要推翻某个 ADR。
- S1.5 证明规划器跑不通（ADR 0003 的备选方案要由团队决定）。
- S2 之后，联机回归（V2）与基线不一致，而且找不到原因。
- AI 必须修改运动组件本身、或者修改物理参数才能工作。这违反 ADR 0003。
- 难度和 Fumble 的数值需要团队看过才能定（S4 的验收本来就要求团队确认）。

决策结论记进 progress.md 的决策表；属于长期决策的，新增一份 ADR。

## 必须知道的事实

- **没有 Steam 时，现在连 host 都起不来（N3）**：`SteamClient.Init` 抛异常会中断整个 NetworkManager 的初始化。Multipass 会初始化它下面的所有传输层，所以就算走 Yak，也必须先完成 ADR 0004 的容错。
- **Multipass 默认在所有传输层上启动 server**：`ServerManager.StartConnection()` 会这样做。单机必须按 design.md §5.1，只在 Yak 上启动，client 启动前先 `SetClientTransport`。S1 的第一步就是实测这一点。
- **单人房间不会自己开始**：全仓库只有 `RoomUI` 的按钮会调用 `RequestStartGame`，所以要用 `TryStartSoloMatchServer` 自动开始。另外，结算后的"返回"路径（`ReturnToRoomMenuKeepingSessionServer`）不会关闭网络，单机必须改走 `StopSession`。
- **AI 的计圈和复活复用现有代码**：只把 owner 门槛改为 `IsProgressAuthority`，不要另写一套计圈规则。
- **单机不会遇到预测回滚问题**：纯 host 会话里没有 reconcile 和回放。单机里如果出现抖动，先查视觉平滑层（V7），不要去改预测逻辑。
- **AI 的 owner 是无效的**：AI 的 `OwnerId` 恒为 -1，`Owner` 是 EmptyConnection，不是 null。任何按 owner 做的判断或键，都会把所有 AI 当成同一个对象。按 owner 区分的代码见 design.md §3.3，统一改用 RacerId（ADR 0002）。
- **比赛是自动全油门的**：油门固定为 1，AI 只需要转向和施法。
- **运动没有线性阻力**：所以 AI 必须往前推演才能稳定走线。不要为了让 AI 好控制而改运动参数。
- **计时时钟要统一**：现在完赛时间和 GO 时间用的不是同一个时钟，比赛计时一律走 Match Clock。
- **现有 UI 里只有技能配装真正生效**：地图和皮肤都是占位。单机跳过地图阶段，皮肤阶段保留。
- **美术用占位**：开场布局、AI 区分、头顶名字、小地图标记都用占位资源，放在 `Assets/Placeholder/`。美术问题不阻塞任何阶段。
- **第三方插件改动**：FishyFacepunch 的 Steam 容错是本地修改（ADR 0004），升级插件时要保留。
