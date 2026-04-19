# Phase 3b V5 Round 2 — Cross-Peer Coverage Script

Date: 2026-04-19
Branch: refactor/prediction-v2
Why round 2: V5 run 1 and run 2 both showed clean parity but `imp-compared=0` and
`tel-compared=0` on the client peer. L11 explains why — the normal
`push_projectile_hands` skill has `allowSelfHit=false`, so self-push fizzles at
the attacker-hit gate before the impulse can reach the predicted-motor queue.
L10 explains the dual-branch gating that makes `imp-compared`/`tel-compared` the
authoritative coverage signal, not the in-game push outcome.

Round 2 forces each peer's OWN motor queue to receive events by making the
attacker and the victim different peers.

---

## Session layout

Two peers:
- **HOST**: Standalone Development Build (`BuddahGo_4.18_dev`), Steam lobby
  host. `BUDDAH_PREDICTION_SHADOW` on. Motor + shadow active.
- **CLIENT**: Unity Editor, MCP-connected. `BUDDAH_PREDICTION_SHADOW` on.
  Motor + shadow active.

Session wall-clock: ≥60s from first replicate tick to "Stop Play" on the Editor.

No code changes. No shadow / motor edits.

---

## Event budget (must all fire; aim for the FINAL 30s of the session so they
land inside the 2000-Log-entry MCP cache tail)

| # | Actor  | Action                                           | Affects queue on | Counter increment   |
|---|--------|--------------------------------------------------|------------------|---------------------|
| 1 | HOST   | cast `push_projectile_hands` aimed at CLIENT's Buddah, land hit | CLIENT `_impulseEventQueue` | client `imp-compared++` |
| 2 | HOST   | cast `push_projectile_hands` aimed at CLIENT's Buddah, land hit | CLIENT `_impulseEventQueue` | client `imp-compared++` |
| 3 | CLIENT | cast `push_projectile_hands` aimed at HOST's Buddah, land hit   | HOST `_impulseEventQueue`   | host `imp-compared++`   |
| 4 | CLIENT | cast `push_projectile_hands` aimed at HOST's Buddah, land hit   | HOST `_impulseEventQueue`   | host `imp-compared++`   |
| 5 | CLIENT | fall off track → fall-respawn triggers                          | CLIENT `_pendingTeleportEvent` | client `tel-compared++` |
| 6 | HOST   | fall off track → fall-respawn triggers                          | HOST `_pendingTeleportEvent`   | host `tel-compared++`   |

**Why cross-peer push (not self-push)**: see L11. `push_projectile_hands` has
`allowSelfHit=false` hardcoded in BuddahHandControl.cs:804 default. Self-push
returns early at HandPushProjectileRuntime.cs:204 before any enqueue.

**Why respawn via track-fall**: `BuddahRespawn.cs:156` routes through
`predictionRespawnBridge.TryRespawnToTrackProgress` → motor's
`RequestAuthoritativeTeleportFromOwner` → `TryQueueTeleportEvent` →
`_pendingTeleportEvent`. The fall-respawn path (BuddahRespawn.cs:126) uses the
predicted-queue branch when prediction mode is active. Manual `Esc`-menu or
scene-reload-based respawns may take a different code path and are NOT valid
coverage evidence for this round.

**Timing**: target the FINAL 30s of the session for events 1-6. The MCP Editor
log cache caps at 2000 entries — earlier events roll out of the captured
window. Cumulative counters will still reflect them, but heartbeats with those
counter increments need to be inside the captured window for verification.

---

## Log capture

After session end (Editor stops Play):
1. User pulls Editor console (client side) — I scrape via MCP and write
   `agent-exchange/console/2026-04-18-phase3b-v5r2-client.log`.

   (Note: dated 04-18 to stay in the phase3b log family; actual session is
   04-19. File-level timestamps inside the digest reflect the true run
   time.)

2. User exports Standalone host console (host-side) — either `Player.log` from
   `%APPDATA%\..\LocalLow\<Company>\<Product>\Player.log`, or copy/paste from
   the build's on-screen log if no file is available. I write
   `agent-exchange/console/2026-04-18-phase3b-v5r2-host.log`.

Both logs must show heartbeats in the active form (not idle). If either peer
is Editor-idle-for-30s the intro skip-tick gate kept locomotion off — that
peer must actively drive.

---

## PASS criteria (per peer, evaluated on the last heartbeat of the captured
window)

All FIVE conditions must hold simultaneously:

1. `loc-div = 0`
2. `imp-div = 0`
3. `tel-div = 0`
4. `imp-compared >= 2`
5. `tel-compared >= 1`

Plus session-wide:
6. 0 non-heartbeat `[D-LOC]` entries in the Warning pull.
7. 0 `[D-LOC FATAL]` entries in the Error pull.

If any peer fails 1-5 on the last heartbeat: FAIL, report diagnosis, no auto-
fix (per user V5 protocol).

If any peer fails 6-7: FAIL, report diagnosis.

If both peers pass all seven conditions: PASS, proceed to commit / close-out.

---

## Predicted heartbeat shape for a PASS (client example)

```
[D-LOC HEARTBEAT] T=<last> active-ticks=<>=2000 skip-ticks=<stable>
  loc-div=0 imp-div=0 tel-div=0
  imp-compared=2 tel-compared=1
```

Counters may be higher than the minimum (e.g. `imp-compared=3` if push landed
three times instead of two) — higher is fine. Lower is FAIL.

---

## Hard stops

- If V5 R2 ALSO shows `imp-compared=0` after cross-peer pushes were confirmed
  landed in-game, the predicted-queue branch is not being taken. Check
  `IsPredictionModeActive()` on the victim's bootstrap at cast time (likely
  false on one peer during a transient window). Report, do not auto-fix.
- If V5 R2 shows `imp-compared>0` but `imp-div>0`, that is a true parity
  divergence — report the first few D-LOC warning lines verbatim. Do not
  auto-fix.
- If V5 R2 FATAL fires on any peer, treat as 3b regression; capture log,
  revert local 3b changes, re-run V1/V2 baseline.

---

## Do NOT

- Do not edit shadow / motor / step code.
- Do not edit the `allowAttackerHit` default.
- Do not add debug print to skills.
- Do not re-run the test multiple times looking for a good-run — the first
  honest run is the data.
