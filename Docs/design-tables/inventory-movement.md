# 配表筛查清单：预测运动、受击、状态效果、起跑交接、重生、圈数、执念、输入与全局设置

筛查日期 2026-10-12，基线 dev 9ff6338。本文件只做盘点，不含实现。总览与跨域结论见 [README.md](README.md)。

> 2026-10-26 玩法深化之后（代码 0dbc551，`PredictionProtocol.Version` 2），下列基线说法不再完整，原文保留：表 3 的命令枚举、叠加规则与 `ResetActiveSkillModifiers`（新增 Freeze / EndModifier / Burst / MaxSpeedMultiplier，技能重置还清 freeze 与 burst，不清反噬限速；带重置的传送也保留反噬限速），见表 3 末尾；表 2 的冲量来源多了 `BodyImpact = 5`（缘起 #4 撞击，只作日志标签）；表 7 的执念参数多了同招加价与反噬惩罚五列，见 [inventory-skills.md](inventory-skills.md) 第 10 节；表 10 的键位（技能键、鼠标瞄准与推掌），见表 10 末尾。

范围：`Assets/Scripts/New_Buddah/**`、`Foundation/`、`GlobalSettings/`、`InputSystem/`、`Debug/`、`GameData/`，Buddah.prefab 上的相关组件，`ProjectSettings/` 中的物理、时间、画质。路径相对 `Assets/Scripts/`；prefab 行号指 `Assets/Character/Prefab/Buddah.prefab`。

## 结论先行

1. 运行时实际生效的是 V2 预测马达。唯一角色预制体 `Assets/Character/Prefab/Buddah.prefab` 的 `BuddahMovementModeSwitcher.runtimeMode: 1`（PredictionV2，prefab:1177）；`BuddahPredictionLegacyIsolationBridge.ApplyMode` 把 legacy `BuddahMovement.enabled` 设为 false（`BuddahPredictionLegacyIsolationBridge.cs:20-24`）。移动数值以 `BuddahPredictedMotorConfig`（prefab:1339-1357）为准。场景里没有 Buddah 实例覆盖，AI 与玩家共用同一预制体，所以每角色数值只有一份。
2. Legacy `BuddahMovement` 虽被禁用，仍有两个字段生效：`launchInheritTime` / `launchBlendTime` 传给预测起跑交接（`BuddahMovement.cs:226-237`，调用方 `RaceBodyIntroStateController.cs:557-563`）。其余移动字段是过期副本，但 legacy 技能效果组件仍会读写它们。
3. 项目已有配表机制（`Config/**`），先例是 `ComboSkillInput.ApplyConfiguredGlobalRules` 用全局规则覆盖 prefab 字段（`ComboSkillInput.cs:157-164`）；6 个已定义但从未读取的键见 [inventory-skills.md](inventory-skills.md) 第 1 节。

## 表 1：BuddahMovementBase（角色基础移动，每角色一行）

数值来源 `BuddahPredictedMotorConfig`（`New_Buddah/Config/BuddahPredictedMotorConfig.cs`），实际值 prefab:1339-1357。

