# 配表筛查清单：资产、Prefab 注册、角色外观、VFX、Feel、音频、场景列表、标签层、物理与输入资产

筛查日期 2026-10-12，基线 dev 9ff6338。本文件只做盘点，不含实现。总览与跨域结论见 [README.md](README.md)。

> 2026-10-26 玩法深化之后（代码 0dbc551）：`SkillSlot_1/2/3.prefab` 的 `SkillSlotUI.tokens`（W / ↑ 图）在运行时隐藏，已退役，不再与 `ComboSkillInput.bindings` 重复（AI 仍用 `ComboSkillInput`，直到 A7）。6 个 `Assets/Skills_Assets/Skills/Skill_*_Anti.asset` 已删，`Skill Database.asset` 只剩 6 个普通技能，另有 `yuanqis` 列表引用 `Assets/Skills_Assets/Yuanqi/` 下的 `Yuanqi_FrostLock`、`Yuanqi_GiantCharge`、`Yuanqi_GiantPalm` 三个资产。`InputSystem_Actions.inputactions` 的 Player map 新增 `SkillSlot1/2/3`（Q/E/R 与 1/2/3），HandPush 加了鼠标左 / 右键（W / ↑ 推掌保留但已废弃）。下文 ComboSkillInput 一段、重复项清单里关于 tokens 的说法、各 `_Anti` 资产的条目（如 `Skill_PushProjectileHands_Anti` 的蓄力 VFX 引用、`accel_anti_vfx` 等缺失 vfxId 的来源）和 Input_Bindings 一节的 Player map 按 2026-10-12 基线记录。

范围：`Assets/Character/**`、`SharedAssets/**`、`Placeholder/**`、`Sources/**`、`Material/**`、`VFX/**`、`Settings/**`、`Resources/**`、`DefaultPrefabObjects.asset`、`FishNet.Config.XML`、`ProjectSettings/`（TagManager、DynamicsManager、InputManager、EditorBuildSettings），以及本项目 prefab 上自有组件的序列化值。GUID 已通过 .meta 解析到具体文件。"代码默认 X / prefab Y" 表示 Inspector 覆盖了 C# 初始值，配表以 prefab 为准。本文件与 skills / movement / match-flow 清单有意重叠，用于从资产侧交叉核对。

## 结论先行

- 整个项目没有任何音频：无 AudioClip 文件、AudioSource、MMSoundManager 引用、MMF_Sound 反馈。
- 没有 per-racer 配色、调色板或材质变体。所有赛车手共用 `DefaultSkin.shadergraph` 子材质，唯一区分是本地玩家头顶 `LocalPlayerMarker`（青色）。
- `SkillVfxDatabase` 只有 2 条，技能资产引用 5 个 vfxId，3 个找不到 prefab。

## Prefab_Registry（网络 / 运行时 prefab 注册）

