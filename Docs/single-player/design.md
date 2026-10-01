# 单机模式设计

- 术语以根目录 [CONTEXT.md](../../CONTEXT.md) 为准。
- 不可轻易回退的决策见 [Docs/adr/](../adr/)。
- 分阶段目标见 [phases.md](phases.md)；执行入口见 [HANDOFF.md](HANDOFF.md)。
- **AI 启动前置（2026-10-01 用户最新决定）**：先完成 S1 单人 Practice，再由用户逐项试玩技能，最后等待用户明确发出开始 AI 的信号。三个条件缺一不可；S1 完成、自动测试通过、文档批准或经过一段时间都不等于开始信号。包括 S1.5 在内的任何 AI 实现、接管调试、采集/可视化工具实现与 AI 运行测试均不得提前开始；信号前只做 AI 文档规划。此前“完成后直接推进 S1.5”的指令已被覆盖。
- 用户最新确定的 AI 实施与测试顺序为：**A1 单个 AI 无技能完整一圈 → A2 单个 AI 无技能三圈比赛及平均圈速/圈间稳定性 → A3 五个独立 AI 带技能比赛，玩家入场后不操作 → A4 通知用户手动试玩完整流程**。前一步通过才推进；这同时约束实现和测试，覆盖旧方案中不同的推进顺序。技能可使用独立的简单概率决策，不要求复杂行为树；仍须遵守既有技能合法性与玩法。 见 [ai-testing.md](ai-testing.md)，当前仅文档。
- **设计事实基线**：`dev` 已合入架构重构（PR #47–#58）；§3.3 的旧代码定位和改造描述以 2026-09-30 基线为背景，不表示这些改造仍未实现。当前 S1 实现与验收状态以 [progress.md](progress.md) 为准（2026-10-01 审计 HEAD `9f2839e`）；设计目标未因局部测试结果而放宽。行号只作定位提示，以符号名为准。

## 0. 首要原则：稳定性优先

**Practice 试玩交付方式（2026-10-01 用户更新）**：功能实现完成且必要编译/基础检查通过后，告知用户可玩版本、启动方式和 [快速试玩清单](practice-playtest.md)。strict14、完整 benchmark/V12 及程序化长测不再作为先交付试玩包的条件；未跑、中断、失败各按事实保留，不因此标通过。已知会阻止正常游玩的功能问题仍须说明和处理。Practice → 用户技能试玩 → 明确 AI 开始信号的门槛及 A1–A4 不变。

1. **单机内容本身的稳定性高于功能完整度。** 这里指单机玩法和流程在玩家手里稳定运行，不是网络层的严谨程度。
   - 任何阶段交付时，Solo Match 都必须能从头到尾反复游玩，不崩溃、不卡死、没有 Exception 或 Error 日志。
   - 功能做不完可以推迟到下一阶段，但已交付的部分不能不稳定。
2. **选最简单、最可预测的实现**：
   - 能复用现有代码路径的，就不另写。
   - 能在服务器上同步完成的，就不引入异步或协程链。
   - 状态必须有明确的初始化和清理点（§4.3 的 `StopSession`）。
3. **失败要可恢复。**
   - 启动失败、AI 卡死、对象缺失都要有兜底：回到设置面板、Stuck Recovery、使用占位或跳过。
   - 不能让玩家卡在半途。
4. **单机验收独立**（2026-10-01 用户更新）：单机任务不运行任何联机测试；V2、Steam 双端和 Solo/Online 交替移出所有单机阶段门槛，标为不适用而非通过。共享契约和现有联机行为仍须保留，联机验证资产留给独立联机任务。
5. **单机有自己的 benchmark**：独立入口、配置和协议由专属 benchmark 任务在 `benchmark.md` 中维护（协议和采样模板已集成，通过状态仍须真实证据）。S1 测 0 AI Practice，后续阶段使用对应 AI 人数/难度；固定种子、路由与明确命名的输入/控制器，记录 warmup、采样轮数/时间、构建/分辨率/质量/FPS、frame time mean/p95/p99、GC/frame 与 GC/秒、内存/对象残留及完整比赛/重开完整性。只与可比的单机基线对照，不套用联机 Editor R8 数据；实机与合成证据分开。

## Practice 完整本地适配（2026-10-01 用户补充）

Practice 的目标是让本地单机完整承接既有联机流程与表现，特别是加载/场景 handoff、开场镜头/动画/倒计时到驾驶的姿态、镜头和输入控制权交接，并真实验证流畅。此要求是本地适配与验收，不是运行联机测试；也不授权全局移除 prediction。用户此前提出的本地 handoff 问题已由 `cb4a9ad` 在已测首局/Rematch 与控制事件范围内修复验证，见 [Solo 呈现时间契约](solo-presentation-timeline.md)；此结论不替代完整 Practice、自然生命周期或技能/VFX 验收。 本地 host 的输入、模拟、视觉平滑和控制权切换路径须分别核对，不能把“无远端网络延迟问题”推导为“不需要任何 handoff 验证”。流程验收覆盖设置/选择、加载就绪、开场、GO/运动解锁、自然完赛、结算、Rematch、返回/退出与再次进入，详见 phases.md 的 S1 表格。

## 1. 目标与功能清单

**Solo Match**：一名 Human Player 对 0–5 名 AI Racer，完全离线（Steam 可以不运行），一局的流程与 Online Match 一样完整。AI 数量为 0 时称为 **Practice**。

