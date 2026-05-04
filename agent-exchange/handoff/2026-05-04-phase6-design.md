# phase6-race-start-handoff-redesign — Design Q&A

**Recon reference:** [2026-05-04-phase6-recon.md](2026-05-04-phase6-recon.md)
**Reviewer verify reference:** [2026-05-04-phase6-recon-verify.md](2026-05-04-phase6-recon-verify.md)
**Active contract:** [Docs/phase-gates/active/phase6-race-start-handoff-redesign-contract.md](../../Docs/phase-gates/active/phase6-race-start-handoff-redesign-contract.md)
**Branch:** `feat/phase6-race-start-handoff-redesign` @ `1f7b1aa` (Stage 1+2 docs landed; Stage 3 design lands as next commit)
**Status:** DESIGN PROPOSAL — no code changes yet. Methodology Rule 12 honored — Section 9 Design row stays ⏸ for cowork-reviewer to author.

---

## 0. Pre-disposed by reviewer (no further discussion)

These were resolved at Stage 2 RECON sign-off; Stage 3 inherits the dispositions verbatim:

| Q | Disposition | Evidence |
|---|---|---|
| **Q1** wire format | **COLLAPSE** — `RequestLaunchHandoffServerRpc` + `QueueLaunchHandoffTargetRpc` chain deletes whole, so `InheritDurationTicks` / `BlendDurationTicks` payload fields go away with the RPC. No "deprecated zero-value" compromise needed. | RECON Surface 4.6; verify Stage C row 1 |
| **Q4** auto-forward | **RESOLVED** — `BuddahPredictedMotor.BuildReplicateData:339` `float throttle = movementAllowed ? 1f : 0f;` is the auto-forward. `OwnerInputBridge` has only `ReadSteering()` (no `ReadThrottle`). Phase 6 unlock = flip `movementAllowed=true`; no new code. | RECON Surface 6; verify Stage B.2 |
| **Q9** reconcile-replay determinism | **ELEVATE to G8b** — Locked state drives `data.MovementAllowed=false` during Phase 3 → reuses motor.cs:466 early-return that produces zero `_predictionRigidbody.AddForce` calls + zero velocity write. Replay-deterministic by reuse, not by adding new gate. | RECON Surface 1.7; verify Stage B.3 |
| **Q10** event ID space | **MOOT** — race-start LaunchHandoffEvent goes away in new design (server-driven, no RPC payload). Teleport keeps own ID space. No overlap. | RECON Surface 4.6; Section 11.1 |
| **Section 11.1** L12 chain disposition | **option (a) full chain deletion** — single linear caller graph race-start ONLY, zero mid-race callers. ~12-file deletion footprint. | RECON Surface 4; verify Stage B.1 |

Below: only the Qs / OQs that demand a Stage 3 substantive call.

---

## Q2 — Spline `T` parameter location

**Picked:** **(B) per-stage Configuration asset** (Inspector field on `IntroSequenceManager`, replacing existing `introSpeedMetersPerSecond`).

**Options considered:**
| Option | Pros | Cons |
|---|---|---|
| (A) Inspector on `BuddahMovement` (per-prefab) | Simple Unity pattern | Per-prefab makes per-stage tuning impossible; designer must edit prefab to change race feel |
| **(B) Inspector on `IntroSequenceManager`** (per-stage) | Existing `introSpeedMetersPerSecond` already lives here; `IntroAssignmentData` already plumbs it per-buddah; designer-tunable per scene | Requires renaming Inspector field + updating 3 callsites |
| (C) Auto-derived from spline length only | Zero designer input | Loses Yonezawa intent — `T` is the *Yonezawa-input parameter* per contract Section 3.1 |

