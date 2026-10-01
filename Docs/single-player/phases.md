# 单机模式分阶段目标

- 设计依据见 [design.md](design.md)，执行流程见 [HANDOFF.md](HANDOFF.md)，进度记在 [progress.md](progress.md)。
- 每个阶段写明**目标**、**涉及模块**、**完成标准**和**验证项**。具体实现由执行者在读完相关代码后决定，但必须遵守 design.md §3 的模块边界、依赖规则和 ADR。
- 各阶段按顺序进行，每个阶段向 `dev` 开一个 PR；已有阶段 PR 时继续使用，协作任务提交由主任务集成。S1 当前为 draft PR #59。
- **稳定性优先**（design.md §0）：每个阶段交付时，单机都必须能从头到尾反复游玩，并通过 V11 稳定性浸泡测试与 V12 单机 benchmark。用户于 2026-10-01 明确取消单机任务内的所有联机测试：V2 不适用，不运行 Steam 双端、联机冒烟或 Solo/Online 交替。

## S0 同步重构结果（已完成）

**目标**：本分支建立在重构之后的代码上。

**已完成**
- 已合并包含 #47–#58 的 `dev`。
- design.md 已按合并后的代码重新核对所有定位和事实（核对日期 2026-09-30）。

## S1 离线底座与 Practice 跑通

**目标**：不开 Steam，也能从主菜单完整玩完一局 Practice（0 个 AI）；联机体验不变。

**涉及模块**
- 契约层：`IMatchRules`/`MatchRules.Current`、`ISessionControl`/`SessionControl.Current`、`ILocalInputBlock`、`IMatchClock`、`RacerId`（本阶段只用 `RacerId.FromClient`）
- `SessionLauncher`（Multipass：FishyFacepunch + Yak，按 design.md §5.1 的顺序启动和停止）、`Online/SoloMatchRules`、`SoloMatchSettings`、`MatchClock`（加上预放的 `MatchClockSync`，偏移恒为 0）、`RaceTiming` + `RaceTimingSync`（按 RacerId，本阶段只有真人）、`RaceEndPolicy`
- Solo UI：设置面板、结算视图、退出确认
- FishyFacepunch 容错（ADR 0004）
- design.md §3.3 中的这些接缝：启动与传输层（含把 `TransportManager.Transport` 改为 Multipass）、单人房间自动开始、本地输入屏蔽、完赛与结算（Practice 冲线即结算，15 秒倒计时改用 Match Clock）、结算演出（`SoloResultsView` 替换 `ResultDecisionUI`）、结算决策、选择阶段、名字、主菜单

**完成标准**
1. 第一步先实测 design.md §5.1 的 Multipass 用法：只在 Yak 上启动 server 和 client，ServerManager 和 ClientManager 的连接事件都正常触发，FishyFacepunch 没有启动 server。实测结论记进 progress.md。
2. Steam 关闭时：
   - 能进入主菜单，启动日志里没有 `NoSteamClient` 及其连带异常（N3）。
   - 联机入口显示"Steam 不可用，请启动 Steam 后重开游戏"并且不可用；"单人游戏"可用。
   Steam 开启时的建房和加入仍须保留 `SessionLauncher` 接入及既有行为；这是实现约束，单机任务不执行联机运行验收，也不将其记为已通过。
3. 设置面板能选择 AI 数量（0–5）和难度，默认值是上次的选择。本阶段 AI 数量大于 0 时，先按 0 处理，或暂时禁用该选项。
4. 通过 Yak 起本机 host，完整走完一局：
   - 单人房间自动开始，不需要 ready。
   - 选择阶段：没有超时，跳过地图投票。
   - 入场 → 比赛 → 冲线后立即结算。
   - 结算显示总用时和每圈 Lap Time。
   - 结算没有超时；可以"再来一局"或"返回"。"返回"会完整关闭会话（`StopSession`），不会留下运行中的 host。
   - 启动失败时：留在设置面板并显示原因。
   - 没有 Steam 时，玩家名显示为"玩家"。
5. Esc 确认退出（选择、开场、比赛阶段都可用）：
   - 对话框打开期间屏蔽玩家车辆的转向输入。
   - 确认后完整关闭会话，回到主菜单首页；之后可以再开单机，且无残留状态。