| # | 功能 | 归属阶段 |
|---|---|---|
| F1 | 没有 Steam 也能启动并进入主菜单；联机入口提示"Steam 不可用，请启动 Steam 后重开游戏" | S1 |
| F2 | 主菜单"单人游戏" → 单机设置（AI 数量 0–5、难度）→ 自动进入选择 → 比赛 → 结算 | S1 |
| F3 | 选择阶段没有超时，跳过地图投票 | S1 |
| F4 | 单机下全员完赛立即结算；Practice 中玩家冲线即结算 | S1 / S3a |
| F5 | 结算显示总用时、每圈 Lap Time 和名次（Practice 不显示名次）；按钮为"再来一局"和"返回"，没有超时 | S1 / S2 |
| F6 | Esc 弹出"退出到主菜单？"，确认后结束对局 | S1 |
| F7 | AI 作为 Racer 完整参赛：出发、计圈、完赛、名次、观战、排行榜名字、完赛后停放 | S2 / S3a |
| F8 | AI 往前推演转向，保留滑稽感；三档难度，Fumble 独立调节 | S1.5 / S4 |
| F9 | AI 卡住时自动恢复（Stuck Recovery） | S3a |
| F10 | AI 按规则施放技能，执念值和反噬规则与玩家一致 | S3b / S5 |
| F11 | 玩家冲线后可以跳过观战（Skip Spectating） | S6 |
| F12 | 其他 Racer 显示头顶名字，小地图显示对手（占位美术） | S6 |
| F13 | 暂停菜单 | S7（后续） |

## 2. 决策汇总

| 主题 | 结论 |
|---|---|
| 形态 | 对战 AI，派对向；Practice 是 0 个 AI 的 Solo Match |
| AI 启动与测试顺序 | Practice 完成 → 用户逐项试玩技能 → 用户明确开始信号；之后 A1 单 AI 无技能一圈 → A2 无技能三圈稳定性 → A3 五个独立 AI 带技能比赛 → A4 通知用户手动试玩。每步通过再下一步，信号前只做文档 |
| 技术路线 | 用 Yak 起离线 host，复用全部服务器权威代码（ADR 0001）。保留共享预测/运动链路，本地 handoff 与平滑路径须结合代码和运行分别核对 |
| Steam | 修改 FishyFacepunch，让它在初始化时容忍 Steam 缺失（ADR 0004）；Steam 晚于游戏启动时，需要重开游戏 |
| 身份 | 比赛数据按 RacerId 区分：真人 = ClientId，AI = 10000 + 序号（ADR 0002）；按连接的逻辑（名单、ready、投票、定向 RPC）保持原样 |
| 流程 | 单人房间自动开始，不需要 ready；去掉投票、选择超时、地图投票、结算超时；再来一局回到选择场景，保留 AI 数量、难度和名字；返回和退出都会关闭会话，回到主菜单首页 |
| 结束 | 沿用"第一名冲线后 15 秒"；单机下全员完赛立即结算；跳过观战等同于倒计时立即到期 |
| 计时 | 服务器用 Match Clock（以服务器 tick 为基准）为每个 Racer 记录开赛、每次过线和完赛的时刻 |
| AI 数量和难度 | AI 0–5 个，默认 3 个；难度分简单、普通、困难，只决定规划精度，不设圈速目标 |
| AI 驾驶 | 与玩家使用同一个预测运动组件；只输出 -1/0/+1；往前推演做转向规划；决策层可替换（ADR 0003）；路线用赛道样条线 |
| AI 不完美 | 四个精度旋钮（反应延迟、推演时长、重新规划间隔、候选分辨率）由难度决定；Fumble 独立；另有走线偏移；追赶机制只调决策层，不改物理 |
| AI 技能 | 配装复用 `LoadoutRules`，在合法候选中随机选；各 AI 独立维护决策，可用简单概率施法，不要求复杂行为树；保留技能合法性与效果（含遮挡降低规划精度） |
| AI 计圈和复活 | 复用现有的计圈、逆行修正和贴地复活逻辑，只把 owner 门槛改为"进度权威"判断；另加 Stuck Recovery |
| AI 完赛后 | 停止驾驶，停放到结算区的锚点，不在结算区乱跑 |
| 观战 | 沿用现有规则（跟随排名最高、还没完赛的 Racer），改为按 RacerId 查找；不提供切换 |
| UI | 头顶名字和小地图对手由 Match Rules 控制，单机开启、联机保持现状；单机默认玩家名为"玩家"（有 Steam 时用 Steam 名） |
| 美术 | 4–6 人出生点和开场布局、AI 区分、头顶名字、小地图标记都先用占位 |
| 暂停 | 本期预留 Match Clock 和 Esc 确认框；暂停菜单和其余计时器迁移放到 S7 |
| 不做 | 存档、Steam 邀请处理（代码里本来就没有这项功能）、手绘赛车线、联机房间 AI 补位、改名、经验公式规划器 |

## 3. 模块架构

### 3.1 分层与依赖规则

```text
┌──────────── 上层实现（新增）─────────────────────────────────────────┐
│ Solo/Match：SessionLauncher · OnlineMatchRules/SoloMatchRules · SoloMatchSettings │
│             RaceTiming · RaceEndPolicy · MatchClock(实现)              │
│ Racers：RacerRegistry(实现) · ServerProgressReporter                 │
│ AI：AIRacerDriver → ForwardSimPlanner(BuddahMotionModel, SplineRacingLine, │
│     AIDifficultyProfile) · AISkillCaster(IAISkillRule[]) · StuckDetector │
│ Presentation：SoloSetupPanel · SoloResultsView · QuitConfirmDialog ·   │
│               SkipSpectateButton · RacerNameTag · MiniMapRacerMarkers │
└──────────────▼ 实现 / 读取 ────────────────────────────────────────────┘
┌──────────── 现有 Gameplay / Prediction / Session（重构后）─────────────┐
│ BuddahPredictedMotor · SkillExecutor · RoomStateManager · LeaderboardManager │
│ RaceFinishManager · PropertiesSelectionManager · ResultDecisionManager … │
└──────────────▼ 只依赖 ──────────────────────────────────────────────────┘
┌──────────── 契约层 Contracts（新增，最底层）─────────────────────────┐
│ IMatchRules + MatchRules.Current · RacerId · IRacerDirectory ·        │
│ ISteeringOverride · ILocalInputBlock · ISessionControl · IMatchClock ·│
│ RaceTiming 数据（按 RacerId）· RacerIdentity（Buddah 上的同步组件）     │
└──────────────────────────────────────────────────────────────────────┘
```

**依赖规则**

