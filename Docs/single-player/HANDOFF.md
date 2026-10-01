# 交接：单机模式实现

**最新有界验证（游戏源码 `9cca08e`）**：见 [选择页与新采样复核](validation-2026-10-01.md)。53项Unity回归通过；选择页720p/1080p修复复核完成；新Development窗口已取得原生GC。handoff仍有每轮一次水平视觉停留，clean-source、strict14/V3与预算未闭合，S1继续doing。以下较早检查点按各自HEAD保留为历史证据。

**当前状态：S1 doing；AI 等待用户后续明确开始信号**。本次文档同步基于 `feat/single-player-mode` 的 `06a440f`；AI 计划已集成为 `17eb1c9`，运行检查点已记入 `24c022d`。三阶段 Esc 新证据通过；真实帧窗口只有诊断资格，V12、strict14 与 V3 精度未完成。`ec2bac2` 的 handoff 修复已通过 40 项相关 Unity 测试；基于 `6e9f851` 的新非 Development Player 完成首次/Rematch 两次真实 60 FPS 开场（5379 帧、0 错误）。GO 邻域未见倒退，但有短暂单帧视觉停留，整体手感仍不标通过。`06a440f` 的 benchmark 工具修复已通过 51 Python + 8 隔离 C# 测试，新 Development 三窗口采样尚未运行。历史验证与未闭合项见 [progress.md](progress.md)。S1.5 未开始；历史暂停检查点不代表当前暂停或验收完成。

选择页独立修复已集成为 `26dd6df`：绘马墙/桌子使用场景已有 URP Lit 替代失效引用（原美术材质缺失，不宣称还原）；Practice 隐藏覆盖卡牌的通用摘要并保留阶段/选三技能提示。仅两个文件的有界改动，静态核对其余 227 场景对象块不变；**尚未执行集成后的 Unity 编译、构建或界面复核**。下一短 session 验证首次进入、六技能选择/移除/替换、皮肤确认、Esc 返回重进及比赛中标题隐藏；不运行联机测试。前述 handoff 构建先于此 UI 修复，不能据此标记最终 UI 通过。

**AI 启动前置（2026-10-01 用户最新决定）**：先完成 S1 单人 Practice，再由用户逐项试玩技能，最后等待用户明确发出开始 AI 的信号。三个条件缺一不可；S1 完成、自动测试通过、文档批准或经过一段时间都不等于开始信号。包括 S1.5 在内的任何 AI 实现、接管调试、采集/可视化工具实现与 AI 运行测试均不得提前开始；信号前只做 AI 文档规划。此前“完成后直接推进 S1.5”的指令已被覆盖。

用户最新确定的 AI 实施与测试顺序为：**A1 单个 AI 无技能完整一圈 → A2 单个 AI 无技能三圈比赛及平均圈速/圈间稳定性 → A3 五个独立 AI 带技能比赛，玩家入场后不操作 → A4 通知用户手动试玩完整流程**。前一步通过才推进；这同时约束实现和测试，覆盖旧方案中不同的推进顺序。技能可使用独立的简单概率决策，不要求复杂行为树；仍须遵守既有技能合法性与玩法。 细节及未定门槛见 [ai-testing.md](ai-testing.md)。

你负责在分支 `feat/single-player-mode` 上实现 Solo Match：一个 Human Player 对 0–5 个 AI Racer，完全离线，流程与 Online Match 一样完整。

| 你需要什么 | 看哪里 |
|---|---|
| 术语（讨论、命名、注释都用这套词） | [CONTEXT.md](../../CONTEXT.md) |
| 功能清单、模块架构、接缝、流程、坑 | [design.md](design.md) |
| 各阶段的目标、完成标准、验证项 | [phases.md](phases.md) |
| 用户试玩交接、AI 圈速/轨迹与技能测试方案 | [ai-testing.md](ai-testing.md) |
| 为什么这样定 | [Docs/adr/](../adr/) |
| 进度、调参记录、决策、新发现 | [progress.md](progress.md) |

仓库通用规则以 [harness.md](../../harness.md) 与 [CONTRIBUTING.md](../../CONTRIBUTING.md) 为准；重构后的 helper 约定见 [Docs/session-helpers.md](../session-helpers.md)。

