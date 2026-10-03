**2026-10-03 一致性清理（工作区，未提交，未运行新的比赛或联机测试）：**
- `IMatchRules.IsSolo` 新增，15 处以 `ReturnTarget == MainMenuHome` / `is SoloMatchRules` 代判“是否 Solo”的写法改用它；`ResultDecisionManager` 保留其真正的 ReturnTarget 判断。
- `RacerAuthority.HasLocalControl` / `IsServerAI(NetworkObject)` 成为“谁驾驶这辆车”的唯一判定；`BuddahMovement`、`BuddahPredictedMotor(.Events)`、`RaceBodyIntroStateController`、`BuddahPredictionVisualRootBridge` 不再把任意无 owner 的服务器对象当作 AI。
- `RacerId.AIBase` / `MaxAI` 与 `IsHumanValue` / `IsAIValue` / `IsValidValue` 取代散落的 10000/10004 字面量。
- `RaceFinishManager` 向 `IRaceEndPolicy.ShouldEnd` 传真实的“真人已完赛”标志（此前为“任何人已完赛”）；缺少 `MatchClockSync` / `RaceTimingSync` 时记录一次 error，不再静默永不结束。
- `RaceBodyIntroStateController.GetNormalizedDistanceT` 的开场弧长修正（来自 `ec2bac2`）在所有模式生效：联机开场车身端点与时序不变，但改为等弧长速度移动，与 dev 的速度曲线不同；需按“联机开场不变”规则明确签字确认。
- Unity batch mode EditMode 全套：342 项，282 通过，0 失败，60 跳过（[Explicit] 历史测试）。
- `AIRacerDriver`：用 `Collision.GetContact` 取代每步分配 contacts；缺 profile 时记录 error 并使用 Normal 产品 profile，不再静默选择 beam 规划器。`PlanObservation` 与 `ISteeringPlanner` 移至 `Assets/Scripts/AI/PlanObservation.cs`。
- `BuddahPredictedMotor.Events.cs` 恢复为 UTF-8（两处注释曾被存为 GBK）。
- Editor 工具 `AIDifficultyAssetTool` / `TrackWallFixtureTool` / `ThrustVectorAcceptanceBuild` 不再依赖 `.worktree/single-player-mode` 路径或个人输出目录。
- **未改的已知问题**（会改变已调校、已验收的 AI 行为，需试玩）：`AIRacerDriver` 的反应延迟会抽稀 `ThrustVectorPlanner` 调用，使其按调用计数的计时（`_collisionSeconds`、摆动计数、`ThrustReplanTicks`）在 Easy/Normal 上变慢；摆动写入 `_chosenTheta` 后可能随机游走；`ThrustVectorPlanner` 中硬编码的 79/78 m/s 与 4.82 rad/s² 假定 FinalMaxSpeed 为 80。
- **仍需联机测试的联机路径行为变化**（超出 Solo 范围）：`SessionLauncher.RequestStopSession` 现自行加载 MainMenu（`SteamLobbyManager` 约 618 行的注释仍假定由 UI 处理）；联机开局 20 s / 30 s 超时会强制返回菜单；`LeaderboardTMPUI` 联机 HUD 不再显示状态块；`SplineProgressTracker.maxProjectionDistance` 默认值 40→30。
- 文档：统一 AI 开工授权与里程碑的唯一全文在本文件，HANDOFF/phases 改为摘要加链接；阶段表、ADR 0003 修订注记、CONTEXT 难度/Fumble 词条、design.md 规划器注记及构建/证据路径已按代码与提交更正。

**2026-10-03 开场表现更新：** 用户验收 6P 自由错位/热胎路线后，已同步 1P–5P，修复 3P 共用落点引用；新增单机 idle 错相及本机玩家头顶 3D 箭头占位（仅开场显示、GO 后立即隐藏）。共享 RaceMap 曲线会用于联机；idle 目前仅单机，箭头按本地拥有者实现但未做联机实测。联机适配事项、证据和门架遮挡限制见 [本次改动与联机交接](intro-presentation-2026-10-03.md)。最终单机实播 383 个采样无捕获错误，35 个 GO 后采样标识均隐藏；33 项开场相关测试通过。非 Development 构建完成状态因 MCP 断连未确认。本次未运行联机测试、未合并。

**2026-10-03 AI 跑线算法里程碑：推力矢量控制器 + 墙走廊 + 三档难度（同 PR59，已提交 `559d57c`）。** V5 beam search 被 `ThrustVectorPlanner` 取代：闭式引导锚点 + 两段式闭环 rollout 选择 + 时间最优姿态层，pace 用摩擦圆；赛车线携带射线测得的左右墙距，rollout 把墙建模为无摩擦导轨（游戏不设刹车正是因为墙）。Hard 禁刹（夹角 ≤ 90°）同场三圈 81.5–83.8 s、Normal 92.8–94.4 s、Easy 114.7–118.9 s（历史 V5 单跑 118.1 s、A2 三圈 116.5 s、spin-fix 126.0 s）；无整圈旋转；五 AI 帧时中位 5.8–7.2 ms（beam 65 ms，Development 构建）。难度经 `MatchSpawnManager.SpawnSoloAI` → `AIDifficultyProfiles.Resolve` 接入产品路径，资产 `Assets/Resources/AI/{Easy,Normal,Hard}.asset` 由 `Tools/ai/difficulty-*.json` 生成；每车随机性（速度偏差、走线偏置、夹角摆动、次优失误、反应抖动）使同档 AI 不再同线。版本比较改用同场比赛（`AITuningHarness --ai-profiles`，`race-results.json`）。完整 EditMode 342 项 0 失败；正式构建 0 错误。**用户已验收。** 未做：车车碰撞建模、IL2CPP、技能与排名/结算（另有进行中改动）；beam 与 Legacy 等价测试保留为对照。规格见 [thrust-vector-controller-spec.md](thrust-vector-controller-spec.md)，全部真机与比赛数据见 [thrust-vector-controller-2026-10-02.md](thrust-vector-controller-2026-10-02.md)，原始分析见 [review-planning-cost-68e4d70.md](review-planning-cost-68e4d70.md)。