6. 联机与单机的所有差异都通过 `IMatchRules` 查询，代码里没有散落的 `if (solo)`。
7. `RaceTiming`、`RaceEndPolicy` 和"第一名冲线后 15 秒"倒计时改用 Match Clock（服务器 tick 为基准，design.md §5.6 中的 S1 范围）；联机下倒计时的时长不变。其余计时器留到 S7。
8. 现有代码只通过契约层访问新模块，不引用 Solo 或 AI 的具体类。
9. `IMatchRules`、`RaceEndPolicy`、`RaceTiming`、`MatchClock` 的计算部分有 EditMode 测试。

**验证**：V1、V3、V8、V10、V11、V12。

## S1.5 规划器可行性验证

**目标**：在投入 S2/S3 之前，证明"往前推演、只按左/不按/右"的规划器能够稳定驾驭现有物理（ADR 0003）。

**涉及模块**：`ISteeringOverride` 接缝（"owner 本身"这条分支）、`BuddahMotionModel`、`ISteeringPlanner`/`ForwardSimPlanner`、`IRacingLine`/`SplineRacingLine`、`AIRacerDriver`、`AIDifficultyProfile`（先只做一档）。

**完成标准**
1. `BuddahMotionModel` 与 motor 一致：同样的初始状态和按键序列，推演 60 tick 后的位置、朝向、速度与实际模拟的偏差在约定阈值内（无碰撞路段）。这一项写成 EditMode 测试，再加一次场景内对照。
2. 在 Practice 中打开调试开关，让 `AIRacerDriver` 接管玩家自己那台 Buddah 的转向：能连续完成 3 圈，并记录圈速、撞墙次数和平均偏离。
3. 规划器只输出 -1/0/+1；在加速、巨大化等改变 `Final*` 的效果下仍能跑完。
4. 调试开关关闭时，玩家输入路径与之前完全一致。
5. 结论写进 progress.md 的调参记录，然后二选一：
   - 跑通：继续往下做。
   - 跑不通：停下来，由团队决定是否启用经验公式方案。

**验证**：V6（单车版）、V9（单车规划耗时；5 AI 的 < 1ms 目标留到支持该负载时）、V10、V11（当前支持配置）、V12。

## S2 Racer 身份

**目标**：比赛数据的归属从"连接"改为"Racer"（ADR 0002），单机稳定且身份迁移完整。

**涉及模块**：契约层的 `IRacerDirectory`、`RacerIdentity`（预先放在 `Buddah.prefab` 上）；`RacerRegistry`；把 S1 已有的 `RaceTiming` 接入 Racer 查询；design.md §3.3 中的排行榜、完赛与结算（包括 `firstFinisherClientId`、`TryGetOwnedCompletionTracker`、`ResolvePlayerNameForClient`、`FinalMatchResultEntry.ClientId`）、结算演出、观战、开场排序、执念值。

**完成标准**
1. 以下数据全部以 RacerId 为键：进度、完赛登记、名次和排行榜、Lap Time、结算演出、观战目标、执念值中"离领先者的距离"、开场排序、显示名。
2. 进度、完赛、名次、观战这些代码路径里，不再用 OwnerId 或 ClientId 作为键。名单、ready、投票、owner 定向 RPC 这类按连接的逻辑保持原样。
3. `RankEntry.ClientId` 改名为 `RacerId`，类型和顺序不变。排行榜显示名取自 RacerRegistry。
4. 已提供"注册一个没有连接的 Racer"的服务器入口，并有 EditMode 测试覆盖。
5. 排行榜显示名从 `gameObject.name #id` 改为玩家名，是有意的改动，要在 PR 里写明；以适用的单机用例验收身份和名字。
6. 改动 `Buddah.prefab` 后，R10（序列化）检查通过：现有字段的值不变，只多了 `RacerIdentity` 组件。

**验证**：V3、V10、V11、V12。

## S3a AI 完整跑完一局

**目标**：AI Racer 作为服务器持有、没有 owner 的 Buddah，能完整参加一局。本阶段 AI 可以用 S1.5 的规划器，也可以直线全速。

**涉及模块**：生成、开场、出发交接接缝；进度与计圈接缝（owner 门槛统一改为 `IsProgressAuthority`）、`ServerProgressReporter`、复活接缝、`StuckDetector`、结算区停放、`RaceEndPolicy`（全员完赛）。

**完成标准**
1. 按 `SoloMatchSettings` 生成 N 个 AI：
   - 每个有独立的 RacerId。
   - 出生点不冲突。
   - 名字取自配置列表（列表为空时用占位名）。
   - 配装按 design.md §5.4 生成。
