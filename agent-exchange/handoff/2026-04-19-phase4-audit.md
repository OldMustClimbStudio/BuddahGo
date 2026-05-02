# Phase 4 Pre-Execution Audit — Locomotion + Impulse Cut-over

Date: 2026-04-19
Branch: refactor/prediction-v2 (Phase 3d merged; see `2026-04-19-phase3d-closeout.md`)
Scope: **read-only**. No `.cs` edits, no adapter wire-up, no Phase 4 implementation started. Findings for reviewer before Phase 4 V1 implementation begins.

Files inspected (non-exhaustive):
- `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs`
  (locomotion block ~441-464, ClampPlanarSpeed ~1209-1223,
   ConsumePendingImpulseEvents 1735-1777, TryApplyServerAuthoritativeImpulse 729-753,
   motor-owned `_impulseEventQueue` declaration motor.cs:37, pre-consume snapshot motor.cs:342,
   shadow capture at locomotion callsites motor.cs:444-445, motor.cs:1752-1756)
- `Assets/Scripts/New_Buddah/Simulation/BuddahLocomotionStep.cs` (59 lines, pure static, shadow)
- `Assets/Scripts/New_Buddah/Simulation/BuddahImpulseStep.cs` (55 lines, pure static, shadow)
- `Assets/Scripts/New_Buddah/Events/BuddahPredictionCommandBus.cs` (178 lines, live channels + TargetRpcs)
- `Assets/Scripts/New_Buddah/Integration/BuddahPredictionCombatAdapter.cs` (Phase 0 empty stub)
- `Assets/Scripts/New_Buddah/Integration/BuddahPredictionCombatRouting.cs` (active static routing class)
- `Assets/Scripts/New_Buddah/Integration/BuddahPredictionLegacyIsolationBridge.cs` (ApplyMode → TryClearChannels)
- `Assets/Scripts/New_Buddah/Bootstrap/BuddahMovementModeSwitcher.cs` (Awake + OnEnable both call ApplyRuntimeMode)
- `Assets/Scripts/New_Buddah/Core/BuddahPredictedReconcileData.cs` (70 lines; `ImpulseQueueState` at line 31)
- `Docs/lessons-log.md` (L7, L9, L10, L12, L13, L15 all Phase-4-relevant)
- `Docs/prediction-refactor-plan/phase-4-prerequisites.md` (Prereq-1 Option B recommendation)
- `Docs/prediction-refactor-plan/12-migration-sequence.md` (Phase 4 scope spec)
- `Docs/prediction-refactor-plan/13-validation-gates.md` (Phase 4 gates V1/V2/V3/V6/V13)
- `Docs/prediction-refactor-plan/07-side-effect-migration.md` (authoritative mapping table)
- `Docs/prediction-refactor-plan/09-integration-adapters.md` (adapter contracts)
- `Docs/prediction-refactor-plan/04-tick-pipeline.md` (post-refactor per-tick step order)
- `agent-exchange/handoff/2026-04-19-phase3d-closeout.md` (pre-Phase-4 carry-over state)
- `agent-exchange/handoff/2026-04-19-phase3d-audit.md` (precedent audit structure + DP-8 extract-resolver pattern)
- `agent-exchange/handoff/phase-8-cleanup-queue.md` (Entries 1 + 2 deferred)

---

## §0 — Executive Summary (for time-pressed reviewer)

Phase 3 (3a/3b/3c/3d) landed shadows with **parity-by-call-site-invariant** for every step. Phase 4 is the first **authority-path cut-over**. Key recommendations (DP detail below):

- **DP-0 (L7)**: **STILL OPEN. Roll into Phase 4 as Prereq-1 (Option b)** — the L7 fix-shape IS the adapter's `Initialize()` late-bind, so the fix and the adapter code arrive in the same commit.
- **DP-1 (Scope split)**: **Split — Phase 4a locomotion, Phase 4b impulse-adapter**. Locomotion cut-over is pure math relocation. Impulse cut-over adds CombatAdapter + Target_EnqueueImpulse migration + CombatRouting deletion + 3 skill-site migrations + L9 behavior-preservation decision. Different blast radii, different gates.
- **DP-2 (Shadow role-flip)**: **Retain inverted shadow through Phase 6 under a NEW define `BUDDAH_PREDICTION_LEGACY_SHADOW`** (independent toggle from `BUDDAH_PREDICTION_SHADOW`). Removal in Phase 7 or Phase 8 cleanup.
- **DP-3 (Ordering)**: **Locomotion first**. Impulse ships against a stabilized locomotion baseline.
- **DP-4 (PASS gates)**: heartbeat semantics re-interpret post-flip (`loc-div` = new authority vs legacy-inverted-shadow). Phase 4 gates V1, V2, V3, V6, V13 per 13-validation-gates.md. **V6 requires real cross-peer RTT** — flag: our V5 harness is single-LAN.
- **DP-5 (Affected files)**: inside `Assets/Scripts/New_Buddah/` (primary) + 3 files under `Assets/Scripts/Buddah/ComboSkill/` (skill callers of `CombatRouting.TryRouteImpulse`). **Flag: Phase 4 REMOVES motor's `QueueImpulseEventTargetRpc` and `TryApplyServerAuthoritativeImpulse` + deletes `BuddahPredictionCombatRouting` static class — these are public-surface RPC/symbol removals, hard-stop territory per CLAUDE.md unless reviewer explicitly approves.**
- **DP-6 (Cut-over risks)**: L9 clamp-strip behavior (preserve vs fix), L10 dual-branch cleanup hazards, reconcile cursor-field alignment between motor's `_impulseEventQueue` snapshot and CommandBus's `ImpulseChannel` (Phase 8 Entry 1 parallel for impulse).
- **DP-7 (Validation sequence)**: harness `prediction` system base sequence + V1 compile + V2 field-audit + V3 gameplay-shake + V6 combat-determinism + V13 perf-budget. V6 is **new to Phase 4** and needs network-latency tooling not yet set up.

**Blocking question for reviewer before V1 begins**: green-light the scope split (4a + 4b), the L7 roll-in, and the motor-RPC-removal surface (the three hard-stop items under DP-5). Without approval on those three, Phase 4 V1 stalls.

---

## §1 — Current Authority Code Inventory (evidence for DP-5)

### §1.1 Locomotion (authoritative inline in motor)

| Element | Location | Shape |
|---------|----------|-------|
| Forward force write | `BuddahPredictedMotor.cs:441` | `Vector3 forwardForce = forwardDirection * (_computedStats.FinalForwardForce * resolvedThrottle);` |
| Forward `AddForce` apply | `BuddahPredictedMotor.cs:~447` (inside same block ~441-464) | via `_predictionRigidbody.AddForce(...)` |
| Turn-torque branch | `BuddahPredictedMotor.cs:451-457` | `if (Mathf.Abs(resolvedSteering) > 0.001f) { ... AddTorque ... }` |
| Turn-torque decay branch | `BuddahPredictedMotor.cs:459-463` | else-path zeroing angular-y |
| `ClampPlanarSpeed` helper | `BuddahPredictedMotor.cs:1209-1223` | reads `rb.velocity`, `PredictionRigidbody.Velocity(clamped)` — strips pending non-angular forces (see L9) |
| Pre-clamp snapshot for shadow | `BuddahPredictedMotor.cs:416` (inside `#if … BUDDAH_PREDICTION_SHADOW`) | `_shadowPreClampVelocity = rb != null ? rb.velocity : Vector3.zero;` |
| Real-side locomotion capture | `BuddahPredictedMotor.cs:444-445` | `_realScratch.CommandedForwardForce = ...; _realScratch.LocomotionRan = true;` |

### §1.2 Impulse (authoritative inline in motor)

| Element | Location | Shape |
|---------|----------|-------|
| Queue field | `BuddahPredictedMotor.cs:37` | `private readonly BuddahPredictedImpulseEventQueue _impulseEventQueue = new();` — **motor-private storage**, distinct from `CommandBus.ImpulseChannel` |
| Server-side enqueue API | `BuddahPredictedMotor.cs:729-753` | `TryApplyServerAuthoritativeImpulse(...)` → `TryQueueImpulseEvent(eventData)` (local) + `QueueImpulseEventTargetRpc(Owner, …)` (to owner) |
| Consume method | `BuddahPredictedMotor.cs:1735-1777` | `ConsumePendingImpulseEvents(uint currentTick)` — `_impulseEventQueue.ConsumeReady(currentTick, lambda)` where lambda does `_predictionRigidbody.AddForce(impulse, Impulse) + AddTorque(Vector3.up * turn, Impulse)` + `ApplyPushGraceFromImpulse` |
| Call site in RunInputs | `BuddahPredictedMotor.cs:358` | `ConsumePendingImpulseEvents(currentTick);` — BEFORE `ClampPlanarSpeed` at line 418 |
| Pre-consume snapshot for shadow | `BuddahPredictedMotor.cs:342` | `_impulseEventQueue.CopyPendingSnapshot(_shadowPreImpulsePendingSnapshot);` |
| Real-side impulse capture | `BuddahPredictedMotor.cs:1752-1756` | `_realScratch.ImpulseRan = true; _realScratch.ShadowLastConsumedImpulseId = max;` |
| Clear site (reset path) | `BuddahPredictedMotor.cs:1826` | `_impulseEventQueue.Clear();` — via teleport `ResetImpulseQueue` flag |

### §1.3 Shadow-step + command-bus state (already-live infrastructure)

- `BuddahLocomotionStep.Run` (see `Simulation/BuddahLocomotionStep.cs:24-56`): pure static, writes `scratch.CommandedForwardForce` + `scratch.CommandedTurnTorque` + `scratch.ClampingApplied` + `scratch.VelocityAfterClamp`. Does NOT touch `rb`.
- `BuddahImpulseStep.Run` (see `Simulation/BuddahImpulseStep.cs:23-53`): pure static, walks `ctx.ImpulsePendingSnapshot` in REVERSE (matches motor's `ConsumeReady` order, per the step's own inline doc), writes `scratch.ImpulseRan` + `scratch.ShadowLastConsumedImpulseId`. **No velocity/torque integration** — cursor-only shadow.
- `BuddahPredictionCommandBus` (see `Events/BuddahPredictionCommandBus.cs`):
  - 4 channels live: `_impulse` / `_teleport` / `_modifier` / `_handoff` (capacity 64 each).
  - `TryEnqueueImpulse(in ImpulseCmd)` at line 36-42: enqueue with drop-full warning.
  - `Target_EnqueueImpulse(NetworkConnection, ImpulseCmd)` at line 91-103: `[TargetRpc]`, does `TryEnqueueImpulse` on the target.
  - `TryClearChannels(BuddahPredictionChannelMask)` at line 68-85: emits `[CommandBus]:ClearAll` log line (Phase 2 V8 observed 4× per spawn, see L7).
- `BuddahPredictionCombatAdapter` (see `Integration/BuddahPredictionCombatAdapter.cs`): **Phase 0 empty stub**. No methods. This is the Phase 4 implementation target.

### §1.4 `ImpulseQueueState` reconcile field

- `BuddahPredictedReconcileData.cs:31` — `public BuddahPredictedImpulseRingSnapshot ImpulseQueueState;`
- Carries motor-private `_impulseEventQueue` snapshot + cursor.
- Phase 4 must decide whether `ImpulseQueueState` (a) stays as motor-snapshot source + gets populated from CommandBus state, (b) gets replaced by a CommandBus-ImpulseChannel snapshot field, or (c) is retired entirely in favor of bus-side reconcile scaffolding. See DP-6.

### §1.5 Caller sites for `BuddahPredictionCombatRouting.TryRouteImpulse`

1. `Assets/Scripts/Buddah/PushHitbox.cs:200` — melee push, `source=MeleePush`.
2. `Assets/Scripts/Buddah/ComboSkill/Skill_PushProjectileHands/HandPushProjectileRuntime.cs:212` — projectile hit, `source=Projectile`.
3. `Assets/Scripts/Buddah/ComboSkill/Skill_PushProjectileHands/ChargedHandProjectileRuntime.cs:362` — charged projectile hit, `source=ChargedProjectile`.
4. `Assets/Scripts/New_Buddah/Debug/BuddahPredictionImpulseDebugBox.cs:45` — debug-only injector.

