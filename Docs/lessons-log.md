# Lessons Log

Append-only record of agent failures and the rule each one created. New entries at top. When file approaches 90 lines, split: keep recent entries here, move older to `lessons-archive-YYYY-MM.md`.

## How to Use This File

Read this before any edit pass. The harness helper includes it under `requiredDocs` for every intent. Each entry has:
- **Date / intent / system / severity** header
- **L# - one-line title**
- **What happened** - the observable failure
- **Cause** - the wrong assumption or skipped step
- **Rule** - the concrete check to run next time
- **Promoted to** - which authoritative doc now also carries this rule, or `log-only` if too narrow

If you are about to do something covered by a rule below, follow the rule. If you discover a new failure mode during a session, append a new entry before closing the task.

---

## L15 — Shadow comparators must defend against `Compare(default, default) != equals` foot-guns (2026-04-19, Phase 3d V2 rerun)

Symptom: Phase 3d V2 host-only rerun produced 4550 `[D-LOC]` per-field
warnings + 74 `[D-LOC FATAL]` emissions, ALL concentrated on a single
field (`SnapshotRotation`) with a fixed 180.000000° delta. All 14 other
handoff fields were bit-identical. Divergence vanished exactly the
instant `hof-compared` flipped 0→1 (real handoff consume fires on both
sides). Raw Editor.log scrape confirmed zero variance in the delta value
— every warning reported `angle-deg=180.000000`.

Cause: the shadow compare at `BuddahPredictedMotor.cs:1599` used
`Quaternion.Angle(realHof.SnapshotRotation, shadowHof.SnapshotRotation)`.
C# / Unity's `default(Quaternion)` is `(0, 0, 0, 0)` — NOT the identity
quaternion `(0, 0, 0, 1)`. When both sides hold the uninitialized zero
quaternion (the typical steady state while `_handoffState.IsActive ==
false`, since `BuddahPredictedLaunchHandoffResolver.Advance` only writes
`CurrentState` / `BlendAlpha` / `IsActive` and never touches
`SnapshotRotation`), the dot product is `0`, so `Quaternion.Angle`
returns `2 * acos(0) * Rad2Deg = 180°`. Both quaternions ARE equal. Only
the compare tool misreports. The underlying shadow math (resolver +
snapshot flow) was correct — DP-8 refactor was behavior-neutral, all
3a/3b/3c parity still held.

Rule going forward:
  (a) Before `Quaternion.Angle(a, b)` in any shadow or parity compare,
      normalize zero-quaternions to identity — or verify the compared
      slot is gated such that `default(Quaternion)` is structurally
      impossible. The minimal fix shape is:
      ```csharp
      if (a.x == 0f && a.y == 0f && a.z == 0f && a.w == 0f) a = Quaternion.identity;
      if (b.x == 0f && b.y == 0f && b.z == 0f && b.w == 0f) b = Quaternion.identity;
      float delta = Quaternion.Angle(a, b);
      ```
  (b) Do NOT fix this by gating the compare on an `IsActive` flag alone.
      That masks potential real divergence when the state is legitimately
      uninitialized on only one side (asymmetric init bugs). The
      normalization fix preserves divergence detection for any non-zero
      mismatch while defusing the zero-vs-zero foot-gun.
  (c) Audit every `Quaternion.Angle` / `Quaternion.Dot` / `Quaternion.Lerp`
      used in shadow compare or parity validation paths for the same
      vulnerability before the next shadow phase lands.
  (d) This foot-gun generalizes: any `Compare(default, default) !=
      "equals"` shape is a candidate false-positive generator. Examples
      beyond Quaternion: custom `Distance(Color, Color)` implementations
      that divide by magnitude, serialized-field hash functions that
      special-case zero, etc. Add an explicit "is populated" check OR a
      normalize step at the comparator boundary.

