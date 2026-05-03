# Phase 7.5 — Visual Jitter Retrofit Design Framework (DRAFT — pre-KICKOFF)

> ## ⚠ AMENDMENT 2026-05-03 — Surface 7 audit findings + rec-cb owner inversion
>
> Surface 7 audit (commit `3d5b328`) confirmed Layer 3 hard-snap hypothesis with strong evidence + caught a critical error in cowork-reviewer's V5 verify report: **rec-cb=2595 is on HOST log, not CLIENT log** (V5 verify had this inverted). Implications:
>
> 1. **Hypothesis tree refinement (Section 1):** Surface 7 explicitly verified at motor.cs:547-572 that `_predictionRigidbody.Reconcile(data.RigidbodyState)` is called directly with no project-side soft cap, threshold gate, or lerp path. This is exactly Layer 3 hard-snap pattern. Combined with `_enableTeleport: 0` on Buddah.prefab (lines 1082+1117) — **the smoother teleport-snap escape valve is ALSO disabled**. So at 80 m/s with 49 Hz reconcile, large corrections have NO escape (no smooth path AND no snap path). HIGH confidence on R7.5-A primary path.
>
> 2. **SMOKE driver focus flip (Section 8 prompt):** "watch CLIENT spectator-view character" → "watch HOST machine's own owner-view (local-control) character". The 49 Hz reconcile pressure manifests on whoever owns the host role, looking at their own predicted character.
>
> 3. **R7.5-A configuration table additions (Section 2.2 Step 3):** beyond enabling smooth-reconciliation, MUST also flip `_enableTeleport: 0 → 1` on Buddah.prefab to restore the snap-escape valve for >threshold corrections. Otherwise the soft-cap fix may push large corrections into the smoother where they pile up and produce visible jitter.
>
> 4. **Surface 7 hypothesis ranking (preserved verbatim from CC audit):**
>    1. HIGH — 49 Hz HOST reconcile rate vs 60 Hz tick + 2-frame smoother budget = structural over-saturation (7.5 + 7.1 + 7.2)
>    2. HIGH — `_enableTeleport: 0` + no project-side cap = no escape valve at 80 m/s (7.1 + 7.2)
>    3. MEDIUM — hitbox-rb / visual-smoother potential desync, conditional on prefab tree (7.4)
>    4. LOW — Layer 1 sound (7.3)
>    5. LOW — frame-time stability sound (7.6)
>
> Document body BELOW unedited from original. Apply HOST↔CLIENT swap on SMOKE focus + add `_enableTeleport: 0 → 1` to R7.5-A config table when promoting to contract.

**Status:** PRE-KICKOFF DRAFT — authored during Phase 7 RECON Surface 8 work to enable rapid Phase 7.5 startup once Phase 7 SMOKE/VERIFY confirms the Layer-3 hard-snap hypothesis. NOT yet promoted to active contract. Promotion path: when Phase 7 closes with regression-found outcome, copy relevant retrofit branch + hypothesis-confirmed details into `Docs/phase-gates/active/phase7-5-contract.md`, then cowork-reviewer stamps Kickoff.

**Amendment status:** AMENDED 2026-05-03 same day post-Surface-7 catch (above).

**Author:** cowork-reviewer (Claude Opus 4.7, harness)
**Date:** 2026-05-03
**Predecessor:** Phase 7 RECON Surface 8 industry reference research at `agent-exchange/handoff/2026-05-03-phase7-recon-surface8-industry-reference.md` + (forthcoming) Surface 7 codebase audit + Phase 7 SMOKE+VERIFY findings
**Goal:** make BuddahGo's visual layer match industry-standard host-client multiplayer smoothness (per Yonezawa explicit framing 2026-05-03)

---

## 1. Hypothesis tree — which retrofit branch activates

Phase 7 verify outcome drives branch selection:

