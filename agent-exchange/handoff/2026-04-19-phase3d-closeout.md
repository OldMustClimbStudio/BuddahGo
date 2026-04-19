# Phase 3d Close-Out Packet — for Claude Code

Date: 2026-04-19
Branch: refactor/prediction-v2
Status: V1 compile clean + V2-postfix host-only PASS + V5 2-peer PASS (both peers).
Ready to commit + push PR; wait for reviewer green-light before merge.

---

## §1 — Final Commit Message

Paste verbatim into commit (`git commit -F agent-exchange/handoff/commit-msg.txt`
or HEREDOC). Long on purpose — captures the 3d architecture decisions
(resolver extract + parity-by-construction), the V2 false-positive
investigation (180° `Quaternion.Angle(default, default)` foot-gun), the
L15 comparator-hygiene lesson, the V5 2-peer gate combination result
(CLIENT exceeded L12 prediction — non-blocking but flagged for Phase 6
planner), and the unchanged deferred-work state (L7 at Phase 4, L12 at
Phase 6, Phase 8 Entries 1+2).

```
Phase 3d — Handoff step shadow + L15 comparator normalize

Adds a parity-verified Euler shadow for the Launch Handoff step of the
tick pipeline. Authority path is untouched. Shadow runs under
BUDDAH_PREDICTION_SHADOW on Standalone + Editor + Development Build only.
Any mismatch on the 15 LaunchHandoffState fields observed after the
post-consume RefreshLaunchState (motor.cs:359) produces a [D-LOC]
warning; 60 consecutive divergences across any category escalate to
[D-LOC FATAL] (phase-agnostic category-list format) and reset the
counter.

=== What this lands ===
- Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchHandoffResolver.cs
  (NEW — pure-static resolver lifted from motor's RefreshLaunchState
  body + AdjustLaunchHandoffForArrivalTick body. Zero Unity / FishNet
  dependencies; takes state by value, takes tickDeltaSeconds from
  caller. Both motor and BuddahHandoffStep call into the same body ->
  parity-by-construction. Per audit §0 and Addendum A: bit-identical
  output on identical inputs under C# / Mono / IL2CPP single-thread).

- Assets/Scripts/New_Buddah/Simulation/BuddahHandoffStep.cs
  (was a Phase 0 stub; fleshed out as the shadow invocation —
  Advance(snapshot), then conditional ProjectForArrivalTick + FromData
  + Advance if the pre-consume pending slot is ripe. Records
  HandoffRan + ShadowLastConsumedHandoffId + ShadowHandoffState on the
  shadow scratch).

- Assets/Scripts/New_Buddah/Core/BuddahPredictionTickContext.cs
  (extended, +3 fields: ShadowPreHandoffHasPending,
  ShadowPreHandoffEvent, ShadowPreHandoffState; three new constructor
  args appended).

- Assets/Scripts/New_Buddah/Simulation/BuddahPredictionShadowScratch.cs
  (extended, +2 live fields: HandoffRan, ShadowHandoffState. The
  Phase 0 ShadowLastConsumedHandoffId field graduates to live usage
  in 3d — its Phase 0 per-event cursor assumption was correct for
  handoff since handoff is genuinely event-queued; DP6 tag retained
  only on ShadowLastConsumedModifierId which remains dead per Phase 3c
  closeout).

- Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs
  (append-only inside `#if (UNITY_EDITOR || DEVELOPMENT_BUILD) &&
  BUDDAH_PREDICTION_SHADOW` gates; plus THREE thin-wrapper refactors
  that preserve behavior exactly — verified in Addendum C):
    * 5 new fields at lines 94-98: _shadowPreHandoffHasPending,
      _shadowPreHandoffEvent, _shadowPreHandoffState,
      _shadowHandoffConsumedCount, _dLocHandoffDivCount.
    * 3 lines appended to the Phase 0+1 snapshot block at lines
      343-345 (captures _hasPendingLaunchHandoffEvent +
      _pendingLaunchHandoffEvent + _handoffState after the
      pre-consume RefreshLaunchState).
    * Real-side mirror at line 373: _realScratch.ShadowHandoffState
      = _handoffState (after the post-consume RefreshLaunchState).
    * BuddahHandoffStep.Run hook + consume-counter increment at
      lines 379-381.
    * BuildTickContext constructor call extended with 3 new named
      args at lines 1265-1267.
    * Shadow_CompareAndReport extended:
        (a) anyRan gate extended with HandoffRan pairs (line 1276).
        (b) heartbeat format extended with hof-div + hof-compared
            (both active and idle variants, lines 1284 + 1298).
        (c) heartbeat reset adds _dLocHandoffDivCount = 0 (lines
            1289 + 1303).
        (d) 15-field handoff compare block (lines 1523-1642) —
            bool IsActive, enum CurrentState, 7 uint tick fields
            exact, BlendAlpha float @ 1e-4, SnapshotPosition /
            SnapshotVelocity / SnapshotAngularVelocity /
            SnapshotForward via magnitude @ 1e-4, SnapshotRotation
            via Quaternion.Angle @ 1e-4 (see L15 fix below).
        (e) hofDiverged aggregated into _dLocConsecutive and
            anyDiverged at lines 1644-1646.
        (f) FATAL message rewritten phase-agnostic at lines 1647-
            1665: `[D-LOC FATAL] T=<tick> shadow formula divergence:
            {<categories>}` where <categories> is a comma-separated
            list of active-this-window category keys (loc|imp|tel|
            mod|hof). Easier to maintain across future shadow phases
            without per-phase text churn.
    * ConsumePendingLaunchHandoffEvent at motor.cs:1892-1907 — calls
      Resolver.ProjectForArrivalTick (new extract), sets
      _realScratch.HandoffRan + _realScratch.ShadowLastConsumedHandoffId
      after FromData.
    * RefreshLaunchState thin wrapper at motor.cs:1942-1952 — calls
      Resolver.Advance, emits the same verbose state-transition log
      at the same conditions as the pre-refactor body (behavior-
      neutrality diff in Addendum C of the audit).
    * AdjustLaunchHandoffForArrivalTick removed (inlined at the
      single caller motor.cs:1878-1890 with preserved verbose log).
      GetElapsedSeconds + ProjectRotationForward removed (dead after
      inline).

- Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs (L15
  comparator normalize, non-3d but bundled):
    * Lines 1599-1617 — SnapshotRotation comparison now normalizes
      default(Quaternion)=(0,0,0,0) to Quaternion.identity before
      Quaternion.Angle. Root cause: Quaternion.Angle(default, default)
      returns 180° (dot=0, 2*acos(0)=180°) producing false-positive
      divergence when both sides hold the uninitialized quaternion
      (typical pre-consume steady state with IsActive=false). V2
      host-only pre-fix run produced 4550 false-positive warnings +
      74 FATALs. V2-postfix + V5 2-peer both show 0 FATALs + 0
      non-heartbeat [D-LOC] across combined ~130s. See lessons-log
      L15.

- Docs/lessons-log.md
  (L15 added, newest-first — shadow comparators must defend against
  `Compare(default, default) != equals` foot-guns. Rules (a)-(d) cover
  the normalize pattern, anti-gating guidance, audit mandate, and
  generalization beyond Quaternion).

- Docs/prediction-refactor-plan/phase-6-prerequisites.md
  (extended with Phase 3d V5 observation: CLIENT reached hof-compared=1
  unexpectedly, exceeding L12 silent-drop prediction. L12 scope
  unchanged — silent-drop analysis stands until reliably reproducible.
  Flag for Phase 6 planner: "L12 may be intermittent in practice,
  confirm reproducibility before Phase 6 fix sign-off").

- agent-exchange/handoff/phase-8-cleanup-queue.md
  (Entry 2 appended — Phase 3b teleport rotation compare normalization.
  Structurally vulnerable to the same Quaternion.Angle default foot-gun
  but gate at motor.cs:1361 requires TeleportRan=true which makes
  real-session occurrence improbable. Deferred, not blocked).

- agent-exchange/handoff/2026-04-19-phase3d-audit.md
  (pre-execution audit + Addenda A (DP-8 resolver extract Q1/Q2/Q3 +
  recommendation), B (dummy-read pattern at motor.cs:493-501
  classification), C (post-V2-FAIL three-column diff proving DP-8
  refactor is behavior-neutral)).

=== Validation (parity-by-call-site-invariant gate model, 3a/3b/3c/3d
    consistent) ===

V1 compile + editor load:
  PASS — assets-refresh + editor state poll (IsCompiling=false) +
  console-get-logs Error=[] — zero errors, zero warnings.

V2 host-only initial run (PRE-L15):
  FAIL — 4550 [D-LOC] SnapshotRotation warnings (all 180° fixed delta)
  + 74 [D-LOC FATAL] emissions across ~10-20s window. All 14 other
  handoff fields bit-identical; 3a/3b/3c parity preserved. Root cause
  localized to Quaternion.Angle(default, default)=180° foot-gun via
  per-field Editor.log scrape (MCP cache evicted per-field warnings at
  500-cap — fell back to raw Editor.log).
  Diagnostic digest: agent-exchange/console/2026-04-19-phase3d-v2-rerun.log

V2 host-only (POST-L15):
  PASS — T=942..T=3462, 22 heartbeats, hof-div=0 sustained, hof-compared
  reached 1 at T=3222, 3a/3b/3c parity preserved, 0 [D-LOC FATAL], 0
  non-heartbeat [D-LOC]. mod-compared grew 1→2521 linearly. L15
  normalize validated.
  Digest: agent-exchange/console/2026-04-19-phase3d-v2-postfix.log

V5 2-peer 60s:
  PASS — both peers cleared all 9 gate criteria (L13 per-peer
  combination).
    HOST (Build, Player.log): T=2573..9533, 59 unique heartbeat ticks,
      mod-compared == active-ticks 1:1 (matches L13 HOST baseline),
      hof-compared=1 at T=4973 holding through end (58 consecutive
      rows on =1 side — single handoff consume during session),
      imp-compared grew 0→8, tel-compared=0 (no teleport events),
      all divs=0, 0 FATAL, 0 non-heartbeat [D-LOC].
    CLIENT (Editor, Editor.log): T=1089..7089, 51 heartbeats,
      hof-compared transitioned 0→1 at T=3489 (EXCEEDED L12 prediction
      — see cross-peer note below), mod-compared / active-ticks =
      6.86:1 (reconcile-replay amplification, lower than L13 baseline
      ~19:1 but still >>1:1), imp-compared grew 0→5, tel-compared=0,
      all divs=0, 0 FATAL, 0 non-heartbeat [D-LOC].
  Digest: agent-exchange/console/2026-04-19-phase3d-v5.log

=== Cross-peer observation (flagged for Phase 6 planning) ===

V5 CLIENT reached hof-compared=1 starting T=3489 and held through
session end (30 heartbeats, all hof-div=0). This EXCEEDS L12's
silent-drop prediction for CLIENT-side handoff coverage (predicted
hof-compared=0). Candidate explanations:
  (a) L12 silent-drop may be INTERMITTENT for handoff, not
      deterministic — TargetRpc return-leg may occasionally land.
  (b) Reconcile-replay may deliver handoff payload via serialized
      state fields (partial L12 relief through the reconcile channel).
  (c) Random tick-race alignment that happened to land favorably.

L12 scope UNCHANGED — silent-drop analysis stands until reliably
reproducible. Phase 6 planner must confirm reproducibility before
fix sign-off. Appended to phase-6-prerequisites.md Prereq-1.

=== Reconcile-replay ratio (architecture diagnostic) ===

The HOST 1:1 vs CLIENT ~7-19:1 mod-compared/active-ticks ratio is
the FishNet reconcile-replay signature confirmed in Phase 3c. CLIENT
ratio 6.86 in V5 is below the Phase 3c baseline (~19) but still well
above 1:1 — all divs=0 across every replay, so shadow is deterministic
under replay amplification. The drop may reflect lighter reconcile
pressure in this 60s segment vs Phase 3c's reference capture. Flagged
as monitoring item (V5 digest §7 anomaly 1), non-blocking.

=== Carry-over state (unchanged from Phase 3c/3d) ===

- Phase 4 (first concrete adapter cut-over): Blocked on L7
  (Switcher double-fire ApplyMode). See phase-4-prerequisites.md
  Prereq-1. Unchanged.

- Phase 6 (teleport + handoff cut-over — scope expanded in 3d audit):
  Blocked on L12 for BOTH teleport path (motor.cs:886-931) AND
  handoff path (motor.cs:768-817 — confirmed structurally identical
  to teleport in Phase 3d audit §3). V5 bonus-coverage observation
  noted above. Unified fix shape Option A vs B decision pending
  (phase-6-prerequisites.md §"Fix Shape Options").

- Phase 8 (cleanup):
  * Entry 1 — serializer precision audit for ModifierState vs
    ComputedStats (unchanged from 3c).
  * Entry 2 — 3b teleport rotation compare normalization (added in
    3d per L15 Rule (c)). Deferred, not blocked.
  * Entry-candidate — remove ShadowLastConsumedModifierId dead field
    (unchanged from 3c; DP6 tag retained).

=== References ===

- V2-postfix PASS digest: agent-exchange/console/2026-04-19-phase3d-v2-postfix.log
- V5 2-peer PASS digest: agent-exchange/console/2026-04-19-phase3d-v5.log
- Phase 3d pre-execution audit + Addenda A/B/C: agent-exchange/handoff/2026-04-19-phase3d-audit.md
- Lessons-log L15 (Quaternion default foot-gun): Docs/lessons-log.md
- Phase 8 cleanup-queue Entry 2: agent-exchange/handoff/phase-8-cleanup-queue.md
- Phase 6 prereq V5 observation: Docs/prediction-refactor-plan/phase-6-prerequisites.md

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## §2 — Pre-PR Checklist

- [x] Compile clean (V1 — assets-refresh + editor state poll, 0 errors)
- [x] V2-postfix host-only PASS (hof-div=0, hof-compared=1,
      0 non-hb [D-LOC], 0 FATAL)
- [x] V5 2-peer HOST PASS (ratio 1:1, 59 unique ticks, hof-compared=1
      from T=4973, imp-compared grew 0→8, all divs=0, 0 non-hb
      [D-LOC], 0 FATAL)
- [x] V5 2-peer CLIENT PASS (ratio 6.86:1 reconcile-replay, 51
      heartbeats, hof-compared=1 from T=3489 exceeded L12 prediction,
      imp-compared grew 0→5, all divs=0, 0 non-hb [D-LOC], 0 FATAL)
- [x] V1 compile clean post-L15 normalize addition (0 errors)
- [x] Motor protected-region untouched outside `#if`-gated append-only
      shadow scope + three behavior-neutral thin-wrapper refactors
      (see Appendix A below for exact lines + Addendum C of audit for
      three-column diff)
