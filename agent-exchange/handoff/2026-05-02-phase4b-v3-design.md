# V3 — Skill Site Migration to CombatAdapter — Design Q&A

**Recon reference:** [agent-exchange/handoff/2026-05-02-phase4b-v3-recon.md](agent-exchange/handoff/2026-05-02-phase4b-v3-recon.md)
**Status:** DESIGN PROPOSAL — no code changes yet
**Recon SIGN-OFF:** 2026-05-02 by reviewer (4 spot-checks per Methodology Rule 2). Q-leans approved.

---

## Q0 — Adapter call signature for non-V2 victims

**Picked:** **(B) Static helper method** — new `BuddahPredictionRouter` static class with `RouteImpulse(...)` entry point. Skills call the static helper. Adapter instance method preserved unchanged for V2 path.

**Justification:**
- Recon §3 established **3 victim categories** that need handling: V2 Buddah (bootstrap + IsPredictionModeActive), Legacy Buddah (bootstrap but mode off → BuddahMovement RPC), PushTargetBox debug (no bootstrap, has the target component). All 4 skill callsites today encode the V2-or-Legacy two-tier pattern with a third branch (PushTargetBox) hidden inside CombatRouting.
- Static router gives skills a **single entry point** (replaces 4-line two-tier dispatch with 1 line), keeps adapter scope **clean to its V2 responsibility** (channel enqueue + RPC), and matches CombatRouting's existing static-dispatch shape so the migration is mechanical.
- Option (A) "adapter handles fallback internally" pollutes adapter with legacy concerns (PushTargetBox + BuddahMovement RPC) it has no business knowing about post-V4 retirement of legacy paths.
- Option (C) "skills check victim type" spreads the 3-way logic across 4 callsites — bad maintenance shape.

**Risk:**
- **Static class with NetworkObject.GetComponent calls** doesn't carry per-instance state, but it does GetComponent up to 3× per call (bootstrap, motor, push-target-box / BuddahMovement). Per-call overhead similar to current CombatRouting (which does the same lookups). Not a regression, but not an optimization either.
- **Mitigation**: Q3 (revisited below) establishes that further caching opportunity exists but doesn't pay off cleanly until V4. Acceptable for V3.
- **Risk: forgetting to migrate a callsite** — recon §1 enumerated exactly 4. After V3 implementation, a grep for `BuddahPredictionCombatRouting.TryRouteImpulse` from `Assets/Scripts/Buddah` and `Assets/Scripts/New_Buddah/Debug` must return 0 hits. Verification step in PR description.

**Open questions:** none.

---

## Q1 — OLD path feed location (where does `motor.TryApplyServerAuthoritativeImpulse` get called from)

**Picked:** **(B) Static helper does dual-feed** — `BuddahPredictionRouter.RouteImpulse` calls BOTH `motor.TryApplyServerAuthoritativeImpulse` (OLD-feed for `_legacyShadowScratch` continuity) AND `bootstrap.CombatAdapter.TryRouteImpulse` (NEW-feed, post-Step-1 rb-writing authority) in the V2 victim branch. Adapter instance method stays unchanged.

**Justification:**
- Follows naturally from Q0=(B). Single dispatch site, single fan-out site, easier to reason about.
- Adapter stays single-purpose (NEW path only) — matches recon §5 lean. Future V4 deletion of OLD-feed becomes a one-line removal in router rather than a body change in adapter.
- Option (A) "adapter internal dual-feed" mixes V2-OLD-path concerns into adapter, complicating the V4 retirement diff (would need to strip lines from adapter body AND delete CombatRouting AND delete `_impulseEventQueue`).
- Option (C) "no OLD feed, retire LEGACY_SHADOW now" — rejected per contract. Loses strict-gate safety net through V3's mechanical churn. V3 is supposed to be a low-risk mechanical refactor; bringing V4's shadow retirement into V3 introduces new failure modes (no `[D-IMP LEG FATAL]` to catch regressions).

