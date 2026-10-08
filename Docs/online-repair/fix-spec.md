# 修复实现规格

每一节对应 [findings.md](findings.md) 的一个根因和 [experiments.md](experiments.md) 的一项实验。只实现**被实验证实**的章节。行号以 `dev` 7389ec8 为准，实现前用 grep 复核。

通用约束：

- 刚体写入只允许在 `RunInputs`（Replicate）及其直接调用的消费函数内；新增代码不得在 Update/FixedUpdate/RPC 回调里直接写 `rb.*`。
- 不改 RPC 签名。`BuddahPredictedReconcileData` 允许改字段语义（已有字段），新增字段需确认 FishNet 代码生成后两端能往返（在 host 与纯 client 上各打印一次收到的值）。
- 每个改动附 EditMode 测试（纯函数部分）或日志证据（运行时部分）。
- 保持 `BuddahPredictedLaunchHandoffResolver`、`BuddahTickMath` 为纯函数。

---

## §0 观察探针（EXP-0 前置，实验结束后移除或留作 `dumpReconcile` 门控）

目的：拿到**每次** reconcile 的位置/速度校正量。现有 `[PredictionIntro][Reconcile]` 日志只在状态变化时输出（`BuddahPredictedMotor.DebugState.cs:38-56` 有去重），`dumpReconcile` 的 `reconcile tick=` 行没有 delta。

- 位置：`BuddahPredictedMotor.ReconcileState`（`BuddahPredictedMotor.cs:502`），在 `bootstrap.DebugState.lastReconcilePositionDelta` 赋值之后。
- 输出一行，`dumpReconcile` 门控：
  `[ReconcileDelta] tick={data.GetTick()} local={TimeManager.LocalTick} owner={IsOwner} server={IsServerInitialized} skipped={skipOwnerIntroReconcile} posDelta={…:0.000} velDelta={…:0.000} speed={data.PlanarSpeed:0.00}`
- 可选第二行（spectator 向后位移）：在 `TimeManager_OnPostTick` 中对非 owner 的 motor 输出 `[SpectatorStep] tick= forwardStep=`，其中 forwardStep 为本 tick 位移在 `transform.forward` 上的投影。
- 统计脚本：对 Player.log / Editor.log grep 上述前缀，按 owner/非 owner 分组算均值、p95、最大、负值占比。脚本放 `Tools/Validation/OnlineRepair/`（Python，不入 Assets）。

---

## §1 Physics Mode → TimeManager（R1）

- 改动：`Assets/Scenes/MainMenu.unity` NetworkManager 上 TimeManager 的 `_physicsMode` 由 0（Unity）改为 1（TimeManager）。在 Inspector 中改，保存场景，确认 diff 只有这一行（Unity 可能顺带重排序列化，无关改动用 `git restore -p` 丢弃）。
- FishNet 行为：TimeManager 会把 `Time.fixedDeltaTime` 设为 `TickDelta`（当前都是 1/60，无变化），把 `Physics.simulationMode` 设为 Script，并在每个 tick 与每个回放 tick 调 `Physics.Simulate`（`PredictionManager.cs:662,709-714`）。
- 需要复核的依赖 FixedUpdate / 自动物理的代码（FixedUpdate 仍会被调用，但物理只在 tick 推进）：
  - `RaceBodyIntroStateController.FixedUpdate`（`:100`）与 `DriveSplinePose`（`:384`）直接写 `rb.position/rotation`。开场阶段刚体 kinematic，写入仍会生效；确认 `[IntroSplineDiag]` 没有新增 backApplied/snapApplied。
  - `BuddahMovement.FixedUpdate`：预测模式下组件被 `BuddahPredictionLegacyIsolationBridge` 禁用，确认 `legacyMovementEnabled=false`。
  - 推手弹体 `HandPushProjectileRuntime` / `ChargedHandProjectileRuntime`（自写 `transform.position`，kinematic），`PushHitbox`，`BuddahRespawn.TeleportToWorldPose`，`PlayerProgressReporter.ApplyResultAreaTeleportLocally`。逐个在 Editor 跑一遍功能。
- 不变量断言（建议随本节加入，放 `BuddahPredictionRuntimeHealthReport` 或 `BuddahPredictedMotor.OnStartNetwork`，Editor/Development 下 `Debug.LogError`）：
  - `TimeManager.PhysicsMode == PhysicsMode.TimeManager`
  - `rb.interpolation == RigidbodyInterpolation.None`
  - `Mathf.Approximately(Time.fixedDeltaTime, (float)TimeManager.TickDelta)`
  - `NetworkObject.GetGraphicalObject() != null`
- 测试：无纯函数。证据为 EXP-1 的读数与上述功能复核清单。

---

## §2 handoff / teleport 的回放安全（R3）

两种方案，由 D1 决定。两者都要先完成 2.0。

### 2.0 公共部分：在 reconcile 中携带服务器的消费 id