- `forwardForce` — Config.cs:9 — 50 / 50 — `BuddahPredictedModifierResolver.Resolve`(Resolver.cs:12) → `BuddahLocomotionStep.Compute`(LocomotionStep.cs:45) → `BuddahPredictedMotor.RunInputs`(Motor.cs:523-531, ForceMode.Force) — ForwardForce:float — 重复：legacy `BuddahMovement.forwardForce`（代码 20 / prefab 50，prefab:612）。
- `maxSpeed` — Config.cs:12 — 80 / 80 — Resolve(:13) → `ClampPlanarSpeed`(Motor.cs:492,736-750) — MaxSpeed:float — 只限制水平 xz 速度。重复 legacy（代码 8 / prefab 80）。`PlayerCamera.speedForMaxZoomOut: 80`（prefab:698）隐式耦合；AI 侧 TopSpeed 79 / clamp 80 / 80 m 窗口也耦合。
- `turnTorque` — Config.cs:10 — 30 / 30 — Resolve(:14) → Compute(:46-48) → AddTorque(Motor.cs:537-539) — TurnTorque:float — 重复 legacy（12 / 30）。
- `turnDecayPerSecond` — Config.cs:11 — 3 / 3 — 无转向输入时线性回正（Motor.cs:544-548，rad/s²）— TurnDecayPerSecond:float — 重复 legacy（8 / 3）。AI 也读（`AIRacerDriver.cs:140`）。
- `turnInputMultiplier` — Config.cs:13 — 代码 1 / prefab 2（生效 2）— `BuildReplicateData`(Motor.cs:331 玩家，:340 AI ISteeringOverride) — TurnInputMultiplier:float — legacy prefab 为 1（prefab:616），不一致；legacy `MovementInvertTurnInputEffect` 仍写 legacy 字段（:25,64,72）。
- 自动油门 `throttle = 1f` — Motor.cs:332 硬编码；`PlayerBuddahInputSource.GetThrottle()` 也返回 1（:29-32）— AutoThrottle:float 默认 1。
- 转向死区 0.001 — Motor.cs:537、LocomotionStep.cs:46 — 可选 SteeringDeadzone，偏工程。
- Rigidbody `m_Mass` — prefab:578 — 2 — `_baseMass`(Motor.cs:160)；`ApplyResolvedMassMultiplier` 每 tick 写 `rb.mass = base × ScaleMassMultiplier`(Events.cs:857-865) — Mass:float。
- Rigidbody `m_Drag / m_AngularDrag` — prefab:579-580 — 0 / 0.05 — PhysX；AI 运动模型也读（`AIRacerDriver.cs:139`，`BuddahMotionModel.cs:48-49`）— LinearDrag / AngularDrag:float。
- Rigidbody `m_UseGravity / m_Constraints / m_CollisionDetection / m_Interpolate` — prefab:592-596 — 1 / 80（冻结 X、Z 旋转）/ Discrete / None — Interpolate 必须 None（Motor.cs:245 校验），不交策划。
- 主体 CapsuleCollider — prefab:547-569 — radius 3.8869、height 8.4932、center (−0.052, 2.951, −0.151)、材质 `Character.physicMaterial` — ColliderRadius / ColliderHeight。命中盒 `RollbackHurtbox` 是近似副本（trigger，r 3.891 / h 8.470，prefab:231-253）。
- `Material/Character.physicMaterial` — dynamic 0 / static 0 / bounce 0 / Combine Average — 建议 PhysicsMaterials 表，每材质一行；同类 `Floor.physicMaterial`（0.4 / 0.3 / 0）、`AirWall.physicMaterial`（0 / 0 / 0）。
- 最大角速度 — `ProjectSettings/DynamicsManager.asset:39 m_DefaultMaxAngularSpeed` — 50 rad/s — prefab 未设 maxAngularVelocity，用项目默认，即转向角速度上限；AI 也读 — MaxAngularVelocity:float。

## 表 2：PushReaction（受击 / 推挤反应）

- `pushGraceSeconds` — Config.cs:14 — 0.25 / 0.25 — 每次吃到冲量触发，`ConsumeImpulseAuthoritativeEntry` → `ApplyPushGraceFromImpulse`(Events.cs:490,827-833) — GraceSeconds:float — 不区分冲量来源。重复 legacy（BuddahMovement.cs:29，回退路径第 3 层 :160）。
- `pushExtraMaxSpeed` — Config.cs:15 — 6 / 6 — 宽限期内临时提高限速（Motor.cs:492；shadow LocomotionStep.cs:62-63；AI `BuddahMotionModel.cs:45`）— ExtraMaxSpeed:float — 重复 legacy（6）。
- 冲量施加方式 — Events.cs:473-477 — ForceMode.Impulse，转向冲量阈值 0.001 — 冲量大小在技能侧（`PushHitbox._impulse`、`BuddahHandControl.pushImpulseStrength: 100` prefab:657、两个 projectile runtime 的 `_hitTurnTorqueImpulse`）— 建议按 `BuddahPredictedImpulseSourceType`（MeleePush / Projectile / ChargedProjectile，`BuddahPredictedImpulseSourceType.cs`）分行加倍率列；目前没有按来源区分的数据。

