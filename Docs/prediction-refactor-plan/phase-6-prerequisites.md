# Phase 6 Prerequisites

Gates that must be closed before Phase 6 (teleport cutover) can begin.
Each entry has a reference lesson ID, a blocking rationale, and a concrete
first diagnostic step.

---

## Prereq-1 — L12: Owner→Server→Owner teleport RPC chain needs failure visibility

**Source**: `Docs/lessons-log.md` L12 (2026-04-19, Phase 3b V5 R2).

**Why blocking**: Phase 6 cuts the authority path over to the predicted-queue
teleport flow. Current `BuddahPredictedMotor.RequestAuthoritativeTeleportFromOwner`
(motor.cs:886-931) silently returns `true` on the non-server branch regardless
of whether the ServerRpc actually reached the server or whether the
`QueueTeleportEventTargetRpc` return-leg reached the owner. Client-initiated
teleports therefore never populate the owner-side `_pendingTeleportEvent`
slot — the predicted-queue consumer never sees the event, and Phase 6's
cut-over would remove the legacy `TeleportToWorldPose` fallback that
currently masks the issue via FishNet reconcile.

**Acceptance criterion**: `V5 R3` style gate where client-initiated
`fall-respawn` produces `tel-compared >= 1` on the client peer's shadow
heartbeat — proving the event entered the client's predicted queue, not
only the server-side queue.

**First diagnostic step (mandatory before writing the fix)**: enable
`bootstrap.LogVerbose` on both peers and run a minimal fall-respawn
session. Expected verbose lines to verify:
- Client-side: none for the outbound (no verbose log in
  `RequestAuthoritativeTeleportFromOwner` non-server branch).
- Server-side: `teleport authoritative create id=... reason=fall-respawn` if
  the ServerRpc arrived.
- Client-side: `teleport enqueued id=...` if the return TargetRpc arrived.

Failure modes rank:
1. Server log missing `teleport authoritative create` → ServerRpc outbound
   silent drop. Check `RequireOwnership = true` race during respawn moment
   (possible if ownership transfer interacts with fall detection timing) and
   FishNet observer state of the client's Buddah on server-side.
2. Server log shows create but client log missing `teleport enqueued` →
   TargetRpc return-leg drop. Candidates: payload size (14 fields incl
   Quaternion + 7 bools vs impulse's 7 fields), tick alignment, FishNet
   observer state.
3. Client log shows `teleport duplicate ignored` → duplicate-ID collision
   against `_lastConsumedTeleportEventId`. Rare; would suggest an earlier
   session teleport (intro?) aligned IDs.

**Fix shape (after diagnosis)**: depends on failure mode. Preferred
direction is a result-TargetRpc that propagates the server-side enqueue
outcome back to the owner, and an owner-side `await` or commit-gate that
falls back to legacy `TeleportToWorldPose` on reported failure. Do NOT
simply remove the legacy fallback at `BuddahRespawn.cs:168` in Phase 6
until the failure-visibility loop is closed.

**Companion work**: `RequestAuthoritativeLaunchHandoffFromOwner`
(motor.cs:768-817) has the same silent-return pattern on the client path.
Phase 3d handoff shadow will likely surface the same coverage gap; apply
the same fix shape there.

---