**Justification:** existing scaffolding ([RaceIntro/IntroSequenceManager.cs:39](Assets/Scripts/RaceIntro/IntroSequenceManager.cs#L39) `[SerializeField, Min(0.1f)] private float introSpeedMetersPerSecond = 20f;` and [RaceIntro/IntroAssignmentData.cs:13](Assets/Scripts/RaceIntro/IntroAssignmentData.cs#L13) `public float introSpeedMetersPerSecond;`) already routes a per-stage scalar through the assignment payload to per-buddah controllers. The cleanest swap is rename `introSpeedMetersPerSecond` → `introTraversalTimeSeconds` in the same 3 files (Inspector default `18f` per Section 3.1). `v_max = 2L / T` is computed at `RaceBodyIntroStateController` from `_assignedPath.TotalLength` + assignment data — reuses existing `_splineProgressTracker` plumbing per Section 3.1 note.

**Risk:** Inspector field rename invalidates serialized scene data with the old field. Mitigation: add `[FormerlySerializedAs("introSpeedMetersPerSecond")]` or use a one-shot scene migration. Stage 4 IMPLEMENT will pick exact pattern.

---

## Q3 — All-buddahs-spline-complete detection / race-start broadcast

**Picked:** **(c) extend existing SyncVar chain with sibling `_raceStartTick: uint`** (per OQ3 reviewer lean).

**Options considered:**
| Option | Tick-stamp G4 | Late-join | New API | Reuse |
|---|---|---|---|---|
| (A) New `[ObserversRpc] BroadcastCountdownBegin(countdownStartTick, raceStartTick)` per Section 3.3 sketch | ✅ Embedded ticks | ⚠ Manual catch-up logic | New RPC | No |
| (B) SyncVar `_gameplayMovementUnlocked` only | ⚠ Not tick-stamped — observer-time scheduling, not tick-deterministic | ✅ FishNet built-in | None | Yes |
| **(C) Extend SyncVar — add `_raceStartTick: uint` + reuse `_gameplayMovementUnlocked` flip** | ✅ Each client gates `if (TimeManager.LocalTick >= _raceStartTick)` | ✅ FishNet replays both SyncVars to late-joiners | One new SyncVar | Yes (extends existing chain) |

**Justification:** existing chain at [RoomStateManager.cs:51-52, 89-90, 298-304, 337-345, 1158-1170](Assets/Scripts/Network/Room/RoomStateManager.cs#L51-L52) (`_gameplayMovementUnlocked` SyncVar + `ReportLocalGameplayLive` ServerRpc + `AreAllClientsGameplayLiveForSequenceServer`) already implements the all-clients-ready signal. Phase 6 redesigns the **trigger condition** (was: motor's handoff consume → ReportLocalGameplayLive; becomes: all-buddahs-spline-complete → server enters countdown → 3s elapsed → flip SyncVars), but the **distribution mechanism** stays identical.

**Concrete shape:**
```
[Server] Each per-buddah RaceBodyIntroStateController detects spline-complete (Phase 1 endpoint reached) →
  RoomStateManager.NotifySplineCompleteServerRpc(sequenceId)  (replaces ReportLocalGameplayLive at this trigger point)
    [Server] _splineCompleteClientIds.Add(callerId)
    if (AreAllClientsSplineCompleteForSequenceServer(seq)):
        uint countdownStartTick = TimeManager.Tick
        uint raceStartTick = countdownStartTick + (uint)Mathf.RoundToInt(3f / TimeManager.TickDelta)  // 180 ticks @ 60Hz
        _raceStartTick.Value = raceStartTick    // SyncVar broadcast — late-join replays this
        _authoritativeGoIssued.Value = true     // existing SyncVar; signals "Phase 3 Lock begin"
[Server] OnTickEvents fires per server tick:
  if (TimeManager.Tick == _raceStartTick.Value && _authoritativeGoIssued.Value && !_gameplayMovementUnlocked.Value):
      _gameplayMovementUnlocked.Value = true   // existing SyncVar — flips Phase 4 unlock
[Each client] reads _raceStartTick.Value + _authoritativeGoIssued.Value via SyncVar OnChange (existing FishNet hook):
  enters Phase 3 Lock immediately (drives motor.MovementAllowed=false via existing chain)
[Each client] per-tick local check in motor.RunInputs:
  if (TimeManager.LocalTick >= _raceStartTick.Value && _gameplayMovementUnlocked.Value): unlock (Phase 4)
```

**Why same-tick simultaneous unlock holds (G4):** every client computes the unlock decision from the same SyncVar-replicated `_raceStartTick` and its own local `TimeManager.LocalTick`. Under FishNet CSP, `TimeManager.LocalTick` is server-authoritative + offset-corrected per-client; clients agree on tick numbering up to ±1 tick (FishNet's normal ordering tolerance). The unlock fires within 0-1 ticks across peers — meets G4 the same way the existing `[D-LOC HEARTBEAT]` cross-peer evidence is consumed.

**Risk:** edge case — if `_raceStartTick.Value` SyncVar arrives at a client AFTER its `LocalTick` already passed `raceStartTick`, the gate fires immediately at receipt (catch-up). Acceptable for Phase 6's UX bar. Q6 below covers this explicitly.

**Open questions:** none — option (c) is firm. Stage 4 IMPLEMENT picks exact field naming + chooses between reusing `_authoritativeGoIssued` semantically vs adding a sibling `_lockedAtTick` SyncVar.

---

## Q5 — Race-Start Timeline asset path

**Picked:** **placeholder `Assets/Cinematics/RaceStart/RaceStartTimeline.playable`** (per contract default), Yonezawa final disposition at Stage 4.

**Justification:** asset path is designer-domain. Stage 4 IMPLEMENT will hold `[SerializeField] PlayableDirector raceStartTimeline;` with a TODO comment until Yonezawa wires the asset. Engineering needs only the `PlayableDirector` reference + `Play()` call — no per-element scripting. Section 3.4.1 sketch is sufficient.

**Risk:** Stage 5 SMOKE blocked until Yonezawa wires asset. Mitigation: SMOKE Path A can run with `raceStartTimeline=null` + `if (raceStartTimeline != null) raceStartTimeline.Play();` guard — proves engineering side works without designer asset.

---

## Q6 — Late-join handling

**Picked:** **(b) get countdown synced with embedded ticks (catch-up immediate)** — late-joiner who receives `_raceStartTick` SyncVar AFTER `LocalTick > raceStartTick` immediately transitions to Phase 4 Unlock.

**Options considered:**
- (a) Skip to race_start immediately (regardless of timing) — loses cinematic UX
- **(b) Tick-driven catch-up** — late-joiner schedules locally based on `_raceStartTick` SyncVar; if tick already passed, immediately unlocks; if still in `[countdownStart, raceStart)` window, starts Race-Start Timeline mid-play OR skips to "lock + immediate unlock"
- (c) Wait for next race — too punishing; UX-unfriendly

**Justification:** option (b) reuses Q3 disposition's tick-comparison gate naturally. SyncVar replay-to-late-joiner is FishNet built-in; the gate `if (TimeManager.LocalTick >= _raceStartTick.Value && _gameplayMovementUnlocked.Value)` works for late-joiners by construction.

**Edge case design call (RECON Surface 8 OQ for reviewer):** when late-joiner enters Phase 3 Lock state but Race-Start Timeline is mid-play locally, two sub-options:
- (b.1) **Don't play Timeline** for late-joiners (no cinematic; immediate lock + immediate or near-immediate unlock if tick already passed). Cleaner, no half-played cinematic.
- (b.2) **Play Timeline from `time = (currentNetworkTime - countdownStartNetworkTime)`** to sync mid-play. More work, edge-case heavy.

**Picked:** **b.1 — no Timeline for late-joiners**. Rationale: late-join during a 3s countdown is rare; Yonezawa's UX bar accepts "you missed the cinematic" over "Timeline displays half-played and looks broken". Stage 4 wires this as `if (TimeManager.LocalTick < _raceStartTick.Value && raceStartTimeline.state != PlayState.Playing) raceStartTimeline.Play(); else skip-and-unlock`.

**Risk:** late-joiner might still see Phase 3 Lock state with no Timeline (no letterbox, no 3-2-1 UI) → looks "broken" UX-wise. If Yonezawa rejects at Stage 5 SMOKE, fall back to b.2.

---

## Q7 — Camera behavior during Phase 3 Lock

**Picked:** **stays on owner buddah at race-start angle** (no special-case code — falls out of existing camera gate).

**Evidence (verify Stage B.5 partial + RECON Surface 6 noted):**
```
git show HEAD:Assets/Scripts/Buddah/PlayerCamera.cs | sed -n '380,389p'
→ if (_introStateController != null && _introStateController.IsIntroActive)
      return true;
  RoomStateManager room = RoomStateManager.Instance;
  return room != null && room.IsMatchPhaseActive && !room.IsGameplayMovementUnlocked;
```
Camera's `Normal` presentation gate at [PlayerCamera.cs:387](Assets/Scripts/Buddah/PlayerCamera.cs#L387) returns `true` whenever `IsMatchPhaseActive && !IsGameplayMovementUnlocked` — i.e., **Phase 3 Lock state already triggers the existing "stable / pre-race" camera state**. No new camera logic needed.

**Justification:** Phase 3 = `_authoritativeGoIssued=true` + `_gameplayMovementUnlocked=false`. Camera reads `IsGameplayMovementUnlocked=false` → stable race-start framing. Phase 4 unlock = `_gameplayMovementUnlocked=true` → camera releases stable state → speed-tracking kicks in.

**Risk:** none expected. Stage 5 SMOKE confirms via SC1 driver observation.

---

## Q8 — Spline-complete failure / quorum

**Picked:** **wait-all + 30s server-side timeout fallback** with abort+reset.

**Options considered:**
- (a) Strict wait-all forever — risk: one network drop hangs the whole race indefinitely
- (b) Quorum (e.g., majority threshold) — race starts with subset; missing players auto-spectate or rejoin in-progress. Complex, edge-case heavy.
- **(c) Wait-all + timeout** — server waits for all `NotifySplineCompleteServerRpc`; if 30s elapses past expected spline-complete time AND not all reported, server logs warning + aborts race + returns to room (`_returningToRoomMenu` path already exists in `RoomStateManager`)

**Justification:** Phase 6 scope is "race-start handoff redesign", not "robust race orchestration". Failure mode = network drop / scene-load hang on a single buddah is rare; surfacing it as a clear abort (vs hanging) preserves debuggability without inventing quorum logic. Existing `_returningToRoomMenu` chain at [RoomStateManager.cs:277-431](Assets/Scripts/Network/Room/RoomStateManager.cs#L277) already implements room-return.

**Risk:** 30s is an arbitrary number; Stage 4 makes it `[SerializeField, Min(5)] private int _splineCompleteTimeoutSeconds = 30;`. Yonezawa-tunable.

**Out of scope reaffirmation:** disconnect-during-Phase-3-Lock still uses existing disconnect handling per contract Section 4.

---

## OQ1 — Spline driver scope expansion (★ supersedes contract Section 3.2.1)

Per reviewer disposition (verify Stage D OQ1 — AGREE), this design doc produces a consolidated implementation file list that **supersedes contract Section 3.2.1** (which is illustrative, not exhaustive). See [Implementation file list (consolidated)](#implementation-file-list-consolidated) below for the full surface.

---

## OQ2 — Legacy non-prediction handoff path

**Resolution:** **document-only — Phase 6 explicitly applies to PredictionV2 mode; Legacy mode retains pre-Phase-6 behavior.**

**Evidence:**
```
git show HEAD:Assets/Scripts/New_Buddah/Bootstrap/BuddahMovementModeSwitcher.cs | sed -n '8,12p'
→ [SerializeField] private BuddahMovementRuntimeMode runtimeMode = BuddahMovementRuntimeMode.Legacy;
git show HEAD:Assets/Scripts/New_Buddah/Bootstrap/BuddahPredictionBootstrap.cs | sed -n '159,162p'
→ public bool IsPredictionModeActive() { return RuntimeMode == BuddahMovementRuntimeMode.PredictionV2; }
```
Mode is configurable; `Legacy` is the **Inspector default** but production prefab is set to `PredictionV2` (verifiable at Stage 4 by inspecting Buddah.prefab or by RECON-style spot-check; here we trust the V5 closeout state). Legacy fallback is therefore reachable IF a developer toggles mode for debugging — it is NOT structurally dead.

**Disposition for Phase 6:**
- The `[BuddahMovement.cs:539, 561, 571-589](Assets/Scripts/Buddah/BuddahMovement.cs#L539)` legacy `_launchState` / `launchBlendTime` / `_launchInheritedVelocity` state machine stays UNTOUCHED.
- New design (stop-then-countdown) only fires when `IsPredictionModeActive()` returns `true` (i.e., motor.RunInputs is the gameplay path); Legacy mode continues with old velocity-inherit handoff.
- Add ONE comment block to `BuddahMovement.cs` near the `_launchState` field (around line 539-540) noting "Phase 6 redesigns race-start handoff for PredictionV2 mode; this Legacy fallback retains pre-Phase-6 behavior."
- Update contract Section 4 "Out of scope" with explicit "Legacy mode handoff path".

**Risk:** if a developer runs Legacy mode debug session post-Phase-6, they will see old jitter behavior. Acceptable — Legacy mode is non-production. Documented in lessons-log if any L# is needed (not at this stage).

---

## OQ4 — Contract line drift

**Resolution:** ALREADY PATCHED by reviewer in active contract Section 10 ("Key references") — line cites updated to "1884 / 1948 / 1976 / 2003". No action needed at Stage 3.

---

## OQ5 — Contract location

**Resolution:** ALREADY PATCHED — contract was copied to `Docs/phase-gates/active/phase6-race-start-handoff-redesign-contract.md` per option (II) "copy-and-redirect". This commit also has agent-exchange/handoff/ original with redirect header. Harness helper now surfaces phase6 contract correctly. No further action.

---

## Section 11.4 — Phase 7.5-A.1 confound layering

**Stage 6 VERIFY note (forward declaration, no Stage 3 action):** Verify report at Stage 6 must measure SC5 against TWO baselines:
1. **Post-Phase-7.5-A.1, pre-Phase-6** (validates 7.5-A.1's M1 fix in isolation)
2. **Post-Phase-6** (validates architectural M1+M2+M3 elimination)

Per reviewer Section 11.4 observation. NOT a Stage 3 design call — this is a Stage 6 SMOKE-driver protocol clarification. Stage 6 VERIFY report must explicitly call out which jitter axis each ship eliminated.

---

## G8b — strict gate elevation (Q9 → G8 sub-clause)

Per Section 11.2 reviewer recommendation + Q9 disposition above, contract Section 5.3 receives:

> **G8b** — During reconcile replay across any tick T inside `[countdownStartTick, raceStartTick)`:
> - `_predictionRigidbody.AddForce` count = 0 across all replays of T
> - `_predictionRigidbody.AddTorque` count = 0 across all replays of T
> - `rb.position`, `rb.velocity`, `rb.angularVelocity` are bit-identical at end-of-replay across all N replays of T
>
> Verify via existing `[D-LOC HEARTBEAT]` cross-replay snapshot evidence + a temporary `#if UNITY_EDITOR` Locked-state replay assertion inside motor (count `_predictionRigidbody.AddForce` invocations during replay; assert == 0 each replay). Implementation falls out of motor.cs:466 `MovementAllowed=false` early-return — gate is a verification check, not a code-path addition.

Stage 4 IMPLEMENT adds the `#if UNITY_EDITOR` assertion as part of motor's MovementAllowed branch; Stage 5 SMOKE captures `[D-LOC HEARTBEAT]` over the lock window; Stage 6 VERIFY greps the raw log to confirm 0 force application across replays.

---

## Implementation file list (consolidated)

★ This list **supersedes contract Section 3.2.1**. Reviewer Section 11.3 observation is satisfied. Stage 4 IMPLEMENT works from this list.

### Area 1 — Spline deceleration formula (contract Section 3.1)

| File | Line | Change |
|---|---|---|
| `Assets/Scripts/RaceIntro/IntroSequenceManager.cs` | :39 | rename `[SerializeField] float introSpeedMetersPerSecond = 20f;` → `[SerializeField] float introTraversalTimeSeconds = 18f;`; add `[FormerlySerializedAs("introSpeedMetersPerSecond")]` if scene migration needed |
| `Assets/Scripts/RaceIntro/IntroSequenceManager.cs` | :339 | `introSpeedMetersPerSecond = introSpeedMetersPerSecond` payload write → `introTraversalTimeSeconds = introTraversalTimeSeconds` |
| `Assets/Scripts/RaceIntro/IntroSequenceManager.cs` | :461 | `float speed = Mathf.Max(0.1f, introSpeedMetersPerSecond);` → derive from `introTraversalTimeSeconds + path.TotalLength` per `v_max = 2L/T` |
| `Assets/Scripts/RaceIntro/IntroAssignmentData.cs` | :13 | rename field `float introSpeedMetersPerSecond` → `float introTraversalTimeSeconds` |
| `Assets/Scripts/RaceIntro/RaceBodyIntroStateController.cs` | :422-433 (`GetNormalizedDistanceT`) | rewrite `distance = elapsed × constantSpeed` → linear-deceleration formula `distance = L × (2t/T - (t/T)²)` per Section 3.1 |
| `Assets/Scripts/RaceIntro/RaceBodyIntroStateController.cs` | :441-443 (`GetIntroSpeedMetersPerSecond`) | rename method to `GetIntroTraversalTimeSeconds` OR keep as `GetVMaxMetersPerSecond` derived from T+L |
| `Assets/Scripts/RaceIntro/RaceBodyIntroStateController.cs` | new method | `NotifySplineCompleteServer` — fires `RoomStateManager.NotifySplineCompleteServerRpc(_activeSequenceId)` when `_splineElapsed >= _traversalTime` AND on owner side; spline endpoint snap (`rb.position = _splineEndPosition; rb.velocity = Vector3.zero;`) |

**Type:** modify (5 line-level), modify (1 method body rewrite), add (1 new method). NO new file.

### Area 2 — Motor Locked state + Inherit/Blend deletion (contract Section 3.2)

| File | Line | Change |
|---|---|---|
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` | :862-911 (`RequestAuthoritativeLaunchHandoffFromOwner`) | **DELETE method** (Section 11.1 option a) |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` | :914+ (`TryApplyServerAuthoritativeLaunchHandoff`) | **DELETE** along with companion |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` | :1027+ (`QueueLaunchHandoffTargetRpc`) | **DELETE** TargetRpc |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` | :1028+ (`RequestLaunchHandoffServerRpc`) | **DELETE** ServerRpc |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` | :1100+ (`TryQueueLaunchHandoffEvent`) | **DELETE** (no producer remains after Section 11.1 a) — verify at Stage 4; if a server-driven path enqueues directly into `_pendingLaunchHandoffEvent` without RPC, keep as the single internal API |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` | :1908-1972 (`ConsumePendingLaunchHandoffEvent`) | simplify body — remove `rb.velocity = eventData.SnapshotVelocity` (set 0 always); remove `BuddahPredictedLaunchHandoffResolver.ProjectForArrivalTick` call (no projection needed); flip Locked→Normal flag |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` | :1976-1985 (`RefreshLaunchState`) | resolver advance shrinks to Locked/Normal switch — keep wrapper, drop verbose-log on multi-state transition |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` | :1986-2001 (`ApplyLaunchHandoffInputScaling`) | **DELETE entire method** (BlendAlpha multiplication gone); call site at :499 also DELETE |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` | :2003-2021 (`ApplyLaunchInheritedVelocity`) | **DELETE entire method**; call site at :500 also DELETE |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` | :339 (BuildReplicateData throttle) | NO CHANGE — auto-forward intact |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` | :330-345 (BuildReplicateData) | add Locked-state gate: when Locked, set `movementAllowed = false` regardless of bridge — drives motor.cs:466 early-return + steering=0/throttle=0 (also Locks input via existing `RoomStateManager.ShouldBlockRaceGameplayInput` chain) |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` | :466-473 (MovementAllowed early-return) | NO CHANGE — reused by Locked state |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` | new `#if UNITY_EDITOR` assertion (Locked-state replay determinism G8b) | inside MovementAllowed=false branch, increment a per-replay counter; assert post-replay that AddForce count == 0 |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` | :2143, :2144, :2159, :2160, :2167, :2179, :2182 (Update*HandoffDebug methods) | drop writes to `bootstrap.DebugState.handoffInheritEndTick / handoffBlendEndTick / handoffBlendAlpha` |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` | :1559-1583 (shadow handoff field comparator block under `#if BUDDAH_PREDICTION_SHADOW`) | drop `InheritEndTick / BlendEndTick / BlendAlpha` field-mismatch comparisons; keep `IsActive / EventId / EventTick / SnapshotPosition / SnapshotRotation` comparisons |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchHandoffData.cs` | :14-15, :32-33, :43-44 | **DELETE fields** `InheritDurationTicks` + `BlendDurationTicks`; trim constructor params from 14 → 12 |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchHandoffState.cs` | :11-12, :16, :25-26, :29-31, :40-41, :45 | **DELETE fields** `InheritEndTick / BlendEndTick / BlendAlpha`; rewrite `FromData` factory to compute single `LockedUntilTick = StartTick + LockedDurationTicks` (where `LockedDurationTicks` is the new field); `CurrentState = (LockedDurationTicks > 0) ? Locked : Normal` |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchData.cs` | new field | `LockedDurationTicks: uint` (replaces `InheritDurationTicks + BlendDurationTicks` summed as the lock-window length; in stop-then-countdown design, this is fixed at server-broadcast time = `raceStartTick - countdownStartTick` = ~180 ticks @ 60Hz) |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchState.cs` | :3-7 (enum) | replace `Normal = 0, Inherit = 1, Blend = 2` → `Normal = 0, Locked = 1` |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchHandoffResolver.cs` | :29-58 (`Advance`) | rewrite body: `if (state.IsActive && state.LockedUntilTick > currentTick) state.CurrentState = Locked; else { state.CurrentState = Normal; state.IsActive = false; }`; remove `BlendAlpha` writes |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchHandoffResolver.cs` | :64-95 (`ProjectForArrivalTick`) | **DELETE entire method** — no projection needed when `SnapshotVelocity = Vector3.zero` |

**Type:** ~5 method DELETEs, ~6 field DELETEs, 1 enum value remap, ~3 new lines (Locked-state gate at BuildReplicateData, G8b assertion). Net: ~150 LOC deletion, ~30 LOC addition.

### Area 3 — Server orchestrator / SyncVar extension (contract Section 3.3 → Q3 option c)

| File | Line | Change |
|---|---|---|
| `Assets/Scripts/Network/Room/RoomStateManager.cs` | new SyncVar field (near :51-52) | `private readonly SyncVar<uint> _raceStartTick = new SyncVar<uint>();` |
| `Assets/Scripts/Network/Room/RoomStateManager.cs` | new public property (near :81-93) | `public uint RaceStartTick => _raceStartTick.Value;` |
| `Assets/Scripts/Network/Room/RoomStateManager.cs` | new SyncVar field (near :51-52) | `private readonly HashSet<int> _splineCompleteClientIds = new HashSet<int>();` (server-side only, NOT SyncVar) |
| `Assets/Scripts/Network/Room/RoomStateManager.cs` | new ServerRpc (after :1158) | `[ServerRpc(RequireOwnership = false)] private void NotifySplineCompleteServerRpc(int sequenceId, NetworkConnection caller = null)` — adds to `_splineCompleteClientIds`; if all match → set `_raceStartTick.Value = TimeManager.Tick + countdownTicks;` + `_authoritativeGoIssued.Value = true` |
| `Assets/Scripts/Network/Room/RoomStateManager.cs` | new public method | `public void ReportLocalSplineComplete(int sequenceId)` — owner-side entry, calls `NotifySplineCompleteServerRpc` (mirror of existing `ReportLocalGameplayLive` shape) |
| `Assets/Scripts/Network/Room/RoomStateManager.cs` | new server tick subscription | hook `TimeManager.OnTick += OnServerTick;` in `OnStartServer`; in `OnServerTick`: `if (TimeManager.Tick == _raceStartTick.Value && _authoritativeGoIssued.Value && !_gameplayMovementUnlocked.Value) _gameplayMovementUnlocked.Value = true;` |
| `Assets/Scripts/Network/Room/RoomStateManager.cs` | timeout coroutine | new server coroutine `SplineCompleteTimeoutCoroutine` — 30s wait, abort race if not all reported |
| `Assets/Scripts/RaceIntro/RaceBodyIntroStateController.cs` | inside new `NotifySplineCompleteServer` method | call `RoomStateManager.Instance.ReportLocalSplineComplete(_activeSequenceId)` on owner side when spline elapsed ≥ traversalTime |
| `Assets/Scripts/RaceIntro/IntroSequenceManager.cs` | server-side flow | (likely no change — existing `MarkAuthoritativeGoIssuedServer` chain may need to defer to spline-complete signal; Stage 4 verifies whether the existing chain still fires or is bypassed in Phase 6) |
| `Assets/Scripts/Network/Room/RoomStateManager.cs` | existing `ReportGameplayLiveServerRpc` (:1158-1170) | **may need rewire** — currently this fires `_gameplayMovementUnlocked=true` when handoff consumed; Phase 6 redirects unlock to `_raceStartTick + tick gate` instead. Stage 4 picks: rewire vs leave + add second flip path. Recommend rewire — single source of truth. |

**Type:** 1 new SyncVar, 1 new ServerRpc, 1 new public method, 1 new tick handler, 1 new timeout coroutine, ~2-3 callsite edits. Net: ~80 LOC additions in RoomStateManager + ~10 LOC in RaceBodyIntroStateController.

### Area 4 — Race-Start Timeline trigger (contract Section 3.4)

| File | Change |
|---|---|
| `Assets/Scripts/RaceIntro/RaceStartCinematicController.cs` (NEW) | per Section 3.4.1 sketch: subscribes to `RoomStateManager._authoritativeGoIssued` SyncVar OnChange + `_raceStartTick` SyncVar OnChange; on countdown begin → stop intro Timeline + play `raceStartTimeline`; on race-start tick → no-op (let Timeline ending play out). Late-join skip per Q6 (b.1). |
| `RoomStateManager` | (no change in this area; Timeline controller subscribes to existing SyncVars) |
| Timeline asset at `Assets/Cinematics/RaceStart/RaceStartTimeline.playable` | Yonezawa creates pre-Stage-5; engineer holds null-guarded reference until then |

**Type:** 1 new file (~80 LOC), 0 modifications to existing files. Asset wiring at Inspector level.

### Area 5 — Auto-forward unlock (contract Section 3.5 → Q4 RESOLVED)

| File | Change |
|---|---|
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` | NO new code per Q4 resolution. Locked→Normal transition happens automatically via SyncVar tick gate (Q3) → BuildReplicateData reads `movementAllowed=true` → throttle=1f kicks in. |

**Type:** zero LOC. Section 3.5's Code skeleton (`UnlockFromRaceStart` method) is NOT NEEDED — the existing chain handles it.

### Area 6 — Documentation + scope flags (Section 4 amendment + Lessons-log + OQ2)

| File | Change |
|---|---|
| `Docs/phase-gates/active/phase6-race-start-handoff-redesign-contract.md` | Section 4 add bullet "Legacy mode handoff path (`runtimeMode = Legacy`) — pre-Phase-6 velocity-inherit pattern retained; Phase 6 applies to PredictionV2 only" |
| `Assets/Scripts/Buddah/BuddahMovement.cs` near :539 | one-line comment `// Phase 6: Legacy mode retains pre-Phase-6 velocity-inherit. PredictionV2 race-start uses stop-then-countdown via prediction motor.` |
| `Docs/lessons-log.md` | likely no new L# (Phase 6 introduces new architecture, not a failure-mode discovery); if Stage 5 SMOKE exposes a lesson, append at that point per Methodology Rule 5 |

**Type:** ~3 single-line edits.

### Stage 4 IMPLEMENT TODO (sequenced)

1. **Area 2 simplification first** (motor + handoff structs) — surface code shrinks, enables clean Area 3 wire-up
2. **Area 1 spline formula** — independent, can run parallel to Area 2
3. **Area 3 server orchestrator** (depends on Area 2's motor MovementAllowed gate landing first; depends on Area 1's `NotifySplineCompleteServerRpc` plumbing)
4. **Area 4 Timeline controller** — independent of Areas 1-3; just SyncVar OnChange subscription
5. **Area 5** — verify zero code change (regression check via greps after Areas 1-4 land)
6. **Area 6 docs** — last commit before Stage 5 SMOKE handoff

Branch already cut: `feat/phase6-race-start-handoff-redesign` (this branch). All 5 area commits land here.

---

## Cross-cutting concerns

**G4 same-tick unlock vs SyncVar broadcast latency:** SyncVar OnChange fires on each client when the receive buffer processes the update; latency is bounded by FishNet's tick latency (~1-2 ticks at typical Steam P2P). The unlock decision is NOT made at OnChange — it's made by per-tick gate `LocalTick >= _raceStartTick.Value`. So even if SyncVar arrives late (within a few ticks of `_raceStartTick`), each client unlocks at the same `_raceStartTick` deterministically up to ±1 tick.

**Reconcile during Phase 3 Lock (G8b):** verified structurally feasible (Q9 disposition); Stage 4 IMPLEMENT adds the `#if UNITY_EDITOR` assertion + Stage 5 SMOKE captures evidence + Stage 6 VERIFY greps.

**Spline-decel formula at low T:** if Yonezawa sets T very small (e.g., 2s), `v_max = 2L/T` becomes very large (e.g., 360m / 2s = 180 m/s). Spline driver writes rb.position directly; no physics integration; safe at any v_max. But Phase 1's "缓缓降低" UX intent requires T ≥ ~5s for natural feel. Stage 4 documents `[Min(5f)]` on the SerializeField (Inspector clamp), or warns if T<5.

**FishNet TickRate dependency for `countdownTicks`:** `countdownTicks = Mathf.RoundToInt(3f / TimeManager.TickDelta)` derives 180 ticks at 60Hz. If TickRate changes (e.g., to 50Hz for some build variant), the Phase 3 Lock duration adjusts automatically — no hardcoded 180. Robust.

---

## Awaiting sign-off

Reviewer (cowork-reviewer) Stage 3 verify checklist:
1. **Q-disposition coherence** — Q2 (B) / Q3 (c) / Q5 placeholder / Q6 (b.1) / Q7 no-op / Q8 wait-all+timeout / OQ2 doc-only — each justified by RECON or verify-report evidence
2. **Section 11.x integration** — 11.1 firm option (a); 11.2 G8b elevated; 11.3 file list expansion; 11.4 Stage 6 baseline split noted
3. **Implementation file list completeness** — 6 areas, ~25 distinct edit points, branch + sequencing plan
4. **Cross-cutting concerns** — G4 latency, G8b feasibility, spline-decel low-T, TickRate dependency

Methodology Rule 12 honored — contract Section 9 Design row stays ⏸ for reviewer. No pre-fill.
