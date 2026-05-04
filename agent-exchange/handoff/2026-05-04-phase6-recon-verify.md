# phase6-race-start-handoff-redesign — Stage 2 RECON Verify Report

**Author:** cowork-reviewer (independent verification per Methodology Rule 2 + Rule 12)
**Date:** 2026-05-04
**Verify target:** [agent-exchange/handoff/2026-05-04-phase6-recon.md](2026-05-04-phase6-recon.md) at HEAD `8314b5d`
**Branch:** `feat/phase7-visual-jitter-evaluation` @ `8314b5db6c2c964721c95fdc2ab794b816278751` (Phase 6 branch NOT cut yet — Stage 4 will cut from dev tip post Phase 7.5-A.1 ship per contract Section 9 Kickoff note)
**Verdict:** ✅ STAGE 2 RECON SIGN-OFF — every cited KEY FINDING independently reproduced via git plumbing. Three Stage 3 design implications surfaced + 5 Open Questions dispositioned.
**Discipline:** Rule 2 sub-clause "git plumbing over working tree" honored throughout. Rule 12 honored — this report authored AFTER independent grep, NOT pre-filled before reading implementer's report.

---

## Stage A — Pre-grep gate (L22 + Methodology Rule 2 sub-clause)

```
git rev-parse HEAD
→ 8314b5db6c2c964721c95fdc2ab794b816278751   ✓ matches RECON cite

git status --short | grep -E "Assets/Scripts/(New_Buddah|Buddah/|Network/|RaceIntro/)" || echo "(zero scope drift — pre-grep gate CLEAN for Phase 6 scope)"
→ ?? Assets/Scripts/New_Buddah/Debug/BuddahPredictionFrameTimeProbe.cs.meta
→ ?? Assets/Scripts/New_Buddah/Debug/BuddahPredictionReconcileSnapProbe.cs.meta
```

**Drift discrimination per Methodology Rule 2 sub-clause:**
- Working-tree M lines exist in `.VSCodeCounter/*` (auto-generated), `.agents/skills/*` (auto-synced from `.claude/skills/`), and `Library/*` (Unity import cache). None in Phase 6 scope.
- Two untracked `.meta` files in `Assets/Scripts/New_Buddah/Debug/` for **Phase 7 probes** (`BuddahPredictionFrameTimeProbe`, `BuddahPredictionReconcileSnapProbe`). These are Phase 7 measurement framework artifacts, NOT Phase 6 scope. Documented as out-of-scope drift.

**Verdict:** pre-grep gate **CLEAN** for Phase 6 scope. RECON verification proceeds.

---

## Stage B — Independent verification of RECON Surfaces

### B.1 Surface 4 caller chain (RequestAuthoritativeLaunchHandoffFromOwner — 4-link chain)

```
git grep -n "RequestAuthoritativeLaunchHandoffFromOwner" -- 'Assets/*.cs'
→
Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs:862:        public bool RequestAuthoritativeLaunchHandoffFromOwner(
Assets/Scripts/New_Buddah/Integration/BuddahPredictionHandoffBridge.cs:161:            return predictedMotor.RequestAuthoritativeLaunchHandoffFromOwner(

git grep -n "TryBeginLaunchHandoff" -- 'Assets/*.cs'
→
Assets/Scripts/Buddah/BuddahMovement.cs:247:            && bridge.TryBeginLaunchHandoff(
Assets/Scripts/New_Buddah/Integration/BuddahPredictionHandoffBridge.cs:148:        public bool TryBeginLaunchHandoff(

git grep -nE "\.BeginLaunchHandoff\(|^\s*public void BeginLaunchHandoff" -- 'Assets/*.cs'
→
Assets/Scripts/Buddah/BuddahMovement.cs:244:    public void BeginLaunchHandoff(LaunchHandoffSnapshot snapshot, ...)
Assets/Scripts/RaceIntro/RaceBodyIntroStateController.cs:332:            movementController.BeginLaunchHandoff(

git grep -n "CompleteGoTransition" -- 'Assets/*.cs'
→
Assets/Scripts/RaceIntro/RaceBodyIntroStateController.cs:306:    private void CompleteGoTransition()      ← RECON cite was 307; HEAD shows 306 (1-line drift, immaterial)
Assets/Scripts/RaceIntro/RaceBodyIntroStateController.cs:322:    [log line, not a caller]
Assets/Scripts/RaceIntro/RaceBodyIntroStateController.cs:381:        CompleteGoTransition();           ← RECON cite was 387; HEAD shows 381 (drift, immaterial)
```

