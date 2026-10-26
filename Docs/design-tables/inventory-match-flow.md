# 配表筛查清单：比赛规则、赛道、大厅房间、流程时长、属性选择、开场、身份与文案

筛查日期 2026-10-12，基线 dev 9ff6338。本文件只做盘点，不含实现。总览与跨域结论见 [README.md](README.md)。

范围：`Assets/Scripts/Match/**`、`Assets/Scripts/Network/**`、`Assets/Scripts/RaceIntro/**`，三个场景 `MainMenu.unity`、`PropertySelection.unity`、`RaceMap.unity` 中本项目组件的序列化值。路径相对 `Assets/Scripts/`。

## 结论先行

- 代码默认值与场景或 prefab 序列化值大量不一致，运行时以序列化值为准。差异较大的：`RoomStateManager._pregameCountdownSeconds` 代码 15 / prefab 18；`PropertiesSelectionManager._stageDurationSeconds` 30 / 60；`IntroSequenceManager.introSpeedMetersPerSecond` 20 / 60；`ResultPresentationTimelineBridge` 延迟 0/1/3/3 对 0.4/1.2/2.1/2.8；`PlayerProgressReporter.reportIntervalSeconds` 0.1 / 0.2；`SplineIntroPath.arcLengthSamples` 128 / 2048。
- "最多 6 人"至少写在 9 处：`RacerId.MaxAI=5`、Lobby `Range(2,6)` 与两处 `Clamp(2,6)`、`PendingMaxPlayers=6`、`_defaultMaxPlayers=6`、FishNet `_maximumClients=6`、6 个 MatchSpawnPoint、6 个 result anchor、IntroLayout 1–6P、Solo 文案「玩家 + 5 AI」、`AISkillCatalog` 校验 `Personalities.Length==MaxAI`。
- 地图投票和皮肤选择是死数据：结算后写进 `ResolvedPropertySelectionCache`，但 `TryGetSelection` 没有调用方；比赛场景永远是 RaceMap。
- 没有 TrackDefinition 实体：赛道就是 RaceMap.unity。起跑点、checkpoint、结果区锚点、intro 布局是场景 Transform；圈数挂在场景 `RaceFinishManager`；checkpoint 规则挂在角色 prefab 的 `LapProgress`。
- 范围内没有按玩家序号分配颜色或材质的代码。
- 已有配表管线（`Config/`，`SelectionRuleRepository` 按 RuleSet / ModeTag / MapTag 查询），新表可沿用。

## 表 1：MatchModeRules（按模式：Online / Solo / Practice）

接口 `Match/Contracts/IMatchRules.cs:9-24`；实现 `OnlineMatchRules.cs:5-20`、`SoloMatchRules.cs:13-28`，全部硬编码 `=>` 常量。Practice 不是独立实现，是 Solo 在 `AICount==0` 时的情况。每行一个 ModeId。

| 字段 | 位置 (Online / Solo) | Online | Solo | 消费方 | 备注 |
|---|---|---|---|---|---|
| IsSolo | :5 / :13 | false | true | 多处 | 结构开关，不给策划 |
| AutoStartRoom | :6 / :14 | false | true | `RoomStateManager.OnStartClient`(163) / `TryStartSoloMatchServer`(622) | |
| RequiresReady | :7 / :15 | true | false | 无读取方 | 实际 ready 规则来自 `RoomStateManager._requireAllClientsReady`，重复且未接线 |
| SelectionTimeoutEnabled | :8 / :16 | true | false | `PropertiesSelectionManager.StartCurrentStageServer`(584-585) | 时长见表 5 |
| SkipMapVote | :9 / :17 | false | true | `RegisterDefaultStageProperties`(566) | |
| VoteOnResults | :10 / :18 | true | false | `SoloResultsView.Update`(55) | 决定 Solo 结果面板还是旧版投票 UI |
| ResultDecisionTimeoutEnabled | :11 / :19 | true | false | `ResultDecisionManager.InitializeDecisionServer`(206) | |
| ReturnTarget | :12 / :20 | Room | MainMenuHome | `FinalizeDecisionServer`(296-300) | enum |
| EndWhenAllRacersFinished | :13 / :21 | false | true | `RaceEndPolicy.ShouldEnd`(12) | Online 全员到达仍走完倒计时 |
| EndOnHumanFinish | :14 / :22 | false | `_aiCount==0` | `RaceEndPolicy.ShouldEnd`(11)；`LeaderboardTMPUI.cs:166` | Practice 唯一区分点 |
| AllowSkipSpectating | :15 / :23 | false | false (S6) | 无消费方 | 占位 |
| AllowQuitDialog | :16 / :24 | false | true | `QuitConfirmDialog.Update`(47) | |
| AllowPause | :17 / :25 | false | false (S7) | 无消费方；`MatchClock.IsPaused` 恒 false | 占位 |
| ShowRacerNameTags | :18 / :26 | false | false | 无消费方 | 占位 |
| ShowOpponentsOnMinimap | :19 / :27 | false | false | 无消费方 | 占位 |
| DefaultPlayerName | :20 / :28 | null | "玩家" | `PlayerIdentity.FallbackName`(Foundation/PlayerIdentity.cs:16)、`ResolvePlayerName`(46)；`MatchSpawnManager.TrySpawnPlayer`(135)；`RoomStateManager.SubmitLocalDisplayName`(634) | 进字符串表 |

