# 交接：单机模式实现

**当前状态：S1 paused（2026-10-01 用户要求暂停并关机）**。已完成与部分证据见 [progress.md](progress.md) 的暂停检查点；独立 benchmark 尚未实现或运行。收到用户继续指令后再恢复，不自动启动 Editor、测试或后续阶段。

你负责在分支 `feat/single-player-mode` 上实现 Solo Match：一个 Human Player 对 0–5 个 AI Racer，完全离线，流程与 Online Match 一样完整。

| 你需要什么 | 看哪里 |
|---|---|
| 术语（讨论、命名、注释都用这套词） | [CONTEXT.md](../../CONTEXT.md) |
| 功能清单、模块架构、接缝、流程、坑 | [design.md](design.md) |
| 各阶段的目标、完成标准、验证项 | [phases.md](phases.md) |
| 为什么这样定 | [Docs/adr/](../adr/) |
| 进度、调参记录、决策、新发现 | [progress.md](progress.md) |

仓库通用规则以 [harness.md](../../harness.md) 与 [CONTRIBUTING.md](../../CONTRIBUTING.md) 为准；重构后的 helper 约定见 [Docs/session-helpers.md](../session-helpers.md)。

## 首要原则

**单机稳定性高于一切**（design.md §0）。
- 每个阶段都要保证单机能反复完整游玩，并通过 V11 浸泡测试。
- 宁可把功能推迟，也不交付不稳定的部分。
- **范围更新（2026-10-01 用户决定）**：单机任务不运行联机测试。Steam 双端、联机冒烟 V2、Solo/Online 交替均不是 S1 或后续单机阶段的完成前置；V2 对本任务不适用，不能写成通过。保留既有联机测试资产，只有独立联机任务才执行。
- 单机使用自己的 `benchmark.md`（独立协议待实现）：S1 为 0 AI Practice，后续按阶段最大受支持负载扩展。实际构建、真实比赛、合成流程分别记证据；不得引用联机 Editor R8 数值冒充单机基线或收益。

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

**阶段完成的含义**：完成标准逐条满足，适用的单机 V 项、构建/序列化检查及独立 benchmark 有实际结果，progress.md 已更新。暂停不是完成：保存当前 HEAD、证据和恢复入口，收到继续指令后从未完成项恢复。

## 需要停下来找团队的情况

- 需要改变 CONTEXT.md 中某个术语的含义，或者需要推翻某个 ADR。
- S1.5 证明规划器跑不通（ADR 0003 的备选方案要由团队决定）。
- 单机真实运行错误或明确的设计冲突无法在当前约束下解决。联机环境或测试结果不阻塞单机工作，也不要求用户提供 Steam 双端。
- V11 浸泡测试出现无法定位的偶发问题。
- AI 必须修改运动组件本身、或者修改物理参数才能工作。这违反 ADR 0003。
- 难度和 Fumble 的数值需要团队看过才能定（S4 的验收本来就要求团队确认）。

决策结论记进 progress.md 的决策表；属于长期决策的，新增一份 ADR。

## 必须知道的事实

- **没有 Steam 时，现在连 host 都起不来（N3）**：`SteamClient.Init` 抛异常会中断整个 NetworkManager 的初始化。Multipass 会初始化它下面的所有传输层，所以就算走 Yak，也必须先完成 ADR 0004 的容错。
- **Multipass 默认在所有传输层上启动 server**：`ServerManager.StartConnection()` 会这样做。单机必须按 design.md §5.1 的顺序，只在 Yak 上启动；每次启动 client 前都要先 `SetClientTransport`；`GlobalServerActions` 保持 true。这套做法已经对照源码确认可行，S1 的第一步是实测它。
- **不要在 RPC 调用栈里停止会话**：会销毁正在执行的 NetworkBehaviour。统一用 `SessionControl.Current.RequestStopSession()`，它会在下一帧执行。
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
