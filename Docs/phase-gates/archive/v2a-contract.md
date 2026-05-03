# V2a — Dual-Feed Inverted Shadow Contract (ARCHIVED — Phase B backfill)

**Phase ID:** phase4b-v2a
**Branch:** feat/phase4b-v2a-dual-feed-inverted-shadow
**Risk:** MEDIUM (dual-feed adds new code path alongside OLD; observation-only — OLD remains rb authority)
**Status:** ✅ MERGED — PR #31 to dev @ `fe17418` (2026-04-30)

> **Backfill note:** original contract was not authored at the time (predates Phase A Phase Gate System docs). This archive entry is reconstructed retrospectively from `lessons-log.md` (L16, L17 — born from V2a's blind spot, surfaced post-merge in V2a-fix) + commit history (`35b70d0 phase4b-v2a: dual-feed inverted shadow (CommandAdapter + LEGACY_SHADOW)`) + README closed-phases attribution.

---

## Scope (locked)

**In scope:**
- Introduce `BUDDAH_PREDICTION_LEGACY_SHADOW` ProjectSettings define
- NEW path adds `BuddahPredictionCombatAdapter.TryRouteImpulse` β-branch — emits `Target_EnqueueImpulse` RPC alongside OLD `_impulseEventQueue` server-local enqueue
- NEW drain `ConsumePendingImpulseEvents_InvertedShadow` reads channel via `TryDequeue` (FIFO ring buffer; pre-tick-stamping)
- Observation-only: OLD remains rb-authority; NEW writes scratch counter only
- HEARTBEAT field `inv-imp-compared` / `inv-imp-div` for observation-gate metrics
- `[D-IMP INV FATAL]` emit on volume divergence (`compared count` mismatch between OLD `imp-compared` and NEW `inv-imp-compared`)

**Out of scope:**
- Tick-stamping channel (V2b Step 0 — promoted urgent post L16)
- Authority flip (V2b Step 1)
- L9 ClampPlanarSpeed fix (Phase 8)

---

## Final outcome

✅ **MERGED, but observation gap discovered post-merge.**

- PR #31 → `dev` @ `fe17418`
- Host-only single-peer smoke: `inv-imp-compared=1, inv-imp-div=0, INV FATAL=0` — clean
- **2-peer LAN smoke (post-merge investigation):** CLIENT showed `inv-imp-compared=0` across 73 heartbeats despite OLD path counting `imp-compared=2` — gate vacuously satisfied (`div=0` is meaningless when counter never increments)
- Root cause: pure FIFO + drain-in-`RunInputs` broke under FishNet reconcile replay (per-replay `_legacyShadowScratch = default` reset clears `ImpulseRan` flag set by replay 1 → PostTick comparator reads empty scratch on last replay)

### Lessons added (post-merge, in V2a-fix PR #32)

- **L16** — Single-consumer FIFO channel + FishNet reconcile replay = structural blindness. `TryDequeue` removes entries permanently on replay 1; replays 2..N see empty channel. **Mitigation in V2a-fix:** relocate drain from `RunInputs` to `PostTick`. **Strategic mitigation deferred to V2b Step 0:** tick-stamp the channel + adopt `ConsumeReady(currentTick)` semantics so events stay queued across replays until their stamped tick passes.
- **L17** — Bidirectional phase-skew is the fingerprint of "observation-only" cross-phase drains, not real divergence. (Born from V2a-fix Path B verification, but root cause traces to V2a's drain-site choice.)

### Carry-forward delivered to V2a-fix / V2b Step 0

- **V2a-fix PRE-WORK:** drain relocation `RunInputs → PostTick` (L16 mitigation, transitional)
- **V2b Step 0 PRE-WORK:** channel tick-stamping + `ConsumeReady` (L16 strategic mitigation; prerequisite for any V2b authority flip)

---

## Sign-off ledger (final, partial — backfill)

| Stage | Date | Signer | Notes |
|---|---|---|---|
| Kickoff | 2026-04-30 (backfill) | Yonezawa | dual-feed scope; pre-Phase-Gate-System era |
| Recon | (backfill — none authored) | n/a | dual-feed shape derived from refactor-plan §09 |
| Design | (backfill — none authored) | n/a | β-branch decision (cmd dual-emit) implicit in adapter code |
| Implementation | 2026-04-30 | Yonezawa | commit `35b70d0`. ProjectSettings define + adapter β-branch + motor inverted-shadow drain + HEARTBEAT prefix |
| Smoke | 2026-04-30 (host-only single peer; 2-peer not run pre-merge) | Yonezawa | Path A clean (compared=1, div=0); 2-peer deferred — became L16 discovery surface post-merge |
| Verify | (backfill — pre-merge limited to host-only) | n/a | strict-gate INV FATAL=0 gate met BUT vacuously — see L16 |
| Merge | 2026-04-30 | Yonezawa | PR #31 merged to dev @ `fe17418`. Discovered as observation-blind post-merge → V2a-fix opened |
