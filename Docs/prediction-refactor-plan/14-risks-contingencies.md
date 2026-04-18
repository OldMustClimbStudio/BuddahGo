# 14 - Risks and Contingencies

[Back to index](index.md)

These are the known risks specific to this refactor and the pre-approved contingency response for each.

## R1 - Event-Id Ring Overrun on High Latency

Risk: at 250+ ms RTT with burst combat traffic, the impulse channel ring may be outpaced.

Contingency:
- Make ring size configurable per prefab (`BuddahPredictionEventConfig`).
- On overrun, trigger a forced full-snap using last server reconcile, logged as `PredictionOverrun`.
- Monitor `PredictionOverrun` count in Debugging UI; if more than 1 per match on average, raise ring size or throttle combat burst rate.

## R2 - Server-Initiated Teleport Ordering

Risk: server-origin teleport ids are forwarded to owner, but if owner is in a reconcile replay across the relay, ordering may differ between what the owner ran locally and what the server replayed.

Contingency:
- Teleports are idempotent in the step (position/rotation set + velocity clear), so out-of-order consumption does not compound.
- Use `TeleportCmd.AbsoluteOrdering` flag to drop any teleport whose id is less than `LastConsumedTeleportId` instead of re-applying.

## R3 - PredictionRigidbody API Surface Gaps

Risk: FishNet may not expose every `Rigidbody` operation we rely on (e.g., mass changes, custom integration).

Contingency:
- Phase 0 includes a 1-day spike auditing `PredictionRigidbody` API surface in FishNet 4.6.20.
- Any missing operation becomes a motor config change (mass baked into config) or is deferred out of `[Replicate]`.
- If mass modulation is truly needed mid-race, add it as a reconciled modifier field and apply via `rb.mass = reconciled.ActiveMass` in `OnPostReconcile` only.

## R4 - Shadow-Mode Divergence Debugging

Risk: Phase 3 shadow compare may show owner and shadow diverging and we can't pin it down.

Contingency:
- Log first-divergence tick with per-field diff snapshot.
- Hold the phase until divergence is understood; do not advance to Phase 4 while divergence exists.
- Budget an extra 2-3 days here; this is the most likely phase to slip.

## R5 - Intro Handoff + Scene Load Race

Risk: `HandoffCmd` enqueued before the owner exists (e.g., during scene load) is lost.

Contingency:
- Server holds intro `HandoffCmd` until all expected Buddahs report `OnStartClient` complete.
- Race start is gated by that ready signal (consistent with existing room orchestration).

## R6 - Legacy Movement Still Referenced

Risk: legacy paths are still scattered across combat, skills, tests.

Contingency:
- Legacy paths stay functional until Phase 8.
- `BuddahPredictionMode.Legacy` keeps routing combat to `BuddahPredictionPushTargetBox`.
- Phase 8 decides if legacy is deleted or archived.

## R7 - Analyzer False Positives

Risk: Roslyn rule in Phase 8 may flag legitimate writes (tests, editor tooling).

Contingency:
- Whitelist by path prefix and by `[PredictionAllowRigidbodyWrite]` attribute for isolated cases.
- Initial ruleset ships as warning, promoted to error in Phase 8 final.

## R8 - Presentation Adapter Subscribers Miss Post-Tick Events

Risk: VFX / camera subscribing to `OnPostTick` snapshot miss events if bootstrap re-initializes.

Contingency:
- Snapshot adapter publishes a sticky "latest" that new subscribers read on subscribe.
- Re-init path emits a `PresentationReset` event.

## R9 - Input Adapter Timing

Risk: single-frame keys sampled in `Update` might miss a tick if steering toggles in the same physics frame.

Contingency:
- Input adapter buffers edge events (pressed/released) and drains into the next `CreateReplicateData`.
- `OwnerInputLive` flag acts as a clean guard.

## R10 - Migration Freeze

Risk: we ship Phase 4 and discover V3 or V4 still fails.

Contingency:
- Recovery per `Docs/recovery.md` - revert to Phase 3 commit, revisit Phase 3 shadow compare with new diagnostic telemetry before retrying Phase 4.

See [15-mental-model.md](15-mental-model.md) for the final state after all risks are mitigated.
