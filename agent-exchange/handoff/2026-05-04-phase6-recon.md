# phase6-race-start-handoff-redesign — Recon Report

**Branch:** `feat/phase7-visual-jitter-evaluation` @ `8314b5d` (Phase 6 branch NOT cut yet — Stage 4 will cut from dev tip post Phase 7.5-A.1 ship per contract Section 9 Kickoff note; this recon runs read-only on the current dev tip surrogate per Stage 2 authorization)
**Status:** RECON ONLY — no code changes; no Q-answers yet; no contract sign-off rows touched (Methodology Rule 12 honored — Recon row in Section 9 stays ⏸ for cowork-reviewer to author independently after verifying this report).
**Scope reminder:** Replace velocity-inherit/Blend handoff state machine at race-start with stop-then-countdown pattern (5-phase architecture per contract Section 2). Motor + spline driver + server orchestrator + Race-Start Timeline + UI.
**Verify discipline:** All claims below backed by `git show HEAD:<path>` or `git grep <pattern> HEAD` per L22 + Methodology Rule 2. No Read-on-working-tree as primary evidence.

---

## 0. Harness helper output (Step 3 of Mandatory Execution Order)

```
> powershell -ExecutionPolicy Bypass -Command "& 'Tools/Harness/Get-BuddahGoHarnessContext.ps1' -Intent refactor -Systems prediction,room-session,config-rules"
Project: BuddahGo
Intent: refactor
Selection mode: explicit
Recommended skill: buddahgo-safe-refactor

Systems:
- [prediction] Prediction Core    Paths: Assets/Scripts/New_Buddah/
- [room-session] Room and Session Orchestration   Paths: Assets/Scripts/Network/Core/, Assets/Scripts/Network/Room/
- [config-rules] Config and Rules    Paths: Assets/Scripts/Config/

Protected files:
- Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs
- Assets/Scripts/New_Buddah/Core/BuddahPredictedReconcileData.cs
- Assets/Scripts/Network/Core/GameNetworkManager.cs
- Assets/Scripts/Network/Core/ConnectionManager.cs
- Assets/Scripts/Network/Room/RoomStateManager.cs
- Assets/Scripts/Config/Runtime/ProjectConfigRuntime.cs

Stop conditions: reconcile/rollback/desync/tick-drift mention; protected file edit
without approval; cross-system in single pass; rename serialized fields / scene names.

Active Phase Gate:
- contract: Docs/phase-gates/active/phase7-contract.md  (Stage 4 IMPLEMENT, last signed 2026-05-03)
- contract: Docs/phase-gates/active/v2b-step1-contract.md  (stale; previously flagged in phase7 RECON Section "Other observations")
```

Note: Phase 6 contract `Docs/phase-gates/active/phase6-race-start-handoff-redesign-contract.md` is the operative document for this recon, but the harness helper currently surfaces Phase 7 + v2b-step1 as the "Active Phase Gate" entries because the helper detects active-state by ledger-row date pattern, not file name. This is **NOT a recon blocker** — Phase 6 contract is the explicit authorization document for this RECON; helper surfacing is a separate housekeeping observation parallel to phase7-recon's "stale active contract" note.

All 6 protected files in scope are touched in this RECON's READ-ONLY surface inventory; no edits.

---

## 1. Motor handoff code path (motor.cs)

[BuddahPredictedMotor.cs](Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs) at HEAD (`8314b5d`).

### 1.1 RunInputs handoff invocation order (motor.cs:380–510)

```
git show HEAD:Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs | sed -n '395,420p'
```
Excerpt (RunInputs early section):
```csharp
RefreshLaunchState(currentTick);                                    // line 396 (pre-consume advance)
_computedStats = BuddahPredictedModifierResolver.Resolve(_modifierState, config, currentTick);
ApplyResolvedMassMultiplier();
_impulseConsumedThisTick = false;
// ... shadow pre-snapshots under #if BUDDAH_PREDICTION_SHADOW ...
ConsumePendingTeleportEvent(currentTick);                           // line 415
ConsumePendingLaunchHandoffEvent(currentTick);                      // line 416  ← entry point
ConsumePendingImpulseEvents_Authoritative(currentTick);
RefreshLaunchState(currentTick);                                    // line 420 (post-consume advance)
```

The post-consume `RefreshLaunchState` at :420 is what writes `_handoffState.CurrentState` (Inherit/Blend/Normal) used downstream for input scaling and velocity blending.

### 1.2 Mid-tick handoff state application (motor.cs:497–500)

```csharp
float resolvedThrottle = data.Throttle;
float resolvedSteering = data.Steering * _computedStats.FinalSteeringSign;
ApplyLaunchHandoffInputScaling(currentTick, ref resolvedThrottle, ref resolvedSteering);   // :499
ApplyLaunchInheritedVelocity(currentTick);                                                  // :500
```

### 1.3 Throttle / steering input gates (motor.cs:1986–1999) — `ApplyLaunchHandoffInputScaling` body

```csharp
private void ApplyLaunchHandoffInputScaling(uint currentTick, ref float throttle, ref float steering)
{
    RefreshLaunchState(currentTick);
    if (!_handoffState.IsActive && _handoffState.CurrentState == BuddahPredictedLaunchState.Normal)
        return;
    switch (_handoffState.CurrentState)
    {
        case BuddahPredictedLaunchState.Inherit:
            throttle = 0f;
            steering = 0f;
            break;
        case BuddahPredictedLaunchState.Blend:
            throttle *= _handoffState.BlendAlpha;
            steering *= _handoffState.BlendAlpha;
            break;
    }
}
```

This is the BlendAlpha multiplication site contract Section 3.2.1 calls out for deletion. Exact lines:

| Line | Code | Phase 6 disposition |
|---|---|---|
| 1997 | `throttle *= _handoffState.BlendAlpha;` | Delete (along with whole switch) |
| 1998 | `steering *= _handoffState.BlendAlpha;` | Delete |

### 1.4 `ConsumePendingLaunchHandoffEvent` body (motor.cs:1908–1972)

```csharp
private void ConsumePendingLaunchHandoffEvent(uint currentTick)
{
    if (!_hasPendingLaunchHandoffEvent || rb == null) return;
    if (_pendingLaunchHandoffEvent.StartTick > currentTick) return;

    BuddahPredictedLaunchHandoffData eventData = _pendingLaunchHandoffEvent;
    _hasPendingLaunchHandoffEvent = false;
    _awaitingAuthoritativeLaunchHandoff = false;
    _localPreHandoffBypassUntilTick = currentTick;
    uint preAdjustStartTick = eventData.StartTick;
    float tickDeltaSeconds = TimeManager != null ? (float)TimeManager.TickDelta : 0f;
    eventData = BuddahPredictedLaunchHandoffResolver.ProjectForArrivalTick(
        eventData, currentTick, tickDeltaSeconds);                  // :1922 — stale-projection
    // ... [HandoffDebug] verbose logs ...
    _lastConsumedLaunchHandoffEventId = eventData.EventId;
    _handoffState = BuddahPredictedLaunchHandoffState.FromData(eventData);
    // ... shadow scratch updates under #if BUDDAH_PREDICTION_SHADOW ...

    Vector3 preVelocity = rb.velocity;
    if (bootstrap != null)
        bootstrap.DebugState.preHandoffSpeed = new Vector3(preVelocity.x, 0f, preVelocity.z).magnitude;

    _introControlActive = false;
    _externalKinematicControlActive = false;
    rb.isKinematic = false;
    _predictionRigidbody.ClearPendingForces();
    rb.position = eventData.SnapshotPosition;                       // :1948 ← contract refers to this as ":1937" (drift)
    rb.rotation = eventData.SnapshotRotation;
    rb.velocity = eventData.SnapshotVelocity;                       // velocity inheritance (Phase 6: must become Vector3.zero)
    rb.angularVelocity = eventData.SnapshotAngularVelocity;
    rb.Sleep();
    rb.WakeUp();
    InitializePredictionRigidbody();
    _splineProgressTracker?.SnapToWorldPosition(eventData.SnapshotPosition);

    if (eventData.SuppressSteeringDurationTicks > 0u)
        _modifierState.SuppressSteeringUntilTick = Math.Max(...);
    if (eventData.RoomBypassDurationTicks > 0u)
        _modifierState.RoomBypassUntilTick = Math.Max(...);

    RefreshLaunchState(currentTick);
    UpdateConsumedHandoffDebug(currentTick, eventData);
    Debug.Log($"[IntroHandoff][Prediction] Handoff consumed eventId=...");
    if (IsOwner && eventData.DebugSequenceId >= 0)
        RoomStateManager.Instance?.ReportLocalGameplayLive(eventData.DebugSequenceId);   // :1968
}
```