**2026-10-02 五 AI 性能成本优化（同 PR59，未合并）：** 逐 AI 计时定位规划为主要 CPU 成本；几何/模型常量缓存和重复工作去除保持控制参数，VJitter 改为显式默认关闭。最终 39/39 测试通过，同构建原版/首版优化中位帧时 147.88→97.52 ms；最终补测 93.18 ms、291 ticks/约9秒（原版180）。**仍有明显剩余卡顿，未宣称完整性能预算通过。** 原额外转圈约束/neutral fallback 和 tracker 修复保留；rank=-1、技能与终场留待后续短模块。详见 [性能归因、实测与边界](performance-costs-2026-10-02.md)。

**2026-10-02 tracker 专项修复已实现并完成直接回归（同 PR59，未合并）：** 保存位置调用实际 tracker 确证 20 m 阈值造成目标区间后 23 点拒绝；赛道路面验证支持有限 30 m。修复选中分支距离校验、限幅后的距离/参数一致性和拒绝帧的 delta/wrap/方向状态。最终 tracker 14/14 通过，首轮其余 83 项相关回归通过；三圈回放 0 拒绝、最大投影偏差 3.269 m，独立非 Development 构建 0 错误/24 警告。**没有新的自然比赛、六人排名或结算通过证据**，旧 rank=-1 与五 AI 性能缺陷继续开放，下一模块做技能/排名/checkpoint 权限/结算消费者迁移。详见 [复现、修复和验收边界](tracker-fix-2026-10-02.md)。下列开场/dev 集成记录保留其当时源码和验证范围。

**2026-10-02 六参赛者底座与开场模块已实现（PR59，未合并 dev）：** 玩家固定 Slot1/index0；五个服务器无 owner AI；唯一身份/注册表、六出生点、4–6 槽位占位路径、既有运镜及权威 GO 到驾驶已接入。89/89 针对性测试与两次加强后 4/4 场景测试通过，最终非 Development 私有构建 0 错误/6 警告。首次/实际 Rematch 共 4701 帧、0 捕获运行错误；六车同帧 GO，AI OwnerId=-1，真人连接始终1。**不是六人整场/排名/结算或性能验收：** 私有采样器下 GO 后中位帧耗时106–128ms，旧排行榜仍将 AI 合并到 -1；当时 tracker 停滞与历史 AI 速度/接触/模型问题未改（tracker 后续修复见顶部）。详见 [实现、证据与后续边界](six-racer-opening-2026-10-02.md)。下一短任务接 owner-keyed 消费方迁移、技能比赛与结算，tracker 专项已另行完成，性能仍需有限诊断；不再增加圈速调参版本。

# 单机模式进度

**2026-10-02 dev 集成复核（历史模块）：** 已普通合并 PR60 / `fc7ab594`，Solo 保留单一物理历史和即时 GO；新源码 79/79 针对性测试、独立非 Development 构建及首局/实际 Rematch 开场复核通过。仅受控结算用于重开，不代表合并后自然三圈、完整结算动画或 A3 已通过。进度 tracker 停滞会影响排名/checkpoint/逆行判断，仍未修复；后续六车模块必须保留这一限制。详见 [集成证据与下一模块](dev-integration-2026-10-02.md)。下文旧结果保留原始源码与范围。

**2026-10-02 入弯转圈修复：** 已验证三圈无额外朝向绕圈，但圈速 126.783 / 123.533 / 127.817 秒（平均 126.044）慢于 V5；行驶侧接触 66 次。此修复不代表提速或 A3，旧调参预算仍结束。详见 [转圈缺陷证据与限制](ai-spin-fix.md)。


**当前 A2 交付（2026-10-02）**：A2 已自然完成同一场三圈：116.650 / 115.667 / 117.283 秒，平均 116.533 秒；终场停驶、ResultInteractive 和五盒恢复已核对。V1–V5 调参预算已用完，固定 V5；90 秒目标与稳定干净圈仍未达到。A3/A4 未开始。 详见 [A2 实现与证据](a2-results.md)。以下 A1/Practice 检查点保留历史来源边界。

最新技能本地验收见 [技能验收范围](skill-acceptance.md)；原 VFX 导入恢复见 [VFX 恢复记录](../vfx-asset-recovery.md)，此前 handoff 见 [Solo 呈现时间契约](solo-presentation-timeline.md)，选择页/Development 见 [历史复核](validation-2026-10-01.md)。旧检查点按各自源码保留。

状态：`todo` / `doing` / `paused` / `blocked` / `done` / `done-unverified`。流程见 [HANDOFF.md](HANDOFF.md)。

## 当前状态与验收边界（2026-10-02 A1 交付后）

**Practice 试玩交付方式（2026-10-01 用户更新）**：功能实现完成且必要编译/基础检查通过后，告知用户可玩版本、启动方式和 [快速试玩清单](practice-playtest.md)。strict14、完整 benchmark/V12 及程序化长测不再作为先交付试玩包的条件；未跑、中断、失败各按事实保留，不因此标通过。已知会阻止正常游玩的功能问题仍须说明和处理。Practice → 用户技能试玩 → 明确 AI 开始信号的门槛及 A1–A4 不变。

S1 仍为 `doing`，普通 Windows x64 非 Development Practice 试玩包已交付，游戏源码为 `21cc3cfd9246856f071b32f5730e0586ff547ea6`，含权威过线计时修复；后续文档提交不改变该构建的游戏代码来源。相关 Unity 回归为 **151 passed / 0 failed / 0 skipped**，普通 Release 构建成功且无构建错误，实际启动到可见主菜单已核对；启动方式见 [快速试玩清单](practice-playtest.md)。用户已初步手动试玩，反馈看起来没大问题、技能都能用；不等于逐项普通/反噬、对手效果、完整生命周期或整体 S1 验收通过。新源码 V3 ±100ms 自然精度采样按用户要求中止，尚无完整圈/场结果；最终源码 strict14/程序化长测、Esc 专项及完整 benchmark/V12 均未完成验证。技能检查点 `9e428c1` 的本地行为和 14/14 回归继续按 [原范围](skill-acceptance.md) 保留；A1 已实现并有自然一圈证据，A2 已有三圈证据（见顶部）。

