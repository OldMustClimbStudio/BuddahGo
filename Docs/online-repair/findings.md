# 联机问题观察与根因分析（2026-10-01）

静态审查 `feat/single-player-mode`（24c022d）与 `dev`（7389ec8）的联机相关代码、prefab 与场景序列化，并对照 `Assets/FishNet/`（4.7.1）源码和 FishNet 官方文档。**没有在 Unity 中运行**；运行数据只引用仓库已有的 [review-runtime-round2.md](../optimization/review-runtime-round2.md)。所有引用的锚点在 dev 上同样存在。

## 用户报告的现象

1. 联机时存在 reconcile 导致的模型撕裂，多数表现为重影，每局稳定复现。
2. 开场 handoff 失调：spline 动画正常，交接后玩家突然被传送回起点并重新加速，没有自然接替感。
3. 速度越快撕裂越大；看自己和看对方都有，程度相近；技能效果基本正常。

## 结论概览

| 编号 | 根因 | 影响的现象 | 置信度 | 性质 |
|---|---|---|---|---|
| R1 | TimeManager 的 Physics Mode 为 `Unity`，预测回放不做物理积分 | 1、3 | 高 | 配置 |
| R2 | 技能 VFX、`沙砾`、碰撞体挂在物理根，模型挂在被平滑的 VisualRoot | 3（撕裂的另一半） | 高 | 结构 |
| R3 | handoff 是一次性、不可回放的事件；pending 清除后被更早的服务器快照覆盖 | 2 | 高 | 设计 |
| R4 | owner 与 server 对 handoff 快照的投影不对称 | 2（交接瞬间一次必然校正） | 中 | 设计 |
| R5 | 回放中用 `TimeManager.LocalTick` 作为当前 tick，FishNet 回放不回拨该值 | 2（修完 R1 后显形） | 中 | 设计 |
| R6 | 服务器侧远端身体在 GO 后被设为 kinematic，直到 ServerRpc 到达 | 2（放大 R3） | 中 | 时序 |

FishNet 本身的行为与其源码和文档一致；以上都是项目侧的配置、结构和设计问题。

## R1：Physics Mode = Unity

- 证据：`Assets/Scenes/MainMenu.unity` 中 TimeManager 的 `_physicsMode: 0`；`Assets/FishNet/Runtime/Managing/Timing/PhysicsMode.cs` 定义 `Unity = 0, TimeManager = 1, Disabled = 2`。项目代码没有任何运行时 `SetPhysicsMode` 调用。
- FishNet 官方文档（TimeManager 组件页）：“When using Client-Side Prediction you must use the TimeManager setting.” 预测配置页：“When using prediction it is essential that Fish-Networking's timing system is used… change the Physics Mode to Time Manager.”
- 源码对应：`PredictionManager.ReconcileToStates` 只有在 `tm.PhysicsMode == PhysicsMode.TimeManager` 时才在每个回放 tick 调用 `SimulatePhysics`（`PredictionManager.cs` 的 `timeManagerPhysics` 分支）。
- 后果链：
  - 每次 reconcile 把刚体 `SetState` 到服务器 k 个 tick 前的状态（k ≈ RTT tick 数 + `_stateInterpolation`），随后 `RunInputs` 被回放 k 次，但没有任何物理步进，只是把 k 份力堆在刚体上等下一次 FixedUpdate 一次性结算。回放等于没有发生。
  - 校正的可见位移 ≈ 速度 × k × tickDelta，与速度成正比；owner（自己的 reconcile）和 spectator（state forwarding 的 reconcile）机制相同，程度相近。
  - `_createLocalStates = true` 的本地状态回退在正常模式下回放后应回到原位；现在变成真实的向后跳跃，再被下一份服务器状态拉回。Steam P2P 的到包抖动会频繁触发。
  - VisualRoot 的平滑器没有启用 teleport 阈值（`Buddah.prefab` `_enableTeleport: 0`），上述来回跳跃被插值成拖影。
- 这也解释了 D-LOC shadow 对比发现不了问题：shadow 与 real 跑在同一份没有物理积分的回放里。

## R2：物理根与 VisualRoot 的速度相关分离

