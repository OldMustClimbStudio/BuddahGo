# 进度：联机预测与开场交接修复

## 收尾（2026-10-05）

- **结论**：用户用 Development Build `4f3954b`（`.worktree/_builds/online-prediction-handoff-4f3954b-dev`，已含 PR #59 单机模式）做了双端联机实测，评价「联机修复的还不错，至少现在完全可玩」。本分支到此收尾，以 draft PR #61 交给用户。
- **实际生效的修复**：
  1. §1 Physics Mode → TimeManager（3a8b997、28414ce）：消除 reconcile 回跳，症状 A（速度相关重影）与 B 的「被拉回起点」都由它解决。
  2. §H 残余（26742dd）：handoff 待消费期间 spline 继续驱动刚体与视觉，消费时落地到碰撞体静止高度（R7.5）。
  3. 经 `dev` 合入的 PR #60（fc7ab59）：本地交接切换的相机速度源、GO 后外推、消费 tick 门控、删除 post-intro 视觉锁。
- **未执行**：EXP-2 到 EXP-5（§2 到 §5）。原因：双端实测已完全可玩，不再需要为 R3–R6 与视觉参考系追加改动。D1–D4 因此无需决定。
- **测试口径的限制**：这次双端实测是定性的。没有采集 `[ReconcileDelta]` 数值，0/100 ms 延迟矩阵也没有按 acceptance.md 逐项跑；没有记录实测时的延迟条件。acceptance.md 的数值阈值因此未验证。
- **已知残余**（静态分析，未在双端实测中被用户指出）：
  - R4：服务器消费 handoff 快照时不投影，owner 按延迟投影；消费后第一份权威快照与 owner 相差约 速度 × 单程延迟（100 ms、60 u/s 时约 3 u），表现为开场一次轻微校正。
  - PR #60 的 GO 后外推与本分支的待消费外推合计上限 0.5 s；RTT 更高时开场会重新停住。
  - 开场交接仍有用户已接受的「一点点停顿」。
- **探针保留**：`[ReconcileDelta]`、`[SpectatorStep]`（`dumpReconcile` 门控）、`[HandoffFrame]`（`logHandoffFrames`，默认关）和两个汇总脚本都保留在代码里，开关默认关闭。
- **如果问题复现**：从下方「历史继续点」接上，先用编辑器作为一端打开 `dumpReconcile` 采一局，再按 experiments.md 从 EXP-2 继续。

## 历史继续点（2026-10-01 至 10-04，问题复现时从这里接上）

