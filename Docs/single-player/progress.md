# 单机模式进度

状态：`todo` / `doing` / `blocked` / `done` / `done-unverified`。流程见 [HANDOFF.md](HANDOFF.md)。

## 阶段

| 阶段 | 目标 | 状态 | PR | 验证结果 | 备注 |
|---|---|---|---|---|---|
| S0 | 同步重构结果 | done | — | 文档事实已按合并后的 dev 复核 | 2026-09-30 合并 dev（含 #47–#58） |
| S1 | 离线单人 Practice 跑通 | doing | [#59 (draft)](https://github.com/OldMustClimbStudio/BuddahGo/pull/59) | Unity 场景闭环通过；V10 149/149；R3 非 Development 构建通过；V1/V2/V3/V8/V11 尚未全部验收 | 2026-10-01：基于 0abc432，dev 7389ec8 为祖先；0 AI；不进入 S1.5 |
| S1.5 | 规划器可行性验证 | todo | | | 不通过则停下来，由团队决策 |
| S2 | Racer 身份 | todo | | | 联机做冒烟检查 |
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
| 2026-09-30 | 原则：单机稳定性优先；联机只做冒烟检查，不做网络专项 | 写入 design.md §0、phases.md V2/V11 | 团队 |
| 2026-09-30 | 默认决策：AI 只需配装，皮肤用占位颜色；观战规则沿用并改为按 RacerId；头顶名字和小地图对手由 Match Rules 控制，单机开启 | 可以调整，见 design.md §2 | 设计默认 |
| 2026-10-01 | AI 名字列表 | 漂移禅师、轮回圈王、面壁达摩、金刚不刹、回头是岸（见 design.md §5.8） | 团队 |

## 新发现

| 日期 | 位置 | 描述 | 关联阶段 |
|---|---|---|---|

## S1 implementation checkpoint — 2026-10-01

- Implemented: Match contracts/rules, Yak-only local host, bounded startup and deferred stop, Practice setup (0 AI), automatic room start, skill/skin selection without map voting or timers, tick-based timing/Lap Times, immediate Practice results, Rematch/Return, and an Escape confirmation dialog which blocks local steering.
- Serialized scene changes: Multipass (Steam + Yak) in MainMenu; preplaced MatchClockSync/RaceTimingSync on the RaceMap network object; Solo UI roots in all three scenes. Chinese UI uses a bundled OFL-licensed subset and a static atlas baked from its 499 supported codepoints. The final quit/results screenshots were inspected after fixing stale glyph mapping. Legacy diagnostic output is log-only and the serialized debug-finish button is disabled.
- Verified in Unity 2022.3.55f1c1 with the Steam API unavailable (no Steam.exe process observed during checks; no user Steam process was stopped): Yak starts both managers without starting Steam transport, then stops both. The real scene integration test passes setup → selection → Escape/confirm → Home → reopen → selection → intro/GO → Escape/cancel → controlled finish → results → Rematch → selection → stop. Input is synthesized through the real Input System Dynamic update; the test temporarily redirects unfocused batch input and restores its settings.
- The independent startup-recovery test also passed: a controlled failure after loading selection defers shutdown, returns to Home, displays the error, and allows setup to reopen. This injects the rollback boundary; it does not simulate a real authentication timeout.
- Regression assertions passed in that integration run: a new timing round clears synchronized old Lap Times; an unowned racer cannot consume the first finish order; final leaderboard registration precedes immediate Practice ending. Controlled timing/finish data is synthetic and does **not** certify physical racing, lap detection, or V11 endurance.
- Final V10 complete-suite result: **149 passed, 0 failed, 0 skipped** (final visible-Editor EditMode suite, including EnterPlayMode scene tests; the preceding batch run also passed 149/149). R1 editor compilation and serialized network binding checks passed; R3 Windows x64 non-Development build **succeeded, 0 errors, 4 BuildReport warnings**. BuildReport warnings concern existing shaders and third-party post-processing resources; existing obsolete-API compiler warnings also remain. The new timing component preserves FishNet's editor Reset callback.
- The final standalone player also started and remained running after reporting Steam initialization unavailable; this is a startup smoke only, not completed-game evidence.
- Acceptance still open: supervised Steam-closed end-to-end gameplay (remaining V1 acceptance); physical lap/time accuracy and completed-race flow (V3); three Rematches plus Solo/Online alternation (V8); actual Steam host/client complete-match smoke (V2); the prescribed 10-match endurance run, phase-by-phase Escape exits, object baseline and managed-memory observations (V11). These require actual gameplay/multiplayer evidence; no S1.5/AI implementation starts before the S1 gate.
- Earlier batch input failures were isolated to the unfocused Editor input buffer and editor-iteration-based waits; the corrected focused integration run passed. Raw logs/screenshots and temporary verification helpers are private and excluded from commits/PRs.
