# 02 · 代码审查：死代码、可能的 bug、内存泄漏

审查对象：`refactor/vfx-refactor` @ `ddb00807`（之后一念光明会话的 `0b77d003..aafad1b7` 只动了黑幕相关文件，本文涉及黑幕的条目按 aafad1b7 复核过行号以外的结论）。分两半做，各自的核实方法写在各节开头。路径相对 `Assets/Scripts/`，另有注明的除外。

- **[PR#63]**：文件在 PR #63（`feat/mechanics-update`）的改动清单里，这次只写报告，等 PR #63 合并后再做。
- 每条的"确定 / 可能"按原样保留；"确定"指读代码或 YAML 能直接证实，没有运行验证的会写明。
- 第二阶段实际改了哪些、没改哪些和原因，见 [README.md](README.md) 的"处理结果"，各条的改动提交也记在那里。

## 实测补充（第二阶段，在 vfx-refactor 编辑器里跑过）

- **第二半 5.1（PropertySelection Canvas 被带进 DontDestroyOnLoad）已实测确认**：单人赛开局后，`PropertiesSelectorUI` 所在的根 `Canvas` 在 `DontDestroyOnLoad` 场景里、处于激活状态，进 RaceMap 时在，比赛结束回到 MainMenu 时还在。第二局时叠成两份没有实测（第二次启动 harness 报错，没走到选择场景），仍按代码推断。

---

# A. 核心：New_Buddah / Network / Match / Foundation / GameData / Config / RaceIntro

路径相对 `Assets/Scripts/`。符号扫描脚本、YAML 引用检查的做法见 A 第一节"核实方法"。


## A · 核实方法（全篇通用）

- 符号扫描脚本（共享笔记目录 `code-review-scripts/symscan.py`）：去掉注释后统计 `Assets/**/*.cs` 全部标识符出现次数，再在 `*.unity/*.prefab/*.asset/*.anim/*.playable/*.controller` 里收集 `m_MethodName/functionName`。"count=1" = 整个工程 C#（含 Tests、Editor）只出现在声明处，且不在任何 UnityEvent/动画事件里。
- 每个 MonoBehaviour 用 `.cs.meta` 的 guid 在场景/prefab/asset 里 grep。
- RaceMap.unity 里只有 3 个 UnityEvent 方法：`HandleTimelineBlackScreenReachedSignal`、`HandleResultPresentationCompleteSignal`、`TriggerDebugFinishRaceFromLocalUi`（`grep m_MethodName Scenes/*.unity`）。
- Tests 里的反射（`Set(obj,"field",…)`、`GetField`）逐个查过，命中的单独注明。

---

## A · 一、可能的 bug

### B1 中 · 可能 — 开赛后有人掉线，剩余玩家可能永远解不开移动锁
`Network/Room/RoomStateManager.cs:1083-1097`（`ReportGameplayLiveServerRpc`）
- 问题：`_gameplayMovementUnlocked` / `_raceStarted` 只在**收到某个客户端的 gameplay-live RPC 时**检查一次 `AreAllClientsGameplayLiveForSequenceServer`。如果 GO 之后、最后一个还没上报的玩家先掉线，`RemovePlayer`（:564）只把他移出名单，`HandleRemoteConnectionState`（:448）再调 `EvaluateRaceStartReadinessServer`，而后者在 `_authoritativeGoIssued` 后直接返回（:879），没人再检查 Gameplay 阶段。剩下的人 `ShouldEnableOwnerMovementInputNow()`（:102）一直是 false。
- 核实：grep `_gameplayMovementUnlocked.Value = true` 全工程只有 :1093 一处；`RemovePlayer`、`EvaluateRaceStartReadinessServer` 读过。
- 修：把解锁判断抽成 `TryUnlockGameplayServer(seq)`，在 `RemovePlayer` 后（InMatch 且 GO 已发）也调用一次；或 Update 轮询里在 `_authoritativeGoIssued && !_gameplayMovementUnlocked` 时检查。窗口很短（GO 到各端上报一般 < 1 RTT），所以是"可能"。

### B2 中低 · 可能 — 开赛倒计时可能出现两个协程同时递减（倒计时双倍速）
`Network/Room/RoomStateManager.cs:884`、`:887-920`、`:923-927`
- 问题：`StartCoroutine(RaceCountdownCoroutine())` 不保存句柄；`StopRaceCountdownServer` 只把 `_raceCountdownActive` 置 false，不停协程。Update 每 0.25 s 轮询 `EvaluateRaceStartReadinessServer`，若检测到"有人没准备好"→Stop，又在 1 s 内恢复→新起一个协程把 active 置 true。旧协程从 `WaitForSeconds(1f)` 醒来检查 `if (!_raceCountdownActive.Value) yield break;`——此时是 true，于是继续递减。两个协程同时 `--`。
- 核实：读了 Update（:149-159）、Evaluate（:863-885）和协程体。
- 修：保存 `Coroutine _raceCountdownRoutine`，Stop 时 `StopCoroutine`；或协程开始时记一个 generation 计数，醒来后比对。

### B3 低 · 确定 — host 上结算表现 RPC 双重执行
`Network/Gameplay/PlayerProgressReporter.cs:146-172`（`BeginPersonalFinishPresentationServer`、`TeleportToHiddenResultAreaServer`、`BeginResultAreaRevealServer`、`EnterResultAreaInteractiveServer`）
- 问题：每个 `*Server` 先本地 `Apply…Locally`，再发默认 `[ObserversRpc]`；host 的客户端也会收到，同一帧附近执行两次（传送两次、`NotifyTeleportTrailRebases` 两次、溶解协程重启）。目前各 Apply 基本幂等，所以只是浪费和日志噪声。
- 核实：grep `ExcludeServer|RunLocally` 只在 CommandBus 出现；这几个 RPC 都是默认属性。
- 修：RPC 加 `ExcludeServer = true`（server 端已本地执行），或 Apply 前 `if (IsServerInitialized) return;` 放到 RPC 体里。

### B4 低 · 确定 — `ObsessionFigure.OnValueChanged` 在 host 上每次变化触发两次 [PR#63]
`GameData/ObsessionFigure.cs:160-163`
- 问题：FishNet SyncVar 在 host 上 `OnChange` 以 `asServer=true` 和 `false` 各调一次；`OnValueChangedSync` 不看 `asServer`，`LeaderboardTMPUI` 订阅者（`UI/InGameUI/Legacy/LeaderboardTMPUI.cs:110`）在 host 上每帧收两次。
- 另：`Update()`（:85-96）服务器每帧写 `_current.Value`（float 每帧变），这个 SyncVar 每 tick 都脏，按人数持续占带宽。
- 修：`if (asServer && base.IsHostInitialized) return;`；恢复值改成按 0.1 s 或按变化量阈值写入。

### B5 低 · 可能 — 中途有人掉线后再加入，出生点可能重叠 [PR#63]
`Network/Match/MatchSpawnManager.cs:138-143`、`:193-211`
- 问题：出生点下标用 `_spawnedPlayers.Count`；有人掉线后 `DespawnPlayer` 删掉字典项，下一个加入者的下标会与仍在场的人重合；人数超过出生点时在连接事件回调里直接 `throw`。
- 核实：读 `TrySpawnPlayer`/`DespawnPlayer`。
- 修：记录已占用下标（`HashSet<int>`），取最小空闲下标；超出时 LogError 并拒绝，而不是在 FishNet 事件里抛异常。

### B6 低 · 可能 — 结算后 Solo"全部完赛"计数可能对不上
`Network/Gameplay/RaceFinishManager.cs:170-174`
- 问题：`racers` 取 `LeaderboardManager.Rankings.Count`（掉线会 `UnregisterPlayer`），`finished` 取 `_finishedRacerIds.Count`（不删）。若 `EndWhenAllRacersFinished` 打开且有人完赛后掉线，`finishedCount == racerCount` 可能永远不成立。Online 行默认 false，Solo 无掉线，所以目前只是隐患。
- 修：用 `>=`，或掉线时同时从 `_finishedRacerIds` 移除。

### B7 低 · 可能 — Editor 里 `GetComponent() ?? …` 回退不生效
`Network/Gameplay/RaceFinishManager.cs:328-330`、`Network/Session/Results/PlayerFinishPresentationController.cs:179-188`、`Network/Gameplay/PlayerProgressReporter.cs:180-196`（`??=` 链）
- 问题：Unity Editor 中 `GetComponent<T>()` 找不到时返回"假 null"对象，C# `??`/`??=` 不认它是 null，后面的 `GetComponentInParent/InChildren` 回退在 Editor 永远不执行（Player 构建正常）。`??=` 缓存的字段之后也不会重新解析。
- 修：改成显式 `if (x == null) x = …`。

### B8 低 · 已登记 — 回放用 `TimeManager.LocalTick` [PR#63]
`New_Buddah/Core/BuddahPredictedMotor.cs:392` — 已登记为 `Docs/online-repair/findings.md` R5，fix-spec P4 写了修法，仍未修（EXP-3 未跑）。不重复计数。

---

## A · 二、内存泄漏 / 资源

### L1 中 · 确定 — 每个 Buddah 实例泄漏一个 `InputActionAsset` [PR#63]
`New_Buddah/Integration/BuddahPredictionOwnerInputBridge.cs:18-27`、`:29-41`、`:51-58`
- 问题：`new InputSystem_Actions()` 内部 `InputActionAsset.FromJson` 创建运行时资产；生成类的 `Dispose()`（`InputSystem/InputSystem_Actions.cs:299-302`）才 `Destroy(asset)`。本桥的 `Dispose()` 只 `Disable()`，从不调 `_inputActions.Dispose()`。而 `SetEnabled(false)` 也会先 `Initialize()`，所以**每个**身体（远端、AI 都算）在 `OnEnable`/`Update` 都会建一个资产。每局 × 每个身体 × 每端各漏一个 ScriptableObject，靠 `Resources.UnloadUnusedAssets` 才可能回收。
- 核实：读生成类 Dispose；`BuddahPredictedMotor.cs:122-149` 只在 `OnStopClient` 调桥的 Dispose，无 OnDestroy。
- 修：桥的 `Dispose()` 里 `Disable(); _inputActions.Dispose(); _initialized = false;`；motor 加 `OnDestroy() => _ownerInputBridge.Dispose()`；`SetEnabled(false)` 在未初始化时直接返回、不创建。
- 同类问题在范围外：`Buddah/BuddahMovement.cs:80`、`BuddahHandControl.cs:186`、`ComboSkill/ComboSkillInput.cs:71` 也 `new InputSystem_Actions()` 且无 `Dispose()`（grep 无调用），请另一子代理核对。

### L2 中 · 确定 — 默认 Standalone 宏打开了三个预测探针
`ProjectSettings/ProjectSettings.asset:827`：`BUDDAH_PREDICTION_SHADOW;BUDDAH_PREDICTION_VISUAL_PROBE;BUDDAH_PREDICTION_PERF_PROBE`
- 问题：这三个宏 + `UNITY_EDITOR || DEVELOPMENT_BUILD` 门控的代码在 Editor 试玩和开发构建里全部生效：每 tick 额外跑 Shadow 步骤与对比（`Core/BuddahPredictedMotor.Shadow.cs`、`.cs:386-455`、回放也跑）；每个 Buddah 被 `AddComponent<BuddahPredictionVisualShakeProbe>`（`Bootstrap/BuddahPredictionBootstrap.cs` Awake）；`BuddahPredictionPerfProbe.AutoInstantiate` 每次场景加载用 `[RuntimeInitializeOnLoadMethod]` 建 DontDestroyOnLoad 对象并开 ProfilerRecorder。Release 构建不受影响。
- 修（不删探针）：从默认 Standalone 宏里去掉这三个，需要时再开；或在 `Docs/diagnostic-log-sampling-r8.md` 写明。

### L3 低 · 确定 — `LeaderboardManager` 每 0.1 s 全量重发排名并全场景查找
`Network/Gameplay/LeaderboardManager.cs:190-207`、`:262-268`、`:302-323`
- 问题：比赛中几乎每次轮询都 dirty，`Rankings.Clear()` + 逐条 `Add` → SyncList 每 0.1 s 把整表（含字符串名）重发给所有客户端；`_leaderboardSnapshotText` 字符串 SyncVar 同频重发；`EnsureServerPlayerRegistrations` 每 0.1 s `FindObjectsByType<PlayerProgressReporter>` 分配数组。`RankEntry` 实现了 `IEquatable` 但没被用来做差量。
- 修：按下标比对、只 `Rankings[i] = entry` 改变的项；注册改成 PlayerProgressReporter 主动注册（已有 `RegisterWithLeaderboardWhenReady`），删掉轮询查找。

### L4 低 · 可能 — `BuddahPredictionVisualRootBridge` 的协程在 OnEnable 重复启动
`New_Buddah/Visual/BuddahPredictionVisualRootBridge.cs:50`
- 问题：`OnEnable() => StartCoroutine(CaptureSoloPhysics())`，无 `OnDisable` 停止；组件 `enabled` 被反复开关时（协程不随组件禁用而停）会叠加多个记录协程，Solo 时间线同一物理步记录两次。目前没找到切换 `enabled` 的代码（grep 无），所以是"可能"。另外它在每个身体、每端、Online 下也每个物理步跑 `ResolveReferences()`。
- 修：存句柄，`OnDisable` 里 `StopCoroutine`；Online 下直接不启动。

### L5 低 · 确定 — `IntroClientController` 有挂起分配时每 0.1 s 全场景扫描
`RaceIntro/IntroClientController.cs:42-49`、`:208-236`、`:337-369`
- 问题：只要某条 assignment 的身体在本端不存在（例如该玩家在拿到 assignment 前掉线），`HasAnyPendingAssignments` 一直为 true，整局每 0.1 s 执行 `FindObjectsByType<RaceBodyIntroStateController>` + 遍历注册表 + 打 Verbose 日志，直到下一个序号覆盖。
- 修：pending 超过 N 秒或该 ObjectId 对应连接已离开时丢弃。

### L6 低 · 确定 — 其它小项
- `Network/Gameplay/SkillVfxReplicator.cs:20` [PR#63]：`static HashSet<int> PrewarmedPrefabIds` 永不清空；有界（按 prefab 数），但关闭 Domain Reload 时第二次进 Play 不再预热。修：`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` 清空。
- `Network/Gameplay/SkillVfxReplicator.cs:136-148` [PR#63]：预热协程中途对象被销毁时，当前 `prewarmObj` 不会被 Destroy（随场景卸载才消失）。
- `RaceIntro/RaceBodyIntroStateController.cs:246` [PR#63]：Solo 下每个 FixedUpdate 在 `_sequenceManager == null` 时调用已弃用的 `FindObjectOfType`。

已查无问题的订阅（成对退订）：`GameNetworkManager`、`SteamLobbyManager`（Steam 静态事件在 OnEnable/OnDisable 成对）、`RoomStateManager`、`PropertiesSelectionManager`、`ResultDecisionManager`、`MatchSpawnManager`、`ObsessionFigure`、`RaceBodyIntroStateController.OnTick`、`SkillCombatEvents.Observed`（只有 AI 诊断 harness 订阅且成对）。按 racer 存的字典：`RaceStartHandshake` 掉线清理、`LeaderboardManager` 掉线 `UnregisterPlayer`、`RacerRegistry` 在 `OnStopNetwork` 注销、`ResolvedPropertySelectionCache` 在会话结束清空，均正常。

---

## A · 三、死代码

### D1 设计表列没有效果：`Lobby.defaultVisibility`
`Network/Lobby/SteamLobbyManager.cs:134`、`:137`、`:190-191`、`:357-361`、`:388-389`
- `PendingVisibility` / `PendingMaxPlayers` 只写不读（注释说"给 debug UI"，那个 UI 已不在）。`Awake` 把表里 `defaultVisibility` 读进 `PendingVisibility`，但 `CreateLobby` 的默认参数是写死的 `Public`，`CreateRoomUI.cs:82` 总是显式传值。所以 `Match/Lobby.defaultVisibility`（`ProjectConfigRecords.Generated.cs:760`）改了不生效。
- 需要用户决定：把它接到 CreateRoomUI 的默认选项，还是从表里退休这一列。不放进安全删除表。

### D2 诊断脚手架（建议用户决定，不在安全删除表）
- `New_Buddah/Validation/BuddahPredictionCompatibilityRegistry.cs`、`BuddahPredictionValidationChecklist.cs`、`BuddahPredictionRuntimeHealthReport.cs`：每个 Buddah 运行时 `AddComponent`（`Bootstrap/BuddahPredictionBootstrap.cs` ResolveReferences），每 0.5 s（有日志消费者时）往 `BuddahPredictionDebugState` 写 15 个说明文字字符串（"Phase 2" 时期的系统地图、清单），这些字符串除 `highRiskFlagsSummary` 外没有任何读者（`BuddahPredictionDebugOverlay.BuildConsoleSummary` 只读其中一小部分）。属于诊断，按规则不建议删，但可以考虑收进同一个开关。
- `New_Buddah/Debug/BuddahPredictionDebugState.cs` [PR#63]：`activeModifiers`、`pendingImpulseSummary` 只声明（count=1）。
- `New_Buddah/Config/BuddahPredictionDebugSettings.cs:13`：`dumpGateState` 只声明。序列化字段，删除会丢 prefab 数据（无影响）。

### 可以安全删除的死代码表

"核实"列的缩写：**S1** = symscan count=1（工程内只有声明）；**S2** = 只有声明 + 本表其它死代码里的调用；**Y** = 场景/prefab/动画 YAML 无引用；**T** = Tests/Editor 无引用。删除 RPC 会改变该 NetworkBehaviour 的 RPC 编号，两端必须同版本（大厅已用 `Application.version` + `PredictionProtocol` 校验，正常构建无问题）。

| # | 文件 | 符号 | 行 | 核实 | 备注 |
|---|---|---|---|---|---|
| 1 | `Network/Room/RoomStateManager.cs` | `RequestReturnToRoomMenuKeepingSession()` | 293-299 | S1 Y T | 连带 #2、#3 |
| 2 | 同上 | `ReturnToRoomMenuKeepingSessionServerRpc` | 396-409 | S2 | 只被 #1 调用；而且没校验 host，任何客户端都能把全房间送回菜单，删掉顺便消除 |
| 3 | 同上 | `ReturnToRoomMenuKeepingSessionServer()` 无参重载 | 746-750 | S2 | 只被 #2 调用；有参重载保留（`ResultDecisionManager.cs:320` 用） |
| 4 | 同上 | `PlayersSummaryText`、`RequireAllClientsReady` 属性 | 87-88 | S1 T | |
| 5 | 同上 + `Network/Room/RoomRoster.cs` | `_playersSummaryText` SyncVar、`UpdatePlayersSummaryText()`（1144-1148 及约 8 处调用）、`RoomRoster.BuildSummary()`（79 起） | 55 等 | #4 删后无读者；T | 每次名单变化都向全员同步一段无人读的字符串。改动点多，单独一个 commit |
| 6 | `Network/Lobby/SteamLobbyManager.cs` | `UpdateCurrentLobbySettings` | 363-392 | S1 T | |
| 7 | 同上 | 事件 `OnMemberJoined`、`OnMemberLeft`、`OnLobbyListRefreshed`、`OnDebugLog` 及其 Invoke | 160、163、166、168；Invoke 在 532、580、588、597 | grep 全工程无订阅（UI 只订其它事件） | `OnLobbyListRefreshed` 删后 `RefreshLobbyListAsync` 里的 `list` 仍给 DTO 用 |
| 8 | 同上 | `PublishDebugLog` 及 12 处调用 | 351-355 | 只服务 #7 的 `OnDebugLog` | 与 #7 同一 commit |
| 9 | `Network/Core/GameNetworkManager.cs` | 事件 `OnRemoteClientStateChanged` 及 Invoke | 67、195 | 全工程无订阅 | 日志行 194 可保留 |
| 10 | `Network/Session/Results/ResultPresentationTimelineBridge.cs` | 事件 `BlackScreenStarted/FullyCovered/Ended`、`TimelineFinished` 及 Invoke；属性 `UseFallbackTimelineDelays`、`HasBlackScreenStarted/FullyCovered/Ended`、`HasTimelineFinished` | 42、45-48、50-53；Invoke 102、114、129、141 | S1/无订阅 T | |
| 11 | 同上 | `HandleTimelineBlackScreenStartSignal`、`HandleTimelineBlackScreenFinishedSignal` | 164-172 | S1 Y（RaceMap 只接了 Reached/Complete 两个信号） | `OnBlackScreenStart/End` 仍被 fallback 协程用，保留 |
| 12 | `RaceIntro/IntroSequenceManager.cs` [PR#63] | `HandleTimelineIntroStartSignal`、`HandleTimelineGoSignal`、`HandleTimelineCountdownSignal`、`PlayTimelineIfAssigned` | 240-262 | S1 Y T | |
| 13 | `RaceIntro/IntroClientController.cs` | `CompleteGoSequence`；空方法 `HandleTimelineVisualIntroSignal/CountdownSignal/GameplayCameraSignal` | 107-114；162-172 | S1；S2（只被 #12 调用，方法体为空） | |
| 14 | `RaceIntro/RaceBodyIntroStateController.cs` [PR#63] | 字段 `_predictionHandoffBridge` 及赋值 | 42、838-839 | 只写不读 | **测试反射写它**：`Tests/EditMode/IntroHandoffContinuityTests.cs:44` 要同时删那一行 |
| 15 | `RaceIntro/IntroPath.cs` | `EvaluateRotation`、`GetTerminalPosition`、`GetTerminalForward`（virtual，无 override） | 21-48 | S1 T | `lockTerminal*` 字段与 `BindTerminalPose` 仍被 `SplineIntroPath` 用，保留 |
| 16 | `RaceIntro/IntroSlot.cs` | `SlotVisualStyle`、`RootPositionOffset`、`RootRotationOffset`、`GetLaunchForward()` | 41-43、50-59 | S1 T | 对应序列化字段 `visualStyle`、`rootPositionOffset`、`rootEulerOffset` 只被这些属性读；删字段会丢 4 个场景/prefab 引用里的数据，建议先只删属性 |
| 17 | `RaceIntro/SplineIntroPath.cs` | `FindNearestT` | 87 起 | S1 T | |
| 18 | `RaceIntro/IntroTimeUtility.cs` | `HasReachedIntroStart` | 119-122 | S1 T | |
| 19 | `Network/Gameplay/PlayerProgressReporter.cs` | `EnterResultAreaServer`、`SyncResultAreaTeleportObserversRpc` | 136-144、236-240 | S1；S2 | `ApplyResultAreaTeleportLocally` 仍被 hidden 传送用，保留 |
| 20 | `Network/Gameplay/RaceCompletionTracker.cs` | `CurrentLapProgress01`、`HasWrongWayLock`、`WrongWayLockedCompletionPercent`、`WrongWayLockedLapIndex`、`LastLegalLapFloorPercent`、`GetComparableCompletionValue()` | 45、50-53、163-166 | S1 T | 只删属性，底层字段被逻辑使用 |
| 21 | `Network/Gameplay/RaceFinishManager.cs` | `HasFirstFinisher`、`FirstFinisherClientId`、`CountdownRemaining` 属性 | 38-40 | S1 T | 字段是 Inspector 只读显示，保留；注意这些是**服务器本地**值，客户端读到的永远是默认值 |
| 22 | 同上 | `resultSceneName` 字段及日志引用 | 19-20、303 | 注释已写 Deprecated；只用于日志 | 删会丢场景里一个字符串值，无影响 |
| 23 | `Network/Gameplay/LeaderboardManager.cs` | `PlayerProgress.ForwardDot`（只写）、`PlayerProgress.CheckpointIndex` / `RankEntry.Checkpoints`（恒为 0） | 129、329、332、346、253-254 | 写入点只有一处常量/直传 | `Checkpoints` 是 SyncList 元素字段，删会变 wire 格式；可与 L3 一起做 |
| 24 | `Network/Session/PropertySelection/ResolvedPropertySelectionCache.cs` | `MatchSceneName`、`SetMatchScene`、`ResolvedSelections`、`SetSelection`、`TryGetSelection` 及 `_resolvedSelections` | 7-8、11、23-38 | S1/只写；T | 调用点 `PropertiesSelectionManager.cs:142、905、912` 一起删；`PlayerSkillLoadouts` 属性（9）也无人读 |
| 25 | `Network/Session/PropertySelection/PropertiesSelectionManager.cs` | `AllowDuplicateSkillSelections`、`GetFilledSkillLoadoutSlotCount` | 94、336 起 | S1 T | 前者还会误导：它返回原始字段，忽略 :1122 的数据库覆盖 |
| 26 | `Network/Session/Results/ResultDecisionManager.cs` | `NextScenePropertySelection`、`ReturnSceneMainMenu` 属性 | 38-39 | S1 T | |
| 27 | `Network/Session/Results/RaceResultAreaManager.cs` | `HasPlacementApplied` | 106-109 | S1 T | `_placedRacerIds` 之后只写不读，可一起删（26、68、102） |
| 28 | `Network/Session/Results/ResultAreaInteractionGate.cs` | `AllowMovementInResultArea`、`AllowSkillsInResultArea` 属性 | 15-16 | S1 T | 字段被静态方法直接读，保留 |
| 29 | `Network/Session/Results/LocalFinishPresentationTimelineBridge.cs` | `IsPlayingLossOfControlTimeline`、`HandleLossOfControlPresentationFinishedSignal` | 19、53、62、66-70 | 属性无读者；方法 Y 无引用 | |
| 30 | `Network/Session/Results/EndFieldSpawnPoint.cs` | `DebugLabel` 属性 + `debugLabel` 字段 | 全文件内 | S1；场景 6 处都是空字符串 | |
| 31 | `New_Buddah/Events/BuddahPredictionCommandBus.cs` | `TeleportChannel/ModifierChannel/HandoffChannel` 属性、`TryEnqueueTeleport/Modifier/Handoff`、`Target_EnqueueTeleport/Modifier/Handoff` RPC，以及 `_teleport/_modifier/_handoff` 通道 | 44-46、56-78、124-167 | S1/S2：三个 Target RPC 无任何调用方；预测走 motor 自己的 teleport/handoff 队列 | 连带 `Events/Payloads/TeleportCmd.cs`、`ModifierCmd.cs`、`HandoffCmd.cs` 整个删除；`TryClearChannels` 与 `BuddahPredictionChannelMask` 只保留 Impulse 位。"Phase 2 脚手架"，单独 commit |
| 32 | `New_Buddah/Events/BuddahPredictionEventChannel.cs` | `[Obsolete] TryDequeue`、`BuildPendingSummary`、`NextSequence`、`LastConsumedId` | 136-150、162 起、60-61 | S1 T | |
| 33 | `New_Buddah/Integration/BuddahPredictionHandoffBridge.cs` | `GetPredictionControlState()`、`IsAuthoritativeLaunchHandoffConsumedOrActive()` | 68-79 | S1 T | `TryGetPredictionControlState` 保留 |
| 34 | `New_Buddah/Visual/BuddahPredictionVisualRootBridge.cs` | `GetVisualScaleMultiplier` | 288 起 | S1 T | |
| 35 | `New_Buddah/Bootstrap/BuddahPredictionBootstrap.cs` | `PredictedMotorConfig`、`CompatibilityRegistry`、`RuntimeHealthReport` 属性 | 49、56-57 | S1 | 字段保留 |
| 36 | `New_Buddah/Core/BuddahPredictedModifierCommandData.cs` [PR#63] | `CreatePushGrace`、`CreateSteeringSuppression`、`CreateRoomBypass` | 72、82、92 起 | S1 T | |
| 37 | `New_Buddah/Core/BuddahPredictedModifierState.cs` [PR#63] | `HasAnyActive` | 24 起 | S1 T | |
| 38 | `Config/Repositories/SelectionRuleRepository.cs` | `HasAnyRules` | 24 | S1 T | |
| 39 | `Config/Repositories/SkillConfigRepository.cs` [PR#63] | `GetDescription` | 46-52 | S1 T | 若 #63 的技能说明 UI 要用，届时再定 |
| 40 | `GameData/LapProgress.cs` | `HasStartedLap`、`TotalProgressPercent` | 48、50 | S1 T | |
| 41 | `GameData/ObsessionFigure.cs` [PR#63] | `DrainPerSecond`、`ExtraRecoveryPerSecond` 属性 | 51、53 | S1 | 字段被配表反射写入、被 Update 使用，保留 |
| 42 | `GameData/TrackSplineRef.cs` | `Progress01AtT` | 81-85 | S1 T | |
| 43 | `Network/Match/PlaceholderPlayerController.cs` | 整个类 | 全文件 | guid 无场景/prefab 引用；唯一引用是 `Assets/Editor/MatchScenePlaceholderSetup.cs:57`（另一子代理范围） | 需与那个 Editor 菜单工具一起删，先和另一子代理对齐 |

**不能删（看起来没人调用但有调用方）**：`BuddahPredictionPerfProbe.AutoInstantiate`（`RuntimeInitializeOnLoadMethod`）、`BuddahPredictionCommandBus.DebugA4FireTestImpulseToOwner`（`ContextMenu` 探针）、`SoloUIBootstrap`（3 个场景挂着）、`BuddahPredictionImpulseDebugBox`（RaceMap 场景挂着，诊断）、`DesignTableOverlay.TrySetField`（公共 API，配表工具可能用，未列入）、`ProjectConfigRecords.cs:39 loadoutId`（配表 schema 字段，由导入器按名写入）、`MatchClockSync._pausedTicks` / `IsPaused`（代码注释写明为 S7 暂停预留）。

---

## A · 四、建议的提交拆分（给主会话）

1. RoomStateManager 回菜单死 RPC（#1-#3）
2. RoomStateManager 摘要 SyncVar（#4-#5）
3. SteamLobbyManager 无订阅事件与调试日志（#6-#8）+ GameNetworkManager #9
4. 结算时间线桥与表现死成员（#10、#11、#19、#26-#30）
5. 开场死信号（#12-#18；#14 连同测试一行）
6. CommandBus Phase 2 脚手架（#31-#32）
7. 预测其余小项（#33-#37）[部分 PR#63]
8. 配置/赛道/选择小项（#20-#25、#38-#42）
9. L1 输入资产泄漏修复（独立 fix commit）
10. B1/B2 RoomStateManager 修复（各一个 commit）

---

# B. 表现、玩法与编辑器：Buddah / AI / UI / VFX / Rendering / Editor / 示例代码

## B · 0. 范围、方法和标记说明

**范围**：
- `Assets/Scripts/` 下的 `Buddah/**`、`AI/**`、`UI/**`、`VFX/**`、`Rendering/**`、`Debug/**`、`GlobalSettings/**`、`InputSystem/**`（不审生成的 `InputSystem_Actions.cs`）、`Editor/**`
- `Assets/Editor/**`
- `Assets/SharedAssets/**`、`Assets/Scenes/**` 下的 .cs

**分工**：
- 我自己读完了 Buddah、VFX、Rendering 的主要文件，以及 SharedAssets/Scenes 示例代码的可达性。
- 两个辅助子代理逐文件读完了 UI（30 个文件）和 AI/Editor（67 个文件），它们的发现在第 5、6 节。其中的高优先条目我抽查过源码。

**方法**：
- **可达性**：从 3 个 Build 场景、全部 `Resources/` 目录和 `ProjectSettings/` 出发，沿 YAML 里的 `guid:` 做传递闭包（脚本 `code-review-scripts/gp-review/closure.py`，结果在 `reach.json`），判断哪些脚本会进入游戏。
- **死符号初筛**：用 `code-review-scripts/gp-review/deadsym.py` 扫一遍，看哪些名字在 `Assets/` 和 `Tools/` 的 .cs/.py 里只出现在声明处，并排除 YAML 里的 `m_MethodName`、`functionName`。初筛结果再逐条人工核实。

**标记**：
- **[PR#63]**：该文件在 PR #63 的改动清单里，等它合并后再动。
- 严重程度分高、中、低。
- "确定"和"可能"分开写：确定 = 读代码或资产 YAML 能直接证实；可能 = 机制成立，但没有运行验证。

---

## B · 1. 优先看的 12 条（按影响排序）

| # | 级别 | 位置 | 一句话 |
|---|---|---|---|
| 1 | 高·确定（未运行） | `UI/MenusUI/SceneFadeController.cs:297-307` | 每次进比赛都把 PropertySelection 的整张根 Canvas 设为 DontDestroyOnLoad，每局累积一份旧选择 UI（子代理 A，见 5.1） |
| 2 | 中·可能 | `Buddah/ComboSkill/Skill_SlowTrap/MovementSlowTrapZoneEffect.cs:133` + `ChargedHandProjectileRuntime.cs:373,399` + `PushHitbox.cs:153,186` | 禅定缓流的触发球（服务器上挂在施法者身下）会被掌和推手当成施法者本体，命中提前约 3.6 m 生效 |
| 3 | 中·确定 | `VFX/InkFluidVolume.cs:117-118,157,203-210` | 墨体积的共享 scratch 和状态池是静态的，离开 RaceMap 后不释放，最多占约 128 MB 显存直到退出游戏 |
| 4 | 中·确定 | `SharedAssets/Benchmark/BenchmarkShortcutManager.cs:22-56` | URP 示例脚本靠 `[RuntimeInitializeOnLoadMethod]` 进入正式游戏：每次启动都建一个 DontDestroyOnLoad 物体并注册 Shift+B（加载不存在的 BenchmarkScene，只会报错） |
| 5 | 中·确定生效，影响可能 | `SharedAssets/Scripts/Editor/DirectionalLightmapperCorrector.cs:6-19` | 示例的贴图导入后处理会改写 RaceMap 的 228 张 `Lightmap-*_comp_dir.png`，把 alpha 至少抬到 0.1 |
| 6 | 中·确定 | `Buddah/ComboSkill/SkillFeelRouter` 路由与代码不一致 | `acceleration_local`、`blackcurtain_observers` 等 4 个 Feel 事件没有路由条目，每次施法在每个客户端打 Warning；缓流受害者走的也是 `acceleration_local` |
| 7 | 中·可能 | `AI/TrackWallProbe.cs:13-26` [PR#63 相关] | 测墙射线没排除赛车层；第一次测量在发车格，旁边的赛车会被当成墙，结果缓存整场（子代理 B，见 6.1） |
| 8 | 中·确定 | `Editor/NetworkManagerValidator.cs:100-131` | "Scan All Scenes" 用 Single 模式逐个打开场景，不提示保存，当前场景未保存的改动会丢（子代理 B） |
| 9 | 中·确定 | `Editor/VFX/Proto/PalmFrontalProto.cs:34-94` | 菜单无条件覆盖手调过的在用 prefab `VFX_Palm_Release.prefab`，还会把依赖重新指回包目录（子代理 B） |
| 10 | 中·确定（是否 bug 需你定） | `UI/InGameUI/Legacy/ResultDecisionUI.cs` | 所在面板一直是未激活状态，没有代码激活它，联机投票 UI 从来不显示（子代理 A） |
| 11 | 低·确定 | Buddah 的 4 个输入脚本 + UI 的 3 个 | `new InputSystem_Actions()` 从不 `Dispose()`；每个赛车（包括 AI 和远端玩家）每局建 4 份 InputActionAsset |
| 12 | 低·确定 | `BuddahHandControl.cs:634-658,883-918,1057-1073` + `HandPushProjectileRuntime.cs` 整个文件 [PR#63] | 非蓄力投射物这条路径不可达（唯一调用方总传 `true`），约 470 行死代码，其中有一个 ObserversRpc |

---

## B · 2. Buddah（玩法与表现）

### 2.1 [中·可能] 缓流触发球被当作施法者的受击体

- **位置**：
  - `Assets/Scripts/Buddah/ComboSkill/Skill_SlowTrap/MovementSlowTrapZoneEffect.cs:133-141`：`Instantiate(triggerColliderPrefab, transform)`，挂在施法者根节点下，只在服务器上生成。
  - `Assets/Scripts/Buddah/ComboSkill/Skill_PushProjectileHands/ChargedHandProjectileRuntime.cs:358-387`：用 `OnTriggerEnter/Stay` 加 `other.GetComponentInParent<NetworkObject>()` 判定命中。
  - 同文件 `:399` 和 `PushHitbox.cs:153,165,171`：`Physics.OverlapBox/Sphere(..., ~0, QueryTriggerInteraction.Collide)`。
  - `PushHitbox.cs:186`：同样用 `GetComponentInParent<NetworkObject>()`。
- **问题**：
  - `SlowTrapZone.prefab` 是一个 trigger 球，局部缩放 15，半径 0.5，也就是半径 7.5，在 Default 层。角色的 CapsuleCollider 半径约 3.89（Buddah.prefab:401）。
  - 两种命中判定都不按层或组件过滤 trigger，只要往上能找到 NetworkObject，就算打中了它。
  - 结果：缓流持续期间（默认 5 s），**别人的掌和推手碰到施法者外围约 3.6 m 的触发球就算命中施法者**，冲量提前施加。施法者自己的掌有 `victimNO == _attacker` 排除，不受影响。
  - 服务器和客户端的物理也因此不一致：客户端没有这个碰撞体。
- **怎么修**：二选一。
  - 把触发球放到专用层，命中查询的 layerMask 去掉这一层。
  - 或者命中判定跳过带 `SlowTrapTriggerRelay` 的碰撞体，或要求命中体带受击标记组件。
- **核实方式**：
  - 读了上面的命中代码。
  - 查了 `Assets/Character/SkillComponents/SlowTrapZone.prefab`：`m_LocalScale 15,0.5,15`、`m_Radius 0.5`、`m_IsTrigger 1`、`m_Layer 0`。
  - 查了 Buddah.prefab 的两个 CapsuleCollider。
  - 没有在比赛里实测，Buddah 根节点的缩放也没逐项核对，所以标"可能"。

### 2.2 [中·确定] Feel 事件 id 和路由条目对不上

- **位置**：
  - `OwnerMovementEffectApplier.cs:25,49`：`PlayFeelLocal("acceleration_local")`
  - `Skill_Acceleration_Anti.cs:26`：`acceleration_anti_observers`
  - `Skill_BlackCurtain.cs:33`、`Skill_BlackCurtain_Anti.cs:33`：`blackcurtain_observers`、`blackcurtain_anti_observers`
  - 技能 SO 里存的也是这几个值。
  - `SkillFeelRouter.cs:35-38`：找不到事件时 `Debug.LogWarning`。
- **问题**：
  - `Buddah.prefab:753-759` 的路由只有 4 个事件：`Anti_Reflection_Local`、`Skill_Reflection_Local`、`palm_launch_local`、`palm_anti_launch_local`。
  - 所以神足通（正向）、缓流命中、一念光明正反，每次施法都会在相关客户端打一条 Warning。Player 构建里 LogWarning 有堆栈开销。
  - 另外，缓流复用 `ApplyAccelerationToOwner`（传负值），受害者客户端播的是 `acceleration_local`。将来一旦给这个事件配上"加速"的 Feel，被减速的人也会看到加速反馈。
- **怎么修**：
  - 不再需要的事件 id，把 SO 值和代码常量清空。
  - 缓流改走单独的事件 id，或者加一个参数，不播加速 Feel。
- **核实方式**：grep `eventId:` 对比 Buddah.prefab，grep 代码里的字面量和 SO 的 `observersFeelEventId:`。

### 2.3 [低·确定] 非蓄力投射物路径不可达 [PR#63]

- **位置**：
  - `BuddahHandControl.cs`：`:634-658` 的 else 分支、`SpawnProjectilePushServer` `:883-918`、`SpawnProjectileVisualObserversRpc` `:1057-1073`
  - `HandPushProjectileRuntime.cs` 整个文件（403 行）
- **问题**：
  - `ActivateProjectilePushMode` 的唯一调用方是 `Skill_PushProjectileHands.cs:53-65`，第 10 个参数 `useChargedProjectileRuntime` 固定传 `true`。所以只要投射物模式开着，`_projectileUseChargedRuntimeServer` 就一定是 true，`SpawnProjectilePushServer` 永远走不到。
  - `ConfigureProjectilePushModeLocal` 的同名参数同样恒为 `true`（`Skill_PushProjectileHands.cs:85`）。
  - `SpawnChargedProjectileServer` 的 `hitTurnTorqueImpulse`、`allowSelfHit` 只有一个调用点，固定传 `0f, false`（`:645-646`）。
  - `projectilePrefab` 在 Buddah.prefab 里为空（`:491`），只在可视化时作为空值兜底。
  - retired.md §4 说 `HandPushProjectileRuntime.cs` 是"玩法代码，还在用"。这句话只是说它不是特效、不该按特效退役；实际上它在运行时不可达。
- **怎么修**：见第 8 节删除表。注意要删的东西里有一个 ObserversRpc：
  - FishNet 的 RPC 编号按声明顺序生成，删掉后，客户端和服务器必须使用同一构建（`Docs/optimization/p1-validation.md:82` 已有这条约束）。
  - `AISkillLifecycleHarness.cs:209` 的 `FindObjectsByType<HandPushProjectileRuntime>` 要一起删。
  - retired.md §4 那一句要改。
- **核实方式**：
  - grep `ActivateProjectilePushMode|ConfigureProjectilePushModeLocal|HandPushProjectileRuntime`，Assets 和 Tools 下的调用点只有上面这些。
  - `HandPushProjectileRuntime.cs.meta` 的 guid 在 prefab/scene/asset 里 0 引用。

### 2.4 [低·确定] 只有声明、没有调用方的方法

逐个 grep 过 Assets、Tools、Docs 和 YAML 的 `m_MethodName`：

- **`PlayerCamera.cs:196-216`** [PR#63]：
  - `SetRuntimeOffset`、`DampenRuntimeOffset`、`DampenRuntimeFieldOfViewOffset` 0 调用方。
  - 因此 `_runtimeOffset` 恒为零（只在 `:220`、`:456` 被清零），`:175`、`:179`、`:466`、`:469` 加的都是零向量。`_runtimeOffsetDampVelocity` 和 `_runtimeFovVelocity` 也只服务这几个死方法。
  - `SetRuntimeFieldOfViewOffset` 有用（`SkillCameraFov.cs` 在调），保留。
- **`SkillExecutor.cs:148-161` `RequestResetActiveSkillEffectsForRespawn` 和 `:376-380` `RequestResetActiveSkillEffectsServerRpc`** [PR#63]：
  - 0 调用方，`Docs/single-player/ai-skill-implementation-review-2026-10-03.md:20` 也写了"已无调用方"。
  - 删的是 ServerRpc，同样要求两端同构建。
- **`SkillLoadout.cs:56-65` `SetSlotServer`** [PR#63]：0 调用方。同组的 `SetSlotServerRpc` 已经按 `p1-validation.md:63` 删过，这个是漏掉的。
- **`SplineProgressTracker.cs:43` `IsClearlyWrongWay`**：0 读者。
- **`BuddahRespawn.cs:138,147`** [PR#63]：
  - `RespawnToTrackProgress` 和 `RequestWrongWayCorrectionRespawn` 的 `resetSkillEffects`、`preserveObsession` 两个参数从来没被读过。
  - 唯一外部调用 `RaceCompletionTracker.cs:262` 传的 `false, true` 就是默认值。

### 2.5 [低·确定] `InputSystem_Actions` 每个赛车建 4 份，从不 Dispose

- **位置**：
  - `BuddahMovement.cs:80`
  - `ComboSkillInput.cs:71`
  - `BuddahHandControl.cs:186`
  - 范围外：`New_Buddah/Integration/BuddahPredictionOwnerInputBridge.cs:23`
  - UI 的 3 处见 5.3
- **问题**：
  - 这些都在 `Awake` 里 `new`，所以 AI、远端玩家、服务器副本也各建一份，而这些对象永远不会启用输入。
  - 只有 `Disable()`，没有 `Dispose()`。生成类的 `Dispose` 会 `Destroy(asset)`（`InputSystem_Actions.cs:299-302`），不调用它，每局就留下 `4 × 赛车数` 个 InputActionAsset。项目里没有调用 `UnloadUnusedAssets`。
- **怎么修**：
  - 改到 `OnStartClient` 里、确认 `IsOwner` 之后再 `new`。
  - `OnDestroy` 里调用 `Dispose()`。
- **核实方式**：grep `new InputSystem_Actions` 和 `Dispose`。泄漏量没有用 Profiler 测，所以影响程度是"可能"。

### 2.6 [低·可能] 一念光明恢复渲染器时会覆盖技能层自己的显隐

- **位置**：`BlackCurtainViewController.cs:602-644`
- **问题**：
  - 受害者看到黑幕时，代码对其他赛车 `GetComponentsInChildren<Renderer>(true)` 全部关掉，并记下原来的 `enabled`；黑幕结束时按记录恢复。
  - 挂在赛车下的技能特效（比如 `palm_buff` 以 `Instantiate(prefab, transform)` 挂在角色下，见 `BuddahHandControl.cs:783`）的渲染器显隐由 `SkillVfxLayer.SetVisible` 控制。黑幕期间层窗口如果开始或结束，恢复时会被写回旧值：
    - 窗口已经结束的层被重新显示；
    - 或者刚开始的层被关掉。
  - 反方向也会出问题：层在黑幕期间激活，会自己把渲染器重新打开，受害者就能看到别人的特效。
- **怎么修**：隐藏时跳过 `SkillVfxLayer` 子树下的渲染器；或者改用层或渲染层过滤（例如 culling mask）代替逐个开关渲染器。
- **核实方式**：读代码；没有运行验证。

### 2.7 [低·确定] AI 复活时 Buff 特效和玩法状态不一致

- **位置**：`SkillExecutor.cs:180-190` → `BuddahHandControl.ResetSkillInputAndEffects`（`:121-127`）→ `ClearProjectilePushModeServer/Local`
- **问题**：
  - AI 复活会清掉服务器上的掌 Buff，但各客户端的 `_projectileBuffVfxLocal`（`:135`）不受影响，Buff 环继续显示到原定结束时间。
  - 玩家复活本来就不清 Buff，所以两边是一致的。
- **怎么修**：复活重置时对所有观察者发一条停止 Buff 特效的消息（对应控制器的 `BeginDissipation`）。
- **核实方式**：读 `ClearProjectilePushModeLocal` 和 `PlayProjectilePushBuffVfxLocal`。

### 2.8 [低·确定] 一念光明直接改共享的材质资产

- **位置**：`BlackCurtainScreenEffect.cs:146-157`
- **问题**：
  - 对 `Skill_BlackCurtain.fullscreenMaterial`（项目里的 .mat 资产）直接 `SetFloat/SetVector`。
  - 在编辑器的 Play 模式下，这会把 .mat 标脏，之后保存项目时，运行时的参数值可能被写进仓库。
  - `Awake` 里的 `DisableEffect()` 在 `_fullscreenMaterial` 还是 null 时什么都不做（`:118-121`），所以如果材质被存成激活状态，下次进入场景时不会被复位。
- **怎么修**：运行时 `new Material(shared)` 一份副本，在 `OnDestroy` 里销毁；或者改用全局 shader 属性。
- **核实方式**：读代码。

### 2.9 [低·确定] 其他

- **`GetComponent<T>() ?? GetComponentInParent<T>()`**：Unity 对象不能用 `??` 判空。编辑器里 GetComponent 找不到时返回的是"假 null"对象，`??` 不会继续往下找，所以编辑器和构建版本的行为不同。出现在：
  - `PlayerCamera.cs:79,81,300` [PR#63]
  - `PlayerScaleEffect.cs:37,174` [PR#63]
  - `CinemachineLocalPlayerFollower.cs:67,72,87`
  - 改成 `TryGetComponent` 或显式判空。
- **`CinemachineLocalPlayerFollower.cs:116-126`**：`FindObjectsOfType<PlayerCamera>()` 每帧调用一次，每帧有分配。
- **`BlackCurtainViewController`**：RaceMap 里 `controlCameraRenderer: 0`（RaceMap.unity:1610），所以 `ApplyCurrentRenderer`、`ReadCurrentRendererIndex`（反射 URP 私有字段）、`_noVolumetricUntil` 和渲染器下标字段都不生效。这是 Inspector 开关，不列入删除表。
- **Legacy 运动路径**：`OwnerMovementEffectApplier` 的非预测分支、`MovementAccelerationEffect`、`MovementRootThenAccelerationEffect`、`MovementInvertTurnInputEffect`。Buddah.prefab 的 `runtimeMode: 1`，代码里没有切换它的地方，所以运行时不可达。这已经登记为 `Docs/optimization/refactor-plan.md` 的 D2（待定），这里不算新发现。

---

## B · 3. VFX

### 3.1 [中·确定] InkFluidVolume 的静态显存不随场景释放

- **位置**：`Assets/Scripts/VFX/InkFluidVolume.cs`
  - `:117` `s_scratch`
  - `:118` `s_pool`
  - `:145-153` 只在 `Application.quitting` 和程序集重载时调用 `DestroyShared`
  - `:203-210` 状态集最多留 `s_poolKeep` 份
  - `:238` `s_poolKeep` 等于预算（prefab 是 8）
- **问题**：
  - `VFX_Palm_Bullet.prefab:249` 的网格是 80×80×192，约 123 万格。
  - 每个状态集 10 B/格，约 12.3 MB。共享 scratch 24 B/格加粗网格，约 30 MB。
  - 一局里同时出现过 8 个掌之后，池里留着约 98 MB，加上 scratch，共约 128 MB 的 3D RenderTexture。离开 RaceMap、回到主菜单或房间后一直不释放，直到退出游戏。
  - 这不算无限增长（有 8 份上限），但菜单阶段白占显存。
- **怎么修**：
  - `OnDisable` 里如果 `s_active.Count == 0`，就调用 `DestroyShared()`，或者延迟一段时间再调用；
  - 或者订阅 `SceneManager.sceneUnloaded` 时清空。
  - 下一局第一掌会重新分配，代价是一次性的。
- **核实方式**：
  - 读完整个文件。
  - grep `DestroyShared|sceneUnloaded`：只有 quitting 和 beforeAssemblyReload 两处。
  - 数字按 Docs/vfx/retired.md §4"每掌 41 MB → 12 MB"核对过。

### 3.2 [低·确定] 预热时会实例化墨体积并立即分配 GPU 网格

- **位置**：`SkillVfxReplicator.PrewarmAllVfx`（Network，范围外）和 `VfxStartupWarmup.WarmupPrefabs`（`VFX/VfxStartupWarmup.cs:94-127`）
- **问题**：
  - 两条预热路径都会实例化 `palm_bullet`。`InkFluidVolume.OnEnable` 会 `Allocate()` 一套状态集，并调用 `PlaceBox` 建一个隐藏盒子。
  - 销毁后状态集进池，这本身就是预热的目的，问题不大。
  - 但 `VfxStartupWarmup.PlayAll` 不关渲染器，也不调用 `Configure`。Light 和粒子会在 `warmupAnchor` 处真实播放 0.1 s，如果锚点在镜头内会闪一下。
- **怎么修**：确认锚点在镜头外，或者像 Replicator 那样先关渲染器。
- **核实方式**：读代码。没有在比赛开场看过画面。

### 3.3 [低·确定] `VfxQuality.Changed` 没有订阅者，注释也过时

- **位置**：`Rendering/VfxQuality.cs:11-19`
- **问题**：注释写"nothing reacts to it yet"，但 `SkillVfxPhaseController.cs:87` 读的就是 `VfxQuality.Current`。`Changed` 事件全仓 0 订阅。
- **怎么修**：改注释。事件可以保留（菜单热切换会用到），或者删掉。

### 3.4 检查后没有问题的项（避免重复劳动）

- **SkillVfxPhaseController**：分离出去的层在 `OnDestroy` 里一起销毁（`:419-428`）。
- **SkillVfxLayer**：全部走 MaterialPropertyBlock，没有 `.material`。
- **PlanarReflection**：
  - `beginCameraRendering` 的订阅和退订配对（`:59-82`）；RenderTexture 和镜像相机在 `OnDisable` 里释放。
  - RaceMap 只有 1 个 Camera，不会出现多相机来回重建 RT 的情况。
  - 小问题（低）：设置把缩放调成 0 时 `_texture` 不释放，直到组件被禁用。
- **InkVolumeColor、TrackEdgeVisibility**：用的都是 MaterialPropertyBlock。
- **GraphicsSettingsApplier**：克隆的管线资产会在 `RestoreOriginals` 和替换时销毁（`:166-171`、`:192-194`）。
- InkFluidVolume 的探针 `ReadStats`、`ReadBounds`、`CellToWorld` 没有代码调用方，但 retired.md §6.4 明确写了保留，这里不列为死代码。

---

## B · 4. Rendering、Debug、GlobalSettings

- **`GraphicsSettingsDebugPanel`**（F9）：在所有构建里都会安装。文件头注释和 graphics-settings.md 都说明这是临时面板，等菜单做好后再删或限制到开发构建。已登记，不算新发现。
- **`VisualJitterDiagnostic`**：挂在 Buddah.prefab 上，`diagnosticsEnabled: 0`，已经是关闭状态。按规则保留。
- **`FrameRateLock`**：启动前把 VSync 设成 0，随后 GraphicsSettingsApplier 再按玩家的选择改写（`GraphicsSettingsApplier.cs:97-101`）。注释已经说明了这个顺序，没有问题。

---

## B · 5. UI（子代理 A 逐文件审查；第 1 条我抽查过源码）

### 5.1 [高·确定（由代码和场景 YAML 推出，未运行）] PropertySelection 根 Canvas 每局累积一份

- **位置**：`SceneFadeController.cs:272-307`、`PropertySelectionMatchTransitionTimeline.cs:123-145`，以及 `PropertiesSelectorUI.cs:147-154` [PR#63]
- **问题**：
  - `PreserveFadeHierarchyIfNeeded` 会对 `crossFadeAnimator.transform.root` 调用 DontDestroyOnLoad。
  - 场景里 Fade 的根节点就是装着 PropertiesSelectorUI、检视面板和各选择根节点的 Canvas。这张 Canvas 会被带进 RaceMap 和 MainMenu，而且没有代码销毁旧的那份。
  - 下次进 PropertySelection 时，旧的 PropertiesSelectorUI 会找到新的管理器，在旧 Canvas 里重复 Instantiate PropertyGroupView，屏幕上出现两套叠在一起的选择 UI。
- **怎么修**：给 Fade 单独一张由 SceneFadeController 持有的 DontDestroyOnLoad Canvas；注册新对象前先销毁旧的根；不要再对任意 Animator 的 root 调用 DontDestroyOnLoad。
- **核实方式**：
  - 我读了 `:272-307` 和 Timeline 的 `RegisterFadeCarrier`，确认它把 Animator 的 root 设为 DontDestroyOnLoad。
  - 子代理用 `m_Father` 链确认了 Fade → Canvas（根）。

### 5.2 [中·确定运行时不执行，是否 bug 需你定] `Legacy/ResultDecisionUI.cs`

- **问题**：所在的 `ResultDecisionPanel`（RaceMap.unity:4307-4326）从 f2139c09（2026-04-08）起就是 `m_IsActive: 0`，没有任何代码、动画或 Timeline 激活它。所以联机投票按钮从来不出现。
- **附带**：
  - `ResultDecisionManager.BuildDecisionSummary` 和 `TryGetLocalDecision`（Network 层）只被它调用。
  - `Docs/design-tables/inventory-ui.md` §8 第 10 条写它"仍在使用"，不准确。
- **另外**：`LeaderboardTMPUI` 仍在用（RaceMap 里是激活的，测试也通过反射调用它），不能删。

### 5.3 [低] 其他

- **`InputSystem_Actions` 不 Dispose**：`MainMenuUI.cs:173`、`SkillInspectController.cs:336`、`SkillWallSelectionController.cs:511`，同 2.5。
- **`SkillWallItemView.cs:428-435`**：`targetRenderer.material` 会隐式复制材质，每个技能牌一份，从不销毁。改用 MaterialPropertyBlock。
- **`SkillSlotUI.cs:221-278`** [PR#63]：冷却 ≤ 0 时状态机会卡在 CooldownHidden，按键提示永久隐藏。现在 12 个技能资产的冷却都大于 0，还没触发。
- **`RoomUI.cs:167-173`**："Host has left the room." 刚写进去就被 `RefreshHeader` 覆盖，从来看不到。
- **订阅标志不复位**：管理器销毁后 `_subscribed` 不复位，新管理器再也订阅不上。出现在 `PropertiesSelectorUI.cs:73-111` [PR#63]、`RoomUI.cs:145-153`、`LeaderboardTMPUI.cs:74-82`。这些 UI 都在每帧轮询刷新，所以影响小。
- **`PropertySelectionMatchTransitionTimeline.cs:88-112`**：运行时 Timeline 兜底路径会 `CreateInstance<TimelineAsset>` 和 `new AnimationClip` 且不销毁；绑定类型也写错了（AnimationTrack 绑 CanvasGroup）。当前场景配置下走不到这条路径。
- **性能**（不属于三类问题）：
  - ObsessionUI 找到本地对象之前每帧调用 `FindObjectsByType` [PR#63]。
  - PropertiesSelectorUI 每帧调用 RefreshUi。
  - PropertyCarouselView 每帧排序并调用 `SetSiblingIndex`。

---

## B · 6. AI 与编辑器（子代理 B 逐文件审查；6.1 我抽查过源码）

### 6.1 [中·可能] `AI/TrackWallProbe.cs:13-26`

- **问题**：
  - 测墙用的是 `Physics.DefaultRaycastLayers`，只排除 trigger，没有排除 Buddah 层。Buddah.prefab 有一个非 trigger 的胶囊体，半径 3.89，在 Buddah 层（6）。
  - `SplineRacingLine.Capture` 第一次调用就测墙，并按赛道缓存到 `ConditionalWeakTable`。这次调用在 `AIRacerDriver.TryGetOverride` 里，位置在等待发车交接的判断之前，也就是赛车还停在发车格的时候。
  - 三档难度都是 `UseWallCorridor: 1`，所以起跑段的墙距可能被测成几米，并且整场都用这个值。
- **怎么修**：layerMask 去掉 Buddah 层（必要时再去掉 RollbackBoundingBox），或者改用编辑器里测好的固定数据。
- **核实方式**：我读了 `Measure`，确认 `QueryTriggerInteraction.Ignore` 加默认层。调用时机和难度配置由子代理核对。

### 6.2 [中·确定] 编辑器工具

- **`Assets/Editor/NetworkManagerValidator.cs:100-131`**：
  - 扫描前没有调用 `SaveCurrentModifiedScenesIfUserWantsTo`，当前场景未保存的改动会丢。
  - 附带 bug：`:143-155` 在循环里覆盖计数，最后只剩最后一个根物体的结果。
- **`Assets/Editor/VFX/Proto/PalmFrontalProto.cs:34-94`**：Build 菜单无条件 `SaveAsPrefabAsset` 覆盖在用的 `如来神掌/Release/VFX_Palm_Release.prefab`（29 个物体，都手调过），而且会从本地包目录重新实例化，丢掉 VfxPackVendor 做过的依赖重指向。改法：照 `SkillReleaseProto` 加 `overwrite=false`。
- **`Assets/Editor/MatchScenePlaceholderSetup.cs`**：
  - 整个工具已经没用：它要生成的 `Assets/Scenes/MatchScene.unity`、`Assets/Prefabs/PlaceholderPlayer.prefab` 都不存在。
  - 运行它会用 `NewScene(Single)` 丢掉当前场景改动，生成一个过时场景，并把它写进 Build Settings。
  - `inventory-ui.md:141`、`audit-report.md:252` 已把它标为遗留，但还没登记退休。

### 6.3 [低] 其他

- **AI 的 profile 所有权**：`AITuningHarness.cs:237-255,404-408` [PR#63]。harness 直接替换 `driver.Profile`，原来自有的 SO 成了孤儿；赛车销毁时又会 Destroy 掉 harness 共享的那份。
- **配表外键**：`DesignTableValidator.cs:105-117` 和 `DesignTableSchema.cs:65-70` [PR#63：Schema]。`list,xxx#ref=` 这类列的外键永远不会被校验。现在没有表用这种写法。
- **XlsxReader**：
  - `rPh` 注音里的文本会被拼进单元格内容。
  - `t="str"` 的公式字符串结果被当成数字规范化。
  - 现有的 7 个 xlsx 都不触发。
- **`DesignTableExporter`**："Validate Only" 每点一次泄漏一个 ProjectConfigDatabase SO；写出时抛异常也会泄漏。
- **`DesignTableDomainRules.cs:59-68,108-116`** [PR#63]：没有判空，列名一变就以 NRE 中断。
- **`AISkillDecision.cs:15-16`** [PR#63]：写死了 `PreviousSpeed > 5f`，`AISkillWorld` 用的是表里的值。现在表值也是 5。
- **`TrackWallFixtureTool.cs:20`**：用 Single 模式打开场景，不提示保存。
- **`RaceMapTemporalNoise.cs`**：
  - RenderTexture 只 `Release()`，不 `DestroyImmediate`（4 处）。
  - 带 DontSave 的材质克隆不销毁。
  - `Begin` 中途抛异常后 `s_Sweep` 不复位，之后一直报"a sweep is running"。
- **`PalmMeshTripo.cs:426`**：`EndStaticPreview()` 返回的 Texture2D 不销毁，每次泄漏 3 张。归墨手那个会话处理；只报泄漏，不涉及去重。
- **`SkillVfxCharacterLayer.cs:19,63-67`**：
  - 每次 `hierarchyChanged` 都对所有根物体做全量 GetComponentsInChildren。
  - Load/UnloadPrefabContents 没有 try/finally。
  - `VfxPackVendor.EffectPrefabs` 漏了 `VFX_Palm_Buff`。
- **过时注释**：
  - `AISkillCatalog.cs:75` [PR#63]：表名应为 AI/SkillTuning。
  - `ThrustVectorPlanner.cs:26`：表名应为 DrivingTuning。
  - `SkillReleaseProto.cs:51`：写的是"1 s"，反噬前摇现在是 2 s；`:54` 的 `cast_anti windowSeconds = 1f` 同样和配表不一致。
- **AI 在 server、client、host 下不会重复执行**：子代理核实过启用条件、权限门槛和按 tick 去重。

---

## B · 7. SharedAssets 与 Scenes 下的示例 .cs

**可达性结论**：
- 从 3 个 Build 场景、Resources 和 ProjectSettings 出发，只有 `Scenes/Oasis/Scripts/Runtime/OasisFog.cs` 可达。原因是 RaceMap.unity:14107 有一个**未激活**的 GameObject `Oasis Fog`，它在代码上依赖 `SharedAssets/Scripts/Runtime/FullscreenEffect.cs`。
- 其余 66 个 .cs 只被 Oasis/Terminal 示例 prefab、OasisScene.unity 或其他示例资源引用，或者完全没人引用。Cockpit 和 Garden 连场景都没有。
- 这些资源整体不该进仓库，已经登记在 `Docs/code-review/03-assets.md`（第 36、38、108 行）和 refactor-plan P7，这里不再重复。

**新发现**：有几个示例脚本不需要任何场景引用，自己就会运行，并且会影响正式游戏或项目资产。

| 文件 | 机制 | 影响 | 级别 |
|---|---|---|---|
| `SharedAssets/Benchmark/BenchmarkShortcutManager.cs:22-56` | `[RuntimeInitializeOnLoadMethod]` | Player 构建和编辑器里每次启动都会：<br>• 建一个 DontDestroyOnLoad 的 "Benchmark Shortcut Manager"；<br>• 启用 `EnhancedTouchSupport`；<br>• 注册 Shift+B 的 InputAction（从不 Dispose）。<br>按 Shift+B 会 `SceneManager.LoadScene("BenchmarkScene")`，这个场景在仓库和 Build Settings 里都不存在，只会报错。 | 中·确定 |
| `Scenes/Garden/Scripts/Runtime/DynamicLightController.cs:90-95` | `[RuntimeInitializeOnLoadMethod]` 订阅 `RenderPipelineManager.beginCameraRendering` | 每个相机每帧都回调一次，遍历一个空列表。开销很小，但属于没用的全局钩子。 | 低·确定 |
| `SharedAssets/Scripts/Editor/DirectionalLightmapperCorrector.cs:6-19` | AssetPostprocessor，匹配 `Lightmap-*_comp_dir` | RaceMap 是 Directional 烘焙（`m_LightmapsBakeMode: 1`），有 228 张 `_comp_dir.png`。每次导入或重烘焙，它都会 GetPixels/SetPixels，把 alpha 至少抬到 0.1，悄悄改写游戏的光照数据，导入也变慢。 | 中·确定生效，画面影响可能 |
| `SharedAssets/Scripts/Editor/LodLightmapCopy.cs:11-35` | `[InitializeOnLoad]` 加上 sceneLoaded、sceneOpened、bakeCompleted、playModeStateChanged、`IProcessSceneWithReport`、`PostProcessBuild` | 3 个 Build 场景都没有 LODGroup，现在是空操作，但每次加载场景、每次打包都会跑。另外：<br>• `:61` 的 `if (Debug.isDebugBuild)` 实际管住了下面的 foreach（应该是误写）；<br>• `:87` 的 `lods[i].renderers[j]` 在某级 LOD 的渲染器比 LOD0 少时会越界，届时会让打包失败。 | 低·确定 |

**建议**：执行 P7 删除示例目录时，先删掉 RaceMap 里那个未激活的 `Oasis Fog` 物体（它是 OasisFog 加 HeightFog.shadergraph 被 Build 引用的唯一原因，见 03-assets.md:101），再连同上表 4 个脚本一起删。在 P7 之前，至少先删 `BenchmarkShortcutManager.cs` 和 `DirectionalLightmapperCorrector.cs`：前者影响正式构建，后者改写游戏光照数据。

**核实方式**：
- 可达性闭包脚本。
- 对 67 个 .cs 逐个用 `.cs.meta` 的 guid 在 `*.unity/*.prefab/*.asset` 里 grep。
- grep `RuntimeInitializeOnLoad|InitializeOnLoad|AssetPostprocessor|ExecuteAlways`。
- `find Assets -name "BenchmarkScene*"` 为空。
- `ls Assets/Scenes/RaceMap | grep -c _comp_dir.png` = 228。
- RaceMap.unity 的 `m_LightmapsBakeMode: 1`。
- 3 个场景的 `LODGroup:` 计数为 0。

---

## B · 8. 可以安全删除的死代码

**通用核实方式**：
- 对符号名 grep 过 `Assets/**/*.cs`（含 Tests、Editor）、`Tools/**/*.py`、`Docs`，以及全部 `*.unity/*.prefab/*.anim/*.playable/*.controller/*.asset` 里的 `m_MethodName/functionName`。
- 对类 grep 过 `.cs.meta` 的 guid。
- 字段确认了不被 `DesignTableOverlay` 按名字反射写入（它只写配表映射里列出的字段）。

**注意事项**：
- 删 RPC 要求客户端和服务器同构建。
- [PR#63] 的项等 PR #63 合并后再删。
- 带 [SerializeField] 的字段删掉后，prefab 里存的值会在下次保存时被丢弃，没有功能影响。

| # | 文件 | 符号 | 行 | 核实方式和备注 |
|---|---|---|---|---|
| 1 | `Scripts/Buddah/PlayerCamera.cs` [PR#63] | `SetRuntimeOffset`、`DampenRuntimeOffset`、`DampenRuntimeFieldOfViewOffset`、字段 `_runtimeOffset`、`_runtimeOffsetDampVelocity`、`_runtimeFovVelocity`，以及 `:175,179,466,469` 里的 `+ _runtimeOffset`、`:220,224,225,456` 的清零 | 47-50、175、179、196-216、220-225、456、466、469 | 3 个方法 0 调用方；`_runtimeOffset` 恒为零。保留 `SetRuntimeFieldOfViewOffset` 和 `_runtimeFovOffset` |
| 2 | `Scripts/Buddah/ComboSkill/SkillExecutor.cs` [PR#63] | `RequestResetActiveSkillEffectsForRespawn`、`RequestResetActiveSkillEffectsServerRpc` | 148-161、376-380 | 0 调用方（Docs 也记录了）。ServerRpc 编号会变 |
| 3 | `Scripts/Buddah/ComboSkill/SkillLoadout.cs` [PR#63] | `SetSlotServer` | 56-65 | 0 调用方 |
| 4 | `Scripts/Buddah/SplineProgressTracker.cs` | `IsClearlyWrongWay` | 43 | 0 读者 |
| 5 | `Scripts/Buddah/BuddahRespawn.cs` [PR#63] | `RespawnToTrackProgress` 和 `RequestWrongWayCorrectionRespawn` 的 `resetSkillEffects`、`preserveObsession` 参数 | 138、147-149 | 方法体不读这两个参数；同时改 `Network/Gameplay/RaceCompletionTracker.cs:262` 的实参 |
| 6 | `Scripts/Buddah/BuddahHandControl.cs` [PR#63] | `:634-658` 的 if/else 只留蓄力分支；`SpawnProjectilePushServer`；`SpawnProjectileVisualObserversRpc`；`ActivateProjectilePushMode` 和 `ConfigureProjectilePushModeLocal` 的 `useChargedProjectileRuntime` 参数及 `_projectileUseChargedRuntimeServer/Local`；`SpawnChargedProjectileServer` 的 `hitTurnTorqueImpulse`、`allowSelfHit` 参数 | 86、131、546-547、632-658、713-742、744-766、841-851、883-918、1057-1073 | 见 2.3。ObserversRpc 编号会变。`GetActivePushCooldown*` 和 `GetChargedPushActionDelayLocal` 里的 `useCharged` 判断随之简化 |
| 7 | `Scripts/Buddah/ComboSkill/Skill_PushProjectileHands/HandPushProjectileRuntime.cs` | 整个文件 | 1-403 | 只被上一行的死路径调用；guid 0 引用。同时删 `Scripts/AI/Diagnostics/AISkillLifecycleHarness.cs:209` 那一项检查 [PR#63]，并改 retired.md §4 的那句说明 |
| 8 | `Scripts/Rendering/VfxQuality.cs` | `Changed` 事件（可选）；修正 `:11-12` 的注释 | 19、24 | 0 订阅者。如果菜单需要热切换就保留事件，只改注释 |
| 9 | `SharedAssets/Benchmark/BenchmarkShortcutManager.cs`（建议连 `Benchmark/` 整个目录） | 整个文件 | 1-105 | 见第 7 节。`PerformanceTest*` 和 `FrameData` 只被 Benchmark 自己引用 |
| 10 | `SharedAssets/Scripts/Editor/DirectionalLightmapperCorrector.cs` | 整个文件 | 1-20 | 见第 7 节。删之前请你确认：RaceMap 现有的方向图是在这个处理之后导入的，删掉后下次重导入 alpha 会变回原值，需要看一眼画面 |
| 11 | `Scenes/Garden/Scripts/**`、`Scenes/Cockpit/Scripts/**`（Garden、Cockpit 没有场景） | 整个目录 | — | guid 0 引用（WashiLightController、LightFlicker、DynamicLightController，以及 Cockpit 的 18 个文件）。Cockpit 的 `MainLight.shadersubgraph` 被 HeightFog 引用，不在 Scripts 目录里，不受影响。已登记 P7 |
| 12 | `Editor/MatchScenePlaceholderSetup.cs` | 整个文件（顺带范围外的 `Network/Match/PlaceholderPlayerController.cs`） | 1-155 | 子代理 B：目标场景和 prefab 都不存在；PlaceholderPlayerController 的 guid 0 引用。删前在 retired 里登记 |
| 13 | `Scripts/AI/AISkillDecision.cs` [PR#63] | `AISkillRacerSnapshot.Velocity`、`Suppressed`、`VisionImpaired`、`LastCastSeconds` | 10、11、14 | 子代理 B：正则 grep 0 读者；不序列化 |
| 14 | `Scripts/AI/AISkillWorld.cs` [PR#63] | 上面字段的写入、`_lastCasts`、`RecordCast` | 22、40、81-83（Velocity）、88、92、97、135-139 | 同上；同时删 `AISkillCaster.cs:241` 的调用 [PR#63] |
| 15 | `Scripts/Editor/DesignTables/DesignTableDomainRules.cs` [PR#63] | Movement/LapRule 回退、AI/SkillGlobal 回退分支 | 21-24（保留 Match/Track）、59-68 | 子代理 B：这两张旧表在 export-map 和 _Export 里都不存在 |
| 16 | `Scripts/Editor/DesignTables/DesignTableSet.cs` | `IsExported(DesignSheet)` | 164-167 | 0 调用 |
| 17 | `Scripts/Editor/DesignTables/DesignTableSchema.cs` [PR#63] | `DesignSheet.SourcePath` | 128、160 | 只写不读；Parse 的 `sourcePath` 参数被测试使用，保留参数 |
| 18 | `Editor/VFX/Proto/SkillReleaseProto.cs` | `Skill.skillId` | 16 | 0 读者（价值低） |
| 19 | `UI/MenusUI/SceneFadeController.cs` | `QueueFadeOutOnNextSceneLoad` | 37-41 | 子代理 A：全仓只有定义 |
| 20 | `UI/MenusUI/SkillWallSelectionController.cs` | `RebuildFromManager`、`RebuildWall`、`TryReplaceSlot`、`HandleItemInspectRequested`、`SelectedSkillIds`、空函数 `PlayRejectedSelectionFeedback` 及其调用、`ResetView` 的 else 分支和参数 | 71、131-134、170-209、236、259-267、341-368、957-963 | 子代理 A：0 调用，或者只互相调用；`ResetView` 唯一调用传 true |
| 21 | `UI/MenusUI/SkillInspectController.cs` | `RequestToggleInspect`、`CurrentItem`、Update 末尾的空分支 | 42、88-89、98-118 | 子代理 A |
| 22 | `UI/MenusUI/SkillInspectableItem.cs` | `CreateInspectVisualInstance`、`SanitizeInstance`、`AllowInspect`、`SkillId`、`visualRoot`、`inspectVisualPrefab` | 10-11、18-19、25-26、34-35、70-114 | 子代理 A。两个 SerializeField 删掉会丢 `技能绘马.prefab` 里的值（没有功能影响） |
| 23 | `UI/MenusUI/SkillWallItemView.cs` | `DisplayName`、`Description`、`PreviewIconKey`、`WallLocalPosition`、`WallLocalRotation`、`OptionData`，以及 `_description`、`_previewIconKey`、`_optionData` | 33、36-37、51-53、58-60、72、75-76 | 子代理 A；`_previewIconKey` 已登记在 inventory-ui.md §4 |
| 24 | `UI/MenusUI/PropertyGroupView.cs` | `PropertyKey` | 23 | 子代理 A |
| 25 | `UI/MenusUI/PropertySelectionMatchTransitionTimeline.cs` | `EnsurePlayableAsset`、`BuildAlphaCurve`、`_runtimeTimeline`、`_runtimeTrack`、`alphaCurve`、Awake 里的兜底 | 14、16-17、32-37、77-121 | 子代理 A 标"可能"：依赖场景里 director 已有资产；这条路径本身也不能工作（5.3）。建议和 5.1 一起重写时再删 |

**下面这些子代理 A 列出的项已登记在 `inventory-ui.md`，这里只引用**："已登记，仍未删"。
- `SkillUiSpriteLibrary` 的 `blackSpriteName`、`whiteSpriteName`
- `MiniMapController` 的 8 个调试字段
- `PropertiesSelectorUI` 的 `readyButton*` [PR#63]
- `PropertyCarouselView.scrollRect`
- `SkillSlotUI.TokenUI.tokenType` [PR#63]：配表来源，删前要看 §4

**不列入删除表、需要你决定的**：
- `ResultDecisionUI` 和 `ResultDecisionPanel`（5.2）。
- `RaceMapUIBootstrap` 整个组件 [PR#63]：子代理 A 判断它的 Instantiate 兜底永远走不到，标"可能"。
- `SceneFadeController` 的 Animator 状态机路径：建议和 5.1 一起重写。
- `BlackCurtainViewController` 的渲染器切换：Inspector 开关。
- Legacy 运动路径：D2。
- `RaceMapTemporalNoise` 的 SSR token：插件保留，等以后可能加回。

---

## B · 9. 只被测试调用的生产代码（建议保留）

- `ThrustVectorPlanner.CaptureCandidates`、`LastCandidateThetas`、`LastCandidateCosts`（`:38,54-55`），以及 `SplineRacingLine.SetWalls`（`:58-63`）：ThrustVectorSpecTests 在用。
- `DesignTableExporter.Run(bool, bool)` 重载、`ValueConverter.CurveToString/F`、`XlsxReader.Sheet.ColumnCount` 的读取：DesignTableTests 在用。
- `InkFluidVolume.PickSimulating`、`RankScore`、`ForceNoCompute`（internal）：测试在用；`ReadStats`、`ReadBounds`、`CellToWorld` 是探针，retired.md §6.4 定了保留。
- 通过 `-executeMethod` 或 MCP 调用的入口：AIDifficultyAssetTool、TrackWallFixtureTool、ThrustVectorAcceptanceBuild、AISkillAcceptanceTools、DesignTableCli、RaceMap* 渲染工具、PalmVolumeBaker、各 Harness.Begin。都有文档，不是死代码。

## B · 10. 没覆盖或没核实到底的部分

- `BuddahMovement.cs`（596 行，Legacy 路径，D2）、`SplineProgressTracker.cs`、`PlayerBlackCurtainTrail.cs`（已登记在 retired §6.3）、`PalmInkTimeline.cs`、`SkillVfxPreviewDriver.cs` 只看了生命周期和资源相关部分，没有逐行审逻辑。
- 2.1、3.2、5.1、6.1 都没有在 Unity 里跑过。2.1 需要在一局里让别人的掌打缓流施法者，观察命中位置；5.1 需要打两局后看 DontDestroyOnLoad 场景里有几张 Canvas。
- 输入资产和 InkFluid 池的泄漏量，没有用 Profiler 实测。