- 默认模式：`MatchRules.cs:7`、`:13` 的 `Reset()` 都是 `new OnlineMatchRules()`。模式选择在 `SessionLauncher.StartSoloHost`(46)、`StartOnlineHost`(52)、`StartOnlineClient`(60)。
- Practice 入口无法到达：`SoloSetupPanel.StartPractice`(87) 固定传 `RacerId.MaxAI`。
- `RaceEndPolicy.ShouldEnd`（`Match/RaceEndPolicy.cs:11-18`）：人类到达 → 全员到达 → 首个到达后倒计时满。倒计时秒数来自 RaceFinishManager（表 2）。
- 2026-10-26 玩法深化阶段 B：训练木桩（README A1）的配置并入 `Match/ModeRule`，五列 `trainingDummyCount`（Practice 3）、`trainingDummySpacingMeters`（15）、`trainingDummyStartOffsetMeters`（90）、`trainingDummyLateralOffsetMeters`（10）、`trainingDummyResetDelaySeconds`（3），Online / Solo 的数量填 0。`MatchSpawnManager.Awake` 只读 Practice 行（`TrainingDummyModeRuleMap`），是否生成仍由 `IMatchRules.SpawnTrainingDummies` 决定；墙距、探测高度等摆放工程值留在组件上。数量大于 0 时其余四格必填（`DesignTableDomainRules.TrainingDummyCellsAreFilled`）。

## 表 2：TrackDefinition / RaceRules（按赛道，目前只有 RaceMap）

| 字段 | 位置 | 当前值 | 消费方 | 建议列 | 备注 |
|---|---|---|---|---|---|
| `RaceRules.DefaultLapsToFinish` | Foundation/RaceRules.cs:3 | 3 | 兜底：RaceCompletionTracker(11,291)、PlayerProgressReporter(229)、LapProgress.cs:69、RaceFinishManager(16 初始值) | Track.Laps:int | 重复源 |
| `RaceFinishManager.lapsToFinish` | Network/Gameplay/RaceFinishManager.cs:16 | 代码 3；场景 3 | 属性 LapsToFinish(36)；`TryRegisterFinish`→`Timing.Finish`(129)、PlayerProgressReporter、RaceCompletionTracker、LapProgress、debug finish(235) | Track.Laps:int（或按模式） | 真实来源是场景值 |
| `RaceFinishManager.postFirstFinishCountdownSeconds` | :17 | 15 / 15 | `TryRegisterFinish`(140)、`EvaluateRaceEndServer`(159,163) → RaceEndPolicy | ModeRules.PostFirstFinishCountdownSec | 只在 Online 有意义 |
| `RaceFinishManager.resultSceneName` | :20 | "RaceMapEndField" | 仅 debug log(293) | — | 遗留死字段，Tooltip 写明 Deprecated |
| `SceneNames.RaceMap` | Foundation/SceneNames.cs:5 | "RaceMap" | `RoomStateManager._raceSceneName`、`PropertiesSelectionManager._resolvedMatchSceneName`、`SoloUIBootstrap`(35,40) | Track.SceneName | 三处重复 |
| `PropertiesSelectionManager._resolvedMatchSceneName` | PropertiesSelectionManager.cs:66 | 场景 "RaceMap"（PropertySelection.unity:5491） | `LoadResolvedMatchSceneWithFadeCoroutine`(639-653) | 应由 Track 表 + 地图投票决定 | 与地图投票无关联 |

起跑点：`MatchSpawnManager._spawnPoints`（Network/Match/MatchSpawnManager.cs:16），值在 RaceMap，顺序 SpawnP1..P6（父物体 1318117449），y=3.3：P1 (-346.81, -68.83)、P2 (-327.23, -72.67)、P3 (-308.77, -56.99)、P4 (-335.51, -64.78)、P5 (-315.93, -68.62)、P6 (-297.47, -52.94)；P1 rot.y -0.8177；P1 与 P4 的 scale.y 7.1155 属脏数据。消费 `TrySpawnPlayer`(121-128)、`SpawnSoloAI`(156)：人类按已生成人数取索引(126)，AI 用 index+1；Solo 要求 1+AICount 个点，不够抛异常。建议 TrackSpawn 表（TrackId、Order、Pos、Rot）或保留场景 Transform 只配顺序。`MatchSpawnPoint.cs` 无字段。