1. **现有代码只依赖契约层。**
   - 它读取 `MatchRules.Current`、`IRacerDirectory`、`ISteeringOverride`、`ILocalInputBlock`、`IMatchClock`，并通过 `SessionControl.Current`（`ISessionControl`）启动和停止会话。
   - 它不引用任何 AI 或 Solo 的具体类。上层模块实现这些接口，或者直接调用现有代码的公开入口。
2. **规则的持有方式**：`SessionLauncher` 在启动会话时设置 `MatchRules.Current`（Online 或 Solo），在 `StopSession` 时复位为 Online。Solo Match 只在 host 进程中存在，所以远端客户端始终是 Online 规则。
3. **纯逻辑与 Unity/网络分离**：规划器、运动模型、技能规则、卡死判定、计时计算和规则对象都是普通类，在 `BuddahGo.Tests` 里写 EditMode 测试。
4. **AI 只在服务器运行**：AI 组件在 `!IsServerInitialized` 时保持禁用，客户端不执行任何 AI 逻辑。
5. **不在运行时添加 NetworkBehaviour**（见 `Docs/networking.md`）：
   - `RacerIdentity` 是 NetworkBehaviour，预先放在 `Buddah.prefab` 上。
   - `AIRacerDriver`、`AISkillCaster` 是普通 MonoBehaviour，也预先放在 prefab 上，默认禁用，由服务器在指定 AI 角色时启用。
6. **联机行为不变**：所有模式差异都通过 `IMatchRules` 查询，`OnlineMatchRules` 返回的就是现有行为。

**程序集与目录**：都放在 `BuddahGo.Runtime` 程序集内。以下为设计布局；S1 当前 UI 位于 `Assets/Scripts/Match/UI/`，与目录及依赖规则相关的未决差异见 progress.md，尚未据此修改设计边界。

| 目录 | 命名空间 | 内容 |
|---|---|---|
| `Assets/Scripts/Match/Contracts/` | `BuddahGo.Match` | 契约层 |
| `Assets/Scripts/Match/` | `BuddahGo.Match` | 会话、规则、计时、结算策略 |
| `Assets/Scripts/Racers/` | `BuddahGo.Racers` | Racer 注册与服务器侧进度 |
| `Assets/Scripts/AI/` | `BuddahGo.AI` | AI 全部模块 |
| `Assets/Scripts/UI/Solo/` | 沿用 UI 现有命名空间 | 单机 UI |

AI 和 Solo 只依赖契约层与现有代码的公开入口，所以以后可以拆成独立程序集；本期不拆。

### 3.2 模块清单

**契约层**

| 模块 | 职责 | 主要接口 |
|---|---|---|
| `IMatchRules` / `MatchRules.Current` | 模式差异的唯一查询点 | `AutoStartRoom`、`RequiresReady`、`SelectionTimeoutEnabled`、`SkipMapVote`、`VoteOnResults`、`ResultDecisionTimeoutEnabled`、`ReturnTarget`（Room / MainMenuHome）、`EndWhenAllRacersFinished`、`EndOnHumanFinish`（Practice）、`AllowSkipSpectating`、`AllowQuitDialog`、`AllowPause`、`ShowRacerNameTags`、`ShowOpponentsOnMinimap`、`DefaultPlayerName` |
| `RacerId` | 参赛者身份值（ADR 0002） | `IsAI`、`FromClient(id)`、`ForAI(index)` |
| `IRacerDirectory` | 按 RacerId 查询 Racer | `TryGet(RacerId, out RacerInfo)`、`All`、`TryGetByObject(NetworkObject)`；`RacerInfo` = Buddah 对象、显示名、IsAI、连接（AI 没有连接） |
| `ISteeringOverride` | 让 motor 从别处取得转向与"是否行驶" | `bool TryGetOverride(out int steering, out bool drive)` |
| `IMatchClock` | 比赛时间的唯一来源 | `Now`（秒，基准为服务器 tick）、`IsPaused` |
| `ILocalInputBlock` | 让本地玩家输入暂时失效，例如 Esc 对话框打开期间；由 owner 输入桥读取 | `IsBlocked` |
| `ISessionControl` / `SessionControl.Current` | 现有代码（SteamLobbyManager、GameNetworkManager、ResultDecisionManager）启动和停止会话的入口；由 `SessionLauncher` 实现 | `StartOnlineHost()`、`StartOnlineClient(hostSteamId)`、`StartSoloHost(settings)`、`RequestStopSession()`（在下一帧执行） |
| `RacerIdentity` | Buddah 上的同步组件，作为 Racer 信息的同步载体 | SyncVar：`RacerId`、`DisplayName`、`IsAI` |

**上层实现**

