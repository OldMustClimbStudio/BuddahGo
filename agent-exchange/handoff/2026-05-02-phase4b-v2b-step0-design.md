# Phase 4b V2b Step 0 — Design Q&A Archive

**Branch:** `feat/phase4b-v2b-step0-tick-stamp` (from `origin/dev` → SHA `bd47f04`)
**Goal:** convert `BuddahPredictionEventChannel<T>` from drain-once FIFO → tick-stamped + `ConsumeReady` semantics so NEW path observation aligns with OLD path under reconcile replay. Strict `[D-IMP INV FATAL] = 0` after merge (no phase-skew tolerance).

**Non-goals:** authority flip (V2b Step 1), skill-site cut-over (V3), CombatRouting deletion (V4), L9 ClampPlanarSpeed (Phase 8), Teleport/Modifier/Handoff drain semantics (only stamping is added now).

---

## Q1 — ConsumeReady cursor advance mechanism

**Decision: option (d) mirror OLD path exactly — `List<Entry>` + `EventTick` gate + `_recentEventIds` HashSet (size 64).** No `_lastConsumedId` cursor. No `IsReplaying` flag. Iteration **forward** with adjust-after-RemoveAt.

### Rationale
- OLD path's `BuddahPredictedImpulseEventQueue` (`Assets/Scripts/New_Buddah/Core/BuddahPredictedImpulseEventQueue.cs`) uses this exact pattern and is replay-safe in production (V2a observation showed zero D-LOC FATAL across all paths).
- Cursor + replay-snapshot mechanism (option a) introduces two pieces of synchronized state and depends on identifying the "final replay" frame, which FishNet does not expose cleanly.
- `IsReplaying` flag detection (option b) couples the channel to FishNet internal state behavior; high cross-version risk.
- Mirror OLD = safest, most predictable, smallest cognitive load.

### Iteration direction (user clarification, 2026-05-02)
**Forward iteration** chosen over OLD's backward iteration. V2b Step 0 callbacks are all-true (every entry consumes), so order is irrelevant here. **V2b Step 1 may introduce conditional `return false` for filter-driven apply ordering** — FIFO semantics matter then. Choosing forward now is preventive; saves a re-pass during Step 1.

OLD path's backward iteration was chosen for `RemoveAt` performance reasons (no shift cost when removing from tail), but at 64-entry cap the difference is negligible. Forward iteration with index adjust-after-RemoveAt:
```csharp
for (int i = 0; i < _pending.Count; ) {
    Entry entry = _pending[i];
    if (entry.EventTick > currentTick) { i++; continue; }
    if (!callback(in entry)) { i++; continue; }
    _pending.RemoveAt(i);
    RememberEventId(entry.Id);
    consumed++;
}
```

### Implementation skeleton (BuddahPredictionEventChannel<T>)
```csharp
public struct Entry { public uint Id; public uint EventTick; public T Payload; }
public delegate bool ConsumeCallback<TPayload>(in BuddahPredictionEventChannel<TPayload>.Entry entry);

private readonly List<Entry> _pending = new();
private readonly HashSet<uint> _recentEventIds = new();
private readonly Queue<uint> _recentEventOrder = new();
private const int MaxRecentIds = 64;

// V2b Step 0 — TryEnqueue takes EventTick (server tick). Q2 answer.
public bool TryEnqueue(in T payload, uint eventTick, out uint id) {
    id = 0u;
    if (_pending.Count >= _capacity) return false;  // existing :DropFull path
    id = _nextSequence++;
    if (_recentEventIds.Contains(id)) return false; // belt-and-braces; id is monotonic
    _pending.Add(new Entry { Id = id, EventTick = eventTick, Payload = payload });
    return true;
}

// V2b Step 0 — replay-safe drain. Mirrors BuddahPredictedImpulseEventQueue.ConsumeReady.
public int ConsumeReady(uint currentTick, ConsumeCallback<T> callback) {
    int consumed = 0;
    for (int i = 0; i < _pending.Count; ) {
        Entry entry = _pending[i];
        if (entry.EventTick > currentTick) { i++; continue; }
        if (!callback(in entry)) { i++; continue; }
        _pending.RemoveAt(i);
        RememberEventId(entry.Id);
        consumed++;
    }
    return consumed;
}

[Obsolete("Use ConsumeReady. TryDequeue is replay-unsafe (see lessons-log L16).")]
public bool TryDequeue(out Entry entry) { /* preserved for any in-flight V2a callsite */ }
```

`_capacity` replaces `_ring.Length` (List has no fixed cap; we enforce 64 ourselves to preserve `:DropFull` behavior).

---

## Q2 — Server tick vs Client tick drift

**Decision: server stamps `EventTick`, propagated through the cmd payload, client uses cmd-supplied tick verbatim.** No client-local-tick stamping at RPC arrival.

### Rationale
- L17 phase-skew root cause: OLD path uses server tick as canonical clock; NEW path stamping at client RPC receipt creates a drift ≥ 1 tick per fan-out, guaranteeing FATAL on every routed event.
- L17 documents "V2b must restore strict `FATAL = 0`" — only achievable if both paths share the same canonical clock.
- OLD's protocol contract: `eventData.EventTick = TimeManager.LocalTick` set on server (motor.cs:845), TargetRpc transports unchanged, client compares `EventTick <= clientTick` against client's local tick. Same model applied here.