- `CreateReconcile`（`BuddahPredictedMotor.cs:214`）：把 `data.LastConsumedHandoffId = 0u`（`:243`）和 `data.LastConsumedTeleportId = 0u`（`:240`）改为写入服务器真实值 `_lastConsumedLaunchHandoffEventId` / `_lastConsumedTeleportEventId`。
- `ReconcileState`：删除对应的 `_ = data.LastConsumed…` 丢弃行，改为读取。
- 两端打印一次收到的值确认序列化往返。

### 2.A 方案 A：服务器确认门控（改动小，对 RTT 不敏感）

语义：owner 本地已消费 id=N 的 handoff/teleport，而收到的服务器快照 `LastConsumed…Id < N`，说明该快照早于服务器消费，不能用它覆盖本地刚体与控制状态。

- `ShouldSkipTransformReconcileDuringIntroOrPendingHandoff`（`:636`）增加分支：
  `if (data.LastConsumedHandoffId < _lastConsumedLaunchHandoffEventId || data.LastConsumedTeleportId < _lastConsumedTeleportEventId) { reason = "server-predates-local-consume"; return true; }`
  仅 owner 路径（函数开头已对非 owner 返回 false）。
- 在同一窗口内，`ReconcileState` 还要跳过 `_handoffState = data.HandoffState`（`:532`）、`_introControlActive/_externalKinematicControlActive` 覆盖（`:540-541`）以及 `rb.isKinematic` 回写（`:553`）；`_modifierState` 可以照常接受。
- `RunInputs`（`:316`）：当上一次 reconcile 被该原因跳过且 `state.IsReplayed()` 为 true 时直接返回（不施力、不 Simulate），否则回放会在未被重置的现态上多推进 k 个 tick。用一个 `_reconcileSkippedForStaleServerState` 标志在 `ReconcileState` 设置、在下一次非回放 `RunInputs` 清除。
- 边界：
  - host owner：`IsServerInitialized` 时服务器与本地 id 同步变化，分支不会触发；仍要跑一次确认。
  - 服务器 id 回绕或重连：id 单调递增 uint，重连时 motor 重新实例化，不处理回绕。
  - 事件被 teleport 的 `ResetModifiers` 清掉（`ConsumePendingTeleportEvent` 把 `_hasPendingLaunchHandoffEvent=false`）：本地 `_lastConsumedLaunchHandoffEventId` 不变，服务器同样不消费，门控条件对称。
  - 门控窗口上限：若超过 `TickRate` 个 tick 服务器 id 仍未追上，记一条 Warning 并放弃门控，防止错误状态下永久不接受校正。
- 测试：把判定抽成纯函数 `BuddahTickMath.ServerSnapshotPredatesLocalConsume(serverHandoffId, localHandoffId, serverTeleportId, localTeleportId)`，EditMode 覆盖相等、落后、超前、零值。

### 2.B 方案 B：回放重放（更接近设计原则，改动大）

语义：已消费事件保留在历史里；reconcile 到消费 tick 之前的状态时，把事件重新入队，使回放在正确 tick 重新应用快照。

- 新增 `_consumedHandoffHistory` / `_consumedTeleportHistory`（环形，容量 ≥ `TickRate`），每项含事件数据与消费时的本地 tick。
- `ReconcileState`：若 `data.GetTick() < 某项消费 tick`，且 `data.LastConsumed…Id < 该事件 id`，则将该事件以 `StartTick = 消费 tick` 重新置为 pending。
- `ConsumePending…` 在回放中消费时不得再 `ProjectForArrivalTick`（已投影过），也不得重复 `ReportLocalGameplayLive`、`ResetActiveSkillEffectsForOwner`、`NotifyTeleportTrailRebases` 等一次性副作用：这些副作用需移到 `state.IsReplayed()==false` 分支。
- 前提：§4 必须先完成（回放中的 `currentTick` 必须是回放 tick，否则 `StartTick <= currentTick` 判断错误）。
- 测试：对 `BuddahHandoffStep` / `BuddahTeleportStep` 的 shadow 路径同步加入历史重放，`Tools/Validation/teleport-handoff-cases.cs.txt` 的位掩码用例扩展"重放"维度。

---

## §3 投影锚点统一（R4）与服务器 kinematic（R6）

### 3.1 投影（D2 决定）

- 现状：`TryApplyServerAuthoritativeLaunchHandoff`（`Events.cs:175`）服务器用 `serverStartTick = TimeManager.LocalTick`（`:195`）、不投影；owner 侧 `ConsumePendingLaunchHandoffEvent`（`:579`）用 `ProjectForArrivalTick`（`:593`）按 `LocalTick − StartTick` 投影，`StartTick = ownerTickAtRequest + 2`（`:196-197`，常量 `HandoffOwnerTickTravelBufferTicks` 在 `BuddahPredictedMotor.cs:32`）。
- 方案 D2-a（最小）：服务器也投影。服务器收到 ServerRpc 时用 `Owner.LocalTick`（`NetworkConnection.LocalTick`，EstimatedTick）或 `TimeManager.RoundTripTime/2` 估算 owner 从采样到服务器消费经过的秒数，调用同一个 `ProjectForArrivalTick` 把快照投影到服务器消费 tick。owner 的 `+2` 常量改为按估算 RTT 计算，或保留并接受 ≤2 tick 的差。
- 方案 D2-b（更干净）：服务器直接在自己的 GO tick 采样 spline（`RaceBodyIntroStateController` 在服务器上也在跑）并下发 handoff，owner 不再发 ServerRpc；TargetRpc 的 `startTick` 用 `Owner.LocalTick` 推算。`RaceBodyIntroStateController.CompleteGoTransition`（`:306`）里 owner 分支的 `BeginLaunchHandoff`（`:332`）改为仅进入 pending、不发请求。需要同时处理 `RoomStateManager.ReportLocalGameplayLive` 的触发时机。
- 测试：`ProjectForArrivalTick` 已是纯函数；对两端投影结果写一个一致性用例（同快照、同经过 tick → 同位置）。