**Code shape sketch (V2 victim branch in router):**
```csharp
if (bootstrap != null && bootstrap.IsPredictionModeActive())
{
    BuddahPredictedMotor motor = victimNetworkObject.GetComponent<BuddahPredictedMotor>();
    if (motor != null && bootstrap.CombatAdapter != null)
    {
        int sourceObjectId = sourceObject != null ? sourceObject.ObjectId : 0;

#if BUDDAH_PREDICTION_LEGACY_SHADOW
        // OLD-feed for _legacyShadowScratch continuity. V4 removes this line + retires the define.
        motor.TryApplyServerAuthoritativeImpulse(impulse, turnTorqueImpulse, sourceType, sourceObjectId);
#endif

        // NEW-feed (post-Step-1 rb-writing authority) — channel enqueue + RPC + Q0 LogicalId stamp.
        return bootstrap.CombatAdapter.TryRouteImpulse(victimNetworkObject, impulse, turnTorqueImpulse, sourceType, sourceObject);
    }
}
```

**LEGACY_SHADOW try/catch fan-out fate (sub-decision from recon §2):**
The current CombatRouting has a `try/catch` around `bootstrap.CombatAdapter?.TryRouteImpulse` (lines 56-63) — a safety net that prevents NEW-path bugs from poisoning OLD's return value when OLD was authority. **Post-Step-1, NEW IS authority.** A NEW-path throw should escalate as FATAL, not be swallowed. **Drop the try/catch entirely** when migrating to router — propagate adapter exceptions normally. Rationale: silent failure of authoritative rb-write is worse than a stack trace. (Recon §2 already flagged this; documenting decision here.)

**Risk:**
- **Per-tick overhead**: dual-feed costs 1 motor call (OLD path) + 1 adapter call (NEW path). Same as today (CombatRouting did exactly this). No regression.
- **Synchronization risk**: OLD and NEW paths must observe the same logical event. Today this is verified by `[D-IMP LEG FATAL]` gate (`leg-imp-div=0` in 216 HBs across V2b Step 1 Path A + Path B). V3 must NOT break this — router calls both paths for the same event in the same call frame, identical args. Adapter's fix-3 dual-emit (server-local + RPC) preserved. Risk = 0 if router faithfully mirrors today's CombatRouting fan-out.

**Open questions:** none.

---

## Q2 — DebugState mirror status check

**Picked:** **(A) DebugState mirror is effectively done; V3 does NOT need to add anything; `pendingImpulseSummary` defers to V4.**

**Justification:**
- V2b Step 1's `ConsumeImpulseAuthoritativeEntry` already writes 6 DebugState fields per consumed impulse: `preImpulseSpeed`, `lastImpulseEventId` (= channel `entry.Id`), `lastImpulseEventTick` (= `entry.EventTick`), `lastImpulseSourceType`, `lastImpulseVector`, `lastImpulseTurnTorque`, `lastImpulseConsumed=true`. Plus `pendingImpulseCount = channel.Count` post-drain. ([motor.cs around line 1900-1920 — verified during V2b Step 1 implementation](Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs#L1900))
- The 1 remaining gap (`pendingImpulseSummary`) was explicitly deferred to V4 in the V2b Step 1 commit comments and contract carry-forward — channel doesn't currently expose a summary-builder helper analogous to OLD's `_impulseEventQueue.BuildPendingSummary()`.
- V3's scope is skill-site migration + adapter fan-out reorg. Adding a summary builder is V4 cleanup work alongside CombatRouting deletion. Mixing concerns increases V3 PR diff for marginal value.
- Option (B) "add pendingImpulseSummary builder now" is reasonable but bloats V3 scope. Decline.
- Option (C) "audit DebugState consumers and drop fields" is a Phase 7+ inspector cleanup, not V3.

**Risk:**
- **Inspector debug panel staleness**: between V3 and V4, the `pendingImpulseSummary` field continues to reflect OLD `_impulseEventQueue` contents only (which IS still RPC-fed during dual-feed era). Useful but not authoritative; dev who reads it should know the channel summary is unavailable until V4. Acceptable transitional state.

**Open questions:** none.

---

## Q3 — GetComponent caching micro-opt — REVISITED under approved Q0=(B)/Q1=(B)

**Picked:** **(A) APPROVED IN PRINCIPLE, BUT IMPLEMENTATION REALITY = no cache target in V3.**