**Network/** folder reference count: **0** (grep confirmed by Explore subagent). Phase 4 does not cross `prediction` → `room-session` / `race-results` / `selection-loadout` boundaries.

### §1.6 L7 double-ApplyMode verified live

- `BuddahMovementModeSwitcher.Awake()` line 20-21: `ResolveReferences(); ApplyRuntimeMode(force: true);`
- `BuddahMovementModeSwitcher.OnEnable()` line 25-27: `ResolveReferences(); ApplyRuntimeMode(force: true);`
- `BuddahPredictionLegacyIsolationBridge.ApplyMode()` line 28: calls `commandBus.TryClearChannels(BuddahPredictionChannelMask.All)` on every transition.
- Net: every Buddah spawn in PredictionV2 mode produces **2× ClearAll pairs = 4× `[CommandBus]:ClearAll` log lines**. Matches the Phase 2 V8 observation logged in L7.

---

## §2 — DP-0 (blocking): L7 prereq status

### What L7 requires (quote from `Docs/lessons-log.md` L7)

> When a component's role includes clearing or resetting state, audit every lifecycle hook (`Awake`, `OnEnable`, `OnNetworkStarted`, `Start`) for calls into that clear. Document whether double-fire is intended. **For Phase 4**: any adapter that enqueues into the CommandBus during init (`Awake`/`OnEnable`/`OnStartNetwork`) must either (a) enqueue AFTER Switcher's double-ApplyMode completes (e.g., defer to first `[Replicate]` tick), or (b) add an "init-complete" gate in the Switcher so ApplyMode short-circuits on repeat calls within one frame. Do not silently accept the double-fire, because the second ClearAll will nuke legitimate pending events.

### L7 is still OPEN

Evidence:
- `BuddahMovementModeSwitcher.cs` lines 20-27 still call `ApplyRuntimeMode(force:true)` from **both** `Awake()` and `OnEnable()`.
- No init-complete gate in `BuddahPredictionLegacyIsolationBridge.ApplyMode()`.
- No `BUDDAH_PREDICTION_ADAPTER_INIT_GATE` define in ProjectSettings.
- Phase-4-prerequisites.md (merged 2026-04-19, Phase 3c) documents Prereq-1 recommendation (Option B).
- Phase 3d closeout §4 explicitly restates: "Phase 4 (first concrete adapter cut-over): Blocked on L7".

### Three-option framing from the task brief

- **(a) Dedicated pre-Phase-4 L7-only pass**: ship L7 fix as its own 1-commit PR, *then* Phase 4.
  - **Pro**: Phase 4 scope stays mechanical; L7 regression (if any) is attributable to one commit.
  - **Con**: Option B (adapter-side defer-until-first-replicate) has **no adapter to defer** until Phase 4 exists. The fix shape needs an adapter to embed in. A standalone L7 PR would either (i) implement Option A (Switcher-local debounce), abandoning the recommended Option B, or (ii) add the Initialize-gate pattern speculatively to empty stubs — wasted scaffolding if Phase 4 changes adapter shape.
- **(b) Roll into Phase 4 scope as Prereq-1**: L7 fix = "CombatAdapter ships with explicit Initialize() late-bind hook that enqueues on first `[Replicate]` tick instead of Awake/OnEnable". The Phase 4 PR body carries both the fix and the adapter.
  - **Pro**: Option B is the recommended fix shape per phase-4-prerequisites.md — this is *how* the fix expresses itself. Single commit, fix-and-adapter together, acceptance criterion ("2× ClearAll per spawn, not 4×") verifies both at once.
  - **Con**: Phase 4 PR grows by ~50-100 lines of Initialize-scaffolding code + the V8-style re-run digest.
- **(c) Defer with risk assessment**: accept 4× ClearAll for Phase 4 and patch later.
  - **Pro**: smallest Phase 4 scope.
  - **Con**: **violates the L7 rule literally** (`Do not silently accept the double-fire, because the second ClearAll will nuke legitimate pending events`). CombatAdapter's first enqueue on a fresh Buddah is exactly the scenario L7 predicts will silently drop data. Not an option for Phase 4.

### Recommendation: **(b) roll into Phase 4 as Prereq-1**.

Companion task per phase-4-prerequisites.md: once Option B lands for CombatAdapter, extract the `Initialize()`-style late-bind into a shared contract (`IBuddahPredictionAdapter` interface or abstract base `BuddahPredictionAdapterBase`) so Phase 5's SkillAdapter and Phase 6's RespawnAdapter/IntroAdapter inherit the pattern by construction.

**This decision gates everything else in Phase 4** — confirm before DP-1.

---

## §3 — DP-1: Scope split (atomic cut-over vs 4a + 4b)

### Shared state check

| State | Locomotion | Impulse | Shared? |
|-------|------------|---------|---------|
| `PredictionRigidbody.velocity` path | `AddForce(forwardForce) + AddTorque(turn)` | `AddForce(impulse, Impulse)` | **YES** — both route through `_predictionRigidbody` |
| Pre-clamp velocity snapshot | reads it | — | locomotion-only |
| `ClampPlanarSpeed` | calls it | — | locomotion-only (per motor.cs:418), but **strips** pending impulse `AddForce` per L9 |
| `_impulseEventQueue` | — | consumes it | impulse-only |
| `_pendingForces` (PredictionRigidbody-internal) | writes | writes | **YES** — both use AddForce, ClampPlanarSpeed drops both |
| Reconcile fields | `RigidbodyState`, `ComputedStats` | `ImpulseQueueState` | disjoint |

**Key coupling**: L9 — impulse + locomotion collide at ClampPlanarSpeed. Impulse queued at motor.cs:358 → `_pendingForces` via `PredictionRigidbody.AddForce`. ClampPlanarSpeed at motor.cs:418 → `PredictionRigidbody.Velocity(clamped)` → calls `RemoveForces(nonAngular:true)` → **strips the impulse's linear component**. Torque component survives.

This is pre-existing behavior. Phase 3b shadow explicitly defers the fix to Phase 4 (per L9 Rule (b)). Phase 4 must decide: preserve-as-is (shadow-proof) or resolve (re-order consume/clamp, or scale-before-clamp).

### Independent verifiability

- **Locomotion-only V3 (gameplay shake) test**: drive Buddah on open track, no skills. Pass if no shake vs pre-refactor baseline. Fully isolates locomotion.
- **Impulse-only V6 (combat determinism) test**: cross-peer push hit. Requires locomotion to be functional (for the victim to move/react), but the delta under test is impulse-specific. Can be run against both pre-Phase-4a (inline locomotion) and post-4a (new-step locomotion) states.

Both gates can be exercised independently.

### In-between-state risk

If 4a lands alone, impulse-shadow continues running under `BUDDAH_PREDICTION_SHADOW` — unchanged. No regression. If 4b lands second, locomotion is already authoritative via `BuddahLocomotionStep`. The shadow/authority ordering doesn't invert.

### Recommendation: **SPLIT into Phase 4a (locomotion) + Phase 4b (impulse-adapter + CombatRouting deletion)**.

Rationale:
1. **Blast radius differential**: 4a touches ~3 motor blocks + 1 shadow-step file. 4b touches CombatAdapter stub (new file), CombatRouting deletion (1 file), motor's Impulse RPC surface (removal of `TryApplyServerAuthoritativeImpulse` + `QueueImpulseEventTargetRpc`), 3 skill-caller migrations, L7 Initialize() fix, CommandBus channel-drain wiring. 10× larger.
2. **Gate mismatch**: V3 validates 4a. V6 validates 4b. V13 applies to both but is additive.
3. **Rollback granularity**: if 4b shows V6 divergence, 4a stays merged. A single Phase 4 PR would rollback both on a V6 failure, losing the 4a locomotion win unnecessarily.
4. **Reviewer bandwidth**: 4b's L7-fix + adapter-pattern-extract + RPC-removal is a full session of review; 4a is a mechanical pass.

### Counter-argument (the plan says one phase)

`Docs/prediction-refactor-plan/12-migration-sequence.md` Phase 4 names both as a single phase ("Day 11-12, 2-day slot"). Splitting extends calendar by 1 day. **Reviewer may prefer 12-migration-sequence.md's original shape** — reasonable given that 3a/3b landed impulse + teleport together. If reviewer rejects split, proceed single-phase with **locomotion-first ordering** (DP-3).

---

## §4 — DP-2: Cut-over direction + shadow role-flip

### The question

When real path switches to the new step, what happens to the old inline code?

### Options

#### Option I — Parity-by-construction (delete old inline code immediately)

- Phase 4a: delete motor.cs:441-464 locomotion block, replace with `BuddahLocomotionStep.Run(...)` call that RETURNS forward-force + turn-torque values motor then applies to rb.
- Phase 4b: delete motor.cs:1735-1777 impulse consume, replace with `BuddahImpulseStep.Run(...)` that drains `CommandBus.ImpulseChannel` and applies rb forces.
- Shadow's existing compare infrastructure (`_realScratch.CommandedForwardForce` vs shadow value) becomes **identical by construction** — both sides call the same step.

**Pro**:
- Smaller motor file post-Phase-4.
- Aligns with 3d DP-8 precedent (resolver extract: motor + shadow both call `BuddahPredictedLaunchHandoffResolver.Advance`).
- No dead code paths.

**Con**:
- Loses observational safety net. If the new step's authoritative behavior differs subtly from the old inline code (e.g., different compiler inlining, different dispatch latency under hot-path), shadow can no longer detect it — both sides are the same code.
- Phase 4's V6 + V13 gates rely on external observation (playtest + profiler), which is harder to bisect than a shadow warning.

#### Option II — Role-flip inverted shadow (keep old inline code as the shadow)

- Phase 4a: introduce `BuddahLocomotionStep` call as the authoritative write-to-rb path. Keep the old inline locomotion block inside a new `#if BUDDAH_PREDICTION_LEGACY_SHADOW` gate, writing to `_legacyShadowScratch.CommandedForwardForce`. `Shadow_CompareAndReport` emits `[D-LOC INV]` warnings when new-authority vs legacy-shadow disagree on the 4 locomotion fields.
- Phase 4b: same pattern for impulse — keep `_impulseEventQueue` + `ConsumePendingImpulseEvents` under `#if BUDDAH_PREDICTION_LEGACY_SHADOW`, new authoritative drain reads from `CommandBus.ImpulseChannel`, both write to their own scratch and get compared.
- Remove the inverted shadow (both define + code) in Phase 7 or Phase 8 cleanup (commit tagged to match Phase 8 cleanup-queue).

**Pro**:
- Observational safety net covers the transition AND the next two phases. If L9 clamp-strip resolution or L10 dual-branch removal shifts behavior, the inverted shadow catches it.
- `BUDDAH_PREDICTION_LEGACY_SHADOW` as a separate define lets us disable inverted shadow independently of the forward shadow (e.g., if inverted fires false positives on an intentional behavior change). Forward shadow (3a/3b/3c/3d) stays on `BUDDAH_PREDICTION_SHADOW`.
- Phase 3 discipline ("shadow phases are observation-only, behavior-neutral") carries into Phase 4 transition for 1-2 phases, not an abrupt discipline flip.

**Con**:
- Motor file grows during Phases 4-6: old inline code + new authority code + shadow-step infrastructure co-exist. Temporary bloat.
- Shadow-vs-shadow compare requires explicit "I intentionally changed behavior" annotations for L9 / L10 resolutions (forward-documented in commit message).

#### Option III — Hybrid: delete immediately for locomotion (4a), role-flip for impulse (4b)

- Locomotion cut-over is pure math relocation — no behavior change. Parity-by-construction is tight. Option I for 4a.
- Impulse cut-over changes storage from motor-private `_impulseEventQueue` to `CommandBus.ImpulseChannel`. Routes ServerRpc through bus instead of motor RPC. **Behavior-preserving is non-obvious**; L9 and L10 both surface here. Option II for 4b.

### Recommendation: **Option III (hybrid)** — delete-immediately for locomotion, inverted-shadow for impulse.

Rationale:
1. Locomotion 4a math is identical motor-vs-step (Explore report §1 confirmed `BuddahLocomotionStep` already mirrors motor line-for-line). Parity-by-construction is trivially true.
2. Impulse 4b changes storage layer + RPC layer + 3 skill callers + adapter scaffolding — too many moving parts for pure parity-by-construction to cover safely.
3. The new `BUDDAH_PREDICTION_LEGACY_SHADOW` define stays active from 4b through Phase 6 (teleport+handoff cutover likely benefits from the same pattern); kill in Phase 7 or 8.

If reviewer prefers uniform Option I across 4a+4b (simpler), acceptable — but the commit message must explicitly carve out "parity-by-construction only; no inverted-shadow fallback" and Phase 4b's V6 gate becomes load-bearing as the sole divergence detector.

---

## §5 — DP-3: Cut-over ordering within a pass

### If single-phase (reviewer rejects DP-1 split)

**Locomotion first within the same PR.** Order the commits in this sequence:
1. Commit 1: locomotion cut-over (motor → `BuddahLocomotionStep`).
2. Commit 2: L7 adapter Initialize() + CombatAdapter population.
3. Commit 3: motor impulse drain migration (`_impulseEventQueue` → `CommandBus.ImpulseChannel`).
4. Commit 4: CombatRouting deletion + 3 skill-caller migration.

Rationale: if Commit 3 or 4 fails validation mid-merge, revert stops at Commit 2 and we ship locomotion alone (de-facto 4a). Commit order produces natural split granularity even inside a single phase.

### If split (recommended)

Phase 4a (locomotion) ships and merges first. Phase 4b branches off the post-4a tip. Independent PRs, independent V3 and V6 passes.

### Rollback scope

- Post-Commit-1 divergence → revert Commit 1 → back to 3d state.
- Post-Commit-3 divergence → revert Commit 3+4 → keep 1+2 → back to "locomotion new, impulse still motor-inline". Valid intermediate state.
- **Not valid intermediate**: revert Commit 1, keep Commit 3+4. New impulse drain depends on bus being live; but bus is already live since Phase 2. Safe.

---

## §6 — DP-4: PASS gate definition for Phase 4

### Post-flip heartbeat semantics

| Counter | Pre-Phase-4 meaning | Post-Phase-4a (locomotion cut-over) | Post-Phase-4b (impulse cut-over) |
|---------|---------------------|-------------------------------------|----------------------------------|
| `loc-div` | forward shadow vs motor inline | 0 by construction (Option I) OR new-authority vs legacy-inverted (Option II) | unchanged from 4a state |
| `loc-compared` | ticks shadow ran (per-tick) | **unchanged** (still per-tick coverage via parity gate) | unchanged |
| `imp-div` | forward shadow vs motor inline | unchanged (motor still consumes from `_impulseEventQueue`) | new-authority (bus drain) vs legacy-inverted shadow (motor queue) under Option II; 0 by construction under Option I |
| `imp-compared` | cumulative event consumes observed | unchanged | unchanged meaning, but counter source moves from `_impulseEventQueue.ConsumeReady` callback to bus-drain path |

### Are compared-counts still load-bearing (per L13)?

**Yes.** L13 rule (a): "Every shadow-path PASS judgement must assert BOTH divergence=0 AND compared-count > 0."

Post-flip, under **Option I (parity-by-construction)**:
- `loc-compared > 0` still proves the step fired. Required.
- `imp-compared > 0` still proves impulses consumed. Required (per L11/L10, "0 counter ≠ success" — with CombatRouting gone, 0 means the adapter's routing did not reach the bus).

Post-flip, under **Option II (inverted shadow)**:
- `loc-div = 0` AND `loc-compared > 0` AND **new: `loc-div-inv = 0`** (inverted-shadow also agrees). Triple gate.
- Analogous triple gate for impulse under 4b.

### New gameplay-level assertion for Phase 4

Shadow mode cannot test:
- **V3 (gameplay shake)**: visible jitter in owner + spectator. Frame-delta observation, 240 fps capture.
- **V6 (combat determinism)**: server-issued impulse hits non-owner Buddah at 100ms RTT; owner's predicted position converges to server reconcile within one tick after impulse landing.
- **V13 (performance budget)**: motor `[Replicate]` time at 60Hz, 8 active Buddahs; average tick cost no worse than +10% vs pre-refactor baseline.

None of these are shadow-observable. They require:
- V3: 4-player race, 60-second loop, 60-100ms RTT. Network-conditioned.
- V6: cross-peer push with artificial 100ms RTT. Requires `Clumsy` / `tc netem` / Unity Network Simulator asset. **Not currently set up in the test harness.** Flag.
- V13: Unity Profiler capture pre/post, comparison.

### Proposed PASS-gate table

#### V2 (single-peer HOST) — Phase 4a locomotion

| # | Criterion | Required |
|---|-----------|----------|
| 1 | V1 compile clean | yes |
| 2 | `loc-div = 0` on every heartbeat (inverted-shadow if Option II) | yes |
| 3 | `loc-compared > 0` at tail | yes |
| 4 | `imp-div / tel-div / mod-div / hof-div = 0` (3a/3b/3c/3d preserved) | yes |
| 5 | 0 non-heartbeat `[D-LOC]`, 0 FATAL | yes |
| 6 | V3 pass: no visible shake on HOST screen recording (240 fps, 30s drive) | yes |
| 7 | V13 pass: `[Replicate]` tick cost ≤ 110% of pre-4 baseline | yes |

#### V5 (2-peer HOST+CLIENT) — Phase 4a locomotion

- All V2 criteria per-peer.
- `loc-compared > 0` on CLIENT (reconcile-replay exercise).
- CLIENT `mod-compared / active-ticks` ratio still >> 1 (reconcile-replay signature preserved — regression indicator per Phase 3d V5 §7 anomaly 1).
- V3 pass on non-owner view (remote Buddah smoothness).

#### V2 (single-peer HOST) — Phase 4b impulse

| # | Criterion | Required |
|---|-----------|----------|
| 1 | V1 compile clean | yes |
| 2 | `imp-div = 0` on every heartbeat | yes |
| 3 | `imp-compared > 0` at tail | yes |
| 4 | Prior gates (loc, tel, mod, hof divs = 0) preserved | yes |
| 5 | 0 non-heartbeat `[D-LOC]`, 0 FATAL | yes |
| 6 | `[CommandBus]:ClearAll` per Buddah spawn = **2** (not 4) — L7 fix verification | yes |
| 7 | V6 pass (local: zero-RTT host self-push convergence, stand-in for cross-peer) | yes |

#### V5 (2-peer HOST+CLIENT) — Phase 4b impulse

- All V2 criteria per-peer.
- CLIENT `imp-compared > 0` (receives cross-peer push via `Target_EnqueueImpulse`). **This is the first gate where L11 cross-peer-push coverage is strictly required** — self-push on default skill won't exercise the bus-routed path.
- V6 pass under 100ms RTT — **REQUIRES network-latency tooling setup**. Flag §8.

---

## §7 — DP-5: Affected files inventory

### Category A — `Assets/Scripts/New_Buddah/` (primary scope)

**Phase 4a (locomotion):**
- `Core/BuddahPredictedMotor.cs` — protected file. Changes:
  - Replace inline locomotion block ~441-464 with `BuddahLocomotionStep.Run` call path (Option I) OR retain under `#if BUDDAH_PREDICTION_LEGACY_SHADOW` (Option II, not recommended for 4a).
  - Move `_realScratch.CommandedForwardForce` assignment site inside the step (self-capture) OR post-step copy.
  - If Option II, add `_legacyShadowScratch.CommandedForwardForce` infrastructure.
- `Simulation/BuddahLocomotionStep.cs` — extend signature to return apply-to-rb values (or call `ref PredictionRigidbody`, breaking purity). Flag: Phase 4 changes the step contract from "shadow-only, pure static, no Unity API" to "authority, applies forces". Decide.
- `Core/BuddahPredictionTickContext.cs` — no field changes expected unless step signature changes reveal a gap.
- `Simulation/BuddahPredictionShadowScratch.cs` — possibly add `_legacyShadowScratch` mirror state if Option II.

**Phase 4b (impulse + adapter):**
- `Core/BuddahPredictedMotor.cs` — protected file. Changes:
  - **DELETE** `TryApplyServerAuthoritativeImpulse` (motor.cs:729-753).
  - **DELETE** `TryQueueImpulseEvent` (helper, if only called by deleted method).
  - **DELETE** `QueueImpulseEventTargetRpc` (motor-side RPC).
  - **DELETE** `_nextImpulseEventId` field (moves to CommandBus or ImpulseCmd payload).
  - **MAYBE DELETE** `_impulseEventQueue` field — depends on whether motor retains local cached queue or reads directly from `CommandBus.ImpulseChannel` each tick.
  - **REPLACE** `ConsumePendingImpulseEvents` body (motor.cs:1735-1777) with a drain from `CommandBus.ImpulseChannel` (via `BuddahImpulseStep.Run` promoted to authority).
  - Update `BuddahPredictedReconcileData.ImpulseQueueState` write path (motor.cs near `CreateReconcile`) — now writes CommandBus-derived state.
  - `_impulseEventQueue.Clear()` call at motor.cs:1826 (from teleport `ResetImpulseQueue`) — migrate to `commandBus.TryClearChannels(BuddahPredictionChannelMask.Impulse)`.
- `Simulation/BuddahImpulseStep.cs` — promote from cursor-only shadow to authority. Add force/torque application (breaks current pure-static contract).
- `Integration/BuddahPredictionCombatAdapter.cs` — **populate** from Phase 0 empty stub. Implement `TryRouteImpulse(NetworkObject victim, Vector3 linear, float turn, BuddahPredictedImpulseSourceType sourceType, NetworkObject sourceObject)`:
  - Server-side: resolve victim's `BuddahPredictionCommandBus`, emit `Target_EnqueueImpulse(victim.Owner, cmd)`, mirror into server-side `_impulse` channel (same cmd, since server replicate consumes it).
  - Add `Initialize()` late-bind hook for L7 fix (Option B per phase-4-prerequisites.md).
  - If prediction mode off → fall back to legacy `PushTargetBox` path (per §09-integration-adapters.md).
- `Integration/BuddahPredictionCombatRouting.cs` — **DELETE entire file** after callers migrate. Include `.meta` removal.
- `Events/BuddahPredictionCommandBus.cs` — possibly add `DrainReadyImpulses(uint currentTick, out List<ImpulseCmd>)` or similar drain API if the step doesn't read channel directly. Minor.
- `Bootstrap/BuddahMovementModeSwitcher.cs` — either unchanged (if adapter-side Initialize() handles L7) or changed to add init-complete gate (Option A fallback). **Recommended: unchanged — Option B puts the fix on the adapter side.**
- `Integration/BuddahPredictionLegacyIsolationBridge.cs` — unchanged (ClearAll still legitimate; adapter Initialize() just runs after it settles).

### Category B — `Assets/Scripts/Buddah/ComboSkill/` (skill stack)

Phase 4b, caller migration:
- `Assets/Scripts/Buddah/PushHitbox.cs:200` — replace `BuddahPredictionCombatRouting.TryRouteImpulse(...)` with `combatAdapter.TryRouteImpulse(...)` (resolve adapter via bootstrap or GetComponent).
- `Assets/Scripts/Buddah/ComboSkill/Skill_PushProjectileHands/HandPushProjectileRuntime.cs:212` — same pattern.
- `Assets/Scripts/Buddah/ComboSkill/Skill_PushProjectileHands/ChargedHandProjectileRuntime.cs:362` — same pattern.

**Flag**: per CLAUDE.md global rules, skill stack is a separate manifest system. Phase 4b crosses `prediction` ↔ `skill-execution` manifest boundary in a small, well-scoped way (caller rewrite only, no SkillExecutor changes). Acceptable because Phase 5 already plans to unify skill-adapter routing. Explicitly call out the cross in the Phase 4b PR.

### Category C — `Assets/Scripts/Network/` (NOT touched)

Grep confirms 0 references to `BuddahPredictedMotor`, `BuddahPredictionCombatRouting`, `BuddahPredictionCombatAdapter` from `Network/`. No cross-system touch.

### Category D — Hard-stop items (CLAUDE.md Stop and Ask)

Three Phase 4b items hit the **hard-stop** list per CLAUDE.md:

1. **RPC signature REMOVAL**: deleting `QueueImpulseEventTargetRpc` (motor public surface) — "The task would rename serialized fields, public RPC entry points, or scene names used by orchestration systems." Removal is structurally a rename-to-nothing. Requires explicit approval.
2. **Public method REMOVAL**: deleting `TryApplyServerAuthoritativeImpulse` — called by `BuddahPredictionCombatRouting`, which is itself deleted, but any other caller (external tools, editor scripts, tests) gets a compile break. Grep confirmed only CombatRouting as callsite, but the method IS public API surface.
3. **Class DELETION**: `BuddahPredictionCombatRouting` entire static class removed — "protected-by-usage" rather than protected-by-manifest, but changes the Integration folder's public shape.

None of these are protected-file edits per harness-manifest.json. But per CLAUDE.md "do not rename … public RPC entry points … unless explicitly asked", removal rises to explicit-approval.

### Serialized fields / scene references

**No serialized field renames expected** in Phase 4. `BuddahPredictedReconcileData.ImpulseQueueState` stays as a field name; only its write-source migrates from motor-private queue to bus-derived snapshot. No scene/prefab rebinding required unless `BuddahPredictionCombatAdapter` gets a serialized component reference on the Buddah prefab — flag if Phase 4b's Initialize() hook needs it.

**Scene assets**: `Assets/Scenes/RaceMap.unity` has uncommitted changes in the current worktree (git status reports `M Assets/Scenes/RaceMap.unity`). These are almost certainly Phase 3d shadow-probe placements, not Phase 4 scope. Recommend: commit/revert before Phase 4 V1 to keep PRs clean.

---

## §8 — DP-6: Known risks specific to cut-over

### R1 — Compiler inlining / branch prediction / register allocation differences

Motor's inline `forwardForce = forwardDirection * (stats.FFF * throttle)` → step static call `BuddahLocomotionStep.Run(...)` → separate function call frame. JIT may inline or not. Hot-path impact per tick is microseconds, but multiplied by 60Hz × 8 Buddahs = possibly measurable on V13.

**Mitigation**: V13 profile pre-Phase-4-baseline → compare post-Phase-4a. If regression >10%, try `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on the step. Phase 8 cleanup-queue candidate if measurement reveals a budget concern.

### R2 — Branch prediction regression on turn-torque decay path

Motor.cs:451-463: `if (|steering| > 0.001f) ... else decay`. Step currently writes `0f` in decay branch instead of applying decay torque. Need to verify the step's decay mirror matches motor's decay write semantics (which may do more than zero — e.g., angular-y specific decay).

**Mitigation**: Step 1 of Phase 4a V1 = read motor.cs:459-463 carefully, confirm step's decay branch matches. Currently the step writes `scratch.CommandedTurnTorque = 0f` in decay branch (per BuddahLocomotionStep.cs:54), while motor's decay branch may do active angular-y damping. **Non-trivial shadow gap.** Needs closer read before Phase 4a V1.

### R3 — L9 clamp-strip behavior preservation vs resolution

Per lessons-log L9: ClampPlanarSpeed strips pending impulse `AddForce` on clamp-triggered ticks. Phase 3b preserved; Phase 4 "must explicitly evaluate whether to preserve or change this behavior."

**Options**:
- **R3.a Preserve**: Phase 4b new impulse drain still routes `AddForce` → same strip on same-tick clamp. Bit-preserving. No V3/V6 regression from this axis.
- **R3.b Resolve (re-order)**: drain impulses **after** clamp (swap order motor.cs:358 ↔ motor.cs:418). Impulse linear now survives clamp. **Behavior change**. Inverted-shadow (Option II DP-2) will flag the change explicitly — a feature, not a bug.
- **R3.c Resolve (scale)**: detect pending impulse magnitude pre-clamp, scale impulse before clamp runs. Complex; requires PredictionRigidbody API that doesn't exist today.

**Recommendation**: R3.a (preserve) for Phase 4b V1. Ship the cut-over first, defer resolution to a Phase 8 cleanup-queue entry. Aligns with "one thing at a time" and lets V6 measure cut-over effect in isolation.

### R4 — L10 dual-branch removal hazard

Per lessons-log L10: current code has two push paths — predicted-queue branch (`bootstrap.IsPredictionModeActive()==true`) and legacy-direct-rb branch (false). Phase 4 deletes CombatRouting, which hosts the gate. Two options:

- **R4.a**: adapter retains the `IsPredictionModeActive()` check, routes to CommandBus (true) or `PushTargetBox` fallback (false). Behavior preserved across modes.
- **R4.b**: adapter assumes prediction always on, drops the fallback branch. Phase 4-v1-shipped-to-production state means "all callers in predicted mode always." Risk: any test mode or legacy-isolation mode that toggles `IsPredictionModeActive()=false` breaks silently.

**Recommendation**: R4.a for Phase 4b V1. Document an explicit Phase 7/8 cleanup-queue entry for the legacy fallback removal (aligns with migration-sequence.md Phase 8 "Delete … `BuddahPredictionPushTargetBox` legacy fallback (if scope allows)").

### R5 — Reconcile cursor alignment motor-queue ↔ bus-channel

Today: `BuddahPredictedReconcileData.ImpulseQueueState` = snapshot of motor's `_impulseEventQueue`. Post-4b: bus owns the queue. Reconcile must now snapshot `CommandBus.ImpulseChannel` state.

**Risk**: if the serializer for `BuddahPredictedImpulseRingSnapshot` assumes motor-queue semantics (e.g., pending events + last-consumed-id), and the bus uses a different cursor convention, reconciled state on a remote peer reads **wrong** last-consumed-id. Delayed desync — the same class of bug that phase-8-cleanup-queue.md Entry 1 (`ModifierState` vs `ComputedStats` precision) warns against.

**Mitigation**: Phase 4b V1 must explicitly confirm the bus-to-reconcile snapshot pipeline produces a byte-identical `ImpulseQueueState` shape for the same `ImpulseCmd` / event history as motor's old queue. Add as a new Phase 8 cleanup-queue entry if audit-level verification is deferred.

### R6 — Adapter init race

Per L7 Rule: adapter Initialize() must fire AFTER Switcher double-ApplyMode settles. First-replicate late-bind is the recommended fix. But `BuddahPredictedMotor.RunInputs` is called once per tick, and if the adapter enqueues on tick T but the bus's Target_EnqueueImpulse for a pre-tick-T server-side event landed at T-1 (which got ClearAll'd by Switcher OnEnable), the pre-T event is lost.

**Mitigation**: Initialize() late-bind on the adapter means the adapter *itself* defers its first enqueue to first-replicate. Any external caller (skill-caller) that fires mid-spawn must also wait until after the adapter reports initialized. Phase 4b must decide: (a) adapter exposes `IsInitialized` property, callers poll, (b) callers enqueue unconditionally and adapter buffers until init-complete, then flushes. **(b) is safer**; (a) re-introduces per-caller discipline that L7 Option C explicitly rejected.

### R7 — DEVELOPMENT_BUILD vs release compile-out

`#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW` gates all current shadow code. If Phase 4 introduces a new `#if BUDDAH_PREDICTION_LEGACY_SHADOW` gate, confirm the define is set in ProjectSettings with the same gate shape — else the inverted shadow either never runs (wrong target define) or runs in release builds (binary bloat regression).

**Mitigation**: define the new symbol in `ProjectSettings/ProjectSettings.asset` scripting-defines `Standalone` target line (same as `BUDDAH_PREDICTION_SHADOW`). Confirm gate pattern `#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_LEGACY_SHADOW`.

---

## §9 — DP-7: Validation sequence for Phase 4

### Harness-helper output (for prediction system, refactor intent)

Base sequence (per `Get-BuddahGoHarnessContext.ps1 -Intent refactor -Systems prediction`):
- `compile-console` — no errors, no new warnings
- `scope-boundary` — changed files match scope, no protected-file touch
- `prediction-integrity` — reconcile data shape unchanged, forces route through prediction, no Rigidbody shortcut
- `network-authority` — server writes still on authority paths, RPC lifecycle valid
- `behavior` — expected behavior stated, confirmed via logs/MCP/test, spot-check adjacent flow

### Phase-specific gates (per `Docs/prediction-refactor-plan/13-validation-gates.md`)

Phase 4 required gates: **V1, V2, V3, V6, V13**.

| Gate | Scope | Phase 4a | Phase 4b |
|------|-------|----------|----------|
| V1 | Compile + no new warnings | required | required |
| V2 | Reconcile field audit (every `[Replicate]` read restored in `[Reconcile]`) | required | required (new work: ImpulseQueueState write-source) |
| V3 | Gameplay shake owner + spectator | **load-bearing** (locomotion is the shake candidate) | required (confirm no new impulse-induced shake) |
| V6 | Combat determinism at 100ms RTT | n/a (impulse not flipped yet) | **load-bearing** (new gate) |
| V13 | Performance budget `[Replicate]` ≤ +10% | required | required |

### Run plan

**Phase 4a V1**: V1 compile → V2 host-only playtest ~60s, no skills, straight drive → V5 2-peer 60s, confirm loc-compared > 0 on both peers, all divs = 0 → V3 owner + spectator 240fps record 30s drive each → V13 profiler capture, compare against Phase 3d baseline. PASS = all 5 gates green.

**Phase 4b V1**: V1 compile → V2 host-only ~60s with **2× cross-peer push via anti_push_projectile OR external helper** (per L11: self-push with default `push_projectile_hands` fizzles, cross-peer push required to exercise `TryRouteImpulse` → adapter → `Target_EnqueueImpulse`) → V5 2-peer 60s with host-to-client push AND client-to-host push → V3 ensure no new shake → V6: 2-peer under 100ms synthetic RTT, cross-peer push, verify owner's predicted position converges to server reconcile within 1 tick post-impulse-landing → V13 profiler. **V8 side-probe: confirm `[CommandBus]:ClearAll` lines per Buddah spawn = 2 (not 4)**, per phase-4-prerequisites.md acceptance criterion.

### Blockers to flag (see §10)

- V6 requires network-latency tooling (`Clumsy` on Windows, or `tc netem` on Linux, or Unity Network Simulator package). **Not confirmed set up.** Without it, V6 stand-in = zero-RTT host self-push convergence, which is weaker than the V6 spec.

---

## §10 — Open decision points for reviewer

Before Phase 4 V1 begins, **reviewer must green-light**:

### 10.1 Scope split (DP-1)

- **A) Split into Phase 4a + Phase 4b** (recommended).
- **B) Single-phase Phase 4** with locomotion-first commit order (DP-3 fallback).

### 10.2 L7 roll-in (DP-0)

- **A) Option (b) — roll L7 fix into Phase 4b as Prereq-1** (recommended; fix shape = adapter Initialize()).
- **B) Option (a) — dedicated pre-Phase-4 L7-only PR**.
- **Reject option (c) — defer** (violates L7 rule literally).

### 10.3 Shadow role-flip (DP-2)

- **A) Option III — hybrid (delete-immediately 4a, inverted-shadow 4b)** (recommended).
- **B) Uniform Option I (delete-immediately 4a+4b)** — simpler motor; V6 gate becomes sole divergence detector.
- **C) Uniform Option II (inverted-shadow 4a+4b)** — heaviest safety net; temporary motor bloat.

### 10.4 L9 clamp-strip behavior (DP-6 R3)

- **A) Preserve (R3.a)** (recommended) — ship cut-over, defer fix.
- **B) Resolve (R3.b or R3.c)** — bundle the fix into Phase 4b.

### 10.5 L10 legacy fallback (DP-6 R4)

- **A) Retain gate (R4.a)** (recommended) — legacy fallback branch in adapter until Phase 7/8.
- **B) Drop fallback (R4.b)** — smaller adapter, higher risk.

### 10.6 Hard-stop approval (DP-5 Category D)

Reviewer must explicitly approve:
- Removal of motor's `TryApplyServerAuthoritativeImpulse` (public API).
- Removal of motor's `QueueImpulseEventTargetRpc` (public RPC entry point).
- Deletion of `BuddahPredictionCombatRouting` static class.
- 3 skill-caller rewrites under `Assets/Scripts/Buddah/ComboSkill/` (cross-manifest-system touch: prediction ↔ skill-execution).

### 10.7 V6 network-latency tooling (DP-7 blocker)

- **A)** Confirm `Clumsy` / `tc netem` / Unity Network Simulator is set up before Phase 4b V1.
- **B)** Accept zero-RTT stand-in for Phase 4b V1 and defer true V6 to a separate post-4b pass.

### 10.8 `BUDDAH_PREDICTION_LEGACY_SHADOW` define (DP-2 Option II/III)

- If Option II/III chosen: **add define to `ProjectSettings/ProjectSettings.asset`** in same target scope as `BUDDAH_PREDICTION_SHADOW` (Standalone).

### 10.9 RaceMap.unity uncommitted changes

- Git status reports `M Assets/Scenes/RaceMap.unity` — presumed Phase 3d scene probe residue. **Commit or revert before Phase 4 V1** so Phase 4 PR diff is clean.

### 10.10 Epsilon + cadence consistency

- Use 1e-4 float epsilon and 1e-4 Quaternion.Angle-degree epsilon consistent with 3a/3b/3c/3d shadow conventions (including L15 zero-quaternion normalize in any new shadow compare paths that involve Quaternion types).

---

## §11 — Hard constraints observed

- No `.cs` file changed.
- No Phase 4 implementation started.
- Findings above — report back. No decisions taken on §10-1 through §10-10.
- Protected-file read (BuddahPredictedMotor.cs) for evidence only, line references cited.
- No `p4` commands issued (repo is git — per L3 2026-04-18 update).
- No protected files touched. Motor reads are inspection-only, not edits.

No code changes until reviewer resolves §10. Report when audit reviewed.

---

## Addendum A — Baseline-state commit tips for Phase 4 pre-run

- Phase 3d HEAD: `f16c9b0` (Phase 3c merge tip) → Phase 3d commits unmerged locally → branch is `refactor/prediction-v2`.
- Phase 3d closeout reports "V5 2-peer PASS (both peers)" but `git log --oneline` on local branch shows Phase 3d commits not yet pushed to main. Verify with `git log origin/main..HEAD` before Phase 4a V1 starts. If Phase 3d merged, Phase 4a V1 branches from the post-merge main; if not, Phase 4a V1 stacks on the Phase 3d tip and waits for 3d merge.
- RaceMap.unity uncommitted change: resolve before Phase 4 V1 per §10.9.

## Addendum B — Recommended commit-message skeleton for Phase 4a (locomotion)

```
Phase 4a — Locomotion cut-over (authority via BuddahLocomotionStep)

Replaces motor's inline locomotion block (motor.cs:441-464) with a
BuddahLocomotionStep.Run authority call. Parity-by-construction: the
step previously ran as Phase 3a shadow; post-4a it IS the authority.
Behavior-neutral by construction.

=== What this lands ===
- Core/BuddahPredictedMotor.cs: inline block replaced with step call; shadow-compare harness retained (loc-div remains a live counter, now defined as "step self-consistency" across reconcile replays — always 0 by construction post-4a).
- Simulation/BuddahLocomotionStep.cs: signature extended to apply forces + torque via ref PredictionRigidbody parameter OR caller-applied from returned struct (decide).

=== Validation ===
V1 compile: PASS.
V2 host-only 60s: loc-div=0 on every heartbeat, loc-compared=active-ticks 1:1; imp/tel/mod/hof parity preserved.
V3 gameplay shake 30s: no visible shake on 240fps owner capture + spectator capture.
V5 2-peer 60s: loc-div=0 both peers; CLIENT reconcile-replay ratio >> 1:1 preserved; V3 spectator pass.
V13: tick cost within +10% of Phase 3d baseline.

=== Carry-over ===
- Phase 4b pending: impulse + CombatAdapter + L7 Initialize() + CombatRouting deletion.
- L9 clamp-strip behavior: preserved by Phase 4a (no clamp/consume re-order).
- Phase 8 cleanup-queue: no new entries from 4a.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

## Addendum C — Recommended commit-message skeleton for Phase 4b (impulse + adapter + L7)

```
Phase 4b — Impulse cut-over + CombatAdapter + L7 Initialize() fix

Migrates impulse storage from motor-private _impulseEventQueue to
BuddahPredictionCommandBus._impulse channel. Implements
BuddahPredictionCombatAdapter (replaces CombatRouting static class).
Ships the L7 Option-B adapter-side defer-until-first-replicate fix as
the adapter's Initialize() late-bind hook. Deletes CombatRouting,
motor's TryApplyServerAuthoritativeImpulse + QueueImpulseEventTargetRpc
+ _impulseEventQueue field.

=== What this lands ===
- Integration/BuddahPredictionCombatAdapter.cs: populated with TryRouteImpulse + Initialize().
- Integration/BuddahPredictionCombatRouting.cs: DELETED + meta.
- Core/BuddahPredictedMotor.cs: impulse-related API removed, consume migrated to BuddahImpulseStep.Run authority draining CommandBus.ImpulseChannel; ImpulseQueueState reconcile write-source migrated to bus.
- Simulation/BuddahImpulseStep.cs: promoted from cursor-shadow to authority (applies rb forces).
- Events/BuddahPredictionCommandBus.cs: minor (drain API if step doesn't read channel directly).
- Assets/Scripts/Buddah/PushHitbox.cs + 2 projectile runtimes: callers migrated from CombatRouting to CombatAdapter.

=== Validation ===
V1 compile: PASS.
V2 host-only 60s + 2× cross-peer push: imp-div=0, imp-compared>0, [CommandBus]:ClearAll = 2/spawn (L7 verification).
V5 2-peer 60s: CLIENT imp-compared>0 (cross-peer Target_EnqueueImpulse exercised).
V3 / V6 / V13: PASS per gates.

=== L-delta ===
- L7 fix acceptance criterion satisfied (2× ClearAll/spawn).
- L10 legacy fallback retained per R4.a (adapter keeps IsPredictionModeActive gate).
- L9 clamp-strip behavior preserved per R3.a (deferred to Phase 8 cleanup-queue).

=== Phase 8 cleanup-queue additions ===
- Entry 3 (NEW): impulse queue serializer precision audit (motor queue semantics → bus channel semantics).
- Entry 4 (NEW): legacy PushTargetBox fallback branch removal (R4.b, once all callers confirmed predicted-mode-always).
- Entry 5 (NEW): L9 clamp-strip resolution decision (R3.b or R3.c).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

End of audit. No `.cs` files changed. Awaiting reviewer decisions on §10 before Phase 4 V1 implementation begins.

---

## Addendum A (2026-04-19, post-reviewer-response) — DP-6 R2 torque-decay equivalence proof

**Reviewer directive**: "You flagged that the motor's inline decay branch may do active angular-y damping that BuddahLocomotionStep.cs:54 writes `0f` for. If this is real, Phase 3a shadow's `loc-div=0` conclusion does NOT prove semantic equivalence, and 4a's delete-immediately premise collapses. Produce side-by-side code block, trace two concrete inputs, verdict EQUIVALENT or SHADOW-GAP."

### A.1 Scratch reset semantics (prerequisite)

`BuddahPredictedMotor.cs:321-324`:
```csharp
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
            _realScratch = default;
            _shadowScratch = default;
#endif
```

Every `RunInputs` entry resets both scratches to `default` (struct all-zero). So `_realScratch.CommandedTurnTorque` starts each tick at `0f`. Any branch that does NOT explicitly write to the field leaves it at `0f`.

### A.2 Side-by-side: motor inline block vs shadow step

**Motor's inline locomotion block** (`BuddahPredictedMotor.cs:441-464`):
```csharp
Vector3 forwardForce = forwardDirection * (_computedStats.FinalForwardForce * resolvedThrottle);
_predictionRigidbody.AddForce(forwardForce, ForceMode.Force);
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
_realScratch.CommandedForwardForce = forwardForce;
_realScratch.LocomotionRan = true;
#endif

if (_computedStats.IsSteeringSuppressed)
    resolvedSteering = 0f;

if (Mathf.Abs(resolvedSteering) > 0.001f)
{
    float turnTorque = resolvedSteering * _computedStats.FinalTurnTorque;
    _predictionRigidbody.AddTorque(Vector3.up * turnTorque, ForceMode.Force);
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW
    _realScratch.CommandedTurnTorque = turnTorque;
#endif
}
else if (config.TurnDecayPerSecond > 0f)
{
    Vector3 angularVelocity = rb.angularVelocity;
    angularVelocity.y = Mathf.MoveTowards(angularVelocity.y, 0f, config.TurnDecayPerSecond * (float)TimeManager.TickDelta);
    _predictionRigidbody.AngularVelocity(angularVelocity);
}
```

**Shadow step** (`BuddahLocomotionStep.cs:29-56`):
```csharp
scratch.LocomotionRan = true;

// Pre-force planar xz clamp (omitted for brevity)
float effMax = ctx.ComputedStats.FinalMaxSpeed
               + (ctx.ComputedStats.IsPushGraceActive ? ctx.PushGraceExtraSpeed : 0f);
Vector3 velocity = ctx.RbVelocityPreTick;
Vector3 planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
if (planarVelocity.sqrMagnitude > effMax * effMax)
{
    Vector3 clampedPlanar = planarVelocity.normalized * effMax;
    scratch.VelocityAfterClamp = new Vector3(clampedPlanar.x, velocity.y, clampedPlanar.z);
    scratch.ClampingApplied = true;
}

// Forward force
scratch.CommandedForwardForce =
    ctx.ForwardDirection * (ctx.ComputedStats.FinalForwardForce * ctx.ResolvedThrottle);

// Torque
scratch.CommandedTurnTorque =
    Mathf.Abs(ctx.ResolvedSteering) > 0.001f
        ? ctx.ResolvedSteering * ctx.ComputedStats.FinalTurnTorque
        : 0f;
```

**Shadow compare site** (`BuddahPredictedMotor.cs:1329`):
```csharp
float trnDelta = Mathf.Abs(_realScratch.CommandedTurnTorque - _shadowScratch.CommandedTurnTorque);
```

### A.3 Concrete input traces

#### Trace 1 — steering = 0 at rest (`rb.angularVelocity = (0,0,0)`, `config.TurnDecayPerSecond = 5f`)

| Path | Branch taken | rb.angularVelocity side effect | `_realScratch.CommandedTurnTorque` |
|------|--------------|--------------------------------|-------------------------------------|
| Motor | else-if decay (line 459-463) | `angularVelocity.y = MoveTowards(0, 0, 5 * 0.01666) = 0f` → `AngularVelocity((0,0,0))` — **no visible delta** (already at 0) | unwritten → remains `0f` (tick-reset default) |
| Shadow step | decay branch (line 52-55, `|0| > 0.001f` is false) | N/A (step does not touch rb) | `scratch.CommandedTurnTorque = 0f` (explicit) |
| Compare | `trnDelta = \|0 - 0\| = 0f` → loc-div = 0 | — | equal |

**Result**: `loc-div = 0`. Mathematically correct. Observable motor behavior also unchanged (no spin, no damping needed). Path is silently-equivalent.

#### Trace 2 — steering = 0 at high angular velocity (`rb.angularVelocity = (0, 3.5, 0)`, `config.TurnDecayPerSecond = 5f`, `TickDelta = 0.01666f`)

| Path | Branch taken | rb.angularVelocity side effect | `_realScratch.CommandedTurnTorque` |
|------|--------------|--------------------------------|-------------------------------------|
| Motor | else-if decay | `angularVelocity.y = MoveTowards(3.5, 0, 5 * 0.01666) = 3.5 - 0.0833 = 3.4167f` → `AngularVelocity((0, 3.4167, 0))` — **rb.angularVelocity.y DROPS 0.0833 per tick** | unwritten → remains `0f` |
| Shadow step | decay branch (same) | N/A — step does not touch rb, scratch does not carry a decay-damp field | `scratch.CommandedTurnTorque = 0f` (explicit) |
| Compare | `trnDelta = \|0 - 0\| = 0f` → loc-div = 0 | — | equal |

**Result**: `loc-div = 0` — **but the motor IS mutating rb.angularVelocity.y** (damping 0.0833 rad/s per tick ≈ 5 rad/s/sec), and the shadow step has no observation of this side effect. The shadow compare is blind to the angular-velocity damp.

### A.4 Verdict: **SHADOW-GAP**

The shadow step's scope is **commanded-scalar-only**. It models:
- `CommandedForwardForce` (Vector3, the input to motor's `AddForce(forwardForce, ForceMode.Force)`)
- `CommandedTurnTorque` (scalar, the input to motor's `AddTorque(Vector3.up * turnTorque, ForceMode.Force)`)
- `ClampingApplied` / `VelocityAfterClamp` (clamp gate bookkeeping)

The step's scope does NOT cover:
- Motor's **else-if decay branch side effect** — a direct `_predictionRigidbody.AngularVelocity(...)` write that mutates `rb.angularVelocity.y` via `Mathf.MoveTowards`. This is not a commanded torque; it's a direct angular-velocity assignment.

**Why Phase 3a shadow's `loc-div=0` is mathematically correct but not semantically sufficient for 4a**:

- Both Traces 1 and 2 produce `loc-div = 0`. The compare at motor.cs:1329 only reads `CommandedTurnTorque`. Motor's decay branch does not write this field; shadow step's decay branch writes `0f`. Both sides agree on `0f`. The compare correctly reports equality.
- However, the motor's decay branch in Trace 2 MUTATES `rb.angularVelocity.y` (drops 0.0833 per tick). This mutation has **no corresponding value in `_realScratch` OR `_shadowScratch`** that either side writes or the compare checks. The shadow step does not model the decay output at all.
- The step's own inline comment at `BuddahLocomotionStep.cs:54` documents this scope reduction explicitly: `"decay branch leaves scratch at zero"`. The author of Phase 3a was aware decay was not modeled and elected to exclude it from shadow scope. This is a **deliberate, documented scope reduction**, not a shadow bug.
- Phase 3a's `loc-div=0` therefore proves commanded-scalar equivalence within the step's declared scope. It does **not** prove behavior-equivalence of motor's decay side effect. Both facts are compatible — the shadow was never making the stronger claim.

**Consequence for Phase 4a delete-immediately premise**:

If Phase 4a deletes motor.cs:441-464 and replaces it with `BuddahLocomotionStep.Run` returning `CommandedForwardForce` + `CommandedTurnTorque` for motor to apply via `AddForce` + `AddTorque`:

- The forward-force path is preserved (step's `CommandedForwardForce` → motor applies `AddForce` → same rb delta).
- The active-steering-torque path is preserved (step's `CommandedTurnTorque` → motor applies `AddTorque` → same rb delta).
- **The decay path is LOST**: the step's commanded torque of `0f` tells motor to apply `AddTorque(Vector3.up * 0f, Force) = no-op`. Motor's original decay `AngularVelocity(...)` write has no step-side replacement. Any Buddah with carry-over angular-y velocity (from a prior steering input, an impulse torque, or a handoff-snapshot angular-velocity inheritance) will no longer have its spin damped when the player releases steering. Spin persists until another damping path (friction, drag, collision) zeros it out.

**Observable gameplay regression**: released-steering spin holds longer. Magnitude: on `TurnDecayPerSecond = 5f`, angular-y drops 5 rad/s per second — so if a Buddah carried 3.5 rad/s at release, decay reaches 0 in ~0.7 seconds. Post-4a-delete-immediately, that decay vanishes and the 3.5 rad/s persists indefinitely (modulo physics friction). **Visible in V3 gameplay-shake at the owner level — the Buddah model rotates when standing still.**

### A.5 Classification

Not "(a) motor false-positive — fix motor to match step": motor's decay branch is legitimate, user-facing gameplay behavior. `config.TurnDecayPerSecond` is a designer-authored setting, not a bug.

Not "(b) step is buggy — fix step to match motor" strictly either: the step was scope-reduced by design with an explicit comment. The Phase 3a commit deferred the scope expansion rather than shipping an incomplete shadow.

**Correct classification**: **step's scope is insufficient for 4a's delete-immediately premise.** Phase 3a validated commanded-scalar parity under that declared scope. Phase 4a's delete-immediately implicitly strengthens the claim to "step replaces motor's full inline block behavior", which the step's current scope does not certify.

### A.6 Options for reviewer (do NOT auto-fix; decision request only)

- **Z1 — Extend step scope to model decay** ("fix step to match motor"): add `Vector3 CommandedAngularVelocityAfterDecay` (or `float AngularYAfterDecay`) field on `BuddahPredictionShadowScratch`. Update step to mirror `Mathf.MoveTowards(ctx.RbAngularVelocityPreTick.y, 0f, ctx.Config.TurnDecayPerSecond * ctx.TickDeltaSeconds)` when the decay branch triggers. Add a 4th compare slot in `Shadow_CompareAndReport` (epsilon 1e-4, gated on `!IsSteeringSuppressed && |steering| <= 0.001f && config.TurnDecayPerSecond > 0f`). Rerun Phase 3a V2 + V5 BEFORE 4a, confirm new field div=0 across heartbeats. Only then proceed with 4a delete-immediately.
  - **Pros**: restores parity-by-construction claim for the expanded scope; 4a delete-immediately becomes safe; future phases can trust the scope extension going forward.
  - **Cons**: adds a Phase 3a re-validation pass (call it Phase 3a-fix-up) before Phase 4a can start. Requires new `RbAngularVelocityPreTick` snapshot on the tick context. ~40 LOC motor + step + scratch + compare.
- **Z2 — Hybrid Phase 4a: keep motor's decay branch inline, migrate only forward-force + active-steering-torque to step**: motor retains `else if (config.TurnDecayPerSecond > 0f) { ... AngularVelocity(...) ... }` unchanged. Only lines 441-446 + 448-458 migrate to step-returned application sites. Shadow compare unchanged (already matches motor's partial authority).
  - **Pros**: smallest Phase 4a diff; no step-scope expansion; no Phase 3a re-validation. Shadow-gap is irrelevant because motor retains the uncovered side effect.
  - **Cons**: motor file stays ~5 lines heavier than Option Z1 post-4a; "step is the authority for locomotion" is a weaker claim ("step is the authority for commanded-scalar locomotion; motor retains direct-rb decay"). Phase 8 cleanup-queue candidate to eventually fold decay into step.
- **Z3 — Step writes rb directly**: pass `_predictionRigidbody` + `rb` + `config` + `tickDeltaSeconds` into the step. Step's decay branch calls `prediction.AngularVelocity(...)` itself.
  - **Pros**: full cut-over, single site-of-truth for all locomotion including decay.
  - **Cons**: breaks step's current "pure static, no Unity API" contract. Tests that relied on the step being a pure function now can't run without a PredictionRigidbody mock. Parity-by-construction claim for Phase 4a is stronger but harder to verify.

### A.7 Recommendation

**Option Z2 — hybrid 4a**. Rationale:
- Smallest Phase 4a diff; no Phase 3a rework required.
- Retains the authority-migration promise for the 95% of locomotion that is commanded-scalar work.
- Explicitly acknowledges the decay-side-effect as a Phase 8 cleanup candidate (Phase 4a closeout adds it to `phase-8-cleanup-queue.md` as Entry 3, parallel to existing entries).
- Preserves Phase 3a shadow's observational mandate without retroactively strengthening the claim.
- If reviewer wants full cut-over, Option Z1 > Z3 (purity > ergonomics).

### A.8 Reviewer decision needed

Per protocol ("If Action 1 returns SHADOW-GAP, STOP and wait for reviewer regardless of Actions 2/3 status"), **stopping here**. Actions 2 (V3/V6/V13 definitions) and Action 3 (RaceMap.unity investigation) are **NOT performed in this addendum**. Await reviewer choice of Z1 / Z2 / Z3 before proceeding.

---

End of Addendum A. No `.cs` files changed. No Phase 3a/4a implementation started.

### A.9 Reviewer Decision (2026-04-19)

**Selected: Option Z2 — Hybrid Phase 4a.**

Rejected rationales (summary):
- **Z1 rejected**: re-opens merged Phase 3a for scope extension + re-validation V5 2-peer playtest. Scope-contract violation (3a closed with its declared commanded-scalar scope; retroactive widening invalidates the closed-out gate model).
- **Z3 rejected**: breaks `BuddahLocomotionStep`'s pure-static contract — the architectural foundation Phase 3 shadow mode depends on (pure static = deterministic replay, no Unity API, no `PredictionRigidbody` binding). Infrastructure cost too high for one side effect.

### A.10 Z2 precise scope boundary (binding for 4a V1 plan)

| Element | 4a V1 disposition | Site |
|---------|-------------------|------|
| `CommandedForwardForce` computation | **Migrate to step** (pure static compute in `BuddahLocomotionStep.Run`) | motor.cs:441 → step |
| `_predictionRigidbody.AddForce(forwardForce, Force)` apply | **Retain in motor** — apply is not compute; step is pure-static compute only | motor.cs:442 (unchanged) |
| `CommandedTurnTorque` computation (active-steering branch, `|steering| > 0.001f`) | **Migrate to step** | motor.cs:453 → step |
| `_predictionRigidbody.AddTorque(Vector3.up * turnTorque, Force)` apply (active-steering branch) | **Retain in motor** | motor.cs:454 (unchanged) |
| Decay branch entire block (`!hasActiveSteering` + `config.TurnDecayPerSecond > 0f` + `Mathf.MoveTowards` + `_predictionRigidbody.AngularVelocity(...)`) | **Retain in motor inline, unchanged** | motor.cs:459-463 (untouched) |
| `IsSteeringSuppressed` zero-out gate | **Retain in motor** (pre-step input shaping) | motor.cs:448-449 (unchanged) |

**Shadow mode behavior (unchanged)**: two-scratch compare of `CommandedForwardForce` + `CommandedTurnTorque`; `loc-div` heartbeat semantics identical to Phase 3a. No new compare slots, no new fields, no epsilon changes. Phase 3a's declared scope remains intact.

**Gate model (unchanged)**: 4a V1 PASS gate on the loc compare line matches Phase 3a's V2 + V5 signatures verbatim. `loc-div = 0` on every heartbeat + `loc-compared > 0` at tail (parity-by-call-site-invariant preserved — step is called every active tick from motor).

**Phase 8 cleanup candidate (separate entry)**: migrating the decay branch to the step is contingent on a future Phase 3a scope extension. Recorded as Entry 3 in `agent-exchange/handoff/phase-8-cleanup-queue.md` with a prerequisite gate so a future Phase 8 agent does not delete the motor decay branch as part of a sweep cleanup.

---

End of Addendum A (including §A.9 Reviewer Decision + §A.10 Z2 scope boundary). Proceed to Actions 2 + 3 below.

---

## Addendum B (2026-04-19, post-Z2-green-light) — V3 / V6 / V13 precise definitions (Action 2)

Reviewer required objective, log-or-profile-evaluable gate definitions for Phase 4's new validation gates (V3 gameplay-shake, V6 combat-determinism, V13 perf-budget). Each gate below specifies: **measurement target**, **tooling**, **capture procedure**, **PASS threshold**, **baseline capture gate** (where baseline is required and not yet on file).

### B.1 — V3: Gameplay shake (owner + spectator)

#### B.1.1 Measurement target

Frame-to-frame delta of the visual root's world-space position and rotation, sampled every `LateUpdate`, emitted per-frame, aggregated per-second and per-session. Two measurable quantities:

- **Visual-position delta magnitude per frame** (meters): `||visualRoot.position_frame_N - visualRoot.position_frame_{N-1}||`.
- **Visual-rotation delta magnitude per frame** (degrees): `Quaternion.Angle(visualRoot.rotation_frame_N, visualRoot.rotation_frame_{N-1})` — with L15 zero-normalize applied.

Rationale: "shake" is high-frequency reversal of position/rotation at render rate. A locomotion cut-over regression expresses as elevated frame-delta p99, even if mean stays stable.

#### B.1.2 Tooling

Add a compile-gated probe `BuddahPredictionVisualShakeProbe` inside `Assets/Scripts/New_Buddah/Debug/` behind a NEW scripting define `BUDDAH_PREDICTION_VISUAL_PROBE` (analogous to `BUDDAH_PREDICTION_SHADOW` separation). Probe samples the Buddah's `BuddahPredictionVisualRootBridge.VisualRoot` transform pose every `LateUpdate`, computes frame-delta magnitudes, emits heartbeat lines every 60 frames:

```
[D-VIS HEARTBEAT] T=<tick> owner=<bool> frames=<n>
  pos-dmax=<f> pos-dp99=<f> pos-davg=<f>
  rot-dmax=<f> rot-dp99=<f> rot-davg=<f>
```

Define lives alongside `BUDDAH_PREDICTION_SHADOW` in `ProjectSettings/ProjectSettings.asset` Standalone scripting defines. Code compiles out in release.

**Why not 240fps screen-record + manual frame inspection** (the plan's original framing): subjective, non-automatable, not reviewer-auditable from a log digest, and requires 8× the storage per test run. The log-probe is evaluable from an Editor.log / Player.log grep identical to 3a/3b heartbeat parsing.

#### B.1.3 Capture procedure (updated 2026-04-19 per reviewer Option B decision on Clarification 2)

**Baseline (3d-tip, pre-Phase-4a V1)** — **2-peer 2-Buddah, 90s**:
1. HOST Build + CLIENT Editor, Phase 3d V5 shape. Both probe defines (`BUDDAH_PREDICTION_VISUAL_PROBE` + `BUDDAH_PREDICTION_PERF_PROBE`) enabled locally on BOTH peers.
2. `BuddahPredictionVisualShakeProbe` on the Buddah prefab root (per-Buddah, correct). `BuddahPredictionPerfProbe` as scene-singleton (Awake guard enforces).
3. Session content: normal locomotion — accel / cruise / decel / steering / decay across the full motion envelope. **No deliberate skill firing.**
4. Duration: 90s minimum; extend to 120s or 150s if 30s-window variance >20% (G2 bind).
5. Probe emits `[D-VIS HEARTBEAT]` with `owner=True` on the local peer's Buddah and `owner=False` on the remote peer's Buddah.

**Post-4a V1 V3 gate** — same 2-peer 2-Buddah 90s shape. Probe defines on BOTH peers. Aggregate pos-dp99 + rot-dp99 per peer per owner tag. Compare against baseline.

#### B.1.4 Baseline capture (required BEFORE 4a V1)

Run probes on `refactor/prediction-v2` post-Phase-3d tip (pre-4a) under the capture procedure above. Record per-peer per-owner-tag aggregates:
- `V3_BASELINE_POS_DP99_{HOST,CLIENT}_{OWNER,SPECTATOR}` (meters per frame)
- `V3_BASELINE_ROT_DP99_{HOST,CLIENT}_{OWNER,SPECTATOR}` (degrees per frame)

Baseline digest files (**4 files**, reviewer-pinned per Clarification 2 Option B):
- `agent-exchange/console/2026-04-19-phase4-baseline-v3-host.log`
- `agent-exchange/console/2026-04-19-phase4-baseline-v3-client.log`
- `agent-exchange/console/2026-04-19-phase4-baseline-v13-host.log` (covers V13 from the same session)
- `agent-exchange/console/2026-04-19-phase4-baseline-v13-client.log` (covers V13 from the same session)

Capture is prerequisite for Phase 4a V1 PASS evaluation; without it, V3 post-4a has nothing to compare against.

#### B.1.5 PASS threshold

For each of {pos, rot} × {owner, spectator}:

- **Primary gate (strict)**: `post-4a <metric>-dp99 <= 1.10 * baseline <metric>-dp99`. +10% ceiling matches V13's philosophy (small regressions tolerated, large ones flagged).
- **Secondary gate (hard cap)**: `post-4a pos-dmax <= 0.25 m/frame`. Covers the case where baseline is pathologically low (1e-6 magnitudes) and +10% is numerically meaningless. 0.25 m/frame at 60fps render = 15 m/s — well above any legitimate Buddah movement, so any frame above it is a visible jump.
- **Tertiary gate (rotation hard cap)**: `post-4a rot-dmax <= 15 deg/frame`. At 60fps = 900 deg/s. Any frame above = flicker.

V3 PASS = all four primary gates + all two hard-cap gates.

#### B.1.6 Fallback if probe infrastructure cannot ship

If the probe class or define cannot land before 4a V1 (reviewer override), fall back to FishNet's built-in smoothing diagnostics OR a manual 240fps OBS capture of owner + spectator with side-by-side pre/post comparison. Not preferred because subjective, but exists as a last-resort pass criterion.

### B.2 — V6: Combat determinism (100ms RTT)

#### B.2.1 Measurement target

After a server-issued impulse lands on a non-owner Buddah at 100ms symmetric RTT, the owner's predicted position at tick `T_impulse + 1` must converge to the server's reconcile-broadcast position at tick `T_impulse + 1` within a prediction epsilon. Measurable:

- **Post-impulse convergence delta** (meters): `||owner_position@T_reconcile_apply - server_reconcile_position@T_reconcile_apply||`, where `T_reconcile_apply` is the owner's tick on which FishNet applies the reconcile packet carrying the impulse-result-tick server state.
- **Rubber-band counter**: number of frames between impulse apply and position-stable (post-reconcile). Stable = delta to previous frame < 0.05m. Rubber-band PASS = stable within `<=1` physics tick of reconcile apply.

#### B.2.2 Tooling

FishNet built-in `LatencySimulator` configured via a new `LatencySimulatorSettings` ScriptableObject wrapper at `Assets/Settings/V6Latency_100ms.asset` (spec'd below — **do not commit the asset yet**).

Asset spec:

```
Path: Assets/Settings/V6Latency_100ms.asset
Type: BuddahPrediction.Testing.V6LatencyProfile (NEW ScriptableObject subclass, to be created in Phase 4a V1 prep pass)
Namespace: BuddahPrediction.Testing (or equivalent — align with NewBuddah convention)

Fields (mirrors FishNet Assets/FishNet/Runtime/Managing/Transporting/LatencySimulator.cs:49-141 serializable shape):
  - bool Enabled = true
  - bool SimulateHost = true
  - long LatencyMs = 50       # per-direction ms; RTT = 2 * 50 = 100ms in client mode
                              # Host mode: FishNet doubles this value per tooltip at line 84
                              # → empirical check required to confirm 100ms RTT in host-client pair
  - double OutOfOrder = 0.0   # 0% out-of-order chance
  - double PacketLoss = 0.0   # 0% drop chance
  - int    TickRateFloorAck   # optional; "Added latency will be a minimum of tick rate" per tooltip
                              # tickrate at 60Hz = 16.666ms floor per direction
                              # 50ms > 16.666ms so no clamp expected

Apply method (runtime):
  void ApplyTo(NetworkManager nm)
  {
      var sim = nm.TransportManager.LatencySimulation; // verify API name at spike-time
      sim.SetEnabled(Enabled);
      sim.SetLatency(LatencyMs);
      sim.SetPacketLoss(PacketLoss);
      sim.SetOutOfOrder(OutOfOrder);
      // SimulateHost field currently has no public setter per LatencySimulator.cs inspection
      // → document this gap; may require reflection OR a FishNet feature request OR a
      //   build-time hard-coded value on the manager prefab
  }
```

**Open gap flagged**: `LatencySimulator._simulateHost` field at LatencySimulator.cs:80 is `[SerializeField] private bool _simulateHost = true;` with NO public setter exposed by the file I inspected (lines 49-141). The wrapper asset cannot toggle it at runtime without reflection or a FishNet PR. Default `true` may be what we want; if so, the wrapper doesn't need to touch it. Flag to confirm in 4a V1 prep.

**Also flagged**: `LatencySimulator` exists in `FishNet.Managing.Transporting` namespace. The `TransportManager` property exposing it must be confirmed — the field `LatencySimulation` is inferred, not verified in the inspected file slice. Reviewer's "LatencySimulatorSettings" naming suggests a newer FishNet API alias; verify at 4a V1 prep (not blocking this audit).

**Clumsy rejected**: per reviewer directive. FishNet's in-process simulator is authoritative because it operates at the transport layer after all pre-send reliability queueing, matching real-world conditions more faithfully than OS-level packet shaping.

#### B.2.3 Capture procedure

1. Spec + create the ScriptableObject in a separate prep pass (not Phase 4a V1 itself).
2. Wire a runtime hook (editor-only `MonoBehaviour` or inspector button) that calls `V6LatencyProfile.ApplyTo(networkManager)` on demand.
3. V5-shape playtest: HOST Build + CLIENT Editor, 60s.
4. Arm probe BEFORE play: load `V6Latency_100ms.asset` onto TransportManager.
5. CLIENT drives; HOST triggers impulse on CLIENT's Buddah at T=X (via existing skill path — `push_projectile_hands_anti` cross-peer hit).
6. CLIENT's shadow infrastructure already captures `imp-compared` + `imp-div`. Extend with new `[D-POS] T=<tick> post-imp-conv-delta=<m> rubber-band-frames=<n>` emission inside `BuddahPredictedMotor.ReconcileState` (or post-reconcile hook) when the reconciled packet carries an impulse-consume-boundary tick.

#### B.2.4 PASS threshold

- **post-imp-conv-delta** `<= 0.05 m` (prediction epsilon, matches owner-side physics tolerance).
- **rubber-band-frames** `<= 1` (convergence completes within 1 physics tick after reconcile apply).
- `imp-div = 0` on every heartbeat (Phase 3b baseline preserved).
- `imp-compared >= 1` on CLIENT peer (cross-peer impulse exercised).

V6 PASS = all 4 criteria on CLIENT peer.

#### B.2.5 Baseline capture (required BEFORE 4b V1, not 4a V1)

V6 is a 4b gate, not 4a. Baseline capture for V6 defers to 4b prep pass. Name reserved: `V6_BASELINE_POST_IMP_CONV_DELTA` (captured at pre-4b tip = post-4a-merged tip).

### B.3 — V13: Performance budget

#### B.3.1 Measurement target

`BuddahPredictedMotor.RunInputs` wall-clock time per tick, averaged across an 8-Buddah race session at 60Hz tick. Three candidate metrics; spec all three, PASS requires all:

- **Primary — avg tick time (ms)**: `mean(runInputs_duration_ms)` across the capture window.
- **Secondary — p99 tick time (ms)**: 99th percentile tick time.
- **Tertiary — GC alloc per tick (bytes)**: `mean(GC.AllocatedMemoryForCurrentThread() delta)` per RunInputs call.

Phase 4a (locomotion cut-over) specifically stresses: (a) step call frame overhead, (b) any hidden allocation in the step's struct copy semantics.

#### B.3.2 Tooling

Unity's `ProfilerRecorder` API, invoked from a runtime probe `BuddahPredictionPerfProbe` behind a NEW scripting define `BUDDAH_PREDICTION_PERF_PROBE` (parallel to `BUDDAH_PREDICTION_SHADOW` / `..._VISUAL_PROBE`). Probe captures:

- `ProfilerRecorder` for `BuddahPredictedMotor.RunInputs` marker (add `[UnityEngine.Profiling.Profiler.BeginSample("BuddahPredictedMotor.RunInputs")]` bracket at motor.cs:315 entry + end at motor.cs:477 exit — scoped under the probe's `#if`).
- `ProfilerRecorder` for `GC.Alloc` category.
- Emits heartbeat every 1000 ticks with avg + p99 + alloc-avg aggregates:

```
[D-PERF HEARTBEAT] T=<tick> ticks=<n>
  rep-avg-ms=<f> rep-p99-ms=<f>
  gc-alloc-avg-b=<n> gc-alloc-max-b=<n>
```

**Why not Unity Profiler manual capture**: manual capture is not scriptable into the V5 digest pipeline, requires live Editor session for analysis, and produces `.data` files not greppable from a headless Build's Player.log. ProfilerRecorder-to-log is consistent with 3a/3b/3c heartbeat discipline.

#### B.3.3 Capture procedure (updated 2026-04-19 per reviewer Option B decision)

**Baseline (3d-tip, pre-Phase-4a V1)** — **2-peer 2-Buddah, 90s** (same session as V3 baseline; probes run concurrently):
1. HOST Build + CLIENT Editor. Both probe defines enabled on BOTH peers.
2. `BuddahPredictionPerfProbe` placed once per scene per peer (Awake singleton guard enforces). 2 peers × 1 probe each = 2 independent V13 data streams.
3. Session content: normal locomotion, full motion envelope, no deliberate skill firing.
4. Duration: 90s minimum; extend if 30s-window variance >20%.
5. Reviewer acknowledged blind spot: 2-Buddah load does NOT exercise concurrent-motor contention patterns that real match (4-8 Buddahs) will. V13 gate at 2 Buddahs is the best achievable with current spawn infrastructure. Phase 8 cleanup-queue Entry 5 tracks the debug-multi-spawn work that would close this gap post-refactor.

**Post-4a V1 V13 gate** — same 2-peer 2-Buddah 90s shape. Compare per-peer rep-avg-ms / rep-p99-ms / gc-alloc-avg-b against baseline.

#### B.3.4 PASS threshold

- `rep-avg-ms(post-4a) <= 1.10 * rep-avg-ms(baseline)` — +10% ceiling per 13-validation-gates.md V13 spec. Applied per-peer.
- `rep-p99-ms(post-4a) <= 1.10 * rep-p99-ms(baseline)` — p99 tracks tail regressions (e.g., first-call JIT overhead on the step). Applied per-peer.
- `gc-alloc-avg-b(post-4a) <= gc-alloc-avg-b(baseline)` — step struct copy should be stack-allocated. Ideal is 0, but reviewer accepts any value at-or-below baseline since baseline is not yet measured.

V13 PASS = all three gates on BOTH peers.

#### B.3.5 Baseline capture (required BEFORE 4a V1)

Same session as V3 baseline (V.1.3 / V.1.4 above). Single 2-peer 2-Buddah 90s session captures BOTH `V3_BASELINE_*` and `V13_BASELINE_*` metrics. Digest files (**4 files**):
- `agent-exchange/console/2026-04-19-phase4-baseline-v13-host.log` (HOST `[D-PERF HEARTBEAT]` aggregates)
- `agent-exchange/console/2026-04-19-phase4-baseline-v13-client.log` (CLIENT `[D-PERF HEARTBEAT]` aggregates)
- plus the two V3 files listed in B.1.4.

### B.4 — Summary: new defines + probe infrastructure in Phase 4 prep

Three new scripting defines, each gating one probe, each compiled out in release builds:

| Define | Probe class | Purpose | Introduced in |
|--------|------------|---------|---------------|
| `BUDDAH_PREDICTION_VISUAL_PROBE` | `BuddahPredictionVisualShakeProbe` | V3 gameplay shake measurement | 4a V1 prep |
| `BUDDAH_PREDICTION_PERF_PROBE` | `BuddahPredictionPerfProbe` | V13 tick-cost + GC-alloc measurement | 4a V1 prep |
| (none new for V6) | `BuddahPredictedMotor.ReconcileState` extension + `V6LatencyProfile` asset | V6 combat-determinism measurement | 4b V1 prep |

Probes ship **before Phase 4a V1's implementation commit** so baseline digests can be captured on 3d-tip. The probe infrastructure itself is a separate, reviewable prep PR (call it Phase 4-probes) that lands before 4a V1.

### B.5 — Dependencies + blockers for 4a V1

Before 4a V1 .cs implementation begins:

- [ ] Phase 4-probes PR ships with `BUDDAH_PREDICTION_VISUAL_PROBE` + `BUDDAH_PREDICTION_PERF_PROBE` defines + probe classes.
- [ ] Baseline digests captured on 3d-tip: `V3_BASELINE_*` metrics + `V13_BASELINE_*` metrics.
- [ ] V6LatencyProfile ScriptableObject wrapper class authored (not yet Applied — 4b V1 prep dependency, not 4a).
- [ ] RaceMap.unity uncommitted change resolved per Addendum C below.

Without the first two bullets, Phase 4a V1's V3 and V13 gates cannot be evaluated objectively. Reviewer may choose to (a) gate 4a V1 entry on probe-PR-first, or (b) accept 4a V1 implementation + probe PR as parallel work with baseline captured just before 4a merge evaluation.

---

## Addendum C (2026-04-19) — RaceMap.unity uncommitted change investigation (Action 3)

Per reviewer directive: do NOT revert, do NOT commit. Report `git diff` + `git log` + `git status` findings.

### C.1 `git status Assets/Scenes/RaceMap.unity`

```
On branch refactor/prediction-v2
Your branch is up to date with 'origin/refactor/prediction-v2'.

Changes not staged for commit:
  (use "git add <file>..." to update what will be committed)
  (use "git restore <file>..." to discard changes in working directory)
	modified:   Assets/Scenes/RaceMap.unity

no changes added to commit (use "git add" and/or "git commit -a")
```

One modified file, unstaged. Branch `refactor/prediction-v2` is up-to-date with origin — this change did not migrate from elsewhere; it originates locally on this branch.

### C.2 `git diff Assets/Scenes/RaceMap.unity`

5 lines added, 0 deleted (`--stat`: `1 file changed, 5 insertions(+)`). Full diff:

```diff
diff --git a/Assets/Scenes/RaceMap.unity b/Assets/Scenes/RaceMap.unity
index 938cfe8..3a28498 100644
--- a/Assets/Scenes/RaceMap.unity
+++ b/Assets/Scenes/RaceMap.unity
@@ -3867,6 +3867,11 @@ PrefabInstance:
       propertyPath: m_Materials.Array.data[0]
       value:
       objectReference: {fileID: 2100000, guid: d16a1212491b840468f60aa80e134ded, type: 2}
+    - target: {fileID: 4269900605184349933, guid: eed43fce88059ab489967ad03a04edf6,
+        type: 3}
+      propertyPath: m_IsActive
+      value: 1
+      objectReference: {fileID: 0}
     - target: {fileID: 4418303095965887071, guid: eed43fce88059ab489967ad03a04edf6,
         type: 3}
       propertyPath: m_LocalScale.x
```

**Semantic interpretation**:
- A single `PrefabInstance` override entry was added to RaceMap.unity's PrefabInstance modification list.
- `target.guid = eed43fce88059ab489967ad03a04edf6, type: 3` — corresponds to the imported model `Assets/Sources/地图/地图场景_3.9.fbx` (verified by grep: the only non-scene file carrying that GUID is `Assets/Sources/地图/地图场景_3.9.fbx.meta`). Type 3 = FBX/Model asset.
- `target.fileID = 4269900605184349933` — the internal Unity fileID for a specific GameObject inside the `地图场景_3.9.fbx` model hierarchy (one of the map's sub-GameObjects; Unity doesn't expose the name from fileID alone without opening the scene).
- `propertyPath: m_IsActive, value: 1` — the override toggles this GameObject's active state from default (inactive in the model, or ambiguous) to **active** (enabled in the scene).

**Shape of change**: scene PrefabInstance override enabling a specific sub-GameObject of the main race map FBX. Zero code change. Zero serialized-field rename. Zero shader/material change.

### C.3 `git log -1 --name-only -- Assets/Scenes/RaceMap.unity`

Last committed modification:

```
commit f13db611c61339e118703a39822998dbe055421d
Author: Yonezawa-Akane <dwh8828@icloud.com>
Date:   Sat Apr 18 03:40:17 2026 -0400

    fix: session bugfix bundle + agent harness scaffolding
    (...)

Assets/Scenes/RaceMap.unity
```

- Committed: **2026-04-18**, by Yonezawa-Akane (verified user; matches `git config user.name` for this branch).
- Commit message: "fix: session bugfix bundle + agent harness scaffolding" — a large bundled commit containing 12+ unrelated changes (gameplay fixes, log noise reduction, config/tooling, agent harness docs, safety ignore rules). RaceMap.unity was part of the bundle but the commit message doesn't specifically call out what changed in it.

Phase 3d shadow work on the branch landed on **2026-04-19** (one day later). Phase 3d's commits touched `BuddahPredictedMotor.cs` / `BuddahPredictedLaunchHandoffResolver.cs` / shadow step files — **no scene changes**. The Phase 3d closeout explicitly lists its touched files; RaceMap.unity is not among them. The uncommitted change is **therefore NOT Phase 3d residue**.

### C.4 Hypotheses (ranked by evidence)

#### H1 — Editor accidental save during playtest (most likely)

Unity saves scene PrefabInstance override lists when the scene is marked dirty by any in-play or in-editor mutation, then saved (explicit Ctrl+S) or via auto-save on domain reload. A playtest that toggled a GameObject's active state (e.g., via game code calling `SetActive(true)` on a map-embedded object, or a user accidentally toggling a GameObject in the Inspector's Active checkbox) can produce exactly this diff shape. The clean single-entry override, all other sibling overrides untouched, and the `_IsActive=1` value all support this. Evidence strength: **high**.

#### H2 — Deliberate map tweak by user, not yet committed

A user intentionally enabled a map sub-object as part of a pending fix (e.g., "re-enable the hidden checkpoint marker"). Would also produce this exact diff shape. Against H2: branch is on `refactor/prediction-v2` (prediction refactor), no open task mentions map changes; nothing in recent handoff packets or `commit-msg.txt` references RaceMap work. Evidence strength: **low-medium**.

#### H3 — Merge conflict residue from pulling origin

Ruled out: `git status` reports branch is up-to-date with origin, no pull-during-merge markers in the diff, no conflict-resolution artifacts. Evidence strength: **ruled out**.

#### H4 — Phase 3d shadow residue

Ruled out: Phase 3d commits touched no scenes. No `BuddahPredictionShadow*` component was attached/detached to any map GameObject by the closeout. Evidence strength: **ruled out**.

### C.5 Consistency with Phase 3d closeout

Phase 3d closeout (`2026-04-19-phase3d-closeout.md`) explicitly lists its touched files (motor + resolver + shadow steps + ShadowScratch + TickContext + lessons-log + phase-6-prereq + phase-8-cleanup-queue + audit doc). **RaceMap.unity is NOT in that list.** The uncommitted change predates (committed baseline 2026-04-18) or post-dates Phase 3d (unstaged modification sometime before/during Phase 4 audit 2026-04-19) by at least one day and is not Phase 3d work.

### C.6 Recommendation for reviewer

The change is semantically trivial (one bool toggle on a map sub-object). Three disposition options:

- **R1 — Commit as-is on a pre-4a-prep micro-commit**: if H2 is correct (deliberate map tweak), a clean single-file commit `"chore(map): enable <subobject> in RaceMap"` gives Phase 4a V1 a clean working tree without losing the intended edit.
- **R2 — Revert and ask user**: if H1 is correct (accidental save), revert restores the committed baseline. Risk: if it was actually a deliberate edit the user meant to keep, revert loses their work silently.
- **R3 — Investigate further (user confirmation)**: reviewer (or user) opens the scene in Unity, identifies which GameObject `fileID: 4269900605184349933` is (name + hierarchy path visible in the Unity inspector once the scene is loaded), and confirms intent. Zero-risk but requires a human-in-the-loop step.

**Recommendation: R3** — because the commit-message cost of "enable <unnamed-subobject>" is information-free for future blame/investigation, and R3 is a 30-second inspector check that either promotes to R1 (with a meaningful commit message naming the GameObject) or to R2 (with confidence). This specific change is NOT a Phase 4 blocker per se, but leaving an uncommitted scene file across Phase 4a V1 implementation makes the 4a PR diff noisier if Unity re-serializes the scene during playtest (would introduce further incidental overrides).

Do NOT take action on this file; reviewer decides R1 / R2 / R3.

---

End of Addendum C. No file write performed on `Assets/Scenes/RaceMap.unity`. Actions 2 + 3 complete. Awaiting reviewer final 4a V1 green-light.
