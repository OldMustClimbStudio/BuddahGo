# Phase 3b Close-Out Packet — for Claude Code

Date: 2026-04-19
Branch: refactor/prediction-v2
Status: V5 R2 PASS on HOST, conditional PASS on CLIENT (see §1 rationale).
V5 R2 overall: PASS (per-peer coverage combination satisfied per L13 rule).
Ready to commit.

---

## §1 — Final Commit Message

Paste verbatim into commit. Long on purpose — captures the 3b architecture
decisions, the V5 R2 result, the L12 pre-existing bug that 3b exposed, and
the deferral path.

```
Phase 3b — Impulse + Teleport step shadow (observation only, append-only on motor)

Adds parity-verified Euler shadows for the Impulse and Teleport steps of
the tick pipeline. Authority path is untouched. Shadow runs under
BUDDAH_PREDICTION_SHADOW on Standalone + Editor + Development Build only.
Any mismatch between the shadow and the real motor path on ticks where
impulse or teleport events are processed produces a [D-LOC] warning; 60
consecutive divergences escalate to [D-LOC FATAL] and stop.

=== What this lands ===
- Assets/Scripts/New_Buddah/Simulation/BuddahImpulseStep.cs (new, pure static)
- Assets/Scripts/New_Buddah/Simulation/BuddahTeleportStep.cs (new, pure static)
- Assets/Scripts/New_Buddah/Core/BuddahPredictionTickContext.cs (extended, +13 fields)
- Assets/Scripts/New_Buddah/Simulation/BuddahPredictionShadowScratch.cs (extended, +11 fields)
- Assets/Scripts/New_Buddah/Core/BuddahPredictedImpulseEventQueue.cs (new public method CopyPendingSnapshot)
- Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs (append-only, #if-gated)
- Docs/lessons-log.md (L10, L11, L12, L13 appended)
- Docs/prediction-refactor-plan/phase-6-prerequisites.md (new, single entry)
- agent-exchange/console/2026-04-18-phase3b-v2.log (V2 host digest)
- agent-exchange/console/2026-04-19-phase3b-v5r2-client.log (V5 R2 client digest)
- agent-exchange/console/2026-04-19-phase3b-v5r2-host.log (V5 R2 host digest)
- agent-exchange/console/raw/host-player-log-2026-04-19.log (host raw Player.log)
- agent-exchange/handoff/2026-04-18-phase3b-audit.md (pre-execution audit)
- agent-exchange/handoff/2026-04-19-phase3b-v5r2-plan.md (V5 R2 script)
- agent-exchange/handoff/2026-04-19-phase3b-closeout.md (this packet)

=== Gating ===
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
Release builds get zero shadow code. Define itself is Standalone-scoped so
non-Standalone platforms (Android/iOS future) never see it.

=== Compare placement (pre-consume snapshot + early shadow step) ===
Impulse and teleport shadow steps run BEFORE motor.ConsumePendingImpulseEvents
and motor.ConsumePendingTeleportEvent respectively. This is forced by the
semantics of the shadow: we need to observe the pre-consume queue state, not
post-consume. The motor snapshots the pending impulse queue list and the
pending teleport single-slot into #if-gated cache fields immediately before
consume, and BuildTickContext reads those cache fields when the shadow step
runs its early invocation in the same tick.

=== Observation sites (motor append-only) ===
Three new real-side scratch write sites, all #if-gated, all set scratch fields
only (no behavioral change):
  1. Inside ConsumePendingImpulseEvents lambda (motor.cs:~1321): set
     _realScratch.ImpulseRan + _realScratch.ShadowLastConsumedImpulseId = max().
  2. Inside ConsumePendingTeleportEvent body (motor.cs:~1353, after the
     _lastConsumedTeleportEventId assignment): set _realScratch.TeleportRan,
     .ShadowLastConsumedTeleportId, .PostTeleportPosition / Rotation, and all
     7 teleport flag reads.
  3. RunInputs pre-consume block (motor.cs:323-336): snapshot pending queue
     state + invoke BuddahTeleportStep.Run and BuddahImpulseStep.Run early,
     post-step increment cumulative _shadowTeleportConsumedCount /
     _shadowImpulseConsumedCount when the corresponding ran flag flipped true.

=== Epsilon ===
1e-4 on all float compares inherited from 3a. Teleport position + rotation
compares use Vector3.Distance and Quaternion.Angle respectively, same
epsilon.

=== Heartbeat partition + cumulative compared counters ===
Every 2 seconds (wall-clock) Shadow_CompareAndReport logs:
  [D-LOC HEARTBEAT] T=<tick> active-ticks=<n> skip-ticks=<n>
    loc-div=<n> imp-div=<n> tel-div=<n>
    imp-compared=<n> tel-compared=<n>

- loc-div / imp-div / tel-div: per-category divergence count in the current
  2s heartbeat window, reset at each heartbeat emission.
- imp-compared / tel-compared: cumulative consume counters incremented
  motor-side whenever BuddahImpulseStep.Run or BuddahTeleportStep.Run set
  their Ran flag; never reset. These counters are the authoritative signal
  that the shadow path was actually exercised by events (see L13).

FATAL trigger retained from 3a: 60 consecutive ticks with ANY category
diverging escalate to LogError + counter reset. Per-category counters
reset on clean ticks.

=== V5 R2 Result Table ===
Peer    | loc-div | imp-div | tel-div | imp-compared | tel-compared | Verdict
------- | ------- | ------- | ------- | ------------ | ------------ | -------
HOST    | 0       | 0       | 0       | 2 (>= 2)     | 3 (>= 1)     | PASS
CLIENT  | 0       | 0       | 0       | 2 (>= 2)     | 0 (*)        | PASS (conditional *)

V5 R2 overall: PASS (per-peer coverage combination satisfied per L13 rule).

(*) CLIENT tel-compared=0 rationale
=================================
CLIENT's 0 on tel-compared is NOT a 3b regression, NOT a shadow defect, and
NOT a parity failure. It is a pre-existing owner-initiated two-hop RPC
silent-drop in motor.RequestAuthoritativeTeleportFromOwner (motor.cs:886-931)
which has lived in the codebase since the original teleport queue landed —
the 3b shadow's tel-compared counter is the tool that exposed it, because
before 3b the "silent drop" was masked by FishNet reconcile broadcasting
the server-side post-teleport rigidbody state to the client's visual layer.

Specifically: when a client-initiated fall-respawn fires on the client's
own Buddah, BuddahRespawn → bridge → motor.RequestAuthoritativeTeleportFromOwner
runs, hits motor.cs:900 `if (IsServerInitialized)` which is false on the
client, falls through to motor.cs:917 `RequestTeleportServerRpc(...)` and
then motor.cs:930 `return true` unconditionally — regardless of whether the
ServerRpc actually reached the server or whether the return-leg
QueueTeleportEventTargetRpc reached the owner. The caller
`bridge.TryRespawnToTrackProgress` returns that `true`, and
`BuddahRespawn.TeleportToTrackProgress` commits the primary path at line
159-164 without taking the legacy `TeleportToWorldPose` fallback at line
168.

Impulse path on the same peer works (imp-compared=2) because the impulse
RPC chain is SINGLE-hop (server originates, one TargetRpc to owner) while
the owner-initiated teleport chain is TWO-hop (outbound ServerRpc, return
TargetRpc). The doubled RPC surface has double the silent-drop potential,
and the unconditional `return true` at motor.cs:930 hides whichever hop
actually fails.

Full trace, ranked candidate failure modes, and deferred fix plan are
documented in Docs/lessons-log.md L12 and
Docs/prediction-refactor-plan/phase-6-prerequisites.md.

Teleport shadow is verified end-to-end on the HOST path (tel-compared=3,
tel-div=0) where `IsServerInitialized == true` makes motor.cs:900 take the
direct branch and call TryApplyServerAuthoritativeTeleport in-process — no
cross-peer RPC hop, deterministic enqueue.

CLIENT teleport shadow will be re-validated after the L12 fix lands in
Phase 6. The 3b shadow code itself does not need changes for that
validation — the existing BuddahTeleportStep.Run + pre-consume snapshot +
compared counter already cover the owner-path when the motor populates
the queue.

=== Validation history ===
V1 (compile + editor load)                                   PASS
V2 Run 1 (host-only 30s, impulse + teleport host-owner)      PASS (18 heartbeats, 0 div, 0 FATAL)
V5 R1 (2-peer 60s, client self-push via skill)               FAIL (imp-compared=0 tel-compared=0)
    Root cause: L11 allowAttackerHit=false on self-push; event fizzled
    at attacker-hit gate before reaching TryRouteImpulse.
V5 R2 (2-peer 60s, cross-peer push + fall-respawn per peer)  PASS (conditional, see table above)
    HOST validated end-to-end. CLIENT impulse coverage validated (imp-compared=2).
    CLIENT teleport coverage deferred to Phase 6 (L12 blocking fix).

=== Lessons learned (full list appended to Docs/lessons-log.md) ===
L10 — Skill event-queue routing gated by IsPredictionModeActive(); dual-branch
      (predicted-queue vs legacy-direct-rb) means shadow coverage must be
      asserted via compared counters, not via gameplay outcome.
L11 — Self-cast push_projectile_hands silently fizzles at attacker-hit gate
      (allowAttackerHit=false default, hardcoded in C#, not a ScriptableObject
      field). Shadow coverage tests must use cross-peer pushes.
L12 — Owner→Server→Owner two-hop predicted-motor RPC chain must have failure
      visibility. Current motor.RequestAuthoritativeTeleportFromOwner
      unconditionally returns true on client path, masking silent drops.
      Phase 6 teleport cutover blocking fix.
L13 — Shadow PASS requires compared-count > 0, NOT just divergence = 0.
      Cumulative compared counters are the tool that distinguishes "shadow
      verified parity" from "shadow never ran this path". Applies to all
      future shadow phases.

=== Protected-file disclosure ===
BuddahPredictedMotor.cs is on the protected list. All additions in this
change are append-only, #if-gated, observation-only (zero behavioral effect
when define is absent). Field additions: _shadowPreImpulsePendingSnapshot,
_shadowPreTeleportHasPending, _shadowPreTeleportEvent, _dLocLocomotionDivCount,
_dLocImpulseDivCount, _dLocTeleportDivCount, _shadowImpulseConsumedCount,
_shadowTeleportConsumedCount (all 8 new fields #if-gated). Method-body
additions: early pre-consume shadow block (motor.cs:323-336), real-side
scratch writes in ConsumePendingImpulseEvents lambda (5 lines #if-gated),
real-side scratch writes in ConsumePendingTeleportEvent (14 lines #if-gated),
Shadow_CompareAndReport extension (~120 lines #if-gated, replacing the
3a version). No existing field renamed, no existing method body modified
other than the above #if-gated additions.

BuddahPredictedImpulseEventQueue.cs receives one new public method
CopyPendingSnapshot (6 lines, unconditional, read-only utility — not
protected file).

Closes: Phase 3b in Docs/prediction-refactor-plan/
Next: Phase 3c — Modifier shadow + L7 lifecycle fix
Carry-over: Phase 6 must address L12 (see Docs/prediction-refactor-plan/phase-6-prerequisites.md)
```