```
Phase 7 Stage 6 VERIFY result:
│
├─ STRICT PASS (no jitter detected, all gates pass)
│     → No retrofit. Phase 7.5 framework DISCARDED.
│       Persistent jitter Yonezawa observed must be from
│       a DIFFERENT factor than measured paths captured.
│       Investigate user-side machine specifics.
│
├─ PATH B no-LatencySim FAILS G3.1-G3.4 (jitter exists even at 0ms latency)
│     → R7.5-A FIRST (config-only retrofit), escalate to R7.5-B
│       if FishNet smooth-reconciliation feature insufficient
│     → Pre-existing pipeline jitter, not LatencySim-induced
│
├─ PATH B 100ms FAILS G3.1/G3.2 + G3.5/G3.6 (jitter scales with reconcile rate)
│     → R7.5-A PRIMARY path (textbook hard-snap fix)
│     → If FishNet smooth-reconciliation feature exists + accessible:
│       config-only retrofit. ~0 LOC.
│     → If FishNet feature missing/inaccessible:
│       fall through to R7.5-B (Gambetta display/true split, ~100 LOC)
│
├─ PATH B 100ms FAILS G3.5/G3.6 ONLY (large rec-snap, jitter not visible)
│     → R7.5-A focused on rec-snap-distance threshold tuning
│     → Reconciliation working but occasional large snaps
│
├─ Surface 7 finds Layer 6 hitbox/visual desync (SC.3 driver red line)
│     → R7.5-D parallel sub-phase (NOT same as A/B/C); narrow scope
│       Re-examine PushHitbox.OverlapBox transform source
│
└─ Multi-layer mixed signal (numerics + driver perception don't agree)
      → R7.5-C HYBRID — multiple retrofits in coordinated sub-phases
        Most likely outcome if smoother config + Layer 6 desync both present
```

**Most likely path (per Surface 8 evidence):** R7.5-A — Layer 3 hard-snap reconciliation, fixable by enabling/configuring FishNet's existing smooth-reconciliation feature. Surface 8 explicitly cites FishNet docs:
> "To address the issues introduced by snap reconciliation, you can improve it by implementing a smooth reconciliation approach."

This is BuddahGo's exact stack. The fix is almost certainly already paved.

---

## 2. R7.5-A — FishNet smooth-reconciliation feature configuration retrofit

### 2.1 Premise

FishNet 4.x exposes a smooth-reconciliation feature (per Surface 8 search snippets from FishNet GitBook docs). Default behavior on `PredictionRigidbody` / `NetworkObject` is snap reconcile to authoritative state. By enabling smooth reconciliation, the visual root catches up over a configurable time window instead of teleporting.

### 2.2 IMPLEMENT scope (estimated ~0 code LOC, prefab/config edits)