Checkpoint：`Checkpoint.checkpointId`（Network/Gameplay/Checkpoint.cs:6，默认 1）；场景 Check1 id1 (-220, 0, -715)、Check2 id2 (-593, 0, -650)、Check3 id3 (-625.3, 0, -227.8)，BoxCollider trigger scale (1, 27.7, 67.6)。链路 `OnTriggerEnter` → `PlayerProgressReporter.ReportCheckpoint`(123) → `LapProgress.TryAdvanceCheckpoint`。建议 TrackCheckpoint 表。`LapProgress.requiredCheckpointCount=3` 挂在角色 prefab 上，不按赛道配置，与场景隐式耦合（详见 [inventory-movement.md](inventory-movement.md) 表 6）。

结果区：`RaceResultAreaManager`（Network/Session/Results/RaceResultAreaManager.cs:15-21）anchorRoot 1266039418、autoCollectAnchorsFromChildren 1、overflowOffset (2.5,0,0)、missingPlayerOffset (0,0,-3)；消费 `ResolvePlacementTransform`(105-145) 按 FinalRank-1 取锚点。锚点 `EndFieldSpawnPoint.spawnOrderIndex`（EndFieldSpawnPoint.cs:7）6 个，y=2.6：0 (-1.2, -71.2)、1 (-2.4, 19.2)、2 (-47.8, 20.4)、3 (40.8, 20.4)、4 (29.7, 44.1)、5 (-27.2, 48.9)。建议 TrackResultAnchor 表 + Track.ResultOverflowOffset。

逆行锁（`RaceCompletionTracker.cs:14-21`，prefab 与代码一致）：wrongWayWrapStartThreshold 0.2、wrongWayWrapEndThreshold 0.8、wrongWayForwardDotThreshold 0、wrongWayRecoveryLapProgressThreshold 0.05、wrongWayForceRespawnLapProgressThreshold 0.85、wrongWayRespawnLapProgress 0.01、wrongWayLap0RespawnLapProgress 0.99、startGraceSeconds 0.35。消费 `EvaluateWrongWayState`(189-223)、`DidWrongWayCrossStartLine`(225-230)、`GetCorrectionRespawnLapProgress`(281-284)、Update 宽限(67-81)。建议 RaceProgressTuning 表（全局或按 Track 覆盖）。复活点用赛道进度 01 表达，没有独立复活点表。

进度上报与排名：`PlayerProgressReporter.reportIntervalSeconds`（:12）代码 0.1 / prefab 0.2，偏工程；`LeaderboardManager.refreshIntervalSeconds`（:18）场景 0.1，下限 `Max(0.1,…)`(192)。排名规则硬编码 `LeaderboardManager.BuildRankings`(233-260)：已完成优先 → FinishOrder → FinishServerTime → 完成百分比 → Lap → Checkpoints → Distance → DisplayName；完成度公式 `RaceCompletionTracker.CalculateFinalCompletionPercent`(177-187)。若策划要调平局规则可做 enum 列表。

## 表 3：Lobby / Room / Session

Lobby（`Network/Lobby/SteamLobbyManager.cs:101-112`，MainMenu）：

| 字段 | 位置 | 值 | 消费方 | 备注 |
|---|---|---|---|---|
| `_defaultMaxPlayers` | :104 | 6，Range(2,6) | `CreateLobby`(247) | 重复 |
| `_defaultLobbyName` | :107 | "My Room" | `CreateLobby`(246) | 字符串表 |
| `_maxSearchResults` | :112 | 20，Range(1,50) | `RefreshLobbyListAsync`(515) | |
| 默认可见性 | 134、228 | Public | CreateLobby | |
| `PendingMaxPlayers` | :137 | 6 | — | 重复 |
| 硬编码人数上下限 | 350、359 | Clamp(2,6) | `SetPendingCreateSettings`、`UpdateCurrentLobbySettings` | 重复 |
| 搜索条件 | 514 | `WithSlotsAvailable(1)` | RefreshLobbyListAsync | |

建议 LobbyConfig 表（MaxPlayers / MinPlayers、DefaultName、DefaultVisibility、SearchMax）。没有重连窗口，host 离开即解散（`HandlePotentialHostLeft` 602-626）。FishNet ServerManager（MainMenu.unity:6306）`_maximumClients=6` 又一处重复；TimeManager `_tickRate=60` 工程参数。

房间（`Network/Room/RoomStateManager.cs`，`Assets/UIMenus/Lobby/RoomStateManager.prefab`）：

