# Phase 6 Prerequisites

Gates that must be closed before Phase 6 (teleport + handoff cutover) can
begin. Each entry has a reference lesson ID, a blocking rationale, and a
concrete first diagnostic step.

Scope note (2026-04-19, post-Phase-3d audit): Phase 6 scope was expanded
from "teleport only" to **teleport + handoff, unified result-delivery fix
shape**. Phase 3d audit (`agent-exchange/handoff/2026-04-19-phase3d-audit.md`
§3) confirmed `RequestAuthoritativeLaunchHandoffFromOwner` is structurally
identical to the teleport silent-drop L12. Both categories ship one fix.

---

## Prereq-1 — L12: Owner→Server→Owner teleport + handoff RPC chains need failure visibility

**Source**: `Docs/lessons-log.md` L12 (2026-04-19, Phase 3b V5 R2). Scope
extended 2026-04-19 after Phase 3d audit.

**Why blocking**: Phase 6 cuts the authority path over to the predicted-queue
flow for BOTH teleport and handoff events. Two structurally identical bugs:

1. **Teleport path**: `BuddahPredictedMotor.RequestAuthoritativeTeleportFromOwner`
   (motor.cs:909-954 at time of writing; line numbers drift with refactors)
   silently returns `true` on the non-server branch regardless of whether
   the ServerRpc actually reached the server or whether the
   `QueueTeleportEventTargetRpc` return-leg reached the owner. Client-initiated
   teleports therefore never populate the owner-side `_pendingTeleportEvent`
   slot.
2. **Handoff path**: `BuddahPredictedMotor.RequestAuthoritativeLaunchHandoffFromOwner`
   (motor.cs:791-840) has the exact same silent-return pattern — returns
   `true` unconditionally at motor.cs:839 regardless of ServerRpc outbound
   or `QueueLaunchHandoffTargetRpc` return-leg delivery. Client-initiated
   handoffs never populate the owner-side `_pendingLaunchHandoffEvent`
   slot.

Phase 6's cut-over would remove the legacy fallback paths (teleport's
`TeleportToWorldPose` at `BuddahRespawn.cs:168`; handoff's FishNet-
reconcile-driven rb pose broadcast) that currently mask both bugs. Must
close the visibility loop BEFORE removing the fallbacks.

**Acceptance criterion (per category)**:
- Teleport: `V5 R3` style gate — client-initiated `fall-respawn` produces
  `tel-compared >= 1` on the client peer's shadow heartbeat.
- Handoff: equivalent gate — client-initiated intro-to-gameplay transition
  produces `hof-compared >= 1` on the client peer's shadow heartbeat.

Both must pass for Phase 6 to unblock.

**Observation (2026-04-19, Phase 3d V5)**: Phase 3d V5 2-peer playtest
CLIENT reached `hof-compared=1` starting T=3489 and held through end of
session (30 heartbeats, all `hof-div=0`). This EXCEEDS the L12 silent-drop
prediction for CLIENT-side handoff coverage (predicted `hof-compared=0`).
Candidate explanations: (a) L12 silent-drop may be intermittent for
handoff rather than deterministic, allowing occasional TargetRpc
return-leg landings; (b) reconcile-replay may deliver handoff payload
via serialized state fields (partial L12 relief through reconcile
channel); (c) random tick-race alignment. Non-blocking for V5 PASS.
**L12 scope unchanged** — silent-drop analysis stands until reliably
reproducible. Flag: **L12 may be intermittent in practice, confirm
reproducibility before Phase 6 fix sign-off.** Reference:
`agent-exchange/console/2026-04-19-phase3d-v5.log` §15 note 2.

**First diagnostic step (mandatory before writing the fix)**: enable
`bootstrap.LogVerbose` on both peers and run a minimal 2-peer session
exercising BOTH categories:
- Fall-respawn from CLIENT once (teleport).
- CLIENT-driven intro-to-gameplay transition (handoff).

Expected verbose lines per category:
- Teleport: server-side `teleport authoritative create id=...`; client-side
  `teleport enqueued id=...` if TargetRpc return arrived.
- Handoff: server-side `[IntroHandoff][Server] Received owner handoff
  request seq=...` + `handoff authoritative create id=...`; client-side
  `[IntroHandoff][Client] Received authoritative handoff eventId=...` if
  TargetRpc return arrived.

Failure modes (shared across both categories):
1. Server log missing the authoritative-create line → ServerRpc outbound
   silent drop. Check `RequireOwnership = true` race during the transition
   moment and FishNet observer state of the client's Buddah on server-side.
