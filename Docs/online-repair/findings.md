# 联机问题观察与根因分析（2026-10-01）

静态审查 `feat/single-player-mode`（24c022d）与 `dev`（7389ec8）的联机相关代码、prefab 与场景序列化，并对照 `Assets/FishNet/`（4.7.1）源码和 FishNet 官方文档。**没有在 Unity 中运行**；运行数据只引用仓库已有的 [review-runtime-round2.md](../optimization/review-runtime-round2.md)。下文行号均以 dev 7389ec8 为准。

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

## 精确锚点（dev 7389ec8）

| 编号 | 文件 | 位置 | 内容 |
|---|---|---|---|
| R1 | `Assets/Scenes/MainMenu.unity` | :6412 | `_physicsMode: 0`（TimeManager 组件） |
| R1 | `Assets/FishNet/Runtime/Managing/Timing/PhysicsMode.cs` | 枚举 | `Unity = 0, TimeManager = 1, Disabled = 2` |
| R1 | `Assets/FishNet/Runtime/Managing/Prediction/PredictionManager.cs` | :662, :709-714 | 仅 `PhysicsMode.TimeManager` 时回放 tick 调 `SimulatePhysics` |
| R1 | `Assets/Character/Prefab/Buddah.prefab` | :588 | Rigidbody `m_Interpolate: 0`（正确，无需改） |
| R1 | `ProjectSettings/TimeManager.asset` | | `Fixed Timestep: 0.01666667`，与 tick 60 一致 |
| R2 | `Assets/Character/Prefab/Buddah.prefab` | :1073 | `_graphicalObject` = VisualRoot（localScale 4） |
| R2 | 同上 | :1077 / :1079 / :1081 | `_ownerInterpolation: 1`、`_adaptiveInterpolation: 3`(Low)、`_spectatorInterpolation: 2` |
| R2 | 同上 | :1082 / :1083 | `_enableTeleport: 0`、`_teleportThreshold: 1` |
| R2 | `Assets/Scripts/Network/Gameplay/SkillVfxReplicator.cs` | :237, :249 | `ConfigureAttachment` 把 VFX `SetParent(transform, false)`，父节点为物理根 |
| R2 | `Buddah.prefab` 层级 | | 根下：`RollbackHurtbox`、嵌套预制体 `沙砾`；VisualRoot 下：`model`、`fotou` 骨架、`Trails`、`FeelVFX`、`LookAt` |
| R2 | `Assets/FishNet/Runtime/Generated/Component/TickSmoothing/UniversalTickSmoother.cs` | `UpdateRealtimeInterpolation` | spectator 实际插值 = RTT tick 数 + 1 + adaptive 等级 |
| R3 | `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` | :214 `CreateReconcile`；:240, :243 | `LastConsumedTeleportId / LastConsumedHandoffId` 被硬写 0 |
| R3 | 同上 | :502 `ReconcileState`；:516 | `_predictionRigidbody.Reconcile(data.RigidbodyState)` |
| R3 | 同上 | :532, :540-541, :553 | `_handoffState`、`_introControlActive`、`_externalKinematicControlActive`、`rb.isKinematic` 被服务器快照覆盖 |
| R3 | 同上 | :636 | `ShouldSkipTransformReconcileDuringIntroOrPendingHandoff`：只在 intro/external/pending 时跳过 |
| R3 | `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.Events.cs` | :124 | `RequestAuthoritativeLaunchHandoffFromOwner`：置 `_awaitingAuthoritativeLaunchHandoff`，发 ServerRpc |
| R3 | 同上 | :579 `ConsumePendingLaunchHandoffEvent`；:604 | 一次性消费：清 pending、写刚体、记录 `_lastConsumedLaunchHandoffEventId` |
| R3 | 同上 | :480 `ConsumePendingTeleportEvent`；:490 | teleport 同样一次性消费 |
| R3 | `Assets/Scripts/RaceIntro/RaceBodyIntroStateController.cs` | :306 `CompleteGoTransition`；:332 | owner 在 GO 时调用 `BeginLaunchHandoff` |
| R4 | `BuddahPredictedMotor.Events.cs` | :175, :195-197 | 服务器 `serverStartTick = LocalTick` 不投影；owner `clientStartTick = ownerTickAtRequest + 2` |
| R4 | `BuddahPredictedMotor.Events.cs` | :593 | owner 消费时 `ProjectForArrivalTick` 按 `LocalTick − StartTick` 投影 |
| R4 | `BuddahPredictedMotor.cs` | :32 | `HandoffOwnerTickTravelBufferTicks = 2u` |
| R5 | `BuddahPredictedMotor.cs` | :342 | `currentTick = TimeManager.LocalTick`，回放中仍是当前 tick |
| R5 | `Assets/Scripts/New_Buddah/Core/BuddahTickMath.cs` | :9 | 冲量路径已用 `ImpulseEventClock`（回放中取 `ServerReplayTick`） |
| R6 | `RaceBodyIntroStateController.cs` | :353 | `!isLocalOwner` 时 `targetRigidbody.isKinematic = true`，服务器上也执行 |
| 日志 | `BuddahPredictedMotor.DebugState.cs` | :38-56 | `[PredictionIntro][Reconcile]` 仅状态变化时输出（有去重） |

## R1：Physics Mode = Unity