| 字段 | 位置 | 值 | 消费方 | 备注 |
|---|---|---|---|---|
| `MaxPlayerNameLength` | :22 | 32 const | `SanitizePlayerName`(643) → `RoomRoster.SanitizePlayerName`(53-63) | RacerNaming.MaxNameLength |
| `_propertiesSelectorSceneName` | :32 | "PropertySelection" | `TransitionToPropertiesSelector`(668-675)、`StartNextGameFromResultServer`(782) | 与 ResultDecisionManager 重复 |
| `_requireAllClientsReady` | :33 | 1 | `CanHostStartGame`(79) | 与 IMatchRules.RequiresReady 重复且互不相通 |
| `_raceSceneName` | :37 | "RaceMap" | — | 重复 |
| `_resultSceneName` | :38 | "RaceMapEndField" | `HandleSceneLoadEnd`(464) | 死分支，场景不存在 |
| `_mainMenuSceneName` | :39 | "MainMenu" | — | 重复 |
| `_pregameCountdownSeconds` | :40 | 代码 15 / prefab 18 | `RaceCountdownCoroutine`(876)，最小 1；UI `LeaderboardTMPUI.cs:197` | 进入 RaceMap 全员就绪后、intro 前的倒计时，Solo 也执行，每秒递减(889)；建议 ModeRules.PregameCountdownSec 按模式 |

Ready 与开局门槛（硬编码）：`RoomRoster.AreAllRequiredPlayersReady`(28-40) 空名单 false，host 不需 ready；host 自动 ready `BuildRoomPlayerState`(572)；回房重置 `ResetReadyForRoomReturn`(65-77)；`CanHostStartGame` 只要求 Players.Count>0(79)，房主一人可开局。建议 ModeRules.MinPlayersToStart:int、HostAutoReady:bool。

SessionLauncher 超时（`Match/SessionLauncher.cs`）：客户端连接 30 s(:66)，Host 启动 20 s(:81)，`ArmStartupDeadline` / `Pump`(146)。建议 SessionConfig 表。RoomStateManager 内部轮询（排除）：就绪 0.25 s(148)；等待场景 manager 10 s 超时、0.35 s 稳定窗口(1166-1167)；0.1 s 轮询(1182,1199)；诊断 0.25/1 s(1157-1159)。

## 表 4：SessionFlow 时长（结算与流程）

结算投票（`Network/Session/Results/ResultDecisionManager.cs`，RaceMap）：
- `decisionDurationSeconds` :21 — 60（代码=场景）— `InitializeDecisionServer`(186)、`DecisionCountdownCoroutine`(212-229)；UI `ResultDecisionUI.cs:117` — ModeRules.ResultDecisionSec:int。
- `nextScenePropertySelection / returnSceneMainMenu` :22-23 — "PropertySelection" / "MainMenu" — `FinalizeDecisionServer`(312,316) — 与 RoomStateManager 重复。
- 投票选项 `ResultDecisionTypes.cs:5-17`：None / StartNewGame / ReturnToRoom。
- 法定规则硬编码（`EvaluateDecisionServer` 252-275）：任一选 ReturnToRoom 立即返回；全员 StartNewGame 再来一局；超时 ReturnToRoom(225)；参与者断线 ReturnToRoom(243)。建议 ResultVoteRules 表（VetoChoice、UnanimousChoice、TimeoutChoice、DisconnectChoice）。

结果演出（`MatchResultPresentationCoordinator.cs:31-37`，RaceMap）：personalFinishDissolveOutSeconds 0.75/0.75（`NotifyPlayerFinishedServer`(120)）；forcedFinishDissolveOutSeconds 0.6/0.6（`BeginFinalResultPresentationInternal`(166)）；resultAreaDissolveInSeconds 代码 1 / 场景 0.75（`HandleTimelineFinishedServer`(234)、`EnterInteractiveAfterRevealCoroutine`(269-270)）；allowMovement/allowSkillsInResultArea 1/1（285，只对人类生效，硬编码 human 284）。溶解最短 0.01：`PlayerFinishPresentationController.cs:154`。建议 ResultPresentation 表。

结算转场 Timeline 桥（`ResultPresentationTimelineBridge.cs:19-28`）：useFallbackTimelineDelays 0（关闭，下面四项当前不生效）；blackScreenStartDelaySeconds 0 / 0.4；blackScreenFullyCoveredDelaySeconds 1 / 1.2；blackScreenEndDelaySeconds 3 / 2.1；timelineFinishedDelaySeconds 3 / 2.8；activeCameraPriority 500 / inactive 0；keepSharedCameraDuringResultArea 1。真实时长来自 `Assets/Animation/CG/EndResultSwitchTimeline.playable`：最长 clip 约 6 s，Signal 在 1 s 和 3 s。fallback 延迟与 timeline 信号是两套数据。