2. Server log shows create but client log missing the enqueued/received
   line → TargetRpc return-leg drop. Candidates: payload size, tick
   alignment, FishNet observer state.
3. Client log shows duplicate-ignored → ID collision against
   `_lastConsumed*EventId`. Rare; earlier-session event aligned IDs.

## Fix Shape Options

Phase 3d audit (Addendum B, 2026-04-19) flagged that the current reconcile
data contract already ships `PendingTeleport` / `PendingHandoff` / related
cursor fields on `BuddahPredictedReconcileData` — shape-only scaffolding
from Phase 0+1 (commit `f43cc1ec`). These fields are written as `default`
by `CreateReconcile` and dummy-read on `ReconcileState` (motor.cs:228-236 +
motor.cs:493-501 at time of writing). The scaffolding supports TWO distinct
Phase 6 fix architectures:

### Option A — Result-TargetRpc + owner-side commit-gate (original plan)

The server's apply path emits a second TargetRpc back to the owner
carrying the enqueue result (success/failure + eventId). Owner's
`RequestAuthoritativeXxxFromOwner` implementations return a Task or set
a pending-result slot; callers `await` or check-then-fallback.

**Pros**:
- Lower bandwidth: TargetRpc fires once per event, not per reconcile
  tick.
- Pending-queue architecture unchanged: owner and server each have
  their own `_pending*` slot; correctness relies on explicit acks.
- Simpler incremental change to existing RPC chain.

**Cons**:
- Per-event ack state machine adds complexity on both sides.
- Owner/server independent slots need manual reconciliation on edge
  cases (client reconnect, server mid-race spawn).
- Dummy reconcile fields stay dead → Phase 8 cleanup queue candidate
  (add `ShadowLastConsumedModifierId` DP6 + these fields together).

### Option B — Server broadcasts pending slot via reconcile data

Server populates `PendingTeleport` / `HasPendingTeleport` /
`LastConsumedTeleportId` / `PendingHandoff` / `HasPendingHandoff` /
`LastConsumedHandoffId` / `AwaitingAuthoritativeLaunchHandoff` /
`LocalPreHandoffBypassUntilTick` in `CreateReconcile`. Client's
`ReconcileState` applies them into its own motor fields. The owner's
`RequestAuthoritativeXxxFromOwner` no longer needs to track ack state
— the pending slot arrives via the next reconcile packet (worst case
latency ≈ reconcile interval, typically 1-2 ticks).

**Pros**:
- Unified trust model: server is source of truth for all prediction
  state; client rolls back and replays from reconcile snapshots. Aligns
  with FishNet's prediction doctrine.
- No per-event ack state machine. Pending slots align naturally across
  reconcile replays.
- Activates the Phase 0+1 shape-only scaffolding → no dead fields on
  reconcile struct.
- Eliminates owner-server independent-slot reconciliation concerns.

**Cons**:
- Higher bandwidth: reconcile packet grows by `sizeof(PendingTeleport)`
  + `sizeof(PendingHandoff)` + cursor uints on EVERY reconcile tick,
  regardless of whether an event is pending.
- First-tick latency: owner may not see its own just-requested event
  until the next reconcile packet arrives (typically ≤16ms; under bad
  network conditions longer).
- Dummy-write/dummy-read pattern at motor.cs:228-236 and motor.cs:493-501
  must be replaced with real assignments — not a cleanup, an
  implementation.

## Decision Path

Planner selects A or B at Phase 6 design time. Both options close the
L12 visibility loop for both categories.

- If **Option A**: add Phase 8 cleanup-queue Entry 2 to remove the
  unused reconcile fields after Phase 6 lands.
- If **Option B**: reference Phase 3d V5 CLIENT's 0 mod-div at 19:1
  ratio as the observational proof that the reconcile-delivery approach
  doesn't introduce precision drift (Phase 8 cleanup-queue Entry 1
  passes observationally via 3c V5 — Phase 6 wire-up would make the
  live usage consistent with that observation).

Do NOT remove legacy fallbacks in Phase 6 (teleport's
`TeleportToWorldPose` fallback at `BuddahRespawn.cs:168`; handoff's
reconcile-rb-pose broadcast reliance) until BOTH Option A or B passes
the per-category acceptance gate.

**Reference reading for Phase 6 planner**:
- `agent-exchange/handoff/2026-04-19-phase3d-audit.md` §3 — L12
  applicability confirmation for handoff (structural identity with
  teleport).
- `agent-exchange/handoff/2026-04-19-phase3d-audit.md` Addendum B —
  reconcile-field scaffolding provenance + classification.
- `Docs/lessons-log.md` L12 — teleport-side symptom + root cause.

---