## 当前 Practice 重点

**尚不可交用户逐项技能试玩**：六技能选择入口已可见，但选择页洋红材质和文字叠放待修复复核，技能效果未逐项验证；handoff 流畅度、V3/V11/V12 也未闭合。入口可用或 Esc 通过不能替代完整可玩交付。

Practice 的目标是让本地单机完整承接既有联机流程与表现，特别是加载/场景 handoff、开场镜头/动画/倒计时到驾驶的姿态、镜头和输入控制权交接，并真实验证流畅。此要求是本地适配与验收，不是运行联机测试；也不授权全局移除 prediction。用户报告当前 handoff 表现一般，原因及修复效果需代码与运行证据，不能静态宣告通过。 具体验收表见 [phases.md](phases.md) 的 S1 本地流程与表现验收；主 S1 任务负责实际运行，模块修复与验收需协调同一构建。

## 首要原则

**单机稳定性高于一切**（design.md §0）。
- 每个阶段都要保证单机能反复完整游玩，并通过 V11 浸泡测试。
- 宁可把功能推迟，也不交付不稳定的部分。
- **范围更新（2026-10-01 用户决定）**：单机任务不运行联机测试。Steam 双端、联机冒烟 V2、Solo/Online 交替均不是 S1 或后续单机阶段的完成前置；V2 对本任务不适用，不能写成通过。保留既有联机测试资产，只有独立联机任务才执行。
- 单机使用自己的 [benchmark.md](benchmark.md)（协议和模板已集成，通过状态须有实际证据）：S1 为 0 AI Practice，后续按阶段最大受支持负载扩展。实际构建、真实比赛、合成流程分别记证据；不得引用联机 Editor R8 数值冒充单机基线或收益。

## 开工条件

S1 开工条件已满足：重构（#47–#58）已经合入 `dev`，本分支已同步，S0 已完成。此条件不授权 AI 开工；S1 完成后进入用户技能试玩与等待信号，按 [ai-testing.md](ai-testing.md) 交接。

## 每次开工

1. 先检查 `git worktree list`、目标分支 HEAD 和未提交内容；主 worktree 保持 `dev`。实现任务使用已有 single-player-mode worktree，协作任务使用主根 `.worktree/` 下各自独立的 worktree，不覆盖他人的工作。
2. 实现任务按需补齐 LFS 资源或同步 `dev`；由该分支负责人处理同步与冲突，不把 merge 当成每个文档任务的固定步骤。
3. 打开 progress.md，先核对 AI 启动前置；仅在已授权范围内继续未完成阶段，不能因 S1 为 `done` 就自动推进 S1.5。当前 S1 使用已有 draft PR #59；文档和 benchmark 提交由主任务集成进同一 PR，不另建 PR。

## 每个阶段的做法

1. 确认该阶段的开工条件已满足；AI 阶段必须有用户明确开始信号记录，再在 progress.md 中标为 `doing`。
2. 读完 phases.md 里该阶段的全部内容，以及它引用的 design.md 章节。
3. 先读受影响的代码，再动手。design.md §3.3 的接缝表是起点，不是完整清单。
4. 写代码时遵守 design.md §3.1 的依赖规则。其中最常被违反的几条：
   - 现有代码只通过契约层（`Assets/Scripts/Match/Contracts/`）访问新模块。
   - 纯逻辑写成普通类，并配 EditMode 测试。
   - 不在运行时添加 NetworkBehaviour：`RacerIdentity`、AI 组件都预先放在 prefab 上。
5. 按 CONTRIBUTING 的格式小步提交，例如 `feat: [S3a] spawn owner-less AI racers`。
6. 跑该阶段要求的 V 项，并在 progress.md 记录结果。当前环境跑不了的项，写"未执行 + 原因"，不能写成通过。
7. 完成标准逐条满足后，标为 `done`，更新该阶段面向 `dev` 的 PR（已有 PR 则复用）。PR 的 Validation 一节列出各 V 项的结果。