**AI 开工授权（2026-10-01 23:51 UTC）**：用户明确说“ok 这样吧。你直接开始做这个吧 然后继续在pr59内实现即可。可以开始启动了你”。现已授权在现有 `feat/single-player-mode` / PR #59 从 A1 开始；A1 已由 `354f10a` 实现并取得自然一圈证据，当时下一步为 A2（无技能三圈与急弯调优）；本次三圈及调参结果见顶部；A2 当前结果见顶部，A3–A4 尚未完成，详见 [progress.md](progress.md) 的“A1 交付证据与 A2 交接”。此明确指令覆盖此前等待开始信号及仅做 AI 文档的暂停状态；Practice 初步试玩、151 项相关回归和可玩包交付不因此升级为全验收，±100ms、最终源码长测/完整 benchmark 等未验证项继续保留。A1 通过才实现 A2，随后 A3、A4；不新开 AI 分支或阶段 PR。

用户最新确定的 AI 实施与测试顺序为：**A1 单个 AI 无技能完整一圈 → A2 单个 AI 无技能三圈比赛及平均圈速/圈间稳定性 → A3 五个独立 AI 带技能比赛，玩家入场后不操作 → A4 通知用户手动试玩完整流程**。前一步通过才推进；这同时约束实现和测试，覆盖旧方案中不同的推进顺序。技能可使用独立的简单概率决策，不要求复杂行为树；仍须遵守既有技能合法性与玩法。

用户技能试玩交接与后续 AI 方案见 [ai-testing.md](ai-testing.md)。已收到初步 Practice/技能反馈，但逐项覆盖与完整验收尚未全部确认，23:51 已另行收到明确 AI 开始指令，现授权从 A1 开始；不得将这一授权或初步反馈自动勾成所有 Practice 验收通过。A1 实现与证据已在 `354f10a` 交付；此前文档任务仅核对事实；本轮 A2 已实际运行 Unity，证据见顶部。

当前文档维护与后续获授权的 AI A1–A4 工作统一沿用现有 `feat/single-player-mode`、single-player-mode worktree 和面向 `dev` 的 draft PR #59；不另开 AI 分支或阶段 PR，不以先 merge 为下一阶段的前提。此用户决定覆盖旧“每阶段新 PR”的通则。文档任务只核对与记录事实；生产代码、工具和实际运行须按各自授权范围执行。

| 范围 | 可确证状态 | 仍缺的验收证据 |
|---|---|---|
| S1 实现 | Yak 离线启动、自动选择、Practice 计时/结算、退出与失败恢复已有代码和测试用例 | 架构差异见下表；已实现不等于整个 S1 通过 |
| V1 / V3 | 旧 `56650e3` / 游戏源码 `9e428c1` 已自然完赛 4 场、共 12 圈；旧首圈计时误差 +183.33 ms 超出 ±100 ms 要求。`21cc3cf` 已集成修复 | 旧 V3 失败保留；新源码自然精度采样已中止，±100ms 尚未验证，用户检查三圈流程和显示不替代毫秒级对照 |
| V7 / 本地 handoff | `cb4a9ad` 两组非 Development 首局/真实 Rematch 均各 5380 帧、0 错误；边界最低有符号速度约 57.97 m/s，无停留或倒退；最终渲染/相机姿态一致，五次控制/传送历史重置通过 | 只覆盖有界 handoff 与受控控制事件；未替代自然完整比赛、自然复活或六技能试玩 |
| V8 | 旧 `56650e3` 自然比赛间实际 Rematch 3 次；更早三阶段 Esc 与活跃特效退出清理按各自源码保留 | 旧长测没有返回 Home；当前修复源码的结果页 Return、重进及完整清理仍待确认，交用户按快速清单反馈 |
| V10 / R1 / R3 / R10 | `21cc3cf` 相关 Unity 回归 151 passed / 0 failed / 0 skipped；普通 Release 构建成功、无构建错误，启动到可见主菜单已核对并交付试玩；另有私有观察包 0 errors / 18 warnings | 构建/启动检查不等于自然计时、长测或完整 benchmark 通过；技能 14/14 与 handoff 65/65 继续按各自历史源码和范围保留 |
| V11 | 旧 `56650e3` strict14 在 4 场自然完赛后按协调要求结束，只有部分证据；更早的首场中断另作历史保留 | **完整 strict14 未完成、未通过**；不再作为先交试玩包的条件，不将未运行的新源码长测记为通过 |
| V12 | `9cca08e` 的独立 Development 三窗口各 3600 帧、共 10800 帧，原生 GC 零缺失、0 运行错误，窗口 `VALID`；measured actual 与 Home 配置分开，不外推为新 handoff 源码性能 | **完整 V12/基线未通过**：预算全 null，clean-source 与最终源码 strict14/Esc 等证据未齐；Development frame/GC 成组使用，不当 Release 结果，不拼接旧被拒采样 |
| VFX / 用户技能试玩准备度 | `9e428c1` 六技能本地触发/到期、正常掌形、神足通原粒子与清理已有分项证据，Animator 扫描警告已修复 | 用户初步反馈看起来没大问题、技能都能用；普通/反噬、部分画面和完整生命周期未逐项确认，0 AI 对手效果仍未验证。普通试玩包已交付，不等于完整 V11/V12 通过 |
| 来源资格 / 文件恢复 | 三文件曾按用户授权备份恢复成功；随后 Unity 自动删除两项孤立 meta 并重写设置，证据及 dirty 状态保留 | 不循环 restore、不清无关 dirty；clean-source 未通过；主根 dev 未改动 |
| V2 / Steam 双客户端 / Solo–Online 交替 | **N/A，不执行，不标通过** | 无单机验收前置 |

文档 worker 仅只读核对证据。`aa7fb05` 的导入恢复/fixture、`9e428c1` 的虚拟键盘实际技能输入、此前 handoff 的 Player 采样分开引用，不合并为完整验收。首次导入失败的历史原因仍未知；其他 checkout 的 Library 不会仅因拉取提交而自动修复。0 AI 未覆盖任何对手命中或受控效果，不因此启动 AI。

选择页 `26dd6df` 的替代 URP Lit/摘要修复已实际复核；随后发现 720p 固定像素 Canvas 导致标题/Confirm 越界，`affbd6f` 只调整三个 CanvasScaler 字段。保存后重进 720p/1080p 检查通过；皮肤页使用同样字段的运行时预览，比赛标题隐藏与退出清理已核对。原缺失美术未还原，不将动作回调测试写成鼠标命中或技能效果通过。

以下局部记录是历史证据；其中“尚未采集/下一 session”等描述只反映当时状态，最新结论以上表与新验证报告为准。

### 历史：S1 handoff 局部复核 — 2026-10-01（游戏修复 ec2bac2）