- [x] L15 foot-gun rule documented (lessons-log.md)
- [x] Phase 8 Entry 2 registered (3b teleport rotation compare
      normalize per L15 Rule (c))
- [x] Phase 6 prereq V5 bonus-coverage observation appended (L12 may
      be intermittent — confirm reproducibility before fix sign-off)
- [x] L13 compared>0 rule satisfied on BOTH peers in V5
      (HOST hof-compared=1, CLIENT hof-compared=1)
- [x] Append-only / observation-only mode preserved (no adapter code
      written, no motor authority-path edit; the three Resolver-call
      thin wrappers at motor.cs:1878-1890 and motor.cs:1942-1952 and
      ConsumePendingLaunchHandoffEvent Resolver call at motor.cs:1892
      are behavior-neutral)
- [x] Parity-by-call-site-invariant gate model maintained (shadow
      step has no internal gate; motor owns skip-gating at RunInputs
      entry) — consistent with 3a/3b/3c
- [ ] PR opened — to be done as part of this closeout action
- [ ] Reviewer green-light → merge → Task #23 completed → advance to
      Task #24 (Phase 4 review)

---

## §3 — Phase 4 starter (first adapter cut-over)

Phase 3 series (3a locomotion + 3b impulse+teleport + 3c modifier +
3d handoff) is **COMPLETE**. Every motor step now has an observation-
only Euler shadow with parity-by-call-site-invariant gate model. Next
up is Phase 4 — the first concrete adapter cut-over.