## 表 3：MovementModifierRules（状态效果规则，每种 modifier 一行）

技能数值在技能侧，经 `SkillExecutor.cs:451-516` → `OwnerMovementEffectApplier` → `BuddahPredictionSkillMovementBridge.TryApply*`(SkillMovementBridge.cs:404-422) 传入马达。马达侧接入点与常量：

- 类型枚举 `BuddahPredictedModifierCommandType` — ModifierCommandData.cs:3-13 — Acceleration / RootThenAcceleration / InvertTurn / Scale / PushGrace / SteeringSuppression / RoomBypass — 表主键 ModifierType。
- Acceleration — ValueA=extraForwardForce、ValueB=extraMaxSpeed、ValueC=duration（ModifierCommandData.cs:24-34）— 应用 Events.cs:367-372，结算 Resolver.cs:40-44（加法叠加）— 列 ExtraForwardForce / ExtraMaxSpeed / DurationSec。
- RootThenAcceleration（SlowTrap）— Events.cs:373-379，Resolver.cs:46-50、55-59 — 定身期 `FinalForwardForce = 0`、`FinalMaxSpeed = 0`，`CompleteStoppedPredictionStep` 把速度含 y 清零（Motor.cs:494-497,676-683）；加速期从定身结束算起（ValueA + ValueD）。
- InvertTurn（ReverseTurn）— Events.cs:380-383，Resolver.cs:52-53 — 硬编码 `FinalSteeringSign = -1`，无幅度参数；legacy 用 `-magnitude`（MovementInvertTurnInputEffect.cs:64），语义不一致。
- Scale（Giant）— Events.cs:384-390，Resolver.cs:30-38 — `FinalTurnTorque *= ScaleMultiplier`；`FinalForwardForce *= ScaleForwardForceMultiplier`；`mass *= ScaleMassMultiplier`(Events.cs:857-865)；maxSpeed 不随巨大化缩放。
- 三个倍率下限 0.1 — Events.cs:386-388、Resolver.cs:32-34、Events.cs:863、VisualRootBridge.cs:291,294 重复 4 次 — 建议全局列 MinScaleMultiplier。
- PushGrace / SteeringSuppression / RoomBypass — Events.cs:391-401 — 只有持续时间；SteeringSuppression 清零转向（Motor.cs:514-515）；RoomBypass 绕过房间移动闸门（Motor.cs:262-264,328-330）。
- 叠加规则 — Events.cs:365-402 — 持续取较大值续期（Math.Max），幅度后写覆盖 — 建议枚举列 StackPolicy；`ModifierCmd.StackPolicy` 已预留未接线（Payloads/ModifierCmd.cs:12-15）。
- 结算顺序 — Resolver.cs:37-63 — 先缩放、再加 Accel、再加 PostRootAccel、再取反、最后定身清零；结果 ≥0 — 规则，写进表说明。
- 持续时间换 tick — `BuddahTickMath.DurationToTicks`(TickMath.cs:80-83) — 向上取整，粒度 1/60 s。
- `ResetActiveSkillModifiers` — Events.cs:27-40 — 只重置 root / accel / scale / invert，不重置 pushGrace / suppress / roomBypass。

2026-10-26 玩法深化（PredictionProtocol v2）新增四条命令，`Movement/ModifierRule`（presentationOnly，只记录）各加一行：Freeze（RefreshMaxDuration，技能重置时清）、EndModifier（瞬时命令，把目标修饰的期限提前到当前 tick，只缩短不延长；马达接受 Acceleration、RootThenAcceleration、Scale、Freeze、Burst、MaxSpeedMultiplier 六种目标，只有缘起消耗神足通 / 金身时才经 `SkillExecutor.EndAccelerationForOwner / EndScaleForOwner` 发 Acceleration / Scale；加速通道只结束正幅度，别人放的减速保留）、Burst（新枚举值 `StackPolicy.Restart`：后到的命令同时覆盖期限与幅度，技能重置时清）、MaxSpeedMultiplier（反噬惩罚 ×0.85，期限取较大、倍率后写覆盖，技能重置与复活都不清）。`Global/__enums__` 的 `ModifierCommandType` 同时改成与 C# `BuddahPredictedModifierCommandType` 相同的编号（Acceleration=1 … RoomBypass=7，Freeze=8 … MaxSpeedMultiplier=11）；原来从 0 起编，而导入校验接受 value 整数、消费方把整数当 C# 枚举值，`1` 在表里是 RootThenAcceleration、在代码里是 Acceleration。