2. 出生点补到 6 个（占位），开场布局能容纳实际人数（4–6 人用占位布局），联机 4–6 人时同样生效。AI 和真人在同一轮生成，并且在开赛就绪判定之前完成。
3. AI 能正常出发：服务器权威交接，不会被设成 kinematic 锁住。
4. AI 的进度、计圈、Lap Time、完赛都在服务器上正确记录：复用 `LapProgress`、`RaceCompletionTracker` 的原有逻辑，没有另写一套计圈规则。名次、结算、观战、排行榜中都以各自的名字出现。
5. AI 的逆行修正、贴地复活、Stuck Recovery 都在服务器侧生效。
6. "全员完赛立即结算"生效。AI 完赛后 `drive=false`；黑屏时由现有停放逻辑按 RacerId 放到结算区锚点（`_placedClientIds` 和 `NotifyPlacementApplied` 改为按 RacerId）；在结算区里不会乱跑。
7. 再来一局时：AI 数量、难度和名字沿用，配装重新随机，没有重复生成或残留对象。

**验证**：V4、V8、V10、V11、V12。

## S3b AI 施法入口与表现

**目标**：让 AI 具备与玩家完全一致的施法能力，并在 host 画面上有完整的技能表现。

**涉及模块**：`SkillExecutor.CastSlotServer`（新增入口）、owner 表现接缝、`AISkillCaster` 骨架（先做"冷却好就放"的调试规则）。

**完成标准**
1. 服务器侧施法入口复用 `TryResolveCast`、`ResolveCastVariant`、`QueueCast`：冷却、执念值、反噬判定，以及观察者 RPC 都与玩家路径一致。
2. 没有 owner 时不发 TargetRpc，也不再有对应的 warning。AI 身上的拖尾、缩放、Feel 在 host 画面上可见。
3. 玩家对 AI、AI 对玩家、AI 对 AI，每个技能及其反噬版都生效。
4. 遮挡视野命中 AI 时，会通知 AI 降低规划精度（这里只做接口，效果在 S4 完成）。

**验证**：V5、V10、V11、V12。

## S4 AI 驾驶与调参场景

**目标**：AI 在多车环境下稳定跑完多圈，有三档可感知的难度，并保留滑稽感。

**涉及模块**：`ForwardSimPlanner`（完整版）、`AIDifficultyProfile`（三档）、Fumble、走线偏移、追赶、遮挡视野时的精度下降、`AITuningHarness`。

**完成标准**
1. 三档难度的精度旋钮和 Fumble 都在 `AIDifficultyProfile` 里可调，不写在代码里。
2. **调参场景**：
   - 可以指定 AI 数量、难度和旋钮值，让 AI 自动跑 N 圈。
   - 输出每圈用时、撞墙次数、平均偏离和每 tick 的规划耗时。
   - 运行中可以手动或按脚本对 AI 施加干扰：技能效果、推一下、临时调高 Fumble。
3. 三档难度的实测指标写入 progress.md：每圈用时、撞墙/圈、平均横向偏离、AI 之间的横向离散度、超车次数、`AI.Plan` 耗时。三档之间的指标要有可区分的差异；滑稽感的最终认可由团队观看后决定，不设圈速目标。
4. 5 个 AI 同场时，横向离散度不低于约定阈值（在调参时确定并记录）；被推、被遮挡后，都能在约定时间内回到走线。

**验证**：V6、V7、V9、V10、V11、V12。

## S5 AI 施法规则

**目标**：AI 在合适的时机施放技能，让单机对局有派对式的互相干扰。

**完成标准**
1. 每个技能有一条 `IAISkillRule`（design.md §5.4），难度影响规则的判断精度，规则逻辑有 EditMode 测试。
2. 调参场景可以单独开关 AI 施法，并统计各技能的施放次数和反噬次数。

**验证**：V5、V6、V10、V11、V12。

## S6 体验收尾（占位美术）

**目标**：Solo Match 在功能上完整可交付，美术用占位。

**完成标准**
1. 玩家冲线后出现"跳过观战"按钮，按下后按倒计时到期的规则立即结算。
2. 其他 Racer 的头顶名字、小地图对手标记，由 Match Rules 控制：单机开启，联机关闭。占位资源都在 `Assets/Placeholder/`。
3. AI 的颜色区分（占位）。
4. 结算界面："再来一局"是默认焦点；Practice 不显示名次。
5. AI 名字列表由团队填写（配置项）。没填时用占位名，不阻塞交付。

