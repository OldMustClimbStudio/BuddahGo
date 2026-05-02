# phase4b-v3 — Independent Verification

**Raw log paths:**
- `agent-exchange/console/raw/2026-05-02-phase4b-v3-single.log` (37,956 lines, 3.0 MB) — Path A
- `agent-exchange/console/raw/2026-05-02-phase4b-v3-host.log` (97,851 lines, 11 MB) — Path B HOST
- `agent-exchange/console/raw/2026-05-02-phase4b-v3-client.log` (248,121 lines, 20 MB) — Path B CLIENT

**Reviewer:** Claude Opus 4.7 (cowork-reviewer)
**Verification date:** 2026-05-02

---

## Path A grep results (Host-only single peer)

```
[D-IMP LEG FATAL]:                                  0
[D-IMP INV FATAL]:                                  0
BuddahPredictionCombatRouting:TryRouteImpulse:      0   (legacy dead — V3 strict gate)
BuddahPredictionRouter:RouteImpulse:                7   (all Tier 2 PushTargetBox)
[D-IMP LEG HEARTBEAT]:                              40
leg-imp-div distinct values:                        {0}
[Channel]:DupReject:                                0
NullReferenceException / UnityEngine.Exception:     0
error CS:                                           0
Parser Failure at line 11 (Router.cs.meta):         0   (post-fix 9ab64f5)
HB span:                                            T=479 → T=5159 (4.7k ticks)
```

Sample stack trace confirming dispatch chain:
```
ChargedHandProjectileRuntime:Update (line 236)
  → BuddahPredictionRouter:RouteImpulse (line 71)
    → BuddahPredictionPushTargetBox:TryApplyServerImpulse (line 48)
      → "[BuddahPredictionV2] push-target hit name=DebugboxCanPush (2) source=ChargedProjectile sourceId=2 impulse=(598.05, 0.00, -48.33) torque=50.00"
```

## Path B HOST grep results

```
[D-IMP LEG FATAL]:                                  0
[D-IMP INV FATAL]:                                  0
BuddahPredictionCombatRouting:TryRouteImpulse:      0
BuddahPredictionRouter:RouteImpulse:                1   (Tier 2 mediated; Tier 1 silent)
[D-IMP LEG HEARTBEAT]:                              101
leg-imp-div distinct values:                        {0}
[Channel]:DupReject:                                0
NullReferenceException / UnityEngine.Exception:     0
error CS:                                           0
[PushHitbox] Hit:                                   5
[BuddahPredictionV2] push-target hit:               1   (Tier 2 = DebugboxCanPush)
HB span:                                            T=1537 → T=7657 (6.1k ticks)
```

PushHitbox Hit victims (5 total):
- 1× owner=-1 victim=DebugboxCanPush → Tier 2 (logged)
- 2× owner=0 victim=Buddah(Clone) → Tier 1 (V2 prediction, silent channel enqueue)
- 2× owner=32767 victim=Buddah(Clone) → Tier 1 (V2 prediction, silent channel enqueue)

## Path B CLIENT grep results

```
[D-IMP LEG FATAL]:                                  0
[D-IMP INV FATAL]:                                  0
BuddahPredictionCombatRouting:TryRouteImpulse:      0
BuddahPredictionRouter:RouteImpulse:                7
[D-IMP LEG HEARTBEAT]:                              132
leg-imp-div distinct values:                        {0}
[Channel]:DupReject:                                0
NullReferenceException / UnityEngine.Exception:     0
error CS:                                           0
[PushHitbox] Hit:                                   0   (server-only gate via IsServerStarted, expected on client)
[BuddahPredictionV2] push-target hit:               7
HB span:                                            T=479 → T=12647 (10.9k ticks)
```

## Cross-check vs implementer's digest

| Metric | Path A digest | Path A grep | Match | HOST digest | HOST grep | Match | CLIENT digest | CLIENT grep | Match |
|---|---|---|---|---|---|---|---|---|---|
| `[D-IMP LEG FATAL]` | 0 | 0 | ✅ | 0 | 0 | ✅ | 0 | 0 | ✅ |
| `[D-IMP INV FATAL]` | 0 | 0 | ✅ | 0 | 0 | ✅ | 0 | 0 | ✅ |
| CombatRouting calls | 0 | 0 | ✅ | 0 | 0 | ✅ | 0 | 0 | ✅ |
| Router stack frames | 7 | 7 | ✅ | 1 | 1 | ✅ | 7 | 7 | ✅ |
| HB rows | 40 | 40 | ✅ | 101 | 101 | ✅ | 132 | 132 | ✅ |
| `leg-imp-div` MAX | 0 | 0 | ✅ | 0 | 0 | ✅ | 0 | 0 | ✅ |
| `[Channel]:DupReject` | 0 | 0 | ✅ | 0 | 0 | ✅ | 0 | 0 | ✅ |
| Exceptions | 0 | 0 | ✅ | 0 | 0 | ✅ | 0 | 0 | ✅ |

All metrics match implementer digests.

## Anomalies

**Router stack frame count appears low vs Hit count (HOST: 5 Hit, 1 Router frame).** Investigated: Tier 1 dispatch (V2 prediction Buddah → CombatAdapter.TryRouteImpulse → channel.TryEnqueue) is silent by design — emits no Debug.Log, so stack traces aren't captured for those 4 of 5 hits. The 1 Router frame is from the Tier 2 PushTargetBox path which DOES log. This is **expected V3 behavior**, not a regression. `leg-imp-div=0` across 101 HOST + 132 CLIENT HBs cross-validates that all 4 silent dispatches were applied correctly (any drop would surface as `leg-imp-cons` divergence).

**Editor.log was on disk and cleared per Methodology Rule 1 sub-rule.** All three logs have a single `Initialize engine version` block — no session-window scoping needed.

## Verdict

**✅ PASS** — All Path A + Path B HOST + Path B CLIENT strict gates met. V3 router migration is functionally correct; CombatRouting is structurally dead (0 production callsites in all 3 logs); authority-flip from Step 1 remains clean (0 LEG FATAL across 273 HEARTBEAT rows total). Ready for Stage 7 MERGE.