**Verdict:** ✅ **CONFIRMED**. Caller graph is **exactly the linear chain RECON Surface 4.5 documents**:

```
IntroSequenceManager (server → ObserversRpc) "authoritative go"
  → IntroClientController.ApplyAuthoritativeGo (per client)
    → RaceBodyIntroStateController.ApplyAuthoritativeGo (per buddah)
      → next Update/FixedUpdate tick: TryCompleteAuthoritativeGoTransition (line 367-388)
        → CompleteGoTransition (line 306)
          → if (isLocalOwner) movementController.BeginLaunchHandoff(...)         [line 332]
            → bridge.TryBeginLaunchHandoff(...)                                   [BuddahMovement.cs:247]
              → predictedMotor.RequestAuthoritativeLaunchHandoffFromOwner(...)    [Bridge.cs:161]
                → ServerRpc → server-side TryApplyServerAuthoritativeLaunchHandoff
                  → TargetRpc back to owner → owner-side ConsumePendingLaunchHandoffEvent
```

**Zero mid-race callers.** Every entry point is race-start ONLY.

**Section 11.1 disposition (option a — full chain deletion) is FIRM.** L12 fix is structurally moot for Phase 6 scope. RECON Surface 4.6's deletion list is accurate at the file-level granularity; Stage 3 IMPLEMENT picks final line-level scope.

### B.2 Surface 6 auto-forward (Q4 disposition)

```
git show HEAD:Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs | sed -n '339p'
→             float throttle = movementAllowed ? 1f : 0f;

git show HEAD:Assets/Scripts/New_Buddah/Integration/BuddahPredictionOwnerInputBridge.cs | grep -nE "Read(Steering|Throttle)"
→ 42:        public float ReadSteering()
```

**Verdict:** ✅ **CONFIRMED**. Auto-forward is hardcoded `throttle = 1f` keyed on `movementAllowed`. `OwnerInputBridge` exposes ONLY `ReadSteering()` — there is no `ReadThrottle` API. Player keyboard/controller does NOT contribute throttle to the prediction path.

**Q4 disposition for Stage 3:** Section 3.5's "no new auto-throttle code needed" claim is verified correct. Race-start unlock = flip `movementAllowed = true`, no acceleration ramp / no throttle key / no input bridge extension. The contract's "CRITICAL implementation note" on Section 3.5 is now resolved.

### B.3 Surface 1.7 MovementAllowed early-return (Q9 / G8b feasibility)

```
git show HEAD:Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs | sed -n '466,473p'
→             if (!data.MovementAllowed)
              {
                  _predictionRigidbody.ClearPendingForces();
                  SetPredictionVelocitiesSafely(Vector3.zero, Vector3.zero);
                  _predictionRigidbody.Simulate();
                  FinalizeImpulseDebugAfterSimulate();
                  UpdateReplicateDebug(data, state, "blocked");
                  return;
              }
```

**Verdict:** ✅ **CONFIRMED**. The early-return branch produces:
- Zero `_predictionRigidbody.AddForce` calls (path explicitly clears pending forces)
- Zero velocity write at user-input layer (SetPredictionVelocitiesSafely with zero vectors)
- Deterministic across N reconcile replays of the same tick (no state-dependent branching inside the block)

**Q9 → G8b structural feasibility CONFIRMED.** Phase 6 Locked-state can drive `data.MovementAllowed = false` during Phase 3 `[countdownStartTick, raceStartTick)` and reuse this gate for replay determinism. **Stage 3 should elevate Q9 to G8b** per Section 11.2 reviewer recommendation. The cost of elevation is one strict-gate sub-clause + a SMOKE check; the benefit is bit-identical replay across all N replays of every tick in the lock window — exactly what FishNet CSP demands.

