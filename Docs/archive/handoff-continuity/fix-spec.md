# 修复实现规格

只实现被 [experiments.md](experiments.md) 证实的条目。行号以 `dev` 7389ec8 为准。每个改动附 EditMode 测试（纯函数部分）或 `[HandoffFrame]` 日志证据（运行时部分）。

## §H 本地交接连续性（R7）

对应 EXP-H。只实现被子实验证实的条目。硬约束：开场 spline 动画的播放效果（路径、速度、时长、相机稳定模式）保持不变；DH1-b 若把 spline 驱动移进 motor，必须以同一 networkTime 采样同一 spline，视觉上不可区分。通用约束：刚体写入只在 Replicate 及其直接调用的消费函数内，不改 RPC 签名，`BuddahPredictedLaunchHandoffResolver` 保持纯函数；§H.0 探针与 `DriveSplinePose` 的 kinematic 速度写入是明确的例外（开场期间刚体 kinematic，由 intro 控制器单写入）。

### H.0 观察探针（EXP-H 前置，实验后移除或留在 `dumpReconcile` 门控）

- owner 的 `BuddahPredictionVisualRootBridge.LateUpdate` 末尾，GO 前后 ±0.5 s 内每帧输出一行 `[HandoffFrame] frame= tick= visualPos= rootPos= rbVel= camFov= camOffset=`，FOV 与偏移从 `PlayerCamera` 暴露只读属性取得。
- 统计脚本扩展 `summarize-reconcile-deltas.py`：按帧计算 VisualRoot 前向位移，输出零位移帧数、负位移帧数、FOV 单帧最大变化。

### H.1 相机速度源与开场速度（R7.1、R7.2 的速度部分）

- `RaceBodyIntroStateController.DriveSplinePose`（`:384`）：写完 `position/rotation` 后写 `targetRigidbody.velocity = snapshot.Velocity`、`angularVelocity = snapshot.AngularVelocity`。Unity 2022 对 kinematic 刚体允许设置，供相机、`SplineProgressTracker` 等读取，不参与积分。
- `PlayerCamera`（`:116-119`、`:390-398`）：退出 `ShouldUseStableIntroCamera` 时不调用 `ResetDynamicCameraOffsets`，改为从当前值继续 SmoothDamp；稳定模式期间也持续以 `rb.velocity` 更新 `_speedFieldOfView` 等内部状态但不应用，使切换当帧目标值与当前值一致。
- 可选：检查 `minSpeedFieldOfView/maxSpeedFieldOfView` 与 `baseFieldOfView` 的关系（当前 30/35 均低于 base 40，意味着进入动态模式必然缩 FOV）。
- 证据：`[HandoffFrame]` 的 FOV 与偏移曲线在 GO 处无台阶。

### H.2 死区与视觉参考系（R7.2、R7.3）

两种方案，由 DH1 决定。

- **DH1-a 最小方案**
  - `TrySampleVisualPoseAtRenderTime`（`RaceBodyIntroStateController.cs:119`）采样时间减去一个 `TickDelta`，使开场视觉滞后与交接后 owner 平滑器的 1 tick 一致。
  - 删除 `BuddahPredictionVisualRootBridge` 的 post-intro 视觉锁（`:454-497` 及其调用），`_goApplied` 后不再把 VisualRoot 吸到物理根，而是立即恢复平滑器。
  - host / 单机：快照按「GO 时刻到消费 tick」的实际差值投影（复用 `ProjectForArrivalTick`，差值由 GO 时记录的 `LocalTick` 算出），消除 1–2 帧停顿。
- **DH1-b 架构方案**
  - `BuddahPredictedMotor` 增加 Intro 态：Replicate 内每 tick 由 `SplineIntroPath` 与 networkTime 求位置/朝向并写刚体（kinematic），同时写 velocity；GO tick 直接迁移到 Inherit。`RaceBodyIntroStateController` 退化为时序与 spline 提供者，不再写刚体；`BuddahPredictionVisualRootBridge` 的平滑器压制、intro 视觉锁、post-intro 锁整体删除；FishNet 平滑器全程驱动 VisualRoot。
  - server 对远端身体同样在 Replicate 内走 Intro 态（消解 R6）；owner 不再发 ServerRpc，handoff 事件退化为「GO tick」（与联机分支的 D2-b 同向，合并时协调）。
  - 前提：联机分支的回放时钟修复（其 fix-spec §4）先合入，或在本分支同步实现。测试：`BuddahHandoffStep` shadow 增加 Intro 态对比。
- 证据：`[HandoffFrame]` 前向位移无零帧、无负帧；`[IntroSplineDiag]` 无 backApplied/snapApplied。

### H.3 角速度与高度（R7.4、R7.5）

- `ApplyLaunchInheritedVelocity`（`Events.cs:674-692`）：Inherit 期间角速度取 `_handoffState.SnapshotAngularVelocity`，Blend 期间按 alpha 向当前角速度过渡，而不是恒为零。
- 高度：若 EXP-H.5 测得落差 > 0.05 u，把 spline 末端采样高度对齐为静止高度（在 `SampleSnapshotAtTime` 对 y 做一次地面射线修正），或在消费时保留 `velocity.y = 0` 并调用 `Physics.SyncTransforms` 后再开碰撞。
- 测试：`BuddahPredictedLaunchHandoffResolver` 若新增角速度过渡函数，保持纯函数并加 EditMode 用例。

## 收尾

- 更新 `Docs/prediction-design.md`：开场 spline 与 motor 的写入者边界、视觉参考系规则。
- 删除 `[EXP-H.n]` 探针提交，或把 `[HandoffFrame]` 留在 `dumpReconcile` 门控下。
- PR 描述列出每个子实验的实测结论与未执行项；注明单机模式与联机分支的合入顺序。
