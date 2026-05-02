# V3 — Skill Site Migration to CombatAdapter — Recon Report

**Branch:** `feat/phase4b-v3-skill-site-migrate` (cut from `dev` @ `3181bc4`; cut blocked locally by unrelated working-tree changes — no changes made on dev tree, recon is read-only)
**Status:** RECON ONLY — no code changes; no Q-answers yet (sequential per Methodology Rule 8)
**Scope reminder:** Migrate 4 skill callsites from `BuddahPredictionCombatRouting.TryRouteImpulse` → `bootstrap.CombatAdapter.TryRouteImpulse` (or static helper). Adapter expansion to subsume CombatRouting's responsibilities (OLD-feed + PushTargetBox fallback). CombatRouting becomes structurally dead, deleted in V4.

---

## 1. Skill site call surface — 4 callsites

### Callsite 1 — Melee push hitbox
[Assets/Scripts/Buddah/PushHitbox.cs:200](Assets/Scripts/Buddah/PushHitbox.cs#L200)
```csharp
if (BuddahPredictionCombatRouting.TryRouteImpulse(victimNO, _impulse, 0f, BuddahPredictedImpulseSourceType.MeleePush, _attacker))
{
    Debug.Log($"[PushHitbox] Routed to PredictionV2 victim={victimNO.name} mode={detectionMode}");
    return;
}

BuddahMovement victimMove = victimNO.GetComponent<BuddahMovement>();
if (victimMove != null)
{
    Debug.Log($"[PushHitbox] Routed to Legacy victim={victimNO.name} mode={detectionMode}");
    victimMove.ApplyPushImpulseTargetRpc(victimNO.Owner, _impulse);
}
```
- **Trigger:** `OnTriggerEnter` / overlap-fallback (`TryApplyHit(other, "trigger" | "overlap-fallback")`).
- **Args:** `(victimNO, _impulse, turn=0f, MeleePush, _attacker)`. `_impulse` is precomputed by hitbox owner.
- **Return handled:** ignored (return-only). Two-tier fallback to `BuddahMovement.ApplyPushImpulseTargetRpc` if Routing returned false.
- **Migration shape post-V3:** if Q0 = (B), `BuddahPredictionRouter.RouteImpulse(victimNO, _impulse, 0f, MeleePush, _attacker)` replaces both branches above (router internalizes V2 vs PushTargetBox vs BuddahMovement-RPC dispatch). If Q0 = (A), call adapter directly + keep BuddahMovement RPC fallback at callsite. **Verbose log lines** at lines 198, 202, 209 will need to migrate or be dropped — they're informational, not gameplay.

### Callsite 2 — Charged hand projectile
[Assets/Scripts/Buddah/ComboSkill/Skill_PushProjectileHands/ChargedHandProjectileRuntime.cs:362](Assets/Scripts/Buddah/ComboSkill/Skill_PushProjectileHands/ChargedHandProjectileRuntime.cs#L362)
```csharp
if (BuddahPredictionCombatRouting.TryRouteImpulse(victimNO, _impulse, _hitTurnTorqueImpulse, BuddahPredictedImpulseSourceType.ChargedProjectile, _attacker))
    return;

BuddahMovement victimMove = victimNO.GetComponent<BuddahMovement>();
if (victimMove != null)
    victimMove.ApplyPushImpulseAndTorqueTargetRpc(victimNO.Owner, _impulse, _hitTurnTorqueImpulse);
```
- **Trigger:** `OnTriggerStay` → `TryApplyHit`. Server-only guard: `InstanceFinder.IsServerStarted`.
- **Args:** `(victimNO, _impulse, _hitTurnTorqueImpulse, ChargedProjectile, _attacker)`. Both linear and angular impulse non-zero.
- **Return handled:** ignored. Same two-tier pattern with `ApplyPushImpulseAndTorqueTargetRpc` (different RPC than callsite 1 — carries torque).
- **Migration shape:** identical to callsite 1, just different `sourceType` enum and the AndTorque legacy RPC.

### Callsite 3 — Regular hand projectile
[Assets/Scripts/Buddah/ComboSkill/Skill_PushProjectileHands/HandPushProjectileRuntime.cs:212](Assets/Scripts/Buddah/ComboSkill/Skill_PushProjectileHands/HandPushProjectileRuntime.cs#L212)
```csharp
if (BuddahPredictionCombatRouting.TryRouteImpulse(victimNO, _impulse, _hitTurnTorqueImpulse, BuddahPredictedImpulseSourceType.Projectile, _attacker))
    return;

BuddahMovement victimMove = victimNO.GetComponent<BuddahMovement>();
if (victimMove != null)
    victimMove.ApplyPushImpulseAndTorqueTargetRpc(victimNO.Owner, _impulse, _hitTurnTorqueImpulse);
```
- Carbon copy of callsite 2 with `sourceType = Projectile` (not ChargedProjectile). Migration identical.

### Callsite 4 — Debug box
[Assets/Scripts/New_Buddah/Debug/BuddahPredictionImpulseDebugBox.cs:45](Assets/Scripts/New_Buddah/Debug/BuddahPredictionImpulseDebugBox.cs#L45)
```csharp
if (BuddahPredictionCombatRouting.TryRouteImpulse(
        victimNetworkObject,
        impulse,
        turnTorqueImpulse,
        BuddahPredictedImpulseSourceType.DebugBox,
        null)) // <-- sourceObject = null
{
    return;
}

if (!fallbackToLegacy)
    return;

BuddahMovement movement = victimNetworkObject.GetComponent<BuddahMovement>();
if (movement != null)
    movement.ApplyPushImpulseAndTorqueTargetRpc(victimNetworkObject.Owner, impulse, turnTorqueImpulse);
```
- **Trigger:** `OnTriggerEnter`. Server-only guard. `triggerOncePerVictim` HashSet dedup.
- **Args:** `(victimNetworkObject, impulse, turnTorqueImpulse, DebugBox, null)`. The `null` sourceObject is unique here — sourceObjectId resolves to 0 in adapter ([adapter.cs:71](Assets/Scripts/New_Buddah/Integration/BuddahPredictionCombatAdapter.cs#L71)).
- **Return handled:** ignored, with `fallbackToLegacy` toggle gating the second-tier RPC.
- **Migration shape:** identical pattern, plus the `fallbackToLegacy` toggle would migrate into the router (or be dropped if router internalizes the fallback policy).

### Common pattern across all 4 callsites
- All 4 are server-side fire (under `InstanceFinder.IsServerStarted` or implicitly server-only via skill execution flow).
- All 4 have the V2-or-Legacy two-tier shape with `BuddahMovement.ApplyPushImpulse{,AndTorque}TargetRpc` as second tier.
- None of the callers consume return value beyond `return-on-true`.
- `Debug.Log` lines at PushHitbox callsite are the only diagnostic output; the other 3 are silent on hit.

---

## 2. CombatRouting body breakdown — [BuddahPredictionCombatRouting.cs](Assets/Scripts/New_Buddah/Integration/BuddahPredictionCombatRouting.cs)

| Block | Lines | Responsibility | Post-V3 owner |
|---|---|---|---|
| Early-null guard | 21-22 | `if (victimNetworkObject == null) return false;` | Adapter (it already has the same guard at [adapter.cs:60](Assets/Scripts/New_Buddah/Integration/BuddahPredictionCombatAdapter.cs#L60)). Router/static helper passes through. |
| OLD V2 branch | 33-36 | `motor.TryApplyServerAuthoritativeImpulse(...)` if `bootstrap != null && motor != null && IsPredictionModeActive()` | **Adapter** (Q1 = A internal dual-feed) OR **router/static helper** (Q1 = B). Either way, adapter or wrapper calls `motor.TryApplyServerAuthoritativeImpulse` to keep `_legacyShadowScratch` populated. |
| PushTargetBox fallback | 39-43 | `pushTargetBox.TryApplyServerImpulse(...)` if no bootstrap or no motor | **Adapter or router** depending on Q0. Q0 = (A) puts it in adapter; Q0 = (B) puts it in router; Q0 = (C) leaves at callsite (rejected by reviewer per contract). |
| LEGACY_SHADOW fan-out | 46-65 | `try/catch` around `bootstrap.CombatAdapter?.TryRouteImpulse(...)` (adapter NEW-path enqueue + RPC, observation only pre-Step-1) | **DROPPED.** Post-V3, adapter IS the entry point — no fan-out indirection needed. Callsite calls adapter directly; adapter does NEW-channel enqueue itself. The try/catch existed because pre-Step-1 the OLD path was authority and a NEW-path bug couldn't be allowed to poison return value. Post-Step-1 NEW path IS authority, so this safety net's value flips: a NEW-path throw should escalate as FATAL, not be swallowed silently. Drop the try/catch entirely. |
| Composite return | 67 | `return oldResult;` — discards adapter return, returns OLD result | **Adapter return value used directly.** Skills consume `adapter.TryRouteImpulse` return as the authoritative result. Router (if Q0=B) returns adapter's value (or PushTargetBox-handled bool). |

### CombatRouting state after V3
- Body becomes structurally dead (no caller).
- Kept in tree for V4 deletion (Methodology Rule 9 — V4 is its own decision).
- File can be marked `[Obsolete]` in this PR for IDE-level discoverability of dead code.

---

## 3. PushTargetBox population audit — KEY FINDING

### Population
- **Component definition:** [Assets/Scripts/New_Buddah/Debug/BuddahPredictionPushTargetBox.cs](Assets/Scripts/New_Buddah/Debug/BuddahPredictionPushTargetBox.cs) — script GUID `e27480bfb0911864c895c3fe2e11bd45`.
- **Production prefab usage:** **0**. No `.prefab` file references the GUID.
- **Scene usage:** **4 instances in `Assets/Scenes/RaceMap.unity`** only. No other scene references the GUID.
- **Component requires:** `[RequireComponent]` Rigidbody + BoxCollider + NetworkObject. Self-contained physics target; not a Buddah, not a player.
- **Fallback gating:** in CombatRouting OLD V2 branch fails when `bootstrap == null || motor == null` — i.e., victim is NOT a Buddah. PushTargetBox debug objects don't have bootstrap/motor → naturally route to PushTargetBox fallback.

### Implication for Q0
- Production gameplay-critical population of "non-V2 victims that need PushTargetBox fallback" = **0 (only 4 in dev RaceMap scene).**
- The fallback is debug-only. Removing it would break dev RaceMap testing but not ship gameplay.
- Q0 simplifies: adapter doesn't NEED to gracefully handle non-bootstrap victims to preserve gameplay; it only needs to handle them to preserve the dev RaceMap debug box flow.

### Second fallback tier — `BuddahMovement.ApplyPushImpulse{,AndTorque}TargetRpc`
- 4 skill callsites also have a SECOND fallback (after CombatRouting returns false): direct `BuddahMovement` RPC to legacy buddah movement.
- This handles: legacy-mode Buddahs (`bootstrap.IsPredictionModeActive() == false`), where CombatRouting returns false (V2 branch gated, PushTargetBox not present) → skill explicit `BuddahMovement` RPC.
- Verified Buddah prefab ([Assets/Character/Prefab/Buddah.prefab](Assets/Character/Prefab/Buddah.prefab)) has BOTH `BuddahPredictionBootstrap` (GUID `4e72f8d13c2549209c313013695b7171`) AND `BuddahMovement` (GUID `287d74e1a9a21354895d5b6afa156cef`). So mode toggle decides which is live.
- **Topology summary:**
  | Victim type | bootstrap? | IsPredictionModeActive? | PushTargetBox? | BuddahMovement? | Routes today |
  |---|---|---|---|---|---|
  | V2-mode Buddah | ✅ | ✅ | ❌ | ✅ | CombatRouting → motor (V2 branch) |
  | Legacy-mode Buddah | ✅ | ❌ | ❌ | ✅ | CombatRouting returns false → skill `BuddahMovement` RPC |
  | PushTargetBox debug | ❌ | n/a | ✅ | ❌ | CombatRouting → PushTargetBox fallback |
  | Other / unknown | ❌ | n/a | ❌ | ❌ | All fallbacks fail; skill silently no-ops |

### Q0-impact summary
- **3 victim categories actually need handling**: V2 Buddah, Legacy Buddah, PushTargetBox.
- Q0 (A) — adapter handles fallback internally — needs to also know about BuddahMovement RPC, OR delegate that to router. Pollutes adapter with legacy concerns.
- Q0 (B) — static router — clean separation: adapter does V2 only; router does the 3-way dispatch (V2 / PushTargetBox / BuddahMovement). Keeps adapter scope tight to its V2 responsibility.
- Q0 (C) — callsite-explicit — spreads 3-way dispatch into 4 skill callsites. Bad.
- **Lean toward Q0 = (B)** with the router being a tiny thin wrapper. Confirms contract recommendation.

---

## 4. Dependency graph: who feeds `motor._impulseEventQueue` today?

Post-Step-1 production state (verified via grep + read):

| Caller | File:line | Path |
|---|---|---|
| `motor.TryApplyServerAuthoritativeImpulse` | [BuddahPredictedMotor.cs:852-876](Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs#L852-L876) | server-side: `TryQueueImpulseEvent(eventData)` (server-local enqueue) + `QueueImpulseEventTargetRpc(Owner, eventId, eventTick, ...)` (relay to Owner client) |
| `motor.QueueImpulseEventTargetRpc` (handler) | [BuddahPredictedMotor.cs:1284-1303](Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs#L1284-L1303) | client-side: receives RPC, calls `TryQueueImpulseEvent(eventData)` |
| `motor.TryQueueImpulseEvent` | [BuddahPredictedMotor.cs:1305-1330](Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs#L1305-L1330) | the actual `_impulseEventQueue.TryEnqueue(eventData)` callsite — both server-local and RPC-handler converge here |
| `BuddahPredictionCombatRouting.TryRouteImpulse` (OLD V2 branch) | [CombatRouting.cs:35](Assets/Scripts/New_Buddah/Integration/BuddahPredictionCombatRouting.cs#L35) | static dispatch: `motor.TryApplyServerAuthoritativeImpulse(...)` |
| Skill callsites (×4) | listed in §1 | static dispatch: `BuddahPredictionCombatRouting.TryRouteImpulse(...)` |

### Current call chain (post-Step-1)
```
skill site → CombatRouting.TryRouteImpulse
                ├─ V2 branch → motor.TryApplyServerAuthoritativeImpulse
                │                ├─ TryQueueImpulseEvent (server-local) → _impulseEventQueue
                │                └─ QueueImpulseEventTargetRpc (relay) → client → TryQueueImpulseEvent → _impulseEventQueue
                ├─ PushTargetBox fallback → pushTargetBox.TryApplyServerImpulse (rb writes directly, NOT through PredictionRigidbody — Rule 7 ⚠️ but pre-existing)
                └─ LEGACY_SHADOW fan-out → bootstrap.CombatAdapter?.TryRouteImpulse → channel enqueue + RPC (NEW path, post-Step-1 = authority)
```

### Post-V3 call chain (assuming Q0=B + Q1=B static router)
```
skill site → BuddahPredictionRouter.RouteImpulse(victim, ...)
                ├─ if bootstrap.IsPredictionModeActive: bootstrap.CombatAdapter.TryRouteImpulse
                │     ├─ NEW (channel enqueue + RPC) — preserved from Step-1
                │     └─ OLD-feed: motor.TryApplyServerAuthoritativeImpulse
                │           ├─ TryQueueImpulseEvent (server-local) → _impulseEventQueue
                │           └─ QueueImpulseEventTargetRpc (relay) → client → ...
                ├─ else if pushTargetBox: pushTargetBox.TryApplyServerImpulse
                └─ else if BuddahMovement: BuddahMovement.ApplyPushImpulse{AndTorque}TargetRpc (legacy buddah path)
```

### Orphan path check
- After V3, no skill callsite calls `CombatRouting.TryRouteImpulse` anymore. ✅ confirmed by grep — only the 4 callsites listed in §1.
- `motor.TryApplyServerAuthoritativeImpulse` is still called from adapter (V2 path, OLD-feed). ✅ no orphan.
- `motor.QueueImpulseEventTargetRpc` still fires from server when adapter calls `TryApplyServerAuthoritativeImpulse`. ✅ no orphan.
- `motor.TryQueueImpulseEvent` still receives from both directions. ✅ no orphan.
- `BuddahPredictionCombatRouting.TryRouteImpulse` becomes unreferenced production code (only `[Obsolete]` placeholder until V4). ✅ correct dead state.
- `_impulseEventQueue` still drained by `ConsumePendingImpulseEvents_LegacyShadow`. ✅ no orphan.

---

## 5. Adapter expansion touchpoints

### Current adapter API ([BuddahPredictionCombatAdapter.cs](Assets/Scripts/New_Buddah/Integration/BuddahPredictionCombatAdapter.cs))
- **Lifecycle:** `Initialize(BuddahPredictionCommandBus)` + `MarkReady()` + `IsReady` getter (L7 latch contract).
- **API:** `TryRouteImpulse(victim, impulse, turnTorque, sourceType, sourceObject) → bool`. Body:
  - Null + initialized + bootstrap-active gate
  - `GetComponent<BuddahPredictionBootstrap>()` per-call (Q3 micro-opt candidate)
  - Server tick stamp + `++_nextLogicalId` per-call
  - Owner-aware enqueue (α: local, β: server-local + RPC)
- **State:** `_commandBus` ref + `_initialized` bool + `_nextLogicalId` uint counter.

### Post-V3 adapter shape (assuming Q1 = A internal dual-feed)
- New code in `TryRouteImpulse` body: BEFORE the existing channel-enqueue lines, call `motor.TryApplyServerAuthoritativeImpulse(impulse, turnTorque, sourceType, sourceObjectId)`. Adapter needs the motor reference.
- New cached field (Q3 = A): `BuddahPredictedMotor _motor` cached at `Initialize` (or first call). Same for `BuddahPredictionBootstrap _bootstrap` already a Q3 candidate.
- Adapter signature unchanged externally — body grows.
- **Q3 (B) deferral risk:** if Step 1 already had bootstrap GetComponent per-call without measurable impact, V3 adding motor GetComponent per-call is similarly cheap. Q3 (A) lean is a nice-to-have, not blocker.

### Post-V3 adapter shape (assuming Q1 = B + Q0 = B router does dual-feed)
- Adapter body unchanged from Step 1 — router handles OLD-feed before delegating to adapter for NEW path.
- Cleaner: adapter stays single-purpose (NEW path only). Router carries the multi-path fan-out cost.
- New file: `Assets/Scripts/New_Buddah/Integration/BuddahPredictionRouter.cs` (Q4 = A).

### Bootstrap reference — already wired
- `BuddahPredictionBootstrap.CombatAdapter` getter exists ([reference at adapter.cs:25 comment + bootstrap.cs construction not re-read here, verified by Step 0 fix-3 contract]).
- Skills use `victim.GetComponent<BuddahPredictionBootstrap>()?.CombatAdapter?.TryRouteImpulse(...)` (or via router static helper).

### Adapter init dependency on motor (Q3 / V3 implementation)
- If adapter caches motor ref, when does it get the ref? Bootstrap is constructed first, then adapter is created inside Bootstrap constructor (existing code). Motor lives on same NetworkObject. Two options:
  - Eager: `Initialize(commandBus, motor)` — adapter resolves motor immediately. Requires bootstrap to fetch motor in its construction order. Verify ordering doesn't violate L7 latch (no enqueue during Awake/OnEnable/OnStartNetwork).
  - Lazy: cache motor on first `TryRouteImpulse` call via `victimNetworkObject.GetComponent<BuddahPredictedMotor>()`. Costs one GetComponent on first call; cached thereafter. Simpler, defers ordering question. Lean here.

---

## Summary — what Q0–Q4 must answer (preview, not answers)

- **Q0** lean toward **(B) static router** per recon §3 finding: 3 victim categories (V2 / PushTargetBox / Legacy buddah BuddahMovement) need handling; static router with 3-way dispatch keeps adapter scope clean. Rejecting (C) is cheap — spreads 3-way logic into 4 callsites.
- **Q1** lean toward **(B) router does dual-feed** if Q0 = (B); both adapter (NEW) and `motor.TryApplyServerAuthoritativeImpulse` (OLD) called from the same router branch for V2 victims. Rejecting (C) (no OLD feed = retire LEGACY_SHADOW now) preserves the strict-gate safety net through V3's mechanical churn.
- **Q2** lean toward **(A) DebugState mirror is effectively done**: Step 1 already wrote 6 fields in `ConsumeImpulseAuthoritativeEntry` ([motor.cs:1923-1929](Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs#L1923-L1929)) + `pendingImpulseCount` from channel. Only `pendingImpulseSummary` deferred to V4 alongside CombatRouting deletion (less churn).
- **Q3** lean toward **(A) cache** in V3 since adapter expansion is already touching the file. Cache `_bootstrap` + `_motor` (lazy on first call, not eager init). +1 cached field is cheap.
- **Q4** lean toward **(A) `BuddahPredictionRouter.cs` in `Assets/Scripts/New_Buddah/Integration/`**, separate file matches CombatRouting precedent; if router becomes thin (just dispatch), V4 may inline back into adapter or delete entirely.

### Methodology session-scoping reminder (Rule 1 sub-rule)
For V3 smoke runs: the user / implementer should clear or rename `Editor.log` BEFORE Path A / Path B PlayMode entry. Step 1 Path A had to retroactively window-scope past 85 historical INV HBs. Avoid that by clearing the log first OR explicitly noting Step session start line in digest.

### Out-of-band finding for V4 PRE-WORK
PushTargetBox.cs:42 calls `targetRigidbody.AddForce(impulse, ForceMode.Impulse)` directly — bypasses PredictionRigidbody (Rule 7 violation). This is **pre-existing** debug code and PushTargetBox debug objects don't run prediction (no motor, no PredictionRigidbody). Not a V3 concern, but flagging for V4 / Phase 7 / Phase 8 cleanup queue: when retiring `BUDDAH_PREDICTION_LEGACY_SHADOW` define, decide whether to delete PushTargetBox entirely (its 4 RaceMap instances are dev-only) or keep with explicit "non-prediction debug only" annotation.

---

Awaiting reviewer Stage 2 sign-off before writing Q0–Q4 design proposals.