The contract approved Q3=(A) "Implement now in V3" based on the assumption that adapter would expand to subsume PushTargetBox lookups (per the Q3 question's framing: "After V3 expansion, adapter may also need GetComponent<PushTargetBox> for fallback. That's 2 GetComponent calls per impulse.").

Under the approved Q0=(B) + Q1=(B), **adapter doesn't expand**:
- PushTargetBox lookup lives in router, not adapter.
- BuddahMovement legacy lookup lives in router, not adapter.
- Adapter's body stays Step-1-identical: 1 `victim.GetComponent<BuddahPredictionBootstrap>()` for the defensive `IsPredictionModeActive` gate, plus the unchanged channel enqueue + RPC dispatch.

Static router has no per-call cacheable state (each `RouteImpulse` call has a different victim; static class can't carry state without a thread-local Dictionary which isn't worth it for ~8 GetComponents per peer per second peak).

**Implication:** V3 ships with **no new caching**. The optimization opportunity Q3 envisioned doesn't materialize because adapter doesn't expand.

**Forward note for V4:**
After V4 retires LEGACY_SHADOW + deletes CombatRouting, the router's V2 branch may inline back into the skill callsites or merge into adapter (single-tier dispatch). At that point a real cache opportunity may emerge (e.g., adapter caches its OWN bootstrap reference if it ever needs it — currently doesn't). V4 design Q&A should re-evaluate.