**Step 1 — discover the FishNet API surface (CC research, since reviewer can't access fish-networking.gitbook.io):**

```
# CC reads on Windows browser (cowork container blocked):
1. fish-networking.gitbook.io/docs/guides/features/prediction/configuring-networkobject
2. fish-networking.gitbook.io/docs/guides/features/prediction (top-level for "smooth reconciliation" mention)
3. Local FishNet 4.6.20 source: 
   git ls-files Assets/FishNet/Runtime/ | xargs grep -l "Reconcile\|Smoothing\|GraphicalSmoothing" | head
4. NetworkObject inspector source files for SerializeField names:
   git grep -E "SerializeField.*[Ss]moothing\|SerializeField.*[Tt]eleport" Assets/FishNet/

Output: a "FishNet smooth-reconciliation API surface map" listing:
  - Class names (e.g., NetworkObject, PredictionRigidbody, ColliderRollbackSettings)
  - Field names + default values (e.g., _useSmoothing, _interpolationType, _teleportThreshold, _smoothingDuration)
  - Whether per-object (prefab) or per-NetworkManager (project settings)
  - Coupling to existing fields BuddahGo uses (PostIntroVisualLockPositionThreshold = 0.75f probably maps to one)
```

**Step 2 — identify what BuddahGo currently has set:**

```
git show HEAD:Assets/Character/Prefab/Buddah.prefab > /tmp/buddah-current.yaml
# extract relevant smoothing/prediction fields per Surface 7 audit 7.2
```

**Step 3 — propose new configuration:**

Per Surface 8 Section 6 R7.5-A recommendation:

| Setting | Current value (TBD post-Surface 7) | Proposed value | Justification |
|---|---|---|---|
| Smooth reconciliation | OFF (suspected) | ON | core fix |
| Interpolation type | Flat (suspected) | Adaptive | BuddahGo is casual, not competitive FPS |
| Owner interpolation ticks | 1 (per Q0 R0.2) | 1 | unchanged |
| Spectator interpolation ticks | 2 (per Q0 R0.2) | 2 | unchanged |
| Teleport threshold (position) | TBD | 0.75 m | matches existing PostIntroVisualLockPositionThreshold |
| Smoothing duration | TBD | ~100 ms | matches Source `cl_smoothtime` industry std |
| Use grace period | TBD | TRUE | absorbs single-frame outliers without snap |
| Detach graphical | FALSE (suspected) | FALSE | only enable if FishNet doc says required for our setup |

**Step 4 — Apply config via prefab YAML edit (single commit):**

```
git checkout -b feat/phase7-5-smooth-reconciliation
# Edit Assets/Character/Prefab/Buddah.prefab — modify the relevant SerializeField values
# (CC reads/writes the prefab YAML; reviewer reviews diff)
```

**Step 5 — validate via re-running Phase 7 SMOKE:**

This is the core validation. Re-run all 3 paths from Phase 7 contract:
- Path A wiring sanity (still passes)
- Path B no-LatencySim (Q3 G3.1-G3.4 should pass cleanly)
- Path B 100ms LatencySim (Q3 G3.1-G3.4 + G3.5-G3.6 + Q3.SEC.1+.SEC.2 all should pass)

If any gate still fails → escalate to R7.5-B.

### 2.3 Risks

- **R7.5-A.1 — FishNet feature unavailable in 4.6.20:** "smooth reconciliation approach" might be a 4.7+ feature, or only on `PredictionRigidbody` not `NetworkObject`. CC validates at Step 1.
- **R7.5-A.2 — Configuration interaction with existing PostIntroVisualLockPositionThreshold:** the project's intro-handoff hysteresis system might fight the new smoothing. Resolve by aligning teleport thresholds.
- **R7.5-A.3 — Different smoothness owner-vs-spectator:** smoothing might fix CLIENT but not improve HOST. Acceptable per Q0 R0.2 (different interp budgets).
- **R7.5-A.4 — Adaptive interpolation might over-correct:** under brief connection spikes, adaptive could buffer too long. Mitigation: cap maximum interpolation buffer at ~3 ticks.

---

## 3. R7.5-B — Gambetta-style display/true position split (fallback path)

### 3.1 Premise

If R7.5-A insufficient (FishNet feature missing or doesn't fully cover BuddahGo's case), implement Gambetta's canonical display/true position split manually on top of FishNet's existing infrastructure.

### 3.2 IMPLEMENT scope (estimated ~80-150 LOC)

**Architecture:**

```
BuddahPredictedMotor                    BuddahPredictionVisualRootBridge
   │                                            │
   │ (rb.position = TRUE pos,                   │ (visualRoot.position =
   │  authoritative, used for                   │  DISPLAY pos, lerp-toward-true)
   │  collision + game logic)                   │
   ▼                                            ▼
   FishNet Reconcile sets rb.position           LateUpdate:
   directly (HARD-SNAP at sim layer —             error = rb.position - displayPos
   this is correct per Fiedler for sim)            if |error| > teleportThreshold:
                                                    displayPos = rb.position  (teleport)
   On Reconcile callback (motor.cs:550):          else:
     visualRootBridge.NotifyReconcile(              displayPos = SmoothDamp(
       prePos = rb.position pre-restore,              displayPos, rb.position,
       postPos = rb.position post-restore)            ref velocity, smoothTime)
                                                  visualRoot.transform.position = displayPos
```

**Files touched:**

| File | Change | LOC |
|---|---|---|
| `Assets/Scripts/New_Buddah/Visual/BuddahPredictionVisualRootBridge.cs` | Add `_displayPosition`, `_displayVelocity` fields. Add `LateUpdate()` lerp catch-up. Add `NotifyReconcile(prePos, postPos)` API. Add SerializeField `_smoothTime = 0.1f`, `_teleportThreshold = 0.75f`. | ~60-80 |
| `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` | At motor.cs:550 area inside `[Reconcile]` callback, after `_reconcileCallbackCount++`, call `_visualRootBridge?.NotifyReconcile(prePos, postPos)`. UNITY_EDITOR-or-always (production should benefit). | ~5-10 |
| `Assets/Character/Prefab/Buddah.prefab` | Wire up SerializeField for VisualRootBridge new params + disable FishNet's _graphicalObject smoother (we replace it). | ~5 YAML lines |

**Pseudocode for VisualRootBridge addition:**

```csharp
// In BuddahPredictionVisualRootBridge

[SerializeField] private float _smoothTime = 0.1f; // Source's cl_smoothtime
[SerializeField] private float _teleportThreshold = 0.75f; // matches PostIntroVisualLockPositionThreshold
private Vector3 _displayPosition;
private Vector3 _displayVelocity;
private bool _displayPositionInitialized;

private void LateUpdate()
{
    if (!_displayPositionInitialized)
    {
        _displayPosition = movementRoot.position;
        _displayPositionInitialized = true;
    }
    Vector3 truePos = movementRoot.position;
    float error = Vector3.Distance(_displayPosition, truePos);
    if (error > _teleportThreshold)
    {
        // Snap on large errors (intro handoff, large reconciles)
        _displayPosition = truePos;
        _displayVelocity = Vector3.zero;
    }
    else
    {
        // Smooth catch-up — Source-style ~100ms exponential
        _displayPosition = Vector3.SmoothDamp(
            _displayPosition, truePos, ref _displayVelocity, _smoothTime);
    }
    // Apply to visual root only (not movementRoot — that stays authoritative)
    visualRoot.position = _displayPosition;
}

internal void NotifyReconcile(Vector3 prePos, Vector3 postPos)
{
    // Optional: log for Q4 probe; visual catch-up driven by LateUpdate above
}
```

### 3.3 Risks

- **R7.5-B.1 — Conflict with FishNet's `_graphicalObject` smoother:** must DISABLE FishNet smoothing to prevent two systems fighting. Critical config step.
- **R7.5-B.2 — Hitbox-visual desync intentional but bounded:** at `_smoothTime = 0.1s` and 60 fps, max display lag = ~6 frames during max correction. Push interaction visuals will look "delayed". Acceptable trade-off per Source pattern.
- **R7.5-B.3 — Animation rig coupling:** if Buddah's animation system is parented under `visualRoot`, animations will inherit the lerp. Usually desirable. If not, may need separate handling.
- **R7.5-B.4 — Camera follow target:** `cameraFollowTarget` is a separate field in VisualRootBridge. Camera might follow `visualRoot` (smoothed) or `movementRoot` (true). Decision: probably visualRoot (smoother camera). Confirm in IMPLEMENT.

---

## 4. R7.5-C — Hybrid (R7.5-A + selective R7.5-B for edge cases)

If R7.5-A handles 95% of cases but specific events (intro-end handoff, large reconciles, push impacts) still produce visible artifacts:

- Apply R7.5-A as base (FishNet smooth-reconciliation feature on)
- Add R7.5-B-style override on `BuddahPredictionVisualRootBridge` for events that smoother can't handle gracefully
- Specifically: post-intro handoff (already has hysteresis in current code) + large reconciles (>0.5m)

**Scope:** R7.5-A files + minimal R7.5-B subset (~30-50 LOC override path on VisualRootBridge).

---

## 5. R7.5-D — Hitbox/visual desync narrow retrofit (parallel sub-phase if Layer 6 surfaces)

If Surface 7 audit 7.4 finds PushHitbox.OverlapBox uses motor.transform.position (rb-driven) but visual_root is smoother-output, players see hitbox-visual desync.

**Resolution options:**

- **Option D-α:** PushHitbox uses visualRoot transform — accepts visual lag = collision lag (industry std)
- **Option D-β:** PushHitbox uses motor.transform but with bounded smoothing time — accepts that visual lag should be small (≤1 tick)
- **Option D-γ:** Split visual lag enforcement — motor lag <half tick → no fix; ≥half tick → adopt α

**Decision:** post-Phase-7 SMOKE evidence-based.

**Scope:** ~20-40 LOC across PushHitbox.cs + maybe BuddahPredictionVisualRootBridge.

---

## 6. Strict gates for Phase 7.5 (any retrofit path)

The Phase 7.5 SMOKE re-runs the same Phase 7 paths (Path A wiring sanity + 2 Path B variants) and must pass:

- All Phase 7 Q3 G3.1-G3.6 absolute thresholds met per peer per `owner=` flag (was: failing pre-retrofit)
- Phase 7 G3.SEC.1 secondary regression check passes (Path-B-100ms ≤ 110% Path-B-no-latency on `pos-dp99`)
- Phase 7 SUCCESS CRITERIA SC.1 / SC.2 / SC.3 all observed clean by driver (no rubber-banding, smooth recoil curves, no hitbox-visual desync)
- New Phase 7.5-specific: `pos-davg` Path-B-100ms reduced by ≥50% vs pre-retrofit baseline (substantive improvement, not just marginal)
- Phase 4b regression test: V5 cross-peer Tier 1 byte-identical chain still works (impulse delivery not broken by smoothing changes)
- Wire-format unchanged (Phase 7.5 should NOT touch ReconcileData / ReplicateData structs)

---

## 7. Carry-forward flags

- **Phase 7.5 → Phase 6** (Teleport + Handoff cut over): Phase 7.5's smoothing settings affect how teleport visuals work. If Phase 7.5 lands first, Phase 6 design should validate teleport snap behavior with smoothing on.
- **Phase 7.5 → Phase 8** (Cleanup): if Phase 7.5 introduces new files (R7.5-B path), Roslyn analyzer should add lint rules for them.
- **Phase 7.5 → future game-feel work:** smoothing parameters (`_smoothTime`, `_teleportThreshold`, interpolation type) become game-feel tuning knobs, not just netcode internals. Document for designers.

---

## 8. Implementation prompt for Claude Code (ready-to-fire post Phase 7 verify)

```
Phase 7.5 — Visual Jitter Retrofit IMPLEMENT

Phase 7 closed with regression-found outcome (verify report at <path>).
Phase 7.5 KICKOFF stamped per <pre-write or just-stamped contract>.
Branch: feat/phase7-5-smooth-reconciliation cut from dev tip post Phase 7
merge.

This prompt assumes hypothesis CONFIRMED: Layer 3 hard-snap reconciliation
is the persistent jitter root cause (Surface 7 7.1 + Surface 8 + SMOKE
Q3 G3.1/G3.2/G3.5 fail on Path B 100ms LatencySim CLIENT side; SC.1
rubber-banding observed). Activates retrofit branch R7.5-A primary, with
escalation to R7.5-B if R7.5-A insufficient.

If Phase 7 verify outcome differs (different layer, mixed signal, no
regression), STOP and ping reviewer for branch redirection.

=== STAGE 4 IMPLEMENT — R7.5-A primary branch ===

Step 1 — FishNet smooth-reconciliation API discovery (read FishNet 4.6.20
source + docs):

  (a) Search local FishNet source for relevant SerializeFields:
      git grep -nE "SerializeField.*[Ss]moothing|SerializeField.*[Tt]eleport|SerializeField.*[Rr]econcil" Assets/FishNet/

  (b) Open these files for fields/methods (one or more should expose
      smoothing config):
      - Assets/FishNet/Runtime/Object/NetworkObject.cs
      - Assets/FishNet/Runtime/Generated/Component/Prediction/PredictionRigidbody.cs
      - Assets/FishNet/Runtime/Generated/Component/TickSmoothing/* (all)
      - Assets/FishNet/Runtime/Generated/Component/ColliderRollback/*

  (c) Read fish-networking.gitbook.io docs (Windows browser, since cowork
      container is allowlist-blocked):
      - /docs/guides/features/prediction/configuring-networkobject
      - /docs/fishnet-building-blocks/components/tick-smoothers/networkticksmoother
      - /docs (root, search "smooth reconciliation")

  Output: brief inventory of available smoothing API in agent-exchange/
  handoff/2026-05-03-phase7-5-fishnet-api-inventory.md. List for each
  exposed smoothing config:
    - Class + field name (e.g., NetworkObject._useSmoothing)
    - Default value
    - Per-prefab or per-NetworkManager
    - What FishNet docs say about behavior
    - Whether it's Gone-In-4.7 or applicable to 4.6.20 (BuddahGo version)
  
  If FishNet's smooth-reconciliation feature ISN'T accessible/configurable
  in 4.6.20 (or only available on PredictionRigidbody but Buddah uses raw
  Rigidbody, etc.), STOP at this step + report; reviewer redirects to
  R7.5-B branch.

Step 2 — Read current Buddah.prefab smoothing config (audit baseline):

  git show HEAD:Assets/Character/Prefab/Buddah.prefab > /tmp/buddah-baseline.yaml
  
  Extract values for the API fields identified in Step 1. List each in
  agent-exchange/handoff/2026-05-03-phase7-5-buddah-prefab-baseline.md.
  
  Cross-reference with Surface 7 7.2 audit findings — these should match.
  If Surface 7 already produced this inventory, just cite it.

Step 3 — Propose retrofit configuration (review-able table):

  Per design framework Section 2.3 R7.5-A target table, propose new values
  per BuddahGo-specific FishNet API surface from Step 1+2. Output as a
  diff-style table in agent-exchange/handoff/2026-05-03-phase7-5-config-proposal.md:

  | Field | Class | Current | Proposed | Justification |
  |---|---|---|---|---|
  | (filled in based on Step 1+2) | ... | ... | ... | (Surface 8 Section 6 rationale) |

  Push the proposal as a draft commit on feat/phase7-5-smooth-reconciliation
  (no Buddah.prefab edit yet — just the proposal docs).
  
  STOP. Ping reviewer for proposal review (~10 min) before applying.

Step 4 — Apply approved configuration to Buddah.prefab:

  After reviewer approves the proposal:
  - Edit Assets/Character/Prefab/Buddah.prefab YAML directly to change
    the SerializeField values per the approved table.
  - Run Unity once + verify prefab still validates (no missing component
    refs, no errors in console).
  - git add Assets/Character/Prefab/Buddah.prefab
  - git commit -m "phase7-5 R7.5-A: enable FishNet smooth-reconciliation
    on Buddah.prefab + tune smoothing parameters"

Step 5 — Re-run Phase 7 SMOKE on the retrofit:

  Per Phase 7 process-flow Stage 5:
  - Path A 30s sanity (probe wiring still works)
  - Path B 60s no-LatencySim (HOST + CLIENT logs)
  - Path B 60s 100ms LatencySim (HOST + CLIENT logs)
  - Save logs to agent-exchange/console/raw/<date>-phase7-5-{path}-{role}-jitter.log
  
  Driver workflow same as Phase 7 (SC.1/SC.2/SC.3 watch-list); explicitly
  compare subjective smoothness to pre-retrofit (driver's own memory of
  Phase 7 SMOKE).

Step 6 — Run AnalyzeJitter.ps1 on new logs + compare to Phase 7 baseline:

  powershell -ExecutionPolicy Bypass -File Tools/Analysis/AnalyzeJitter.ps1 \
    -LogPaths agent-exchange/console/raw/2026-05-03-phase7-5-*.log
  
  Output table: per peer per path, pre-retrofit vs post-retrofit:
    pos-davg, pos-dp99, pos-dmax, rec-snap-{p99,max}
  
  Phase 7.5-specific gate: pos-davg Path-B-100ms reduced ≥50% vs pre.
  Phase 7 G3 absolute gates: all should now PASS.

Step 7 — Smoke digest + Implementation row stamp:

  Author SMOKE digest at agent-exchange/console/2026-05-03-phase7-5-digest.md
  with comparison table + driver SC.1/SC.2/SC.3 observations.
  
  Stamp Implementation row + Smoke row in active/phase7-5-contract.md
  (Rule 12: implementer can own-stamp these two; NOT Verify or Recon).
  
  Push + ping reviewer for Stage 6 VERIFY.

  If gates STILL FAIL after R7.5-A applied:
    - STOP. Document failure mode in SMOKE digest.
    - Reviewer redirects to R7.5-B branch (~80-150 LOC implementation
      per design framework Section 3).
    - Reviewer authors R7.5-B-specific implementation prompt
      (template ready in framework but not finalized until R7.5-A
      escalation needed).

=== If R7.5-A insufficient → R7.5-B fallback ===

R7.5-B implementation prompt is provided in design framework Section 9
(below) but should NOT be executed without explicit reviewer redirection
from R7.5-A failure.

=== Discipline reminders ===

- Rule 12: implementer pre-fills Implementation + Smoke rows in contract;
  NOT Recon / Design / Verify rows.
- L21 + L22: any "X file unchanged" claim verified via git diff origin/dev
  -- <path>; not via Read on working tree.
- Rule 1-D + 1 (raw log discipline): SMOKE produces full Editor.log per
  path, saved to agent-exchange/console/raw/.
- Rule 7 (PredictionRigidbody integrity): R7.5-A is config-only — no rb
  writes added. R7.5-B adds reads from rb in LateUpdate but no writes.
- Rule 10 (commit hygiene): contract edits committed same PR cycle.
- Rule 11 (SMOKE driver discipline): no time-sensitive probe in 7.5;
  Rule 11 moot here.
- New L23-style execution-test (Rule 2 sub-clause): if 7.5 modifies
  AnalyzeJitter.ps1, re-run end-to-end + paste stdout into verify report.
```

---

## 9. R7.5-B fallback implementation prompt (held in reserve)

Provided here so the framework is complete; only fires if R7.5-A can't fix
the issue.

```
Phase 7.5 — Visual Jitter Retrofit R7.5-B FALLBACK

R7.5-A insufficient (FishNet smooth-reconciliation feature unavailable
or didn't reduce jitter to within Q3 thresholds). Implementing
Gambetta-style display/true position split per design framework Section 3.

Per design framework, files to touch:
  - BuddahPredictionVisualRootBridge.cs: ~60-80 LOC additions (display
    position field + LateUpdate lerp + NotifyReconcile API + new
    SerializeFields _smoothTime, _teleportThreshold)
  - BuddahPredictedMotor.cs: ~5-10 LOC at motor.cs:550 ([Reconcile]
    callback) — call NotifyReconcile after _reconcileCallbackCount++.
    UNITY_EDITOR or production-permanent (decide per perf budget).
  - Buddah.prefab: ~5 YAML lines — disable FishNet's _graphicalObject
    smoother + wire VisualRootBridge new SerializeFields.

Step 1 — read BuddahPredictionVisualRootBridge full source:
  git show HEAD:Assets/Scripts/New_Buddah/Visual/BuddahPredictionVisualRootBridge.cs

  Verify the current LateUpdate / hot-path doesn't already do something
  conflicting. Check the existing PostIntroVisualLockPositionThreshold +
  unlock hysteresis fields — they should remain functional for intro
  handoff while new smooth catch-up handles steady-state reconciles.

Step 2 — implement the display/true split per design framework Section
3.2 pseudocode. Key constraints:
  - displayPosition initializes to movementRoot.position on first
    LateUpdate (not Awake — prefab might not be in scene at Awake time)
  - Vector3.SmoothDamp with ref velocity field (not Lerp — SmoothDamp
    matches Source's exponential behavior more naturally)
  - Teleport threshold check before SmoothDamp — if exceeded, snap
  - Apply to visualRoot.position only — movementRoot stays authoritative

Step 3 — single-line addition to motor.cs:550:
  After _reconcileCallbackCount++:
    _visualRootBridge?.NotifyReconcile(prePos, postPos);
  
  Where prePos/postPos are captured at start/end of [Reconcile] callback
  body. Per Q4 design R4.2, decide whether postPos = post-replay or
  authoritative-tick (probably authoritative-tick = "the snap location"
  is what visual smooths to).

Step 4 — Buddah.prefab YAML:
  - Disable FishNet _graphicalObject smoother (find the relevant component
    on the NetworkObject inspector, set its enabled = false OR remove its
    smoothing config OR make it pass-through).
  - Wire new VisualRootBridge SerializeFields (_smoothTime, _teleportThreshold).

Step 5 — same SMOKE + analyzer + verify cycle as R7.5-A Step 5-7.

Discipline reminders same as R7.5-A.
```

---

## 10. Open questions for Yonezawa (pre-KICKOFF, optional)

Not blocking — Phase 7 SMOKE/VERIFY drives most of these:

- **OQ7.5.1 — Allowlist FishNet docs domain?** If you want the cowork container to fetch full FishNet GitBook docs (currently blocked), add `fish-networking.gitbook.io` to Settings → Capabilities. Optional — CC reads on Windows can fill the gap.
- **OQ7.5.2 — Phase 7.5 PR strategy:** single PR with R7.5-A only, OR R7.5-A + safety net of R7.5-B in same branch (extra commits if A fails)? Reviewer recommends single PR + escalate via fresh R7.5-B-only PR if needed; cleaner audit trail.
- **OQ7.5.3 — Retrofit timing:** Phase 7.5 fires immediately on Phase 7 close (recommended), OR queued behind Phase 6 / Phase 8? Reviewer recommends immediate — jitter is user-facing impact.
- **OQ7.5.4 — Game-feel tuning ownership:** post Phase 7.5 land, who owns the smoothing parameter knobs (`_smoothTime`, `_teleportThreshold`)? Designer-tunable Inspector fields? Code-locked? Reviewer recommends Inspector with default + warn-on-extreme-values.

---

## 11. Promotion to active contract (when Phase 7 closes)

Once Phase 7 verify confirms hypothesis (or adjacent), promote this draft:

1. `git mv` this file to: `agent-exchange/handoff/<keep here as predecessor reference>` (keep it; don't move)
2. Author NEW: `Docs/phase-gates/active/phase7-5-contract.md`
3. New contract structure: scope (locked) + PRE-WORK Q (which retrofit branch + any hypothesis-confirmation specifics) + strict gates + carry-forward + sign-off ledger
4. cowork-reviewer stamps Kickoff row, references Surface 7 + 8 + Phase 7 verify report as predecessor evidence
5. CC cuts `feat/phase7-5-smooth-reconciliation` branch + executes the implementation prompt above
