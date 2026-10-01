# 排查实验顺序（按可能性从高到低）

每次只改一个变量，用同一套场景和同一组指标测量，判定后决定停止还是进入下一项。实验用最小探针验证假设；被证实的假设再按 [fix-spec.md](fix-spec.md) 做正式修复。根因编号 R1–R6 见 [findings.md](findings.md)。

## 两个症状分开计分

| 症状 | 代号 | 达标定义（来自 acceptance.md） |
|---|---|---|
| 行驶中撕裂 / 重影，速度越快越大 | **A** | owner 稳态 reconcile 位置校正 p95 ≤ 0.05 u 且最大值 ≤ 1 tick 位移；spectator 向后位移帧占比 < 1%；目视无随速度增长的重影 |
| 开场交接后回到起点重新加速 | **B** | consume 后 1 秒内无 > 0.5 u 校正；位置沿前进方向单调；平面速度不低于继承速度 − 10%；三阶段 tick 跨度与 server 一致 |

退出规则：某症状达标，后面为它安排的实验就不做；两个症状都达标即结束。最差情况五项全部走完。

## 实验卫生

- 每项实验前先跑**预检**，确认假设的前提确实存在；前提不存在就跳过该项并记录。
- 一次只改一个变量。探针单独提交，标题 `chore: [EXP-n] …`，正式修复前 `git revert`。prefab/场景改动用 `git restore` 回退。
- 每项实验跑一遍标准脚本（见下），四个视角 × 两档延迟。
- 结果填入 [progress.md](progress.md) 的实验表。静态检查不能写成运行通过。

## 标准脚本

1. 两端进入同一局，等待开赛。开赛过程本身就是 B 的样本。
2. 顶速直线 60 秒（按住前进，不转向）。
3. 连续转向 60 秒（S 形）。
4. 释放一次推击、一次减速陷阱，确认技能仍正常（回归，不计分）。
5. 重复开赛 5 次（结果区选择"再来一局"）。

视角矩阵：host-owner、client-owner、host 观察 client、client 观察 host。延迟：TransportManager `_latencySimulator` 0 ms 与 100 ms（两端都设，`_simulateHost: 1`）。

## 指标采集

开 `Buddah.prefab` → BuddahPredictionBootstrap → Debug Settings 的 `enableVerboseLogs` 与 `dumpReconcile`（或 `NetDebug.EnableVerboseLog = true`）。Development Build 的日志在 Player.log，Editor 的在 Editor.log。

| 指标 | 来源 | grep 前缀 | 说明 |
|---|---|---|---|
| 每次 reconcile 的位置/速度校正 | fix-spec §0 探针 | `[ReconcileDelta]` | 现有 `[PredictionIntro][Reconcile]` 有去重，只在状态变化时输出，不能用来算分布 |
| spectator 向后位移 | fix-spec §0 探针 | `[SpectatorStep]` | forwardStep < 0 的帧占比 |
| 物理根与 VisualRoot 距离 | `DebugState.motorVisualPosDelta`，经控制台镜像 | `[BuddahPredictionV2]` 摘要 | 0.5 s 采样一次，看量级即可 |
| handoff 时间线 | 既有日志 | `[HandoffDebug] owner request sending ServerRpc` → `[HandoffDebug] ServerRpc received` → `[HandoffDebug] TargetRpc received` → `[HandoffDebug] consuming queued handoff` → `[IntroHandoff][Prediction] Handoff consumed` → `handoff state transition` | 需要 `enableVerboseLogs` |
| 投影量 | 既有日志 | `[HandoffDebug] stale handoff adjusted … staleTicks=` | 预期 ≈ RTT tick − 2 |
| spline 驱动异常 | 既有日志 | `[IntroSplineDiag]` 中 `backApplied=True` 或 `snapApplied=True` | |
| 确定性 | Standalone 保留 `BUDDAH_PREDICTION_SHADOW` | `[D-LOC HEARTBEAT]` 的 `loc-div/tel-div/mod-div/hof-div` | |
| 严格的 GO 后 1 秒校正 | 归档夹具 `Tools/Validation/ReviewRuntimeRound2` | `summarize-runtime-stream.py` 的 `correctionStats` | 需要应用观察补丁，只在一次性 worktree 使用 |

统计脚本放 `Tools/Validation/OnlineRepair/`，输出均值 / p95 / 最大 / 负值占比，按 owner 与非 owner 分组。

---

## EXP-0 基线（必做）

- 先加 fix-spec §0 探针（这是唯一允许在基线前做的改动，它不改变行为）。
- 跑标准脚本，填基线表。
- 确认两个前提：
  - `MainMenu.unity` 的 TimeManager `_physicsMode` 是否为 0（Unity）。
  - 现象是在 host 还是纯 client 上观察到的。按分析，host 自己的身体不应有 A/B；host 看远端有 A。若 host 自己的身体也有 A，说明还有分析之外的原因，优先排查多写入者（EXP-4），并回报。

## EXP-1 Physics Mode → TimeManager（R1，针对 A）

