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

## R7：交接瞬间的本地不连续（与网络无关，2026-10-01 补充）

EXP-1 之后用户在单端仍观察到交接瞬间“小卡一下”，并且单机模式也有同样现象。静态审查 host / 单机走的本地路径（`RequestAuthoritativeLaunchHandoffFromOwner` 在 `IsServerInitialized` 时直接 `TryApplyServerAuthoritativeLaunchHandoff`，`staleTicks=0`，不经过 reconcile），发现五处在 GO 瞬间同时发生的不连续，都来自“spline 驱动 → motor 驱动”的系统切换本身：

| 子项 | 位置 | 内容 | 量级（intro 60 u/s，RaceMap 设定） |
|---|---|---|---|
| R7.1 相机速度源 | `PlayerCamera.cs:116-156`、`:378-410`；`RaceBodyIntroStateController.cs:396` | 开场期间刚体 kinematic 且用 `rb.position` 直写，`rb.velocity` 全程为 0；相机在 `IsIntroActive` 期间走 `ApplyStableCameraState`（FOV=base 40、偏移归零），GO 当帧切回动态模式后以 `rb.velocity`（瞬间 0→60）为输入 | FOV 40→33.75 在 0.1 s 内完成（`speedZoomSmoothTime`），距离偏移 0→7.5，方向偏移 0→(15,0,15) 在 0.75 s 内完成。这是一次纯相机的“顿挫” |
| R7.2 死区 | `RaceBodyIntroStateController.cs:102`（`_goApplied` 后 FixedUpdate 不再驱动）；`BuddahPredictionVisualRootBridge.cs:267-268`、`:501-515`（`_goApplied` 后 `TrySampleVisualPoseAtRenderTime` 返回 false，退化为把 VisualRoot 吸到静止的物理根）；`BuddahPredictedMotor.Events.cs:195`、`:584` | GO 发生在 MonoBehaviour Update/FixedUpdate，FishNet tick 已在当帧更早执行（NetworkManager `DefaultExecutionOrder(short.MinValue)`），事件 `StartTick=LocalTick` 要到下一 tick 才消费；期间 spline 不再写、motor 还未写，身体与视觉都停住 | host / 单机约 1–2 帧（16–33 ms，≈1–2 u）；纯 client 为整个 RTT（这部分属 R3/R4） |
| R7.3 视觉参考系切换 | `BuddahPredictionVisualRootBridge.cs:192-224`（owner 开场期间压制 FishNet 平滑器，VisualRoot 直接按渲染时刻采样 spline，零延迟）；`:454-497`（退出后 4 帧内差距 ≥0.75 u 就把 VisualRoot 吸到物理根）；`Buddah.prefab:1077` `_ownerInterpolation: 1` | 交接后 VisualRoot 改由 FishNet owner 平滑器驱动，固定落后物理根 1 tick；加上 post-intro 锁的一次硬吸附，视觉在切换处先停、再吸、再以 1 tick 滞后跟随 | 1 tick ≈ 1 u 的一次性后退，叠加 R7.2 |
| R7.4 角速度清零 | `BuddahPredictedMotor.Events.cs:688`（Inherit/Blend 期间 `SetPredictionVelocitiesSafely(…, Vector3.zero)`）；`RaceBodyIntroStateController.cs:332` 传 `clearAngularVelocity=false` 但下一 tick 即被清零 | 若 spline 末端仍有曲率，航向角速度在交接瞬间从 ω 突变为 0，Inherit 12 tick + Blend 20 tick 内不恢复 | 取决于 spline 末端形状；直线末端则无 |
| R7.5 重力 / 碰撞体恢复 | `RaceBodyIntroStateController.cs:356`（GO 时恢复碰撞体）；`Events.cs:617`（消费时 `isKinematic=false`，`m_UseGravity: 1`） | 开场期间刚体悬停在 spline 高度且无碰撞；交接后同一 tick 开启重力与碰撞。若 spline 高度 ≠ 轮胎/底盘静止高度，会有一次下落或去穿透弹跳 | 需实测 snapshot.y 与消费后 10 tick 的 rb.y |

速度曲线本身是连续的：`m_Mass: 2`、`m_Drag: 0`、`forwardForce: 50`，Inherit 锁 60 u/s 12 tick，Blend 期间 velocity = Lerp(60, 当前) 且推力按 alpha 缩放，当前速度只会从 60 缓升（0.42 u/s 每 tick），不存在减速再加速。所以“卡一下”不是速度掉落，而是 R7.1 的相机顿挫 + R7.2/R7.3 的 1–3 帧位置停顿叠加。

单机模式同样出现，与此分析一致：单机走 host 路径，R7.1–R7.5 全部在本地发生，不依赖任何网络消息。纯 client 在此之上再叠加 R3/R4 的 RTT 死区与投影差。

### 修复方向（待决定，未实现）

1. **R7.1**：开场期间给 kinematic 刚体写入 spline 速度（`DriveSplinePose` 里 `rb.velocity = snapshot.Velocity`，Unity 2022 允许对 kinematic 刚体设置 velocity），或让 `PlayerCamera` 通过桥接读取“当前速度提供者”而不是 `rb.velocity`；同时让 `ShouldUseStableIntroCamera` 退出时不重置动态偏移，而是从当前值平滑。
2. **R7.2 + R7.3（架构性）**：把开场 spline 驱动移进 motor 的 Replicate 作为一个状态（Intro 态：每 tick `position = spline(t)`，与 Inherit/Blend/Normal 同一写入者、同一 tick 时钟）。切换变成同一 tick 内的状态迁移，没有死区；VisualRoot 全程由 FishNet 平滑器驱动，滞后恒定为 1 tick，post-intro 锁与平滑器压制逻辑可整体删除。spline 由 networkTime 决定，server 与 client 可各自确定性求值，也顺带消解 R6 并缩小 R3/R4 的窗口。
3. **最小替代**：保留现结构，但 (a) 渲染时刻 spline 采样改为 `renderTime − tickDelta`，与平滑器滞后一致；(b) 删除 post-intro 视觉锁；(c) owner/host 在 GO 当帧直接消费（`StartTick = LocalTick`，并在 `CompleteGoTransition` 后立即 `RunInputs` 前消费）或将 snapshot 按 GO 到消费的实际 tick 差投影。
4. **R7.4**：Inherit/Blend 期间保留快照角速度（或当前角速度）而不是归零。
5. **R7.5**：实测后决定是否把 spline 末端高度对齐静止高度，或消费时保留 y 速度为 0 并用 `Physics.SyncTransforms` 先落地。

验证口径：GO 前后各 0.5 s 逐帧记录 VisualRoot 位置、物理根位置、`rb.velocity`、相机 FOV 与跟随偏移；判定为逐帧前向位移无零帧、无负帧，FOV 与偏移曲线无台阶。
