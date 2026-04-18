# Safety - Forbidden Actions and Protected Systems

## Absolutely Forbidden
- Calling low-level scene APIs directly for multiplayer flow when an existing orchestrator already owns the transition.
- Editing prediction tick or reconcile logic without reading prediction docs and affected bridges first.
- Writing gameplay truth from client-only visual or UI code.
- Bypassing `SkillExecutor` and invoking gameplay skill effects directly.
- Hardcoding around `ProjectConfigRuntime` or repository rules to avoid fixing the real contract.
- Moving result-scene transitions out of `RoomStateManager`.
- Using Linux `rm` (or any non-`p4` delete) on files under the Perforce workspace. Use `p4 delete <path>`. If `p4 fstat` says session expired, stop and ask the user to `p4 login`. (See `Docs/lessons-log.md` L3.)

## Protected Files
Read-only unless the task explicitly targets them.

```text
Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs
Assets/Scripts/New_Buddah/Core/BuddahPredictedReconcileData.cs
Assets/Scripts/Network/Core/GameNetworkManager.cs
Assets/Scripts/Network/Core/ConnectionManager.cs
Assets/Scripts/Network/Room/RoomStateManager.cs
Assets/Scripts/Network/Lobby/SteamLobbyManager.cs
Assets/Scripts/Network/Session/PropertySelection/PropertiesSelectionManager.cs
Assets/Scripts/Network/Session/Results/ResultDecisionManager.cs
Assets/Scripts/Buddah/ComboSkill/SkillExecutor.cs
Assets/Scripts/Config/Runtime/ProjectConfigRuntime.cs
```

## High-Risk Files
Always re-read before editing.

```text
Assets/Scripts/New_Buddah/Core/BuddahPredictedModifierResolver.cs
Assets/Scripts/New_Buddah/Integration/BuddahPredictionSkillMovementBridge.cs
Assets/Scripts/New_Buddah/Bootstrap/BuddahPredictionBootstrap.cs
Assets/Scripts/Network/Gameplay/RaceFinishManager.cs
Assets/Scripts/RaceIntro/IntroSequenceManager.cs
Assets/Scripts/Config/Definitions/ProjectConfigDatabase.cs
```

## Prediction Rules
- Server is authoritative for gameplay state.
- Clients may predict but cannot define truth.
- All gameplay forces and movement modifiers must go through the prediction system when prediction mode is active.
- Do not modify Rigidbody or transform gameplay state directly as a shortcut around prediction.
- Client-only logic must not create gameplay divergence.

## Skill Rules
- Base and anti variants must stay paired when backfire logic exists.
- Skill metadata must remain aligned with runtime repositories and asset definitions.
- Movement-affecting skills must use the supported prediction bridge when prediction mode is active.

## Config Rules
- Repositories are the supported read path.
- Validator and runtime behavior must stay compatible after config changes.
- Do not silently invent fallback IDs, property keys, or skill IDs in UI logic.

## Stop and Ask
- The task spans multiple manifest systems and cannot be separated.
- The task changes a protected file.
- The task mentions reconcile, rollback, desync, tick drift, or scene sync uncertainty.
- The task would rename serialized fields, public methods used by scenes, or scene names.
- The task is ambiguous about the base or anti skill target.