**Alternative considered: drop adapter's defensive bootstrap gate**
Router pre-validates `bootstrap.IsPredictionModeActive()` before calling adapter. Adapter's redundant check at [BuddahPredictionCombatAdapter.cs:67-69](Assets/Scripts/New_Buddah/Integration/BuddahPredictionCombatAdapter.cs#L67-L69) becomes dead under router-mediated calls — but non-dead if any future caller bypasses router. **Keep adapter's gate** as defense-in-depth. Saves 1 GetComponent per call to remove it; cost of leaving it is negligible (Unity's GetComponent on a known type is ~O(few-component-iterations)). Defense-in-depth wins.

**Risk:**
- None at V3. Adapter unchanged, router does fresh GetComponent calls every fire (matches today's CombatRouting cost).

**Open questions:** none — Q3 effectively defers to V4 with a clean rationale.

---

## Q4 — Static helper class location

**Picked:** **(A) New file `BuddahPredictionRouter.cs` in `Assets/Scripts/New_Buddah/Integration/`.**

**Justification:**
- Matches CombatRouting precedent (same folder, same static-class pattern). Easy for next agent to find.
- Sibling to CombatRouting during the V3→V4 transition window — both files visible, dead one marked `[Obsolete]` for IDE-level discoverability.
- Cleanest V4 delete: when CombatRouting is removed, router stays where it is or can be inlined later. Single-file scope.
- Option (B) "static method on adapter" mixes V2-only adapter with V2-or-Legacy-or-PushTargetBox dispatch concerns → bad coupling.
- Option (C) "static method on Bootstrap" — Bootstrap is per-instance (NetworkBehaviour), but the router is invoked with arbitrary victims. Confusing semantic mismatch.

**File skeleton (full proposed body):**
```csharp
// Assets/Scripts/New_Buddah/Integration/BuddahPredictionRouter.cs
using FishNet.Connection;
using FishNet.Object;
using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Debugging;
using UnityEngine;

namespace NewBuddah.PredictionV2.Integration
{
    /// <summary>
    /// Phase 4b V3 — single static dispatch entry point for skill→victim impulse routing.
    /// Replaces BuddahPredictionCombatRouting.TryRouteImpulse + skill-callsite legacy
    /// BuddahMovement RPC fallbacks. Three-tier dispatch:
    ///   1. V2 Buddah: bootstrap.IsPredictionModeActive → motor.TryApplyServerAuthoritativeImpulse
    ///      (OLD shadow feed under LEGACY_SHADOW define) + bootstrap.CombatAdapter.TryRouteImpulse
    ///      (NEW rb-writing authority post-Step-1).
    ///   2. PushTargetBox debug: pushTargetBox.TryApplyServerImpulse.
    ///   3. Legacy Buddah: BuddahMovement.ApplyPushImpulseAndTorqueTargetRpc.
    /// V4 retires LEGACY_SHADOW define → strip OLD-feed line + delete CombatRouting.
    /// </summary>
    public static class BuddahPredictionRouter
    {
        public static bool RouteImpulse(
            NetworkObject victimNetworkObject,
            Vector3 impulse,
            float turnTorqueImpulse,
            BuddahPredictedImpulseSourceType sourceType,
            NetworkObject sourceObject)
        {
            if (victimNetworkObject == null)
                return false;

            // Tier 1 — V2 prediction Buddah.
            BuddahPredictionBootstrap bootstrap = victimNetworkObject.GetComponent<BuddahPredictionBootstrap>();
            if (bootstrap != null && bootstrap.IsPredictionModeActive())
            {
                BuddahPredictedMotor motor = victimNetworkObject.GetComponent<BuddahPredictedMotor>();
                if (motor != null && bootstrap.CombatAdapter != null)
                {
                    int sourceObjectId = sourceObject != null ? sourceObject.ObjectId : 0;
#if BUDDAH_PREDICTION_LEGACY_SHADOW
                    // OLD-feed for _legacyShadowScratch continuity through V4 retirement.
                    motor.TryApplyServerAuthoritativeImpulse(impulse, turnTorqueImpulse, sourceType, sourceObjectId);
#endif
                    // NEW-feed (post-Step-1 rb-writing authority).
                    return bootstrap.CombatAdapter.TryRouteImpulse(victimNetworkObject, impulse, turnTorqueImpulse, sourceType, sourceObject);
                }
            }

            // Tier 2 — PushTargetBox debug target (RaceMap-only, dev objects).
            BuddahPredictionPushTargetBox pushTargetBox = victimNetworkObject.GetComponent<BuddahPredictionPushTargetBox>();
            if (pushTargetBox != null)
            {
                int sourceObjectId = sourceObject != null ? sourceObject.ObjectId : 0;
                return pushTargetBox.TryApplyServerImpulse(impulse, turnTorqueImpulse, sourceType, sourceObjectId);
            }

            // Tier 3 — Legacy buddah path (BuddahMovement, mode toggle off OR motor missing).
            BuddahMovement victimMove = victimNetworkObject.GetComponent<BuddahMovement>();
            if (victimMove != null)
            {
                victimMove.ApplyPushImpulseAndTorqueTargetRpc(victimNetworkObject.Owner, impulse, turnTorqueImpulse);
                return true;
            }

            // No handler; silent no-op (consistent with today's CombatRouting + skill double-fallback no-match behavior).
            return false;
        }
    }
}
```

**Note on Tier 3 single-RPC consolidation:** All 4 skill callsites today have a Tier-3 fallback. PushHitbox uses `ApplyPushImpulseTargetRpc(owner, impulse)` (no torque variant); the other 3 use `ApplyPushImpulseAndTorqueTargetRpc(owner, impulse, torque)`. Behaviorally equivalent when `turnTorqueImpulse == 0f` since `ApplyPushImpulseTargetRpc` calls `ApplyPushAndTorqueLocal(impulse, 0f)` internally ([BuddahMovement.cs:427-430](Assets/Scripts/Buddah/BuddahMovement.cs#L427-L430)). Router consolidates onto the AndTorque variant — single RPC call site. PushHitbox passing `turnTorqueImpulse=0f` flows correctly.

**Risk:**
- None.

**Open questions:** none.

---

## Cross-cutting concerns

### Skill callsite migration shape — example for PushHitbox

**Before (current):**
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

**After (post-V3):**
```csharp
BuddahPredictionRouter.RouteImpulse(victimNO, _impulse, 0f, BuddahPredictedImpulseSourceType.MeleePush, _attacker);
```

The V2-vs-Legacy verbose `Debug.Log` lines — drop them. Router emits its own log if needed; current behavior is silent on the other 3 callsites anyway, so PushHitbox's verbose was the outlier. If the dev needs hit observation, it's covered by `[CommandBus]:Recv` logs (V2 victim) or `BuddahPredictionPushTargetBox`'s own `verboseLogs` flag (PushTargetBox debug). Legacy Buddah path has no current logging post-migration; if needed, add `Debug.Log` inside Tier 3 of router.

### Migration shape for the other 3 callsites
Identical 1-line replacement:
- `ChargedHandProjectileRuntime.cs:362` → `BuddahPredictionRouter.RouteImpulse(victimNO, _impulse, _hitTurnTorqueImpulse, ChargedProjectile, _attacker);`
- `HandPushProjectileRuntime.cs:212` → `BuddahPredictionRouter.RouteImpulse(victimNO, _impulse, _hitTurnTorqueImpulse, Projectile, _attacker);`
- `BuddahPredictionImpulseDebugBox.cs:45` → `BuddahPredictionRouter.RouteImpulse(victimNetworkObject, impulse, turnTorqueImpulse, DebugBox, null);` (the `fallbackToLegacy` toggle in the debug box becomes unused; either remove the field or leave for V4 cleanup).

### LEGACY_SHADOW try/catch — DROPPED
Per Q1 sub-decision: CombatRouting's `try/catch` around adapter fan-out (lines 56-63) does NOT migrate. Post-Step-1, NEW is authority — exceptions in NEW path are real bugs that should propagate. Router calls adapter directly; if adapter throws, the exception escapes to caller (skill site, then to skill execution layer). Current callsite skills don't have outer try/catch — exception would propagate to FishNet's tick loop and surface as console error. That's correct behavior.

### Tier-3 logging consolidation
Router's Tier 3 fires `BuddahMovement.ApplyPushImpulseAndTorqueTargetRpc` on Legacy Buddahs. Today the per-callsite Debug.Log ("Routed to Legacy") is missing on 3 of 4 callsites. Decision: don't add new logging in router — match the silent majority. If V3 smoke surfaces a need, add Tier 3 log in V3.1 follow-up.

### `BuddahPredictionImpulseDebugBox.fallbackToLegacy` toggle
Today: gates whether to fall through from CombatRouting to BuddahMovement RPC. Post-V3: router internalizes the fallback unconditionally. Toggle becomes dead. Two options:
- (a) **Remove `fallbackToLegacy` field** in V3 implementation.
- (b) **Leave the field**, internally ignored, defer removal to V4 cleanup.

Lean toward **(a) remove now** — V3 touches the file anyway, dead field is dev-time confusing. Tradeoff: minor public-Inspector serialized-field surface change (Inspector users editing the box won't see the toggle anymore). Per CLAUDE.md hard-stop list, "renaming serialized fields" requires explicit approval — REMOVING a serialized field also crosses that line. **Re-vote: leave the field for V4 cleanup**, document it as no-op in V3.

### Verification grep for V3 PR
Methodology Rule 2 mandates: post-implementation grep `BuddahPredictionCombatRouting.TryRouteImpulse` from `Assets/Scripts/Buddah` and `Assets/Scripts/New_Buddah/Debug` MUST return 0 hits (4 callsites all migrated to router).

---

## Awaiting sign-off

Reviewer needs to approve before Stage 4 IMPLEMENT begins:
1. Q0 = (B) static router file at `Assets/Scripts/New_Buddah/Integration/BuddahPredictionRouter.cs` ✓ (lean approved at recon sign-off; this doc commits to specific code shape)
2. Q1 = (B) router does dual-feed, code shape as proposed in Q4 file skeleton
3. Q1 sub-decision: drop CombatRouting's `try/catch` fan-out — DO NOT migrate to router
4. Q2 = (A) no DebugState changes in V3; pendingImpulseSummary defers to V4
5. Q3 = (A)-with-revision: nothing to cache in V3; rationale recorded for V4 reconsideration
6. Q4 = (A) new file location confirmed
7. Cross-cutting: PushHitbox verbose `Debug.Log` lines DROPPED in migration; matches silent majority. Push back if you want them preserved or migrated to router.
8. Cross-cutting: `BuddahPredictionImpulseDebugBox.fallbackToLegacy` toggle LEFT IN PLACE as dead serialized field; defer removal to V4 (per CLAUDE.md hard-stop on serialized field removal).
9. Cross-cutting: skill callsite migration shape — single line `BuddahPredictionRouter.RouteImpulse(...)` replacing the V2-or-Legacy two-tier block at all 4 sites.

If all 9 approved, branch cut from dev (when working tree allows) and Stage 4 IMPLEMENT begins.