- 实际 Unity EditMode 相关回归 **40 passed / 0 failed / 0 skipped**：新增 IntroHandoffContinuity 12、HandoffClock 20、RaceStartHandshake 5、SoloSessionFlow 2、SoloTransport 1。MCP 的首个异步测试状态滞留不计作结果；以独立 TestRunner 回调落盘结果为准。
- 新 Windows x64 非 Development 私有观察构建基于 `6e9f851`（含 `ec2bac2`），**0 errors / 15 compiler warnings**。首次与真实 Rematch 按钮后的第二次开场分别记录 2689/2690 帧，从选择/开场前即固定 targetFPS 60、vSync 0；不是解锁后才限帧。捕获错误为 0，最终 Home 的 client/server 均停止且 Clock/Timing 清空，Player 正常退出。
- 两次 GO 前后 [-0.5, +1] 秒内未观察到视觉位置沿当前车头方向倒退；GO 帧视觉与终点快照误差分别为 0 / 0.000000477 m。该邻域最大 frame delta 为 16.980 / 16.757 ms，最大视觉单帧位移 1.1951 / 1.1956 m，最大镜头位移 1.9442 / 1.9080 m；镜头旋转未变，FOV 最大单帧变化 0.7635 / 0.7539 度。这些是观察量，不是自定性能或手感阈值。
- 每轮仍有两次 GO 后 body 移动而视觉位置单帧停留：首次分别为 frame 2442（body 仅竖向约 0.167 m）、2445（body 位移约 0.985 m）；重开为 5505/5508。因此只确认本次未见倒退，不宣告整体流畅性通过。两次采样均未捕获 pending 帧：GO 在同帧已被 motor 消费，新增 pending 姿态保持分支仍只有测试证据。每个新 racer 的 handoff eventId 为 1，不将跨对象相同 ID 推断为重复执行。
- movement unlock 在 GO 后约 17.8 / 15.9 ms，转向抑制在约 145.6 / 149.1 ms 后解除；后续普通 Input System A/D 均实际产生正负转向。为到达 Rematch，第一轮使用一次明确标记的受控终点登记，**不是自然完赛、V3 精度或 V11 长测**。未运行 AI 或任何联机测试。
- `06a440f` 集成独立 benchmark 修复：测量 actual 与退出后设置分开、检查新鲜 GC 样本、缺失保持 null，新增单独 Development 构建协议。集成后 51 Python 与 8 隔离 C# 测试通过；实际新 Development 采样未执行，下一短 session 重建后完成三个 30 秒预热/60 秒测量窗口，不与旧 Release 帧数据拼接。预算仍待定。
- 选择页只读定位：`PropertySelection.unity` 的 `地图/绘马墙` 与 `地图/桌子` MeshRenderer 材质引用为 null；标题对象为 `Canvas/SharedUIRoot/Header/PageTitleText` 与 `StageTitleText`。交给独立 UI 任务处理，本次未改场景。原始帧、截图、构建与来源哈希均只在私有证据中保存。
- 受保护设置及两项删除 meta 未恢复，dirty 来源资格仍未闭合；主根用户 WIP 不变。S1 仍 doing，strict14/V3/V12 与用户试玩准备度未通过。

### 历史：S1 真实验收 — 2026-10-01（游戏源码 27685f9）

- 已核验 instrumented non-Development Player 的全部构建文件哈希与交接清单一致，三个私有辅助源码与已提交模板一致；普通发行构建、此前三项场景测试仍按各自历史记录引用。本轮 Python 分析器 42/42 通过。
- **三阶段 Esc 新证据通过**：selection、intro-before-GO、driving-after-GO 均真实打开确认框、屏蔽输入、通过 ConfirmQuit 回 Home 并重开。每次 client/server 停止，Buddah/Reporter 为 0，Clock/Timing 清空，捕获错误为 0；独立回执分析返回 VALID。这不替代结果页 Rematch/Return 或 strict14。
- **V12 未通过**：三个真实物理驾驶窗口各预热至少 30 秒、测量约 60 秒，各 3600 连续帧，窗口配置均为 1280×720、quality 2、vSync 0、targetFPS 60，报告丢帧为 0，捕获错误为 0。原始 frame mean/p95/p99（ms）：16.6692/16.8968/17.0080、16.6679/16.8968/17.0021、16.6687/16.9109/17.0262；仅为诊断分布，不是已合格基线。
- **旧 Release 采样缺陷（后续独立 Development 补测见最新验证报告，不回填旧数据）**：三个窗口共 10800 帧的 GC 分配列全部缺失，Release 的 `GC Allocated In Frame` 不可用/空；Main Thread 样本可用。`Finish` 在回 Home 后记录全局 actual，此时 host 恢复 targetFPS 500，导致分析器报 `Window settings changed`。须区分测量配置与退出后配置，并确定独立 GC 采样方案；禁止把 heap 差值或缺失值填成分配量。预算仍未确定，不编造通过。
- 构建来源仍含保留的设置/孤立 meta 差异；没有恢复受保护 WIP，也没有把 dirty 来源改写成 clean。该状态不能取得工具要求的干净源码资格。
- **handoff 流畅度仍未验证**：本次窗口开始前的 intro/GO/unlock 按 host targetFPS 500 运行；60 FPS 在 movement-unlocked 之后才施加，不能据此宣称正常帧率下开场到驾驶平顺。此后 `ec2bac2` 已集成修复：等待 motor 消费 launch handoff 时保持 GO 姿态，并修正路径距离的重复转换；当时没有修复后运行结果；其后独立构建与局部运行结果见上节。唯一 Unity 验收任务负责新构建及真实姿态/镜头/输入连续性验证；最终 strict14 与新 V3 触发精度长测仍未完成。
- 当前 Editor 实际技能选择提供六个 unlocked ID：acceleration、slowtrap、blackcurtain、giant、push_projectile_hands、reverseturn。此项证明选择入口可用，不代表每项技能效果已验收。反转转向正常效果排除施法者，0 AI Practice 无法验证其对手效果；不新增 AI 靶子。选择场景截图同时出现洋红材质和文字叠放，列为待修复复核的独立资源/UI 项，未推定其来源。当前不能标为 user-ready；六个选项可用不等于完整界面或六项技能表现通过。
- 原始日志、配置、CSV、回执、来源清单和实际界面截图均私有保存；截图已在工作对话展示，不上传公共 PR。S1 保持 doing；AI 仍等待 Practice 完成、用户逐项试玩及明确开始信号。