本地到达演出（`LocalFinishPresentationTimelineBridge.cs:14-16`）：rewindBeforePlay 1、stopOnResultAreaInteractive 0、stopOnRaceReset 1；Timeline `LocalPlayerFinishTimeline.playable`。低优先级。溶解属性名 `PlayerFinishPresentationController.dissolvePropertyName` "_DissolveAmount"（:11,:23）排除。

## 表 5：PropertySelection（选择阶段与选项）

- 阶段键 const（PropertiesSelectionManager.cs:22-24）："map"、"skill_loadout"、"skin"。顺序硬编码 `RegisterDefaultStageProperties`(566-568)：map → skill_loadout → skin，map 取决于 SkipMapVote。建议 SelectionStage 表（Key、DisplayName、Mode:enum、Order、按模式是否启用）。
- 阶段显示名与模式：map "Map Selection" Vote (1125)；skill_loadout "Skill Loadout Selection" Multi (1082-1083)；skin "Skin Selection" Single (1133)。
- 占位选项（1126-1128、1134-1136）：Map_A/B/C 显示 "Map A/B/C" 描述 "Default placeholder map A."；Skin_A/B/C 同样占位。建议 SelectionOption 表（Key、OptionId、DisplayName、Desc、Icon、Unlocked，字段已在 `SelectablePropertyOption.cs:8-13`）。结果无人读取。
- 阶段序列化字段（PropertySelection.unity）：`_stageDurationSeconds` :65 代码 30 / 场景 60（`StartCurrentStageServer`(584)、`StageCountdownCoroutine`(594-608)；UI `PropertiesSelectorUI.cs:218`）→ ModeRules.SelectionStageSec，可能按阶段；`_sceneTransitionFadeDurationSeconds` :67 1 / 2（`ResolveMatchTransitionDurationSeconds`(668-678)）；`_preferTimelineForMatchTransition` :68 1 / 1（优先 `PropertySelectionMatchTransitionTimeline` 时长，与 fade 时长重复）；`_registerDefaultStageProperties` :64 1。
- 技能配装：`SkillLoadout.SlotCount=3`（Buddah/ComboSkill/SkillLoadout.cs:9），被 `ResolvedPropertySelectionCache`(44) 与 PropertiesSelectionManager(191,308,476,488,1000,1011) 使用；RPC 签名写死 3 参数 `SubmitSkillLoadoutSelection`(427-430)、`SubmitSkillLoadoutSelectionServerRpc(slot0,slot1,slot2)`(461,476-481)。建议 LoadoutRules.SlotCount 并标注"不可单独由配表改"。`_allowDuplicateSkillSelections` :74 false，被配表覆盖（`SkillDatabase.GetAllowDuplicateSkillSelections`，SkillDataBase.cs:160-168）。`_fallbackSkillIds` :76 ["acceleration","push_projectile_hands","blackcurtain"]，被配表 fallback 池覆盖（SkillDataBase.cs:206-213），三层来源。校验 `LoadoutRules.ValidateSubmission / HasCompleteSelection / FindNextAutoFillSkillId`（LoadoutRules.cs:10-66）：空槽允许提交但视为未完成，超时自动补全只针对技能阶段（`FinalizeStageOnCountdownServer` 978-993）。
- 结算规则：Vote `TryGetVoteWinner`(935-956) 平票取选项顺序靠前；HostOnly 取房主；Single/Multi 取第一个提交(958-970)。

## 表 6：IntroSequence（开场演出）

全局参数（`RaceIntro/IntroSequenceManager.cs:37-45`，RaceMap）：

| 字段 | 代码 | 场景 | 消费方 | 备注 |
|---|---|---|---|---|
| fallbackIntroDuration | 3 | 3 | 284、474、492、592 | |
| introSpeedMetersPerSecond | 20 | 60 | `BuildAssignmentData`(355)；`GetLongestAssignedSplineDurationSecondsServer`(482) | |
| handoffLeadSeconds | 0.5 | 0.5 | 357 | 与 `RaceBodyIntroStateController.defaultHandoffLeadTime=0.5` 重复 |
| introLeadInSeconds | 1 | 1 | `ScheduleAndBroadcastVisualStartServer`(458) | |
| randomizeSlots | 1 | 1 | 145 | Solo 强制不打乱（硬编码 !IsSolo） |
| deterministicShuffleSeed | 20260404 | 20260405 | `ShuffleSlots` | 种子固定，每局打乱结果相同 |
| timelineDirector | — | `Assets/Animation/CG/TransitionToRaceMap.playable` | — | 最长 clip 约 18 s |

intro 时长 = max(timeline 时长, 最长样条长度 ÷ 速度)（470-477、479-493），下限 0.1(455)。建议 IntroConfig 表。