### 3.2 服务器侧远端身体的 kinematic

- `CompleteGoTransition` 对 `!isLocalOwner` 的 `targetRigidbody.isKinematic = true`（`RaceBodyIntroStateController.cs:353`）在服务器上会使远端玩家的身体在 GO 到 handoff 消费之间保持 kinematic，而 `RunInputs` 仍在施力。
- 改动：在服务器上（`IsServerInitialized`）不设置 kinematic，由 motor 的 `_externalKinematicControlActive` 决定；或者让服务器在 GO 时就为远端身体进入 `authoritativePending`，走 `SimulateZeroVelocityPredictionStep`。纯 spectator client 保持 kinematic 不变。
- 证据：服务器日志不再出现对 kinematic 刚体施力的 Unity 警告；GO 后到消费前的服务器快照 `PlanarSpeed` 为 0 且位置不变。

---

## §4 回放时钟（R5）

- 现状：`RunInputs` 用 `currentTick = TimeManager.LocalTick`（`BuddahPredictedMotor.cs:342`）。FishNet 回放时不改 `LocalTick`（`PredictionManager.ReconcileToStates` 只维护 `ClientReplayTick/ServerReplayTick`）。
- 改动：`currentTick = state.IsReplayed() ? data.GetTick() : TimeManager.LocalTick`。owner 的 `data.GetTick()` 是客户端 tick；spectator 收到的 replicate tick 是服务器 tick，与本地 handoff/modifier 的本地时钟不一致，所以 spectator 路径使用 `PredictionManager.ClientReplayTick`（回放中）作为本地时钟。把选择逻辑抽成 `BuddahTickMath.ReplicateClock(isServer, isReplaying, isOwner, localTick, dataTick, clientReplayTick)`。
- 影响面：`RefreshLaunchState`、`ApplyLaunchHandoffInputScaling`、`ApplyLaunchInheritedVelocity`、`ConsumePendingTeleportEvent/HandoffEvent` 的 `StartTick <= currentTick`、`BuddahPredictedModifierResolver.Resolve`、`IsLocalPreHandoffBypassActive`。冲量通道已用 `ServerReplayTick`，不动。
- 测试：`BuddahTickMathTests` 增加 ReplicateClock 的 host / owner 前向 / owner 回放 / spectator 回放四个分支。
- 证据：100 ms 下 `[D-LOC HEARTBEAT]` 的 `hof-div`、`mod-div`、`tel-div` 为 0。

---

## §5 视觉参考系（R2）

- `SkillVfxReplicator.ConfigureAttachment`（`SkillVfxReplicator.cs:237`）把 VFX `SetParent(transform, false)`（`:249`），父节点是物理根。改为 `NetworkObject.GetGraphicalObject() ?? transform`。注意 VisualRoot 的 `localScale` 是 4，`SkillVfxAttachmentSettings.InheritTargetScale` 为 false 的 VFX 需要补偿缩放，或改用 `SetParent(graphical, worldPositionStays: true)` 后再设置本地偏移。
- `Buddah.prefab`：`沙砾` 嵌套预制体由根移到 `VisualRoot` 下（保持世界位姿）。`RollbackHurtbox` 留在根。
- 平滑参数（D3 决定）：`_adaptiveInterpolation`（`Buddah.prefab:1079`，当前 3=Low）→ Off 并保留 `_spectatorInterpolation: 2`；`_enableTeleport`（`:1082`）→ 1，`_teleportThreshold`（`:1083`）从 2 开始试。两处 `_enableTeleport`（`:1082`、`:1117`）属于不同组件，只改 NetworkObject 那一处。
- `BuddahPredictionVisualRootBridge` 的平滑器压制与"post-intro 视觉锁"在 §1/§2 修好后重新评估是否还需要；若 EXP-4.3 显示关闭更稳定，则删除该逻辑而不是继续加条件。
- 证据：`DebugState.motorVisualPosDelta` 与 VFX 锚点距离随速度的曲线；录像对比。

---

## 收尾

- 更新 [networking.md](../networking.md)："预测要求 TimeManager Physics Mode"写入常见问题表；[prediction-design.md](../prediction-design.md)：事件消费的回放安全规则、回放时钟选择表加入 handoff/teleport 行。
- 删除所有 `[EXP-n]` 探针提交（或把 `[ReconcileDelta]` 保留在 `dumpReconcile` 门控下）。
- PR 描述列出每节的实测数字与未执行项。