- **假设**：回放不做物理积分，所以每次 reconcile 都是不可恢复的回跳，位移与速度和 RTT 成正比。
- **预检**：EXP-0 已确认 `_physicsMode: 0`；Rigidbody 插值为 None；`Fixed Timestep` 为 1/60。
- **干预**：NetworkManager → TimeManager → Physics Mode 改为 TimeManager。不改别的。细节与回归面见 fix-spec §1。
- **测量**：标准脚本全量；回归面逐项过一遍。
- **判定**：
  - A 达标 → 症状 A 结束，保留改动；继续 EXP-2 处理 B。
  - A 明显改善但未达标 → 保留改动，进入 EXP-5。
  - A 无变化 → 保留改动（FishNet 预测的硬前提，不回退），进入 EXP-4。
- **预期**：A 大幅改善；B 不变，甚至因为回放开始真正执行而更清楚地显示为"消费后被拉回起点"。

## EXP-2 延长 handoff 消费后的 reconcile 屏蔽窗口（R3，针对 B）

- **假设**：消费 handoff 后，更早的服务器快照把刚体和 handoff 状态覆盖回起点。
- **预检**：100 ms、纯 client owner：`[HandoffDebug] consuming queued handoff` 之后 1 到 3 tick 内出现一条 `[ReconcileDelta]`，其 `posDelta` 约等于 `stale handoff adjusted` 的投影距离，且其后 `handoff state transition` 显示状态回到 Normal 或 Inherit 重新开始。看到这个序列才做本项。
- **干预**（临时探针）：消费 handoff 后的 N 个 tick 内，`ReconcileState` 继续跳过刚体 reconcile 和 `_handoffState / _introControlActive / _externalKinematicControlActive / rb.isKinematic` 的覆盖，并对 `state.IsReplayed()` 的输入直接返回。N = RTT tick 数 + 2 + 2，100 ms 下约 10。
- **测量**：开赛 10 次 × 两档延迟；consume 后 1 秒的校正分布；三阶段 tick 跨度；视频。
- **判定**：
  - B 达标 → 假设成立。探针不能作为最终方案（硬编码 N 对 RTT 敏感），按 fix-spec §2 实现（D1 决定 A/B 方案）；B 结束。
  - B 改善但仍有一次固定大小的校正，其大小约为 速度 × 单程延迟 → 进入 EXP-3。
  - B 无变化 → 回退探针，进入 EXP-3，并复查预检日志是否看错了对象（host 的身体不会有这条序列）。

## EXP-3 统一投影与回放时钟（R4 + R5，针对 B 的残余）

- **假设**：owner 投影而 server 不投影，交接瞬间两边必然相差 速度 × (单程延迟 − 2 tick)；回放用现在的 LocalTick 推进阶段，让回放与前向不一致。
- **预检**：`stale handoff adjusted … staleTicks=` 明显大于 2；服务器 `handoff authoritative create … pos=` 等于原快照位置。
- **干预**（两步，分开提交）：
  1. 探针：owner 消费时不投影（临时丢弃 `ProjectForArrivalTick` 结果），观察残余校正是否消失。消失说明不对称是来源，正式方案见 fix-spec §3.1（D2 决定）。
  2. 探针：`RunInputs` 内 `currentTick` 在回放时改用 `data.GetTick()`（owner），看 100 ms 下 `[D-LOC HEARTBEAT]` 的 `hof-div / mod-div` 是否归零。正式方案见 fix-spec §4。
- **判定**：B 达标 → 结束。否则进入 EXP-4。

## EXP-4 排除多写入者与状态镜像（R6 与可能性 4，针对 A/B 残余）

- **假设**：刚体或控制标志被 tick 之外的代码写入，或被多份镜像互相覆盖。
- **预检**：EXP-1 之后若 host 自己的身体仍有 A，或 B 在 EXP-2/3 后仍有大小不固定的跳动，才做本项。
- **干预**（每次只开一个，分别测）：
  1. 服务器侧：`CompleteGoTransition` 对非本地 owner 不再 `isKinematic = true`（fix-spec §3.2），看服务器 GO 到 ServerRpc 之间是否还产生 kinematic 施力警告与异常快照。
  2. 开场 spline 驱动：把 `DriveSplinePose` 的刚体写入改为只在 tick 回调执行（或暂时只写 VisualRoot），看 `[IntroSplineDiag]` 的 backApplied/snapApplied。
  3. VisualRootBridge：临时关闭 `lockVisualRootDuringIntroAndPresentation`，看交接后 4 帧内是否反而更稳定。
  4. 列出 prediction 模式下仍会写 `rb.*` 的脚本（`BuddahRespawn`、`PlayerProgressReporter` 结果区传送、`BuddahMovement`、`PushHitbox`、推手弹体），逐个加一行日志确认 Replicate 之外没有写入到玩家刚体。
- **判定**：找到写入者并移除后 A/B 达标 → 结束。否则进入 EXP-5。

## EXP-5 视觉参考系（R2，针对 A 的残余"分离感"）