| 模块 | 类型 | 职责 | 复用场景 |
|---|---|---|---|
| `SessionLauncher` | 普通类，由 GameNetworkManager 持有 | 选择传输层并启动或停止会话（细节见 §5.1）；设置和复位 `MatchRules.Current` | 本地测试、LAN 模式 |
| `OnlineMatchRules` / `SoloMatchRules` | 纯逻辑 | 两种模式的规则值 | 新增模式时只加一个实现 |
| `SoloMatchSettings` | 不可变数据 | AI 数量、难度、AI 名字（再来一局时沿用） | 调参场景、自动化测试 |
| `MatchClock` | 服务器侧；暂停偏移由 RaceMap 场景里预放的 `MatchClockSync`（NetworkBehaviour）同步。S1 只预放该组件，偏移恒为 0 | `Now = (服务器 tick − 暂停累计 tick) × TickDelta` | 暂停、计时 |
| `RaceTiming` | 服务器侧，按 RacerId（S1 就以 RacerId 为键，此时只有 `RacerId.FromClient`） | 记录开赛、每次过线、完赛的时刻，算出 Lap Time 和总用时；结果由 RaceMap 场景里的 `RaceTimingSync`（SyncList）同步。S1 就交付 | 联机结算以后也能显示圈速 |
| `RaceEndPolicy` | 纯逻辑 | 按规则和 Racer 状态决定何时结算 | — |
| `RacerRegistry` | 每一端都有 | 由场景中所有 `RacerIdentity` 建立 RacerId 索引，实现 `IRacerDirectory`；服务器负责分配 RacerId | 联机 AI 补位 |
| `ServerProgressReporter` | 服务器侧 | 对没有 owner 的 Racer，把原本走 ServerRpc 的进度上报改成服务器上直接调用同一个登记入口 | 服务器权威计圈 |
| `AIRacerDriver` | MonoBehaviour，在 prefab 上，默认禁用 | 实现 `ISteeringOverride`：每 tick 调用规划器得到 -1/0/+1；完赛后 `drive=false` | 自动驾驶测试、联机 AI 补位 |
| `ISteeringPlanner` / `ForwardSimPlanner` | 纯逻辑 | 往前推演候选按键序列并评分（ADR 0003） | 以后可加经验公式实现 |
| `BuddahMotionModel` | 纯逻辑 | 与 motor 一致的平面运动公式（§5.3），复用 `BuddahLocomotionStep.Compute` | 规划器、调参、测试 |
| `IRacingLine` / `SplineRacingLine` | 纯逻辑包装 | 前方目标点、切线、偏离距离；带可配置的走线偏移 | 手绘赛车线 |
| `AIDifficultyProfile` | ScriptableObject | 精度旋钮、Fumble、走线偏移、追赶系数、卡死阈值 | 调参场景 |
| `AISkillCaster` + `IAISkillRule` | MonoBehaviour（服务器侧）加纯逻辑 | 判定时机，调用服务器施法入口 | 联机 AI 补位 |
| `StuckDetector` | 纯逻辑 | 判断进度停滞或长时间逆行 | 也可以给真人用 |
| `AITuningHarness` | 调参场景和工具 | 让 AI 自动跑圈、统计数据，并能施加干扰 | 回归和性能测量 |
| Solo UI | Presentation | 设置面板（含启动失败提示）、结算视图、退出确认、跳过观战、头顶名字、小地图标记 | 头顶名字和小地图标记联机也能开启 |

### 3.3 与现有代码的接缝

只在下列位置给现有系统加接入点，不重写原逻辑：

