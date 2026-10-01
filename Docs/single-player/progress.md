# 单机模式进度

状态：`todo` / `doing` / `paused` / `blocked` / `done` / `done-unverified`。流程见 [HANDOFF.md](HANDOFF.md)。

## 当前状态与验收边界（2026-10-01 恢复后）

S1 为 `doing`，S1.5 未开始且等待用户后续明确 AI 开始信号。本次文档基于实现分支 `ec2bac2`；AI 计划已通过 `17eb1c9` 集成，`24c022d` 记录的运行证据对应游戏源码 `27685f9`。后续 handoff 修复不能沿用这些结果宣告通过；下表区分已集成、已验证与待验证。

**AI 启动前置（2026-10-01 用户最新决定）**：先完成 S1 单人 Practice，再由用户逐项试玩技能，最后等待用户明确发出开始 AI 的信号。三个条件缺一不可；S1 完成、自动测试通过、文档批准或经过一段时间都不等于开始信号。包括 S1.5 在内的任何 AI 实现、接管调试、采集/可视化工具实现与 AI 运行测试均不得提前开始；信号前只做 AI 文档规划。此前“完成后直接推进 S1.5”的指令已被覆盖。

用户最新确定的 AI 实施与测试顺序为：**A1 单个 AI 无技能完整一圈 → A2 单个 AI 无技能三圈比赛及平均圈速/圈间稳定性 → A3 五个独立 AI 带技能比赛，玩家入场后不操作 → A4 通知用户手动试玩完整流程**。前一步通过才推进；这同时约束实现和测试，覆盖旧方案中不同的推进顺序。技能可使用独立的简单概率决策，不要求复杂行为树；仍须遵守既有技能合法性与玩法。

用户技能试玩交接与后续 AI 方案见 [ai-testing.md](ai-testing.md)。当前没有记录证明 Practice 已完成、用户已完成逐项技能试玩或已发出 AI 开始信号，三项均不可自动勾选。

主任务负责生产代码与实际测试；独立 benchmark 任务负责工具和 `benchmark.md`；文档任务只核对与记录事实，提交由主任务集成进 #59。

| 范围 | 可确证状态 | 仍缺的验收证据 |
|---|---|---|
| S1 实现 | Yak 离线启动、自动选择、Practice 计时/结算、退出与失败恢复已有代码和测试用例 | 架构差异见下表；已实现不等于整个 S1 通过 |
| V1 / V3 | 历史可见 Editor 三圈自然完赛 734.000 s；合成场景测试覆盖结果与一次 Rematch；三阶段 Esc 退出已有历史记录 | 完整结果页停留/实际 Return 按钮和后续流程仍未闭合；Player 启动冒烟不等于完整比赛 |
| V7 / 本地 handoff | `ec2bac2` 已集成 GO 姿态保持与路径距离求值修正及相关测试源；尚无本次可核实的修复后测试结果 | 新源码构建、正常帧率下姿态/镜头/输入交接与重开衔接的真实流畅性证据；此前 60 FPS 窗口开始于解锁后 |
| V8 | `27685f9` 真实 selection/intro/driving 三阶段 Esc、清理及重开回执 VALID；client/server 停止，Buddah/Reporter 为 0，Clock/Timing 清空，捕获错误为 0 | 结果页连续 3 次 Rematch、Return 后重开及完整生命周期仍未闭合；不能把 Esc 重开等同结果按钮验收 |
| V10 / R1 / R3 / R10 | 历史全套 152/152、Editor 编译/序列化检查通过；非 Development 构建成功，0 errors / 18 warnings | 今天文档任务未重新执行这些检查；全套历史结果不引入联机 gate，后续代码变化需对应验证 |
| V11 | 历史 strict14 在首场主样本完成前中断；本轮最终 strict14 尚未开始，**未完成、未通过** | 最终源码的完整生命周期、对象计数和 Player 内存趋势；短帧窗口或历史 heap 局部观测不能证明稳定 |
| V12 | 分析器 42/42；三个真实窗口各 3600 帧、实测约 59.99 FPS，无报告丢帧/捕获错误；**仅诊断，未通过** | 10800 帧 GC 全缺失；Home 摘要 500 FPS 与窗口 60 FPS 冲突导致拒绝；来源资格及预算未闭合，不能冒充合格基线 |
| 用户技能试玩准备度 | 六技能选择入口可用；仅验证可选，未验证每项施放/效果 | 选择页洋红材质/文字叠放待修复复核，完整 Practice 验收未闭合；**尚不可标 user-ready** |
| V2 / Steam 双客户端 / Solo–Online 交替 | **N/A，不执行，不标通过** | 无单机验收前置 |