### Implementation
1. Add `uint EventTick` field to all 4 cmd payloads (`ImpulseCmd`, `TeleportCmd`, `ModifierCmd`, `HandoffCmd`). +4 bytes per cmd over wire.
2. Adapter (server-side) constructs cmd with `EventTick = bootstrap.CommandBus.TimeManager.LocalTick`. CommandBus is a `NetworkBehaviour` exposing `TimeManager` — no extra wiring needed. (Per user's Q2 confirmation.)
3. Server-side local-enqueue branch (`owner.IsHost == true`): `_commandBus.TryEnqueueImpulse(cmd, cmd.EventTick)`.
4. Server-side RPC fan-out: `_commandBus.Target_EnqueueImpulse(owner, cmd)` — cmd carries EventTick to client.
5. Client-side RPC handler: `TryEnqueueImpulse(cmd, cmd.EventTick)` — uses cmd-supplied tick, NOT client local.
6. Drain: `bus.ImpulseChannel.ConsumeReady(currentClientTick, callback)` — gate `entry.EventTick <= currentClientTick`.

Edge case: if RPC delivery latency causes client's tick to already be > server's stamp at arrival, `EventTick <= currentTick` is true → drains immediately. Same as OLD path. Phase-skew impossible because the comparison is always against a tick that's >= EventTick.

---

## Q3 — Cross-channel API consistency

**Decision: all 4 channels stamp EventTick + take `currentTick` parameter. Only Impulse drains via `ConsumeReady` in this PR. Teleport/Modifier/Handoff stamp now, drain semantics unchanged.**

### Rationale
- API uniformity: 4 channels are structurally homogeneous; uniform signatures reduce maintenance load.
- Future-proofing: V2b Step 1+ migrates Teleport/Modifier/Handoff drains. Stamp data is in place ahead of time so per-channel migration touches only motor's drain code, not bus/payload/channel wiring.
- Cost negligible: +4 bytes/cmd × 3 unused channels, +1 method parameter × 4 enqueue methods. Compiles out to noise.

### What changes per channel

| Channel | Cmd EventTick | TryEnqueue(tick) | RPC handler stamps | ConsumeReady drain | Motor drain wired |
|---|---|---|---|---|---|
| Impulse | ✅ | ✅ | ✅ | ✅ | ✅ (RunInputs) |
| Teleport | ✅ | ✅ | ✅ | ✅ (added, unused) | ❌ (no consumer yet) |
| Modifier | ✅ | ✅ | ✅ | ✅ (added, unused) | ❌ (no consumer yet) |
| Handoff | ✅ | ✅ | ✅ | ✅ (added, unused) | ❌ (no consumer yet) |

`TryDequeue` marked `[Obsolete]` on all 4 channels. Existing V2a-era callsite (`ConsumePendingImpulseEvents_InvertedShadow`'s drain loop) is rewritten to use `ConsumeReady`. No active TryDequeue caller remains after this PR; obsolete shim retained for safety.

---

## Q4 — Branch base verification

`refactor/prediction-v2` HEAD is still `39c5217` (Phase 4b V1 only). V2a code lives only on `dev` (`fe17418` merge of PR #31) + V2a fix (`bd47f04` merge of PR #32). User confirmed `dev` as base. Branch created: `feat/phase4b-v2b-step0-tick-stamp` from `origin/dev`.

---

## Drain location: PostTick → RunInputs

V2a fix (L16) moved `ConsumePendingImpulseEvents_InvertedShadow` to PostTick because the FIFO TryDequeue was replay-unsafe. With ConsumeReady the channel is replay-safe by construction, so the drain returns to RunInputs immediately after `ConsumePendingImpulseEvents` (OLD drain) — both paths now drain at the same lifecycle phase, eliminating L17's phase-skew at the call-site level.

Per-tick reset of `_legacyShadowScratch = default` stays in RunInputs at line ~388-394 (kept across V2a fix and now). Symmetry with `_realScratch` / `_shadowScratch` resets preserved.

---

## Re-test gate (strict, no phase-skew tolerance)

### Path A — host-only Editor, 30s smoke
- `inv-imp-compared ≥ 1`
- `inv-imp-div = 0` (final HB AND every HB in window)
- **`[D-IMP INV FATAL] = 0` (strict)**
- `[D-LOC FATAL] = 0`
- `[CommandBus]:ClearAll = 4`

### Path B — 2-peer LAN, 60s smoke (no LatencySim)
HOST + CLIENT both:
- `inv-imp-compared ≥ 3`
- `inv-imp-div = 0`
- **`[D-IMP INV FATAL] = 0` (strict)**
- `[D-LOC FATAL] = 0`
- `[CommandBus]:ClearAll = 4` per spawn
- CLIENT: `[CommandBus]:Recv ch=Impulse ≥ 1` (validates RPC EventTick propagation)

### Stop conditions
- `inv-imp-div > 0` in any HEARTBEAT window → stop, dump, return to handoff.
- `[D-IMP INV FATAL] ≥ 1` → stop. Compare detail: same direction symptom (`ranOld != ranNew`) means tick-stamp implementation has a hole; volume mismatch (`cntOld != cntNew`) means dedupe / recent-IDs misbehaved.
- `[CommandBus]:ClearAll ≠ 4` per spawn → L7 latch broken.
- Any OLD-path `[D-LOC FATAL]` → tick-stamp diff polluted OLD path (OLD code was not modified — investigate).