## 表 4：LaunchHandoff（起跑交接）

- `launchInheritTime` — legacy `BuddahMovement.cs:33` — 0.2 / 0.2（prefab:622）— `BeginLaunchHandoff` → `BuddahPredictionHandoffBridge.TryBeginLaunchHandoff` → `RequestAuthoritativeLaunchHandoffFromOwner` → `TryApplyServerAuthoritativeLaunchHandoff`(Events.cs:226) — InheritSeconds:float — 预测路径数值来自已禁用的 legacy 组件。
- `launchBlendTime` — `BuddahMovement.cs:34` — 0.32 / 0.32（prefab:623）— Events.cs:227；混合期油门与转向按 BlendAlpha 线性放大（Events.cs:800-803；HandoffResolver.cs:49-58）— BlendSeconds:float — 曲线硬编码线性。
- 继承期行为 — Events.cs:796-799,807-825 — 油门转向清零，水平速度在快照速度与当前速度间 Lerp，角速度清零 — 规则备注。
- `suppressTurnInputSeconds` — `RaceBodyIntroStateController.cs:560` 字面量 0.15；`BuddahMovement.cs:226` 参数默认 0.15 — SuppressSteering(Events.cs:688-689) — SuppressTurnSeconds:float — 两处重复硬编码。
- `bypassRoomStateSeconds` — 来自 `RaceBodyIntroStateController.defaultHandoffLeadTime`（0.5 / 0.5，prefab:1023）或 `_assignment.handoffLeadTime`(:710-713)；调用处下限 `Max(0.1,…)`(:559)；马达内下限 `Max(bypass, 0.5)`(Events.cs:173) — RoomBypassSeconds / MinPreHandoffBypassSeconds — 两层下限。
- `clearAngularVelocity` — RaceBodyIntro:561 false；BeginLaunchHandoff 参数默认 true — ClearAngularVelocity:bool — 默认不一致。
- `handoffGroundSnapMaxDistance` — Config.cs:19 — 代码 1.5，prefab YAML 无此字段（用代码默认）— `TrySnapHandoffPositionToGround`(Events.cs:737) — GroundSnapMaxDistance:float。
- `handoffGroundProbeHeight` — Config.cs:21 — 3，prefab 缺失 — Events.cs:738,747 — GroundProbeHeight:float。
- `MinimumSnapDistance` — `BuddahHandoffGroundSnap.cs:11` — 0.0005 — 偏工程。
- 地面探测排除层 — Events.cs:749-750 — 字符串 "TrackEdge"、"RollbackBoundingBox" 加自身层（Buddah=6）— 见表 9。
- `HandoffOwnerTickTravelBufferTicks` — Motor.cs:34 — 2 — 网络工程参数，排除。

## 表 5：RespawnRules（重生）

重生逻辑在 `Buddah/BuddahRespawn.cs`，预测模式下转到 `BuddahPredictionRespawnBridge`（BuddahRespawn.cs:159-168；运行时 AddComponent，:47-48）。

- `respawnFloorTag` — BuddahRespawn.cs:14 — "RespawnFloor"（prefab:941）— 见表 9。
- `continuousContactRespawnSeconds` — :15 — 2 / 2 — `OnCollisionStay`(:90) 持续接触多久触发 — ContactSeconds。
- `postRespawnCooldownSeconds` — :16 — 1.5 / 1.5 — CooldownSeconds。
- `respawnHeightOffset` — :19 — 3 / 3 — `TeleportToTrackProgress`(:154) — HeightOffset。
- `respawnClearanceCheckRadius / additionalRespawnLiftStep / maxAdditionalRespawnLift` — :20-22 — 0.75 / 1.5 / 6（prefab:945-947）— `ResolveSafeRespawnPosition`。
- 重生 teleport 标志 — `BuddahPredictionRespawnBridge.cs:295-301` — 全部硬编码 true（snapProgress、zeroLinear/AngularVelocity、resetModifiers、resetImpulseQueue、resetPushGrace、rebaseTrails）— 建议按 `BuddahPredictedTeleportSourceType`（DropRespawn / WrongWayCorrection / Manual）分行。
- 重生原因到来源类型映射 — BuddahRespawn.cs:281-289 — 按字符串 "wrong"、"fall"、"drop"、"respawn" — 偏工程。
- 逆行修正阈值在 `RaceCompletionTracker`（见 [inventory-match-flow.md](inventory-match-flow.md) 表 2）。