按人数布局（IntroLayout，`layouts` :37）：Layout_1P slot 398625393；Layout_2P 398625393、1300263913（与 1P 共用 slot0）；Layout_3P 3 slot；Layout_4P/5P/6P 名称带 "(Placeholder)"，来自 `Assets/Placeholder/Intro/Layout4P/5P/6P.prefab`，splineId Placeholder_NP_SlotK。Solo 是 6 人，始终用占位 6P 布局。消费 `ResolveLayout`(327-340) 按 playerCount 精确匹配，缺失打 warning 并跳过 intro(139)。建议 IntroLayout 表（PlayerCount、SlotRefs[]）或保留场景只配 SlotIndex 顺序。

槽位视觉（`IntroSlot.visualStyle`，IntroSlot.cs:25-31）：所有槽位都是代码默认 yawScale 0.75、rollScale 10、swayAmplitude 0.06、swayFrequency 3.5；rootPositionOffset / rootEulerOffset 全 0。范围内没有找到消费 `SlotVisualStyle` 的代码，需确认是否死数据。

样条（SplineIntroPath / IntroPath）：`arcLengthSamples`（SplineIntroPath.cs:12）代码 128 / 所有实例 2048，工程；`IntroPath.lockTerminalPositionToSlot / lockTerminalForwardToSlot`（IntroPath.cs:6-7）场景 1 / 0；`splineId` 场景用层级路径，prefab 用 Placeholder_NP_SlotK。

角色 intro 状态机（`RaceIntro/RaceBodyIntroStateController.cs:33-40`，Buddah.prefab）：defaultHandoffLeadTime 0.5（prefab）；maxGoOvershootSeconds 0.15、maxHandoffPendingOvershootSeconds 0.35、introVisualLagTicks 1（代码默认，prefab 未序列化；Tooltip 说明须与 Owner Interpolation 一致）；velocitySampleDeltaSeconds 0.02（prefab）。多为工程参数。硬编码：`BeginLaunchHandoff(…, 0.15f, …)`(560)；待机动画速度与相位 0.94 + 0.02·k、黄金比例相位(418-419)。`IntroClientController.pendingRetryIntervalSeconds=0.1`(:28) 工程。范围内没有 "3-2-1" 数值，倒计时视觉由 timeline signal 驱动（`HandleTimelineCountdownSignal` 244）。

## 表 7：RacerIdentity / Naming

- `RacerId.AIBase`（Match/Contracts/RacerId.cs:9）10000，工程，不配表。`RacerId.MaxAI`(:10) 5：`SoloMatchSettings` 校验(17)、`SoloSetupPanel`(87)、`AISkillCatalog.cs:107`、`IsAIValue`(19)。建议 ModeRules.Solo.AICount 与 MaxRacers 一起配置。
- 名字兜底：AI 名空 → "AI " + (index+1)（RacerIdentity.cs:23）；人类名空 → "Player"（同）；人类生成时 Online "Player " + clientId / Solo 用 DefaultPlayerName "玩家"（MatchSpawnManager.cs:135）；AI 显示名优先 `settings.AINames[index]`（从未传值）否则 `personality.DisplayName`（MatchSpawnManager.cs:166）；AI GameObject 名 "AI Racer " + (index+1)(:167)；全局兜底 `PlayerIdentity.FallbackName`（Foundation/PlayerIdentity.cs:16）`$"Player {clientId}"`；解析顺序 lobby 成员 → Steam 本人 → Solo 下 Steam 名 → 兜底（PlayerIdentity.cs:32-50）。名字长度上限 32。建议 RacerNaming 表 + 字符串 key。
- AI 难度：`MatchSpawnManager.SpawnSoloAI`(168) → `AIDifficultyProfiles.Resolve(difficulty, index)`；人格来自 AISkillCatalog。

## 表 8：SoloSetup（`Match/UI/SoloSetupPanel.cs`）

| 字段 | 位置 | 值 | 备注 |
|---|---|---|---|
| DifficultyKey | :10 | "SoloMatch.Difficulty"（PlayerPrefs） | 工程 |
| 默认难度 | :23 | 1 (Normal)，Clamp(0,2) | SoloConfig.DefaultDifficulty:enum |
| DifficultyLabels | :11 | {"简单","普通","困难"} | 长度 3，与 `SoloDifficulty` enum（SoloMatchSettings.cs:6）和 AISkillCatalog `Difficulties.Length==3` 重复 |
| AI 数量 | :87 | 固定 RacerId.MaxAI | 不能选 0–5 |

## 字符串表（范围内全部硬编码）

