# BuddahGo Visual Layer — Strategic Direction (post Phase 7.5)

**Author:** cowork-reviewer (Claude Opus 4.7, harness)
**Date:** 2026-05-03
**Status:** Strategic direction proposal — input to Yonezawa for confirmation. Not yet a contract; informs Phase 7.5 + Phase 7.6 (proposed) + Phase 8.x scope.
**Predecessors:** Phase 7 RECON Surface 7 (CC architecture audit) + Surface 8 (cowork-reviewer industry reference) + Phase 7.5 design framework + Yonezawa Stage 3 framing change ("persistent jitter is real, must match industry-standard")
**Goal:** define the architectural target state for BuddahGo's visual layer + map concrete adds/mods/retires to specific phases

---

## Headline

**BuddahGo's visual layer needs to graduate from "FishNet-defaults + ad-hoc patches" to "first-class architectural concern with explicit logic-vs-render separation, project-owned reconcile correction policy, and designer-tunable game-feel knobs".** Phase 7.5 R7.5-A delivers the tactical fix (~0 LOC config) but the strategic direction beyond is to formalize what's currently implicit — make the visual layer a system with a contract, not a side-effect of the prediction stack's defaults.

---

## 1. Target architectural state (Layer-by-Layer post Phase 7.5+)

The 6-layer model from Surface 7 (Layer 1 fixed tick → Layer 6 hitbox/visual) becomes a contract with explicit ownership:

| Layer | Owner | Source of truth | Contract |
|---|---|---|---|
| L1 fixed tick | FishNet TimeManager | 50 Hz tick rate | Don't fight; physics in FixedUpdate / OnTick callbacks only |
| L2 owner CSP | `BuddahPredictedMotor` (predicted state) | Predicted position evolves from input replay | Already correct (V1-V5). Don't touch. |
| **L3 reconcile correction** | **NEW (currently implicit / FishNet default)** | **`BuddahReconcileCorrectionPolicy`** | **NEW: project-side gate decides snap-vs-smooth-vs-skip per reconcile event** |
| L4 observer interp | FishNet `_graphicalObject` smoother | Smoothed transform on visual root child | Configurable per-object (Adaptive vs Flat) |
| L5 visual smoothing | Same FishNet smoother + (optional) project-side display-position split | Render position lerps to logic position over budget | NEW: explicit `_smoothTime` + `_teleportThreshold` Inspector knobs |
| L6 hitbox/visual desync | **EXPLICIT contract** | rb.position (logic) drives collision; visual lags by ≤2 frames | NEW: documented contract + Inspector-exposed lag budget |

The **bold** rows are the strategic additions. Currently L3 + L5 + L6 are implicit, default-driven, or scattered across files. Direction makes them explicit, testable, designer-tunable.

---

## 2. ADD — what's new in the strategic target

### A1. Project-side reconcile correction policy (NEW class or VisualRootBridge extension)

**File:** likely `Assets/Scripts/New_Buddah/Core/BuddahReconcileCorrectionPolicy.cs` (NEW), or absorbed into `BuddahPredictionVisualRootBridge` extension.

**Job:** sit between motor.cs:559 `_predictionRigidbody.Reconcile()` and the actual rb.position write. Decides per-reconcile-event:
- If `delta < _ignoreThreshold` (e.g., 5cm): **skip** — let prediction continue, don't reconcile this tick. Drops 49 Hz reconcile rate to 5-10 Hz on small drifts.
- If `_ignoreThreshold ≤ delta < _smoothCorrectionThreshold` (e.g., 5-50cm): **smooth-correct** over `_smoothTime` (~100ms) — apply correction to logic position immediately, but render position lerps over budget.
- If `delta ≥ _smoothCorrectionThreshold` (e.g., ≥50cm): **snap** — accept that this is too large to hide, snap visual to logic, accept the visible jump (player perceives "I respawned" or "lag spike resolved").