## 表 6：LapRules（圈数与检查点，GameData）

数值在 `GameData/LapProgress.cs`，实际值 prefab:738-758。

- `startPointTag` — :15 — "StartingPoint" — 见表 9。
- `nearEndThreshold01` 0.85、`nearStartThreshold01` 0.2、`minimumForwardWrapDelta01` 0.5 — :18-21 — 只用于 `isValidForwardWrap`(:211-216)，该变量算后从未使用，死代码。
- `minimumCrossingCooldownSeconds` — :20 — 0.5 — `OnTriggerEnter`(:105,120,135) — CrossCooldownSec。
- `maximumProgressJumpWithoutCheckpoint01` — :22 — 0.5 — `TryAdvanceSplineCheckpoints`(:342)。
- `minForwardDot` — :23 — 0 — :108,216,339。
- `minimumWorldDistanceFromStartToArmLap` — :24 — 25 — :199 — ArmDistance。
- `useSequentialCheckpoints` — :27 — true / true。
- `autoAdvanceSplineCheckpoints` — :28 — 代码 true / prefab 0 — :336。
- `requiredCheckpointCount` — :29 — 3 / 3 — 必须与场景 checkpoint 数一致，但挂在角色 prefab 上，不是按赛道配置。
- `fallbackCheckpointProgresses01` — :30 — [0.25, 0.5, 0.75] — 适合每张地图一张检查点子表。
- `RaceRules.DefaultLapsToFinish` — `Foundation/RaceRules.cs:3` — 3 — 兜底，被 LapProgress.cs:69、PlayerProgressReporter.cs:229、RaceCompletionTracker.cs:11、RaceFinishManager.cs:16 使用 — 与未读取的全局规则键 race_lap_count 重复 — LapsToFinish:int。

## 表 7：ObsessionRules（执念值，GameData）

`GameData/ObsessionFigure.cs`，实际值 prefab:774-784，prefab 与代码默认差异大，以 prefab 为准。完整字段表见 [inventory-skills.md](inventory-skills.md) 第 10 节。要点：maxValue 100/1000；drainPerSecond 1/10（字段名叫 drain，实际是每秒下降量，与键 obsession_recovery_baseline 重复）；catchupRecoveryA 0.01（R = a·d²，与键 catchup_recovery_a 重复）；backfirePMaxPercent 75/95；backfireK 0.012/0.008；midpoint 500；keepPMaxAtOrAbove 1000。全部建议进 ObsessionRules（全局一行）。

## 表 8：TrackSampling（GameData）

- `TrackSplineRef.samples` — `TrackSplineRef.cs:10` — 代码 400 / `RaceMap.unity:8887` 场景 200 — `BuildArcLengthTable` — 偏工程或按地图配置。
- 二分迭代 18 — :88 — 工程。

## 表 9：LayersTags（名称常量，只读表或代码生成，不给策划改）

- Tags（`ProjectSettings/TagManager.asset`）：StartingPoint、TrackEdge、MainLight、ScreenCamera、CinemachineTarget、RespawnFloor。
- Layers：6=Buddah、7=BlackCurtainTrail、8=TrackEdge、9=RollbackBoundingBox。
- 代码硬编码字符串：`LayerMask.GetMask("TrackEdge","RollbackBoundingBox")`(Events.cs:750)；"StartingPoint"(LapProgress.cs:15)；"RespawnFloor"(BuddahRespawn.cs:14)；"TrackEdge"(BlackCurtainViewController.cs:23)；`playerTag = "Buddah"`(MiniMapController.cs:25，"Buddah" 是 layer 名不是 tag，疑似 bug)；`SceneNames.MainMenu / PropertySelection / RaceMap`(Foundation/SceneNames.cs:3-5)。