- 分支 `fix/online-prediction-handoff`，worktree `.worktree/online-prediction-handoff`，HEAD 含 EXP-0 探针、§1 修复与文档提交（见下表）。
- 2026-10-04：`dev` e11292e（PR #59 单机模式全部内容，含四轮清理）已合入本分支（8a52a62）。冲突两处：`RaceBodyIntroStateController.cs`（PR #59 的 Solo 物理时钟分支与本分支的 handoff 待消费 spline 驱动在同一函数）和 `Docs/README.md`（表格行，两边都保留）。解法：`TrySampleVisualPoseAtRenderTime` 先走 Solo 的 `_soloPhysicsClock` 早退，再走本分支的 `introDriving || IsSplineDrivingAfterGo()`；外推上限 `GetOvershootCapSeconds()` 保留，Solo 下仍为 0；`IsSplineDrivingAfterGo` 增加 `_soloPhysicsClock` 守卫并改用 PR #59 的 `HasMovementAuthority`（owner 或服务器 AI）。注意：Solo 的 `ConsumeSoloLaunchBeforePhysics` 也经 `ConsumePendingLaunchHandoffEvent`，所以消费时落地（R7.5）对单机同样生效。验证：batchmode EditMode（`-assemblyNames BuddahGo.Tests`）299/299 通过（排除两个会 EnterPlayMode 的类），这两个类单独跑 3/3 通过，合计 302/302；注意带 -nographics 时 EnterPlayMode 会让批处理编辑器直接退出且不写结果，需去掉 -nographics 单独跑。Unity MCP 服务本次未连上（ECONNREFUSED），用批处理模式代替。
- 2026-10-02：`dev` fc7ab59（PR #60，`fix/launch-handoff-continuity`，本地交接卡顿修复）已合入本分支（bd05136，仅 `Docs/README.md` 表格行冲突，两行都保留）；根 checkout 已快进到 fc7ab59。合并后 Editor 编译无错误，EditMode 92/92 通过。
- 合入 PR #60 对本分支实验的影响（EXP-2/3 开始前复核）：
  1. owner 的 handoff 请求现在在 GO 当刻按「当前时间」采样快照（含 GO 后沿末端切线外推），且 owner 身体在消费前持续外推，上限 `maxGoOvershootSeconds`=0.15 s。纯 client 的 GO→消费死区在 RTT ≤ 150 ms 时已被本地外推掩盖；RTT 更高时身体重新 park，R7.2 的停顿会回来。
  2. R4 不对称未变：服务器按自己的 StartTick 消费、不投影；owner 消费时按 staleTicks 投影。消费后首个权威快照与 owner 仍相差 速度 × 单程延迟（100 ms、60 u/s 时约 3 u）。这是 EXP-2/EXP-3 现在应观察到的主要 B 残余。
  3. motor 的 blocked 判断改为 `!data.MovementAllowed && !_computedStats.IsRoomBypassActive`（消费 tick 不再清零继承速度）。`_computedStats` 在 reconcile 中由 ModifierState 重算，回放安全；server 与 owner 的 bypass 起点各自来自本端消费 tick，仍有 ≤ 单程延迟的差异，由 R4 一并处理。
  4. `introVisualLagTicks`=1 必须与 `Buddah.prefab` NetworkObject `_ownerInterpolation`=1 一致（已核对）。若 EXP-5 调整插值参数，要同步改它。
  5. 开场动画：GO 之前的 spline 驱动未变，但 VisualRoot 全程改为落后 1 tick 渲染（60 u/s 下约 1 u，相机随 VisualRoot，相当于整体延后 16 ms）。是否肉眼可察由用户目视确认。R7.5（消费后 rb.y 3.30→3.92 上浮）在 PR #60 中仍未处理。
- 2026-10-02：用户决定 handoff 的残余也在本分支修（单端可验证，便于与联机修复一起验收）。合并 PR #60 后用户单端仍见「开场一点小停顿」，静态分析指向两处，均已在本分支修复（提交见修复表「§H 残余」行）：
  1. GO→消费窗口：`CompleteGoTransition` 后 `FixedUpdate` 与 `TrySampleVisualPoseAtRenderTime` 都因 `_goApplied` 退出，物理根停在最后一次 spline 写入处，VisualRoot 被 `ApplyVisualRootStabilization` 吸到物理根（抵消 1 tick 滞后），消费后平滑器再把滞后长回来，表现为视觉停一拍。修复：本地 owner 在 handoff 待消费期间（`BuddahMovement.IsAuthoritativeLaunchHandoffPending`）继续沿 spline 末端切线驱动刚体与渲染采样，外推上限 `maxGoOvershootSeconds + maxHandoffPendingOvershootSeconds`（0.15 + 0.35 s，Inspector 可调），同时覆盖纯 client 的 RTT 死区。
  2. R7.5 落地去穿透：消费 tick 身体被放在 spline 高度（低于碰撞体静止高度约 0.6 u），PhysX 以 `m_DefaultMaxDepenetrationVelocity: 10` 推出，首个 tick 无水平位移。修复：消费时按当前姿态测量碰撞体最低点到根的间距，向下射线找地面（忽略自身层与触发器），在 `handoffGroundSnapMaxDistance`（1.5 u）内把根高度直接设到静止高度，owner 与 server 同一路径（`BuddahHandoffGroundSnap.TryResolveRestY` 纯函数，EditMode 测试）。
  验证：Editor 编译无错误，EditMode 98/98。用户单端目视（2026-10-02）：开场仍有一点点停顿，但不影响体验，用户决定到此为止，不再追。探针数据未采。
  若将来要继续追：先开 `logHandoffFrames` 跑 `summarize-handoff-frames.py` 确认剩余是零位移帧还是相机，再看 R7.4（Inherit/Blend 期间角速度清零）与 1 tick 视觉滞后在消费帧的对齐。