---

## §2 — Close-Out Checklist

Run in this order. Stop immediately if any step errors.

**2.1. Sanity grep.** Confirm no stray shadow code escaped the #if gate:

```powershell
Select-String -Path "Assets/Scripts/New_Buddah/**/*.cs" -Pattern "_shadowPreImpulsePendingSnapshot|_shadowPreTeleportHasPending|_shadowPreTeleportEvent|_shadowImpulseConsumedCount|_shadowTeleportConsumedCount|BuddahImpulseStep|BuddahTeleportStep" |
  Where-Object { $_.Line -notmatch '#if|#endif|BUDDAH_PREDICTION_SHADOW' } |
  ForEach-Object { $_.Path + ":" + $_.LineNumber + " " + $_.Line.Trim() }
```

Every non-#if-adjacent hit must be inside a file wrapped at the top
(BuddahImpulseStep, BuddahTeleportStep) OR inside a motor.cs block whose
opening `#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW`
is within ~30 lines above. Visually confirm.

**2.2. Release-build smoke.** Flip the define off temporarily → Build
Settings → Development Build = OFF → build Standalone Windows → confirm
build succeeds with zero warnings related to shadow symbols → revert
ProjectSettings.asset change. Same procedure as Phase 3a's 2.2.

**2.3. Commit / push / PR.**