### 当前 Practice 质量边界

Practice 的目标是让本地单机完整承接既有联机流程与表现，特别是加载/场景 handoff、开场镜头/动画/倒计时到驾驶的姿态、镜头和输入控制权交接，并真实验证流畅。此要求是本地适配与验收，不是运行联机测试；也不授权全局移除 prediction。用户此前提出的本地 handoff 问题已由 `cb4a9ad` 在已测首局/Rematch 与控制事件范围内修复验证，见 [Solo 呈现时间契约](solo-presentation-timeline.md)；此结论不替代完整 Practice、自然生命周期或技能/VFX 验收。65 项 Unity 回归、两组各 5380 帧的首局/真实 Rematch 及五次历史重置已有独立证据，不再将旧单帧停留列为当前阻塞。原 VFX 导入及六技能本地行为已有分项证据；自然复活、对手效果/部分完整画面/手感、strict14/V3 与完整 V12 仍待各自验证；运行由当前唯一 Unity 任务负责。

### 设计与实现待核对

| 差异 | 证据与影响 | 主实现任务的处理点 |
|---|---|---|
| 菜单依赖边界已修复 | `ad55ff0` 将具体 UI 调用改为 `SoloUIBootstrap` 注册的 `Action<SoloMatchSettings>` 回调 | 此项不再列为待修复；不据此宣告全部 S1 验收通过 |
| 会话创建职责存在设计内部冲突 | design.md §3.2 明定 `GameNetworkManager` 持有 `SessionLauncher`，代码也如此；§3.1 却要求现有代码仅依赖契约 | 明确组合入口是否有显式例外，或调整构造位置；在决定前不宣告严格依赖检查通过 |
| Solo UI 目录偏离设计 | 设计指定 `Assets/Scripts/UI/Solo/`，实际为 `Assets/Scripts/Match/UI/`，命名空间仍为 `SteamMultiplayer.UI` | 确认保留目录还是迁移及其序列化影响；不为对齐文档擅自移动代码 |

已修正文档中的过时前置：Steam 容错、单人自动开赛和 S1 Match Clock 已实现；S6 不再要求重跑 V2。S1.5 的 V11 引用与全阶段稳定性要求对齐，单车性能证据不冒充 5 AI 指标。0 AI 锁定是 S1 已允许的限制，不是 AI 完整功能已交付。

## 阶段

