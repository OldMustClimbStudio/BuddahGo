# 单机模式（Solo Match）：现状与入口

Solo Match：一个 Human Player 对 0–5 个 AI Racer，完全离线，流程与 Online Match 一样完整。实现分支 `feat/single-player-mode` / PR #59 已于 2026-10-04 合入 `dev`（`e11292e`）。之后的改动按普通任务从 `dev` 开分支。

## 现状（2026-11 dev）

| 范围 | 状态 | 未闭合 |
|---|---|---|
| S1 离线 Practice | 可玩，用户已试玩；0 AI 时生成 3 个训练木桩（机制更新） | 长测、完整 benchmark（V11/V12）未跑 |
| S2 Racer 身份 | 已实现（`RacerId`，ADR 0002） | 同上 |
| S3a 五个 AI 跑完一局 | 六车同场三圈自然完赛 | 通用 Stuck Recovery、全配置长测 |
| S3b / S5 AI 施法与五种人格 | 已实现；A7 后 AI 与真人走同一施法门（`SkillExecutor.CastSlotServer`）和统一反噬 | 数值与人格辨识度等用户试玩；联机未验证 |
| S4 AI 驾驶 | `ThrustVectorPlanner` 三档难度（`Assets/Resources/AI/*.asset`），用户已验收：Hard 约 82 s、Normal 约 93 s、Easy 约 116 s 一圈 | 横向离散度、车车碰撞建模 |
| S6 体验收尾 / S7 暂停 | 未开始 | |

逐阶段证据和历史调参见 [progress.md](progress.md)；历史交接、阶段性复核和一次性修复记录在 [archive/single-player/](../archive/single-player/)。机制更新（PR #74）已合入 `dev`：单键施法、鼠标瞄准、W 推进、取消全局施法锁、统一反噬、训练木桩，规则以 [玩法深化](../gameplay/README.md) 为准；缘起在 PR #75（`feat/yuanqi`）。

## 去哪看

| 需要 | 看 |
|---|---|
| 术语 | [CONTEXT.md](../../CONTEXT.md) |
| 模块架构、接缝、依赖规则、坑 | [design.md](design.md) |
| 阶段目标与 V 项定义 | [phases.md](phases.md) |
| AI 驾驶规格 | [thrust-vector-controller-spec.md](thrust-vector-controller-spec.md) |
| AI 技能管线与人格 | [ai-skills.md](ai-skills.md) · [ai-personalities.md](ai-personalities.md) · [试玩清单](ai-skill-playtest.md) |
| 玩家与 AI 共享的技能规则（单键施法、施法节奏、执念与反噬、训练木桩） | [玩法深化](../gameplay/README.md) · [训练模式验证记录](../gameplay/practice-verification.md) |
| AI 调校与测试方法 | [ai-testing.md](ai-testing.md) |
| Solo 呈现时间线（开场到驾驶的交接） | [solo-presentation-timeline.md](solo-presentation-timeline.md) |
| 性能采样协议（要跑 benchmark 时） | [benchmark.md](benchmark.md) |
| 为什么这样定 | [Docs/adr/](../adr/) |

## Solo 改动的检查

按 [CONTRIBUTING](../../CONTRIBUTING.md) 的"按改动验证"：每个提交能编译；分支末端跑一次 EditMode。只有改到比赛流程、驾驶或 AI 行为时，才在末端跑一次 Practice 或 Solo 比赛。Solo 任务不跑联机测试（Steam 双端、联机冒烟、Solo/Online 交替），但保留这些测试资产。benchmark 只在任务涉及性能时才跑。

## 需要停下来找人

- 要改变 CONTEXT.md 里某个术语的含义，或推翻某个 ADR。
- AI 必须改运动组件或物理参数才能工作（违反 ADR 0003）。
- 难度、人格或 Fumble 数值需要团队拍板。
- 单机真实运行错误或设计冲突在现有约束下解决不了。

## 必须知道的事实

- **Steam 容错**：`FishyFacepunch.Initialize` 捕获 Steam 初始化异常，继续初始化其它传输层；`SessionLauncher` 用 Yak 启动离线 host（ADR 0004）。这是第三方插件的本地修改，升级时要保留。
- **Multipass 默认在所有传输层上启动 server**：单机必须按 design.md §5.1 的顺序只在 Yak 上启动；每次启动 client 前先 `SetClientTransport`；`GlobalServerActions` 保持 true。
- **不要在 RPC 调用栈里停止会话**：会销毁正在执行的 NetworkBehaviour。统一用 `SessionControl.Current.RequestStopSession()`，它在下一帧执行。
- **单人房间自动开始**：`RoomStateManager.StartSoloAfterInitialScenes` 等待本地连接认证和初始场景加载后调用 `TryStartSoloMatchServer`。
- **AI 的 owner 无效**：`OwnerId` 恒为 -1，`Owner` 是 EmptyConnection（不是 null）。按 owner 做的判断或键会把所有 AI 当成同一个对象；统一用 RacerId（ADR 0002，接缝见 design.md §3.3）。
- **训练木桩不是参赛者**：`MatchSpawnManager.SpawnTrainingDummies` 用同一个 Racer prefab 生成，无 owner，`RacerIdentity.IsTrainingDummy` 为真，racer id 为 −1；只加普通 MonoBehaviour（`TrainingDummy`、`TrainingDummyReadout`）。`IsServerControlled` / `HasLocalControl` 包含木桩，`IsServerAI` / `IsProgressAuthority` 不包含，所以它不计圈、不排名，但技能能找到它。数量与位置在 `Match/ModeRule` 的 Practice 行。
- **AI 的计圈和复活复用现有代码**：只把 owner 门槛换成 `IsProgressAuthority`，不另写计圈规则。
- **不在运行时添加 NetworkBehaviour**：`RacerIdentity` 和 AI 组件预先放在 prefab 上。现有代码只通过契约层（`Assets/Scripts/Match/Contracts/`）访问 Solo 模块。
- **推进**：玩家按住 W 推进（2026-10-30 起不再自动全油门），AI 经同一推进开关一直推进，只转向和施法；**运动没有线性阻力**，所以 AI 要往前推演才能稳定走线。不为好控制而改运动参数。
- **计时用 Match Clock**：GO、圈速和完赛用服务器 tick；真人圈数经约 100 ms 进度上报，不能声称冲线精确到一个 tick。
- **handoff 流畅度要实测**：本地 host 不代表姿态、镜头、输入交接自然流畅；不授权全局移除 prediction。
- **FishNet 本地修改**：`Assets/FishNet/Runtime/Utility/TransformTickSmoother.cs` 新增 `SetPresentationSuspended(bool)`，只由 `BuddahPredictionVisualRootBridge` 用于 Solo 呈现时间线；升级 FishNet 后要重新应用。
- **占位美术**：开场布局、AI 区分、头顶名字、小地图标记用 `Assets/Placeholder/` 的占位资源；地图和皮肤选择是占位，只有技能配装真正生效。
