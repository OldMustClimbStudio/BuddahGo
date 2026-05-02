# phase4b-v4 — Recon Report (v2 — amended after reviewer Stage-2 challenge)

**Branch:** feat/phase4b-v4-cleanup (cut from dev @ 06882bb — V3 PR #36 merge commit)
**Status:** RECON ONLY — no code changes; no Q-answers yet
**Scope reminder:** CombatRouting deletion + LEGACY_SHADOW define retirement + V3 carry-forward cleanup (`pendingImpulseSummary`, `fallbackToLegacy`, vestigial reconcile field).
**Amendments since v1:** 7 items added/corrected per reviewer feedback (1 CRITICAL field-name + L7 latch survival; 2 HIGH orphan + snapshot trace; 1 HIGH comment surface; 2 MEDIUM doc/wire-format).

---

## 1. Symbol deletion surface

### 1.1 `BuddahPredictionCombatRouting` (entire file)
- **File:** [`Assets/Scripts/New_Buddah/Integration/BuddahPredictionCombatRouting.cs`](Assets/Scripts/New_Buddah/Integration/BuddahPredictionCombatRouting.cs) (76 lines)
- **Production callers:** 0 (V3 PR #36 verified).
- **Disposition:** delete file + meta. Comment cleanup is now Item 1.6 (broader surface than v1).

### 1.2 `BuddahPredictedMotor` symbols
| Symbol | Decl line | Other touchpoints | Disposition |
|---|---|---|---|
| `_legacyShadowScratch` field | motor.cs:138 | :261, :400 (resets), :463-:470 (compare), :1413, :1788, :1985-:1988 (writes) | delete |
| `_legacyImpulseDivCount` | motor.cs:139 | :466, :470 (++), :1432, :1449 (reset+log) | delete |
| `_legacyImpulseComparedCount` | motor.cs:140 | :466 (++), :1431, :1448 (log) | delete |
| `_impulseEventQueue` field | motor.cs:40 | :231, :420, :1316, :1329, :1977, :2040, :2397 | delete |
| `_shadowPreImpulsePendingSnapshot` field | motor.cs:99 | :420 (Copy), :1379 (passed to step) | delete (full chain dead — see Item 1.5) |
| `ConsumePendingImpulseEvents_LegacyShadow` method | motor.cs:1975 | :446 (call site under #if) | delete method + helper `ConsumeImpulseLegacyShadowEntry` (motor:1985+) |
| `TryApplyServerAuthoritativeImpulse` method | motor.cs:858 | called by Router.cs:58 (under #if), CombatRouting.cs:40 (deleted) | delete |
| `TryQueueImpulseEvent` method | motor.cs:1311 | called by motor:875, :1308 | delete |
| `QueueImpulseEventTargetRpc` method | motor.cs:1291 | called by motor:876 | delete |
| `[D-IMP LEG FATAL]` emit | motor.cs:469 (`Debug.LogError`) | inside `Shadow_CompareAndReport` legacy block | delete |
| `[D-IMP LEG HEARTBEAT]` emits | motor.cs:1431, :1448 | inside HB heartbeat region under #if | delete |

### 1.3 `BuddahPredictedImpulseEventQueue` (entire file)
- **File:** [`Assets/Scripts/New_Buddah/Core/BuddahPredictedImpulseEventQueue.cs`](Assets/Scripts/New_Buddah/Core/BuddahPredictedImpulseEventQueue.cs)
- **Type def:** line 6 (`public sealed class`)
- **Helper method `CopyPendingSnapshot`:** line 53
- **Production users:** only `BuddahPredictedMotor._impulseEventQueue` (deleted above) + comment refs in `BuddahPredictionEventChannel.cs:9, :103` (V2b Step 0 redesign mirror note — comment cleanup only)
- **Disposition:** delete file + meta.

### 1.4 `BuddahPredictedImpulseRingSnapshot` (orphan check)
- **File:** [`Assets/Scripts/New_Buddah/Core/BuddahPredictedImpulseRingSnapshot.cs`](Assets/Scripts/New_Buddah/Core/BuddahPredictedImpulseRingSnapshot.cs)
- **Type def:** line 3 (struct)
- **Sole consumer:** `BuddahPredictedReconcileData.ImpulseQueueState` field (see Item 5)
- **Disposition (CONFIRMED orphan):** if Item 5 deletes the reconcile field, this type has zero references → delete file + meta.

### 1.5 `BuddahImpulseStep` orphan + snapshot chain (NEW — reviewer HIGH)
**Trace** (verified via grep — 0 production callers, 2 historical comment refs):

```
_shadowPreImpulsePendingSnapshot (motor.cs:99)
  ← write at motor.cs:420         (CopyPendingSnapshot from queue)
  → read at motor.cs:1379         (passed as `impulsePendingSnapshot:` named arg)
  → BuddahPredictionTickContext   (motor.cs:1368 ctor → ctx.ImpulsePendingSnapshot field)
  → BuddahImpulseStep.Run         (BuddahImpulseStep.cs:30 reads `ctx.ImpulsePendingSnapshot`)
```

`BuddahHandoffStep` and `BuddahLocomotionStep` do NOT read `ctx.ImpulsePendingSnapshot` — verified.

**`BuddahImpulseStep.Run` was killed in V2b Step 1 Q4 amendment** (motor.cs:105 + :429 comments confirm). The class declaration at [`BuddahImpulseStep.cs:21`](Assets/Scripts/New_Buddah/Simulation/BuddahImpulseStep.cs#L21) is now dead — 0 callers.

**Cascade deletions (full snapshot chain dead):**
- [`BuddahImpulseStep.cs`](Assets/Scripts/New_Buddah/Simulation/BuddahImpulseStep.cs) — entire file + meta
- `BuddahPredictionTickContext.ImpulsePendingSnapshot` field at [`BuddahPredictionTickContext.cs:24`](Assets/Scripts/New_Buddah/Core/BuddahPredictionTickContext.cs#L24)
- `BuddahPredictionTickContext` constructor `impulsePendingSnapshot` parameter at [`:65`](Assets/Scripts/New_Buddah/Core/BuddahPredictionTickContext.cs#L65) + ctor body assign at [`:94`](Assets/Scripts/New_Buddah/Core/BuddahPredictionTickContext.cs#L94)
- `_shadowPreImpulsePendingSnapshot` field (motor.cs:99) + write site (:420 — `CopyPendingSnapshot` call dies with the queue)
- `motor.cs:1379` named arg removal (3 BuildTickContext callers at :425/:489/:597 unaffected — they pass the same field)

**Disposition:** delete entire snapshot chain in V4. Q3 implementation must verify all 3 BuildTickContext call sites compile clean after the param drops out of the ctor signature.

### 1.6 Comment / docstring cleanup surface (NEW — reviewer HIGH; broader than v1)
Add post-deletion grep mandate to PR description: `BuddahPredictionCombatRouting` / `_legacyShadowScratch` / `_impulseEventQueue` / `BuddahImpulseStep` returns 0 hits in `Assets/**/*.cs` post-merge.

| File | Line(s) | Reference(s) | Action |
|---|---|---|---|
| `BuddahPredictionRouter.cs` | 11 | `<see cref>` to CombatRouting.TryRouteImpulse | rewrite docstring to remove cref |
| `BuddahPredictionRouter.cs` | 15-16 | comment about `motor.TryApplyServerAuthoritativeImpulse` + `_legacyShadowScratch` | rewrite Tier 1 comment block |
| `BuddahPredictionRouter.cs` | 32 | comment about V4 retirement of CombatRouting + LEGACY_SHADOW | rewrite (now describes done state) |
| `BuddahPredictionRouter.cs` | **57-58** | **NOT just comment — line 58 is the actual `motor.TryApplyServerAuthoritativeImpulse` CALL inside `#if BUDDAH_PREDICTION_LEGACY_SHADOW`** | strip `#if`/`#endif` (lines 56, 59) AND delete the call body (lines 57-58) |
| `BuddahPredictionCombatAdapter.cs` | 17, 19 | summary docstring referencing LEGACY_SHADOW + CombatRouting | rewrite |
| `BuddahPredictionCombatAdapter.cs` | 64 | comment cross-referencing `TryApplyServerAuthoritativeImpulse` | rewrite |
| `BuddahPredictionCombatAdapter.cs` | 91 | comment about LEGACY_SHADOW observation shadow | rewrite (NEW is sole authority post-V4) |
| `BuddahPredictionCombatAdapter.cs` | 99 | comment cross-referencing `TryApplyServerAuthoritativeImpulse` | rewrite |
| `BuddahPredictionEventChannel.cs` | 9 | "mirrors `BuddahPredictedImpulseEventQueue.cs`" | rewrite (no longer mirrors a deleted file) |
| `BuddahPredictionEventChannel.cs` | 103 | "Mirrors `BuddahPredictedImpulseEventQueue.ConsumeReady`" | rewrite |
| `BuddahImpulseStep.cs` | 8 | comment about queue ConsumeReady reverse-walk | gone with file delete |
| `BuddahPredictedMotor.cs` | 105 | "BuddahImpulseStep.Run was killed" historical note | delete (action complete in V4) |
| `BuddahPredictedMotor.cs` | 429 | "Phase 4b V2b Step 1 — Q4 amendment: BuddahImpulseStep.Run early-shadow call" | delete |

PR deliverable adds an explicit post-merge grep mandate: any of the above identifier strings appearing in remaining `Assets/**/*.cs` after V4 merge = re-open scope.

---

## 2. `BUDDAH_PREDICTION_LEGACY_SHADOW` define audit

**Total** `#if` blocks in production code: **12** across 3 files.

### 2.1 ProjectSettings
- **`ProjectSettings/ProjectSettings.asset` line 827** — `Standalone:` define list contains `BUDDAH_PREDICTION_LEGACY_SHADOW`. This is the canonical authority — Unity's Editor uses Standalone defines.
- **No separate Editor entry observed.**
- **Disposition:** strip `;BUDDAH_PREDICTION_LEGACY_SHADOW` substring from Standalone line.

### 2.2 Block map (CRITICAL CORRECTION on motor.cs:382)
| File | Line | Block contents | Post-V4 disposition |
|---|---|---|---|
| `BuddahPredictionRouter.cs` | 56-59 | OLD-feed `motor.TryApplyServerAuthoritativeImpulse` line in Tier 1 | delete entire `#if/#endif` wrapper block (3-line body) |
| `BuddahPredictionCombatRouting.cs` | 51 | fan-out try/catch under #if | gone with file delete |
| `BuddahPredictedMotor.cs` | 130 | `_legacyShadowScratch` decl region | delete entire region (covers field + counters) |
| **`BuddahPredictedMotor.cs`** | **143** | L7 latch tracker (`_combatAdapterInitialized`) — corrected field name | **see Item 2.3 — strip wrapper, KEEP field decl** |
| `BuddahPredictedMotor.cs` | 260 | `_legacyShadowScratch = default` reset | delete line |
| **`BuddahPredictedMotor.cs`** | **382-396** | L7 late-bind block — `bootstrap.CombatAdapter.MarkReady()` gate | **see Item 2.3 — CRITICAL: strip wrapper only, KEEP body** |
| `BuddahPredictedMotor.cs` | 399 | `_legacyShadowScratch = default` reset (second) | delete line |
| `BuddahPredictedMotor.cs` | 445 | `ConsumePendingImpulseEvents_LegacyShadow` call + LEG FATAL compare block (459-471) | delete entire block |
| `BuddahPredictedMotor.cs` | 1409 | `_legacyShadowScratch.ImpulseRan` clause in shouldRun-active expression | delete clause (single line in OR chain) |
| `BuddahPredictedMotor.cs` | 1430 | `[D-IMP LEG HEARTBEAT]` idle-tick log | delete block |
| `BuddahPredictedMotor.cs` | 1447 | `[D-IMP LEG HEARTBEAT]` active-tick log | delete block |
| `BuddahPredictedMotor.cs` | 1963 | `ConsumePendingImpulseEvents_LegacyShadow` method definition + helper | delete entire region |

### 2.3 CRITICAL — L7 latch survival (motor.cs:143 decl + :382-396 body) — corrected from v1

**v1 ERROR:** I named the field `_combatAdapterLateBound` and suggested "likely delete with #if since the latch is OLD-path concept". BOTH are wrong.

**Correct facts (verified by grep + read of motor.cs:148/:389/:392):**

```csharp
// motor.cs:148
private bool _combatAdapterInitialized;

// motor.cs:382-396 (current state — wrapped in #if BUDDAH_PREDICTION_LEGACY_SHADOW)
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

**Why the latch must survive V4** (key insight reviewer flagged):
- L7 latch is **NOT LEG-axis-specific.** It exists to defer adapter's first `MarkReady()` past `BuddahMovementModeSwitcher`'s double-ApplyMode 4-ClearAll spawn sequence.
- Post-V4, NEW path is still rb-write authority; ClearAll still triggers; CombatAdapter still needs the latch to gate `MarkReady()` correctly.
- The `#if BUDDAH_PREDICTION_LEGACY_SHADOW` wrapper is a V2a-era historical artifact (when NEW was shadow and being wiped didn't matter — the latch was conditional on dual-path running).

**Q3 design REQUIREMENT (CRITICAL):**
- **STRIP** the `#if BUDDAH_PREDICTION_LEGACY_SHADOW` / `#endif` wrappers (lines 382, 396).
- **KEEP** the body unchanged (lines 383-395 — comment + if-block).
- **KEEP** the `_combatAdapterInitialized` field declaration at motor.cs:148. (The motor.cs:143 entry in the v1 block-map referred to the same region; the field decl line is :148.)

**Failure mode if implementer follows v1's "likely delete with #if" verbatim:**
- `bootstrap.CombatAdapter.MarkReady()` never fires.
- CombatAdapter sits with `_isReady=false` indefinitely (or whatever default state).
- Channel enqueues during the early window (post-spawn, pre-some-other-trigger) get dropped silently.
- Strict gate misses it: D-LOC FATAL=0 stays clean (no compare reference); LEG axis is gone.
- Visual smoke misses it: push test typically happens mid-game, NOT during the spawn-window race condition that L7 was designed to fix.
- **Highest-risk regression in V4.** Q3 design must include a callout test: spawn-window impulse fires within first N ticks → confirm channel `[CommandBus]:Recv` count > 0 in raw log (Rule 1 sub-rule D applies — pre-dump self-verify).

---

## 3. `pendingImpulseSummary` consumer audit

### 3.1 Field declaration
- [`BuddahPredictionDebugState.cs:127`](Assets/Scripts/New_Buddah/Debug/BuddahPredictionDebugState.cs#L127) — `public string pendingImpulseSummary = "none";`

### 3.2 Writers (all delete with `_impulseEventQueue`)
- `BuddahPredictedMotor.cs:232`, `:1330`, `:2398` — all sourced from `_impulseEventQueue.BuildPendingSummary()`

### 3.3 Readers
**Single live reader: `BuddahPredictionDebugOverlay.cs:106`** — concatenates into HUD overlay text. Development-only, not user-facing.

### 3.4 Prefab serialized value
- `Buddah.prefab:1329` — `pendingImpulseSummary: none` (default — not editor-modified)

### 3.5 Q0 lean implications
- (A) Add `BuildPendingSummary()` on channel: ~15 LOC; preserves overlay diagnostic.
- (B) Drop field entirely: cleanup of overlay + DebugState + prefab YAML key. Overlay loses one diagnostic line.
- (C) Stub-only: not aligned with V4 cleanup spirit.

**Lean: (A) — channel summary builder.** Aligns with V3 design Q&A's documented "transitional state" expectation.

---

## 4. `fallbackToLegacy` consumer audit

### 4.1 Declaration
- [`BuddahPredictionImpulseDebugBox.cs:17`](Assets/Scripts/New_Buddah/Debug/BuddahPredictionImpulseDebugBox.cs#L17) — `[SerializeField] private bool fallbackToLegacy = true;`

### 4.2 Code-path readers
**ZERO.** Comment at `:46` confirms field is no-op post-V3.

### 4.3 Scene/prefab serialized values
- `RaceMap.unity:10955` — `fallbackToLegacy: 1`. No prefab variants.

### 4.4 Q1 lean implications
**Lean: (A) — remove the field.** CLAUDE.md hard-stop's underlying concern (still-used field) is provably resolved. Unknown YAML key drops silently on next scene save.

---

## 5. Reconcile data field disposition (`ImpulseQueueState`)

### 5.1-5.3 (unchanged from v1)
- Field: `BuddahPredictedReconcileData.cs:31` — `public BuddahPredictedImpulseRingSnapshot ImpulseQueueState;`
- Only writer: motor.cs:303 (`= default`)
- Only readers: motor.cs:649 (discard), motor.cs:683 (debug log of always-zero values)
- **Vestigial confirmed.**

### 5.4 Wire format implication — peer coordination plan (NEW — reviewer MEDIUM)

`BuddahPredictedReconcileData` is a FishNet `[Reconcile]`-marked struct. Removing `ImpulseQueueState` changes serialized layout. Cross-version peer mismatch = silent reconcile data corruption (deserialize reads wrong field offsets).

**Deployment coordination plan (Q3 to ratify, V4 PR description must include explicit section):**
1. **Atomic deployment requirement** — V4 deletes the field; pre-V4 client + V4 host (or vice-versa) reading each other's reconcile packets = undefined behavior. No backward-compat shim planned.
2. **Steam build chain** — V4 PR merge to `main` triggers a Steam build push. All test sessions must verify both peers have pulled the post-V4 build before joining a lobby. Ad-hoc playtests across an in-flight V4 rollout window are **forbidden** until both peers confirm matching version hash.
3. **Lobby version handshake** — current Steam lobby flow does NOT include explicit prediction-protocol version negotiation. Recon flag: V5 PRE-WORK should consider adding a `predictionProtocolVersion` integer to lobby metadata + connection-time handshake. V4 itself does NOT add this (out of scope) — interim mitigation is calendar-based coordination + Steam version lock at PR merge time.
4. **Rollback flow** — if V4 is reverted post-merge, all peers must downgrade simultaneously. Same atomic requirement, reverse direction.
5. **PR description mandate** — V4 PR body has a "Deployment coordination" section spelling items 1-4 out for the merging reviewer's checklist.

### 5.5 Type orphan cascade
With `ImpulseQueueState` removed, `BuddahPredictedImpulseRingSnapshot` becomes orphan (Item 1.4 cross-confirmed). Delete file + meta.

---

## 6. `Docs/prediction-refactor-plan/` chapter disposition (NEW — reviewer MEDIUM)

Four chapters cite symbols V4 deletes. Disposition decision needed in Q3 design:

| Chapter | Cited symbol(s) | Disposition options |
|---|---|---|
| `02-directory-structure.md` | `BuddahPredictionCombatRouting` in dir layout | (a) update inline; (b) footer "historical, see Phase 4b"; (c) move to `archive/` folder |
| `07-...` | (per reviewer note — verify scope in design) | same options |
| `09-integration-adapters.md` | `BuddahPredictionCombatRouting` adapter pattern | same options |
| `12-migration-sequence.md` | references CombatRouting in cut-over plan | same options |

**Reviewer-suggested lean: (b) footer note.** Reasoning: chapters describe historical refactor design intent; rewriting them inline loses the "why" trail. A footer like _"As of Phase 4b V4 (date), CombatRouting is deleted; see `Docs/phase-gates/archive/v4-contract.md` for closure record"_ keeps the historical text + adds a forward-pointer.

Q3 design ratifies: (a) / (b) / (c). Lean stated, not locked.

---

## 7. (NEW — folded into Item 5.4 above — wire format peer coordination)

See Item 5.4. Recap: V4 PR description deliverable adds an explicit "Deployment coordination" section. Q3 design body must spell out: atomic build requirement, Steam version lock, lobby handshake (V5 PRE-WORK flag), rollback flow.

---

## Summary — what Q0–Q4 must answer (preview, not answers)

| Q | Lean | Notes from amendments |
|---|---|---|
| **Q0** `pendingImpulseSummary` resolution | (A) Add channel `BuildPendingSummary()` | Single live consumer; preserves dev diagnostic. |
| **Q1** `fallbackToLegacy` removal | (A) Remove field | 0 code readers; 1 scene serialized value drops silently. |
| **Q2** Adapter caching | (B) Defer to Phase 8 | V4 cleanup-heavy; perf opt without profiler is scope mixing. |
| **Q3** Define retirement procedure | (A) Strip define + delete `#if` blocks atomically | **CRITICAL caveat — motor.cs:382 L7 latch:** STRIP wrapper only, KEEP body (`bootstrap.CombatAdapter.MarkReady()` call must survive — gate against Switcher's 4-ClearAll spawn-window race). Q3 design body must call this out explicitly + add spawn-window smoke probe to strict gate. |
| **Q4** Strict gate recalibration | (A) D-LOC FATAL only | LEG axis disappears; D-LOC FATAL=0 + visual smoke + counter sanity + **NEW spawn-window probe** (per Q3 caveat). |

**Additional design-Q items surfaced by amendments:**
- **Snapshot chain cascade (Item 1.5):** Q3 implementation must trace `BuddahImpulseStep.cs` + `BuddahPredictionTickContext.ImpulsePendingSnapshot` field/ctor-param + `_shadowPreImpulsePendingSnapshot` field together; verify all 3 BuildTickContext callers compile clean.
- **Comment cleanup grep mandate (Item 1.6):** PR description includes post-merge grep verification for 4 dead identifier strings.
- **Refactor-plan docs disposition (Item 6):** Q3 ratifies (a)/(b)/(c); lean (b) footer.
- **Wire format peer coordination (Item 5.4):** PR description includes explicit "Deployment coordination" section; V5 PRE-WORK flag for lobby version handshake.

Awaiting Stage 2 sign-off (re-verification of v2 amendments) before writing Q-answer proposals (Stage 3).