- `_prefabs[]` — `Assets/DefaultPrefabObjects.asset`（FishNet）— [0] `Assets/Character/MenuPlayer.prefab`，[1] `Assets/VFX/神足通/Acceleration粒子效果.prefab`，[2] `Assets/UIMenus/Lobby/RoomStateManager.prefab`，[3] `Assets/Character/Prefab/Buddah.prefab`（PrefabId 14）— FishNet 自动生成（`FishNet.Config.XML`: PrefabGenerator Enabled, SpawnableOnly, SearchScope 0）— 工程管理不进表。问题：MenuPlayer 只有 NetworkObject + EmptyNetworkBehaviour，场景与代码都不引用，疑似遗留；Acceleration粒子效果 带 NetworkObject，但 SkillVfxReplicator 本地实例化时关掉网络组件，属误注册。
- `_playerPrefab` — RaceMap.unity（MatchSpawnManager）— Buddah.prefab — 唯一赛车手 prefab，AI 也用它，靠禁用状态的 AIRacerDriver / AISkillCaster 切换。
- `_roomStateManagerPrefab` — MainMenu.unity（GameNetworkManager）— RoomStateManager.prefab。
- `uiPrefab` — RaceMap（RaceMapUIBootstrap）— `Assets/UI/UIprefap.prefab` — 场景里已摆一个实例，Bootstrap 经 `FindExistingUiInScene` 复用，两条来源。
- `pushHitboxPrefab` — Buddah.prefab（BuddahHandControl）— `Assets/Character/Prefab/Cube.prefab` — scale (3.54, 3.65, 10.47)，BoxCollider trigger size (1.64, 1, 1.416) center (-0.32, 0, 0.208)；组件只有 PushHitbox，无序列化字段。
- `projectilePrefab` — Buddah.prefab（BuddahHandControl）— null，未用。
- `triggerColliderPrefab` — `Assets/Skills_Assets/Skills/Skill_SlowTrap.asset` — `Assets/Character/SkillComponents/SlowTrapZone.prefab` — scale (15, 0.5, 15)，CapsuleCollider trigger r 0.5 h 2，有效半径约 7.5；带 MeshRenderer 但 materials 空；根节点 localPosition (355.07, 1.24, 144.79) 是脏数据。
- `chargedProjectileVfxPrefab` — Skill_PushProjectileHands(.asset / _Anti) — `Assets/VFX/如来神掌/如来神掌VFXGraph.prefab`（VFX Graph `如来神掌.vfx`）。
- `chargedProjectileLaunchEffectPrefab` — Skill_PushProjectileHands — `Assets/VFX/如来神掌/射击特效.prefab`。
- `optionItemPrefab` — PropertyGroupView.prefab — PropertyOptionItemView.prefab。`propertyGroupPrefab` — PropertySelection.unity（PropertiesSelectorUI）— PropertyGroupView.prefab。`wallItemPrefab` — PropertySelection（SkillWallSelectionController）— `技能绘马.prefab`。`playerEntryPrefab` — MainMenu（RoomUI）— RoomPlayerEntry.prefab。`listItemPrefab` — MainMenu（LobbyBrowserUI）— LobbyBrowserListItem.prefab。
- `vfxPrefabs[] / shaderVariantCollections[]` — RaceMap（VfxStartupWarmup）— 只有 slowtrap_vfxZone.prefab 与 `Assets/VFX/PreloadShader.shadervariants`；runOncePerApp 1、dontDestroyOnLoad 1（代码默认 false）、runOnAwake 1、keepAliveSeconds 0.1 — 预热列表未含 Acceleration粒子效果 与 如来神掌 系列。建议 VFX_Table 加 warmup 列。

## Racer_Tuning（Buddah.prefab 根节点，Layer 6 "Buddah"）

物理：Rigidbody mass 2、drag 0、angularDrag 0.05、useGravity 1、constraints 80、interpolate 0；CapsuleCollider r 3.887 h 8.493 center (-0.05, 2.95, -0.15) 物理材质 `Assets/Material/Character.physicMaterial`（0/0/0）；RollbackHurtbox 子物体 CapsuleCollider trigger r 3.891 h 8.470，FishNet NetworkCollider `_physicsType=1`、`_boundingBoxSize` (3,3,3)；`VisualRoot.localScale` (4,4,4)，Giant 在此基础上乘。

BuddahMovement（旧移动）prefab 值 / 代码默认：forwardForce 50/20、turnTorque 30/12、turnDecayPerSecond 3/8、maxSpeed 80/8、turnInputMultiplier 1/1（与 PredictedMotorConfig 的 2 冲突）、pushGraceSeconds 0.25、pushExtraMaxSpeed 6、launchInheritTime 0.2、launchBlendTime 0.32。孤儿字段 directHeadingDegreesPerSecond 360、directHeadingAngularDamping 14、directHeadingSnapAngle 2。

BuddahPredictedMotorConfig prefab：forwardForce 50、turnTorque 30、turnDecayPerSecond 3、maxSpeed 80、turnInputMultiplier 2（代码 1）、pushGraceSeconds 0.25、pushExtraMaxSpeed 6；handoffGroundSnapMaxDistance 1.5 与 handoffGroundProbeHeight 3 未序列化。`BuddahMovementModeSwitcher.runtimeMode=1` 决定用哪份。建议合成一行 Racer_Tuning。

BuddahHandControl（推手）prefab / 代码：rotationSpeedDegPerSec 180、pushWindupSeconds 0.1、pushLifetimeSeconds 0.3/0.12、pushForwardOffset 7/0.9、pushSideOffset 4/0.25、pushLeftSideBias -0.1/-0.05、pushRightSideBias 0.1/0.05、pushHeightOffset 1、pushImpulseStrength 100/6、pushCooldownSeconds 0.4/0.25、leftHandTriggerName/rightHandTriggerName "LeftW"/"RightUp"（Animator 参数）、projectileVisualLocalEuler (0,0,0)、projectileVisualScale (1,1,1)。Animator `Assets/Animation/BuddahAnim/PlayerBuddah_Animator.controller` 参数：RightUp、LeftW、IsAnti（trigger），Blend、IdlePlaybackSpeed（float）。