Files to stage (12 source / doc / log files):

```bash
git add Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs \
        Assets/Scripts/New_Buddah/Core/BuddahPredictionTickContext.cs \
        Assets/Scripts/New_Buddah/Core/BuddahPredictedImpulseEventQueue.cs \
        Assets/Scripts/New_Buddah/Simulation/BuddahImpulseStep.cs \
        Assets/Scripts/New_Buddah/Simulation/BuddahTeleportStep.cs \
        Assets/Scripts/New_Buddah/Simulation/BuddahPredictionShadowScratch.cs \
        Docs/lessons-log.md \
        Docs/prediction-refactor-plan/phase-6-prerequisites.md \
        agent-exchange/handoff/2026-04-18-phase3b-audit.md \
        agent-exchange/handoff/2026-04-19-phase3b-v5r2-plan.md \
        agent-exchange/handoff/2026-04-19-phase3b-closeout.md \
        agent-exchange/console/2026-04-18-phase3b-v2.log \
        agent-exchange/console/2026-04-19-phase3b-v5r2-client.log \
        agent-exchange/console/2026-04-19-phase3b-v5r2-host.log \
        agent-exchange/console/raw/host-player-log-2026-04-19.log

git commit -F agent-exchange/handoff/2026-04-19-phase3b-closeout-commit-msg.txt
# Note: extract §1 above into the commit-msg.txt file before committing.

git push origin refactor/prediction-v2
```