- FishNet 官方文档 TimeManager 组件页："When using Client-Side Prediction you must use the TimeManager setting."；预测配置页："When using prediction it is essential that Fish-Networking's timing system is used… change the Physics Mode to Time Manager."
- 项目代码没有运行时 `SetPhysicsMode` 调用；场景值即生效值。
- 后果链：
  - 每次 reconcile 把刚体 `SetState` 到服务器 k 个 tick 前的状态（k ≈ RTT tick 数 + `_stateInterpolation`=2），随后 `RunInputs` 被回放 k 次，但没有物理步进，只是把 k 份力堆在刚体上等下一次 FixedUpdate 一次性结算。回放等于没有发生。
  - 校正的可见位移 ≈ 速度 × k × tickDelta，与速度成正比；owner 与 spectator 机制相同，程度相近。
  - `_createLocalStates = true`（PredictionManager 默认）的本地状态回退在正常模式下回放后回到原位；现在是真实的向后跳跃，再被下一份服务器状态拉回。Steam P2P 的到包抖动会频繁触发。
  - VisualRoot 平滑器没有 teleport 阈值，这些来回跳跃被插值成拖影。
- 这也解释了 D-LOC shadow 对比发现不了问题：shadow 与 real 跑在同一份没有物理积分的回放里。

## R2：物理根与 VisualRoot 的速度相关分离

- owner 插值 1 tick，spectator 实际插值 ≈ RTT tick 数 + 1 + 3。按 80 u/s、60 tick：owner 的模型落后物理根约 1.3 u，spectator 落后 8 到 10 u。这是 FishNet 预测平滑的固有行为。
- 挂在物理根的可见物会和模型撕开：复制的技能 VFX、`沙砾`、`RollbackHurtbox`。运行记录 case 12 测得发射点与模型相差 18 到 20 u，量级一致。
- 模型内部（蒙皮、骨架、拖尾、FeelVFX）都在 VisualRoot 下，不会因层级而撕裂。Animator 为 Normal 更新模式、无 root motion；RaceMap 相机抗锯齿为 SMAA，无 TAA/运动模糊，排除渲染侧重影。

## R3：handoff 消费不可回放，被旧快照覆盖

纯客户端 owner 的实际序列：

1. GO 时刻 `CompleteGoTransition` → `BuddahMovement.BeginLaunchHandoff` → `RequestAuthoritativeLaunchHandoffFromOwner`，置 `_awaitingAuthoritativeLaunchHandoff = true`，发 ServerRpc。期间 owner 跳过刚体 reconcile，刚体停在起点。
2. 服务器约 RTT/2 后收到，以自己的 `LocalTick` 入队并在下一 tick 消费，不投影；同时向 owner 发 TargetRpc。
3. owner 约 RTT 后收到并立即消费：清除 pending，把刚体设到投影后的快照位置和 20 u/s 速度。
4. 此时在途以及插值缓冲中的，是服务器**消费之前**生成的 reconcile。pending 一清，`ReconcileState` 不再跳过：刚体被拉回起点、速度归零、`_handoffState` 覆盖为 default、intro/external 标志也被覆盖。事件已出队，回放不会重新应用快照，于是玩家在起点以零速度被 Normal 态推力重新加速。再过几 tick，带 handoff 的服务器状态到达，又被向前拉。
5. 仓库测量佐证：开赛后第一秒 owner 的 reconcile 位置校正均值 0.6 到 1.1 u，峰值 4 到 17 u（review-runtime-round2.md 校正表）。

`LastConsumedHandoffId / LastConsumedTeleportId` 字段存在于 `BuddahPredictedReconcileData`（:22, :26）但被硬写 0 并丢弃，说明确认门控曾被设计但未完成。Teleport（复活、结果区）走同一条一次性路径，存在同类风险。

## R4：投影不对称

owner 按 `LocalTick − StartTick`（≈ RTT − 2 tick）投影，服务器不投影。两边在交接瞬间相差 `速度 × (单程延迟 − 2 tick)`，服务器权威，必然产生一次校正。常量 2 tick 无法覆盖真实 RTT。

## R5：回放用现在的 LocalTick

`PredictionManager.ReconcileToStates` 只维护 `ClientReplayTick/ServerReplayTick`，不改 `TimeManager.LocalTick`。回放旧 tick 时 Inherit/Blend 推进、modifier 到期、teleport/handoff 的 `StartTick <= currentTick` 判断都按"现在"计算，与前向不一致。冲量通道已处理，其他事件没有。R1 修复后这会成为新的校正来源。

## R6：服务器侧远端身体在 GO 后 kinematic

服务器上，远端玩家的身体从 GO 到 ServerRpc 到达的几个 tick 内是 kinematic，而 `RunInputs` 仍对它施力。这段服务器快照本身不正确，恰好是 R3 第 4 步覆盖 owner 的那几帧。

## 未验证与边界

- 没有运行 Unity，没有新采集日志；数值来自代码推导和仓库既有记录。
- 用户回报（2026-10-01）：改动前 host 与纯 client 都有现象 1 和 2，与「host 自己的身体不会出现 R1/R3」的推断不符；EXP-1 后 host 单端测试中现象 1 消失、现象 2 不再拉回起点。host 本地 client 侧的本地状态回退在 Unity 物理模式下同样是真实回跳，详见 progress.md 的「新发现」。
- 技能命中正常，与分析一致：命中判定在物理根和服务器上，不依赖 VisualRoot。