PlayerCamera prefab / 代码：directionalOffsetPerSpeed (0.5,0,0.4)/(0.25,0,0.25)、maxDirectionalOffset (15,0,15)/(4,0,4)、baseFieldOfView 40/60、zoomBySpeed 1、speedForMaxZoomOut 80/10、minSpeedFieldOfView 30/50、maxSpeedFieldOfView 35/72、maxSpeedDistanceOffset 10/12、speedZoomSmoothTime 0.1/0.2、adaptToPlayerScale 1、cameraDistanceScaleFactor 1、cameraFovPerExtraScale 0、offsetSmoothTime 0.75/0.1；stableIntroExitSmoothTime 0.6、stableIntroExitBlendDuration 1.2 未序列化。建议 Camera_Table。

ComboSkillInput：stepWindowSeconds 0.35、maxBuffer 6、useGeneratedInputActions 1；bindings（代码默认空列表，prefab 是唯一来源）Skill_1 slot0 W,Up,W；Skill_2 slot1 Up,W,Up；Skill_3 slot2 W,W,Up,Up。重复：`Assets/UI/UI预制键位/SkillSlot_1/2/3.prefab` 的 `SkillSlotUI.tokens`（Slot3 prefab 为空，靠 UIprefap 嵌套覆盖成 4 个）。

SkillLoadout：`_defaultSkillIds` [acceleration, push_projectile_hands, blackcurtain]；`_skillDatabase` → Skill Database.asset。重复：PropertiesSelectionManager._fallbackSkillIds 同样三个。注意配置仓库 MAIN 的 race_default 第 3 槽是 slowtrap（见 skills 第 13 节）。

ObsessionFigure prefab / 代码：maxValue 1000/100、minValue 0、completionGapToLeaderPercent 0、extraRecoveryPerSecond 0、drainPerSecond 10/1、catchupRecoveryA 0.01、initialValue 0、backfirePMaxPercent 95/75、backfireK 0.008/0.012、backfireMidpointX 500、backfireKeepPMaxAtOrAboveX 1000。

LapProgress prefab：startPointTag "StartingPoint"、nearEndThreshold01 0.85、nearStartThreshold01 0.2、minimumCrossingCooldownSeconds 0.5、minimumForwardWrapDelta01 0.5、maximumProgressJumpWithoutCheckpoint01 0.5、minForwardDot 0、minimumWorldDistanceFromStartToArmLap 25、useSequentialCheckpoints 1、autoAdvanceSplineCheckpoints 0（代码 true）、requiredCheckpointCount 3、fallbackCheckpointProgresses01 [0.25,0.5,0.75]。requiredCheckpointCount 应与场景 3 个 Checkpoint 一起进 Track 表。

RaceCompletionTracker（全部等于代码默认）：wrongWayWrapStartThreshold 0.2、wrongWayWrapEndThreshold 0.8、wrongWayForwardDotThreshold 0、wrongWayRecoveryLapProgressThreshold 0.05、wrongWayForceRespawnLapProgressThreshold 0.85、wrongWayRespawnLapProgress 0.01、wrongWayLap0RespawnLapProgress 0.99、startGraceSeconds 0.35。

SplineProgressTracker：jumpMetersThreshold 8、windowRadiusT 0.2（代码 0.1）、windowSteps 40、maxProjectionDistance 30、maxStepFactor 1.5（工程）。BuddahRespawn（全部等于代码默认）：respawnFloorTag "RespawnFloor"、continuousContactRespawnSeconds 2、postRespawnCooldownSeconds 1.5、respawnHeightOffset 3、respawnClearanceCheckRadius 0.75、additionalRespawnLiftStep 1.5、maxAdditionalRespawnLift 6、enableVerboseRespawnLogs 1。BuddahSpeedParticleEffect：playSpeedThreshold 30（代码 20）、stopSpeedThreshold 28（代码 18）、usePlanarSpeed 1，粒子指向嵌套 `沙砾.prefab`。RaceBodyIntroStateController：defaultHandoffLeadTime 0.5、velocitySampleDeltaSeconds 0.02；其余代码默认；与 IntroSequenceManager.handoffLeadSeconds 0.5 同义。PlayerProgressReporter reportIntervalSeconds 0.2（代码 0.1）。SkillVfxReplicator：vfxDatabase → SkillVfxDatabase.asset、prewarmOnStart 1、prewarmAliveSeconds 0.05、revealDelayFrames 2。LocalPlayerOverheadMarker：heightAboveVisualRoot 8.5、targetPixelHeight 32、minimumScale 1.25、maximumScale 8。