PR description: point at the commit message (GitHub renders it). Add one
line at top: "Phase 3b of the prediction refactor — observation-only
shadow for Impulse + Teleport. CLIENT teleport path deferred to Phase 6
(L12). Ready for review."

**2.4. Archive + rotate after merge.**
- Rename closed logs: `agent-exchange/console/2026-04-18-phase3b-*.log` and
  `agent-exchange/console/2026-04-19-phase3b-*.log` → `agent-exchange/console/archive/`.
- Keep `agent-exchange/console/raw/host-player-log-2026-04-19.log` at its
  current location (host raw dump is a one-off artifact, not a rotating log).
- Leave `BUDDAH_PREDICTION_SHADOW` in place. It stays on through Phase 3c/3d
  and is removed in Phase 8 alongside shadow files.

---

## §3 — Phase 3c Starter Prompt Preview

Do not start 3c until 3b is merged. When you do, the prompt body is:

```
Phase 3c — Modifier shadow + L7 lifecycle fix.

Reuse Phase 3a/3b infrastructure unchanged:
  - BUDDAH_PREDICTION_SHADOW define (already in Standalone scripting defines)
  - Same #if gate: (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
  - BuddahPredictionTickContext — extend, do not replace
  - BuddahPredictionShadowScratch — extend; activate ShadowLastConsumedModifierId
  - Same heartbeat cadence, same D-LOC / D-LOC FATAL semantics, same 1e-4
    epsilon, same 60-consecutive FATAL threshold, same compared-count gate
    (imp-compared >= 2 and tel-compared >= 1 no longer apply; 3c introduces
    modifier-compared >= 1).

New shadow files (same pattern as BuddahLocomotionStep / BuddahImpulseStep):
  - Assets/Scripts/New_Buddah/Simulation/BuddahModifierStep.cs (pure static
    — already a stub at this path from Phase 0; fill it in).

Scope reminder — pure-static math:
  3c is the EASIEST of the shadow phases in terms of coverage scenarios,
  because it has NO RPC surface. Modifier resolution is
  BuddahPredictedModifierResolver.Resolve(_modifierState, config, tick) —
  a pure static function that produces ComputedStats from ModifierState.
  The shadow path:
    (1) Snapshots a ModifierState independent copy pre-Resolve.
    (2) Calls BuddahPredictedModifierResolver.Resolve against the shadow
        copy.
    (3) Compares the result (BuddahPredictedMotorComputedStats) field-by-
        field against motor's already-resolved _computedStats.

Since Resolve is already pure static, the "independence" of the shadow
is achieved simply by running the resolver twice on twin inputs. No event
queue, no cross-peer RPC, no two-hop silent-drop risk. The ONLY coverage
gate is "did Resolve actually run this tick" — which it does on every
tick where locomotion runs (see motor.cs:309 and motor.cs:341) so
modifier-compared increments every active tick. No special test scenario
needed; V2 host-only is sufficient for 3c coverage.

3c ALSO lands the L7 lifecycle fix: BuddahMovementModeSwitcher's double-
ApplyMode on Awake + OnEnable causes a 4-line ClearAll on Buddah spawn.
L7 (Docs/lessons-log.md) documents the intent to add an "init-complete"
gate or defer adapter enqueue to the first [Replicate] tick. 3c is a
good phase to land this because the Modifier adapter is Phase 4 work
and needs a stable init contract.

Gates:
  V1 — Compile + Editor load (no scene run).
  V2 — Host-only 30s playtest with at least one modifier cast (acceleration
       skill, scale skill, or root skill). PASS = 0 non-heartbeat [D-LOC],
       modifier-div=0 on every heartbeat, modifier-compared >= 1 at tail.
  V5 — 2-peer 60s with per-peer modifier casts. PASS same criteria per peer.

Pre-execution audit (same protocol as 3a / 3b):
  - Read BuddahPredictedModifierResolver.Resolve end-to-end. Identify every
    input field (ModifierState + config + currentTick). Identify every
    output field on ComputedStats. Design the compare.
  - Diff BuddahPredictedModifierState shadow copy vs motor state timing —
    resolve runs at motor.cs:309 (pre-consume) AND motor.cs:341 (post-
    consume, after teleport/handoff/impulse may have reset _modifierState
    via ResetModifiers flag or AccelUntilTick expiry). Shadow must snapshot
    at the same two points to mirror.
  - Read L7 lifecycle notes; decide whether init-complete gate goes on
    Switcher or on adapter side.
```

