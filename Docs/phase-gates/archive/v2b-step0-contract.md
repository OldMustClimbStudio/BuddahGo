# V2b Step 0 — Tick-Stamp Channel Contract (ARCHIVED — Phase B backfill)

**Phase ID:** phase4b-v2b-step0
**Branch:** feat/phase4b-v2b-step0-tick-stamp
**Risk:** MEDIUM (channel data-shape change + drain-semantics change; observation-only — OLD still rb-authority)
**Status:** ✅ MERGED — PR #33 to dev @ `49e3c2f` (2026-05-02)

> **Backfill note:** original contract was not authored at the time. Reconstructed from `lessons-log.md` L18 + commit history (`fb9d055 phase4b-v2b-step0: tick-stamp BuddahPredictionEventChannel<T>` + fix-2 `0e67d2e` + fix-3 `90e594f`) + V2b Step 1 archive contract cross-references + README closed-phases attribution.

---

## Scope (locked)

**In scope:**
- `BuddahPredictionEventChannel<T>.Entry` adds `EventTick` (server-canonical clock) field — replay-safe drain per L16/L17 strategic mitigation
- New API `ConsumeReady(uint currentTick, ConsumeCallback callback)` replaces `TryDequeue`. Entries with `EventTick > currentTick` stay queued across replays; entries with `EventTick ≤ currentTick` presented to callback (callback returns true → consume + RememberLogicalId; false → leave pending)
- `[Obsolete]` decorator on legacy `TryDequeue` — V4 will delete
- Adapter β-branch stamps `cmd.EventTick = TimeManager.LocalTick` (server-side; cross-RPC); CLIENT uses `cmd.EventTick` verbatim (no client-local restamping per L17 phase-skew root cause)
- Motor's NEW drain rewritten to `ConsumeReady`
- HEARTBEAT prefix renamed `inv-` semantics ready (actual rename to `leg-` deferred to V2b Step 1 authority flip)
- Strict `FATAL = 0` gate post-Step-0 (no phase-skew tolerance going forward)

**Out of scope:**
- Authority flip (V2b Step 1)
- LogicalId dedup (Step 1 Q0)
- LEGACY_SHADOW define retirement (V4)

---

## Final outcome

✅ **STRICT PASS, MERGED — but with side-effect mirror discovery surface.**

- PR #33 → `dev` @ `49e3c2f` — 3 implementation commits: `fb9d055` (initial tick-stamp) + `0e67d2e` (fix-2: INV compare from PostTick → RunInputs) + `90e594f` (fix-3: β-branch server-local enqueue + IsPredictionModeActive gate)
- Path A host-only: clean
- Path B 2-peer LAN initial: CLIENT clean (`inv-imp-compared=11, FATAL=0`); HOST 11 reverse FATAL (`ranOld=True ranNew=False`)
- Volume audit: HOST OLD `imp-compared=9` vs NEW `inv-imp-compared=9` — counter aligned, but per-tick FATAL because HOST-side server-replica of CLIENT-owned victim never received NEW-channel entry. Adapter β-branch only emitted RPC; missed server-local enqueue mirror that OLD's `TryApplyServerAuthoritativeImpulse` always performed
- **Fix-3 (commit `90e594f`):** β-branch adds server-local enqueue alongside RPC (dual-emit topology) + adds `bootstrap.IsPredictionModeActive()` gate (parity with OLD path)
- Re-smoke post fix-3: STRICT PASS — `FATAL = 0` both peers; `inv-imp-compared` matches OLD `imp-compared` per peer

### Lessons added

- **L18** — Dual-path migration must mirror OLD's full side-effect surface before tightening strict FATAL gate. OLD's `TryApplyServerAuthoritativeImpulse` does six things unconditionally beyond the RPC: server-local `TryQueueImpulseEvent`, RPC to Owner, `IsPredictionModeActive()` gate, cmd-side `_nextImpulseEventId++`, 6 DebugState writes, 3 LogVerbose lines. NEW adapter mirrored only the RPC. Items 1+3 produce real cross-peer divergence under strict FATAL gate. **Rule:** any dual-path migration must perform line-by-line OLD-side side-effect audit before tightening strict gate. Categorize each as (a) compare-relevant — mirror immediately, (b) observation-only — mirror or document drop with explicit justification, (c) architecturally moot — explain why NEW design makes it irrelevant.

### Carry-forward delivered to V2b Step 1

- **Cross-fire dedup decision (Step 1 Q0):** dead `_recentEventIds` check + cmd-side EventId field
- **DebugState mirror or audit-and-drop decision (Step 1 deferred to V3):** 6 DebugState fields written by OLD path; NEW must mirror or explicitly drop with justification

---

## Sign-off ledger (final, partial — backfill)

| Stage | Date | Signer | Notes |
|---|---|---|---|
| Kickoff | 2026-05-02 (backfill) | Yonezawa | tick-stamp scope; pre-Phase-Gate-System formal kickoff |
| Recon | 2026-05-02 (backfill — implicit) | n/a | scope = channel API change + adapter call-site update + motor drain rewrite |
| Design | 2026-05-02 (backfill — implicit) | n/a | `EventTick` field + `ConsumeReady` semantics chosen as L16/L17 strategic mitigation |
| Implementation | 2026-05-02 | Yonezawa | commits `fb9d055` + `0e67d2e` (fix-2 RunInputs relocation) + `90e594f` (fix-3 dual-emit + IsPredictionModeActive gate). L18 born during fix-3 root cause investigation. |
| Smoke | 2026-05-02 | Yonezawa (driver) + Claude (scrape) | Multiple iterations. Final post fix-3: 0 FATAL both peers. |
| Verify | 2026-05-02 (backfill — implicit) | n/a | volume audit per peer; strict `FATAL=0` met post fix-3 |
| Merge | 2026-05-02 | Yonezawa | PR #33 merged to dev @ `49e3c2f`. L18 entry into lessons-log. V2b Step 1 authority flip cleared to proceed. |