## Racer_Visuals（外观 / 拖尾 / 配色）

- 模型 `Assets/Character/佛头动画tpost.fbx`；SkinnedMeshRenderer 材质为 `Assets/Material/Skin/DefaultSkin.shadergraph` 内嵌材质，所有赛车手相同；无 per-index 颜色、材质或 prefab variant，`RacerIdentity` 只同步 DisplayName。
- 名字：AI 默认 "AI " + (index+1)，否则 `SoloMatchSettings.AINames[index]` 或 personality.DisplayName；GameObject 名 "AI Racer N"（MatchSpawnManager.cs:166）；玩家默认 "Player"。硬编码。
- 本地玩家标记 `Assets/Placeholder/UI/LocalPlayerMarker.prefab`（mesh LocalPlayerMarker_Arrow.asset），`LocalPlayerMarker_Cyan.mat` _BaseColor (0.05, 0.9, 0.95, 1)，`LocalPlayerMarker_Outline.mat` _BaseColor (0.025, 0.1, 0.13, 1) → Racer_Visuals.local_marker_color。
- 加速拖尾 PlayerAccelerationTrail（嵌套 `Assets/VFX/神足通/AccelerationTrail.prefab`）：fallbackWidth 1（代码 0.5）、fallbackColor (1, 0.7, 0, 1)、fallbackTime 0.6、fadeOutMultiplier 1；嵌套覆盖 m_Time 0.5（源 3）、widthMultiplier 10/15、m_Emitting 0。
- 一念光明拖尾 BlackCurtainTrail 子物体（Layer 7）：TrailRenderer 材质 `SG_BlackCurtainTrail.shadergraph`，widthMultiplier 5、m_Time 1、渐变 (1, 0.70, 0) → 白 alpha 0；`PlayerBlackCurtainTrail` fallbackColor cyan、fallbackWidth 0.5、fallbackTime 0.6、fadeOutMultiplier 1（代码默认）。fallbackColor（cyan）与实际颜色（橙）不一致。
- 速度粒子嵌套 `Assets/VFX/VisualEffectGraph/沙砾.prefab`，覆盖 burst count 3、SizeModule 曲线。
- 物理材质：Character（0/0/0）、Floor（dynamic 0.4，static 0.3，RaceMap）、AirWall（0/0/0，RaceMap）→ Physics_Materials 表。

## VFX_Table

`Assets/Skills_Assets/SkillVfxDatabase.asset`（只有 key → prefab，无时长）：

| vfxId | prefab | 粒子情况 | 时长来源 |
|---|---|---|---|
| accel_back_vfx | `Assets/VFX/神足通/Acceleration粒子效果.prefab` | 3 个 PS，duration 10 不循环，lifetime 0.3–0.5 | Skill_Acceleration：vfxDurationSeconds 0 → 回落 durationSeconds 10；stopBeforeEnd 1；offset (0,0,-2)；euler (0,180,0) |
| slowtrap_vfx | `Assets/VFX/禅定缓流/slowtrap_vfxZone.prefab` | 根 PS 循环 5 s；子 PS lifetime 最长 10 | Skill_SlowTrap：vfxDurationSeconds 11（zone 10） |

slowtrap_vfxZone 的 `SkillVfxAttachmentSettings`：followTargetPosition 1、inheritTargetRotation 0（代码 true）、inheritTargetScale 0。

缺失 vfxId（技能资产引用，数据库无条目，运行时 warning）：accel_anti_vfx（Skill_Acceleration_Anti）、reverse_turn_trap_vfx（Skill_ReverseTurn）、reverse_turn_trap_anti_vfx（Skill_ReverseTurn_Anti）。资产覆盖代码默认：Skill_SlowTrap 资产 slowtrap_vfx / 代码 "slow_trap_vfx"；Skill_SlowTrap_Anti 资产空 / 代码 "slow_trap_anti_vfx"。

建议 VFX_Table 列：vfx_id、prefab_path、default_duration、stop_before_end、local_offset、local_euler、follow_pos / inherit_rot / inherit_scale、warmup。目前 offset 与 duration 散在各 Skill_*.asset。

