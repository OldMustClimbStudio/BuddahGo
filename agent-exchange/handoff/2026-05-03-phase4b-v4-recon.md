# phase4b-v4 — Recon Report

**Branch:** feat/phase4b-v4-cleanup (cut from dev @ 06882bb — V3 PR #36 merge commit)
**Status:** RECON ONLY — no code changes; no Q-answers yet
**Scope reminder:** CombatRouting deletion + LEGACY_SHADOW define retirement + V3 carry-forward cleanup (`pendingImpulseSummary`, `fallbackToLegacy`, vestigial reconcile field).

---

## 1. Symbol deletion surface

Grep evidence for every symbol marked for deletion in V4 contract. Production caller count = 0 across all targets (only doc/comment cross-refs remain; those rewritten alongside deletion).

### 1.1 `BuddahPredictionCombatRouting` (entire file)
- **File:** [`Assets/Scripts/New_Buddah/Integration/BuddahPredictionCombatRouting.cs`](Assets/Scripts/New_Buddah/Integration/BuddahPredictionCombatRouting.cs) (76 lines)
- **Production callers:** 0 (V3 PR #36 verified). Only references in code:
  - `BuddahPredictionRouter.cs:11` — `<see cref>` docstring
  - `BuddahPredictionRouter.cs:32` — comment about V4 retirement
  - `BuddahPredictionCombatAdapter.cs:19` — comment about old fan-out site
- **Disposition:** delete file + meta. Update 3 docstring/comment crefs.

### 1.2 `BuddahPredictedMotor` symbols
| Symbol | Decl line | Other touchpoints | Disposition |
|---|---|---|---|
| `_legacyShadowScratch` field | motor.cs:138 | :261, :400 (resets), :463-:470 (compare), :1413, :1788, :1985-:1988 (writes) | delete |
| `_legacyImpulseDivCount` | motor.cs:139 | :466, :470 (++), :1432, :1449 (reset+log) | delete |
| `_legacyImpulseComparedCount` | motor.cs:140 | :466 (++), :1431, :1448 (log) | delete |
| `_impulseEventQueue` field | motor.cs:40 | :231, :420, :1316, :1329, :1977, :2040, :2397 | delete |
| `_shadowPreImpulsePendingSnapshot` field | motor.cs:99 | :420 (Copy), :1379 (passed to step) | delete (orphans with queue) |
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

---

## 2. `BUDDAH_PREDICTION_LEGACY_SHADOW` define audit

**Total** `#if` blocks in production code: **12** across 3 files (CombatRouting deletion takes one; CombatAdapter has comment refs only, no `#if` block).

### 2.1 ProjectSettings
- **`ProjectSettings/ProjectSettings.asset` line 827** — `Standalone:` define list contains `BUDDAH_PREDICTION_LEGACY_SHADOW`. This is the canonical authority — Unity's Editor uses Standalone defines.
- **No separate Editor entry observed** (other platforms `Stadia`, `VisionOS`, `iPhone` etc. don't define it).
- **Disposition:** strip `;BUDDAH_PREDICTION_LEGACY_SHADOW` substring from Standalone line. Single ASCII edit.

### 2.2 Block map
| File | Line | Block contents | Post-V4 disposition |
|---|---|---|---|
| `BuddahPredictionRouter.cs` | 56-59 | OLD-feed `motor.TryApplyServerAuthoritativeImpulse` line in Tier 1 | delete entire `#if/#endif` wrapper block (3 lines body) |
| `BuddahPredictionCombatRouting.cs` | 51 | fan-out try/catch under #if | gone with file delete |
| `BuddahPredictedMotor.cs` | 130 | `_legacyShadowScratch` decl region | delete entire region (covers field + counters) |
| `BuddahPredictedMotor.cs` | 143 | L7 latch tracker (`_combatAdapterLateBound`) — **CHECK: V4 adapter still needs late-bind?** | likely delete with #if since the latch is OLD-path concept |
| `BuddahPredictedMotor.cs` | 260 | `_legacyShadowScratch = default` reset (in InitializeMotor or similar) | delete line |
| `BuddahPredictedMotor.cs` | 382 | Phase 4b V2a L7 late-bind for CombatAdapter | **CHECK design Q3:** is adapter late-bind still needed post-V4? Adapter `_bootstrap` field still exists; this block writes to it lazily. Probably needs to stay if adapter has lazy bootstrap field; just strip the `#if` wrapper. |
| `BuddahPredictedMotor.cs` | 399 | `_legacyShadowScratch = default` reset (second) | delete line |
| `BuddahPredictedMotor.cs` | 445 | `ConsumePendingImpulseEvents_LegacyShadow` call + LEG FATAL compare block (459-471) | delete entire block |
| `BuddahPredictedMotor.cs` | 1409 | `_legacyShadowScratch.ImpulseRan` clause in shouldRun-active expression | delete clause (single line in OR chain) |
| `BuddahPredictedMotor.cs` | 1430 | `[D-IMP LEG HEARTBEAT]` idle-tick log | delete block |
| `BuddahPredictedMotor.cs` | 1447 | `[D-IMP LEG HEARTBEAT]` active-tick log | delete block |
| `BuddahPredictedMotor.cs` | 1963 | `ConsumePendingImpulseEvents_LegacyShadow` method definition + helper | delete entire region |

**Sub-conditional pattern:** several blocks use `#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW && BUDDAH_PREDICTION_LEGACY_SHADOW` (lines 130, 445, 1963). Stripping LEGACY_SHADOW collapses those to `#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW` — but since the block bodies are LEG-axis-specific, the entire block goes.

**KEY FINDING — motor.cs:382/L7 late-bind:** The `_combatAdapterLateBound` latch logic on line 382-397 lives under `#if BUDDAH_PREDICTION_LEGACY_SHADOW`. Q3 design needs to clarify: is this latch still required for V4 NEW-only path? Adapter's `_bootstrap` lazy reference still needs to be set somewhere; if the latch is removed, adapter must self-acquire bootstrap on first use. Verify in Q3 implementation step.

---

## 3. `pendingImpulseSummary` consumer audit

### 3.1 Field declaration
- [`BuddahPredictionDebugState.cs:127`](Assets/Scripts/New_Buddah/Debug/BuddahPredictionDebugState.cs#L127) — `public string pendingImpulseSummary = "none";` (default value)

### 3.2 Writers (all delete with `_impulseEventQueue`)
- `BuddahPredictedMotor.cs:232` — `BuildPendingSummary()` from queue (in `OnReconcile`?)
- `BuddahPredictedMotor.cs:1330` — write inside `TryQueueImpulseEvent`
- `BuddahPredictedMotor.cs:2398` — write inside another path

### 3.3 Readers (consumer audit — Q0 decision input)
**Single live reader: `BuddahPredictionDebugOverlay.cs:106`** — concatenates into HUD overlay text:
```csharp
$"Pending Impulses: {state.pendingImpulseSummary}\n"
```
This is an in-game debug HUD overlay (development-only, not user-facing).

### 3.4 Prefab serialized value
- `Buddah.prefab:1329` — `pendingImpulseSummary: none` (default — not editor-modified)

### 3.5 Q0 lean implications
- (A) Add `BuildPendingSummary()` on channel: ~15 LOC; preserves overlay diagnostic. Channel's `_pending` list trivially iterable.
- (B) Drop field entirely: must also remove from DebugState + DebugOverlay.cs:106 line + prefab YAML key. Overlay loses one diagnostic line.
- (C) Stub-only: leaves dead public surface; not aligned with V4 cleanup spirit.

**Lean: (A) — channel summary builder.** Reasoning: cost is small, debug overlay is the only consumer but it's actually used by devs investigating prediction issues. Aligns with V3 design Q&A's documented "transitional state" expectation that V4 would land the channel-side builder.

---

## 4. `fallbackToLegacy` consumer audit

### 4.1 Declaration
- [`BuddahPredictionImpulseDebugBox.cs:17`](Assets/Scripts/New_Buddah/Debug/BuddahPredictionImpulseDebugBox.cs#L17) — `[SerializeField] private bool fallbackToLegacy = true;`

### 4.2 Code-path readers
**ZERO.** Confirmed via grep: only the comment at `BuddahPredictionImpulseDebugBox.cs:46` ("'fallbackToLegacy' serialized field is now ignored — router unconditionally falls back to BuddahMovement RPC for Legacy buddah victims") which is a deliberate V3 carry-forward marker. The field gates nothing post-V3.

### 4.3 Scene/prefab serialized values
- `RaceMap.unity:10955` — `fallbackToLegacy: 1` (true) on the scene's `BuddahPredictionImpulseDebugBox` instance.
- No prefab variants observed.

### 4.4 Q1 lean implications
- (A) Remove field entirely. Unity drops the unknown YAML key on next scene save (silent — no console warning, no behavioral change because field was already no-op).
- (B) Mark `[Obsolete]`. Field stays serialized; Inspector shows strikethrough; potential future re-use unlikely.

**Lean: (A) — remove the field.** CLAUDE.md hard-stop on serialized field removal exists to protect against breaking scenes/prefabs that rely on the value. Post-V3, the field is provably dead (0 readers, comment confirms no-op). Q1 contract recommendation = (A).

---

## 5. Reconcile data field disposition (`ImpulseQueueState`)

### 5.1 Field declaration
- [`BuddahPredictedReconcileData.cs:31`](Assets/Scripts/New_Buddah/Core/BuddahPredictedReconcileData.cs#L31) — `public BuddahPredictedImpulseRingSnapshot ImpulseQueueState;`

### 5.2 Writer audit
- **`BuddahPredictedMotor.cs:303`** — `data.ImpulseQueueState = default;` — **only writer is a default/zero-init.** No real ring snapshot ever populated into reconcile data.

### 5.3 Reader audit
- **`BuddahPredictedMotor.cs:649`** — `_ = data.ImpulseQueueState;` — discard read (compiler null-suppression / linter hush).
- **`BuddahPredictedMotor.cs:683`** — `Debug.Log($"impHead={data.ImpulseQueueState.Head} impCount={data.ImpulseQueueState.Count}")` — debug log printing always-zero values.

**KEY FINDING — vestigial as confirmed by V2b Step 1 Q2:** field has zero real producers and only a debug-log reader showing always-zero values. Safe to delete entirely.

### 5.4 Wire format implication
`BuddahPredictedReconcileData` is a FishNet `[Reconcile]`-marked struct. Removing a field changes serialized layout. **All peers must run V4 simultaneously.** No backward-compat concern documented in contract — full release rebuild expected. Mention as Q3 implementation note.

### 5.5 Type orphan cascade
With `ImpulseQueueState` removed, `BuddahPredictedImpulseRingSnapshot` (struct in own file) becomes orphan → delete file + meta (cross-confirmed in Item 1.4).

---

## Summary — what Q0–Q4 must answer (preview, not answers)

| Q | Lean | Reasoning preview |
|---|---|---|
| **Q0** `pendingImpulseSummary` resolution | (A) Add channel `BuildPendingSummary()` | Single live consumer (`BuddahPredictionDebugOverlay.cs:106`); cost ~15 LOC; preserves dev diagnostic. (B) costs cleanup of overlay + DebugState + prefab YAML key for marginal LOC saving. |
| **Q1** `fallbackToLegacy` removal | (A) Remove field | 0 code-path readers (V3 already neutered); 1 scene serialized value at `RaceMap.unity:10955` will drop silently on next save. CLAUDE.md hard-stop's underlying concern (still-used field) is provably resolved. |
| **Q2** Adapter caching | (B) Defer to Phase 8 | Cleanup-heavy V4 + perf opt without profiler measurement = scope mixing. Wait for dedicated Phase 8 perf pass. |
| **Q3** Define retirement procedure | (A) Strip define + delete `#if` blocks in same commit | Atomic deletion; PR-review grep verifies completeness. (B)'s "intentional compile error" pattern is harder to review and mid-PR risks broken master if force-pushed wrong. **Caveat:** motor.cs:382 L7 late-bind logic needs implementation-level decision (keep adapter lazy late-bind path? Strip wrapper but keep body?) — defer to Q3 design body. |
| **Q4** Strict gate recalibration | (A) D-LOC FATAL only | Q4 amendment in V2b Step 1 already dropped impulse axis from D-LOC. Post-V4 LEG axis disappears. D-LOC FATAL=0 + visual smoke + counter sanity remains the safety net until V5 LatencySim adds new probe. |

Awaiting sign-off before writing Q-answer proposals (Stage 3).