**阶段完成的含义**：完成标准逐条满足，适用的单机 V 项、构建/序列化检查及独立 benchmark 有实际结果，progress.md 已更新。S1 完成只进入用户技能试玩交接，不自动启动 AI。暂停不是完成：保存当前 HEAD、证据和恢复入口，收到继续指令后从未完成项恢复。

## 需要停下来找团队的情况

- 需要改变 CONTEXT.md 中某个术语的含义，或者需要推翻某个 ADR。
- Practice 完成后，用户技能试玩尚未完成或尚未收到明确的 AI 开始信号：保持 AI 未开始，仅维护文档。
- S1.5 证明规划器跑不通（ADR 0003 的备选方案要由团队决定）。
- 单机真实运行错误或明确的设计冲突无法在当前约束下解决。联机环境或测试结果不阻塞单机工作，也不要求用户提供 Steam 双端。
- V11 浸泡测试出现无法定位的偶发问题。
- AI 必须修改运动组件本身、或者修改物理参数才能工作。这违反 ADR 0003。
- 难度和 Fumble 的数值需要团队看过才能定（S4 的验收本来就要求团队确认）。

决策结论记进 progress.md 的决策表；属于长期决策的，新增一份 ADR。

## 必须知道的事实

- **N3 已在 S1 实现中处理**：`FishyFacepunch.Initialize` 捕获 Steam 初始化异常并记录不可用警告，继续初始化其它传输层；`SessionLauncher` 用 Yak 启动离线 host。历史验证已覆盖 Steam 不可用时的启动/停止，不能再把 ADR 0004 容错列为未实现前置。
- **Multipass 默认在所有传输层上启动 server**：`ServerManager.StartConnection()` 会这样做。单机必须按 design.md §5.1 的顺序，只在 Yak 上启动；每次启动 client 前都要先 `SetClientTransport`；`GlobalServerActions` 保持 true。当前实现遵循该顺序，历史 `SoloTransportTests` 已验证 Yak 两端事件与停止状态；完整 S1 验收仍见 progress.md。
- **不要在 RPC 调用栈里停止会话**：会销毁正在执行的 NetworkBehaviour。统一用 `SessionControl.Current.RequestStopSession()`，它会在下一帧执行。
- **单人房间自动开始已接入**：`RoomStateManager.StartSoloAfterInitialScenes` 等待本地连接认证和初始场景加载后调用 `TryStartSoloMatchServer`。单机结算返回已通过 `SessionControl.Current.RequestStopSession()` 延后完整关闭；真实结果页按钮与重复生命周期验收仍未闭合，不能以代码存在代替验证。
- **AI 的计圈和复活复用现有代码**：只把 owner 门槛改为 `IsProgressAuthority`，不要另写一套计圈规则。
- **handoff 流畅度需实测**：不能因本地 host 就假定所有姿态/镜头/输入交接自然流畅。先检查开场与驾驶控制权、加载状态、物理/视觉平滑接缝及实际预测调用路径；当前任务不授权全局移除 prediction，不凭静态分析断言抖动原因或修复通过。
- **AI 的 owner 是无效的**：AI 的 `OwnerId` 恒为 -1，`Owner` 是 EmptyConnection，不是 null。任何按 owner 做的判断或键，都会把所有 AI 当成同一个对象。按 owner 区分的代码见 design.md §3.3，统一改用 RacerId（ADR 0002）。
- **比赛是自动全油门的**：油门固定为 1，AI 只需要转向和施法。
- **运动没有线性阻力**：所以 AI 必须往前推演才能稳定走线。不要为了让 AI 好控制而改运动参数。
- **S1 计时已统一到 Match Clock**：GO、圈速和完赛使用服务器 tick 时钟；真人圈数仍经约 100 ms 进度上报观察，不能声称冲线精确到一个 tick。暂停与其余计时器迁移仍属 S7。
- **现有 UI 里只有技能配装真正生效**：地图和皮肤都是占位。单机跳过地图阶段，皮肤阶段保留。
- **美术用占位**：开场布局、AI 区分、头顶名字、小地图标记都用占位资源，放在 `Assets/Placeholder/`。美术问题不阻塞任何阶段。
- **第三方插件改动**：FishyFacepunch 的 Steam 容错是本地修改（ADR 0004），升级插件时要保留。