**LOC:** ~80-150 if new class, ~50-80 if extension to VisualRootBridge.

**Phase:** Phase 7.5-B (fallback) or Phase 7.6 (post Phase 7.5-A regardless of whether 7.5-A is sufficient — formalize the policy even if FishNet's default is "good enough").

### A2. Explicit display-position vs logic-position split (Gambetta pattern)

**File:** extension to `BuddahPredictionVisualRootBridge.cs` OR new sibling.

**Job:**
- `movementRoot.position` = logic position (used for collision, hitbox, reconcile target — authoritative)
- `visualRoot.position` = render position (lerp-toward logic via SmoothDamp over `_smoothTime`, snap if `> _teleportThreshold`)
- This is what FishNet's `_graphicalObject` smoother already does in principle, but having it explicit at project level lets the team tune it independently of FishNet's defaults

**LOC:** ~60-80 if new extension; might be redundant if FishNet smoother is properly configured (R7.5-A path).

**Phase:** Phase 7.6 (only if R7.5-A insufficient) — or accept that FishNet does it for us and document instead of reimplement.

### A3. Designer-facing game-feel knobs (Inspector exposed)

**File:** new SerializeField additions to `BuddahPredictedMotor` config OR `BuddahPredictionVisualRootBridge`.

**Knobs:**
- `_smoothTime` (float seconds, default 0.1, range 0.05-0.3) — catch-up duration
- `_teleportThreshold` (float meters, default 0.75, range 0.5-2.0) — snap-vs-smooth boundary
- `_ignoreThreshold` (float meters, default 0.05, range 0-0.2) — below this, skip reconcile entirely
- `_maxSmoothableErrorPerFrame` (float meters/frame, default 0.30, range 0.10-1.0) — cap per-frame catch-up rate
- `_ownerInterpolationTicks` (int, default 1, range 0-3) — already in FishNet
- `_spectatorInterpolationTicks` (int, default 2, range 1-5) — already in FishNet
- `_adaptiveInterpolation` (bool, default true) — already in FishNet (flat vs adaptive)

**Tooltip + range attribute on each.** Designers can tune game-feel without touching code. Phase 7.5-A might just expose what FishNet has; Phase 7.6 adds the project-side ones.

**LOC:** ~30-50 SerializeField + Range/Tooltip attributes.

**Phase:** Phase 7.5-A (FishNet ones) + Phase 7.6 (project-side ones if A2 lands).

### A4. Permanent ReconcileSnapProbe + VisualShakeProbe (no cleanup post Phase 7)

Currently per Phase 7 design Q4, `BuddahPredictionReconcileSnapProbe.cs` is a Phase-7-SMOKE-only probe gated behind `BUDDAH_PREDICTION_RECONCILE_PROBE` define, removed post-SMOKE per R1.3.

**Strategic direction: keep both probes permanent on dev** alongside `BUDDAH_PREDICTION_VISUAL_PROBE` + `BUDDAH_PREDICTION_PERF_PROBE`. They're zero-cost when define is undefined; enabling them gives game-feel feedback during any session. Make probe-driven `[D-VIS HEARTBEAT]` + `[D-REC HEARTBEAT]` lines part of the standard dev observability suite.

**Phase:** Phase 7.5-A or Phase 7.6 — flip R1.3 cleanup discipline from "remove probe define" to "keep probe define as permanent dev-mode infra; cleanup applies only to ProjectSettings drift unrelated to probe defines".

### A5. Hitbox-visual contract documented + linted

**Files:** code comment in `PushHitbox.cs` declaring "uses logic position by design; visual lag tolerated up to N frames" + Roslyn analyzer rule (Phase 8) catching any code that mixes the two.

**Contract:** `PushHitbox.OverlapBox` uses `box.transform` (which depends on prefab tree). Direction: ensure the box collider attaches under `movementRoot` (logic position), NOT under `visualRoot` (render position). If currently under `visualRoot`, retrofit prefab to move it.

**LOC:** ~5 LOC code comment + Roslyn rule (Phase 8 scope).

**Phase:** Phase 7.6 (prefab inspection + correction) + Phase 8 (lint enforcement).

### A6. Game-feel tuning playbook (designer-facing doc)

**File:** `Docs/game-feel-tuning.md` (NEW).

**Content:** for each knob from A3, document:
- What it does (in plain language, not netcode jargon)
- Symptom when too low (e.g., "_smoothTime too low → visible micro-stutters under reconcile")
- Symptom when too high (e.g., "_smoothTime too high → controls feel laggy")
- Default value rationale + safe tuning range
- Cross-references to L# lessons + Phase 7.5/7.6 verify reports

**Phase:** Phase 7.6 closeout, OR a dedicated documentation phase.

---

## 3. MODIFY — what changes in-place

### M1. Buddah.prefab smoother config (Phase 7.5-A immediate)

Per Surface 7 audit 7.2 + Phase 7.5 framework R7.5-A:

| Field | Current | Target | Why |
|---|---|---|---|
| `_enableTeleport` (line 1082+1117) | 0 | 1 | Restore snap-escape valve at >threshold |
| `_teleportThreshold` (current 1m) | 1 | 0.75 | Align with `PostIntroVisualLockPositionThreshold` |
| `_adaptiveInterpolation` | 3 (already on?) | 3 (confirm) | BuddahGo is casual not competitive |
| `_ownerInterpolation` | 1 | 1 | Unchanged, owner near-instant |
| `_spectatorInterpolation` | 2 | 2 | Unchanged, 2-tick observer buffer |
| Smooth-reconciliation feature toggle | ? | ON | core fix per Surface 8 / FishNet docs |
| `_useGracePeriod` | ? | ON if available | absorbs single-frame outliers |

Phase 7.5-A scope. ~5-10 YAML lines.

### M2. Motor [Reconcile] body (Phase 7.6 if A1 lands)

**Before (motor.cs:559):**
```csharp
if (!skipOwnerIntroReconcile)
    _predictionRigidbody.Reconcile(data.RigidbodyState);
```

**After:**
```csharp
if (!skipOwnerIntroReconcile) {
    var policy = _bootstrap.ReconcileCorrectionPolicy; // injected via Bootstrap
    var decision = policy.Evaluate(rb.position, data.RigidbodyState);
    switch (decision.kind) {
        case ReconcileDecision.Skip:    /* no-op, prediction continues */ break;
        case ReconcileDecision.Smooth:  _predictionRigidbody.Reconcile(data.RigidbodyState); 
                                        _visualRootBridge?.NotifySmoothCorrection(decision.smoothBudget); break;
        case ReconcileDecision.Snap:    _predictionRigidbody.Reconcile(data.RigidbodyState);
                                        _visualRootBridge?.NotifyTeleportSnap(); break;
    }
}
```

**LOC:** +10-15 motor.cs lines + new `BuddahReconcileCorrectionPolicy.cs` class.

**Phase:** Phase 7.6 (post Phase 7.5-A regardless of whether 7.5-A is sufficient — this formalizes the policy as project-owned, makes future tuning safe).

### M3. PostIntroVisualLockPositionThreshold integration (Phase 7.6)

Currently in `BuddahPredictionVisualRootBridge.cs`:
```csharp
PostIntroVisualLockPositionThreshold = 0.75f;
```

Direction: this threshold should be the SAME as `_teleportThreshold` from A3. Currently they're separate constants in separate places. Modify to reference a single source-of-truth:

**Option (a):** Move both into a shared `BuddahVisualBudgetConfig` ScriptableObject. Bridge + smoother both read from it.

**Option (b):** Make `PostIntroVisualLockPositionThreshold` derived from the smoother's `_teleportThreshold` field via dependency injection.

**LOC:** ~20-40 LOC depending on option.

**Phase:** Phase 7.6.

### M4. R1.3 cleanup discipline scope (Phase 7 contract amendment)

Current Phase 7 R1.3 statement (in active contract Design row): "VISUAL_PROBE + RECONCILE_PROBE MUST be removed post-SMOKE".

Strategic direction (per Recon row amendment 2026-05-03 + A4 above): VISUAL_PROBE is permanent dev infra (already on dev). RECONCILE_PROBE should ALSO become permanent dev infra (per A4). So R1.3 cleanup discipline contracts to: "no Phase 7 cleanup needed; both probes are now dev-permanent observability infra".

Already disposed in Recon row amendment (option α), but Design row R1.3 statement should be amended to match in Phase 7's closeout commit.

**Phase:** Phase 7 closeout (PR commit messages note the change).

---

## 4. RETIRE — what gets deprecated/removed

### R1. "Default trust FishNet entirely" reconcile pattern

The current motor.cs:559 pattern (unconditional `_predictionRigidbody.Reconcile()`) is structurally Anti-pattern 4a from Surface 8 (snap reconciliation on every tick at high frequency). When A1 lands, this pattern is replaced by project-side policy.

**Status:** retire when A1 lands (Phase 7.6 if A1 is necessary; might stay if R7.5-A is sufficient).

### R2. Hardcoded thresholds (PostIntroVisualLockPositionThreshold = 0.75f)

Hardcoded constants in source — fine for prototyping, bad for game-feel iteration. Replace with Inspector fields per A3.

**Status:** retire when M3 lands (Phase 7.6).

### R3. R1.3 "remove probe define" cleanup discipline

Current Phase 7 design assumed probes are per-phase ephemeral. Strategic direction makes them permanent. R1.3 retires as cleanup discipline; replaced by "probe defines stay; cleanup gates only catch ProjectSettings drift".

**Status:** retire when Phase 7 closes (per M4 + A4).

### R4. Implicit visual-vs-logic position assumptions

Currently the codebase has `transform.position` reads at multiple places — sometimes meaning logic, sometimes meaning visual, sometimes ambiguous. This is fragile.

**Status:** retire over Phase 7.6 + Phase 8 — every `transform.position` read on Buddah objects must be explicit which it means (`movementRoot.position` vs `visualRoot.position`).

---

## 5. Phase mapping

| Phase | Status | Scope (per this strategy) | Estimated LOC |
|---|---|---|---|
| Phase 7 | in-flight (Stage 4) | measure jitter, characterize root cause | (already scoped) |
| **Phase 7.5-A** | proposed (R7.5-A from framework) | **M1 + A4 partial (keep RECONCILE_PROBE permanent)** | ~5-10 LOC YAML + 0 code |
| **Phase 7.6** | NEW proposal | **A1 + A2 (if needed) + A3 + A5 prefab + M2 + M3 + R1, R2 retire** | ~150-300 LOC + prefab + doc |
| Phase 8.x | already scoped | A5 lint rule (Roslyn) + R4 cleanup + L9 ClampPlanarSpeed + general housekeeping | TBD |
| Phase 6 | already scoped | Teleport + Handoff cut-over (informed by Phase 7+7.5+7.6 visual findings) | TBD |
| (game-feel doc) | optional | A6 tuning playbook | doc-only |

**The new strategic insertion is Phase 7.6** between current Phase 7.5 retrofit and Phase 8 cleanup. Phase 7.6 takes the visual layer from "config tuned but architecture implicit" to "architecture explicit + designer-tunable + future-proof".

---

## 6. Quality gates beyond Phase 7.5

Phase 7.5-A's success criterion: re-run Phase 7 SMOKE, all Q3 thresholds pass, SC.1-SC.3 driver observations clean.

**Strategic quality gates (post Phase 7.6):**

1. **Designer can tune game-feel without code change.** Open Buddah.prefab Inspector, adjust `_smoothTime`, hit play, see difference. No source edit / recompile required for parameter sweeps.
2. **Reconcile policy is greppable + understandable.** `git grep ReconcileCorrectionPolicy` returns the single source of truth; new contributor can read it + understand the logic-vs-render decision tree in 5 minutes.
3. **Probes give continuous game-feel feedback.** Every dev Editor session emits `[D-VIS HEARTBEAT]` + `[D-REC HEARTBEAT]` lines; `Tools/Analysis/AnalyzeJitter.ps1` runs against any session log and produces a tabular game-feel report.
4. **Hitbox-visual contract is enforced.** Roslyn analyzer (Phase 8) catches any new code that reads `transform.position` ambiguously. Existing code is grandfathered + cleaned up over time.
5. **Documented for designers.** `Docs/game-feel-tuning.md` exists; designers reference it for tuning.

---

## 7. What does NOT change (preserved invariants)

To avoid Phase 7.5/7.6 from sprawling:

- **V1-V5 prediction stack correctness:** untouched. Wire format frozen post-V5.
- **FishNet 4.6.20 version:** no upgrade. Strategic direction works within current FishNet API.
- **Server-authority + client-prediction model:** unchanged.
- **Single-host P2P via Steam:** unchanged.
- **Existing PostIntroVisualLockPositionThreshold hysteresis:** preserved (M3 just integrates it with the new threshold instead of having parallel constants).
- **CombatAdapter / ImpulseChannel / other Phase 4b achievements:** untouched.

---

## 8. Open strategic questions for Yonezawa

Decisions needed before committing to this direction. Non-blocking for Phase 7.5-A (which is purely tactical config), but block Phase 7.6 KICKOFF.

### Q-strategy.1 — Phase 7.6 yes/no?

Option (a) **Stop at Phase 7.5-A.** If R7.5-A reduces jitter to user-acceptable, declare done. Don't formalize policy; trust FishNet defaults. **Risk:** future game-feel tuning ad-hoc, scattered, regressive.

Option (b) **Full Phase 7.6.** Formalize architecture per this doc. ~150-300 LOC + prefab + designer doc. **Reward:** future game-feel tuning is designer-doable; new contributors understand the visual layer in minutes.

Option (c) **Skip 7.6, fold relevant pieces into Phase 8.** Bundle A1+A2+A3 retire patterns into Phase 8 cleanup phase. **Risk:** Phase 8 grows to be 3x its current scope; harder to land cleanly.

**Reviewer recommendation:** (b) **Phase 7.6 dedicated**. Visual layer is core game-feel; deserves its own phase. Phase 8 stays focused on its current scope (Roslyn analyzer + L9 ClampPlanarSpeed + general cleanup). Phase 7.6 sits between Phase 7.5 (tactical fix) and Phase 8 (general housekeeping) — neat in the timeline.

### Q-strategy.2 — Designer-tunable knobs ownership?

Game-feel knobs (A3) become Inspector fields. Question: who owns the values long-term?

- (a) Engineering team owns defaults, designers tune in branches before merging.
- (b) Designers own day-to-day, engineering reviews on extreme-value-warn.
- (c) Locked to engineering until game-feel is stable, then handed over.

**Reviewer recommendation:** (b) with `[Range]` + `[Tooltip]` attributes that warn at extremes. Use Phase 7.6 as the handoff moment.

### Q-strategy.3 — How aggressive should reconcile-skip be?

A1's `_ignoreThreshold` (skip reconcile if delta < X) is the most aggressive change. It says: under low drift, **don't bother reconciling**, let prediction run. Industry-standard precedent (Source / Halo / Photon Fusion) is mild — typically 1-5cm threshold. BuddahGo at 80 m/s might want larger (5-15cm).

- (a) Conservative (5cm) — minimal departure from current behavior, only skips truly negligible drift.
- (b) Moderate (10-15cm) — drops 49 Hz reconcile rate to ~10 Hz on no-LatencySim baseline; bigger smoothness gain.
- (c) Aggressive (20cm+) — close to Source's worst-case "noticeable error allowed" budget; max smoothness, slight collision lag risk.

**Reviewer recommendation:** start (b) 10cm in Phase 7.6, tune via `_ignoreThreshold` Inspector knob in subsequent dev cycles. SMOKE re-run validates per knob change.

### Q-strategy.4 — Phase 7.6 timing?

- (a) Immediately after Phase 7.5-A merges. Sequential.
- (b) Parallel to Phase 6 (Teleport + Handoff cut-over). 7.6 affects how Phase 6 visualizes teleport snap; doing them parallel risks coupling.
- (c) After Phase 6 + Phase 8 — wait, then revisit visual layer with full info.

**Reviewer recommendation:** (a) sequential. Phase 7.6's outputs (game-feel knobs, hitbox-visual contract) inform Phase 6 + Phase 8 design. Doing 7.6 first sets the baseline.

### Q-strategy.5 — Allowlist FishNet docs domain?

Currently cowork container can't fetch `fish-networking.gitbook.io`. Adding it to Settings → Capabilities would let cowork-reviewer pull full FishNet API docs for Phase 7.5-A precise field-name verification + Phase 7.6 design Q&A authoring.

- (a) Add to allowlist.
- (b) Keep restriction; CC reads on Windows browser + relays.
- (c) Add temporarily for Phase 7.5/7.6 only, then revert.

**Reviewer recommendation:** (a) — once added, it's reusable for all future FishNet-touching work. No real downside (FishNet docs are read-only, low risk).

---

## 9. Summary — what to commit to

If Yonezawa accepts this direction, the strategic commitments are:

1. **Phase 7.5-A is tactical only** — get the immediate jitter fixed via FishNet config retrofit. Don't expand its scope.
2. **Phase 7.6 is strategic** — formalize the visual layer as an architectural concern. Add A1+A2+A3 (project-side reconcile policy + display-position split if needed + designer knobs). Modify M1+M2+M3. Retire R1+R2.
3. **Phase 8 stays clean** — A5 (lint rule) + R4 (transform.position discipline) + existing scoped Phase 8 work (Roslyn / L9 / cleanup). No additions from this direction.
4. **Probes are permanent infra** — A4 retires R3. RECONCILE_PROBE joins VISUAL_PROBE + PERF_PROBE on dev permanently.
5. **Game-feel becomes designer-tunable** — A3 + A6.
6. **Hitbox-visual contract is explicit + enforced** — A5 + R4.

If accepted, immediate next steps:
1. Yonezawa replies with disposition on Q-strategy.1 through Q-strategy.5.
2. Cowork-reviewer authors `agent-exchange/handoff/2026-05-03-phase7-6-kickoff-draft.md` (if Q1 = b).
3. Phase 7.5-A proceeds as planned (this doc doesn't change Phase 7.5).
4. Once Phase 7.5-A merges, Phase 7.6 KICKOFF stamps + RECON begins.

If rejected or scoped down, Phase 7.5-A still runs as-is; future visual-layer work falls back to ad-hoc per-PR pattern.

---

## 10. Cross-references

- Phase 7 contract: `Docs/phase-gates/active/phase7-contract.md` (Recon row 2026-05-03 amendment captures alignment)
- Phase 7 RECON Surface 7: `agent-exchange/handoff/2026-05-03-phase7-recon.md` Section 7 (CC audit, commit 3d5b328)
- Phase 7 RECON Surface 8: `agent-exchange/handoff/2026-05-03-phase7-recon-surface8-industry-reference.md` (cowork-reviewer industry research)
- Phase 7.5 retrofit framework: `agent-exchange/handoff/2026-05-03-phase7-5-design-framework.md` (R7.5-A through R7.5-D)
- L24 candidate: task #65 — data-attribution self-check at write time, post Phase 7 close
- Methodology: `Docs/phase-gates/methodology.md` Rules 1-12 (no new rules proposed by this direction; potential Rule 13 from L24)