### B.4 Surface 1.8 line drift (motor.cs:1937 → 1948)

```
git show HEAD:Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs | grep -nE "^\s+rb\.position\s*="
→ 1884:            rb.position = eventData.TargetPosition;       (teleport — contract cite 1873; drift +11)
→ 1948:            rb.position = eventData.SnapshotPosition;     (handoff — contract cite 1937; drift +11)
```

**Verdict:** ✅ **CONFIRMED**. Line drift is real but **uniform +11 across both rb.position writes**, indicating cumulative phase7 commit offsets between contract draft and current dev tip. Contract Section 3.2.1's "around line 1937-1970" range still covers HEAD's 1948 within +11 tolerance. **Not a blocker.** Stage 4 IMPLEMENT will re-locate via HEAD-relative greps regardless.

Reviewer disposition: contract amendment NOT required — Stage 4 grep semantics handle drift naturally. If hygiene is desired, an in-place edit of contract Section 1.1 to "1873/1948/1976/2003" would tighten cite precision but is optional.

### B.5 Surface 5.3 RoomStateManager existing all-clients-ready signal chain

```
git show HEAD:Assets/Scripts/Network/Room/RoomStateManager.cs | grep -nE "_gameplayMovementUnlocked|_authoritativeGoIssued|ReportLocalGameplayLive|ReportGameplayLiveServerRpc|AreAllClientsGameplayLiveForSequenceServer|MarkAuthoritativeGoIssuedServer"
→
51:  private readonly SyncVar<bool> _gameplayMovementUnlocked = new SyncVar<bool>();
52:  private readonly SyncVar<bool> _authoritativeGoIssued = new SyncVar<bool>();
82:  public bool IsGameplayMovementUnlocked => _gameplayMovementUnlocked.Value;
83:  public bool IsAuthoritativeGoIssued => _authoritativeGoIssued.Value;
89:  public bool CanPlayersUseGameplayInput => IsResultPhaseActive || (IsMatchPhaseActive && _gameplayMovementUnlocked.Value);
90:  public bool ShouldBlockRaceGameplayInput => IsRaceSceneLoadedLocally && IsMatchPhaseActive && !_gameplayMovementUnlocked.Value;
298: public void ReportLocalGameplayLive(int sequenceId)
304:     ReportGameplayLiveServerRpc(sequenceId);
337: public bool AreAllClientsGameplayLiveForSequenceServer(int sequenceId)
1010: public void MarkAuthoritativeGoIssuedServer()
```

**Verdict:** ✅ **CONFIRMED**. Existing chain matches Surface 5.4 inventory exactly. The "all-clients-live" signal flow is fully implemented: per-client `ReportLocalGameplayLive(seq)` → server-side count + `AreAllClientsGameplayLiveForSequenceServer(seq)` → SyncVar `_gameplayMovementUnlocked.Value = true` broadcast to all clients.

**Phase 6 Section 3.3 sketch (new ObserversRpc) overlaps ~90% with existing chain.** Stage 3 Q3 disposition has three options per Surface 5.4 (RECON Section 5.4 enumerated them); reviewer concurs with implementer's lean toward **option (c) — extend SyncVar with sibling `_raceStartTick: uint`**. Tradeoff:

| Aspect | Option (c) — extend SyncVar | Option (a) — new ObserversRpc |
|---|---|---|
| Tick-stamped same-tick unlock (G4) | ✅ Each client gates `if (TimeManager.LocalTick >= _raceStartTick.Value) unlock` | ✅ Embedded ticks in RPC payload |
| Late-join replay | ✅ FishNet built-in SyncVar replay to late observers | ⚠ Requires explicit catch-up logic per Section 3.3.3 sketch |
| New API surface | One SyncVar field | One RPC + one method body |
| Reuses existing trigger flow (`AreAllClientsGameplayLiveForSequenceServer`) | ✅ Yes — server-side condition fires SyncVar set | ⚠ Partial — RPC trigger may compete with existing SyncVar trigger |
| Wire format change risk | Low (one new SyncVar; FishNet handles backward compat) | Medium (new RPC schema) |