**KEY observation 1.4** — line 1968 is a **non-trivial cross-system coupling**: motor's consume callback fires `RoomStateManager.Instance.ReportLocalGameplayLive(seq)`. This is the existing same-tick "all clients live" signal that drives `_gameplayMovementUnlocked` SyncVar. See Surface 5.

### 1.5 `RefreshLaunchState` (motor.cs:1976–1985) — thin wrapper over `BuddahPredictedLaunchHandoffResolver.Advance`

```csharp
private void RefreshLaunchState(uint currentTick)
{
    BuddahPredictedLaunchHandoffState advanced =
        BuddahPredictedLaunchHandoffResolver.Advance(_handoffState, currentTick);
    if (bootstrap != null && _handoffState.IsActive
        && _handoffState.CurrentState != advanced.CurrentState)
        bootstrap.LogVerbose(...);
    _handoffState = advanced;
}
```

Resolver body (state-machine logic) lives at `BuddahPredictedLaunchHandoffResolver.Advance` — see Surface 2.4. Phase 6 simplification: collapse `Inherit/Blend/Normal` → `Locked/Normal`.

### 1.6 `ApplyLaunchInheritedVelocity` (motor.cs:2003–2021) — full body

```csharp
private void ApplyLaunchInheritedVelocity(uint currentTick)
{
    if (rb == null || _predictionRigidbody == null) return;
    RefreshLaunchState(currentTick);
    if (_handoffState.CurrentState == BuddahPredictedLaunchState.Normal) return;
    Vector3 currentVelocity = rb.velocity;
    Vector3 inheritedPlanar = new Vector3(_handoffState.SnapshotVelocity.x, 0f, _handoffState.SnapshotVelocity.z);
    Vector3 currentPlanar = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
    float blend01 = _handoffState.CurrentState == BuddahPredictedLaunchState.Inherit
        ? 0f : _handoffState.BlendAlpha;
    Vector3 planar = Vector3.Lerp(inheritedPlanar, currentPlanar, Mathf.Clamp01(blend01));
    SetPredictionVelocitiesSafely(new Vector3(planar.x, currentVelocity.y, planar.z), Vector3.zero);
    if (bootstrap != null) bootstrap.DebugState.postHandoffSpeed = planar.magnitude;
}
```

Phase 6 disposition: **DELETE entire method** (Section 3.2.1).

### 1.7 Reconcile callback (motor.cs:547–588) — interaction with handoff state

```csharp
[Reconcile]
private void ReconcileState(BuddahPredictedReconcileData data, Channel channel = Channel.Unreliable)
{
    _reconcileCallbackCount++;
    if (_predictionRigidbody == null || data.RigidbodyState == null) return;
    Vector3 preReconcilePosition = rb != null ? rb.position : Vector3.zero;
    Vector3 preReconcileVelocity = rb != null ? rb.velocity : Vector3.zero;
    bool introControlled = data.IntroControlActive || data.ExternalKinematicControlActive;
    bool authoritativePending = IsPredictionAuthoritativeHandoffPending();
    bool skipOwnerIntroReconcile = ShouldSkipTransformReconcileDuringIntroOrPendingHandoff(...);
    if (!skipOwnerIntroReconcile)
        _predictionRigidbody.Reconcile(data.RigidbodyState);
    Vector3 postReconcilePosition = rb != null ? rb.position : Vector3.zero;
    // ... debug-state writes only after this point
}
```

Reconcile does NOT itself touch `_handoffState`. Replay determinism for Locked-state (contract Section 11.2 / G8b candidate) lives in `RunInputs` early-return path:

```
git show HEAD:Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs | sed -n '465,475p'
→
if (!data.MovementAllowed)
{
    _predictionRigidbody.ClearPendingForces();
    SetPredictionVelocitiesSafely(Vector3.zero, Vector3.zero);
    _predictionRigidbody.Simulate();
    FinalizeImpulseDebugAfterSimulate();
    UpdateReplicateDebug(data, state, "blocked");
    return;
}
```