| 接缝 | 位置 | 做法 |
|---|---|---|
| **启动与传输层** | `GameNetworkManager.StartHost`（`:99`，内部先 `ServerManager.StartConnection` 再 `ClientManager.StartConnection`）；调用方 `SteamLobbyManager.cs:318/430/492/620`；MainMenu 的 NetworkManager（`MainMenu.unity:6267`，目前只挂了 FishyFacepunch） | 改用 Multipass，子传输层顺序固定为 `[0]=FishyFacepunch, [1]=Yak`；所有启动和停止都走 `SessionLauncher`（§5.1） |
| **单人房间自动开始** | `RoomStateManager.RequestStartGame`（`:266`，现在唯一的调用方是 `RoomUI.cs:199`）调用 `StartGameServerRpc`（`:352`，host 校验在 `:362`）；host 标记在 `OnStartClient` 刷新（`:156-159`）；第一个 host 会自动 ready（`:569`），`CanHostStartGame`（`:78`）随即成立 | 新增服务器侧 `TryStartSoloMatchServer()`：在 host 刷新之后，`MatchRules.Current.AutoStartRoom` 为真时，**延后一帧**（或者等 `OnClientLoadedStartScenes` 之后）调用 `TransitionToPropertiesSelector`（`:629`）。不经过 ready UI |
| **转向与行驶** | `BuddahPredictedMotor.BuildReplicateData`（`:284`，读转向在 `:292`，油门固定为 `movementAllowed ? 1 : 0`，在 `:293`） | 如果本端对该对象有权威（服务器且无 owner，或者 owner 本身），并且存在启用的 `ISteeringOverride`，就使用它给出的转向和 `drive`；否则走原来的 owner 输入桥。S1.5 的调试接管走"owner 本身"这条分支 |
| **本地输入屏蔽** | `BuddahPredictionOwnerInputBridge.ReadSteering`（`:42-48`） | `ILocalInputBlock.IsBlocked` 为真时返回 0（Esc 对话框打开期间） |
| **生成** | `MatchSpawnManager`（`:97`、`:117`、`:123`；出生点列表只有 3 个，`RaceMap.unity:7743-7746`；`GetSpawnPoint` 按 `%` 循环，`:158`） | 真人和 AI 在同一轮生成，并且在开赛就绪判定和开场收集车身之前完成：AI 无 owner、分配 RacerId、出生点序号接在真人之后；用 `SkillLoadout.SetSlotsServer`（`:43`）写入配装。出生点补到 6 个（占位） |
| **开场** | `IntroSequenceManager`：布局只有 1–3 人（`RaceMap.unity:13424-13434`）；用 `FindObjectsByType` 收集车身（`:489`）；排序比较函数在 `:491-500` | 4–6 人用占位布局；按 RacerId 排序，真人在前 |
| **出发交接** | `RaceBodyIntroStateController`（`:301`、`:353` 会把非本地 owner 的车设为 kinematic）；`TryApplyServerAuthoritativeLaunchHandoff`（`BuddahPredictedMotor.Events.cs:175`） | 没有 owner 的车在服务器上走权威交接路径，不设 kinematic |
| **进度与计圈** | `LapProgress`（`Update`、`OnTriggerEnter`、`:119` 和 `TryAdvanceCheckpoint :296` 都有 owner 门槛，前两处在 `:58/:75`）；`PlayerProgressReporter.ReportCheckpoint`（`:110`）；`RaceCompletionTracker`（`:61`、`:248`，逆行判定 `EvaluateWrongWayState` 在 `:188-222`）；`PlayerProgressReporter`（`:28` 有 IsOwner 门槛，ServerRpc 在 `:74`）；`SplineProgressTracker`（没有门槛） | **复用，不重写**：把这些 owner 门槛统一改为 `IsProgressAuthority = IsOwner \|\| (IsServerInitialized && !Owner.IsValid)`。`ServerProgressReporter` 只负责把"ServerRpc 上报"这一步换成服务器上的直接调用。在 host 上，AI 的刚体真实参与物理，trigger 会正常触发 |
| **排行榜** | `LeaderboardManager._progressByClientId`（`:21`）、`RankEntry`（`:350`）、显示名 `"{gameObject.name} #{OwnerId}"`（`PlayerProgressReporter.cs:193-195`） | 键改为 RacerId；`RankEntry.ClientId` 改名为 `RacerId`；显示名取自 `IRacerDirectory` |
| **完赛与结算** | `RaceFinishManager`：`_finishedClientIds`（`:27`、`:120-127`）、`firstFinisherClientId`、`TryGetOwnedCompletionTracker`、`ResolvePlayerNameForClient`（只查 RoomStateManager）、15 秒倒计时（`:76-84`）；`FinalMatchResultEntry.ClientId` | 全部改为 RacerId；名字从 `IRacerDirectory` 取；结算时机交给 `RaceEndPolicy` |
| **结算演出** | `MatchResultPresentationCoordinator`（`:47`、`:119`、`TryGetReporter :248-254`） | 键改为 RacerId；在 `ResultInteractive` 阶段，单机用 `SoloResultsView` 替换 `ResultDecisionUI` |
| **结算决策** | `ResultDecisionManager`：60 秒超时（`:20`、`:212-219`，超时分支在 `:223` 调用 `FinalizeDecisionServer`）、参与者（`:191`）、`FinalizeDecisionServer`（定义在 `:277`）调用 `ReturnToRoomMenuKeepingSessionServer`（`:307`，会加载 MainMenu，但不关闭网络） | 单机下不计超时；"再来一局"沿用现有的重开路径；"返回"由 `FinalizeDecisionServer` 根据 `ReturnTarget=MainMenuHome` 改为调用 `SessionControl.Current.RequestStopSession()`。**不能在 RPC 调用栈里同步停止会话**，否则会销毁正在执行的 NetworkBehaviour，所以要延到下一帧执行 |
| **结算区** | 现有停放发生在黑屏时：`MatchResultPresentationCoordinator.HandleTimelineBlackScreenFullyCoveredServer`（`:173-203`）按 `FinalRank-1` 取锚点，然后调用 `PlayerProgressReporter.TeleportToHiddenResultAreaServer`（`:139`）；`RaceResultAreaManager._placedClientIds`（`:26`）和 `NotifyPlacementApplied(int clientId)`（`:91`）按 ClientId 记录；`allowMovementInResultArea: 1`（`RaceMap.unity:12485`） | AI 完赛时只设 `drive=false`。黑屏时由现有逻辑按 RacerId 停放（`_placedClientIds` 和 `NotifyPlacementApplied` 改为按 RacerId）。AI 在结算区里始终保持 `drive=false` |
| **观战** | `RaceSpectatorTargetResolver`（`:45-51`）、`CinemachineLocalPlayerFollower`（`:60-72`） | 按 RacerId 查找目标，规则不变 |
| **施法** | `SkillExecutor.CastSlotServerRpc`（`:176`）已经拆成 `TryResolveCast`（`:185`）、`ResolveCastVariant`（`:224`）、`QueueCast`（`:261`） | 新增服务器侧 `CastSlotServer(slot)`，复用这三步 |
| **owner 表现** | `Apply*ToOwner` 的 TargetRpc（`SkillExecutor :350-417`，只判断 `conn == null`，EmptyConnection 仍会发送） | 没有 owner 时不发 TargetRpc；拖尾、缩放、Feel 改为由 observers 路径在 host 上播放 |
| **复活** | `BuddahRespawn`（门槛 `IsLocalOwner :226-231`，用在 `:85`、`:132`）；`TryApplyServerAuthoritativeTeleport`（`Events.cs:256-270`）；`RequestResetActiveSkillEffectsForRespawn` 有 IsOwner 门槛（`SkillExecutor :114`），`ResetActiveSkillEffectsForOwner`（`:139`）没有 | 门槛改为 `IsProgressAuthority`；AI 的复活和 Stuck Recovery 直接调用服务器权威传送，以及 `ResetActiveSkillEffectsForOwner` |
| **执念值追赶** | `ObsessionFigure`（`:114` 用 `entry.ClientId == OwnerId`） | 改为按 RacerId 查找 |
| **名字** | `RoomStateManager.SubmitLocalDisplayName`（`:601-611`，Steam 无效时在 `:603` 直接 return）；`PlayerIdentity.ResolvePlayerName`（回退为 `Player {id}`） | 单机里没有 Steam 时，真人名字用 `MatchRules.Current.DefaultPlayerName`（"玩家"）；AI 名字来自配置列表 |
| **选择阶段** | `PropertiesSelectionManager`（阶段在 `:563-568`，超时 60 秒，倒计时在 `:596`） | 单机下跳过地图阶段，取消超时；皮肤阶段保留（它本来就只是占位） |
| **主菜单** | `MainMenuUI`（`:215-224` 按 IsInLobby 切换面板；`:159` 已经处理 Esc） | 首页新增"单人游戏"入口；Steam 不可用时禁用联机入口并提示 |

## 4. 关键流程

### 4.1 开始一局 Solo Match

```text
主菜单「单人游戏」→ SoloSetupPanel（默认取上次的选择）
  → SessionLauncher.StartSoloHost(settings)
      按 §5.1 的顺序启动；失败时回滚，留在设置面板显示错误信息
  → RoomStateManager.OnStartClient 刷新 host（第一个 host 自动 ready）
  → TryStartSoloMatchServer()（延后一帧）→ TransitionToPropertiesSelector → 选择场景
  → 选择：技能配装 → 皮肤（占位）；没有超时，跳过地图投票
  → RaceMap：同一轮生成真人和 N 个 AI，登记 RacerIdentity
  → Intro：按 Racer 数量选布局（4–6 人用占位）；服务器权威交接
  → GO：RaceTiming 用 MatchClock 记录开赛时刻
```

### 4.2 比赛中，每个服务器 tick

