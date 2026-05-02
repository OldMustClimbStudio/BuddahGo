# phase4b-v4 — Design Q&A

**Recon reference:** [`agent-exchange/handoff/2026-05-03-phase4b-v4-recon.md`](agent-exchange/handoff/2026-05-03-phase4b-v4-recon.md) (v2 — Stage 2 SIGN-OFF 2026-05-03)
**Status:** DESIGN PROPOSAL — no code changes yet
**Branch:** feat/phase4b-v4-cleanup (cut from dev @ 06882bb)

---

## Q0 — `pendingImpulseSummary` resolution

**Picked:** **(A) Add `BuildPendingSummary()` to channel.**

**Justification:**
- One live consumer: `BuddahPredictionDebugOverlay.cs:106` (dev HUD overlay). The overlay is actually used by devs investigating prediction issues — losing the diagnostic line is a real (if small) regression.
- Cost is ~15 LOC. The channel's `_pending` list is trivially iterable; mirror OLD's `BuddahPredictedImpulseEventQueue.BuildPendingSummary()` shape for `eventTick=N logicalId=M src=...` per-pending row, joined with `;`.
- Aligns with V3 design Q&A's documented "transitional state" expectation — V3 explicitly deferred this to V4 with the understanding the channel-side builder would land here.
- Option (B) drops the field, requiring overlay edit + DebugState edit + prefab YAML cleanup for marginal LOC saving + diagnostic regression.
- Option (C) stub-only is dead surface; misaligned with V4 cleanup spirit.