工作区干净；`Mono.Cecil.sln.meta` / `LiteNetLib.csproj.meta` 的删除是 Unity 清理被忽略文件的孤儿 meta，与本任务无关，不要提交。
- 已完成：EXP-0（探针）、EXP-1 / §1（Physics Mode → TimeManager）。用户单端实测：症状 A 消失，症状 B 不再强行拉回起点，但交接切换瞬间仍有一次小卡顿。双端实测与 0/100 ms 延迟矩阵**未做**。
- 先决条件：EXP-2 是否需要做，取决于一次**双端、100 ms** 的开场测试（纯 client 作 owner）。R3 只在有延迟的纯 client 上触发，host 单端看不到。若 client 开场不再被向后拉、位置单调向前，则 EXP-2 跳过，B 记为达标；切换瞬间的小卡顿若不可接受再做 EXP-3（R4/R5）。若 client 仍被向后拉，则做 EXP-2。
- 交接瞬间的小卡顿（R7）已拆到独立分支 `fix/launch-handoff-continuity`（`.worktree/launch-handoff-continuity`，`Docs/handoff-continuity/`），本分支只做联机问题；开场 spline 动画播放效果不变仍是本分支的验收不变项。
- 若需要：按 experiments.md 做 **EXP-2**（§2.0 携带服务器消费 id + §2.A 确认门控，D1 已倾向方案 A，待用户确认），针对交接切换瞬间的小卡顿；之后 EXP-3（D2 倾向 D2-a）。实现前先在 100 ms 下看 `[HandoffDebug] consuming queued handoff` 之后 1 到 3 tick 内的 `[ReconcileDelta]`，确认预检序列。
- 环境恢复步骤：
  1. 用 2022.3.55f1c1 打开本 worktree（`Logs/online-repair/Editor.log`），在编辑器 "MCP for Unity" 标签页点 Connect，再 `set_active_instance online-prediction-handoff@41b28e58f7c667d2`。
  2. 双端测试用 ParrelSync 克隆 `.worktree/online-prediction-handoff_clone_0`（Clones Manager 打开，或 Hub 添加该目录）。
  3. 采样前把 `Buddah.prefab` → BuddahPredictionBootstrap → Debug Settings 的 `enableVerboseLogs`、`dumpReconcile` 置 1（已回退，不提交）；日志用 `Tools/Validation/OnlineRepair/summarize-reconcile-deltas.py` 汇总。
  4. 延迟模拟：`MainMenu.unity` NetworkManager → TransportManager → `_latencySimulator`，两端都设。

状态取值：`todo` / `doing` / `blocked` / `done` / `done-unverified`。实验与修复的定义分别见 [experiments.md](experiments.md) 与 [fix-spec.md](fix-spec.md)。

## 实验表