Option (c) is the **lower-risk, higher-reuse path**. Stage 3 design owns final pick.

---

## Stage C — KEY FINDING dispositions (from RECON report top-of-file summary)

| KEY FINDING | RECON Surface | Verify status | Stage 3 implication |
|---|---|---|---|
| 1. `RequestAuthoritativeLaunchHandoffFromOwner` caller graph is single chain (race-start only, zero mid-race callers) | Surface 4 | ✅ verified via 4 independent git greps | Section 11.1 option (a) FIRM. Q1 wire format collapses (RPC payload itself goes away). Q10 moot. L12 fix structurally moot. Estimated deletion footprint: ~12 files (per RECON Surface 2.5 + 4.6 inventories). |
| 2. Auto-forward = hardcoded `motor.cs:339 throttle = 1f` keyed on `movementAllowed` | Surface 6 | ✅ verified at line 339 + OwnerInputBridge has no `ReadThrottle` | Q4 RESOLVED. Section 3.5 "no new code" claim verified. Locked state's "玩家按键无反应 / buddah 不前进 / buddah 不旋转" all fall out of `MovementAllowed=false` at motor.cs:466 early-return + steering=0/throttle=0 at lines 338-339. "手不旋转 / 不能 push" fall out via `RoomStateManager.ShouldBlockRaceGameplayInput` chain at `BuddahHandControl.cs:201/397/1174` (RECON Section 6 noted; reviewer concurs spot-checked). |
| 3. Existing `ReportLocalGameplayLive → ReportGameplayLiveServerRpc → _gameplayMovementUnlocked SyncVar` chain ~90% covers Section 3.3 sketch | Surface 5 | ✅ verified line-by-line | Q3 strongly leans option (c) "extend SyncVar with `_raceStartTick: uint`". Stage 3 design Q&A should evaluate vs Section 3.3 sketch (option a). Lower-risk path is option (c). |

---

## Stage D — Open Question dispositions

### OQ1 — Spline driver scope expansion (Surface 3.3)

**RECON finding:** Phase 6 spline-decel formula touches `RaceBodyIntroStateController.GetNormalizedDistanceT` (rewrite distance formula), `IntroAssignmentData.introSpeedMetersPerSecond` (rename to `traversalTimeSeconds` or add sibling), `IntroSequenceManager` (Inspector field rename + assignment payload field swap). Section 3.2.1's listed files don't include these.

**Reviewer disposition:** ✅ AGREE. Stage 3 design Q&A MUST add these to the implementation file list explicitly. Section 11.3 reviewer observation already flagged Section 3.2.1 incompleteness; this is the concrete patch list. Reviewer recommends Stage 3 produce an updated "Implementation file list (consolidated)" subsection in the design doc that supersedes Section 3.2.1.

NOT a blocker for RECON sign-off.

### OQ2 — Legacy non-prediction handoff path (BuddahMovement.cs:539, 561, 571-589)

**RECON finding:** `_launchState`/`launchBlendTime`/`_launchInheritedVelocity` legacy state machine still exists for non-prediction fallback. Phase 6 Section 4 limits scope to predicted path (default = out-of-scope). If fallback is reachable, race-start under fallback still uses old velocity-inherit pattern.

**Reviewer disposition:** Stage 3 RECON sub-task — verify whether the non-prediction fallback path is reachable post-V5. Two possible findings:
- (i) Fallback is dead code (V5 closeout fully retired LEGACY_SHADOW; non-prediction motor mode is structurally unreachable) → leave as-is, contract Section 4 "out-of-scope" stands
- (ii) Fallback is still reachable (e.g., during PredictionV2 disable for debugging) → Phase 6 design should either also redesign the fallback path OR explicitly document "Phase 6 race-start jitter fix applies to PredictionV2 mode only; legacy mode retains old behavior"