非数据库 VFX：`如来神掌VFXGraph.prefab`（VisualEffect `如来神掌.vfx`，属性 Progress）；`射击特效.prefab`（8 个 PS，根 PS 循环 5 s，子 PS lifetime 0.13–1）。

Feel 反馈（`Assets/VFX/FeelPrefab/*`，嵌套在 Buddah.prefab 的 FeelVFX / 技能释放 下），路由 `SkillFeelRouter.entries` → Feel_Events 表：

| eventId | feedbackPlayer | 内容 |
|---|---|---|
| acceleration_shared | null | 未接线 |
| Anti_Reflection_Shared | 反噬Reflection_Shared | MMF_VisualEffect |
| Anti_Reflection_Local | 反噬Reflection_Local | 源 prefab：CinemachineImpulse + WhiteBalance_URP；Buddah 追加 MMF_Bloom（RemapIntensityOne 100）与 MMF_Bloom_URP（5），InitialDelay 1，ShakeDuration 1 |
| Skill_Reflection_Local | 技能Reflection_Local | 源：Impulse（InitialDelay 1，Velocity 1,1,1，ImpulseSource=ImpluseSource.prefab），WhiteBalance_URP（RemapTemperatureOne 100），Bloom_URP（RemapIntensityOne 5，ShakeDuration 1）；Buddah 嵌套覆盖 RemapTemperatureOne -15、RemapIntensityOne 4，FeedbacksList 删减为 2 项 |
| Skill_Reflection_Shared | 技能Reflection_Shared | MMF_VisualEffect；TargetVisualEffect 被 Buddah 覆盖为 技能Particle 的 VisualEffect |

四个 Reflection eventId 硬编码在 `SkillExecutor.cs:53-56`；黑幕的 blackcurtain_observers / blackcurtain_anti_observers 在 router 无条目；Skill_Acceleration 的 observersFeelEventId 为空（代码默认 "acceleration_shared"），而 router 该项 player 也是 null。`Assets/Character/Prefab/ImpluseSource.prefab`（CinemachineImpulseSource）→ Feel_Events.camera_shake：shape 3、duration 1、sustain 0.2、decay 0.7、impactRadius 100、dissipation 100、defaultVelocity (100, -100, 100)、amplitudeGain 1；RaceMap 场景也放了一个实例。一念光明全屏效果材质为 `SG_BlackCurtain.shadergraph` 子材质。

## Audio_Table

范围内没有任何音频资产或引用：无 .wav/.mp3/.ogg，prefab 与场景无 AudioSource / AudioClip，`Assets/Scripts` 无 AudioClip / AudioSource / MMSoundManager，Feel 反馈无 MMF_Sound。Feel 自带 `MMSoundManagerAudioMixer.mixer` 未使用。建议 Audio_Table 作为新表预留，以 eventId 为键，与 Feel_Events 和技能 ID 对齐。

## HUD_UI 与 Lobby_Menu_UI（资产侧交叉核对）

HUD（UIprefap.prefab）：ObsessionUI 与 SkillUiSpriteLibrary、SkillSlotUI、LapAddLineUITrigger、MiniMapController 的值与 [inventory-ui.md](inventory-ui.md) 一致；补充 MiniMapController invertY 1、swapXY 1、clampToBounds 1，playerTag "Buddah" 不是合法 tag（TagManager 无此 tag），RaceMap 实例覆盖为空，enableDebugLogs 0（代码 true）。

Lobby/菜单：RoomStateManager.prefab `_pregameCountdownSeconds` 18（代码 15）、`_requireAllClientsReady` 1、场景名 PropertySelection / RaceMap / RaceMapEndField / MainMenu；SteamLobbyManager `_defaultMaxPlayers` 6、`_defaultLobbyName` "My Room"、`_maxSearchResults` 20；MainMenuUI allowEscapeBack 1、escapeFromHomeQuits 0；RoomUI.mainMenuSceneName "MainMenu"；PropertiesSelectionManager `_stageDurationSeconds` 60（代码 30）、`_sceneTransitionFadeDurationSeconds` 2（代码 1）、`_allowDuplicateSkillSelections` 0、`_resolvedMatchSceneName` "RaceMap"、`_preferTimelineForMatchTransition` 1；PropertyCarouselView、技能绘马（SkillWallItemView、SkillInspectableItem inspectRotationEuler (0,180,0)）、SkillWallSelectionController、PropertySelectionStageSceneController、SkillInspectController、SkillInspectPanelUI、PropertySelectionMatchTransitionTimeline 的值与 UI 清单一致。