**Implementation outline:**
- Add `public string BuildPendingSummary()` on `BuddahPredictionEventChannel<T>`. Iterates `_pending`, formats each entry with shape `eventTick=<tick> logicalId=<id>` (channel doesn't have rich source data; OLD's `src=` field omitted unless the entry's payload carries it — verify in implementation).
- Replace 3 call sites in `BuddahPredictedMotor.cs:232/:1330/:2398` (`_impulseEventQueue.BuildPendingSummary()` → `_impulseChannel.BuildPendingSummary()` or equivalent NEW-side handle).
- Field stays at `BuddahPredictionDebugState.cs:127`. Default value `"none"` unchanged.

**Risk:**
- Channel summary format diverges from OLD's. Devs reading the HUD across a V4 boundary may briefly see a different string layout. Mitigation: match OLD's tag order where possible.
- If channel's payload struct doesn't carry source info that OLD's `BuddahPredictedImpulseEventData` carried, the summary loses data. Mitigation: accept reduced info (LogicalId-stamped tick is the primary diagnostic anyway).

**Open questions:** none. Implementation detail (exact format string) handled in IMPLEMENT stage.

---

## Q1 — `BuddahPredictionImpulseDebugBox.fallbackToLegacy` removal

**Picked:** **(A) Remove the field.**

**Justification:**
- 0 code-path readers (verified by grep, see Recon Item 4.2). The comment at `BuddahPredictionImpulseDebugBox.cs:46` explicitly states the field is no-op post-V3.
- 1 scene serialized value at `RaceMap.unity:10955` (`fallbackToLegacy: 1`). Unity drops unknown YAML keys silently on next scene save — no warning, no behavioral change because field was already no-op.
- CLAUDE.md hard-stop on serialized field removal exists to protect against breaking scenes/prefabs that DEPEND on the value. Post-V4, the field's underlying purpose (LEGACY_SHADOW define-gated path) is gone; the field can no longer affect runtime behavior. Hard-stop's premise is provably resolved.
- Option (B) `[Obsolete]` mark leaves dead surface; future V5/Phase 8 readers must reconfirm dead-ness all over again.

**Implementation outline:**
- Delete the `[SerializeField] private bool fallbackToLegacy = true;` line (DebugBox.cs:17).
- Delete the explanatory comment at DebugBox.cs:46 (no longer needed; field gone).
- Open `RaceMap.unity` in Unity → re-save scene to drop the orphan YAML key. (Or hand-strip the line; verify `git diff` shows clean removal.) Document the re-save step in PR description.

**Risk:**
- If a scene/prefab variant we haven't found contains the field, re-save loses the user-set value. Mitigation: pre-merge grep `fallbackToLegacy` across `Assets/**/*.{prefab,unity,asset}` to confirm `RaceMap.unity` is the sole serialized location. Already verified in recon — confidence high.

**Open questions:** none.

---

## Q2 — Adapter caching opportunity

**Picked:** **(B) Defer to Phase 8 perf cleanup.**

**Justification:**
- V4 is deletion-heavy; mixing perf opt with structural cleanup blurs PR review boundaries.
- Adapter still does `GetComponent<Bootstrap>` per fire post-V4. Profile measurement is the right gate for caching decisions, not gut-feel "feels expensive".
- Phase 8 has dedicated perf scope; profiler measurements + Roslyn analyzer + `ClampPlanarSpeed` (L9) all live there. Caching micro-opt belongs in that toolbelt.
- Option (A) lazy-cache pattern is simple but introduces a state machine (uninitialized / initialized / re-initialized after destroy) that needs its own correctness story — doesn't fit V4's "delete dead code, change nothing live" spirit.

**Risk:** none introduced by deferral. Existing GetComponent cost per fire is the post-V3 baseline; V4 doesn't make it worse.

**Open questions:** none.

---

## Q3 — Define retirement procedure (CRITICAL — multi-part body per reviewer mandate)

**Picked:** **(A) Strip define from ProjectSettings + delete `#if/#endif` wrapper code in same commit (atomic deletion).**

**Justification:**
- Atomic deletion: PR-review grep verifies completeness in one pass (`BUDDAH_PREDICTION_LEGACY_SHADOW` returns 0 hits across `Assets/**` post-merge).
- Option (B)'s "intentional compile error" two-step is messier to review and risks half-merged state if force-pushed wrong.
- 12 `#if` blocks across 3 files + 1 ProjectSettings line is a manageable atomic diff for review.

### Q3.1 — L7 latch survival (CRITICAL — strip/keep distinction)

**Recon Item 2.3** flagged this as the highest-risk regression in V4. Implementer must follow strip-wrapper-keep-body precisely.

#### `BuddahPredictedMotor.cs:382-396` — BEFORE (current state)

```csharp
#if BUDDAH_PREDICTION_LEGACY_SHADOW
            // Phase 4b V2a — L7 late-bind for the CombatAdapter. ShouldRunPrediction()
            // returning true here proves we're past BuddahMovementModeSwitcher's
            // double-ApplyMode (Awake + OnEnable both fire ApplyMode → 4
            // [CommandBus]:ClearAll lines per spawn). MarkReady() runs exactly
            // once per motor instance; subsequent enqueues from CombatAdapter
            // will not be wiped by a second ClearAll. Per L7 rule (b).
            if (!_combatAdapterInitialized && bootstrap != null && bootstrap.CombatAdapter != null)
            {
                bootstrap.CombatAdapter.MarkReady();
                _combatAdapterInitialized = true;
            }
#endif
```

#### `BuddahPredictedMotor.cs:382-396` — AFTER (Q3 design)

```csharp
            // Phase 4b — L7 late-bind for the CombatAdapter. ShouldRunPrediction()
            // returning true here proves we're past BuddahMovementModeSwitcher's
            // double-ApplyMode (Awake + OnEnable both fire ApplyMode → 4
            // [CommandBus]:ClearAll lines per spawn). MarkReady() runs exactly
            // once per motor instance; subsequent enqueues from CombatAdapter
            // will not be wiped by a second ClearAll. Per L7 rule (b).
            if (!_combatAdapterInitialized && bootstrap != null && bootstrap.CombatAdapter != null)
            {
                bootstrap.CombatAdapter.MarkReady();
                _combatAdapterInitialized = true;
            }
```

**Diff intent:**
- DELETE 2 lines: `#if BUDDAH_PREDICTION_LEGACY_SHADOW` (line 382) + `#endif` (line 396).
- KEEP 13 lines unchanged: comment block (383-388) + if-block (389-393) + closing brace.
- KEEP `_combatAdapterInitialized` field decl at `motor.cs:148` (NOT in any LEGACY_SHADOW block).
- Comment edit: drop "Phase 4b V2a" → "Phase 4b" (the V2a-era reason for the wrapper is gone; the latch itself spans all of 4b).

**Why this matters (failure mode if implementer follows v1's wrong "delete with #if" suggestion):**
- `bootstrap.CombatAdapter.MarkReady()` never fires.
- CombatAdapter's `_isReady=false` indefinitely.
- Channel enqueues during the spawn-window race condition (post-spawn, before whatever else triggers ready) get dropped silently.
- D-LOC FATAL=0 stays clean (no compare reference for spawn-window-only drops).
- LEG axis is gone (V4 retired it).
- Visual smoke misses it: push test typically happens mid-game, not in the 4-ClearAll spawn window.
- **Highest-risk silent regression in V4.**

### Q3.2 — Spawn-window smoke probe (added to strict gate)

Per Methodology Rule 1 sub-rule (D) and Q4 amendment:

**New strict gate row (Path A + Path B):**
```
Spawn-window L7 latch verified: in the first 60 ticks post-buddah-spawn,
either ≥1 [CommandBus]:Recv ch=Impulse line OR ≥1 downstream impulse
evidence (DebugState.lastConsumedImpulseId increment) — proves
bootstrap.CombatAdapter.MarkReady() fired and channel enqueue is alive.
```

**Implementation in smoke procedure:**
- Path A: spawn buddah, wait ~1s, fire ChargedHandProjectile at self/DebugBox within first 5s of spawn → verify in raw log: spawn tick `T_s` from `[Bootstrap]` log; first impulse Recv at `T_s + delta` where `delta < 60 ticks`.
- Path B HOST: same on host side. Path B CLIENT: receives Recv from server-driven push, verify same tick distance.
- If Recv lands but distance > 60 ticks, partial pass → flag as warning, investigate whether the latch fires later than expected.

**This probe replaces the LEG-axis early-warning** — once LEG is retired, the spawn-window race is the exact regression class the LEG observation layer was originally designed to surface.

### Q3.3 — Cascade deletion order (Item 1.5 implementation sequencing)

Implementer follows this exact order to keep the codebase compile-clean at each step:

| Step | Action | Compile target |
|---|---|---|
| 1 | Delete `BuddahImpulseStep.cs` + `.meta` | Compile fails: motor.cs:105/:429 comments still reference (only comments, fine) but TickContext.cs:24/:65/:94 still has `ImpulsePendingSnapshot` field/param — compiles clean (unused but valid). |
| 2 | Delete `BuddahPredictionTickContext.ImpulsePendingSnapshot` field (`.cs:24`), ctor param (`.cs:65`), ctor body assign (`.cs:94`) | Compile fails: motor.cs:1379 still passes `impulsePendingSnapshot:` named arg → expected. |
| 3 | Delete motor.cs:1379 named-arg line `impulsePendingSnapshot: _shadowPreImpulsePendingSnapshot,` | Compile clean: 3 BuildTickContext callers (`:425/:489/:597`) pass via `BuildTickContext()` helper which now has 1 fewer param. |
| 4 | Delete motor.cs:99 field `_shadowPreImpulsePendingSnapshot` + motor.cs:420 write `_impulseEventQueue.CopyPendingSnapshot(_shadowPreImpulsePendingSnapshot);` | Compile fails: line 420 references `_impulseEventQueue` which is the V4 main-deletion target → covered by main motor cleanup. |
| 5 | Main motor LEGACY_SHADOW deletion pass (Item 2.2 block map) | Compile clean. |
| 6 | Delete `BuddahPredictedImpulseEventQueue.cs` + `.meta` | Compile clean (motor field already gone in step 5). |
| 7 | Delete `BuddahPredictedReconcileData.ImpulseQueueState` field + motor.cs:303 writer + motor.cs:649/:683 readers | Compile clean. |
| 8 | Delete `BuddahPredictedImpulseRingSnapshot.cs` + `.meta` | Compile clean (sole consumer field already gone). |
| 9 | Strip `BUDDAH_PREDICTION_LEGACY_SHADOW` from `ProjectSettings.asset:827` Standalone line | Compile clean (all `#if` blocks already deleted in step 5). |
| 10 | Delete `BuddahPredictionCombatRouting.cs` + `.meta` (post-step-5 it has no callers; entire body was already under #if) | Compile clean. |
| 11 | Comment cleanup pass (Item 1.6 13-row table) | Compile clean (comments don't affect build). |
| 12 | Refactor-plan footer additions (Q3.5) | N/A. |

**Verification at each step:** `Assets/Refresh` (or AssetDatabase.Refresh via MCP) → check console for 0 errors. Step 5 is the largest atomic; commit per checkpoint optional but recommended for review.

### Q3.4 — Wire format `[Reconcile]` struct change deployment coordination (5-point ratification)

**RATIFIED** as proposed in recon Item 5.4. Verbatim plan for V4 PR description:

1. **Atomic deployment requirement.** V4 deletes `BuddahPredictedReconcileData.ImpulseQueueState`. Pre-V4 client + V4 host (or vice-versa) reading each other's reconcile packets = silent corruption (deserialize wrong field offsets). No backward-compat shim.
2. **Steam build chain timing.** V4 PR merge to `main` triggers Steam build push. Build completion on Steam = ~10-15 min after merge (estimate; verify with first push). **Test sessions across an in-flight V4 rollout window are forbidden** until both peers have pulled matching version hash. PR-merging reviewer must coordinate with active playtesters before approving merge.
3. **Lobby version handshake.** Current Steam lobby flow does NOT include explicit prediction-protocol version negotiation. V4 itself does NOT add this — out of scope. Mitigation in V4 era: calendar coordination + Steam version lock at PR merge time. **V5 PRE-WORK flag (added):** consider adding `predictionProtocolVersion` integer to lobby metadata + connection-time handshake as a permanent fix.
4. **Rollback flow.** If V4 reverted post-merge: same atomic requirement, reverse direction. All peers downgrade simultaneously. Document in PR description's "Deployment coordination" section.
5. **PR description mandate.** V4 PR body MUST contain a "Deployment coordination" section spelling out items 1-4 for the merging reviewer's checklist. Template snippet provided in IMPLEMENT stage handoff.

### Q3.5 — `Docs/prediction-refactor-plan/` chapter disposition

**Picked: (b) Footer note.** Reasoning: chapters describe historical refactor design intent; rewriting them inline loses the "why" trail. A footer adds a forward-pointer without erasing the historical text.

**Footer text template** (append to chapters 02 / 07 / 09 / 12 — verify exact chapter list during IMPLEMENT — at end of each affected chapter):

```markdown
---

> **Historical note (Phase 4b V4, 2026-05-03):** This chapter references symbols
> retired during Phase 4b V4 cleanup, including `BuddahPredictionCombatRouting`,
> `BuddahPredictedImpulseEventQueue`, and the `BUDDAH_PREDICTION_LEGACY_SHADOW`
> define. The original architectural rationale captured here remains valid for
> understanding the design history; for current-state code paths, see
> [`Docs/phase-gates/archive/v4-contract.md`](../../Docs/phase-gates/archive/v4-contract.md)
> (post-V4 closure record) and the live router at
> [`Assets/Scripts/New_Buddah/Integration/BuddahPredictionRouter.cs`](../../Assets/Scripts/New_Buddah/Integration/BuddahPredictionRouter.cs).
```

(Path adjustments per chapter location verified during IMPLEMENT — relative-path math depends on whether chapters live one or two levels deep.)

**Risk:** none structural. Footer is additive markdown; no code or build implications.

**Open questions:** verify the exact chapter list during IMPLEMENT (recon listed 02 / 07 / 09 / 12; reviewer's Item 6 paraphrase noted "07 — verify scope in design"). Implementer greps `BuddahPredictionCombatRouting` across `Docs/prediction-refactor-plan/` and reports the actual hit list before applying footers.

---

## Q4 — Strict gate recalibration (post-LEG retirement)

**Picked:** **(A) D-LOC FATAL only + visual smoke + counter sanity + (NEW) spawn-window probe.**

**Justification:**
- Q4 amendment in V2b Step 1 already dropped the impulse axis from D-LOC (no `_dLocImpulseDivCount` post-Step-1). LEG axis was the sole impulse-correctness signal.
- Post-V4, LEG axis disappears. D-LOC FATAL covers locomotion / teleport / modifier / handoff axes — those remain operational and unchanged.
- Visual smoke (push lands on host buddah, PushGrace activates) covers gameplay correctness end-to-end.
- Counter sanity (`leg-imp-compared` etc. all gone; `[CommandBus]:Recv` count and `DebugState.consumedImpulseCount` mononic-increase remain).
- **NEW probe — spawn-window L7 latch verification (Q3.2)** — replaces the safety-net role LEG axis played for spawn-time race conditions.
- V5 LatencySim 100ms RTT probe is the next safety check for cross-tick ordering. V4 → V5 should be quick succession; no need for additional V4 observation surface.
- Option (B) "design fresh observation layer" cost is high; effort better spent on V5.
- Option (C) "keep LEG axis but invert authority understanding" makes the gate semantically confusing and contradicts the V4 deletion intent.

**Final V4 strict gate (locked):**

### Path A — Host-only single peer ~30s
- `[D-LOC FATAL]` count = 0
- `[CommandBus]:ClearAll` = 4 (single buddah spawn — Switcher's double-ApplyMode signature)
- `[CommandBus]:DropFull` = 0
- `[Channel]:DupReject` = 0 baseline (post-Step-1 LogicalId dedup)
- 0 references to `BUDDAH_PREDICTION_LEGACY_SHADOW` in `Assets/**` (grep verification, post-deletion)
- 0 references to deleted symbols (5 strings: `_legacyShadowScratch`, `ConsumePendingImpulseEvents_LegacyShadow`, `BuddahPredictionCombatRouting`, `BuddahPredictedImpulseEventQueue`, `BuddahImpulseStep`)
- 0 `[D-IMP LEG ...]` lines (proves emit code retired)
- Methodology Rule 7 verified: 0 direct `rb.AddForce` / `rb.AddTorque` in remaining code paths
- Methodology Rule 1 sub-rule (D): post-smoke ≥3 actual impulse hits landed
- **NEW: Spawn-window L7 probe** — first impulse `[CommandBus]:Recv ch=Impulse` lands within 60 ticks of buddah spawn `T_s` (proves `bootstrap.CombatAdapter.MarkReady()` fired correctly)
- Visual: push lands on host buddah, PushGrace activates

### Path B — 2-peer LAN ~60s (HOST + CLIENT)
- All Path A criteria per peer
- `[CommandBus]:ClearAll` = 4 × N spawns per peer
- CLIENT `[CommandBus]:Recv ch=Impulse` ≥ 1 (β path liveness)
- CLIENT Recv line carries both `eventTick=N` AND `logicalId=N` fields (Step 1 LogicalId dedup wire format)

**Risk:** none introduced by Q4=A. Spawn-window probe is additive (catches the regression class previously caught by LEG); D-LOC + visual + counter sanity already proven through Step 1 + V3.

**Open questions:** none.

---

## Cross-cutting concerns

### CC1 — Q3 atomicity vs Q3.3 stepwise cascade
Q3 design intent is "atomic deletion in one commit". Q3.3 cascade order is for the implementer's local working sequence to keep compile clean — they may produce a single squashed commit at the end, OR multiple step-commits + final squash on PR merge. Either path satisfies Q3=(A); the stepwise order matters for the implementer's iteration loop, not the merge artifact.

### CC2 — Q3.1 (L7 latch keep) vs Q4 (spawn-window probe)
The spawn-window probe verifies the L7 latch survived correctly. Q3.1 is the structural change; Q4 spawn probe is the gate that catches Q3.1 implementer mistakes. Two layers of defense against the highest-risk regression.

### CC3 — Q0 (channel summary) interacts with Item 1.5 (snapshot chain)
`pendingImpulseSummary` writers are inside motor methods that get deleted (Q3.3 step 5). Q0=(A) implementation must land BEFORE step 5 of cascade so the new write sites (`_impulseChannel.BuildPendingSummary()`) replace the old ones (`_impulseEventQueue.BuildPendingSummary()`) atomically. Sequencing: Q0 channel-side builder + 3 call-site rewrites land first; then cascade deletion proceeds.

### CC4 — Wire format change (Q3.4) timing relative to V5 PRE-WORK
V4 PR adds the V5 PRE-WORK flag for `predictionProtocolVersion` lobby handshake but doesn't implement it. V5 PRE-WORK section in V5 contract gets the carry-forward. V4 ships with the calendar-coordination interim mitigation.

---

## Awaiting sign-off

Reviewer (cowork) Stage 3 sign-off authorizes Stage 4 IMPLEMENT. Sign-off ledger row "Design" gets stamped with date + signer in `Docs/phase-gates/active/v4-contract.md`.

Implementation will produce:
- One feature branch (`feat/phase4b-v4-cleanup`, already cut at `06882bb`).
- Cascade-ordered code changes per Q3.3 (12 steps; squashed commits acceptable).
- PR with:
  - Q4 strict-gate metric tables (Path A + Path B HOST + Path B CLIENT).
  - "Deployment coordination" section per Q3.4 5-point plan.
  - Post-merge grep mandate for 5 dead identifier strings.
  - Refactor-plan footer additions per Q3.5.
- Smoke + verify deliverables per Stages 5/6.

Methodology compliance reminders for IMPLEMENT:
- **Rule 8** strict on V4: no skipping ahead. Reviewer SIGN-OFF before code.
- **Rule 7** PredictionRigidbody integrity: NEW drain (post-Step-1 authority) untouched by V4 — should remain compliant. Cleanup only deletes OLD-side observation code.
- **Rule 10** commit hygiene: every implementation step commits immediately; no untracked-only state across sessions.
- **Rule 1 sub-rule (D)** + **Rule 2 trust hierarchy**: post-smoke event sanity check + downstream evidence priority over stack-frame counts. Both are now strict requirements.
