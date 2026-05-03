# V1 — CombatAdapter Scaffold + L7 Latch Contract (ARCHIVED — Phase B backfill)

**Phase ID:** phase4b-v1
**Branch:** feat/phase4b-v1-combat-adapter-scaffold
**Risk:** LOW (scaffold-only; no rb-write authority change)
**Status:** ✅ MERGED — PR #29 to dev @ `39c5217` (2026-04-20)

> **Backfill note:** original contract was not authored at the time (predates Phase A Phase Gate System docs). This archive entry is reconstructed retrospectively from `lessons-log.md` L7 + commit history (`45898d4 phase4b-v1: CombatAdapter scaffold + L7 Initialize() latch`) + README closed-phases attribution. Sign-off ledger reflects what is recoverable; gaps marked `(backfill)`.

---

## Scope (locked)

**In scope:**
- `BuddahPredictionCombatAdapter.cs` scaffold — class skeleton + `Initialize(BuddahPredictionCommandBus)` + `MarkReady()` + `IsReady` accessor + `TryRouteImpulse` stub
- L7 latch contract: adapter MUST NOT enqueue on `Awake/OnEnable/OnStartNetwork`; only on first `[Replicate]` tick post-Switcher-double-ApplyMode (after 4 `[CommandBus]:ClearAll` lines have all landed)
- Wire `Initialize()` from `BuddahPredictionBootstrap.Awake()`; wire `MarkReady()` from motor's first post-spawn `[Replicate]` tick
- No rb-writes; no LEGACY_SHADOW define yet (introduced V2a)

**Out of scope:**
- Dual-feed observation gate (V2a)
- Tick-stamping channel (V2b Step 0)
- Authority flip (V2b Step 1)

---

## Final outcome

✅ **MERGED — scaffold validated; L7 latch rule born.**

- PR #29 → `dev` @ `39c5217`
- Adapter wired via Bootstrap; `IsReady` gate prevents premature enqueue during Switcher's double-`ApplyMode` (4× `[CommandBus]:ClearAll` per spawn)
- No production rb-writes; observation-only

### Lessons added

- **L7** — Lifecycle hooks can silently double-fire state-clearing calls. `BuddahMovementModeSwitcher.Awake()` + `OnEnable()` both call `ApplyRuntimeMode(force:true)` → 4 `[CommandBus]:ClearAll` lines per spawn instead of 2 expected. Harmless at V1 (no enqueues), but **any future adapter that enqueues during init MUST gate on `MarkReady()`**, OR the second ClearAll wave will nuke legitimate pending events. Promoted to log-only; revisited at V4 with concrete adapter code.

---

## Sign-off ledger (final, partial — backfill)

| Stage | Date | Signer | Notes |
|---|---|---|---|
| Kickoff | 2026-04-20 (backfill) | Yonezawa | scaffold-only scope; pre-Phase-Gate-System era — no formal kickoff doc |
| Recon | (backfill — none authored) | n/a | scope was self-evident (scaffold); no recon report exists |
| Design | (backfill — none authored) | n/a | adapter shape derived directly from `Docs/prediction-refactor-plan/09-integration-adapters.md` |
| Implementation | 2026-04-20 | Yonezawa | commit `45898d4`. ~80 LOC adapter + Bootstrap wire + motor MarkReady site |
| Smoke | 2026-04-20 (backfill) | Yonezawa | Editor playmode validated `IsReady=false` until first post-spawn Replicate; ClearAll = 4 per spawn observed (informed L7) |
| Verify | (backfill — implicit) | n/a | scaffold; no FATAL gates yet |
| Merge | 2026-04-20 | Yonezawa | PR #29 merged to dev @ `39c5217`. L7 entry into lessons-log. |
