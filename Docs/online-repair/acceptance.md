# 联机修复：目标效果与测试计划

本文定义修复完成时应观察到的行为，以及每个阶段必须跑的测试。根因编号 R1–R6 见 [findings.md](findings.md)，实现细节见 [fix-spec.md](fix-spec.md)，实验顺序见 [experiments.md](experiments.md)。阈值中标注“待定”的，在 P0 基线采集后由团队确认并回填。

## 最终需求效果

### 常态行驶（任意速度，含 80 u/s 顶速）

- owner 在 0 ms 与 100 ms 模拟延迟下，稳态 reconcile 位置校正的 p95 不超过 0.05 u，最大值不超过 1 个 tick 的位移（速度 × tickDelta）。
- spectator 看到的远端玩家连续前进，不出现向后跳跃；向后位移（相对上一帧沿前进方向的投影）为负的帧占比低于 1%。
- 模型、技能 VFX、`沙砾`、拖尾在同一参考系内：技能 VFX 与模型的锚点距离不随速度增长。owner 的 `motorVisualPosDelta` 不超过 1 tick 位移；spectator 不超过所配置插值 tick 数的位移。
- 不出现与速度成正比的重影。高速视觉是否可接受仍由用户目视决定，录像作为证据。

### 开场交接

- 纯客户端 owner：spline 阶段结束到 Normal 态之间，刚体位置沿前进方向单调，不回到起点；平面速度在交接后第一秒内不低于继承速度减 10%（阈值待定）。
- `[HandoffDebug] consuming queued handoff` 之后 1 秒内，不出现位置校正超过 0.5 u（待定）的 reconcile；`_handoffState` 不会在 BlendEnd 之前被重置为 default。
- Inherit / Blend / Normal 三个阶段在 server 与 client 上的 tick 跨度一致（配置为 12 / 20）。
- host 作为 owner 的行为不回退。
- 服务器对远端身体不再在 GO 之后对 kinematic 刚体施力。

### 不变项

- 技能命中、冲量、modifier、复活传送的现有行为不变（R6/R7 对比基线）。
- 单机模式不受影响（它不走 reconcile）。

## 测试环境

- 沿用团队做法：host 用 Development Build，client 用 Editor 或 ParrelSync 克隆。Standalone 保留 `BUDDAH_PREDICTION_SHADOW`，看 `[D-LOC HEARTBEAT]` 的 `loc-div/tel-div/mod-div/hof-div`。
- 延迟矩阵：FishNet 延迟模拟 0 ms 与 100 ms（双端），再加一次真实 Steam 会话。
- 视角矩阵：host-owner、client-owner、host 观察 client、client 观察 host。
- 采样：按 experiments.md 的"指标采集"表执行；每次 reconcile 的校正量来自 fix-spec §0 的 `[ReconcileDelta]` 探针（现有 `[PredictionIntro][Reconcile]` 有去重，不能算分布）。

## 阶段与测试

### P0 基线（任何代码改动之前）

1. 顶速直线与连续转向各 60 秒，四个视角 × 两档延迟。导出 reconcile 位置/速度校正的均值、p95、最大值；导出 spectator 向后位移帧占比。
2. 开赛 5 次，记录每次 GO 后第一秒的校正分布和 `HandoffDebug` 事件顺序（consume 与其后第一个大校正的 tick 差）。
3. 录制 owner 与 spectator 视角各一段高速视频。
4. 结果写入 [progress.md](progress.md) 的基线表。

### P1 Physics Mode → TimeManager（R1）

- 改动：NetworkManager 上 TimeManager 的 Physics Mode 改为 TimeManager。Rigidbody 插值已是 None，fixedDeltaTime 已是 1/60，不需要其他调整。
- 回归面：所有依赖 FixedUpdate 与自动物理的代码。重点：`RaceBodyIntroStateController.FixedUpdate` 的 spline 驱动、`PushHitbox`、推手弹体 `HandPushProjectileRuntime / ChargedHandProjectileRuntime`、`BuddahRespawn`、结果区传送。
- 验收：重复 P0 第 1 项；owner 稳态校正达到最终效果阈值；D-LOC 各轴 div=0；回归面功能与基线一致。
- 预期 handoff 的第一秒校正仍在，这是 P2 的目标。

### P2 handoff 与 teleport 的回放安全（R3、R4、R6）

- 改动方向（二选一，由团队决定）：
  - 确认门控：`CreateReconcile` 填入服务器真实的 `LastConsumedHandoffId / LastConsumedTeleportId`；owner 在服务器快照落后于本地消费时继续跳过刚体 reconcile 与状态覆盖，并在该窗口内对 `ReplicateState` 为 Replayed 的输入直接返回。
  - 回放重放：保留已消费事件及其消费 tick；reconcile 到消费 tick 之前的状态时重新入队，让回放在正确 tick 重新应用快照。
- 同时统一投影锚点：由服务器在 GO 时刻采样 spline 并下发 handoff，用 `Owner.LocalTick` 推算 owner 起始 tick；或至少让 server 与 owner 使用同一套投影规则。服务器对远端身体不再在 GO 后设置 kinematic。
- 验收：开赛 10 次 × 两档延迟，全部满足“开场交接”一节；Inherit/Blend/Normal 跨度一致；复活传送连续 5 次无回跳。

### P3 视觉参考系（R2）

- 改动方向：技能 VFX 的父节点改为 graphical object；`沙砾` 移到 VisualRoot 下；评估 spectator 的 adaptive 插值是否关闭或降级；启用 teleport 阈值防止大校正被拖成重影。
- 验收：`motorVisualPosDelta` 与 VFX 锚点距离满足最终效果；case 12 同类测量下发射点与模型的偏差不随速度增长；录像对比 P0。

### P4 回放时钟（R5）

- 改动方向：`RunInputs` 内与 tick 相关的推进（handoff 阶段、modifier 到期、teleport/handoff 消费判断）使用输入自身的 tick 或 `ServerReplayTick`，与冲量通道一致。
- 验收：100 ms 下 D-LOC `hof-div / mod-div / tel-div` 为 0；在 Inherit/Blend 窗口内施放 modifier 技能不产生额外校正。

### 收尾

- 四个视角 × 两档延迟 × 真实 Steam 会话各跑一遍 P0 的全部项目，与基线对比写入 progress.md。
- 更新 [networking.md](../networking.md) 与 [prediction-design.md](../prediction-design.md) 中与 Physics Mode、事件回放和视觉参考系相关的约束。

## 需要团队决定

- D1：P2 采用确认门控还是回放重放。
- D2：handoff 快照的权威来源是否改为服务器直接采样 spline。
- D3：spectator 插值策略（adaptive 等级或固定值）与 teleport 阈值数值。
- D4：各阈值中标注“待定”的最终数值。