| 阶段 | 目标 | 状态 | PR | 验证结果 | 备注 |
|---|---|---|---|---|---|
| S0 | 同步重构结果 | done | — | 文档事实已按合并后的 dev 复核 | 2026-09-30 合并 dev（含 #47–#58） |
| S1 | 离线单人 Practice 跑通 | doing | [#59 (draft)](https://github.com/OldMustClimbStudio/BuddahGo/pull/59) | 游戏源码 21cc3cf 普通试玩包已交付，相关回归 151/151、构建及菜单启动已核对；旧 V3 失败保留，新源码 ±100ms、长测及完整 V12 未验证；V2 不适用 | 用户初步试玩反馈技能可用，逐项覆盖尚未全部确认；S1 不标 done；AI A1、A2 已完成（见 S1.5） |
| S1.5 | 规划器可行性验证 | done-unverified | #59 | 354f10a A1 自然一圈；A2 同场自然三圈平均 116.533 s（V5 beam）；随后 `559d57c` 推力矢量控制器 Normal 同场三圈 92.8–94.4 s | A1/A2 完成；V11/V12 未针对本阶段执行；beam 已由 ThrustVectorPlanner 取代 |
| S2 | Racer 身份 | doing（部分） | #59 | `ba30112`：RacerIdentity/RacerRegistry/IRacerDirectory、六个唯一 RacerId，89/89 针对性测试 | 排行榜/进度/完赛/结算改按 RacerId 仍为工作区未提交改动，未验收；仅单机验收 |
| S3a | AI 完整跑完一局 | doing（部分） | #59 | `ba30112` 同轮生成五个无 owner AI、六出生点、4–6 人布局、权威 GO；`559d57c` 产品路径五 AI 六车同场三圈自然完赛（无技能） | 排名/结算区停放、Stuck Recovery、Rematch 及六人结算未验收 |
| S3b | AI 施法入口与表现 | todo | | | |
| S4 | AI 驾驶与调参场景 | doing（部分） | #59 | `559d57c` 三档难度（`Assets/Resources/AI/*.asset`）经同场比赛实测，用户已验收；`AITuningHarness --ai-profiles` | 横向离散度阈值、被推/遮挡后回线、技能干扰及车车碰撞建模未做 |
| S5 | AI 施法规则 | todo | | | |
| S6 | 体验收尾（占位美术） | todo | | | |
| S7 | 暂停（后续） | todo | | | |

## AI 实施与测试里程碑（A1、A2 已交付）

| 步骤 | 完成内容 | 状态 | 推进条件 |
|---|---|---|---|
| A1 | 单个 AI 无技能完整自然跑一圈，记录圈速与轨迹 | done（仅一圈检查点） | 354f10a / run04：124.917 秒、1250 样本；急弯接触和调校缺口保留，不等于整场/全验收 |
| A2 | 单个 AI 无技能三圈比赛，检查平均圈速与圈间稳定性 | done | 同一场自然三圈 116.650 / 115.667 / 117.283 秒，平均 116.533 秒（V5，预算用完），90 秒目标未达到；`559d57c` 推力矢量 Normal 同场三圈 92.8–94.4 s，见 [a2-results.md](a2-results.md) 与 [thrust-vector-controller-2026-10-02.md](thrust-vector-controller-2026-10-02.md) |
| A3 | 五个独立 AI 带技能比赛，玩家入场后不操作；记录各自完赛率/时间/轨迹 | 未开始（五 AI 无技能同场比赛已有 `559d57c` 证据；AI 施法仅文档设计） | A2 通过；正常完成困难则调整复测，成功率与 DNF 原因用于诊断调优，不设机械通过百分比 |
| A4 | 通知用户手动试玩完整流程并记录反馈 | 未开始 | A3 通过；不得把通知或自动化通过等同用户已试玩 |

普通无障碍、无技能每圈约 **90–120 秒**是调校目标，不是每圈硬性通过区间，也不外推至 A3 技能混战。速度、跟随、反应等独立参数字段已在 A1 实现；快捷难度/精准度 UI 与 Easy/Hard 映射仍未实现或校准。测试盒子只临时 disable、记录并恢复，不删除。A2 起步和证据边界见 [HANDOFF](HANDOFF.md) 的“历史交接：A1 已实现，随后进入 A2”。

## A1 交付证据与 A2 交接（2026-10-02）

实现提交 **`354f10a381af280acdded2bbb6c76b43c24b62f4`**，沿用 PR #59。共享力规则的平面预测模型、样条路线、有限 beam-search 规划器、默认禁用的 prefab `AIRacerDriver`、转向 override、Development 采样器和轨迹渲染工具已实现。验证的是 host-owned 唯一车身接管，未生成 ownerless AI，也未实现 A2–A4。普通 Practice 的默认驾驶输入保留。运行与字段说明见 [Tools/ai/README.txt](../../Tools/ai/README.txt)。

本节依据 A1 任务的原始回执、事件、诊断及源码清单只读复核；私有原始文件未上传。证据标识保留如下，不能把不同运行混成一次通过：

| 证据标识 | 实际结果 | 边界 |
|---|---|---|
| `run04-player/summary.json`、`trajectory.jsonl`、`events.jsonl` | 自然一圈 **124.916667 秒**，1250 个真实样本，25 次侧碰撞进入事件，0 runtime errors、0 检测到的位置跳变；6 个 checkpoint/圈切换记录，2 次短暂逆行，无 stall 事件 | `productMatchFinished=false`；侧接触进入次数不等于去重碰撞次数；有急弯擦墙，不是无碰撞圈或 90–120 秒调校完成 |
| `run04-player/diagnostics.json` | 样本连续、Match Clock 单调，1249 个相邻采样间隔均为 6 tick；**96/96** 个无侧碰撞 60-tick 窗口达标，另列 28 个侧碰撞窗口 | 预声明容限：位置 0.5m、速度 0.5m/s、朝向 3°；最大误差分别 0.325675m、0.045229m/s、0.008728°，不外推墙碰撞/坡面/技能 |
| `final/tests-summary.json`、`tests-cases.jsonl` | **33 passed / 0 failed / 0 skipped**：11 项 AIDriving 与 22 项计时/时钟/Solo 流程检查 | 针对性范围，包含真实 PhysX 30/60Hz、实际地面摩擦、路线 wrap 和默认禁用 prefab；不是全套回归/长测 |
| `handoff/build-result.json` | 最终 Windows Development 构建 **Succeeded、0 errors / 12 warnings**，13.607 秒，2026-10-02 00:59:31 UTC 完成 | 不是普通 Release benchmark；保留既有 WIP/helper 的构建不具备 clean-source 资格 |

**来源差异必须保留**：run04 的 12 项源码清单与最终交付对照，只有 `AITuningHarness.cs` 增加可选 JSON profile 入口；默认 planner/model/motor/profile 字节相同。最终二进制已构建，但未重复跑完整一圈；新增 profile CLI 只经编译检查，未独立运行验证。源码/二进制 SHA256 清单分别保存在 `final/`、`handoff/`，不把最终构建说成另一次自然圈。

**默认与失败实验**：当前实验 Normal 为目标速度 65、预测 3 秒、控制块 0.15 秒、beam 24、每 3 tick 重规划；这些是规划参数，不改变刚体/油门规则。`run05-player` 仅将预测延长到 5 秒，在首段急弯附近反复转圈/侧碰，240 驾驶秒超时：0 圈、2401 样本、96 侧接触进入、6 stall、84 wrong-way、1 次检测跳变、0 runtime errors；已恢复 3 秒默认。该跳变没有相应 respawn 证据，不能推断成复活。无侧碰模型窗口仅 135/167 达标；长预测/高负载一致性未合格。碰撞忽略与规划负载可能相关，但独立因果未证实。采样的最近一次规划耗时均值由 run04 的 6.50ms 增至 run05 的 11.30ms，不是正式 profiler benchmark，也未达到未来五 AI 亚毫秒目标。

较早 run01 有 Editor 暂停/恢复，仅探索；run02 在 GO 前 domain reload，零驾驶样本；run03 为摩擦修正前版本，128.817 秒/19 接触，保留诊断用途。run04 的轨迹图来自真实 XZ 样本；隐藏 Player 的黑截图不作视觉证明。

**恢复与保护**：run04/run05 均记录五个 `RaceMap/DebugBox` 测试对象恢复 `activeSelf=true`，未删除物体，未改 RaceMap 场景。A1 交接记录用户 Player 未被注入输入或终止，28 项原 WIP 全部保留；本轮文档核查不操作任何 Player、Unity 或测试。

**下一步 A2**：在同分支/同 PR 中，以 3 秒默认和 run04 作为有界参照，分析急弯碰撞、转向时机及规划成本，扩展同一无技能车身至连续三圈自然比赛。保存逐圈/平均用时、波动、叠加轨迹、事件与失败原因；不以三个独立一圈代替。当前不实现 A3。最新目标和五版预算以下节为准，成功率/波动作诊断；A2、A3、A4、快捷预设、五 AI 性能、±100ms、完整 strict14/长测和 Release benchmark 均未通过。

## 当前 A2 调优预算（2026-10-02 用户最新指令）

优先尽可能精准地驾驶，直接朝 **90 秒/圈**优化；此前 90–120 秒是历史调校范围，本轮以 90 秒为明确优化目标。A1 的 **124.917 秒**是既有参照，不计入新预算。从本次指令起最多 **5 个实质候选版本 V1–V5**，每版记录配置、相对改动、实际圈速、真实轨迹与失败/碰撞等诊断；构建重试或同配置复测须归入该候选，不得改名重置五版预算。

若五版仍未达到目标，停止继续追调，保留最佳可靠且已验证的配置为常规难度，报告实际圈速、与 90 秒的差距及剩余问题；不能只凭最快单圈选择一个不可靠配置。A2 最终仍须同一单车无技能连续三圈自然比赛，记录逐圈/平均用时及波动，不能用三个独立一圈替代。五版预算限制继续提出调优候选，不取消最终三圈验证；若验证失败，如实报告，不能标 A2 通过或偷偷增加第六版。A3 不在当前任务范围，即使 A2 通过也只交接结果。

保持共享物理、自动全油门、数字转向、合法 checkpoint 与权威计时；不靠改运动规则、传送、手工 Finish 或隐藏失败达标。成功率/圈速波动继续作为可靠性诊断，不另造统计硬门槛。下一任务在 progress.md 逐版登记，当前尚无 V1–V5 候选结果。

## AI 调参记录（S1.5 / S4 / S5）

只在取得用户 AI 开始信号后填写实际结果；完整字段与逐圈轨迹方案见 [ai-testing.md](ai-testing.md)。每圈圈速及失败/中断记录保存在对应运行证据中，不只填平均值。

| 运行标识/阶段 | 配置与 seed | 计划/完成圈数、完成率 | 逐圈圈速与轨迹证据 | 碰撞/复位/卡住 | 技能条件及效果/恢复 | 规划耗时 | 结论与未测项 |
|---|---|---|---|---|---|---|---|

## 决策

| 日期 | 事项 | 结论 | 决定人 |
|---|---|---|---|
| 2026-09-30 | 设计会话 Q1–Q34 | 见 design.md §2 | 团队 |
| 2026-09-30 | 补充决策 Q35–Q40：提前结算、Lap Time、Stuck Recovery、RacerId 规则、AI 配装、S1.5 与 S3 拆分 | 按推荐方案执行，见 design.md §2 | 团队 |
| 2026-09-30 | 历史原则：联机只做冒烟检查 | 已被 2026-10-01 用户的“单机不跑联机测试”决定覆盖 | 团队 |
| 2026-10-01 | 单机不需要任何联机测试；单机应有自己的 benchmark | V2/Steam 双端/联机交替移出单机 gate；新增独立 V12 协议 | 用户 |
| 2026-10-01 | 历史关机暂停检查点 | 当时 S1 paused；保存部分 Player 证据，不把未完成长测记为通过；当前已恢复 | 用户 |
| 2026-10-01 | 恢复单机工作，并由独立任务维护文档与 benchmark | S1 doing；只更新可核实证据，沿用 #59；不提前开始 S1.5 | 用户 |
| 2026-10-01 | AI 必须等待 Practice 完成、用户逐项技能试玩及明确开始信号 | 覆盖此前直接推进 S1.5 的指令；信号前 AI 只落文档 | 用户 |
| 2026-10-01 | Practice 完整适配既有流程，重点保证 handoff 流畅 | 开场镜头/动画/倒计时→驾驶及加载/结算/重开/返回须本地真实验证；不运行联机测试、不授权全局移除 prediction | 用户 |
| 2026-10-01 | AI 实施与测试按 A1–A4 逐步通过 | 单 AI 无技能一圈 → 单 AI 无技能三圈稳定性 → 五独立 AI 带技能比赛（玩家不操作，可用简单概率施法）→ 通知用户手动试玩；覆盖旧的不同推进顺序 | 用户 |
| 2026-09-30 | 默认决策：AI 只需配装，皮肤用占位颜色；观战规则沿用并改为按 RacerId；头顶名字和小地图对手由 Match Rules 控制，单机开启 | 可以调整，见 design.md §2 | 设计默认 |
| 2026-10-01 | AI 后续沿用同一 PR 与诊断调校口径 | 继续 feat/single-player-mode / PR #59，不新 AI 分支、不按阶段新开 PR；普通无障碍无技能 90–120 秒为调校目标，成功率/圈速波动用于诊断；23:51 授权时尚未实现；现 A1 已交付，见 2026-10-02 记录 | 用户 |
| 2026-10-01 | AI 名字列表 | 漂移禅师、轮回圈王、面壁达摩、金刚不刹、回头是岸（见 design.md §5.8） | 团队 |

### 最新授权原文（2026-10-01 23:51 UTC）

> ok 这样吧。你直接开始做这个吧 然后继续在pr59内实现即可。可以开始启动了你

该指令覆盖此前等待 AI 开始信号的暂停状态。该授权下的 A1 已在 354f10a 交付；后续从 A2 接手，继续同分支、同 PR #59，不把仍未验证的 Practice 项标通过。

## 新发现

| 日期 | 位置 | 描述 | 关联阶段 |
|---|---|---|---|

## 历史证据：S1 implementation checkpoint — 2026-10-01

- Implemented: Match contracts/rules, Yak-only local host, bounded startup and deferred stop, Practice setup (0 AI), automatic room start, skill/skin selection without map voting or timers, tick-based timing/Lap Times, immediate Practice results, Rematch/Return, and an Escape confirmation dialog which blocks local steering.
- Serialized scene changes: Multipass (Steam + Yak) in MainMenu; preplaced MatchClockSync/RaceTimingSync on the RaceMap network object; Solo UI roots in all three scenes. Chinese UI uses a bundled OFL-licensed subset and a static atlas baked from its 499 supported codepoints. The final quit/results screenshots were inspected after fixing stale glyph mapping. Diagnostic telemetry is log-only and the serialized debug-finish button is disabled. Online ordinary standings remain visible; Practice hides ranking through the existing EndOnHumanFinish rule. The left-side legacy names are static placeholders, not a replacement standings view.
- Verified in Unity 2022.3.55f1c1 with the Steam API unavailable (no Steam.exe process observed during checks; no user Steam process was stopped): Yak starts both managers without starting Steam transport, then stops both. The real scene integration test passes setup → selection → Escape/confirm → Home → reopen → selection → intro/GO → Escape/cancel → controlled finish → results → Rematch → selection → stop. Input is synthesized through the real Input System Dynamic update; the test temporarily redirects unfocused batch input and restores its settings.
- The independent startup-recovery test also passed: a controlled failure after loading selection defers shutdown, automatically restores the visible Solo setup panel with the failed difficulty and error, an enabled Start button, and no surviving player/host. Normal Return/Escape still return Home. This injects the rollback boundary; it does not simulate a real authentication timeout.
- Regression assertions passed in that integration run: a new timing round clears synchronized old Lap Times; an unowned racer cannot consume the first finish order; final leaderboard registration precedes immediate Practice ending. Controlled timing/finish data is synthetic and does **not** certify physical racing, lap detection, or V11 endurance.
- Final V10 complete-suite result: **152 passed, 0 failed, 0 skipped** (visible-Editor EditMode suite after startup-recovery and Online HUD review fixes, including EnterPlayMode scene tests and three standings regressions; the preceding baseline batch run passed 149/149). R1 editor compilation and serialized network binding checks passed; R3 Windows x64 non-Development build **succeeded, 0 errors, 18 BuildReport warnings**. The latest BuildReport includes 14 compiler warnings in existing code (obsolete PredictionSmoother API, unused fields, and unreachable code) plus the same four shader/third-party resource warnings; the earlier cached build reported only those four asset warnings. The new timing component preserves FishNet's editor Reset callback.
- The final standalone player also started and remained running after reporting Steam initialization unavailable; this is a startup smoke only, not completed-game evidence.
- Current scope: V2 and all online testing are **not applicable** to this Solo task by the user decision on 2026-10-01. Remaining Solo gates include full V3 results/button follow-through, V8 consecutive Rematches/reopen, V11 complete endurance/object/memory evidence and V12 independent Solo benchmark. Historical test results remain evidence, not an online prerequisite.
- Earlier batch input failures were isolated to the unfocused Editor input buffer and editor-iteration-based waits; the corrected focused integration run passed. Raw logs/screenshots and temporary verification helpers are private and excluded from commits/PRs.

## 历史证据：S1 acceptance continuation — 2026-10-01

- Review fixes: `73f29b3` restores Online standings without diagnostic fields and carries a one-time failed-Solo request across deferred shutdown/menu initialization. The scene test verifies the setup is automatically visible with its original difficulty and error.
- Independently exercised Escape via virtual keyboard events on the normal Dynamic input update in selection, intro before GO, and racing after GO. All three confirmation exits reached visible Home with both network managers stopped, zero PlayerProgressReporter objects, timing services cleared, and the input block released. No Error/Exception/Assert was captured during this operator run.
- Superseded requirement: the previously recorded Steam two-endpoint prerequisite is removed from this Solo task. No Steam accounts or online session are required to resume or complete Solo stages; V2 is N/A, not passed.
- Physical offline race: normal virtual A/D input drove the unmodified body through the real start trigger, sequential checkpoints and three complete laps. Natural finish total **734.000 s**, Lap Times **242.567 / 243.067 / 248.367 s**. The result phase became interactive approximately 3.75 s after finish, with no 15-second Practice end delay. All 734 one-second racing samples contained 10 NetworkObjects; no Error/Exception/Assert was captured. The operator then verified complete shutdown/Home. This was a visible-Editor run with Steam unavailable, not a standalone endurance result.
- Physical timing observation: the second lap transition interval observed by EditorApplication.update was 243.217 s versus the synchronized 243.067 s (0.150 s difference); the third interval was 248.283 s versus 248.367 s (0.083 s difference). Timing is recorded through the existing 100 ms progress-report RPC, and Editor observation adds frame scheduling delay; these measurements must not be described as single-tick finish-line precision.
- The first physical operator stopped at ResultInteractive before the results view activated its buttons. Consequently its intended 35-second results hold and actual Return button check did not execute and remain open; no product failure was inferred from this operator sequencing issue. Editor GC samples include Editor/Console overhead and do not close V11's Player memory-growth gate.
- Warning provenance was checked against dev `7389ec8`: all six source files producing the 14 compiler warnings have identical Git blob IDs in dev and the reviewed feature head. The unreachable branch is the existing constant-disabled ranking log. FishNet APIs and those warning-producing fields were not changed by S1. The remaining four asset warnings match the earlier report; no new product-code warning was identified. The separate instrumented acceptance build is not the ordinary R3 deliverable.

## 历史暂停检查点 — 2026-10-01 user request

以下记录描述关机前保存状态；当前已恢复工作，以本文顶部状态为准。

- At this saved checkpoint, S1 was **paused**, not done. Published implementation/review checkpoints are `73f29b3` and `88f763a`; the complete suite previously passed 152/152, including real Unity scene tests. The ordinary Windows x64 non-Development release succeeded with 0 errors and 18 warnings, whose existing-source provenance is recorded above.
- Completed physical evidence: one visible-Editor three-lap Practice race, total 734.000 s, natural checkpoints/finish and immediate Practice result transition; three-phase Escape/confirm/Home cleanup; automatic failed-start setup restoration with original difficulty/error.
- The independent instrumented Player strict14 sequence was stopped at the user's pause request during its first main race (lap 3, approximately 73.57% complete at normal closure). It had not completed a main sample, so neither strict14 nor the ten-match V11 gate passed. Partial live measurements showed one Buddah, one reporter, ten NetworkObjects and about 14 MB managed heap, with no captured game Error/Exception before stopping. These are partial observations, not a memory-stability conclusion.
- Private helpers, source hashes, process manifest, logs, partial JSONL and normal/instrumented builds are saved locally. They are excluded from this PR. The temporary Unity helper sources were removed from Assets at this checkpoint. Correction (2026-10-03): private helpers exist again as local, untracked files (`Assets/Scripts/Match/SoloContinuousAcceptanceRuntime.cs`, `SoloBenchmarkFrames.cs`, `SoloCrossingProbe.cs`, `SoloHandoffObservation.cs`, `PrivateRaceFlow.cs`, compiled only under `BUDDAH_PRIVATE_*` defines outside the Editor, plus Editor-only `Assets/Editor/Solo*.cs`, `PrivateRaceFlowBuild.cs`, `ThrustVectorPrivateBuild.cs`, `SinglePlayerLocalBootstrap.cs`); they are not part of the PR. The committed operator source is `Tools/SinglePlayerBenchmark/*.cs.txt`; `SoloContinuousAcceptanceRuntime.cs.txt` was synced on 2026-10-03 to include the `precision3` plan.
- The saved strict14 evidence lacks all-frame performance percentiles, allocation/frame and allocation/second, a fixed-seed run protocol and captured quality/FPS configuration. It is functional/endurance evidence only. The independent benchmark session stopped cleanly at baseline `88f763a` without creating source, protocol, or a new commit. At that checkpoint, `Tools/SinglePlayerBenchmark/` and `benchmark.md` were **not implemented or executed**; do not borrow online Editor R8 numbers or repeat a long run solely to fill metadata.
- Remaining acceptance order after resumption: read this checkpoint and implement the bounded independent benchmark package/protocol; confirm all local/remote checkpoints; reopen the feature project; finish actual results hold/Return/Rematch and required multi-match integrity; close the bounded S1 0-AI benchmark protocol/measurement; update applicable Solo gates. Do not begin S1.5 while S1's applicable Solo gates remain incomplete. The later user decision adds two further requirements: the user completes the skill playtest and explicitly signals AI implementation may begin; S1 completion alone is insufficient. No online tests or Steam coordination are part of this resume plan.