- SessionLauncher（Match/SessionLauncher.cs）:35 "Steam 不可用，请启动 Steam 后重开游戏"；:45 "单机设置缺失。"；:58 "主机地址为空。"；:65 "无法连接主机。"；:68 "连接失败，请重试。"；:78 "无法启动本地服务器。"；:80 "无法连接本地服务器。"；:83 "启动失败，请重试。"；:90 "传输层配置无效：需要 FishyFacepunch + Yak Multipass。"；:93 "传输层顺序无效。"；:95 "会话仍在运行或关闭中，请稍后重试。"；:146 "连接超时，请重试。"
- QuitConfirmDialog（Match/UI）:29 "退出到主菜单？"；:31 "本局进度不会保留。比赛仍在进行。"；:33 "继续比赛"；:35 "退出到主菜单"；:86 "暂时无法退出，请取消后重试。"；:90 "正在返回主菜单…"。按键 Esc 与 Gamepad East（51-53）属输入绑定。
- SoloResultsView：:31,86 "Practice 完成" / "比赛结果"；:40 "再来一局"；:42 "返回"；:67 "暂时无法再来一局，可以返回主菜单。"；:68 "正在进入选择…"；:69 "正在返回主菜单…"；:99 "计时数据暂不可用"；:103 "总用时  " / "完整圈速数据不可用"；:106 "第 {i} 圈  "；:116 "DNF"；:117 "     圈时  "；:118 "—"；:121 "   /   "。时间格式 `FormatTime`(129-134) "mm:ss.fff"，无效 "--:--.---"，全项目唯一格式化处。建议 Format 表。
- SoloSetupPanel：:25 "单人游戏"；:26 "六人单人赛 · 玩家 + 5 AI"（与 MaxAI 重复）；:28 "五种技能人格 · 固定配装"；:30 "难度"；:41 "开始单人赛"；:43 "返回"；:84 "初始化尚未完成，请稍后重试。"；:89 "启动失败，请重试。"；:93 "正在进入选择…"。
- SoloUIBootstrap：:69 "单人游戏"；:94 "正在初始化…"。
- 其他：`SoloMatchRules.DefaultPlayerName` "玩家"；英文调试/旧 UI 文案：`RoomRoster.BuildSummary`(82-95) "Players"、"(none)"、"Host"/"Player"、"Leader"/"Ready"/"Waiting"；`LeaderboardManager.BuildLeaderboardSnapshotText`(278-293) "(empty)"、" - Lap "、" - Finished #"；`ResultDecisionManager.BuildDecisionSummary`(159) "Waiting"；SteamLobbyManager 流程消息（235-712，如 "Steam is not initialized."、"Already in a lobby."、"Host has left the room."）；PropertiesSelectionManager 阶段与选项显示名（表 5）；RacerIdentity "AI n"/"Player"。
- UI 样式（偏美术）：`SoloUIFactory.cs` 21-23 参考分辨率 1280×720、match 0.5；42-86 颜色；面板尺寸字号；Canvas sortingOrder 32000/250/200。

## 重复与遗留来源

- 圈数：`RaceRules.DefaultLapsToFinish=3` 被 4 处兜底引用，真实值是场景 `RaceFinishManager.lapsToFinish=3`。
- 场景名："RaceMap" 在 SceneNames、RoomStateManager._raceSceneName、PropertiesSelectionManager._resolvedMatchSceneName；"PropertySelection" 在 SceneNames、RoomStateManager、ResultDecisionManager；"MainMenu" 在 SceneNames、RoomStateManager、ResultDecisionManager、SessionLauncher(136)。
- "RaceMapEndField" 遗留死数据：`RaceFinishManager.resultSceneName`(20) 与 `RoomStateManager._resultSceneName`(38)；`EndMatchAndLoadResultScene` 名字也是遗留。
- 最多 6 人 / 5 AI：至少 9 处。
- Ready 规则：`IMatchRules.RequiresReady`（无读取）与 `RoomStateManager._requireAllClientsReady`（生效）。
- 结果区移动/技能开关两套：`ResultAreaInteractionGate.allowMovementInResultArea/allowSkillsInResultArea`（ResultAreaInteractionGate.cs:11-12，场景 1/1，消费 `ShouldAllowMovementInput`(46) / `ShouldAllowSkillInput`(65)）与 `MatchResultPresentationCoordinator` 同名字段（:36-37，消费 285），各自生效可能矛盾。
- handoff lead：`IntroSequenceManager.handoffLeadSeconds=0.5` 与 `RaceBodyIntroStateController.defaultHandoffLeadTime=0.5`。
- 结算转场时长：fallback 延迟（未启用）与 EndResultSwitchTimeline signal。进入比赛转场：`_sceneTransitionFadeDurationSeconds` 2 与 PropertySelectionMatchTransitionTimeline（优先）。intro 时长：fallbackIntroDuration 3、TransitionToRaceMap（约 18 s）、样条÷速度，三者取最大。
- 技能 fallback 与重复策略三层：配表 → SkillDatabase → PropertiesSelectionManager SerializeField。
- 地图投票、皮肤选择、`ResolvedPropertySelectionCache.ResolvedSelections` / `MatchSceneName` 只写不读。
- 代码默认与序列化值不一致汇总：pregame 15/18；stage 30/60；fade 1/2；introSpeed 20/60；shuffleSeed 20260404/20260405；resultAreaDissolveIn 1/0.75；Timeline 延迟 0/1/3/3 对 0.4/1.2/2.1/2.8；reportInterval 0.1/0.2；arcLengthSamples 128/2048；`LapProgress.autoAdvanceSplineCheckpoints` true/false。
- 遗留与死代码：`PlaceholderPlayerController`（Network/Match/PlaceholderPlayerController.cs:12-14，`_moveSpeed=5`、`_rotationSpeedDegrees=720`、`_inputSendInterval=0.05`，旧占位角色）；`GameNetworkManager` MainMenu 里残留 `_legacyLobbyManagerPrefab: {fileID: 0}`；`SoloMatchSettings.AINames` 无调用方传入；`IntroSlot.VisualStyle` 无消费方（待确认）；`OasisScene.unity`（Assets/Scenes/Oasis/）不含本项目组件且不在 Build Settings（Build 只有 MainMenu、PropertySelection、RaceMap）；`Assets/Scenes/Cockpit`、`Garden`、`Terminal`、`RaceMapEndField` 只有资源文件夹无 .unity。