本次只读核对执行任务的检查点、结构化证据汇总及 Git 集成记录，更新可确证事实；未重新运行分析器或 Unity/Player，不把正在修复的采样器或 handoff 写成验证通过。已知早期测试输入失败随后修复；strict14 属于人为中断，结果页操作器提前结束属于验收未执行，均不可据此推断新的产品失败。

### S1 本轮真实验收 — 2026-10-01（游戏源码 27685f9）

- 已核验 instrumented non-Development Player 的全部构建文件哈希与交接清单一致，三个私有辅助源码与已提交模板一致；普通发行构建、此前三项场景测试仍按各自历史记录引用。本轮 Python 分析器 42/42 通过。
- **三阶段 Esc 新证据通过**：selection、intro-before-GO、driving-after-GO 均真实打开确认框、屏蔽输入、通过 ConfirmQuit 回 Home 并重开。每次 client/server 停止，Buddah/Reporter 为 0，Clock/Timing 清空，捕获错误为 0；独立回执分析返回 VALID。这不替代结果页 Rematch/Return 或 strict14。
- **V12 未通过**：三个真实物理驾驶窗口各预热至少 30 秒、测量约 60 秒，各 3600 连续帧，窗口配置均为 1280×720、quality 2、vSync 0、targetFPS 60，报告丢帧为 0，捕获错误为 0。原始 frame mean/p95/p99（ms）：16.6692/16.8968/17.0080、16.6679/16.8968/17.0021、16.6687/16.9109/17.0262；仅为诊断分布，不是已合格基线。
- **采样器待修复/补测**：三个窗口共 10800 帧的 GC 分配列全部缺失，Release 的 `GC Allocated In Frame` 不可用/空；Main Thread 样本可用。`Finish` 在回 Home 后记录全局 actual，此时 host 恢复 targetFPS 500，导致分析器报 `Window settings changed`。须区分测量配置与退出后配置，并确定独立 GC 采样方案；禁止把 heap 差值或缺失值填成分配量。预算仍未确定，不编造通过。
- 构建来源仍含保留的设置/孤立 meta 差异；没有恢复受保护 WIP，也没有把 dirty 来源改写成 clean。该状态不能取得工具要求的干净源码资格。
- **handoff 流畅度仍未验证**：本次窗口开始前的 intro/GO/unlock 按 host targetFPS 500 运行；60 FPS 在 movement-unlocked 之后才施加，不能据此宣称正常帧率下开场到驾驶平顺。此后 `ec2bac2` 已集成修复：等待 motor 消费 launch handoff 时保持 GO 姿态，并修正路径距离的重复转换；相关测试源已加入，但本次没有修复后运行结果。唯一 Unity 验收任务负责新构建及真实姿态/镜头/输入连续性验证；最终 strict14 与新 V3 触发精度长测仍未完成。
- 当前 Editor 实际技能选择提供六个 unlocked ID：acceleration、slowtrap、blackcurtain、giant、push_projectile_hands、reverseturn。此项证明选择入口可用，不代表每项技能效果已验收。反转转向正常效果排除施法者，0 AI Practice 无法验证其对手效果；不新增 AI 靶子。选择场景截图同时出现洋红材质和文字叠放，列为待修复复核的独立资源/UI 项，未推定其来源。当前不能标为 user-ready；六个选项可用不等于完整界面或六项技能表现通过。
- 原始日志、配置、CSV、回执、来源清单和实际界面截图均私有保存；截图已在工作对话展示，不上传公共 PR。S1 保持 doing；AI 仍等待 Practice 完成、用户逐项试玩及明确开始信号。