```text
AIRacerDriver.TryGetOverride → ForwardSimPlanner.Plan → 转向 (-1/0/+1)，drive=true
BuddahPredictedMotor.BuildReplicateData 取得覆盖值 → 正常模拟（与玩家同一套物理）
LapProgress / RaceCompletionTracker：在服务器上以"进度权威"身份运行 → ServerProgressReporter 直接登记
  → 检测到过线时调用 RaceTiming.OnLapCompleted
StuckDetector 判定卡死 → 服务器权威传送，并重置技能效果
AISkillCaster 按规则判定 → SkillExecutor.CastSlotServer
真人照旧由 owner 上报；服务器观察到圈数增加时记录过线时间（精度约 ±0.1 秒，取决于上报间隔）
```

### 4.3 结束、跳过观战、退出、再来一局

```text
RaceEndPolicy：
  联机 → 第一名冲线后 15 秒
  单机 → 15 秒，或者全员完赛（取先到者）；Practice 中玩家冲线立即结束
  跳过观战 → 立即结束，未完赛的 Racer 按"倒计时到期"的规则排名
AI 完赛 → drive=false；黑屏时由现有逻辑按 RacerId 停放到结算区锚点
结算（ResultInteractive）：SoloResultsView 显示名次（Practice 不显示）、总用时、每圈 Lap Time；没有超时
  再来一局 → 沿用现有的重开路径回到选择场景；SoloMatchSettings 和 AI 名字沿用，配装重新随机
  返回     → FinalizeDecisionServer → SessionControl.Current.RequestStopSession()（下一帧执行）
Esc（选择、开场、比赛阶段）→ QuitConfirmDialog：
  对话框打开期间，比赛仍在进行（还没有暂停），但 ILocalInputBlock 屏蔽玩家车辆的转向输入
  确认 → StopSession()（不结算）；取消 → 恢复输入
StopSession() 的顺序：ClientManager.StopConnection() → ServerManager.StopConnection(true) → MatchRules.Current 复位为 Online
  → 清理 SoloMatchSettings 和 ResolvedPropertySelectionCache → LoadScene(MainMenu)（参照 RoomUI.cs:203-208 的现有做法）
```

## 5. 各部分设计要点

### 5.1 离线启动与会话（S1）

**N3 原始问题（S1 前基线；当前容错已实现）**
- `FishyFacepunch.Initialize` 里的 `SteamClient.Init`（`FishyFacepunch.cs:98-101`）会抛出 `NoSteamClient`，紧接着的 `SteamNetworking.AllowP2PPacketRelay`（`:103`）同样依赖 Steam。
- 异常发生在 `NetworkManager.InitializeComponents` 的 TransportManager 一步，之后的各个 Manager 都不会初始化。
- `Multipass.Initialize` 会逐个初始化所有子传输层（`:150-161`）。
- 原始结论：必须先完成 ADR 0004 的容错。当前 `FishyFacepunch.Initialize` 已捕获 Steam 初始化异常并继续初始化传输层；历史离线启动验证见 progress.md。

**Multipass 的用法（当前 S1 已实现，历史 Yak 传输验证见 progress.md）**

*前置配置*
- S1 已将 MainMenu 的 `TransportManager.Transport` 从 FishyFacepunch 改为 Multipass。子传输层固定为 `[0]=FishyFacepunch, [1]=Yak`；旧场景行号不再代表当前序列化位置。
- `GlobalServerActions` 保持 true。设为 false 会让 `ServerManager.StartConnection` 和 `StopConnection` 直接报错并失败（`Multipass.cs:517-525/831/881`）。

*为什么不能走 `ServerManager.StartConnection()`*
- 它只是透传给 `Transport.StartConnection(true)`（`ServerManager.cs:357-360`），而 Multipass 会在所有子传输层上启动 server（`Multipass.cs:826-838`）。
- 没有 Steam 时，FishyFacepunch 只记录错误并返回 false（`FishyFacepunch.cs:398-401`），但 Yak 仍然会被启动，留下一个"半启动"的 server。

*直接启动指定传输层是安全的*
- `ServerManager.Started`、场景对象的建立都发生在事件回调 `Transport_OnServerConnectionState` 里（`ServerManager.cs:520-553`；`Started = IsAnyServerStarted()`）。
- `ServerObjects` 只在 `IsOnlyOneServerStarted()` 时才执行 `SetupSceneObjects`，这本来就是为 Multipass 设计的。
- 所以直接调用 `multipass.StartConnection(true, index)` 时，ServerManager、场景加载、对象生成都照常工作。

*启动和停止的顺序*

| 场景 | 顺序 |
|---|---|
| 单机 | `MatchRules.Current = Solo` → `mp.StartConnection(true, 1)`（失败则回滚并报错）→ `mp.SetClientTransport(1)` → `ClientManager.StartConnection()`（失败则停止 server、回滚并报错） |
| 联机 host | `MatchRules.Current = Online` → `mp.StartConnection(true, 0)` → `mp.SetClientTransport(0)` → `ClientManager.StartConnection()` |
| 联机 client | `mp.SetClientTransport(0)` → `mp.SetClientAddress(steamId, 0)` → `ClientManager.StartConnection()` |
| 停止 | `ClientManager.StopConnection()` → `ServerManager.StopConnection(true)` |

- **停止时**：`ServerManager.StopConnection(true)` 会停止所有子传输层，没有启动的那个返回 false 属于正常情况，不能当作失败处理。
- **每次启动前都要重设 `ClientTransport`**：它是持久字段，切换模式后不会自动复位。`StartConnection(false, index)` 不看 index，只使用 `ClientTransport`（`:857-872`）。
- **Yak 的特性**：
  - 只支持一个本地 client。
  - client 先于 server 启动时会停在 Starting，server 启动后自动转为 Started。
  - `GetConnectionAddress` 返回空串（`Yak.cs:55-58`），所以 host 判定会回退为"本地连接就是 host"（`RoomStateManager :578-590`）。

**SessionLauncher**：实现 `ISessionControl`（契约层），接口见 §3.2。`SteamLobbyManager` 中现有的 4 处启动和停止调用（`:318/430/492/620`），都改为通过 `SessionControl.Current` 调用。