Applies to: Phase 3d handoff shadow SnapshotRotation compare (fixed in
this patch), Phase 3b teleport rotation compare (`motor.cs:1376`,
structurally vulnerable — `eventData.TargetRotation = default(Quaternion)`
would trigger the same false positive; gate at motor.cs:1361 requires
`TeleportRan=true` which makes real-session occurrence improbable but
not impossible — tracked as
`agent-exchange/handoff/phase-8-cleanup-queue.md` Entry 2), future
Phase 4-6 reconcile-delivery compare paths.

Promoted to: `log-only` (phase-specific compare fixes are localized).
`agent-exchange/handoff/phase-8-cleanup-queue.md` Entry 2 carries the
3b teleport normalization task. If a broader audit finds additional
sites, promote to `Docs/prediction-refactor-plan/13-validation-gates.md`
as a general shadow-comparator hygiene rule.

## L13 — Shadow PASS requires compared-count > 0, not just divergence = 0 (2026-04-19, Phase 3b V5 R2 close-out)

Symptom: Phase 3b V5 R2 CLIENT peer showed teleport shadow with 0 divergence
across every captured heartbeat (tel-div=0), 0 non-heartbeat `[D-LOC]`, 0
FATAL — but `tel-compared=0` across the entire session window. If we had
used only divergence=0 as the PASS criterion, we would have shipped 3b
without ever validating the teleport shadow code path once.

Cause: the cumulative `tel-compared` counter (added late in 3b) exposed the
pre-existing owner→server→owner teleport RPC silent-drop (see L12). Before
that counter existed, "shadow quiet" was indistinguishable from "shadow
never exercised". Visual respawn still worked on client because FishNet's
reconcile broadcasts the server-side post-teleport rigidbody state — so the
absence of prediction-local teleport side-effects (`ResetModifiers`,
`ResetImpulseQueue`, `ResetPushGrace`, `RebaseTrails`, `SnapProgress`) was
invisible to gameplay. This is a plausible candidate root cause for the
V3 / V4 all-Buddah-shake and remote-flicker symptoms Phase 7 is deferred
to address.

Rule going forward:
  (a) Every shadow-path PASS judgement must assert BOTH divergence=0 AND
      compared-count > 0 (per-peer, with per-peer coverage combinations
      allowed to satisfy the gate for paths whose RPC routing only fires
      on a subset of peers).
  (b) Phase 3c / 3d / future-phase shadow gates MUST include a compared-
      count assertion. "Loc-div=0" style gates are not sufficient.
  (c) When a shadow stays completely silent across a full session, the
      default assumption is "path was never exercised", NOT "path is
      parity-clean". Investigate coverage before celebrating.

Applies to: Phase 3c Modifier shadow, Phase 3d Handoff shadow, Phase 4-6
cutover validation gates.

Promoted to: `Docs/prediction-refactor-plan/13-validation-gates.md` (Phase
3/4+ shadow gate definition should reference this rule).

## L12 — Owner→Server→Owner two-hop predicted-motor RPC chain must have failure visibility (2026-04-19, Phase 3b V5 R2)

Symptom: V5 R2 CLIENT peer `tel-compared=0` across full session despite
user-confirmed visual fall-respawn. HOST peer `tel-compared=3` in same
session proves the server-direct teleport enqueue path works end-to-end.
The client-side chain is: `BuddahRespawn` → bridge → motor.
`RequestAuthoritativeTeleportFromOwner` (BuddahPredictedMotor.cs:886-931) →
ServerRpc → server-side enqueue + TargetRpc back to owner → owner-side
enqueue. On client, the chain makes TWO RPC hops (outbound ServerRpc, return
TargetRpc). Impulse path is a single hop (server is the originator) and
works cleanly (`imp-compared=2` on client).