### 当前 Practice 质量要求（待验证）

Practice 的目标是让本地单机完整承接既有联机流程与表现，特别是加载/场景 handoff、开场镜头/动画/倒计时到驾驶的姿态、镜头和输入控制权交接，并真实验证流畅。此要求是本地适配与验收，不是运行联机测试；也不授权全局移除 prediction。用户报告当前 handoff 表现一般，原因及修复效果需代码与运行证据，不能静态宣告通过。当前未收到覆盖这些完整动态交接的新通过证据；静态修复和已有局部流程结果不自动关闭此项。S1 主任务负责唯一 Unity 验收执行，handoff 模块修复结果按实际构建和运行记录集成。

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
| S1 | 离线单人 Practice 跑通 | doing | [#59 (draft)](https://github.com/OldMustClimbStudio/BuddahGo/pull/59) | 历史 V10 152/152、R3 构建通过；新三阶段 Esc VALID、分析器 42/42；V3/V7/V8/V11/V12 未全部闭合；V2 不适用 | 源码 ec2bac2；运行证据 27685f9；尚非 user-ready；AI 未开始 |
| S1.5 | 规划器可行性验证 | todo | | 未开始；等待用户 AI 开始信号 | Practice 完成、用户逐项技能试玩、明确开始信号三项齐备才启动 |
| S2 | Racer 身份 | todo | | | 仅单机验收 |
| S3a | AI 完整跑完一局 | todo | | | |
| S3b | AI 施法入口与表现 | todo | | | |
| S4 | AI 驾驶与调参场景 | todo | | | |
| S5 | AI 施法规则 | todo | | | |
| S6 | 体验收尾（占位美术） | todo | | | |
| S7 | 暂停（后续） | todo | | | |

## AI 实施与测试里程碑（尚未开始）

| 步骤 | 完成内容 | 状态 | 推进条件 |
|---|---|---|---|
| A1 | 单个 AI 无技能完整自然跑一圈，记录圈速与轨迹 | 未开始 | 三项启动前置满足后才能实现；本步通过再 A2 |
| A2 | 单个 AI 无技能三圈比赛，检查平均圈速与圈间稳定性 | 未开始 | A1 通过；本步通过再 A3，数值稳定性门槛待定 |
| A3 | 五个独立 AI 带技能比赛，玩家入场后不操作；记录各自完赛率/时间/轨迹 | 未开始 | A2 通过；正常完成困难则调整复测，成功率门槛待定 |
| A4 | 通知用户手动试玩完整流程并记录反馈 | 未开始 | A3 通过；不得把通知或自动化通过等同用户已试玩 |

## AI 调参记录（S1.5 / S4 / S5，尚未开始）

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
| 2026-10-01 | AI 名字列表 | 漂移禅师、轮回圈王、面壁达摩、金刚不刹、回头是岸（见 design.md §5.8） | 团队 |

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
- Private helpers, source hashes, process manifest, logs, partial JSONL and normal/instrumented builds are saved locally. They are excluded from this PR. The temporary Unity helper sources were removed from Assets.
- The saved strict14 evidence lacks all-frame performance percentiles, allocation/frame and allocation/second, a fixed-seed run protocol and captured quality/FPS configuration. It is functional/endurance evidence only. The independent benchmark session stopped cleanly at baseline `88f763a` without creating source, protocol, or a new commit. At that checkpoint, `Tools/SinglePlayerBenchmark/` and `benchmark.md` were **not implemented or executed**; do not borrow online Editor R8 numbers or repeat a long run solely to fill metadata.
- Remaining acceptance order after resumption: read this checkpoint and implement the bounded independent benchmark package/protocol; confirm all local/remote checkpoints; reopen the feature project; finish actual results hold/Return/Rematch and required multi-match integrity; close the bounded S1 0-AI benchmark protocol/measurement; update applicable Solo gates. Do not begin S1.5 while S1's applicable Solo gates remain incomplete. The later user decision adds two further requirements: the user completes the skill playtest and explicitly signals AI implementation may begin; S1 completion alone is insufficient. No online tests or Steam coordination are part of this resume plan.