**单人房间**
- host 身份已由 #48（N1）修正：没有大厅时，"本地连接就是 host"。
- 自动开始见 §3.3 的"单人房间自动开始"。

**会话清理**：`StopSession()` 必须清除 global 的 `RoomStateManager`、`MatchRules.Current`、`SoloMatchSettings` 和静态缓存。单机 V8 通过 Solo → Home → Solo 的退出、重开和连续 Rematch 验证残留，不运行 Solo/Online 交替。

### 5.2 Racer、名字与计时（S2）

- **RacerRegistry 必须新建**：现有的 `PlayerRegistry` 只是 `BuddahMovement` 的列表，没有 id 索引。
- **同步载体是 Buddah 上的 `RacerIdentity`**：服务器在生成时写入 RacerId、显示名和 IsAI，每一端都由它建立索引。这样联机时头顶名字也能用。
- **计时**
  - S1 前的问题：完赛时间用 `Time.unscaledTimeAsDouble`，GO 时间用 tick 换算出的网络时间，两者不能相减。
  - 当前 S1 已用 Match Clock 记录 GO、`RaceTiming` 与完赛；S2 继续做身份迁移。服务器时钟精度不等于真人过线采样精度：圈数经 `PlayerProgressReporter` 约 100 ms 上报后观察，历史实测不能证明单 tick 冲线精度。
  - `RankEntry.FinishServerTime` 的含义保持不变，联机界面照旧使用它；Lap Time 放在 `RaceTimingSync` 里。
- **改名的真正风险**：`RankEntry` 改名后，线上格式不变（所有端都是同一个构建），真正的风险在调用点，例如 `RaceSpectatorTargetResolver.cs:45/51`、`ObsessionFigure.cs:114`，S2 要逐个修改并回归。

### 5.3 AI 驾驶（S1.5 / S4）

**运动事实（规划器必须遵守）**

| 参数 | 值 | 位置 |
|---|---|---|
| 质量 | 2 | `Buddah.prefab:572` |
| 线性阻力 | 0，横向速度不会衰减 | `Buddah.prefab:573` |
| 角阻力 | 0.05 | `Buddah.prefab:574` |
| 刚体约束 | 锁 X、Z 旋转 | `Buddah.prefab:589` |
| 推进 | 沿水平朝向施加 `FinalForwardForce × throttle` | `BuddahLocomotionStep.Compute`（`:37`），motor 在 `:460` 调用 |
| 转向输入 | `steering × TurnInputMultiplier` | motor `:292` |
| 转向 | 按住时 `AddTorque(steering × FinalTurnTorque)` | motor `:476` |
| 松键衰减 | 角速度按 `TurnDecayPerSecond` 线性衰减 | motor `:481-485`，`Mathf.MoveTowards` |
| 速度上限 | `ClampPlanarSpeed(FinalMaxSpeed + 推人宽限期 6)` | motor `:429`，定义在 `:671` |
| 特殊状态 | `IsSteeringSuppressed` 时转向为 0；开场交接期间输入会被缩放 | motor `:448-451` |
| 默认配置 | 50 / 30 / 3 / 80 | `BuddahPredictedMotorConfig.cs:9-12`，prefab `:606-609` |
| 玩家输入 | 数字量，A/D 只能给出 -1、0、+1 | `PlayerBuddahInputSource.GetSteering :34` |

**规划方法**
- 每隔"重新规划间隔"个 tick，用 `BuddahMotionModel` 把一组候选按键序列向前推演"推演时长"个 tick，候选形如"左 / 不按 / 右，保持 k tick 后松开"。
- 评分项：与走线的横向偏离、速度方向与切线的夹角、前方目标点的进度、与赛道边缘的接近程度。
- 推演不含碰撞，撞墙或被推后靠下一次重新规划修正。
- 规划器读取当前的 `Final*` 数值和特殊状态，技能改变推力或质量后自动生效。

**精度与追赶**
- 难度只决定精度：反应延迟（使用 N tick 前的状态）、推演时长、重新规划间隔、候选分辨率。
- Fumble 在输出端以一定概率替换按键。
- 遮挡视野期间：推演时长缩短，反应延迟增加。
- 追赶：落后的 AI 降低 Fumble、提高精度；领先的 AI 反过来。

**S1.5 可行性验证**：仅在 AI 启动前置满足后，在 Practice 中，通过调试开关让 `AIRacerDriver` 作为 `ISteeringOverride` 接管玩家自己那台 Buddah（host 就是 owner，走"owner 本身"分支）。不需要 Racer，也不需要生成 AI。

**可量化的验收指标与轨迹证据**

先建立无技能干扰的基础驾驶记录，核对后再加入技能。建议固定场景、seed、初始条件和控制参数，记录位置、Match Clock 时间、tick、圈号及赛段；按圈叠加路线，标注碰撞、复位、卡住、技能命中与恢复。具体字段、图表和未定阈值见 [ai-testing.md](ai-testing.md)。相同 seed 不保证物理轨迹逐位一致。

- 计划/实际完成圈数、逐圈用时、圈及比赛完成率、撞墙次数、平均横向偏离；未完圈不填零圈速，不隐藏失败样本。
- 同配置逐圈轨迹的一致性、偏离位置与重复失误赛段；圈速改善和稳定性改善分开判断。
- 各 AI 之间横向位置的离散度（用来衡量有没有排成一列）。
- 超车次数。
- 规划耗时：用 profiler marker `AI.Plan` 统计，每帧汇总。

### 5.4 AI 技能（S3b / S5）

A2 单车无技能三圈通过后，在 A3 实现五个独立 AI 带技能比赛。玩家入场但开赛后不操作，各 AI 独立选择目标与动作，可用简单概率规则，不要求复杂行为树。单项推挤、减速、加速等受控干扰可用于诊断；它不能代替 AI 实际施放、五车比赛与自然完赛证据。不同合法配装记录条件/目标/时机/效果，矩阵见 [ai-testing.md](ai-testing.md)。保留冷却、执念、反噬及现有比赛结束规则，不以改变玩法获得通过。