## Track_Scenes（RaceMap.unity）

- Checkpoint Check1/2/3（checkpointId 1/2/3）：(-220, 0, -715)、(-593, 0, -650)、(-625.3, 0, -227.8)，BoxCollider trigger scale (1, 27.7, 67.6) → Track_Checkpoints（track_id、cp_id、pos、rot_y、size）。
- 出生点 MatchSpawnPoint ×6（SpawnP1–P6，y 3.3），顺序由 `MatchSpawnManager._spawnPoints` 决定：P1 (-346.81, -68.83)、P2 (-327.23, -72.67)、P3 (-308.77, -56.99)、P4 (-335.51, -64.78)、P5 (-315.93, -68.62)、P6 (-297.47, -52.94)；P1 与 P4 scale.y 7.1155 属脏数据。
- 结算区 EndFieldSpawnPoint ×6（spawnOrderIndex 0–5，y 2.6）；RaceResultAreaManager overflowOffset (2.5,0,0)、missingPlayerOffset (0,0,-3)。
- RaceFinishManager（LeaderBoardManager 物体）：lapsToFinish 3、postFirstFinishCountdownSeconds 15、resultSceneName "RaceMapEndField"（遗留）。LeaderboardManager refreshIntervalSeconds 0.1。ResultDecisionManager decisionDurationSeconds 60、nextScenePropertySelection "PropertySelection"、returnSceneMainMenu "MainMenu"。MatchResultPresentationCoordinator：personalFinishDissolveOutSeconds 0.75、forcedFinishDissolveOutSeconds 0.6、resultAreaDissolveInSeconds 0.75（代码 1）、allowMovement/allowSkills 1。ResultPresentationTimelineBridge：blackScreenStartDelaySeconds 0.4（代码 0）、blackScreenFullyCoveredDelaySeconds 1.2（1）、blackScreenEndDelaySeconds 2.1（3）、timelineFinishedDelaySeconds 2.8（3）、useFallbackTimelineDelays 0。
- IntroSequenceManager：layouts 1P–6P（4P/5P/6P 来自 `Assets/Placeholder/Intro/Layout4P/5P/6P.prefab`）、fallbackIntroDuration 3、introSpeedMetersPerSecond 60（代码 20）、handoffLeadSeconds 0.5、introLeadInSeconds 1、randomizeSlots 1、deterministicShuffleSeed 20260405（代码 20260404）。Layout*P.prefab 每个 slot IntroSlot visualStyle 等于代码默认；SplineIntroPath arcLengthSamples 2048（代码 128）、splineId Placeholder_<N>P_Slot<i>、lockTerminalPositionToSlot 1。TrackSplineRef samples 200（代码 400）。
- BlackCurtainViewController：defaultRendererIndex 0（代码 -1）、trackEdgeTag "TrackEdge"、trackEdgeFadeInDelay 0.5、outdoorVolumeObjectName "Volume"（代码 "Outdoor Volume"）、blackCurtainActiveVolumeObjectName "Volumn_Dark"（代码 "BlackCurtain Active Volume"，场景物体名拼写错误）；Volume profile `Assets/Scenes/RaceMap/Volume_Dark Profile.asset`。TrackEdgeVisibility：材质 SG_MapEdge，属性 _EdgeVisible 0/1。
- 场景标签：StartingPoint、RespawnFloor、MainLight 各 1，TrackEdge 2（嵌套实例 Layer 8）。

## Scene_Flow（`ProjectSettings/EditorBuildSettings.asset`）

Build 顺序：0 `Assets/Scenes/MainMenu.unity`、1 `PropertySelection.unity`、2 `RaceMap.unity` → Scene_Flow 表（scene_id、path、build_index）。场景名常量在 `Foundation/SceneNames.cs`，另以字符串出现在 RoomStateManager.prefab、RaceFinishManager、ResultDecisionManager、PropertiesSelectionManager、RoomUI，共约 5 处。`RaceMapEndField` 无 .unity 且不在 Build 列表，`Assets/Scenes/RaceMapEndField/` 只有 LightingData 与反射探针，但 RoomStateManager.prefab 与 RaceFinishManager 仍引用。`com.unity.input.settings.actions` 指向 `Assets/Scripts/InputSystem/InputSystem_Actions.inputactions`。