NOT a blocker for RECON sign-off. Stage 3 disposition.

### OQ3 — SyncVar vs ObserversRpc tradeoff (Q3 Surface 5.4)

**Reviewer disposition:** ✅ AGREE with RECON-level lean toward option (c). See Stage B.5 above for full tradeoff matrix. Stage 3 design owns final pick.

### OQ4 — Contract line drift (1937 → 1948 + 1873 → 1884)

**Reviewer disposition:** Contract amendment OPTIONAL. Stage 4 grep semantics handle drift naturally; "around 1937-1970" tolerance covers HEAD's 1948. If hygiene is desired, an in-place edit at contract Section 1.1 + 3.2.1 to use "1873/1948/1976/2003" is a 5-second amendment.

Reviewer will patch the agent-exchange/handoff/ contract Section 1.1 cite in this verify pass for hygiene. Not gating sign-off.

### OQ5 — Phase 6 contract not in `Docs/phase-gates/active/`

**RECON finding:** Phase 6 contract lives at `agent-exchange/handoff/2026-05-04-phase6-race-start-handoff-redesign-contract.md`; the Phase Gate System (per Methodology + CLAUDE.md "read in order: ... `Docs/phase-gates/active/<current-phase>-contract.md`") expects active contracts under `Docs/phase-gates/active/`. Harness helper currently surfaces phase7 + v2b-step1 (per RECON Surface 0).

**Reviewer disposition:** **VALID housekeeping concern**, but the move/copy decision affects audit trail and is NOT reviewer-domain to action unilaterally. Surface to Yonezawa for disposition before Stage 3 starts.

Two options:
- **(I) Move-and-update**: `git mv agent-exchange/handoff/2026-05-04-phase6-...md Docs/phase-gates/active/phase6-race-start-handoff-redesign-contract.md`. Single canonical source. README + implementer-prompt references update accordingly. agent-exchange/handoff/ trail loses the phase 6 contract entry — minor audit-trail reduction.
- **(II) Copy-and-redirect**: copy to active/. Add header note to agent-exchange/handoff/ original "MOVED — canonical lives at active/phase6-...md; this copy preserved as the Stage 1 KICKOFF artifact". Stage 3+ edits ONLY land at active/. Audit trail intact + harness helper sees the contract.

Reviewer leans (II) (preserves audit trail, methodology Rule 10 commit hygiene satisfied either way). NOT gating Stage 2 sign-off — can be actioned at Stage 3 prep window.

NOT a blocker.

---

## Stage E — Stage 2 sign-off recommendation

✅ **STAGE 2 RECON SIGN-OFF GRANTED.**

All KEY FINDINGs reproduced via independent git plumbing. Open Questions disposed without blockers. Pre-grep gate CLEAN for Phase 6 scope. Methodology Rule 12 honored — Recon row in contract Section 9 sign-off ledger written by reviewer AFTER this verify report's evidence chain, NOT pre-filled.

**Reviewer authorizes:**
1. Stamp Recon row in contract sign-off ledger Section 9
2. Implementer Stage 3 DESIGN-QA proceeds when user gives go (Stage 3 reads RECON + this verify + Section 11 + this report's Stage D OQ dispositions for design constraints)
3. Stage 3 DESIGN-QA outputs to `agent-exchange/handoff/2026-05-04-phase6-design.md` per Methodology process-flow

**Outstanding items routed to Yonezawa for disposition:**
1. **OQ5 contract location decision** (move to `Docs/phase-gates/active/` or stay) — affects helper surfacing + Stage 3 starting context
2. **OQ4 line-drift amendment** — minor; reviewer will patch for hygiene unless Yonezawa says skip
3. **Stage 3 design heads-up:** Q3 leans option (c) reuse SyncVar; Q4 RESOLVED auto-forward; Q9 elevate to G8b; Q1+Q10 collapse under Section 11.1 option (a) deletion; Section 3.2.1 file list expansion needed

End of Stage 2 verify report.