- **配装**：先打乱 `LoadoutRules` 的候选顺序，再用 `FindNextAutoFillSkillId` 逐槽填充，最后用 `SkillLoadout.SetSlotsServer` 写入。默认不允许重复。
- **施放时机参考**：以下条件是既有设计参考；用户允许 A3 使用简单概率决策，不要求完整复杂策略后才开始测试。保留合法性检查，为每个 AI 记录独立的决策状态与随机流；概率/尝试间隔尚待定。

| 技能 | 普通版施放时机 |
|---|---|
| 加速 | 前方是直道 |
| 减速陷阱 | 身后近处有 Racer |
| 反转转向 | 前方有 Racer 正在过弯 |
| 遮挡视野 | 附近有对手 |
| 推手 / 弹丸 | 侧前方有 Racer |
| 巨大化 | 周围 Racer 多 |

- 难度只影响规则判断的精度。反噬概率照常由执念值决定。技能池以配置仓库为准。

### 5.5 计圈、复活与 Stuck Recovery（S3a）

- 计圈、逆行修正（`EvaluateWrongWayState :188-222`）和贴地复活（`RespawnFloor` 持续接触 2 秒）都**复用现有代码**，只改门槛（§3.3）。这样 AI 和真人走同一套校验，以后不会分叉。
- 新增的 `StuckDetector`：样条线进度连续 N 秒没有前进（默认 3 秒），或者逆行超过 M 秒，就复活到当前进度处，朝向对齐切线。阈值放在 `AIDifficultyProfile` 里。

### 5.6 Match Clock

**基准**：服务器 tick。`Now = (Tick − 暂停累计 tick) × TickDelta`。暂停累计值由 RaceMap 场景里预放的 `MatchClockSync`（NetworkBehaviour，SyncVar）同步到客户端。

**S1 范围（只做服务器侧）**：只让 `RaceTiming`、`RaceEndPolicy` 和"第一名冲线后 15 秒"倒计时（`RaceFinishManager :78/:125`）使用 Match Clock。联机下倒计时的时长不变。

**S7 范围（暂停）**：其余计时器在 S7 再迁移。其中复活和推人判定框跑在 owner 客户端上，需要客户端也能读到同步后的时钟。

| 计时器 | 位置 |
|---|---|
| 开赛倒计时 | `RoomStateManager :861` |
| 技能冷却与施法延迟 | `SkillExecutor :213/:271/:287` |
| 执念值衰减 | `ObsessionFigure :83` |
| 复活 | `BuddahRespawn :63/:76/:203` |
| 推人判定框 | `PushHitbox :51/:105` |
| 完赛宽限期 | `RaceCompletionTracker :72` |
| 结算倒计时 | `MatchResultPresentationCoordinator :268`、`ResultDecisionManager :212` |
| 选择阶段倒计时 | `PropertiesSelectionManager :596` |

FishNet 的 tick 按 `unscaledDeltaTime` 累加（`TimeManager.cs:700`），所以暂停方案在 S7 设计。

### 5.7 可用性与占位美术

**可用性**
- 完整功能的设置默认值是上一次的选择（用 PlayerPrefs 记住），首次默认为 3 个 AI、普通难度。S1 按 phases.md 的阶段限制锁定 0 AI Practice，只保留难度选择；不能把此状态记为 0–5 AI 功能已完成。
- 启动失败时留在设置面板，显示原因。
- Steam 不可用的提示写明"请启动 Steam 后重开游戏"。
- 结算界面上，"再来一局"是默认焦点。
- 所有单机对话框都可以用键盘操作。

**占位美术**

| 内容 | 占位做法 |
|---|---|
| 4–6 号出生点 | 在现有出生点之后按间距补位 |
| 4–6 人开场布局 | 复制现有布局，按间距平移 |
| AI 区分 | 给 AI 的材质加一个颜色偏移 |
| 头顶名字 | TMP 文字，朝向相机 |
| 小地图对手 | 彩色圆点 |

所有占位资源都放在 `Assets/Placeholder/`，方便以后统一替换。

### 5.8 AI 名字

AI 名字放在配置里（不写在代码中），当前列表如下：

| 名字 | English | 由来 |
|---|---|---|
| 漂移禅师 | Drift Zen Master | 这个游戏本来就以漂移为主 |
| 轮回圈王 | Lap King of Samsara | 跑圈就是在轮回 |
| 面壁达摩 | Wall-Facing Bodhidharma | 专门撞墙的那位 |
| 金刚不刹 | Diamond No-Brakes | 金刚不坏，也不刹车 |
| 回头是岸 | Turn-Back Shore | 逆行专业户 |

**分配规则**
- 每局开始时，把列表随机打乱后依次分给 AI，同一局里名字不重复。
- "再来一局"时沿用上一局的分配，名字不变（§2 的"流程"一行）。
- 列表长度不足 AI 数量时，不够的用占位名"AI {序号}"补齐。

## 6. 风险

| 风险 | 应对 |
|---|---|
| 规划器无法稳定驾驭这套物理 | 获得用户 AI 开始信号后，在 S2/S3 前执行 S1.5；ADR 0003 保留经验公式作为备选 |
| Multipass 只启动单个传输层的行为与预期不同 | S1 历史 Yak 启停验证已有结果，见 progress.md；传输实现改变时复核 §5.1 的 API |
| 改 RacerId 时意外扩大共享代码范围 | S2 单独成阶段，遵守既有契约与兼容约束；联机测试属于独立任务，不进入单机 gate |
| 长时间连续游玩时出现泄漏、状态累积或偶发异常 | 每个阶段都跑稳定性浸泡测试（V11） |
| AI 卡在 owner 门槛上 | §3.3 已逐个列出接缝，S3a 的完成标准逐项覆盖 |
| 单机会话重开时状态残留 | 统一由 `StopSession` 清理；V8 验证 Solo、Home、Solo 及 Rematch 循环 |
| host 上的视觉平滑层（TickSmoother）出现抖动 | V7 专项观察；不要改预测逻辑 |
| 5 个 AI 的规划开销 | V9：用 `AI.Plan` marker 统计，发布构建中每帧总耗时 < 1ms |