| 实验 | 针对 | 预检结果 | 状态 | 提交 | A 结果 | B 结果 | 决定 |
|---|---|---|---|---|---|---|---|
| EXP-0 基线（含 §0 探针） | A/B | `_physicsMode`=0 已确认；Editor 编译通过，EditMode 78/78 | done（无数值基线：用户在改动前只有目视记忆，两端都有 A/B） | 8046598 | 目视：改前有速度相关重影 | 目视：改前交接后被拉回起点重新加速 | 探针保留在 `dumpReconcile` 门控下；数值基线未采 |
| EXP-1 Physics Mode | A | 前提满足：`_physicsMode`=0、Rigidbody 插值 None、fixedDeltaTime=1/60（MCP 实测） | done（单端目视达标；双端与延迟矩阵未测） | 3a8b997, 28414ce | 用户 2026-10-01 单端实测：重影消失 | 不再强行拉回起点；交接切换瞬间仍有一次小卡顿 | 保留；A 的双端复核并入 EXP-2 的测试；B 残余进入 EXP-2；2026-10-05 用户双端实测完全可玩 |
| EXP-2 消费后屏蔽窗口 | B | 未做预检 | 未执行（2026-10-05 收尾：用户双端实测完全可玩，不再需要） | | | 双端实测未见拉回起点 | 不做 |
| EXP-3 投影与回放时钟 | B | | 未执行（2026-10-05 收尾：用户双端实测完全可玩，不再需要） | | | | 不做 |
| EXP-4 多写入者 | A/B | | 未执行（2026-10-05 收尾：用户双端实测完全可玩，不再需要） | | | | 不做 |
| EXP-5 视觉参考系 | A | | 未执行（2026-10-05 收尾：用户双端实测完全可玩，不再需要） | | | | 不做 |

## 正式修复表

| 章节 | 内容 | 前置实验 | 状态 | 提交 | 验收结果 |
|---|---|---|---|---|---|
| §H 残余 | handoff 待消费期间 spline 持续驱动（R7.2/R7.3 残余）+ 消费时落地（R7.5） | PR #60 合入后用户单端复现 | done（定性：仍有一点点停顿，用户接受） | 26742dd | 用户单端目视可接受；逐帧量化未做 |
| §1 | Physics Mode → TimeManager + 不变量断言 | EXP-1 | done（验收矩阵未跑） | 3a8b997, 28414ce | 场景运行时 `PhysicsMode=TimeManager`、Buddah 刚体插值 None、graphical=VisualRoot（MCP execute_code 读取）；验收矩阵待跑 |
| §2 | handoff / teleport 回放安全（D1 选 A 或 B） | EXP-2 | 未执行（2026-10-05 收尾：用户双端实测完全可玩，不再需要） | | |
| §3.1 | 投影锚点统一（D2） | EXP-3 | 未执行（2026-10-05 收尾：用户双端实测完全可玩，不再需要） | | |
| §3.2 | 服务器侧远端身体 kinematic | EXP-4.1 | 未执行（2026-10-05 收尾：用户双端实测完全可玩，不再需要） | | |
| §4 | 回放时钟 | EXP-3.2 | 未执行（2026-10-05 收尾：用户双端实测完全可玩，不再需要） | | |
| §5 | 视觉参考系（D3） | EXP-5 | 未执行（2026-10-05 收尾：用户双端实测完全可玩，不再需要） | | |
| 收尾 | 全矩阵回归、文档更新、探针移除 | 全部 | done（部分）：文档已更新（networking.md、prediction-design.md、本文件）；探针按团队规则保留并默认关闭；全矩阵回归未跑，以用户双端定性实测代替 | 见 git log | 用户双端实测完全可玩 |

## 基线表（EXP-0 填写）

| 视角 / 延迟 | reconcile 位置校正 均值 / p95 / 最大 (u) | spectator 向后位移帧占比 | GO 后 1 秒最大校正 (u) | consume 到首个大校正的 tick 差 | motorVisualPosDelta 顶速 (u) |
|---|---|---|---|---|---|
| host-owner / 0 ms | | | | | |
| client-owner / 0 ms | | | | | |
| host 观察 client / 0 ms | | | | | |
| client 观察 host / 0 ms | | | | | |
| host-owner / 100 ms | | | | | |
| client-owner / 100 ms | | | | | |
| host 观察 client / 100 ms | | | | | |
| client 观察 host / 100 ms | | | | | |