Phase 4 is **BLOCKED on L7** (BuddahMovementModeSwitcher double-fire
ApplyMode). Prereq spec:
`Docs/prediction-refactor-plan/phase-4-prerequisites.md` Prereq-1.
Recommended fix: **Option B** — adapter-side defer-until-first-replicate
via `Initialize()` late-bind hook. Acceptance criterion: V8-style
re-run produces 2 `[CommandBus]:ClearAll` lines per Buddah spawn
(not 4), OR adapter-internal probe proves pending-enqueue survives
the Switcher double-fire window.

No pre-execution audit is needed for Phase 4 — L7 fix is well-scoped.
Entry point: apply Option B, re-run the V8 reproducer, confirm
compared>0 on the first-tick adapter enqueue path.

No 3e phase exists — Phase 3 closed with 3d.

---

## §4 — Carry-over reminders

Do not let these fall off the map between phases.

### Phase 4 (first concrete adapter cut-over)
- **Blocked on L7** (Switcher double-fire ApplyMode). See
  [phase-4-prerequisites.md](../../Docs/prediction-refactor-plan/phase-4-prerequisites.md)
  Prereq-1. Recommended fix: Option B — adapter defers enqueue until
  first `[Replicate]` tick via an `Initialize()` late-bind hook.

### Phase 6 (teleport + handoff cut-over)
- **Blocked on L12** for BOTH categories — teleport
  (`motor.cs:886-931`) AND handoff (`motor.cs:768-817`, confirmed
  structurally identical by Phase 3d audit §3). Unified fix shape —
  Option A (result-TargetRpc + commit-gate) vs Option B (reconcile-
  delivery) — decision pending at Phase 6 design time. See
  [phase-6-prerequisites.md](../../Docs/prediction-refactor-plan/phase-6-prerequisites.md)
  `Fix Shape Options` section.