## Tags_Layers（`ProjectSettings/TagManager.asset`）

Tags：StartingPoint（LapProgress.startPointTag）、TrackEdge（BlackCurtainViewController.trackEdgeTag）、MainLight、ScreenCamera（SharedAssets）、CinemachineTarget、RespawnFloor（BuddahRespawn.respawnFloorTag）。Layers：6 Buddah、7 BlackCurtainTrail、8 TrackEdge、9 RollbackBoundingBox；"TrackEdge" 与 "RollbackBoundingBox" 在 `BuddahPredictedMotor.Events.cs:750` 用 GetMask 硬编码。Sorting layers 只有 Default。建议只读常量表，tag 字符串不交策划。`MiniMapController.playerTag="Buddah"` 不是合法 tag。

## Physics（DynamicsManager、TimeManager）

gravity (0, -9.81, 0)；`m_LayerCollisionMatrix` 全 ff（所有 layer 两两碰撞）；`m_SimulationMode` 2（Script，FishNet 预测驱动）；defaultSolverIterations 6、bounceThreshold 2、queriesHitTriggers 1；Fixed Timestep 0.01666667（60 Hz）；FishNet TimeManager `_tickRate` 60（MainMenu）。建议 Global_Physics 表但修改风险高，建议只读。

## Input_Bindings（`InputSystem_Actions.inputactions`）

Player map：Movement（Button）`<Keyboard>/a`、`/d`、`/space`；HandRotation leftArrow、rightArrow；HandPush w、upArrow。Menu map：MenuSelect `<Mouse>/leftButton`；MenuCancel `<Keyboard>/escape`；MenuInspect `<Mouse>/rightButton`、`<Keyboard>/f`。Control schemes Keyboard&Mouse、Gamepad、Touch、Joystick、XR，但无手柄绑定。代码绕过 action 的硬编码：`QuitConfirmDialog.cs:51` `Keyboard.current.escapeKey`，`SkillInspectController.cs:410` `Mouse.current.position`；Combo token 映射（W / Up）在 ComboSkillInput。`ProjectSettings/InputManager.asset` 是旧系统默认轴，activeInputHandler 2（Both），本项目代码不用。`Assets/SharedAssets/FirstPersonController/InputSystem/FirstPersonInputActions.inputactions` 是示例资源，排除。建议 Input_Bindings 表（map、action、binding_path、scheme）；运行时改键需导出 overrides JSON。

## Graphics（是否与策划相关待定）

QualitySettings 当前等级 2 = High Fidelity → `Assets/Settings/URP-HighFidelity.asset`（GraphicsSettings 默认管线）；Performant → URP-Performant；Balanced → URP-Balanced。`TryUltra URP.asset / TryUltra URP_Renderer.asset`、`MapLight.lighting`、`Material/Sky.mat` 无引用；`SampleSceneProfile.asset` 只有 MainMenu 用。

## Resources（全部文件）

`Assets/Resources/AI/Easy.asset`、`Normal.asset`、`Hard.asset`（AIDifficultyProfile，按路径加载）；`Assets/Resources/AI/SkillPersonalities.json`；`Assets/Resources/Config/ProjectConfigDatabase.json`。`Assets/Data/Config/ProjectConfigDatabase_Main.asset` 被 `Skill Database.asset._projectConfigDatabase` 引用；`Assets/Data/SkillConfig/ProjectConfigSample.json` 是样例。

## 重复与遗留来源

重复可调值：
1. BuddahMovement 与 BuddahPredictedMotorConfig 7 个运动参数各一份，turnInputMultiplier 1 对 2。
2. ComboSkillInput.bindings 与 SkillSlot_1/2/3 tokens（及 UIprefap 对 Slot3 覆盖）。
3. SkillLoadout._defaultSkillIds 与 PropertiesSelectionManager._fallbackSkillIds，加两边代码默认，同一默认组合 4 份；MAIN race_default 第 3 槽又不同。
4. RaceBodyIntroStateController.defaultHandoffLeadTime 与 IntroSequenceManager.handoffLeadSeconds。
5. 场景名字符串约 5 处，两处指向不存在的 RaceMapEndField。
6. 3 圈规则 `RaceRules.DefaultLapsToFinish` 与场景 lapsToFinish；检查点数 LapProgress.requiredCheckpointCount=3 与场景 3 个 Checkpoint。
7. 小地图世界边界代码默认与 prefab 各一份，是 RaceMap 专属数据。
8. Feel 参数被 Buddah.prefab 嵌套覆盖改写（技能Reflection_Local Temperature 100→-15、Bloom 5→4；反噬Reflection_Local 追加两个 Bloom）。改 FeelPrefab 源文件不会生效。