## 表 10：InputBindings（InputSystem）

- `InputSystem/InputSystem_Actions.inputactions`（`.cs` 自动生成）：Player/Movement A、D、Space (:37-69)；Player/HandRotation ←、→ (:70-91)；Player/HandPush W、↑ (:92-113)；Menu：MenuSelect 左键、MenuCancel Esc、MenuInspect 右键或 F (:148-193)。
- 代码绕过绑定：`PlayerBuddahInputSource.GetSteering` 硬编码 Key.A → -1、Key.D → +1，只认数字量（:45-48）；Space 虽绑定但不起作用，改键不会生效。同类硬编码 `BuddahHandControl.cs:412-452`（LeftArrow / RightArrow / W / UpArrow）、`ComboSkillInput.cs:123-124`（W / UpArrow）。
- 控制方案声明了 Gamepad / Touch / Joystick / XR 但无绑定；D 键 group 写成 ";Keyboard&Mouse;XR"，疑似误配。
- 消费方 `BuddahPredictionOwnerInputBridge.Initialize/ReadSteering`(OwnerInputBridge.cs:24,191-197)，受 `LocalInputBlock.IsBlocked` 阻断。
- 建议表 InputBindings（Action、Key、Scheme），外加 SteeringInputMode（数字/模拟）、SteeringDeadzone；改键需先去掉硬编码 KeyCode。
- 2026-10-26（玩法深化 A2 的键位，阶段 B 记进表）：`InputSystem_Actions.inputactions` 新增 `SkillSlot1/2/3` 三个动作与 HandPush 的鼠标左 / 右键绑定；`Global/InputBinding` 加 doc 列 `status`，补齐 SkillSlot1/2/3（Q/E/R 与 1/2/3 同时绑定，`SkillExecutor.OnSkillKeyPressed`）和 HandPush 的鼠标左/右键（主键位，`BuddahHandControl.TryResolvePushHand`）；W/↑ 推掌标为“废弃内容，后续可能再调整”；←/→ 只在没有鼠标时做回退，主瞄准是光标投到地面的方向，转速上限 `Push.basic.rotationSpeedDegPerSec`。
- 2026-10-30（玩法深化 A8，前进改为按住 W）：`Movement` 动作加 `<Keyboard>/w`，`HandPush` 删掉 W、↑；`Global/InputBinding` 加 W 前进一行（主键位）、删掉 W/↑ 推掌两行；`Movement.autoThrottle`（doc 列）注释改成“前进不再自动”，值仍为 1。同日起跑交接改为完全物理：`Movement.launchInheritSeconds` 0.2 → 0、`launchBlendSeconds` 0.32 → 0，doc 列 `launchSuppressTurnSeconds` 0.15 → 0（`RaceBodyIntroStateController.LaunchSuppressTurnSeconds`）。下文 0.2 / 0.32 / 0.15 是 2026-10-12 基线。

## 表 11：GlobalSimulation / GlobalSettings