- **Phase 3d V5 cross-peer observation**: CLIENT hof-compared=1
  exceeded L12 prediction. L12 scope unchanged — but Phase 6
  planner must confirm reproducibility (is L12 silent-drop
  intermittent, deterministic, or reconcile-relieved?) before
  sign-off. See phase-6-prerequisites.md "Observation (2026-04-19,
  Phase 3d V5)" note.
- **Acceptance criterion (per category, per L12 Rule (a))**:
  teleport: client-initiated fall-respawn produces `tel-compared >= 1`
  on CLIENT. Handoff: client-initiated intro-to-gameplay transition
  produces `hof-compared >= 1` on CLIENT. V5 saw CLIENT
  hof-compared=1 unprompted — this is the first accidental evidence
  in either direction.

### Phase 8 (cleanup)
- **Entry 1**: serializer precision audit for ModifierState vs
  ComputedStats (unchanged from 3c). Phase 3c V5 CLIENT ratio
  passed this watchpoint OBSERVATIONALLY; Phase 8 must deliver
  audit-level evidence.
- **Entry 2** (NEW in 3d): Phase 3b teleport rotation compare
  normalization. `motor.cs:1376` `Quaternion.Angle(default, default)`
  is structurally vulnerable to the same 180° foot-gun L15 defused
  for 3d. Gate at `motor.cs:1361` (`TeleportRan=true`) makes real-
  session occurrence improbable; deferred, not blocked. See
  [phase-8-cleanup-queue.md](phase-8-cleanup-queue.md) Entry 2.