疑似遗留 / 未引用（场景、prefab、asset、timeline 无 GUID 引用，代码无按路径加载）：`Assets/Animation/CG/AnimationLap/冲线白色线条.prefab`；`Assets/Sources/地图/反转法线空气墙.prefab`；`Assets/UI/UI预制键位/FXJS_SkillUIRoot-1.prefab`；`Assets/VFX/FeelPrefab/Acceleration_Local.prefab`、`Acceleration_Shared.prefab`；`Assets/VFX/如来神掌/HandProjectile.prefab` 及只被它引用的 `如来神掌.prefab`；`Assets/VFX/禅定缓流/Decceleration_VFX.prefab`；`Assets/Character/MenuPlayer.prefab`（只在 DefaultPrefabObjects 注册）；`Assets/Settings/TryUltra URP*.asset`、`MapLight.lighting`、`Material/Sky.mat`；`Assets/Scenes/RaceMapEndField/`；`Assets/Scenes/Oasis/**`、`Terminal/**`、`Cockpit/**`、`Garden/**` 与 `Assets/SharedAssets/**`（Unity URP 示例；FPS_Controller_Prefab 与 RuntimeDataCanvas_Prefab 只被不在 Build 列表的 OasisScene 引用）。删除前须按 CONTRIBUTING 确认运行时加载引用。

孤儿序列化字段：Buddah.prefab BuddahMovement 上 directHeading* 三个。

配置断链：vfxId accel_anti_vfx、reverse_turn_trap_vfx、reverse_turn_trap_anti_vfx 无条目；Feel eventId blackcurtain_observers、blackcurtain_anti_observers 不在 router；acceleration_shared 的 player 为 null。

## 排除（工程向）

NetworkObject、NetworkTransform（禁用）、NetworkCollider、各种 `_componentIndexCache` 与组件互引；BuddahPredictionBootstrap 的 debugSettings 与整个 debugState 块；BuddahPredictionDebugOverlay（anchor 16,16；panel 760×1000；mirrorSummaryToConsole 1）；VisualJitterDiagnostic（logEveryNFrames 2）；各 Bridge 组件；BuddahMovementModeSwitcher.runtimeMode；场景 BuddahPredictionPushTargetBox ×4 与 BuddahPredictionImpulseDebugBox；MiniMapController 所有 debug* / test* 字段；所有 enableDebugLogs / enableVerbose*；SkillVfxReplicator prewarm；PlayerProgressReporter 与 LeaderboardManager 刷新间隔；SplineProgressTracker 投影参数；TrackSplineRef.samples；SplineIntroPath.arcLengthSamples；FishNet.Config.XML、DefaultPrefabObjects（自动生成）、URP 与 Quality 资产、DynamicsManager 求解器参数；旧 InputManager.asset；SharedAssets 示例。

## 待确认问题

1. BuddahMovement 与 BuddahPredictedMotorConfig 以哪个为准（turnInputMultiplier 1 还是 2）？表里是否只留一份，运行时同时下发？
2. 是否需要 per-racer 外观（颜色、皮肤）？目前无数据结构支持；`Assets/Sources/Skin_Subs` 三个 sbsar 材质看起来是候选皮肤但未接线。
3. RaceMapEndField 是否正式废弃？RoomStateManager 与 RaceFinishManager 字段是否删？
4. 缺失的 3 个 vfxId 与 2 个 Feel eventId，补资源还是从技能资产清掉？
5. VFX 时长、偏移、朝向是否迁到 VFX_Table，让技能只引用 vfx_id？
6. 小地图边界、Checkpoint、出生点都是 RaceMap 专属。多赛道时 Track_Scenes 是否改为 track_id 多表？由场景导出还是表驱动生成？
7. Feel 嵌套覆盖是否扁平化（每 eventId 独立 prefab，或在表里列关键参数）？
8. 音频从零建表：键是技能 ID 还是 Feel eventId？是否用 Feel 的 MMSoundManager？
9. RaceMap 同时有 UIprefap 实例与 RaceMapUIBootstrap，以哪个为准？
10. 是否增加手柄绑定？
