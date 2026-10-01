# 排查实验顺序（按可能性从高到低）

目的：每次只改一个变量，用同一套场景和同一组指标测量，判定后决定停止还是进入下一项。不是最终修复方案；某项实验证实假设后，再按 [acceptance.md](acceptance.md) 做正式修复。根因编号 R1–R6 见 [findings.md](findings.md)。

## 两个症状分开计分

| 症状 | 代号 | 达标定义（来自 acceptance.md） |
|---|---|---|
| 行驶中撕裂 / 重影，速度越快越大 | **A** | owner 稳态 reconcile 位置校正 p95 ≤ 0.05 u 且最大值 ≤ 1 tick 位移；spectator 向后位移帧占比 < 1%；目视无随速度增长的重影 |
| 开场交接后回到起点重新加速 | **B** | consume 后 1 秒内无 > 0.5 u 校正；位置沿前进方向单调；平面速度不低于继承速度 − 10%；三阶段 tick 跨度与 server 一致 |

退出规则：某症状达标，后面为它安排的实验就不做；两个症状都达标即结束。最差情况五项全部走完。

## 实验卫生

- 每项实验前先跑**预检**，确认假设的前提确实存在；前提不存在就跳过该项并记录。
- 一次只改一个变量。实验改动单独提交，标题 `chore: [EXP-n] …`，便于 `git revert`。prefab/场景改动用 `git restore` 回退。
- 每项实验在四个视角 × 两档延迟（0 / 100 ms）下各跑一遍标准脚本：顶速直线 60 s、连续转向 60 s、开赛 5 次。
- 结果填入 [progress.md](progress.md) 的实验表。静态检查不能写成运行通过。
- 实验用临时手段（常量、开关）验证假设即可；被证实的假设在正式修复阶段用正确做法重做。

## 指标采集

- `dumpReconcile` 打开，取 `DebugState.lastReconcilePositionDelta / lastReconcileVelocityDelta`，按 tick 导出，算均值 / p95 / 最大。
- `DebugState.motorVisualPosDelta`（物理根与 VisualRoot 的距离）。
- `[HandoffDebug]`、`[IntroHandoff]`、`[IntroSplineDiag]` 的时间线；特别是 `consuming queued handoff` 到其后第一个大校正的 tick 差。
- Standalone 保留 `BUDDAH_PREDICTION_SHADOW`，看 `[D-LOC HEARTBEAT]` 各轴 div。
- spectator 向后位移：对远端身体每帧记录位置，沿其前进方向投影，统计为负的帧占比。
- 每项实验录 owner 与 spectator 视角各一段高速视频，和 EXP-0 对比。

---

## EXP-0 基线（必做）

- 不改任何东西，跑标准脚本，填基线表。
- 顺便确认两个前提：
  - `MainMenu.unity` 的 TimeManager `_physicsMode` 是否为 0（Unity）。
  - 现象是在 host 还是纯 client 上观察到的。按分析，host 自己的身体不应有 A/B；host 看远端有 A。若 host 自己的身体也有 A，说明还有分析之外的原因，优先排查多写入者（EXP-4）。

## EXP-1 Physics Mode → TimeManager（R1，针对 A）

- **假设**：回放不做物理积分，所以每次 reconcile 都是不可恢复的回跳，位移与速度和 RTT 成正比。
- **预检**：EXP-0 已确认 `_physicsMode: 0`；Rigidbody 插值为 None；`Fixed Timestep` 为 1/60，与 tick rate 60 一致。
- **干预**：NetworkManager → TimeManager → Physics Mode 改为 TimeManager。不改别的。
- **测量**：标准脚本全量。额外关注依赖 FixedUpdate 的功能是否仍正常：开场 spline 驱动、推手弹体、PushHitbox、复活、结果区传送。
- **判定**：
  - A 达标 → 症状 A 结束，保留改动；继续 EXP-2 处理 B。
  - A 明显改善但未达标（校正下降一个数量级、重影仍随速度增长） → 保留改动，进入 EXP-5。
  - A 无变化 → 保留改动（这是 FishNet 预测的硬前提，不回退），进入 EXP-4。
- **预期**：A 大幅改善；B 不变，甚至因为回放开始真正执行而更清楚地显示为"消费后被拉回起点"。

## EXP-2 延长 handoff 消费后的 reconcile 屏蔽窗口（R3，针对 B）

- **假设**：消费 handoff 后，更早的服务器快照把刚体和 handoff 状态覆盖回起点。
- **预检**：100 ms 下看纯 client owner 的日志：`consuming queued handoff` 之后 1 到 3 个 tick 内出现一条 reconcile，其 `posDelta` 约等于投影距离，且其后 `handoff state transition` 显示状态回到 Normal 或 Inherit 重新开始。能看到这个序列才做本项。
- **干预**（临时探针，二选一）：
  - 在 motor 里加一个临时常量：消费 handoff 后的 N 个 tick 内，`ReconcileState` 继续跳过刚体 reconcile 和 `_handoffState / _introControlActive / _externalKinematicControlActive` 的覆盖，并对 `ReplicateState` 为 Replayed 的输入直接返回。N 取 RTT tick 数 + `_stateInterpolation` + 2，100 ms 下约 10。
  - 或者只做观察：把 `CreateReconcile` 里的 `LastConsumedHandoffId` 填成服务器真实值并打印，确认覆盖 owner 的那几份快照确实 id 为 0。
