# Architecture - System Boundaries and Ownership

## Layer Model

| Layer | Scope | Primary owners | Write authority |
|---|---|---|---|
| `server-orchestration` | connection lifecycle, room flow, scene transitions | `GameNetworkManager`, `ConnectionManager`, `RoomStateManager`, `ResultDecisionManager` | server only |
| `session-selection` | lobby membership, property selection, skill loadout selection | `SteamLobbyManager`, `PropertiesSelectionManager` | server validates, clients request |
| `prediction-core` | owner input, motor simulation, reconcile state, bridge routing | `BuddahPredictedMotor`, `BuddahPredictedReconcileData`, `BuddahPredictedModifierResolver`, `BuddahPredictionBootstrap` | prediction stack with server reconciliation |
| `skill-execution` | combo detection, cast dispatch, skill data lookup, base and anti execution | `ComboSkillInput`, `SkillExecutor`, `SkillAction`, `SkillDataBase`, `SkillLoadout` | `SkillExecutor` owns runtime skill dispatch |
| `config-rules` | legal skill definitions, selection rules, global rules, validation | `ProjectConfigRuntime`, `ProjectConfigDatabase`, repositories, validators | config assets and repositories |
| `race-and-results` | race intro, progress tracking, finish order, result presentation, next-match decisions | `IntroSequenceManager`, `RaceCompletionTracker`, `RaceFinishManager`, `LeaderboardManager`, `MatchResultPresentationCoordinator`, `ResultDecisionManager` | split between server orchestration and presentation listeners |
| `presentation` | UI, VFX, cameras, visual bridges | `BuddahPredictionPresentationBridge`, `BuddahPredictionVisualRootBridge`, in-game UI, menu UI | read simulation and sync state only |

## Ownership Rules
- `GameNetworkManager` and `ConnectionManager` own transport startup and FishNet manager lifetime.
- `RoomStateManager` owns room-to-selection, selection-to-race, result-to-next-game, and result-to-main-menu transitions.
- `PropertiesSelectionManager` owns synchronized pre-game selections and readiness checks.
- `ResultDecisionManager` owns result voting state, but delegates actual scene transitions back to `RoomStateManager`.
- `BuddahPredictedMotor` owns predicted movement state. Other systems may request effects only through supported bridges.
- `SkillExecutor` is the only supported runtime entry point for skill casts and anti-skill resolution.
- `ProjectConfigRuntime` and its repositories own runtime access to skill, selection, and global rules.
- Presentation systems may mirror or decorate state, but they must not become a second gameplay authority.

## Practical System Map

| System | Root paths | Canonical entry points | Protected hotspots |
|---|---|---|---|
| Room and scene orchestration | `Assets/Scripts/Network/Core/`, `Assets/Scripts/Network/Room/` | `GameNetworkManager`, `RoomStateManager` | `GameNetworkManager.cs`, `ConnectionManager.cs`, `RoomStateManager.cs` |
| Lobby and selection | `Assets/Scripts/Network/Lobby/`, `Assets/Scripts/Network/Session/PropertySelection/` | `SteamLobbyManager`, `PropertiesSelectionManager` | `SteamLobbyManager.cs`, `PropertiesSelectionManager.cs` |
| Prediction | `Assets/Scripts/New_Buddah/` | `BuddahPredictionBootstrap`, `BuddahPredictedMotor` | `BuddahPredictedMotor.cs`, `BuddahPredictedReconcileData.cs`, bridge classes |
| Skills | `Assets/Scripts/Buddah/ComboSkill/` | `ComboSkillInput`, `SkillExecutor` | `SkillExecutor.cs`, `SkillAction.cs`, paired base and anti skills |
| Config and rules | `Assets/Scripts/Config/` | `ProjectConfigRuntime`, validator and repositories | `ProjectConfigRuntime.cs`, `ProjectConfigDatabase.cs` |
| Race intro and results | `Assets/Scripts/RaceIntro/`, `Assets/Scripts/Network/Gameplay/`, `Assets/Scripts/Network/Session/Results/` | `IntroSequenceManager`, `RaceCompletionTracker`, `ResultDecisionManager` | `IntroSequenceManager.cs`, `RaceFinishManager.cs`, `ResultDecisionManager.cs` |
| UI and presentation | `Assets/Scripts/UI/`, VFX, visual bridges | menu UI, in-game UI, presentation bridge classes | visual bridge and result presentation hooks |

## Cross-Layer Policy

| From | To | Allowed path |
|---|---|---|
| session-selection | skill-execution | through validated loadout and property state only |
| skill-execution | prediction-core | through `BuddahPredictionSkillMovementBridge` or approved effect bridges |
| prediction-core | presentation | through read-only visual and camera bridges |
| config-rules | session-selection or skill-execution | through repositories and runtime initialization |
| presentation | gameplay state | forbidden unless routed back through owning server or skill system |

## High-Coupling Hotspots
- `RoomStateManager` couples room state, property selection, race start, result return, and scene management.
- `PropertiesSelectionManager` couples selection sync, readiness, cached selections, and transition timing.
- `SkillExecutor` couples combo input, database lookup, anti variant resolution, RPC dispatch, and prediction movement routing.
- `BuddahPredictedMotor` couples owner input, simulation, reconcile, modifiers, and bridge compatibility.
- `ResultDecisionManager` couples post-race votes with `RoomStateManager` transition requests.
- `ProjectConfigRuntime` couples config asset loading with runtime repositories used by skill and selection code.

## System Rules Worth Enforcing

### Prediction
- Server is authoritative for gameplay truth.
- Clients may predict but may not define authoritative state.
- Gameplay forces and movement modifiers must route through the prediction stack when prediction is active.
- Client-only visual logic must not create gameplay divergence.

### Skills
- Base and anti variants are a pair when backfire behavior exists.
- `SkillExecutor` is the only cast dispatcher.
- Runtime skill metadata must stay aligned with config repositories.

### Room and results
- Scene changes for multiplayer flows must remain inside the orchestrators that already own them.
- Do not let menu or result UI call low-level scene APIs directly.

### Config
- Do not bypass repositories with hardcoded fallback gameplay values unless the task explicitly updates the config contract.
- Validation changes must consider both editor validation and runtime repository reads.