- `FrameRateLock.TargetFrameRate` — `GlobalSettings/FrameRateLock.cs:9` — 120，强制 `vSyncCount = 0`(:14-15)，RuntimeInitializeOnLoad — TargetFrameRate:int — 必须是 60 的整数倍（与 tick rate 耦合）。
- FishNet `_tickRate` — `Scenes/MainMenu.unity:6412` — 60；`_maximumFrameTicks` 3、`_allowTickDropping` 1、`_physicsMode` 1 — 工程参数。
- Fixed Timestep — `ProjectSettings/TimeManager.asset` — 0.01666667 — 必须等于 TickDelta（Motor.cs:247 校验）— 与 tickRate 重复定义同一值。
- 物理全局（DynamicsManager.asset）：Gravity (0, −9.81, 0)、BounceThreshold 2、SleepThreshold 0.005、ContactOffset 0.01、SolverIterations 6 / 1、SimulationMode 2 (Script)、碰撞矩阵全开 — 建议 GlobalPhysics 表：Gravity、BounceThreshold、MaxAngularSpeed 给策划，其余排除。无每角色重力缩放。
- 画质（QualitySettings.asset）：Performant / Balanced / High Fidelity；Standalone 默认 2，WebGL 1，Server 0；各档 vSync 被 FrameRateLock 覆盖为 0；URP 资源 `Assets/Settings/URP-*.asset` — 偏工程或美术。
- NetworkObject 预测与平滑（prefab:1076-1089）：`_enablePrediction` 1、`_predictionType` 1、`_ownerInterpolation` 1、`_adaptiveInterpolation` 3、`_spectatorInterpolation` 2、`_enableTeleport` 0、`_teleportThreshold` 1 — 偏工程，可做只读网络表。
- `SoloPresentationTimeline`：延迟 = Time.fixedDeltaTime，容量 64（VisualRootBridge.cs:69；SoloPresentationTimeline.cs:23）— 工程。
- `Assets/Settings`、`Assets/Sources` 下无移动或物理相关 SO，只有 URP 与 Substance。

## 重复与遗留来源

1. legacy `BuddahMovement`（`Buddah/BuddahMovement.cs:22-34`，prefab:612-623）与 `BuddahPredictedMotorConfig` 重复 forwardForce、turnTorque、turnDecay、maxSpeed、turnInputMultiplier、pushGrace、pushExtraMaxSpeed。预测模式下组件禁用，FixedUpdate 不跑；但仍被 `PlayerRegistry` 当作玩家索引（Foundation/PlayerRegistry.cs）。`turnInputMultiplier` 两处不一致（1 对 2）。prefab 残留代码已删字段：`directHeadingDegreesPerSecond 360`、`directHeadingAngularDamping 14`、`directHeadingSnapAngle 2`（prefab:617-619）。
2. legacy 仍生效：`launchInheritTime` / `launchBlendTime` 只在 legacy 组件上，却驱动预测交接。
3. legacy 技能效果组件直接写 legacy 字段：`MovementAccelerationEffect.cs:33-87`、`MovementRootThenAccelerationEffect.cs:38-131`、`PlayerScaleEffect.cs:39-160`、`MovementInvertTurnInputEffect.cs:25-72`。只在 `OwnerMovementEffectApplier` 判断预测不可用时走；同一技能两套效果实现，应以 `BuddahPredictionSkillMovementBridge` 为准。
4. legacy 冲量回退第 3 层：`BuddahPredictionRouter.cs:65-71` 可能调 `BuddahMovement.ApplyPushImpulseAndTorqueTargetRpc`，用 legacy pushGraceSeconds。
5. AI 复刻马达公式：`AI/BuddahMotionModel.cs` 重写限速、阻尼、推力，参数来自 `_config` 与 Rigidbody。改马达规则时 AI 模型必须同步。
6. shadow 路径复刻：`BuddahLocomotionStep.Run` 与 `BuddahPredictedMotor.Shadow.cs:73` 读同组 config，`BUDDAH_PREDICTION_SHADOW` 宏下编译，诊断用。
7. 未读取的全局规则键：见 skills 第 1 节；`combo_input_window_seconds` 与 `ComboSkillInput.stepWindowSeconds 0.35`（prefab:801）重复，运行时以表为准。
8. 同一数值两处定义：`RaceRules.DefaultLapsToFinish = 3` 与 `RaceFinishManager.lapsToFinish`、`RaceCompletionTracker` 私有常量；`TrackSplineRef.samples` 代码 400 / 场景 200；`ObsessionFigure` 与 `LapProgress.autoAdvanceSplineCheckpoints` 代码默认与 prefab 不一致。
9. 死代码与未用通道：`LapProgress.isValidForwardWrap`（:211-216）及三个字段无效；CommandBus 的 Teleport / Modifier / Handoff 通道及 `Target_Enqueue*` 在 New_Buddah 以外无调用者，`ModifierCmd.Kind / Magnitude / StackPolicy` 未接线；`BuddahPredictionEventChannel.TryDequeue` 已 Obsolete；`BuddahPredictionDebugSettings.enableOnScreenDebug` 已弃用；`BuddahPredictionDebugOverlay` prefab 残留 `anchor`、`panelSize`（prefab:1390-1391）；`BuddahPredictionBootstrap.debugState` 整块运行时状态被序列化进 prefab（prefab:1217-1338）。