- `NetworkObject._graphicalObject` 是 `VisualRoot`；owner 插值 1 tick，spectator 用 `AdaptiveInterpolationType.Low`（实际插值 ≈ RTT tick 数 + 1 + 3，见 `UniversalTickSmoother.UpdateRealtimeInterpolation`）。按 80 u/s、60 tick：owner 的模型落后物理根约 1.3 u，spectator 落后 8 到 10 u。这是 FishNet 预测平滑的固有行为。
- 挂在物理根而不是 VisualRoot 下的可见物会和模型撕开：
  - `SkillVfxReplicator.ConfigureAttachment` 把复制的技能 VFX `SetParent(transform, false)`，`transform` 是 Buddah 根。
  - prefab 中直接挂在根下的 `沙砾` 嵌套预制体与 `RollbackHurtbox`。
  - 运行记录 case 12 测得发射点与模型相差 18 到 20 u，量级一致。
- 模型本身（`model` 蒙皮、`fotou` 骨架、`Trails`、`FeelVFX`、`LookAt`）都在 VisualRoot 下，模型内部不会因层级而撕裂。

## R3：handoff 消费不可回放，被旧快照覆盖

纯客户端 owner 的实际序列：

1. GO 时刻 `RaceBodyIntroStateController.CompleteGoTransition` → `BuddahMovement.BeginLaunchHandoff` → `BuddahPredictedMotor.RequestAuthoritativeLaunchHandoffFromOwner`，置 `_awaitingAuthoritativeLaunchHandoff = true`，发 ServerRpc。期间 owner 跳过刚体 reconcile（`ShouldSkipTransformReconcileDuringIntroOrPendingHandoff`），刚体停在起点。
2. 服务器约 RTT/2 后收到，以**自己的** `LocalTick` 入队并在下一 tick 消费，不做前向投影（`TryApplyServerAuthoritativeLaunchHandoff` 的 `serverStartTick`）；同时向 owner 发 TargetRpc。
3. owner 约 RTT 后收到并立即消费：清除 pending，把刚体设到投影后的快照位置和 20 u/s 速度（`ConsumePendingLaunchHandoffEvent`）。
4. 此时正在路上以及插值缓冲中的，是服务器**消费之前**生成的 reconcile。pending 一清，`ReconcileState` 不再跳过：`_predictionRigidbody.Reconcile` 把刚体拉回起点、速度归零；`_handoffState = data.HandoffState` 把状态覆盖为 default；`_introControlActive/_externalKinematicControlActive` 也被覆盖。事件已经出队，回放不会重新应用快照，于是玩家在起点以零速度被 Normal 态的推力重新加速。再过几 tick，带 handoff 的服务器状态到达，又被向前拉。
5. 仓库自己的测量佐证：开赛后第一秒 owner 的 reconcile 位置校正均值 0.6 到 1.1 u，峰值 4 到 17 u（review-runtime-round2.md 的校正表）。

`CreateReconcile` 中 `LastConsumedHandoffId / LastConsumedTeleportId` 被硬写为 0 并在 `ReconcileState` 中丢弃，说明确认门控曾被设计但未完成。Teleport（复活）走同一条一次性消费路径，存在同类风险，只是因为复活时速度低而不明显。

## R4：投影不对称

owner 按 `LocalTick − StartTick`（≈ RTT − 2 tick）投影快照，服务器放在原快照位置不投影。两边在交接瞬间就相差 `速度 × (单程延迟 − 2 tick)`，服务器权威，必然产生一次校正。`HandoffOwnerTickTravelBufferTicks = 2` 的常量无法覆盖真实 RTT。

## R5：回放用现在的 LocalTick

`RunInputs` 以 `TimeManager.LocalTick` 作为 `currentTick`。`PredictionManager` 回放时不回拨 `LocalTick`，所以回放旧 tick 时 Inherit/Blend 阶段推进、modifier 到期、teleport/handoff 的 `StartTick <= currentTick` 判断都按“现在”计算，与前向模拟不一致。冲量通道已经用 `ServerReplayTick` 处理了这一点（`BuddahTickMath.ImpulseEventClock`），其他事件没有。R1 修复后这会成为新的校正来源。

## R6：服务器侧远端身体在 GO 后 kinematic

`CompleteGoTransition` 对非本地 owner 的身体执行 `targetRigidbody.isKinematic = true`。在服务器上，远端玩家的身体从 GO 到 ServerRpc 到达的几个 tick 内，`RunInputs` 仍对 kinematic 刚体施力，这段服务器状态本身不正确，恰好是 R3 第 4 步覆盖 owner 的那几帧。

## 未验证与边界

- 没有运行 Unity，没有新采集日志；上面的数值来自代码推导和仓库既有记录。
- 没有确认用户观察到现象的是 host 还是纯 client。按分析，host 自己的身体不会出现 R1/R3；host 看远端玩家会出现 R1 的 spectator 分支和 R2。
- 技能命中正常，与分析一致：命中判定在物理根和服务器上，不依赖 VisualRoot。