- **Entry-candidate (add when touching the area)**: remove
  `ShadowLastConsumedModifierId` dead field (DP6 tag present since
  Phase 3c). Unchanged.

### Architecture observation (non-blocking)
- **Reconcile-replay ratio drift**: V5 CLIENT ratio 6.86:1 (vs Phase
  3c baseline ~19:1). Both ratios produce div=0 on every row. Drop
  is flagged as V5 digest §7 anomaly 1. Worth monitoring across
  future V5 runs — if ratio trends further toward 1:1 on CLIENT,
  investigate reconcile-interval / replay-tick-count config changes.
- **L15 comparator hygiene**: any future shadow compare using
  `Quaternion.Angle` / `Quaternion.Dot` / `Quaternion.Lerp` /
  custom `Distance(Color,Color)` etc. MUST defend against
  `Compare(default, default) != equals` per L15 Rule (d). Phase 8
  Entry 2 is one instance; future shadow phases should pre-audit.

---

## Appendix A — Protected-file disclosure (BuddahPredictedMotor.cs)

All additions are inside `#if (UNITY_EDITOR || DEVELOPMENT_BUILD) &&
BUDDAH_PREDICTION_SHADOW` gates UNLESS otherwise noted. The three
thin-wrapper refactors (RefreshLaunchState, inlined
AdjustLaunchHandoffForArrivalTick, ProjectForArrivalTick callsite)
are NOT gated and DO modify protected-region code — but are proven
behavior-neutral in Addendum C of the audit (three-column diff,
line-by-line equivalence table). No authority-path edit. Append-only
in observation scope per L6; behavior-preserving refactor per audit
Addendum C.

