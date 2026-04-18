# System Map - Runtime Flow Ownership

## 1. Lobby and Connection Flow
```text
SteamLobbyManager.CreateLobby / JoinLobby
  -> ConnectionManager starts transport role
  -> GameNetworkManager wires FishNet managers
  -> RoomStateManager becomes room authority
```
Authority: server for gameplay state, Steam lobby for session membership handshake.
Risk: starting a server or client outside the lobby handshake can desync Steam and FishNet state.

## 2. Room to Property Selection Flow
```text
RoomStateManager host start request
  -> FishNet global scene load: PropertySelection
  -> PropertiesSelectionManager spawns and syncs readiness state
  -> ResolvedPropertySelectionCache is cleared and rebuilt for the next match
```
Authority: `RoomStateManager` owns the transition. `PropertiesSelectionManager` owns selection state after the scene loads.
Risk: scene ownership is shared across two systems. Do not move transition calls into menu UI or random scene helpers.

## 3. Property Selection and Loadout Resolution
```text
PropertiesSelectionManager
  -> receives property choices and skill loadout RPCs
  -> validates readiness and stage progression
  -> commits resolved selection state before race load
```
Authority: server validates every client submission.
Risk: selection rules come from config repositories. Hardcoded UI defaults can drift from legal runtime choices.

## 4. Race Intro and Gameplay Handoff
```text
RoomStateManager marks race scene ready
  -> IntroSequenceManager assigns intro slots and timing
  -> clients arm local intro visuals
  -> authoritative go signal hands control to live gameplay
```
Authority: room flow and intro timing are server coordinated.
Risk: intro timing, movement unlock, and prediction ownership all intersect here.

## 5. Prediction Tick Flow
```text
owner input
  -> BuddahPredictedInputData
  -> BuddahPredictedMotor simulate
  -> modifier and movement gate bridges
  -> reconcile compare
  -> visual and camera bridges update presentation
```
Authority: server reconciles final state.
Risk: direct Rigidbody or transform gameplay changes outside this stack can create rollback or desync failures.

## 6. Skill Execution Flow
```text
ComboSkillInput
  -> SkillExecutor cast request
  -> SkillDataBase and config repositories resolve metadata
  -> base or anti SkillAction executes
  -> prediction movement bridge applies movement-affecting effects
  -> observers and local presentation play feedback
```
Authority: `SkillExecutor` owns dispatch, server validates the cast.
Risk: bypassing config repositories or skipping the anti pair breaks legal runtime behavior.

## 7. Race Progress and Finish Flow
```text
PlayerProgressReporter
  -> RaceCompletionTracker
  -> LeaderboardManager
  -> RaceFinishManager
  -> MatchResultPresentationCoordinator
```
Authority: server only.
Risk: client-side shortcuts in progress, lap, or finish order are bugs even if the UI looks correct.

## 8. Result Decision Loop
```text
ResultDecisionManager
  -> collects player votes
  -> resolves final decision
  -> delegates next scene request to RoomStateManager
```
Authority: server only.
Risk: result voting is not the scene owner. The actual transition still belongs to `RoomStateManager`.

## 9. Config Runtime Flow
```text
ProjectConfigRuntime.EnsureInitialized
  -> ProjectConfigDatabase
  -> SkillConfigRepository / SelectionRuleRepository / GlobalRuleRepository
  -> skill, selection, and input systems consume validated data
```
Authority: config assets define allowed data. Runtime repositories define the legal read path.
Risk: changing config shape affects both editor validation and runtime behavior across multiple systems.