- **测量**：开赛 10 次 × 两档延迟；consume 后 1 秒的校正分布；三阶段 tick 跨度；视频。
- **判定**：
  - B 达标 → 假设成立。探针不能作为最终方案（硬编码 N 对 RTT 敏感），正式修复按 acceptance.md P2 的确认门控或回放重放实现；B 结束。
  - B 改善但仍有一次固定大小的校正，其大小约为 速度 × 单程延迟 → 进入 EXP-3。
  - B 无变化 → 回退探针，进入 EXP-3，并复查预检日志是否看错了对象（host 的身体不会有这条序列）。

## EXP-3 统一投影与回放时钟（R4 + R5，针对 B 的残余）

- **假设**：owner 投影而 server 不投影，交接瞬间两边必然相差 速度 × (单程延迟 − 2 tick)；回放时用现在的 LocalTick 推进阶段，让回放与前向不一致。
- **预检**：日志里 `stale handoff adjusted … staleTicks=` 的值明显大于 2；server 侧 `handoff authoritative create` 的位置等于原快照位置。
- **干预**（两步，分开提交）：
  1. 探针：owner 消费时不投影（临时把 `ProjectForArrivalTick` 的结果丢弃，直接用原快照），观察残余校正是否消失。消失说明不对称是来源，正式方案再决定由谁投影。
  2. 探针：`RunInputs` 内与 tick 相关的推进在回放中改用输入自身 tick（`data.GetTick()`），与冲量通道口径一致。看 100 ms 下 D-LOC 的 `hof-div / mod-div` 是否归零。
- **判定**：B 达标 → 结束。否则进入 EXP-4。

## EXP-4 排除多写入者与状态镜像（R6 与 findings 可能性 4，针对 A/B 残余）

- **假设**：刚体或控制标志被 tick 之外的代码写入，或被多份镜像互相覆盖。
- **预检**：EXP-1 之后若 host 自己的身体仍有 A，或 B 在 EXP-2/3 后仍有不规则的（大小不固定的）跳动，才做本项。
- **干预**（每次只开一个，分别测）：
  1. 服务器侧：`CompleteGoTransition` 对非本地 owner 不再 `isKinematic = true`，看服务器在 GO 到 ServerRpc 之间的状态是否还会产生 kinematic 刚体上的施力警告和异常快照。
  2. 开场 spline 驱动：把 `DriveSplinePose` 的刚体写入改为只在 tick 回调里执行（或暂时只写 VisualRoot 不写刚体），看交接前最后几帧的 `[IntroSplineDiag]` 是否仍有回退/大步。
  3. VisualRootBridge：临时关闭 `lockVisualRootDuringIntroAndPresentation`，看交接后 4 帧内的视觉是否反而更稳定（两个平滑器互相争抢的证据）。
  4. 列出所有在 prediction 模式下仍会写 `rb.*` 的 MonoBehaviour（复活、结果区传送、legacy movement、hitbox），逐个在运行时打断点或加一行日志，确认 Replicate 之外没有写入。
- **判定**：找到写入者并移除后 A/B 达标 → 结束。否则进入 EXP-5。

## EXP-5 视觉参考系（R2，针对 A 的残余"分离感"）

- **假设**：撕裂的剩余部分不是模拟校正，而是物理根与 VisualRoot 的固有插值差，加上挂错层级的可见物。
- **预检**：EXP-1 之后 owner 的 `motorVisualPosDelta` 稳定约等于 1 tick 位移（80 u/s 时约 1.3 u），spectator 约等于 (RTT tick + 4) 个 tick 位移；同时技能 VFX 或 `沙砾` 肉眼可见地领先模型。
- **干预**（分开提交，逐个测）：
  1. 技能 VFX 的父节点临时改为 graphical object；`沙砾` 挪到 VisualRoot 下。
  2. `Buddah.prefab` 上 `_adaptiveInterpolation` 改 Off、`_spectatorInterpolation` 保持 2，观察 spectator 的分离与平滑度权衡。
  3. 开启 `_enableTeleport`，阈值从 2 u 起试，看大校正是否不再被拖成重影。
- **判定**：目视和 `motorVisualPosDelta` 达标 → 结束。否则把剩余现象、数据和视频记入 progress.md 的"新发现"，停下来找人。

---

## 顺序总览

```
EXP-0 基线
  ├─ A: EXP-1 ──达标──> 结束A
  │        └─未达标──> EXP-4 ──> EXP-5
  └─ B: EXP-2 ──达标──> 结束B
           └─残余──> EXP-3 ──> EXP-4
```

EXP-1 永远第一个做，且无论结果都保留。EXP-2 依赖 EXP-1 之后的回放真实执行，否则读数不可信。