**验证**：V1、V3–V12；V2 不适用，不运行联机测试。

## S7（后续）暂停

**目标**：Solo Match 可以暂停。

**完成标准**
1. 其余比赛计时器迁移到 Match Clock（design.md §5.6 的 S7 范围）。其中跑在 owner 客户端上的复活和推人判定框，读取同步后的时钟。
2. 暂停时 Match Clock、FishNet tick 和物理都停下；继续后状态与暂停前一致。
3. 暂停菜单提供：继续 / 重来 / 回主菜单 / 设置；Esc 确认框并入暂停菜单。
4. 选择阶段和结算阶段的暂停行为有明确定义。

**验证**：覆盖新增暂停/恢复行为及受影响的单机用例，并按本页共同要求完成 V11、V12；V2 不适用。

## 验证项

验收以 [progress.md](progress.md) 的证据状态为准：代码已实现、历史已验证、运行失败、未跑或未完成必须分开记录；本页列出要求，不宣告通过。S1 仍有未闭合项，S1.5 未开始。

单机阶段独立执行 R1（编辑器编译，无新增错误/警告）、R3（非 Development 构建）；改 prefab/场景时执行 R10（序列化）。这些检查不依赖 V2。V12 协议由专属 benchmark 任务在 `benchmark.md` 中维护（尚待集成和实测），当前阶段只闭合已支持的负载，不提前要求未实现 AI。

| ID | 内容 |
|---|---|
| V1 | Steam 关闭：能进入主菜单，没有 N3 异常；联机入口提示不可用；单机完整走完一局 |
| V2 | **不适用于单机任务**：已于 2026-10-01 按用户要求移出单机阶段门槛；不执行、不得记为通过。原有联机测试资产保留给独立联机任务 |
| V3 | Practice：自动开始；选择阶段没有超时、没有地图投票；冲线立即结算；总用时和 Lap Time 正确；结算没有超时；再来一局 / 返回 / Esc 退出都能反复走通，"返回"后没有残留的 host |
| V4 | 带 1 / 3 / 5 个 AI，三档难度：完赛、名次、名字、观战、排行榜、Lap Time 都正确；全员完赛立即结算；跳过观战的结果与倒计时到期一致 |
| V5 | 技能矩阵：玩家对 AI、AI 对玩家、AI 对 AI，每个技能及反噬版都生效，在 host 画面中可见，没有 TargetRpc warning |
| V6 | 调参场景：每档难度跑 ≥5 圈的统计；施加干扰后能恢复；没有排成一列 |
| V7 | 视觉：host 画面中玩家和 AI 没有异常抖动（重点观察 TickSmoother） |
| V8 | 单机生命周期：连续再来一局 3 次、结果页返回后再开单机、各阶段 Esc 退出；Solo → Home → Solo 不残留状态，也不重复生成对象。不执行联机交替 |
| V9 | 性能：用 profiler marker `AI.Plan` 统计 5 个 AI 时每帧的规划总耗时；发布构建目标 < 1ms |
| V10 | 单机相关用例及受影响的共享纯逻辑用例通过，新增纯逻辑有对应测试；按用例范围过滤，不把联机测试纳入单机 gate。既有全套历史结果可保留为历史证据，不要求为单机重跑联机用例 |
| V11 | **单机稳定性浸泡**：用当前阶段支持的最大配置（S3a 之后为 5 个 AI、困难难度），连续进行 10 局 Solo Match，完成标准如下：<br>• 每局都有一次"再来一局"，每 3 局有一次"返回"后重开<br>• 选择、开场、比赛各阶段各做一次 Esc 退出<br>• 全程日志没有 Exception/Error<br>• 没有卡死，也没有 AI 永久卡住（Stuck Recovery 都成功）<br>• 场景里的 Buddah、网络对象数量每局回到相同的值<br>• 托管内存在第 2 局之后没有持续增长 |
| V12 | **独立单机 benchmark**：按 `benchmark.md`（尚待集成和实测）的可复现入口/配置测当前阶段支持的最大负载；S1 为 0 AI Practice。完整记录构建与采样协议、frame time mean/p95/p99、GC 分配速率、内存/对象残留及真实比赛/重开完整性。只比较可比的单机基线，实机与合成证据分开；字段缺失标未测，不从 heap 差值伪造 GC 分配率 |
