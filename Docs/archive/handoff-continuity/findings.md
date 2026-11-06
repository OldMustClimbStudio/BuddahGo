# 开场交接切换的本地卡顿：观察与根因分析（2026-10-01）

来源：从 `fix/online-prediction-handoff` 分支拆出。联机分支的 findings.md 把开场交接后“被拉回起点”归因于网络（R3/R4），而把 TimeManager 物理模式修好之后残留的“切换瞬间小卡一下”在 host 与单机上同样出现，属于 spline 驱动到 motor 驱动的本地切换问题，独立成本分支处理。行号以 `dev` 7389ec8 为准（本分支基线），实现前用 grep 复核。

## 用户报告的现象

- 开场 spline 动画正常；GO 后从强制移动切到 motor 移动的瞬间有一次小卡顿，之后行驶正常。
- host、纯 client、单机模式都出现；联机分支修掉 reconcile 回跳后依旧存在。

## R7：交接瞬间的本地不连续（与网络无关）

在联机分支 `fix/online-prediction-handoff` 把 FishNet Physics Mode 改为 TimeManager（消除 reconcile 回跳）之后，用户在单端仍观察到交接瞬间“小卡一下”，并且单机模式也有同样现象。静态审查 host / 单机走的本地路径（`RequestAuthoritativeLaunchHandoffFromOwner` 在 `IsServerInitialized` 时直接 `TryApplyServerAuthoritativeLaunchHandoff`，`staleTicks=0`，不经过 reconcile），发现五处在 GO 瞬间同时发生的不连续，都来自“spline 驱动 → motor 驱动”的系统切换本身：

| 子项 | 位置 | 内容 | 量级（intro 60 u/s，RaceMap 设定） |
|---|---|---|---|
| R7.1 相机速度源 | `PlayerCamera.cs:116-156`、`:378-410`；`RaceBodyIntroStateController.cs:396` | 开场期间刚体 kinematic 且用 `rb.position` 直写，`rb.velocity` 全程为 0；相机在 `IsIntroActive` 期间走 `ApplyStableCameraState`（FOV=base 40、偏移归零），GO 当帧切回动态模式后以 `rb.velocity`（瞬间 0→60）为输入 | FOV 40→33.75 在 0.1 s 内完成（`speedZoomSmoothTime`），距离偏移 0→7.5，方向偏移 0→(15,0,15) 在 0.75 s 内完成。这是一次纯相机的“顿挫” |
| R7.2 死区 | `RaceBodyIntroStateController.cs:102`（`_goApplied` 后 FixedUpdate 不再驱动）；`BuddahPredictionVisualRootBridge.cs:267-268`、`:501-515`（`_goApplied` 后 `TrySampleVisualPoseAtRenderTime` 返回 false，退化为把 VisualRoot 吸到静止的物理根）；`BuddahPredictedMotor.Events.cs:195`、`:584` | GO 发生在 MonoBehaviour Update/FixedUpdate，FishNet tick 已在当帧更早执行（NetworkManager `DefaultExecutionOrder(short.MinValue)`），事件 `StartTick=LocalTick` 要到下一 tick 才消费；期间 spline 不再写、motor 还未写，身体与视觉都停住 | host / 单机约 1–2 帧（16–33 ms，≈1–2 u）；纯 client 为整个 RTT（这部分属 R3/R4） |
| R7.3 视觉参考系切换 | `BuddahPredictionVisualRootBridge.cs:192-224`（owner 开场期间压制 FishNet 平滑器，VisualRoot 直接按渲染时刻采样 spline，零延迟）；`:454-497`（退出后 4 帧内差距 ≥0.75 u 就把 VisualRoot 吸到物理根）；`Buddah.prefab:1077` `_ownerInterpolation: 1` | 交接后 VisualRoot 改由 FishNet owner 平滑器驱动，固定落后物理根 1 tick；加上 post-intro 锁的一次硬吸附，视觉在切换处先停、再吸、再以 1 tick 滞后跟随 | 1 tick ≈ 1 u 的一次性后退，叠加 R7.2 |
| R7.4 角速度清零 | `BuddahPredictedMotor.Events.cs:688`（Inherit/Blend 期间 `SetPredictionVelocitiesSafely(…, Vector3.zero)`）；`RaceBodyIntroStateController.cs:332` 传 `clearAngularVelocity=false` 但下一 tick 即被清零 | 若 spline 末端仍有曲率，航向角速度在交接瞬间从 ω 突变为 0，Inherit 12 tick + Blend 20 tick 内不恢复 | 取决于 spline 末端形状；直线末端则无 |
| R7.5 重力 / 碰撞体恢复 | `RaceBodyIntroStateController.cs:356`（GO 时恢复碰撞体）；`Events.cs:617`（消费时 `isKinematic=false`，`m_UseGravity: 1`） | 开场期间刚体悬停在 spline 高度且无碰撞；交接后同一 tick 开启重力与碰撞。若 spline 高度 ≠ 轮胎/底盘静止高度，会有一次下落或去穿透弹跳 | 需实测 snapshot.y 与消费后 10 tick 的 rb.y |