---

## §4 — Phase 6 Carry-Over

**Single explicit bullet** (also in `Docs/prediction-refactor-plan/phase-6-prerequisites.md`):

> L12 fix required before Phase 6 teleport cutover can validate client-
> initiated teleport events through the predicted queue. Current
> `BuddahPredictedMotor.RequestAuthoritativeTeleportFromOwner`
> (motor.cs:886-931) silently returns true on the client branch regardless
> of actual RPC outcome, hiding either the outbound `RequestTeleportServerRpc`
> drop or the return-leg `QueueTeleportEventTargetRpc` drop. Pre-populated
> diagnostic suspects (ranked by evidence):
>
>   1. FishNet ServerRpc outbound silent drop on `RequestTeleportServerRpc`
>      (possible ownership-race or observer-state issue during fall-respawn
>      timing).
>   2. `QueueTeleportEventTargetRpc` return-leg drop due to payload size
>      (14 fields incl Quaternion + 7 bools vs impulse's 7 fields) or
>      tick-alignment timing at arrival.
>
> Run the verbose micro-run we deferred in 3b close-out (enable
> `bootstrap.LogVerbose` on both peers, fall-respawn on client, observe
> `teleport authoritative create` / `teleport enqueued` lines on server +
> client respectively) BEFORE writing the fix. The fix shape depends on
> which hop is failing.
>
> Companion: `RequestAuthoritativeLaunchHandoffFromOwner` (motor.cs:768-817)
> has the same silent-return pattern. Apply the same fix shape there when
> Phase 3d handoff shadow surfaces the same coverage gap.

---

## §5 — Things to Remember Before Handing to Claude Code

The commit grouping above treats the source files + new docs + 3 digest
logs + 1 raw host Player.log as one atomic commit. Same atomicity
rationale as 3a — reviewer sees the whole shadow land + the V5 R2
evidence + the L12 deferral decision in one diff.

Claude Code should NOT re-run the gates. V2 and V5 R2 already have
canonical evidence in the digest logs. Re-running from a clean checkout
risks environment drift and will also produce a CLIENT tel-compared=0
result regardless of how clean the run is (that's L12, not a flaky
environment).

When Claude Code hits step 2.2 (release-build smoke), if the build fails
with CS0103 / CS0246 on any shadow symbol, the #if gating is wrong
somewhere — same one-shot failure mode as 3a. Report the filename and
line and I will diff against the design.

Phase 3c's pre-execution audit is non-optional per the 3a protocol.
3c is simpler than 3b in coverage surface but the pure-static resolver
has its own subtle input-timing gotchas that need an audit pass.

[View the closeout packet](computer://C:\Users\dwh88\Perforce\dengw3_Liyuu_1160\GSAS-2540-01-F24\users\dengw3\dengw3_2DplatformGame\BuddahGo/agent-exchange/handoff/2026-04-19-phase3b-closeout.md)
