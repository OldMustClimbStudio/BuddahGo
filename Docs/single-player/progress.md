# 单机模式进度

状态：`todo` / `doing` / `paused` / `blocked` / `done` / `done-unverified`。流程见 [HANDOFF.md](HANDOFF.md)。

## 当前状态与验收边界（2026-10-01 恢复后）

用户已恢复工作；S1 为 `doing`，S1.5 未开始。文档审计基于实现分支 `9f2839e`（文档提交；历史实现/验证见 `73f29b3`、`88f763a`），不把正在发生的未提交工作视作已验证交付。主任务负责生产代码与实际测试；独立 benchmark 任务负责工具和 `benchmark.md`；文档任务只核对与记录事实，提交由主任务集成进 #59。

| 范围 | 可确证状态 | 仍缺的验收证据 |
|---|---|---|
| S1 实现 | Yak 离线启动、自动选择、Practice 计时/结算、退出与失败恢复已有代码和测试用例 | 架构差异见下表；已实现不等于整个 S1 通过 |
| V1 / V3 | 历史可见 Editor 三圈自然完赛 734.000 s；合成场景测试覆盖结果与一次 Rematch；三阶段 Esc 退出已有历史记录 | 完整结果页停留/实际 Return 按钮和后续流程仍未闭合；Player 启动冒烟不等于完整比赛 |
| V8 | 历史合成重开与退出恢复有局部证据 | 连续 3 次 Rematch、结果页返回后重开及完整对象清理链 |
| V10 / R1 / R3 / R10 | 历史全套 152/152、Editor 编译/序列化检查通过；非 Development 构建成功，0 errors / 18 warnings | 今天文档任务未重新执行这些检查；全套历史结果不引入联机 gate，后续代码变化需对应验证 |
| V11 | strict14 在首场主样本完成前被用户中断；**未完成，未通过** | 完整 10 局生命周期、对象计数和 Player 内存趋势；约 14 MB heap 局部观测不能证明稳定 |
| V12 | 独立 benchmark 正在专属任务中开发，尚无已集成协议或正式结果 | 可复现入口/配置、完整性能采样与真实比赛/重开证据；旧多人 R8 和中断 strict14 均不能充当基准 |
| V2 / Steam 双客户端 / Solo–Online 交替 | **N/A，不执行，不标通过** | 无单机验收前置 |

本次审计只读代码、测试源和已提交的历史记录，未重跑 Unity/Player，也未重新核验私有原始日志。已知早期测试输入失败随后修复；strict14 属于人为中断，结果页操作器提前结束属于验收未执行，均不可据此推断新的产品失败。

### 设计与实现待核对

| 差异 | 证据与影响 | 主实现任务的处理点 |
|---|---|---|
| 现有菜单直接引用 Solo 具体 UI | `MainMenuUI.TryRestoreFailedSoloSetup` 调用 `GetComponent<SoloSetupPanel>().RestoreSettings`，与 design.md §3.1 和 S1 完成标准 8 的契约边界不一致 | 通过契约或上层注册回调恢复设置；保留启动失败自动恢复行为并验证。文档不自行放宽边界 |
| 会话创建职责存在设计内部冲突 | design.md §3.2 明定 `GameNetworkManager` 持有 `SessionLauncher`，代码也如此；§3.1 却要求现有代码仅依赖契约 | 明确组合入口是否有显式例外，或调整构造位置；在决定前不宣告严格依赖检查通过 |
| Solo UI 目录偏离设计 | 设计指定 `Assets/Scripts/UI/Solo/`，实际为 `Assets/Scripts/Match/UI/`，命名空间仍为 `SteamMultiplayer.UI` | 确认保留目录还是迁移及其序列化影响；不为对齐文档擅自移动代码 |

已修正文档中的过时前置：Steam 容错、单人自动开赛和 S1 Match Clock 已实现；S6 不再要求重跑 V2。S1.5 的 V11 引用与全阶段稳定性要求对齐，单车性能证据不冒充 5 AI 指标。0 AI 锁定是 S1 已允许的限制，不是 AI 完整功能已交付。

## 阶段

| 阶段 | 目标 | 状态 | PR | 验证结果 | 备注 |
|---|---|---|---|---|---|
| S0 | 同步重构结果 | done | — | 文档事实已按合并后的 dev 复核 | 2026-09-30 合并 dev（含 #47–#58） |
| S1 | 离线单人 Practice 跑通 | doing | [#59 (draft)](https://github.com/OldMustClimbStudio/BuddahGo/pull/59) | Unity 场景闭环通过；V10 152/152；R3 非 Development 构建通过；V1/V3/V8/V11/V12 尚未全部验收；V2 不适用 | 2026-10-01 已恢复；审计 HEAD 9f2839e；0 AI；未开始 S1.5 |
| S1.5 | 规划器可行性验证 | todo | | | 不通过则停下来，由团队决策 |
| S2 | Racer 身份 | todo | | | 仅单机验收 |
| S3a | AI 完整跑完一局 | todo | | | |
| S3b | AI 施法入口与表现 | todo | | | |
| S4 | AI 驾驶与调参场景 | todo | | | |
| S5 | AI 施法规则 | todo | | | |
| S6 | 体验收尾（占位美术） | todo | | | |
| S7 | 暂停（后续） | todo | | | |

## AI 调参记录（S1.5 / S4 / S5）

| 日期 | 阶段 | 难度 | 旋钮值 | 圈数 | 平均圈速 | 撞墙/圈 | 平均偏离 | 规划耗时 | 结论 |
|---|---|---|---|---|---|---|---|---|---|

## 决策

| 日期 | 事项 | 结论 | 决定人 |
|---|---|---|---|
| 2026-09-30 | 设计会话 Q1–Q34 | 见 design.md §2 | 团队 |
| 2026-09-30 | 补充决策 Q35–Q40：提前结算、Lap Time、Stuck Recovery、RacerId 规则、AI 配装、S1.5 与 S3 拆分 | 按推荐方案执行，见 design.md §2 | 团队 |
| 2026-09-30 | 历史原则：联机只做冒烟检查 | 已被 2026-10-01 用户的“单机不跑联机测试”决定覆盖 | 团队 |
| 2026-10-01 | 单机不需要任何联机测试；单机应有自己的 benchmark | V2/Steam 双端/联机交替移出单机 gate；新增独立 V12 协议 | 用户 |
| 2026-10-01 | 历史关机暂停检查点 | 当时 S1 paused；保存部分 Player 证据，不把未完成长测记为通过；当前已恢复 | 用户 |
| 2026-10-01 | 恢复单机工作，并由独立任务维护文档与 benchmark | S1 doing；只更新可核实证据，沿用 #59；不提前开始 S1.5 | 用户 |
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
- Remaining acceptance order after resumption: read this checkpoint and implement the bounded independent benchmark package/protocol; confirm all local/remote checkpoints; reopen the feature project; finish actual results hold/Return/Rematch and required multi-match integrity; close the bounded S1 0-AI benchmark protocol/measurement; update applicable Solo gates. Do not begin S1.5 while S1's applicable Solo gates remain incomplete. No online tests or Steam coordination are part of this resume plan.