Cause: `RequestAuthoritativeTeleportFromOwner` at motor.cs:930 unconditionally
returns `true` on the non-server branch — caller (bridge → BuddahRespawn)
treats this as "primary path succeeded" and commits, skipping the legacy
fallback at `BuddahRespawn.cs:168` (`TeleportToWorldPose`, direct
`rb.position = targetPosition`). Whether the ServerRpc actually reached
server, whether the TargetRpc return-leg actually reached client, neither
is reported back. Visual respawn still happens because server-side
`TryApplyServerAuthoritativeTeleport` (motor.cs:713-766) enqueues on
server-side motor → server's tick consumes → server's `rb.position` moves
→ FishNet reconcile broadcasts position delta → client's reconcile snaps
rb.position to match. The client's own `_pendingTeleportEvent` slot is
never populated, so shadow's pre-consume snapshot stays empty.

Rule going forward:
  (a) Any owner→server→owner two-hop predicted-motor RPC chain must EITHER
      (i) propagate the server-side enqueue result back to the owner via
      the return-leg TargetRpc (or a separate result TargetRpc), and have
      the owner commit only on confirmed success, OR (ii) not commit to
      the primary path on the owner side — let the legacy fallback cover
      silent-drop scenarios until cut-over is complete.
  (b) `RequestAuthoritativeTeleportFromOwner` at motor.cs:886-931 needs
      fix before Phase 6 teleport cutover. Candidates (ranked by evidence):
      (1) FishNet `RequestTeleportServerRpc` outbound silent drop due to
      observer state or RequireOwnership race; (2) `QueueTeleportEventTargetRpc`
      return-leg drop due to payload size (14 fields incl Quaternion) or
      tick-timing. Verbose micro-run before writing fix is mandatory.
  (c) Until (a) is implemented, validation of client-initiated predicted
      teleport paths is blocked. Phase 3b accepts V5 R2 as conditional
      PASS on host-validated shadow + defers client-path validation to
      post-Phase-6.

Applies to: Phase 6 teleport cutover (blocking fix), Phase 3d handoff
shadow (same owner→server→owner pattern in `RequestAuthoritativeLaunchHandoffFromOwner`
motor.cs:768-817; verify same silent-return pattern and plan the same
L12 fix shape there).

Promoted to: `Docs/prediction-refactor-plan/phase-6-prerequisites.md`
(new file, this one bullet).

---

## L11 — Self-cast push projectile fizzles silently at the attacker-hit gate (2026-04-19, Phase 3b V5 Run 2)

Symptom: V5 Run 2 CLIENT peer shows 0 non-heartbeat `[D-LOC]`, 0 FATAL, clean
parity — but cumulative `imp-compared` and `tel-compared` stay at 0 across 8
heartbeats spanning the full captured window. User confirmed self-push with the
normal `push_projectile_hands` skill and at least one respawn, so shadow ought to
have observed events.

Root cause: `Skill_PushProjectileHands` (the normal, non-anti variant) fires
through `BuddahHandControl.SpawnProjectilePushServer`, which leaves
`allowSelfHit` at its default `false` (BuddahHandControl.cs:804 default,
invoked at BuddahHandControl.cs:593 with 7 positional args). The projectile's
trigger handler at HandPushProjectileRuntime.cs:204 then gates
`if (!_allowAttackerHit && victimNO == _attacker) return;` — self-hits are
early-returned BEFORE `BuddahPredictionCombatRouting.TryRouteImpulse`. Nothing
reaches `TryApplyServerAuthoritativeImpulse`, nothing enqueues on either peer.
Only the Anti variant (`Skill_PushProjectileHands_Anti.cs:50`) sets
`allowSelfHit: true`. No ScriptableObject field exposes this — it is hard-wired
per-skill in C#.