- **假设**：撕裂的剩余部分不是模拟校正，而是物理根与 VisualRoot 的固有插值差，加上挂错层级的可见物。
- **预检**：EXP-1 之后 owner 的 `motorVisualPosDelta` 稳定约等于 1 tick 位移（80 u/s 时约 1.3 u），spectator 约等于 (RTT tick + 4) 个 tick 位移；同时技能 VFX 或 `沙砾` 肉眼可见地领先模型。
- **干预**（分开提交，逐个测，细节见 fix-spec §5）：
  1. 技能 VFX 父节点改为 graphical object；`沙砾` 挪到 VisualRoot 下。
  2. `_adaptiveInterpolation` → Off，`_spectatorInterpolation` 保持 2。
  3. `_enableTeleport` → 1，阈值从 2 u 起试。
- **判定**：目视和 `motorVisualPosDelta` 达标 → 结束。否则把剩余现象、数据和视频记入 progress.md 的"新发现"，停下来找人。

## EXP-6 本地交接连续性（R7，针对「切换瞬间小卡顿」，单端可测，联机修复完成后再做）

- **顺序约束**：本项排在 EXP-2 到 EXP-5 之后。先把联机（reconcile、handoff 回放安全、投影）修正并验收，再处理本地切换，避免两类改动互相掩盖。
- **硬约束**：开场 spline 动画的播放效果（路径、速度、相机稳定模式、时长）必须保持与现在一致；任何干预或修复若改变开场动画观感，即判为不通过。
- **假设**：EXP-1 之后剩余的卡顿来自 spline 驱动到 motor 驱动的本地切换（findings.md R7），host 与单机同样出现，与网络无关。
- **预检**：单端（host 或单机）开场仍能看到切换瞬间卡一下；`[HandoffDebug] consuming queued handoff` 的 `currentTick` 比 `[IntroGo] CompleteGoTransition` 所在 tick 大 1 以上。
- **采集**：GO 前后各 0.5 s 逐帧记录 VisualRoot 位置、物理根位置、`rb.velocity`、相机 FOV 与跟随偏移（临时探针，见 fix-spec §6.0）。判定口径见 acceptance.md「交接连续性」。
- **干预**（每次只开一个，分别测，全部可在 Inspector 或一行代码内完成，不进正式提交）：
  1. **R7.1 相机**：`Buddah.prefab` → PlayerCamera 的 `zoomBySpeed` 置 0 且 `directionalOffsetPerSpeed` 置 (0,0,0)。卡顿消失或明显减弱 → 相机速度源是主因。
  2. **R7.2 死区**：在 `RaceBodyIntroStateController.DriveSplinePose` 末尾临时加 `targetRigidbody.velocity = snapshot.Velocity`，并在 `CompleteGoTransition` 里打印 `TimeManager.LocalTick`，与 consume 的 tick 比较。差值 ≥1 即确认死区存在；同时观察相机是否因速度连续而不再顿挫（与 1 交叉验证）。
  3. **R7.3 视觉参考系**：`Buddah.prefab` → BuddahPredictionVisualRootBridge 的 `lockVisualRootDuringIntroAndPresentation` 置 0。切换处「停、吸、再跟」是否消失；开场期间是否出现新的视觉滞后（预期 1 tick）。
  4. **R7.4 角速度**：查看所用 spline 末端是否为直线（SplineIntroPath 末段切线变化）；若有曲率，临时把 `ApplyLaunchInheritedVelocity` 中的角速度参数改为 `rb.angularVelocity`。
  5. **R7.5 高度**：日志打印消费时 `snapshot.Position.y` 与消费后第 10 tick 的 `rb.position.y`，差值 > 0.05 u 即存在落差。
- **判定**：
  - 1 或 2 单独就让卡顿消失 → 按 fix-spec §6.1 实施相机与速度源修复（最小方案），其余子项按需。
  - 1、2 之后仍有位置停顿 → 3 也成立，按 §6.2 决定最小方案（渲染采样偏移 + 删 post-intro 锁 + GO 当帧消费）还是架构方案（spline 进 motor 状态，D5）。
  - 全部干预后仍有卡顿 → 记入 progress.md「新发现」，停下来找人。
- **预期**：host 与单机在本项后交接连续；纯 client 的 RTT 死区与投影差已在 EXP-2/EXP-3 处理完毕，本项不再涉及。

---

## 顺序总览

```
EXP-0 基线（含 §0 探针）
  ├─ A: EXP-1 ──达标──> 结束A
  │        └─未达标──> EXP-4 ──> EXP-5
  └─ B: EXP-2 ──达标──> 结束B
           └─残余──> EXP-3 ──> EXP-4
联机 A/B 全部达标后 ──> EXP-6（本地交接连续性，单端；开场动画效果不变）
```

EXP-1 永远第一个做，且无论结果都保留。EXP-2 依赖 EXP-1 之后的回放真实执行，否则读数不可信。EXP-6 只需单端，但必须等联机项全部达标后再做。
