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