Rule going forward:
  (a) Shadow coverage for impulse paths cannot be validated by self-push with
      the normal `push_projectile_hands`. Coverage tests must use cross-peer
      pushes (peer A casts toward peer B's Buddah) OR the Anti skill.
  (b) When a V5-style gate asserts `imp-compared > 0` and the counter stays 0,
      do NOT assume the shadow snapshot/queue copy is broken. First verify that
      the skill actually reached `TryRouteImpulse` — easiest proof is to cross-
      peer push and watch the counter jump.
  (c) `allowAttackerHit` being hardcoded in C# (not on the asset) means any
      future "allow self-push for testing" toggle requires a code change. Do
      not add such a toggle to production skill configs.

Applies to: Phase 3b V5 coverage gate, all future cross-peer shadow coverage
phases (3c modifier cast-on-self, 3d handoff cast-on-self).

Promoted to: log-only (scope is narrow to impulse-cast tests).

## L10 — Skill event-queue routing is gated by `IsPredictionModeActive()`; fallback paths bypass the motor queue (2026-04-19, Phase 3b audit)

Symptom: V5 Run 2 CLIENT peer ran for 60s+, shadow locomotion heartbeats
emitted with divergences=0 throughout, yet `imp-compared=0 tel-compared=0` for
the whole session. Shadow impulse/teleport step invocation was verified (no
`IsOwner`/`IsServerInitialized` gate on the pre-consume block at
BuddahPredictedMotor.cs:323-336). The gap was in the upstream routing.

Root cause:
  (1) Push projectile → `BuddahPredictionCombatRouting.TryRouteImpulse`
      (BuddahPredictionCombatRouting.cs:25) gates routing on
      `bootstrap.IsPredictionModeActive() == true`. On true, calls
      `predictedMotor.TryApplyServerAuthoritativeImpulse` →
      `TryQueueImpulseEvent` → `_impulseEventQueue.TryEnqueue` — shadow sees it.
      On false, falls back to `PushTargetBox` or the legacy
      `BuddahMovement.ApplyPushImpulseAndTorqueTargetRpc` →
      `ApplyPushAndTorqueLocal` → direct `rb.AddForce(impulse, Impulse)` at
      BuddahMovement.cs:445 — queue bypassed, shadow blind.
  (2) Respawn → `BuddahRespawn.TeleportToTrackProgress` (line 156) gates on
      `predictionBootstrap.IsPredictionModeActive() && predictionRespawnBridge
      != null`. On true, bridges into `predictedMotor
      .RequestAuthoritativeTeleportFromOwner` → `TryQueueTeleportEvent` →
      `_pendingTeleportEvent` — shadow sees it. On false, falls back to
      `BuddahRespawn.TeleportToWorldPose` — direct `rb.position = targetPosition`
      at line 187 — queue bypassed, shadow blind.
  Each gameplay path has a predicted-queue branch AND a legacy-direct-rb
  branch. Shadow only sees the predicted-queue branch.

Rule going forward:
  (a) Shadow coverage assertions (`imp-compared > 0`, `tel-compared > 0`) prove
      the predicted-queue branch was exercised, not just that the gameplay
      action happened. A green game action + 0 counter = the action took the
      legacy branch.
  (b) When writing shadow coverage tests, explicitly note whether the test
      depends on `IsPredictionModeActive() == true` on every peer involved. For
      push, both the attacker's server-side and the victim's owner-side must be
      in PredictionV2 mode; for respawn, the bootstrap and the respawn bridge
      must both be present and active.
  (c) Phase 4 authority cut-over should collapse the dual-branch pattern by
      deleting the legacy-direct-rb branches. Until then, coverage tests must
      verify the predicted branch wins, not just that the gameplay succeeded.

Applies to: Phase 3b coverage gate (impulse, teleport), Phase 3c modifier
coverage gate (same pattern: modifier commands have legacy + predicted paths),
Phase 3d handoff coverage gate.

Promoted to: log-only (narrow to dual-branch coverage gating).

---

## L9 — Pre-existing motor quirk: ClampPlanarSpeed strips same-tick queued impulses (2026-04-18, Phase 3b audit)

Observed during Phase 3b pre-execution audit (no failure yet, preventive entry).

Motor flow in `BuddahPredictedMotor.RunInputs`:
  1. `ConsumePendingImpulseEvents(currentTick)` — queues `_predictionRigidbody.AddForce(impulse, ForceMode.Impulse)` into `_pendingForces`. Not yet drained to PhysX.
  2. `ClampPlanarSpeed(...)` — if planar speed over cap, calls `_predictionRigidbody.Velocity(clampedVel)`. Per `PredictionRigidbody.Velocity` (PredictionRigidbody.cs:358), this writes `rb.velocity` immediately AND calls `RemoveForces(nonAngular: true)`, which STRIPS pending `AddForce` / `AddRelativeForce` / `AddExplosiveForce` entries — including the impulse queued one step prior.
  3. `_predictionRigidbody.Simulate()` — drains remaining queue.

So: when an impulse arrives on a tick that also triggers clamp, the linear component of the impulse is silently dropped at the PhysX layer. The torque component survives (RemoveForces(nonAngular:true) preserves AddTorque entries).

Rule going forward:
  (a) This is pre-existing motor behavior. Phase 3b shadow MUST mirror it bit-for-bit — shadow's `ShadowLastConsumedImpulseId` still marks the event consumed (it WAS removed from the queue), only its PhysX effect is partially lost. Do NOT "fix" this in 3b. Commanded-value compare tolerates it; velocity-delta compare would not, which is one reason 3b defers velocity-delta.
  (b) Phase 4 authority cut-over must explicitly evaluate whether to preserve or change this behavior. Candidates: re-order consume + clamp so impulse runs after clamp; or scale impulse before clamp instead of dropping. Decision belongs to Phase 4 design, not 3b.
  (c) If Phase 4 changes the behavior, an L-entry for the behavior flip is mandatory so shadow comparators in later phases stay aligned.

Applies to: Phase 3b shadow (accept as-is), Phase 4 authority cut-over (must decide), Phase 3c/3d shadows (no direct impact).

---

## L8 — Shadow parity must mirror motor's intermediate transformations, not idealized Euler (2026-04-18, Phase 3a V2 Run 1)

Symptom: 99 [D-LOC] warnings on V2 Run 1. Two distinct patterns:
  - fwd=7.5→5.0→2.5 three-tick ramp on T=3245-3247
  - clamp-gate mismatch realClamp=True shadowClamp=False sustained across ~300 ticks

Root cause 1: Shadow consumed raw input.Throttle / input.Steering. Motor applies
ApplyLaunchHandoffInputScaling (motor.cs:1537) which decays these values during
Blend/Inherit launch phases. Shadow was mathematically correct against idealized
input but wrong against motor's actual input.

Root cause 2: BuildTickContext read rb.velocity AFTER ClampPlanarSpeed ran.
Motor's ClampPlanarSpeed writes via _predictionRigidbody.Velocity() which updates
rb.velocity IMMEDIATELY (PredictionRigidbody.cs:363). Shadow saw post-clamp state
and its ClampingApplied flag could never evaluate True when motor's did.

Rule going forward:
  (a) Shadow steps take the motor's POST-transformation values as inputs via
      TickContext parameters — not raw replicate data. If motor transforms input
      X into X' before consuming it, shadow must receive X', not X.
  (b) Any motor state that is read during gate evaluation or formula execution
      AND mutated later in the same tick must be snapshotted at the read-point,
      not looked up live by shadow. Precedent: _shadowPreClampVelocity (state
      timing). The earlier _shadowAuthHandoffSnapshot precedent was designed but
      dropped once the motor's own early-return gate was deemed sufficient — see
      Phase 3a commit "Gate mirror" section for details.
  (c) "Independence gaps" (places where shadow still consumes motor's computed
      value instead of recomputing) are acceptable as long as they are explicitly
      called out in the commit message and deferred to a named later phase. 3a
      defers resolver math to 3c and handoff scaling to 3d.

Applies to: all subsequent shadow phases (3b Impulse+Teleport, 3c Modifier, 3d Handoff).

---

## 2026-04-18 | refactor | prediction | med (Phase 4 watchpoint)

**L7 - Lifecycle hooks can silently double-fire state-clearing calls**
- **What happened**: Phase 2 V8 expected 2 `[CommandBus]:ClearAll` lines per Buddah spawn (one bus-internal, one bridge-wrap). Actual log showed 4 lines per spawn across all 3 runs. Cause: `BuddahMovementModeSwitcher.Awake()` and `BuddahMovementModeSwitcher.OnEnable()` both call `ApplyRuntimeMode(force:true)`, so `BuddahPredictionLegacyIsolationBridge.ApplyMode` runs twice on spawn, each producing a ClearAll pair. Harmless at Phase 2 because no adapter enqueues yet, so channels are empty both times.
- **Cause**: Defensive duplication in a lifecycle-sensitive component. Awake and OnEnable normally fire sequentially on an enabled GameObject; the author hedged by calling ApplyMode from both.
- **Rule**: When a component's role includes clearing or resetting state, audit every lifecycle hook (`Awake`, `OnEnable`, `OnNetworkStarted`, `Start`) for calls into that clear. Document whether double-fire is intended. For Phase 4: any adapter that enqueues into the CommandBus during init (`Awake`/`OnEnable`/`OnStartNetwork`) must either (a) enqueue AFTER Switcher's double-ApplyMode completes (e.g., defer to first `[Replicate]` tick), or (b) add an "init-complete" gate in the Switcher so ApplyMode short-circuits on repeat calls within one frame. Do not silently accept the double-fire, because the second ClearAll will nuke legitimate pending events.
- **Promoted to**: log-only (low severity at Phase 2, revisit at Phase 4 with concrete adapter code).

## 2026-04-18 | refactor | prediction | high

**L6 - Tests must match what the phase actually wires up**
- **What happened**: Phase 1 Serialization Gate was designed as a gameplay-driven check expecting `[SerGate]` log to surface non-default values when teleport/handoff/skills triggered. Two 20+ second 2-peer playtests both showed all-default values (`pendingTp=False`, `impHead=0`, etc.); second test confirmed a skill cast occurred but `impHead` stayed 0. Initially misread as either a gameplay-window issue or a serializer bug.
- **Cause**: Phase 1 deliberately zero-inits new reconcile fields and does NOT mirror motor private state (`_impulseEventQueue`, `_pendingTeleportEvent`, `_awaitingAuthoritativeLaunchHandoff`, etc.) into them - that mirroring is Phase 2/3 work, and the new window's Phase 1 report stated this explicitly. The test was designed against state the phase explicitly does not touch, so no amount of gameplay can exercise the new fields.
- **Rule**: Before designing a verification test for a phase, re-read the phase's scope contract. Identify which old paths still own state and which new paths are not yet wired. If the test depends on data flowing through code that this phase explicitly does not touch, the test is invalid for this phase. For "shape exists but nothing writes to it yet" phases, use a forced-value probe: server-side direct write of distinctive constants (e.g., `0xDEADBEEF`, `(123.45, 67.89, -12.34)`) into the exact field inside `CreateReconcile()`, then verify the constants arrive on a `!IsOwner` peer. Remove the probe before phase commit.
- **Promoted to**: log-only.

## 2026-04-17 | refactor | prediction | high (preventive)

**L5 - Reconcile/Input data serializer must include new fields**
- **What happened**: Not yet observed; flagged during Phase 1 authorization.
- **Cause**: When extending FishNet `IReconcileData` / `IReplicateData`, compile passes and a quick playtest may also pass, but reconcile silently loses the new field's value if the serializer (auto-attribute or hand-written Write/Read) does not include it. Symptom is a delayed desync, not a build error.
- **Rule**: After adding any field to ReconcileData or InputData, audit the serialization path. If auto-attribute, confirm the attribute is present and the type is supported. If hand-written, confirm both Write and Read cover the new field. Add a one-shot `Debug.Log` of one new field value in `[Reconcile]` for the first playtest and confirm a non-default value arrives on a remote peer.
- **Promoted to**: `Docs/prediction-refactor-plan/03-data-contracts.md` Serialization Gate section.

## 2026-04-17 | refactor | prediction | med

**L4 - Strategy pivot must update onboarding artifacts**
- **What happened**: Plan pivoted from "run FishNet API spike" to "trust-and-warn at call sites". `16-api-spike-checklist.md` and `handoff-context.md` were updated, but the opening prompt template handed to the next agent still said "first run the spike", causing the new window to (correctly) flag the contradiction and refuse to proceed.
- **Cause**: Pivot didn't propagate to all entry points (prompts, status headers, skill stubs).
- **Rule**: When a strategy pivots, grep the plan for the pre-pivot phrasing and update every hit. Update onboarding/handoff prompts, index status header, and any skill files that reference the old strategy. Treat the pivot as not landed until grep is clean.
- **Promoted to**: `Docs/prediction-refactor-plan/handoff-context.md` Pivot Sync Checklist footer.

## 2026-04-17 | any | any | high

**L3 - Delete tracked files through the VCS, not around it** *(updated 2026-04-18: repo migrated to git-only; Perforce is now decoration)*
- **What happened**: `rm Assets/Scripts/Sandbox/PredictionApiSpike.cs` returned `Operation not permitted` because Perforce kept tracked files read-only at the time. On 2026-04-18 the user confirmed the project now runs on git; `p4` is not used for commits anymore even though the workspace path still contains "Perforce".
- **Cause**: Agent reached for a bare `rm` without thinking about which VCS owns the index and how the deletion becomes a staged change.
- **Rule**: This repo is git. Delete tracked files with `git rm <path>` so the deletion is staged automatically. If you used plain `rm` (allowed under git because files are writable), immediately follow with `git add -A` to stage the delete. Never commit a state where a tracked file is gone from disk but still present in the index. Do not run any `p4` command - it will either do nothing useful or create noise. The directory name "Perforce" in the absolute path is historical; trust git.
- **Promoted to**: `Docs/safety.md` Absolutely Forbidden (rewritten 2026-04-18).

## 2026-04-17 | refactor | prediction | high

**L2 - FishNet API names: grep source, do not infer**
- **What happened**: Wrote `PredictionRigidbody.Position(...)` and `.Rotation(...)` in plan docs and spike code. Actual API is `MovePosition(Vector3)` / `MoveRotation(Quaternion)` (Unity-Rigidbody-style naming). Caught at compile, but propagated to four plan docs first.
- **Cause**: Inferred the API name from naming intuition instead of grepping the FishNet source tree.
- **Rule**: Before the first call site of any FishNet API symbol, grep `Assets/FishNet/Runtime/` for the exact method name. Never write a FishNet API call from memory. If the symbol is not in the source tree, the assumption is wrong - stop and resolve before writing.
- **Promoted to**: `Docs/prediction-refactor-plan/16-api-spike-checklist.md` preamble.

## 2026-04-17 | refactor | prediction | med

**L1 - Phase stub completeness: cross-reference the API spec, not the example list**
- **What happened**: Phase 0 created 3 payload stubs (Impulse / Teleport / Modifier) when the canonical command bus API requires 4 (missing `HandoffCmd`).
- **Cause**: Implementer treated the `Payloads/*.cs (ImpulseCmd, TeleportCmd, ModifierCmd...)` example in `02-directory-structure.md` as exhaustive.
- **Rule**: When a phase creates a family of files (steps, adapters, payloads, channels), the count comes from the spec that defines the full API surface, not from an example list. For Phase 0-2 in this refactor that means: structure (02) + data contracts (03) + command bus API (06) + adapters (09).
- **Promoted to**: `Docs/prediction-refactor-plan/12-migration-sequence.md` Phase 0 cross-reference gate.