前提确认：`_physicsMode` 实测值 = 0（Unity，`MainMenu.unity:6412`，2026-10-01 在本 worktree 读取，EXP-1 已改为 1）；现象观察端 = 两者（用户回报：改动前 host 与纯 client 都有 A 和 B）。

## 决策表

| 编号 | 问题 | 决定 | 日期 |
|---|---|---|---|
| D1 | §2 采用确认门控（A）还是回放重放（B） | 不决定：对应修复未执行（收尾时不需要）。若问题复现再定；D1 建议 A、D2 建议先 D2-a 仍有效 | 2026-10-05 |
| D2 | handoff 快照是否改为服务器直接采样（D2-b）或服务器同样投影（D2-a） | 不决定：对应修复未执行（收尾时不需要）。若问题复现再定；D1 建议 A、D2 建议先 D2-a 仍有效 | 2026-10-05 |
| D3 | spectator 插值策略与 teleport 阈值 | 不决定：对应修复未执行（收尾时不需要）。若问题复现再定；D1 建议 A、D2 建议先 D2-a 仍有效 | 2026-10-05 |
| D4 | acceptance.md 中标注"待定"的阈值 | 不决定：对应修复未执行（收尾时不需要）。若问题复现再定；D1 建议 A、D2 建议先 D2-a 仍有效 | 2026-10-05 |

## 新发现

（与本任务无关、不在本分支修复的缺陷记在这里。）

- 2026-10-01：交接瞬间小卡顿的本地根因候选记为 findings.md R7；其中 R7.1（相机以 `rb.velocity` 为速度源，开场期间恒为 0）与 R7.2（GO 到下一 tick 消费之间的 1–2 帧死区）在 host 与单机上都成立，属本分支范围内的交接问题，不是无关缺陷。用户决定：拆成独立分支 `fix/launch-handoff-continuity`，单机模式可直接合入；本分支只做联机。

- 2026-10-01：findings.md 假设 host 自己的身体不会出现 A/B，但用户回报改动前 host 端也有，且 EXP-1 后在单端测试中消失。说明 host 本地 client 侧的 reconcile（`_createLocalStates` 的本地状态回退）在 Unity 物理模式下同样是真实回跳；根因排序不变，EXP-4 的"多写入者"暂不需要提前。
- 2026-10-01：Physics Mode 改为 TimeManager 后，FishNet 把 `Physics.simulationMode` 持久化为 Script。Editor 里直接 Play 不含 NetworkManager 的场景时刚体不会步进，属已知代价，不在本分支处理。

## 备注

- 2026-10-01：本 worktree 专用 Unity 2022.3.55f1c1 实例已通过 MCP 连接（`online-prediction-handoff@41b28e58f7c667d2`）。ParrelSync 克隆位于 `.worktree/online-prediction-handoff_clone_0`（Assets/ProjectSettings 为符号链接，Library/Packages 为副本，参数 `client`）。
- 2026-10-01：FishNet `TimeManager.OnValidate` 在 Physics Mode 改为 TimeManager 后把 `Physics.simulationMode` / `Physics2D.simulationMode` 写成 Script 并持久化到 `ProjectSettings/DynamicsManager.asset`、`Physics2DSettings.asset`（28414ce）。游戏内 NetworkManager 为 DontDestroyOnLoad 且 Solo 也经 `GameNetworkManager` 启动 host，物理仍由 TimeManager 推进；但在 Editor 里直接 Play 不含 NetworkManager 的场景（如单独打开 RaceMap）时物理不会步进。
- 2026-10-01：`Buddah.prefab` 的 `enableVerboseLogs` / `dumpReconcile` 在工作区临时置 1 用于采样，不提交。

- 2026-10-01：分支创建，提交准备文档（findings / acceptance / experiments / fix-spec / HANDOFF）。审查为静态分析，未运行 Unity。