## 排除区（工程参数，不给策划）

- 调试开关：`BuddahPredictionDebugSettings` enableVerboseLogs / dumpReplicate / dumpReconcile / dumpGateState（prefab:1204-1209 全 0）；`BuddahPredictionCameraBridge.forceDefaultFollowTargetForDiagnosis`（0）；`BuddahPredictionVisualRootBridge.logHandoffFrames`（false）、`logHandoffFramesAfterGo`（45，prefab 缺失）；`BuddahPredictionDebugOverlay.mirrorSummaryToConsole`（1）、`consoleMirrorIntervalSeconds`（0.5）；`BuddahPredictionRuntimeHealthReport.HealthLogIntervalSeconds`（0.5）；PerfProbe / VisualShakeProbe `_heartbeatFrames`（60）、`_logPrefix`；`VisualJitterDiagnostic` diagnosticsEnabled 默认 false，logEveryNFrames 代码 3 / prefab 2，heartbeatEveryNFrames 120；`BuddahRespawn.enableVerboseRespawnLogs`（1）。
- RaceMap 调试物体：`BuddahPredictionImpulseDebugBox` impulse (0,0,50)、turnTorque 10、triggerOnce 1（RaceMap.unity:11510；代码默认 (0,0,16) / 8）；3 个 `BuddahPredictionPushTargetBox` linearDamping 0.35、angularDamping 0.5（RaceMap.unity:3369 / 10235 / 15756）。
- 视觉根平滑：`stabilizeOwnerVisualRootInPrediction`（代码 true / prefab 0）、`lockVisualRootDuringIntroAndPresentation`（1）（prefab:1443-1444）。
- 网络与预测内部常量：HandoffOwnerTickTravelBufferTicks 2；DefaultChannelCapacity 64、MaxRecentIds 64（CommandBus.cs:27；EventChannel.cs:237-238）；A4ProbeLogicalId；s_handoffGroundHits 8；各处 epsilon 与 Shadow 比对容差；MinimumSnapDistance 0.0005。
- 运行模式 `runtimeMode`（Legacy / PredictionV2）属架构开关。
- 固定约束：Rigidbody Interpolate=None、TimeManager PhysicsMode、fixedDeltaTime == TickDelta（Motor.cs:236-251）。

## 待确认问题

1. `launchInheritTime` / `launchBlendTime` 迁移后归属：`BuddahPredictedMotorConfig`、新 LaunchHandoff 表，还是随地图的 `IntroAssignmentData`（已有 handoffLeadTime）？
2. 移动参数全局一行，还是按角色 / 皮肤 / AI 难度分行？目前只有一个预制体。
3. 新表沿用 `ProjectConfigDatabase` 的 GlobalRule 字符串键值方式（`TryGetFloat` 覆盖 prefab 字段），还是强类型表？6 个空键接上还是删除？
4. InvertTurn 是否支持幅度（legacy 用 `-magnitude`，V2 固定 −1）？
5. Giant 是否也应提高 maxSpeed？推冲量是否按来源（近战 / 投射物 / 蓄力）设不同 pushGrace？
6. 改键需求是否成立？若成立，先去掉 `PlayerBuddahInputSource`、`BuddahHandControl`、`ComboSkillInput` 的硬编码 KeyCode。
7. `MiniMapController.playerTag = "Buddah"` 用 layer 名而非 tag，是否 bug？
8. `FrameRateLock` 120 是否开放为玩家设置？若开放，须限制为 tickRate 整数倍。
9. prefab 缺失 `handoffGroundSnapMaxDistance` / `handoffGroundProbeHeight`，靠代码默认生效；迁表以代码默认为基准是否可接受？
10. legacy `BuddahMovement` 与 4 个 legacy 效果组件何时删除？`PlayerRegistry` 仍依赖 `BuddahMovement` 作玩家索引。