If Phase 6 wires Locked-state to drive `data.MovementAllowed = false` during Phase 3 (per contract Section 3.2.2 sketch's "input ignored at gate" intent), the existing :466 early-return path produces **zero force application + zero velocity write through `_predictionRigidbody`** every replay. Reconcile replays of any tick T inside `[countdownStartTick, raceStartTick)` would all hit this branch deterministically. **G8b is achievable by re-using `MovementAllowed` rather than adding a new gate.** This is information for Stage 3 design Q — RECON does not pick a path.

### 1.8 Other rb.position direct writes (motor.cs:1873 + 1948 cited by contract)

```
git show HEAD:Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs | grep -nE "^\s+rb\.position\s*="
→
1873:            rb.position = eventData.TargetPosition;     (inside ConsumePendingTeleportEvent — teleport path; Phase 6 OUT-OF-SCOPE per contract Section 4)
1948:            rb.position = eventData.SnapshotPosition;   (inside ConsumePendingLaunchHandoffEvent — Phase 6 IN-SCOPE; line drifted from contract's :1937 cite)
```

**KEY FINDING 1.8**: contract Section 1.1 cites `motor.cs:1873 (teleport rb.position write) / 1937 (handoff rb.position write) / 1976 (RefreshLaunchState) / 2003 (ApplyLaunchInheritedVelocity)`. The HEAD lines are **1873 / 1948 / 1976 / 2003** — only the handoff-rb-write line drifted (1937 → 1948). Likely cause: phase7 commits between contract draft and current dev tip. Stage 4 should re-locate via the HEAD line, not the contract line. Contract Section 3.2.1's "around line 1937-1970" range still covers 1948 within +11 tolerance.

---

## 2. Handoff data structures

### 2.1 [BuddahPredictedLaunchHandoffData.cs](Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchHandoffData.cs)

```
git show HEAD:Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchHandoffData.cs
```
Fields (struct):
```
EventId                  uint
StartTick                uint
SnapshotPosition         Vector3
SnapshotRotation         Quaternion
SnapshotVelocity         Vector3
SnapshotAngularVelocity  Vector3
SnapshotForward          Vector3
InheritDurationTicks     uint    ← Phase 6 wire-format Q1 candidate: keep zero / remove
BlendDurationTicks       uint    ← Phase 6 wire-format Q1 candidate: keep zero / remove
SuppressSteeringDurationTicks  uint
RoomBypassDurationTicks  uint
DebugSequenceId          int
EnableDebugLogs          bool
```
Constructor (lines 21-46) takes 14 params in fixed order — consumed by motor's `RequestLaunchHandoffServerRpc` payload + `QueueLaunchHandoffTargetRpc` payload (motor.cs:898-908 + :1027-1056). Wire-format change requires both RPC signatures to be edited atomically — Q1 disposition is non-trivial.

### 2.2 [BuddahPredictedLaunchHandoffState.cs](Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchHandoffState.cs)

Fields (struct):
```
IsActive                 bool
EventId / EventTick / StartTick   uint
InheritEndTick           uint   ← Phase 6 candidate for removal
BlendEndTick             uint   ← Phase 6 candidate for removal
SuppressSteeringUntilTick / RoomBypassUntilTick   uint
CurrentState             BuddahPredictedLaunchState (enum)
BlendAlpha               float  ← Phase 6 candidate for removal
SnapshotPosition / SnapshotRotation / SnapshotVelocity / SnapshotAngularVelocity / SnapshotForward
```

`FromData(BuddahPredictedLaunchHandoffData data)` static factory (lines 22-50) computes:
- `inheritEndTick = StartTick + InheritDurationTicks`
- `blendEndTick = inheritEndTick + BlendDurationTicks`
- initial `state = Inherit if InheritDurationTicks > 0 else Blend if BlendDurationTicks > 0 else Normal`
- `BlendAlpha = state == Blend ? 0f : (state == Normal ? 1f : 0f)`

Phase 6: `FromData` simplifies — every Locked->Normal handoff has `state = Locked` (or `Normal` post-unlock); BlendAlpha disappears.

### 2.3 [BuddahPredictedLaunchState.cs](Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchState.cs)

```csharp
public enum BuddahPredictedLaunchState : byte
{
    Normal = 0,
    Inherit = 1,
    Blend = 2
}
```

Phase 6 redesign: replace with `Normal = 0, Locked = 1` (per contract Section 3.2.1). NOTE: enum order/byte values touch wire format **iff** the enum is serialized — verify at Stage 3. (Quick scan: enum is held in `BuddahPredictedLaunchHandoffState.CurrentState` — this struct is NOT in `BuddahPredictedReconcileData` per inspection; reconciles only carry `LastConsumedHandoffId`. So enum value remap is local-only and wire-safe. Stage 3 must re-confirm.)

### 2.4 [BuddahPredictedLaunchHandoffResolver.cs](Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchHandoffResolver.cs)

Two pure-static methods (zero deps): `Advance(state, currentTick)` and `ProjectForArrivalTick(eventData, currentTick, tickDeltaSeconds)`.

`Advance` body (excerpt — full state machine):
```csharp
if (!state.IsActive) {
    state.CurrentState = Normal; state.BlendAlpha = 1f; return state;
}
if (state.InheritEndTick > currentTick && state.InheritEndTick > state.StartTick) {
    state.CurrentState = Inherit; state.BlendAlpha = 0f;
}
else if (state.BlendEndTick > currentTick && state.BlendEndTick > state.InheritEndTick) {
    state.CurrentState = Blend;
    uint blendTicks = state.BlendEndTick - state.InheritEndTick;
    uint elapsedBlendTicks = currentTick > state.InheritEndTick
        ? currentTick - state.InheritEndTick : 0u;
    state.BlendAlpha = blendTicks > 0u ? Mathf.Clamp01((float)elapsedBlendTicks / blendTicks) : 1f;
}
else {
    state.CurrentState = Normal; state.BlendAlpha = 1f; state.IsActive = false;
}
return state;
```

`ProjectForArrivalTick` projects stale `eventData.SnapshotPosition` forward by `staleTicks * tickDeltaSeconds * SnapshotVelocity`. Used at motor.cs:1922 to forward-project a handoff event whose `StartTick` is older than `currentTick` at consume.

**Phase 6 disposition (per contract Section 3.2.1)**:
- `Advance`: collapse to `if (state.IsActive && state.LockedUntilTick > currentTick) → Locked, else → Normal & IsActive=false`. Removes `InheritEndTick`/`BlendEndTick`/`BlendAlpha` reads.
- `ProjectForArrivalTick`: NO LONGER NEEDED if SnapshotVelocity = Vector3.zero (projection is identity). Phase 6 may delete or short-circuit. Stage 3 should pick deletion.

### 2.5 Cross-file references to handoff-state machinery (full inventory)

```
git grep -n "ConsumePendingLaunchHandoffEvent|RefreshLaunchState|ApplyLaunchInheritedVelocity|RequestAuthoritativeLaunchHandoffFromOwner|BlendAlpha|InheritEndTick|BlendEndTick|InheritDurationTicks|BlendDurationTicks" HEAD -- 'Assets/*.cs'
```

| File | Line(s) | Symbol | Phase 6 disposition |
|---|---|---|---|
| `Assets/Scripts/Buddah/BuddahMovement.cs` | 539, 561, 571 | `ApplyLaunchInheritedVelocity` (legacy non-prediction path) | Delete if removing legacy path; otherwise leave (Phase 6 only touches prediction path per contract Section 4 out-of-scope on legacy). Stage 3 disposition. |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchHandoffData.cs` | 14, 15, 43, 44 | InheritDurationTicks / BlendDurationTicks fields | Q1 wire-format decision |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchHandoffResolver.cs` | 23-55 | full Advance() body | Collapse to Locked/Normal |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchHandoffState.cs` | 11, 12, 16, 25, 26, 29, 31, 40, 41, 45 | InheritEndTick / BlendEndTick / BlendAlpha fields + FromData() math | Drop fields + simplify factory |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` | 126 (comment), 396, 416, 420, 431, 433, 500, 768, 862, 927, 963, 964, 976, 1208, 1209, 1511, 1559, 1561, 1564, 1566, 1580, 1583, 1908, 1962, 1976, 1986, 1997, 1998, 2003, 2008, 2015, 2143, 2144, 2159, 2160, 2167, 2179, 2182 | All handoff-side machinery + shadow compares + DebugState mirrors | Touch all (delete dead lines, simplify state machine) |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictionTickContext.cs` | 42 (comment) | references `ConsumePendingLaunchHandoffEvent` in shadow doc-comment | Update comment |
| `Assets/Scripts/New_Buddah/Debug/BuddahPredictionDebugOverlay.cs` | 92, 124 | OnGUI rendering of handoffInheritEndTick / handoffBlendEndTick / handoffBlendAlpha | Remove dropped-field references |
| `Assets/Scripts/New_Buddah/Debug/BuddahPredictionDebugState.cs` | 61, 79, 80 | handoffBlendAlpha / handoffInheritEndTick / handoffBlendEndTick fields | Drop |
| `Assets/Scripts/New_Buddah/Integration/BuddahPredictionHandoffBridge.cs` | 161 | `predictedMotor.RequestAuthoritativeLaunchHandoffFromOwner(...)` | See Surface 4 disposition |
| `Assets/Scripts/New_Buddah/Simulation/BuddahHandoffStep.cs` | 9, 11, 12, 19 | shadow step doc-comments + body | Update if shadow step kept; the shadow compares blend/inherit/alpha will become vacuous |
| `Assets/Scripts/New_Buddah/Simulation/BuddahPredictionShadowScratch.cs` | 50, 52 | ShadowHandoffState / ShadowLastConsumedHandoffId comments + fields | Update |

This is the complete deletion / edit footprint. Section 3.2.1 of the contract names ~5 files explicitly; the actual touch list is ~12 files. Section 11.3 reviewer observation ("Section 3.2.1 file list completeness") is **CONFIRMED** — see Surface 3 + Surface 5b for the missing files.

---

## 3. Bridge + spline-side driver (★ Section 11.3 reviewer observation)

### 3.1 [BuddahPredictionHandoffBridge.cs](Assets/Scripts/New_Buddah/Integration/BuddahPredictionHandoffBridge.cs) (full)

```
git show HEAD:Assets/Scripts/New_Buddah/Integration/BuddahPredictionHandoffBridge.cs  → 173 lines
```
Surface (relevant methods):

| Method | Returns | Purpose | Phase 6 disposition |
|---|---|---|---|
| `IsPredictionHandoffActive()` | bool | bootstrap+motor present check | KEEP |
| `IsLaunchHandoffActive()` | bool | wraps `predictedMotor.IsLaunchHandoffActive` | KEEP (Locked-state still active) |
| `IsPredictionIntroControlActive()` | bool | wraps motor flag | KEEP |
| `IsPredictionExternalKinematicControlActive()` | bool | wraps motor flag | KEEP |
| `IsAuthoritativeLaunchHandoffPending()` | bool | wraps motor flag | KEEP (still meaningful pre-Locked) |
| `IsAuthoritativeLaunchHandoffConsumedOrActive()` | bool | wraps motor flag | KEEP |
| `GetPredictionControlState()` / `TryGetPredictionControlState(out struct)` / `TryGetPredictionControlState(out 3 bools)` | struct / bool | snapshot 4 motor flags | KEEP |
| `LogBridgeStateSnapshot(state)` | void | dedup + Debug.Log | KEEP |
| `TrySetIntroControlActive(bool)` | bool | proxy to motor | KEEP |
| `TrySetExternalKinematicControlActive(bool)` | bool | proxy to motor | KEEP |
| `TryBeginLaunchHandoff(snapshot, inherit, blend, bypass, suppress, clearAng, seq, dbg)` | bool (line 148-165) | proxy to `predictedMotor.RequestAuthoritativeLaunchHandoffFromOwner` | **DELETE** if the L12 chain is removed — see Surface 4 |

**KEY OBSERVATION 3.1**: the bridge plumbing is structured so that EVERY caller of the L12 owner→server→owner chain enters via `TryBeginLaunchHandoff` at line 148. There is no other entry. Deleting `TryBeginLaunchHandoff` and its `RequestAuthoritativeLaunchHandoffFromOwner` target removes the entire owner-initiated chain.

### 3.2 [BuddahMovement.cs](Assets/Scripts/Buddah/BuddahMovement.cs) handoff plumbing

```
git grep -n "SyncPredictionHandoffStateFromBridge|RequestAuthoritativeLaunchHandoffFromOwner|HandoffBridge|launchHandoffBridge|_handoffBridge" HEAD -- 'Assets/Scripts/Buddah/BuddahMovement.cs'
```

| Line | Symbol | Purpose | Phase 6 disposition |
|---|---|---|---|
| 47 | `[SerializeField] private BuddahPredictionHandoffBridge predictionHandoffBridge;` | inspector ref | KEEP |
| 74 | `public bool IsLaunchHandoffActive => ...` | gameplay-side state query | KEEP |
| 88-89, 351-352, 358 | bridge runtime resolution / AddComponent fallback | `TryGetPredictionHandoffBridge` | KEEP (still need bridge-active check during Locked) |
| 231 | `bridge.TrySetIntroControlActive(active)` (in `SetIntroControlActive`) | KEEP |
| 244-263 | `BeginLaunchHandoff(snapshot, bypass, suppress, clearAng, seq, dbg)` (full body — calls `bridge.TryBeginLaunchHandoff(...)`) | **DELETE entire method** if L12 chain removed (see Surface 4) |
| 315 | `bridge.TrySetExternalKinematicControlActive(active)` | KEEP |
| 365-399 | `SyncPredictionHandoffStateFromBridge()` (full body — mirrors 4 motor flags into BuddahMovement's local state for legacy gameplay-input gating) | KEEP — still needed; Phase 6 Locked state must still surface to BuddahMovement (input-gate side; e.g., camera + skill-input gate). Phase 6 may simplify the 4 flags to fewer once Inherit/Blend gone |
| 452 | call site of `SyncPredictionHandoffStateFromBridge` (inside `RefreshLocalControlState`) | KEEP |
| 539, 561, 571-589 | legacy non-prediction `ApplyLaunchInheritedVelocity(blend01)` + `UpdateLaunchState` state machine (`_launchState`/`launchBlendTime`/`_launchInheritedVelocity`) | OUT-OF-SCOPE for Phase 6 (legacy non-prediction path; contract Section 4 limits Phase 6 to predicted path). Stage 3 confirm. |
| 635 | `bool movementUnlocked = room != null && room.IsGameplayMovementUnlocked;` (gameplay-input gate) | KEEP — this is part of the existing all-clients-live signal chain (Surface 5) |

### 3.3 [RaceBodyIntroStateController.cs](Assets/Scripts/RaceIntro/RaceBodyIntroStateController.cs) — spline-side driver entry to handoff

`RaceBodyIntroStateController` is the per-buddah intro controller (file size 540+ lines). Phases:
```
IntroPhase {
    None,
    IntroKinematic,    // spline kinematic, _visualStarted=false→true between visual-start RPC and authoritative-go
    HandoffWindow,     // approaching go time (handoffStartTime = goTime - GetHandoffLeadTime())
    RaceLive           // post-go; spline relinquished
}
```

**Spline drive (FixedUpdate, lines 100-118):**
```csharp
private void FixedUpdate()
{
    if (!_hasAssignment || _assignedPath == null || targetRigidbody == null
        || _goApplied || !_visualStarted) return;
    double now = GetSmoothedNetworkTimeSeconds();
    TryCompleteAuthoritativeGoTransition(now);
    if (_goApplied) return;
    if (!TryGetResolvedTiming(out IntroSequenceTiming timing)) return;
    DriveSplinePose(IntroTimeUtility.GetClampedIntroNetworkTime(timing, now));
}

private void DriveSplinePose(double networkTime)
{
    SampleSnapshotAtTime(networkTime, out LaunchHandoffSnapshot snapshot);
    _latestSplineSnapshot = snapshot;
    if (targetRigidbody == null) return;
    float normalizedT = GetNormalizedDistanceT(networkTime);
    Vector3 prePosition = targetRigidbody.position;
    targetRigidbody.position = snapshot.Position;     // single-writer rb.position assignment
    targetRigidbody.rotation = snapshot.Rotation;
    MaybeLogSplineDriveDiagnostics(networkTime, normalizedT, prePosition, snapshot);
}
```

**Speed model (lines 422-443):**
```csharp
private float GetNormalizedDistanceT(double networkTime)
{
    if (_assignedPath == null) return 0f;
    double elapsedSeconds = networkTime - _resolvedIntroStartNetworkTime;
    if (elapsedSeconds <= 0) return 0f;
    float distance = (float)elapsedSeconds * GetIntroSpeedMetersPerSecond();
    float totalLength = Mathf.Max(0.0001f, _assignedPath.TotalLength);
    return _assignedPath.TAtDistance(Mathf.Min(distance, totalLength));
}
private float GetIntroSpeedMetersPerSecond()
    => Mathf.Max(0.1f, _assignment.introSpeedMetersPerSecond);
```

**KEY FINDING 3.3** — current spline driver uses **constant-velocity** traversal: `distance = elapsed × constantSpeed`. Speed comes from `IntroAssignmentData.introSpeedMetersPerSecond` (set at server-side intro assignment in [IntroSequenceManager.cs:39](Assets/Scripts/RaceIntro/IntroSequenceManager.cs#L39) `[SerializeField, Min(0.1f)] private float introSpeedMetersPerSecond = 20f;`). Phase 6 Section 3.1 (Yonezawa-input parameter T + linear/quadratic deceleration formula) **REPLACES** this constant-velocity model with a time-based traversal where:
- Total time `T` is the new Yonezawa parameter (replaces `introSpeedMetersPerSecond`).
- `v(t) = v_max × (1 - t/T)` (linear) computes per-tick distance step.
- Endpoint velocity = 0 (replaces "still going at 20m/s when handoff fires").

Per Section 11.3, this is **the** reason Section 3.2.1's file list is incomplete — the spline driver is the entire other half of the redesign. Stage 4 IMPLEMENT must touch:
- `RaceBodyIntroStateController.GetNormalizedDistanceT` — rewrite distance formula
- `IntroAssignmentData.introSpeedMetersPerSecond` — replace with `traversalTimeSeconds` (or compute v_max from total length + T at intro-assignment time)
- `IntroSequenceManager` — Inspector field rename + assignment payload field swap
- `SplineIntroPath.TotalLength` — already exposes arc-length; reusable as `L` in formula

**Handoff trigger (line 329-336)** — `CompleteGoTransition`:
```csharp
if (movementController != null && isLocalOwner)
{
    _runtimeState = IntroRuntimeState.AuthoritativeHandoffPending;
    RoomStateManager.Instance?.ReportLocalGameplayLive(_activeSequenceId);
    Debug.Log($"[IntroHandoff][Body:{name}] Local owner launching handoff seq={_activeSequenceId}.");
    movementController.BeginLaunchHandoff(
        snapshot,
        Mathf.Max(0.1f, GetHandoffLeadTime()),
        0.15f,
        false,
        _activeSequenceId,
        false);
}
```

This is the SOLE call site of `BuddahMovement.BeginLaunchHandoff` in the codebase (verified by `git grep -n "movementController.BeginLaunchHandoff" HEAD -- 'Assets/*.cs'` → 1 hit). Therefore the entire owner-side L12 owner→server→owner chain is exercised ONLY by `RaceBodyIntroStateController.CompleteGoTransition`. Surface 4 confirms.

---

## 4. ★ Section 11.1 L12 caller inventory of `RequestAuthoritativeLaunchHandoffFromOwner`

### 4.1 Direct caller of `predictedMotor.RequestAuthoritativeLaunchHandoffFromOwner`

```
git grep -n "RequestAuthoritativeLaunchHandoffFromOwner" HEAD -- 'Assets/*.cs'
→
HEAD:Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs:862:        public bool RequestAuthoritativeLaunchHandoffFromOwner(
HEAD:Assets/Scripts/New_Buddah/Integration/BuddahPredictionHandoffBridge.cs:161:            return predictedMotor.RequestAuthoritativeLaunchHandoffFromOwner(
```
**Exactly ONE caller** of the motor method: [BuddahPredictionHandoffBridge.TryBeginLaunchHandoff](Assets/Scripts/New_Buddah/Integration/BuddahPredictionHandoffBridge.cs#L148) at line 161.

### 4.2 Direct caller of `bridge.TryBeginLaunchHandoff`

```
git grep -n "TryBeginLaunchHandoff" HEAD -- 'Assets/*.cs'
→
HEAD:Assets/Scripts/Buddah/BuddahMovement.cs:247:            && bridge.TryBeginLaunchHandoff(
HEAD:Assets/Scripts/New_Buddah/Integration/BuddahPredictionHandoffBridge.cs:148:        public bool TryBeginLaunchHandoff(
```
**Exactly ONE caller**: [BuddahMovement.BeginLaunchHandoff](Assets/Scripts/Buddah/BuddahMovement.cs#L244) at line 247.

### 4.3 Direct caller of `BuddahMovement.BeginLaunchHandoff`

```
git grep -n "movementController.BeginLaunchHandoff\|\.BeginLaunchHandoff(" HEAD -- 'Assets/*.cs'
→
HEAD:Assets/Scripts/Buddah/BuddahMovement.cs:244:    public void BeginLaunchHandoff(LaunchHandoffSnapshot snapshot, ...)
HEAD:Assets/Scripts/RaceIntro/RaceBodyIntroStateController.cs:332:                movementController.BeginLaunchHandoff(
```
**Exactly ONE caller**: [RaceBodyIntroStateController.CompleteGoTransition](Assets/Scripts/RaceIntro/RaceBodyIntroStateController.cs#L307) at line 332.

### 4.4 Direct caller of `RaceBodyIntroStateController.CompleteGoTransition`

```
git grep -n "CompleteGoTransition" HEAD -- 'Assets/*.cs'
→
HEAD:Assets/Scripts/RaceIntro/RaceBodyIntroStateController.cs:307:    private void CompleteGoTransition()
HEAD:Assets/Scripts/RaceIntro/RaceBodyIntroStateController.cs:386:        Debug.Log(  ... ); // (inside TryCompleteAuthoritativeGoTransition)
HEAD:Assets/Scripts/RaceIntro/RaceBodyIntroStateController.cs:387:        CompleteGoTransition();
```
Single private caller: `TryCompleteAuthoritativeGoTransition` (lines 365-388), which is in turn called from `Update()` line 75 and `FixedUpdate()` line 104, both gated by `_authoritativeGoIssued && _authoritativeGoPendingTransition && _visualStarted && now >= scheduledGoTime`. The intro state machine sets these flags via `ApplyAuthoritativeGo` (line 234-265) which is called from `IntroSequenceManager.cs:407-408` per the intro-sequence broadcast chain.

### 4.5 Complete linear caller graph (race-start ONLY)

```
IntroSequenceManager (server → ObserversRpc) "authoritative go"
  → IntroClientController.ApplyAuthoritativeGo (per client)
    → RaceBodyIntroStateController.ApplyAuthoritativeGo (per buddah)
      → flips _authoritativeGoIssued + _authoritativeGoPendingTransition
      → next Update/FixedUpdate tick: TryCompleteAuthoritativeGoTransition fires when scheduledGoTime reached
        → CompleteGoTransition (line 307)
          → if (isLocalOwner) movementController.BeginLaunchHandoff(snapshot, ...)         [line 332]
            → bridge.TryBeginLaunchHandoff(snapshot, ...)                                   [BuddahMovement.cs:247]
              → predictedMotor.RequestAuthoritativeLaunchHandoffFromOwner(snapshot, ...)    [Bridge.cs:161]
                → if (IsServerInitialized) → TryApplyServerAuthoritativeLaunchHandoff (local server-fast-path)
                → else → RequestLaunchHandoffServerRpc + sets _awaitingAuthoritativeLaunchHandoff=true
                  → server-side handler → TryApplyServerAuthoritativeLaunchHandoff
                    → TryQueueLaunchHandoffEvent (server motor)
                    → QueueLaunchHandoffTargetRpc (back to owner)
                      → owner-side handler → TryQueueLaunchHandoffEvent (owner motor)
                        → next tick: ConsumePendingLaunchHandoffEvent (owner motor)
```

### 4.6 KEY FINDING — Section 11.1 disposition

`RequestAuthoritativeLaunchHandoffFromOwner` (motor.cs:862) and its associated `RequestLaunchHandoffServerRpc` (motor.cs:1028) + `QueueLaunchHandoffTargetRpc` (motor.cs:1027) are called **ONLY via the race-start chain above**. There are **zero mid-race callers** (no skill system, no respawn, no anti-skill triggers, no pause-resume, no debug menu). `git grep` exhaustively returned exactly the single caller chain.

**Therefore Section 11.1 disposition is firmly option (a):** *Phase 6 should DELETE the entire `RequestAuthoritativeLaunchHandoffFromOwner` chain along with its callers (the new server-driven path replaces it).* No L12 fix is needed as a residual concern — there are no residual callers.

Concrete deletion list (read-only inventory; Stage 3 design picks final scope):
- `BuddahPredictedMotor.cs`: `RequestAuthoritativeLaunchHandoffFromOwner` (motor.cs:862-911), `RequestLaunchHandoffServerRpc` (motor.cs:1028+), `QueueLaunchHandoffTargetRpc` (motor.cs:1027+ TargetRpc — see motor.cs:1100-1140 region for the body), `_awaitingAuthoritativeLaunchHandoff` field + uses, `_localPreHandoffBypassUntilTick` field + uses (only used as part of pre-handoff bypass), `LaunchHandoffSnapshot` parameter type at the API
- `BuddahPredictionHandoffBridge.cs`: `TryBeginLaunchHandoff` method (lines 148-165)
- `BuddahMovement.cs`: `BeginLaunchHandoff` method (lines 244-263)
- `RaceBodyIntroStateController.cs`: `movementController.BeginLaunchHandoff(...)` callsite at line 332; replace with no-op (server-driven new race-start path makes this redundant) or delete entire CompleteGoTransition's owner-branch
- `LaunchHandoffSnapshot.cs` (Assets/Scripts/RaceIntro/) — type may stay as-is (still useful internally); Stage 3 decides

The new architecture in contract Section 3.3 is **server-originated single-hop ObserversRpc broadcast**: the L12 two-hop visibility risk is **structurally eliminated** at race-start (per Section 11.1's own observation). This RECON confirms zero blockers to that conclusion.

**Implication for Q10**: `motor.cs:1873 teleport` and the new race-start LaunchHandoff event live in **separate code paths** post-Phase-6 (race-start handoff event no longer exists in motor; mid-race teleport untouched). EventID space concern in Q10 is moot — there is no shared ID space because the race-start event-type goes away.

---

## 5. Server orchestrator integration points (Q3 disposition material)

### 5.1 [Network/Room/](Assets/Scripts/Network/Room/) directory inventory

```
git ls-tree -r --name-only HEAD:Assets/Scripts/Network/Room/
→
RoomPlayerState.cs
RoomStateManager.cs
```
Only 2 files in Room/. `RoomStateManager` is the orchestrator.

### 5.2 [Network/Core/](Assets/Scripts/Network/Core/) directory inventory

```
ConnectionManager.cs
GameNetworkManager.cs
NetDebug.cs
```
Three files. `GameNetworkManager` owns the FishNet `NetworkManager` singleton + `TimeManager` access; `ConnectionManager` handles connection setup; `NetDebug` is utility.

### 5.3 RoomStateManager existing race-start signal chain (Q3-relevant)

```
git grep -n "_authoritativeGoIssued|_gameplayMovementUnlocked|_raceStarted|ReportLocalGameplayLive|ReportGameplayLiveServerRpc|MarkAuthoritativeGoIssuedServer|AreAllClientsGameplayLiveForSequenceServer" HEAD -- Assets/Scripts/Network/Room/RoomStateManager.cs
```
Highlights:

| Line | Surface |
|---|---|
| 51-52 | `private readonly SyncVar<bool> _gameplayMovementUnlocked = new SyncVar<bool>();` + `_authoritativeGoIssued` |
| 81-93 | public properties: `IsRaceStarted`, `IsGameplayMovementUnlocked`, `IsAuthoritativeGoIssued`, `IsWaitingForAuthoritativeGameplayLive`, `CanPlayersUseGameplayInput`, `ShouldBlockRaceGameplayInput`, `AreAllClientsGameplayLiveServer` |
| 130-139 | log on SyncVar value-change (`[IntroGo]` / `[GameplayUnlock]` markers) |
| 298-304 | `public void ReportLocalGameplayLive(int sequenceId)` — client-side entry; calls `ReportGameplayLiveServerRpc(sequenceId)` |
| 944-967 | `EvaluateRaceStartReadinessServer` — gated on `_matchSessionPhase == InMatch` + `AreAllPlayersReadyForRaceServer`; spins `RaceCountdownCoroutine` |
| 970-1003 | `RaceCountdownCoroutine` — pre-game countdown over `_pregameCountdownSeconds` (default 15s SerializeField), then sets `_pregameCountdownCompleted=true`, `_raceStarted=false`, `_gameplayMovementUnlocked=false` |
| 1010-1018 | `MarkAuthoritativeGoIssuedServer()` — flips `_authoritativeGoIssued=true`, called from `IntroSequenceManager:200` |
| 1158-1170 | `[ServerRpc(RequireOwnership=false)] ReportGameplayLiveServerRpc(int seq)` → `_gameplayLiveClientIds.Add(caller.ClientId)` → if `AreAllClientsGameplayLiveForSequenceServer(seq)` then `_gameplayMovementUnlocked=true; _raceStarted=true;` |
| 337-345 | `AreAllClientsGameplayLiveForSequenceServer(int seq)` — checks per-sequenceId match across all `Players` |

### 5.4 KEY FINDING — existing all-clients-ready signal already exists

The **existing chain** for "all clients are gameplay-live" is:
```
each owner client (post handoff consume / post visual go transition):
    → RoomStateManager.Instance?.ReportLocalGameplayLive(seq)
      → ReportGameplayLiveServerRpc(seq)
        → server: _gameplayLiveClientIds.Add(callerId)
          → if AreAllClientsGameplayLiveForSequenceServer(seq):
              _gameplayMovementUnlocked.Value = true   ← FishNet SyncVar BROADCAST
              _raceStarted.Value = true                 ← FishNet SyncVar BROADCAST
```

**Phase 6 Section 3.3 sketch is structurally similar** but uses `[ObserversRpc]` with embedded `countdownStartTick` / `raceStartTick` instead of SyncVar. Tradeoff for Q3 disposition (Stage 3):

| Pattern | Same-tick simultaneous unlock (G4) | Late-join handling | Wire-format friction | Re-uses existing? |
|---|---|---|---|---|
| (a) `[ObserversRpc] BroadcastCountdownBegin(countdownStartTick, raceStartTick)` per Section 3.3 sketch | ✅ Embedded ticks let each client schedule local actions deterministically | Section 3.3.3 covers (catch-up if tick passed) | New RPC + new fields | No (new code) |
| (b) Reuse SyncVar `_gameplayMovementUnlocked` + add a sibling `_raceStartTick` SyncVar | ⚠ SyncVar broadcast time is **NOT tick-stamped**; each client receives the SyncVar update at its own observe time. G4's "same-tick unlock" is approximate, NOT exact. | SyncVar replays the latest value to late-joiners (FishNet built-in) | No new RPC | Yes (re-uses) |
| (c) SyncVar `_raceStartTick` (uint, server-set) + each client gates `if (TimeManager.LocalTick >= _raceStartTick) unlock;` | ✅ Tick-stamped via SyncVar payload + local TimeManager.LocalTick comparison | SyncVar broadcasts to late-joiners; late-joiner reads value and immediately unlocks | No new RPC; one new SyncVar | Partial (extends existing) |

**Recommendation lean for Stage 3 Q3**: option (c) is the "reuse-and-extend" choice — minimum new code, retains tick-stamped semantics for G4. Stage 3 design Q&A should evaluate explicitly. RECON does not pick.

### 5.5 GameNetworkManager + TimeManager hooks

```
git grep -n "TimeManager\.\|OnTick\|TickRate\|TimeManager\.OnTick\|TimeManager\.Tick" HEAD -- Assets/Scripts/Network/Core/GameNetworkManager.cs Assets/Scripts/Network/Room/RoomStateManager.cs
```
Returns only inspector field cache logic, no `TimeManager_OnTick` / `OnTick` event subscription on either file. The `TimeManager.OnTick` event is consumed by motor (`TimeManager_OnTick` in BuddahPredictedMotor.cs) — no orchestrator-level tick subscription currently.

**Implication**: if Phase 6 Section 3.3.2's "Server tick = `raceStartTick` → BroadcastRaceStart" sketch is implemented, RoomStateManager must subscribe to `TimeManager.OnTick` for the first time (or a SyncVar+local TimeManager.LocalTick gate per option (c) above sidesteps this). Stage 3 disposition.

---

## 6. ★ Section 3.5 KEY FINDING — Auto-forward mechanism (Q4 disposition)

```
git show HEAD:Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs | sed -n '330,345p'
```
[BuddahPredictedMotor.BuildReplicateData](Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs#L330-L345):
```csharp
private BuddahPredictedInputData BuildReplicateData()
{
    uint currentTick = TimeManager != null ? TimeManager.LocalTick : 0u;
    _computedStats = BuddahPredictedModifierResolver.Resolve(_modifierState, config, currentTick);
    bool preHandoffBypassActive = IsLocalPreHandoffBypassActive(currentTick);
    bool movementAllowed = _movementGateBridge.IsMovementAllowed(gameObject)
                           || _computedStats.IsRoomBypassActive
                           || preHandoffBypassActive;
    float steering = movementAllowed ? (_ownerInputBridge.ReadSteering() * config.TurnInputMultiplier) : 0f;
    float throttle = movementAllowed ? 1f : 0f;                  // ← THE auto-forward mechanism
    bool ownerInputLive = IsOwner && movementAllowed;
    ...
    BuddahPredictedInputData replicate = new BuddahPredictedInputData(steering, throttle, movementAllowed, ownerInputLive);
    ...
}
```

`_ownerInputBridge` only exposes `ReadSteering()` ([BuddahPredictionOwnerInputBridge.cs:43-49](Assets/Scripts/New_Buddah/Integration/BuddahPredictionOwnerInputBridge.cs#L43-L49)):
```csharp
public float ReadSteering()
{
    if (!_initialized || !_enabled || _legacyInputSource == null) return 0f;
    return _legacyInputSource.GetSteering();
}
```
**There is no `ReadThrottle`** — the input bridge does not expose throttle at all. Player keyboard/controller input only contributes to steering.

**KEY FINDING 6**: the "auto-forward" logic is **a hardcoded throttle = 1f gate at motor.cs:339** keyed on `movementAllowed`. There is no acceleration ramp, no throttle key, no config field for forward force scaling at unlock. Once `movementAllowed=true` for a tick, the buddah commits full forward force (`commandedForwardForce = forwardDirection * computedStats.FinalForwardForce * 1f` per [BuddahLocomotionStep.Compute:45](Assets/Scripts/New_Buddah/Simulation/BuddahLocomotionStep.cs#L45)).

**Phase 6 Section 3.5 disposition**: NO new "auto-throttle" code is needed. The race-start unlock mechanism reduces to ensuring `movementAllowed` flips `false → true` at `raceStartTick`. This requires that the gate inputs (`_movementGateBridge.IsMovementAllowed(gameObject)` + `_computedStats.IsRoomBypassActive` + `preHandoffBypassActive`) all return true at `raceStartTick`. The first input is the dominant lever; it traces to `BuddahPredictionMovementGateBridge.IsMovementAllowed` → existing `RoomStateManager.CanPlayersUseGameplayInput` chain (line 89): `IsResultPhaseActive || (IsMatchPhaseActive && _gameplayMovementUnlocked.Value)`.

So Phase 6 unlock = flip `_gameplayMovementUnlocked = true` at server-determined `raceStartTick`. This is **already** what `ReportGameplayLiveServerRpc + AreAllClientsGameplayLiveForSequenceServer` does today. Phase 6's redesign reuses ~90% of the existing signal chain, replacing only the trigger semantics (was: "fired by motor's handoff consume", becomes: "fired by all-buddahs-spline-complete + 3s countdown elapse").

**Stage 3 implication for Section 3.5**: Section 3.5 says "existing 'auto-forward' logic in motor's normal-state tick path is what should activate. No new 'auto-throttle' code needed". RECON **CONFIRMS** this with concrete evidence — the auto-forward is `motor.cs:339 throttle = 1f`. The contract's "CRITICAL implementation note" on Q4 ("confirm via code reading...") is now resolved: throttle is NOT input-driven on the prediction path; it is hardcoded.

**Side effect for Section 3.2.2 (Locked state)**: Section 3.2.2 says "玩家按键无反应; buddah 不前进, 不旋转, 手不旋转, 不能 push". Setting Locked-state gating on `data.MovementAllowed = false` automatically enforces "buddah 不前进, 不旋转" because both throttle and steering go to 0 at motor.cs:338-339 in the same conditional. The "手不旋转" is a separate hand component (Stage 2 RECON has not surfaced the hand-rotation owner; recommend Stage 3 grep for `BuddahHandControl` rotation logic to confirm it's also gated by `IsRaceGameplayBlocked` — quick check shows `BuddahHandControl.cs:201/397/1174` already gate on `IsRaceGameplayBlocked` which delegates to `RoomStateManager.ShouldBlockRaceGameplayInput`, so hand rotation IS already blocked when `_gameplayMovementUnlocked=false`. ✓). "不能 push" — verify same pattern at `BuddahHandControl.cs:201` (entry to push-spawn) — same `IsRaceGameplayBlocked` gate, so push-skill is also gated.

**Conclusion for Section 3.2.2**: Locked state input gating reduces to "drive `_gameplayMovementUnlocked=false` during Phase 3", which is **already** the default state until `ReportGameplayLiveServerRpc` flips it true. The Locked behaviors fall out of the existing gate chain — no new input-blocker code per skill/hand needed.

---

## 7. Mapping to Section 11 reviewer observations

### 7.1 Section 11.1 — RequestAuthoritativeLaunchHandoffFromOwner caller inventory
**ADDRESSED** in Surface 4. Single caller chain; race-start ONLY; zero mid-race callers; option (a) "delete entire chain" is the dominant choice for Stage 3 disposition. L12 fix is **structurally moot** under the new design — Phase 6 should NOT carry an L12 fix shape.

### 7.2 Section 11.2 — Q9 reconcile-replay during Locked state (G8b candidate)
**RECON evidence cited**: motor.cs:466-473 `if (!data.MovementAllowed)` early-return path produces zero force application + zero velocity write through `_predictionRigidbody`. Reconcile callback at motor.cs:547-588 does NOT itself touch `_handoffState` (it calls `_predictionRigidbody.Reconcile(data.RigidbodyState)` then writes DebugState). If Phase 6 wires Locked-state to `data.MovementAllowed = false` during Phase 3, replays of any tick T inside `[countdownStartTick, raceStartTick)` ALL deterministically take the same early-return branch. **G8b is achievable structurally** by reusing the existing MovementAllowed gate. Stage 3 design owns final disposition (elevate Q9 to G8b, or keep with explicit "pass criterion = reconcile replay determinism" annotation). RECON does not pick.

### 7.3 Section 11.3 — Section 3.2.1 file list completeness
**ADDRESSED** in Surface 2.5 + Surface 3 + Surface 4.6. Contract Section 3.2.1 names ~5 files; the actual touch list is ~12 files. Missing-from-3.2.1 list:
- `BuddahPredictionHandoffBridge.cs` — `TryBeginLaunchHandoff` method
- `BuddahMovement.cs` — `BeginLaunchHandoff` method, `SyncPredictionHandoffStateFromBridge`, plus legacy `ApplyLaunchInheritedVelocity` (out-of-scope for Phase 6 per Section 4 unless Stage 3 expands)
- `RaceBodyIntroStateController.cs` — `CompleteGoTransition` callsite at line 332 + spline drive `GetNormalizedDistanceT` formula (Section 3.1 spline-decel rewrite)
- `IntroAssignmentData.cs` + `IntroSequenceManager.cs` — `introSpeedMetersPerSecond` Inspector field rename to `traversalTimeSeconds`
- `BuddahPredictionDebugOverlay.cs` + `BuddahPredictionDebugState.cs` — drop `handoffInheritEndTick / handoffBlendEndTick / handoffBlendAlpha` fields
- `BuddahHandoffStep.cs` + `BuddahPredictionShadowScratch.cs` — shadow comparator fields/comments
- `BuddahPredictionTickContext.cs` — doc-comment reference to `ConsumePendingLaunchHandoffEvent`
- `BuddahPredictedMotorConfig.cs` — Section 11.3 mentioned `maxSpeed` for spline-decel `v_max` derivation. RECON inspection: spline driver derives `v_max` from `_assignment.introSpeedMetersPerSecond` (intro assignment data), NOT from motor config. So **`BuddahPredictedMotorConfig.cs` does NOT need touching** for the spline formula — Stage 3 may relax Section 11.3's third bullet.

### 7.4 Section 11.4 — Phase 7.5-A.1 confound layering
**Out of RECON scope** (this is a Stage 6 VERIFY concern). Noted as flagged.

---

## 8. Open questions for reviewer

1. **Spline driver scope expansion (Surface 3.3 KEY FINDING)** — Section 3.1 of contract is conceptually fully scoped (linear/quadratic deceleration formula given), but Section 3.2.1's file list does not mention `RaceBodyIntroStateController.GetNormalizedDistanceT` or the `IntroAssignmentData` field rename. RECON has surfaced that these are the actual edit points. Should Stage 3 design explicitly add them to the implementation file list, or is "Spline driver per buddah" in Section 3.1 understood to cover them implicitly?

2. **Legacy non-prediction handoff path (BuddahMovement.cs:539, 561, 571-589)** — `_launchState`/`launchBlendTime`/`_launchInheritedVelocity` legacy state machine still exists for the non-prediction fallback path. Phase 6 contract Section 4 limits scope to "predicted path"; out-of-scope for Phase 6 per default. But if non-prediction fallback is still used in any code path post-Phase-4b, leaving it unchanged means race-start under fallback still uses the old velocity-inherit pattern. Stage 3 should verify whether fallback is still reachable or if it's effectively dead code post-V5.

3. **Q3 SyncVar vs ObserversRpc tradeoff (Surface 5.4)** — Section 3.3 sketch invents a new ObserversRpc; existing SyncVar `_gameplayMovementUnlocked` already provides the all-clients signal. Should Stage 3 re-evaluate the design to favor reusing the SyncVar (with an added `_raceStartTick: uint` SyncVar for tick-stamping) — option (c) — over building a new RPC layer? RECON-level answer is "yes, simpler and reuses existing chain", but Stage 3 owns the disposition.

4. **Contract line drift (Section 1.8 KEY FINDING)** — contract Section 1.1 cites motor.cs:1937 for the handoff `rb.position = SnapshotPosition` write; HEAD has it at 1948. Likely caused by phase7 commits between contract draft and current dev tip. Contract update (cite drift to ~1948) optional but improves locator precision for Stage 4.

5. **Phase 6 contract not yet in `Docs/phase-gates/active/`** — contract lives at `agent-exchange/handoff/2026-05-04-phase6-race-start-handoff-redesign-contract.md`; the Phase Gate System (per Methodology + CLAUDE.md "Before starting work on any phase, read in order: ... `Docs/phase-gates/active/<current-phase>-contract.md`") expects active contracts under `Docs/phase-gates/active/`. The harness helper currently surfaces phase7 + v2b-step1 as active, NOT phase6. Recommend reviewer move/copy the contract to `Docs/phase-gates/active/phase6-race-start-handoff-redesign-contract.md` after Stage 2 RECON sign-off so the helper surfaces it correctly for Stage 3 onwards.

---

## Summary — what Q1–Q10 must answer (preview, not answers)

- **Q1** wire format: tradeoff between deprecated zero-value fields (Steam-build backward compat) vs full removal. RECON evidence: enum `BuddahPredictedLaunchState` is NOT in `BuddahPredictedReconcileData` payload (verified Surface 2.3 inspection), so enum value remap is local-only. `InheritDurationTicks/BlendDurationTicks` are in `BuddahPredictedLaunchHandoffData` which is the wire payload of `RequestLaunchHandoffServerRpc + QueueLaunchHandoffTargetRpc` — but if Surface 4.6 disposition (delete entire chain) lands, those RPCs and their wire format also vanish. Q1 collapses into Q-cluster around Section 11.1 disposition. Stage 3 leans toward option (a) full deletion since the chain itself goes.
- **Q2** spline `T` parameter location: existing field is `IntroAssignmentData.introSpeedMetersPerSecond`, set in `IntroSequenceManager` Inspector. Phase 6 should add a sibling `traversalTimeSeconds` field (or replace in-place); per-stage configurability is a design Q. RECON has no preference.
- **Q3** all-buddahs-spline-complete detection: existing `ReportGameplayLiveServerRpc + _gameplayMovementUnlocked` SyncVar already implements (a)+(b)-equivalent semantics. Stage 3 strongly leans toward reusing it (Surface 5.4 option c).
- **Q4** auto-forward mechanism: **RESOLVED** by Surface 6 KEY FINDING — hardcoded `throttle = 1f` at motor.cs:339, gated by `movementAllowed`. Section 3.5's CRITICAL implementation note is now satisfied.
- **Q5** Race-Start Timeline asset path: out of RECON scope (Yonezawa's asset move).
- **Q6** late-join during countdown: Section 3.3.3 sketch has the right shape; existing `_gameplayMovementUnlocked` SyncVar replays to late-joiners trivially (FishNet built-in) — option (c) in Surface 5.4 covers this with no new code.
- **Q7** camera during Phase 3 Lock: out of motor scope; check `PlayerCamera.cs:387` (gates on `room.IsGameplayMovementUnlocked`); Stage 3 design should grep for camera lock behavior during locked state.
- **Q8** spline-complete failure / quorum: out of RECON scope; Stage 3 disposition.
- **Q9** reconcile-replay during Locked: **RESOLVED structurally** (Surface 11.2 — reuses motor.cs:466 MovementAllowed=false early-return). Stage 3 to elevate to G8b or keep with annotation.
- **Q10** event ID space: **MOOT** post Surface 4.6 disposition — race-start LaunchHandoffEvent goes away in new design; teleport stays in own ID space; no overlap concern.

---

Awaiting cowork-reviewer Stage 2 reviewer SIGN-OFF (independent verification of Surfaces 1-6 + KEY FINDINGS via git plumbing + Section 11.1/11.2/11.3 disposition acceptance/pushback) before Stage 3 DESIGN-QA is authored.