速度曲线本身是连续的：`m_Mass: 2`、`m_Drag: 0`、`forwardForce: 50`，Inherit 锁 60 u/s 12 tick，Blend 期间 velocity = Lerp(60, 当前) 且推力按 alpha 缩放，当前速度只会从 60 缓升（0.42 u/s 每 tick），不存在减速再加速。所以“卡一下”不是速度掉落，而是 R7.1 的相机顿挫 + R7.2/R7.3 的 1–3 帧位置停顿叠加。

单机模式同样出现，与此分析一致：单机走 host 路径，R7.1–R7.5 全部在本地发生，不依赖任何网络消息。纯 client 在此之上再叠加联机分支负责的 RTT 死区与投影差（该分支 findings.md 的 R3/R4），本分支不处理。

### 修复方向（待决定，未实现）

1. **R7.1**：开场期间给 kinematic 刚体写入 spline 速度（`DriveSplinePose` 里 `rb.velocity = snapshot.Velocity`，Unity 2022 允许对 kinematic 刚体设置 velocity），或让 `PlayerCamera` 通过桥接读取“当前速度提供者”而不是 `rb.velocity`；同时让 `ShouldUseStableIntroCamera` 退出时不重置动态偏移，而是从当前值平滑。
2. **R7.2 + R7.3（架构性）**：把开场 spline 驱动移进 motor 的 Replicate 作为一个状态（Intro 态：每 tick `position = spline(t)`，与 Inherit/Blend/Normal 同一写入者、同一 tick 时钟）。切换变成同一 tick 内的状态迁移，没有死区；VisualRoot 全程由 FishNet 平滑器驱动，滞后恒定为 1 tick，post-intro 锁与平滑器压制逻辑可整体删除。spline 由 networkTime 决定，server 与 client 可各自确定性求值，也顺带消解 R6 并缩小 R3/R4 的窗口。
3. **最小替代**：保留现结构，但 (a) 渲染时刻 spline 采样改为 `renderTime − tickDelta`，与平滑器滞后一致；(b) 删除 post-intro 视觉锁；(c) owner/host 在 GO 当帧直接消费（`StartTick = LocalTick`，并在 `CompleteGoTransition` 后立即 `RunInputs` 前消费）或将 snapshot 按 GO 到消费的实际 tick 差投影。
4. **R7.4**：Inherit/Blend 期间保留快照角速度（或当前角速度）而不是归零。
5. **R7.5**：实测后决定是否把 spline 末端高度对齐静止高度，或消费时保留 y 速度为 0 并用 `Physics.SyncTransforms` 先落地。

验证口径：GO 前后各 0.5 s 逐帧记录 VisualRoot 位置、物理根位置、`rb.velocity`、相机 FOV 与跟随偏移；判定为逐帧前向位移无零帧、无负帧，FOV 与偏移曲线无台阶。
