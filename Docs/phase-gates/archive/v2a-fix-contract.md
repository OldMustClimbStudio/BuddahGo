# V2a fix — PostTick Drain Relocation Contract (ARCHIVED — Phase B backfill)

**Phase ID:** phase4b-v2a-fix
**Branch:** fix/phase4b-v2a-postick-drain
**Risk:** MEDIUM (transitional fix; observation-only — OLD still rb-authority)
**Status:** ✅ MERGED — PR #32 to dev @ `bd47f04` (2026-05-02)

> **Backfill note:** original contract was not authored at the time. Reconstructed from `lessons-log.md` L16 + L17 + commit history (`9116192 phase4b-v2a-fix: relocate inverted-shadow drain to PostTick (L16)` + `1fd2d73 phase4b-v2a-fix: L17 phase-skew rule + Path A/B digests + PR body update`) + README closed-phases attribution.

---

## Scope (locked)

**In scope:**
- Relocate `ConsumePendingImpulseEvents_InvertedShadow` from `RunInputs` (replay-unsafe; L16) to `TimeManager_OnPostTick` (replay-safe — fires exactly once per tick, after the replay loop)
- Update `[D-IMP INV HEARTBEAT]` to read scratch from PostTick consume site
- L17 phase-skew tolerance documented in observation gate (bidirectional FATAL pattern + `compared == OLD == Recv` totals + `div=0` at final HB = harmless phase skew, NOT real divergence)
- Path A + Path B re-verification post relocation

**Out of scope:**
- V2b Step 0 channel tick-stamping (strategic mitigation; replaces this transitional fix)
- Authority flip (V2b Step 1)

---

## Final outcome

✅ **STRICT PASS, MERGED.**

- PR #32 → `dev` @ `bd47f04`
- 2 implementation commits: `9116192` (L16 relocation) + `1fd2d73` (L17 documentation + digests + PR body)
- Path A host-only: `inv-imp-compared=1, inv-imp-div=0, INV FATAL=0` — clean (carry-over from V2a)
- Path B 2-peer LAN: bidirectional phase-skew pattern confirmed — 5 forward FATAL on CLIENT + 6 reverse FATAL on HOST, BUT `inv-imp-compared = OLD imp-compared = Recv` on both sides + `inv-imp-div = 0` on both sides → matches L17 fingerprint = harmless phase skew under V2a observation gate (NOT real divergence)
- Counter no longer 0-blind; L16 fix verified

### Lessons added

- **L16** — Single-consumer FIFO channel + FishNet reconcile replay = structural blindness (root cause spec).
- **L17** — Bidirectional phase-skew fingerprint: forward FATAL on one side + mirror reverse FATAL on the other + matching `compared == OLD == Recv` totals + `div = 0` final = phase skew, NOT divergence. Phase skew is harmless under V2a observation but **WOULD be a real desync under V2b authority** because each peer would write rb on a different tick. **V2b Step 0 (non-negotiable prerequisite):** tick-stamp channel + `ConsumeReady` semantics — eliminates phase-skew tolerance from production-path gates.

### Carry-forward delivered to V2b Step 0

- Channel must adopt `EventTick` field + `ConsumeReady(currentTick, callback)` semantics BEFORE any authority flip lands
- Strict `FATAL = 0` gate post-Step-0 (no phase-skew tolerance in production paths)

---

## Sign-off ledger (final, partial — backfill)

| Stage | Date | Signer | Notes |
|---|---|---|---|
| Kickoff | 2026-05-02 (backfill) | Yonezawa | fix scope; pre-Phase-Gate-System formal kickoff |
| Recon | 2026-05-02 (backfill — implicit) | n/a | scope = single-line drain-site move; recon implicit in L16 root cause writeup |
| Design | 2026-05-02 (backfill — implicit) | n/a | mitigation choice (PostTick vs full tick-stamping) discussed inline; tick-stamp deferred to V2b Step 0 |
| Implementation | 2026-05-02 | Yonezawa | commits `9116192` (relocation) + `1fd2d73` (L17 doc + digests) |
| Smoke | 2026-05-02 | Yonezawa (driver) + Claude (scrape) | Path A + Path B 2-peer LAN. Bidirectional phase-skew fingerprint observed and documented as non-blocking per L17. |
| Verify | 2026-05-02 (backfill — implicit) | n/a | volume audit `inv-imp-compared == OLD imp-compared == Recv` confirmed both sides; `div=0` at final HB |
| Merge | 2026-05-02 | Yonezawa | PR #32 merged to dev @ `bd47f04`. L16 + L17 entries into lessons-log; V2b Step 0 promoted urgent. |