## 排除（工程参数）

`PredictionProtocol.Version=1`（Network/PredictionProtocol.cs:12）；Lobby metadata 键 l_name/l_ver/l_host/l_vis/l_max/l_pv（SteamLobbyManager.cs:91-98）；`SessionLauncher` SteamTransport=0 / LocalTransport=1(30) 与 PhysicsMode 切换(105)；FishNet TimeManager；`RacerId.AIBase`；`NetDebug.EnableVerboseLog`、`NetLog` TAG、各处 enableDebugLogs/enableVerboseLogs；RoomStateManager 轮询超时；`IntroClientController.pendingRetryIntervalSeconds`；RaceBodyIntroStateController overshoot、lag ticks、velocity sample、诊断常量(11-14)、0.15f 参数；`SplineIntroPath.arcLengthSamples`；`LeaderboardManager.refreshIntervalSeconds`；`PlayerProgressReporter.reportIntervalSeconds`；`SoloResultsView.TimesRefreshInterval=0.25`；`SkillVfxReplicator` prewarmOnStart 1、prewarmAliveSeconds 0.05、revealDelayFrames 2（SkillVfxReplicator.cs:12-14）及 DefaultLocalOffset (0,0,-2)、DefaultLocalEuler (0,180,0)(16-17)；`SkillVfxAttachmentSettings` 跟随标记（Vfx/SkillVfxAttachmentSettings.cs:6-8）；`MatchClock`、`MatchClockSync`、`RaceTimingSync`、`RaceTiming`、`RacerRegistry`、`RaceStartHandshake`、`RoomDiagnostics` 无可调数据；Canvas sortingOrder、UI layer 5。

## 待确认问题

1. `_pregameCountdownSeconds=18` 在 Solo 下也生效，且与 TransitionToRaceMap 约 18 s 相同。按模式拆分？与 intro timeline 是否同一字段？
2. 圈数按赛道、按模式，还是叠加？
3. 地图投票、皮肤选择要接线（Track 表配 SceneName，Skin 表接角色外观）还是从阶段移除？
4. Practice（0 AI）是否作为独立模式行开放？Solo AI 数量是否可配（0–5）？若可配，intro 布局、起跑点、AISkillCatalog 长度校验都要改。
5. 两套 allowMovement/allowSkillsInResultArea 以哪套为准？
6. `RequiresReady`（未接线）是否替换 `_requireAllClientsReady`？`MinPlayersToStart` 目前 1 是否符合设计？
7. `LapProgress.requiredCheckpointCount` 与 fallback 进度是否改为按 Track 配置？
8. 4/5/6P intro 布局是占位，Solo 永远用 6P 占位。正式布局交策划配表，还是美术场景摆放、只把 SlotIndex 放表？
9. 是否沿用 `Assets/Scripts/Config` 管线（RuleSet / ModeTag / MapTag）挂 ModeRules / Track？
10. `deterministicShuffleSeed` 固定导致每局站位相同。要固定站位、按局变化，还是按名次排？
11. Online 无重连窗口。是否预留"重连窗口"字段？
12. `IntroSlot.VisualStyle` 无消费方，需与角色/动画负责人确认。