| Lines            | Scope                                                                  |
|------------------|-------------------------------------------------------------------------|
| 90-98            | 5 field declarations + 3-line group comment (shadow-gated)             |
| 343-345          | Pre-consume snapshot for handoff (shadow-gated, appended to 3b block)  |
| 371-373          | Real-side handoff mirror comment + `_realScratch.ShadowHandoffState` assign (shadow-gated) |
| 379-381          | BuddahHandoffStep.Run hook + consume-counter increment (shadow-gated)  |
| 1265-1267        | BuildTickContext: 3 new named args (shadow-gated call)                 |
| 1276             | anyRan gate extended with HandoffRan pair                              |
| 1284             | Idle heartbeat Debug.Log: hof-div + hof-compared added                 |
| 1289             | Idle heartbeat reset: `_dLocHandoffDivCount = 0`                       |
| 1298             | Active heartbeat Debug.Log: hof-div + hof-compared added               |
| 1303             | Active heartbeat reset: `_dLocHandoffDivCount = 0`                    |
| 1310             | `bool hofDiverged = false;` local in Shadow_CompareAndReport           |
| 1523-1642        | 15-field handoff compare block (2 flag/enum exact, 7 uint exact, BlendAlpha @ 1e-4, 4 Vector3 magnitude @ 1e-4, SnapshotRotation Quaternion.Angle @ 1e-4 **with L15 zero-normalize at 1599-1617**) |
| 1644             | `if (hofDiverged) _dLocHandoffDivCount++;`                             |
| 1646             | anyDiverged aggregation extended with `hofDiverged`                    |
| 1647-1665        | FATAL message rewritten phase-agnostic (category-list format)          |
| 1878-1890        | `ConsumePendingLaunchHandoffEvent`: inlined AdjustLaunchHandoffForArrivalTick → Resolver.ProjectForArrivalTick call + preserved stale-adjusted verbose log (behavior-neutral refactor, **NOT shadow-gated**) |
| 1892             | `_handoffState = FromData(eventData)` — unchanged semantics            |
| 1906-1907        | `_realScratch.HandoffRan = true; _realScratch.ShadowLastConsumedHandoffId = eventData.EventId;` (shadow-gated) |
| 1942-1952        | `RefreshLaunchState` thin-wrapper over Resolver.Advance with preserved verbose transition log (behavior-neutral refactor, **NOT shadow-gated**) |

**Removed** (behavior-neutral, inlined at callsites or redundant):
- `AdjustLaunchHandoffForArrivalTick` — entire method body (pre-3d
  motor.cs:1764-1790). Inlined at motor.cs:1878-1890 as a
  `Resolver.ProjectForArrivalTick(...)` call with the preserved
  stale-adjusted verbose log gated on `StartTick != preAdjustStartTick`.
- `GetElapsedSeconds` — dead after AdjustLaunchHandoffForArrivalTick
  removal.
- `ProjectRotationForward` — dead after AdjustLaunchHandoffForArrivalTick
  removal.

Audit verification command (for future review):
```
grep -nE 'Phase 3d|_shadowPreHandoffHasPending|_shadowPreHandoffEvent|_shadowPreHandoffState|_shadowHandoffConsumedCount|_dLocHandoffDivCount|BuddahHandoffStep\.Run|BuddahPredictedLaunchHandoffResolver|hof-div|hof-compared|hofDiverged|ShadowHandoffState|HandoffRan|ShadowLastConsumedHandoffId' Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs
```

---

End of packet. Awaiting user green-light after PR lands, before
`git merge`. Task #23 (Phase 3 review) → completed after merge;
advance to Task #24 (Phase 4 review).
