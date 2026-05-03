# phase4b-v5 — Recon Report

**Branch:** feat/phase4b-v4-cleanup (RECON-only; V5 branch `feat/phase4b-v5-latency-terminal-gate` to be cut from dev @ V4 merge commit at Stage 4 IMPLEMENT)
**Status:** RECON ONLY — no code changes; no Q-answers yet
**Scope reminder:** LatencySim 100ms RTT terminal gate + lobby protocol-version handshake + reconcile-before-RPC double-apply probe (Phase 4b closeout)
**Risk (contract):** MEDIUM — spot-check sampling allowed per L21; negative-claim positive verification still mandatory

---

## 1. TransportManager + LatencySimulator API surface

| Item | Location | Note |
|---|---|---|
| `LatencySimulator` class | `Assets/FishNet/Runtime/Managing/Transporting/LatencySimulator.cs:13` | `[Serializable]`, instance held on `TransportManager` |
| Field on TransportManager | `Assets/FishNet/Runtime/Managing/Transporting/TransportManager.cs:86` | `private LatencySimulator _latencySimulator = new();` |
| Public accessor | `TransportManager.cs:90` | `public LatencySimulator LatencySimulator { get; }` |
| Toggle API | `LatencySimulator.cs:67-74` | `SetEnabled(bool value) → _enabled = value; Reset();` |
| Latency field | `LatencySimulator.cs:83-87` | `_latency` long, ms range 0-60000, **doubled on host** per docstring |
| `CanSimulate` predicate | `LatencySimulator.cs:46` | `GetEnabled() && (Latency > 0 || PacketLost > 0 || OutOfOrder > 0)` |
| Wire-fire site | `TransportManager.cs` (`HandleClientReceivedDataArgs` etc., not yet read) | LatencySim is invoked from TransportManager packet pipeline; no game code touch needed if Inspector toggle used |

**Activation path (V5 IMPLEMENT — no decision yet, RECON only):**
- **Option E1 — Inspector toggle:** flip `_enabled = true` + `_latency = 50` on the NetworkManager prefab/scene asset's TransportManager. Cleanest; persists across Editor sessions.
- **Option E2 — Runtime API:** `InstanceFinder.TransportManager.LatencySimulator.SetEnabled(true)` + reflect-set `_latency` (private field; would need either making it public or routing via TransportManager method if FishNet exposes one). Revert-on-quit semantics. Locate-on-need; not yet read.

**KEY FINDING — `_latency` is per-direction, doubled on host:** Per `LatencySimulator.cs:84` tooltip "When acting as host this value will be doubled". For 100ms RTT symmetric: set `_latency = 50` on both peers (host's host-loopback gets 100ms, send + receive each gets 50ms = 100ms RTT). Confirm during Stage 4 IMPLEMENT spike.

---

## 2. Lobby flow + Q1 protocol version handshake surface

`Assets/Scripts/Network/Lobby/SteamLobbyManager.cs` is the sole owner.

| Item | Location | Note |
|---|---|---|
| Metadata KEY constants | `:90-94` | 5 keys: `l_name`, `l_ver`, `l_host`, `l_vis`, `l_max` |
| Write site (host-create) | `:412-416` | `lobby.SetData(KEY_APP_VERSION, Application.version)` etc. |
| Read site (client-join) | `:452-453` | `string hostSteamId = lobby.GetData(KEY_HOST_STEAM_ID)` |
| Existing failure path | `:457-462` | Missing `KEY_HOST_STEAM_ID` → `lobby.Leave()` + `FireJoinFailed` |
| List-filter site | `:626-642` | `BuildLobbyListItemData` reads `KEY_APP_VERSION`, sets `IsVersionMatch` for UI gating |
| Steam version comparison | `:627` | `string.Equals(version, Application.version, StringComparison.Ordinal)` |

**Q1 insertion blueprint (RECON estimate, not design):**
1. Add constant `KEY_PREDICTION_PROTOCOL_VERSION = "l_pv"` at `:95`
2. Add static `const int PREDICTION_PROTOCOL_VERSION = 1;` in a shared types file (candidate: `BuddahPredictionBootstrap.cs` namespace neighbor, or a new `Assets/Scripts/Network/PredictionProtocol.cs`). Bump on every wire-format change (next bump = V5 if Q0 mitigation B/C lands).
3. Host write at `:417` — `lobby.SetData(KEY_PREDICTION_PROTOCOL_VERSION, PREDICTION_PROTOCOL_VERSION.ToString());`
4. Client read at `:454` — `string remotePvStr = lobby.GetData(KEY_PREDICTION_PROTOCOL_VERSION);`
5. Mismatch handler (~10 LOC) — `int.TryParse + compare + lobby.Leave + FireJoinFailed("Prediction protocol mismatch: host=X, you=Y. Update your client.")`
6. Optionally surface in `LobbyListItemData` (`:21-33`) for pre-join UI filtering — adds `IsPredictionProtocolMatch` bool, joins `IsJoinable` AND-clause at `:641`.

**Estimated LOC:** ~25-35 (1 const + 1 KEY + 1 write + 1 read + ~10 mismatch handler + ~10 list-filter optional). Matches V5 contract Q1-A "~30 LOC" estimate.

---

## 3. ReconcileData current shape (post-V4) + Q0 mitigation surface

`Assets/Scripts/New_Buddah/Core/BuddahPredictedReconcileData.cs` (67 lines, all read).

**Fields (verified post-V4):**
```
RigidbodyState, ModifierState, ComputedStats, HandoffState,
IntroControlActive, ExternalKinematicControlActive, MovementAllowed,
PlanarSpeed, ServerForward,
PendingTeleport, HasPendingTeleport, LastConsumedTeleportId,
PendingHandoff, HasPendingHandoff, LastConsumedHandoffId,
AwaitingAuthoritativeLaunchHandoff, LocalPreHandoffBypassUntilTick
```

**Confirmed absent (V4 deletion landed):** `ImpulseQueueState` field — V4 cascade step 7 removed it.
**Confirmed pattern:** Teleport + Handoff already use `LastConsumedXxxId` pattern (uint). Q0 mitigation B (`LastConsumedImpulseLogicalId` or `LastConsumedImpulseTick`) is structurally identical insertion + would slot at `:23` next to `LastConsumedTeleportId`.

**KEY FINDING — wire-format change again:** Adding any field to `BuddahPredictedReconcileData` is a `[Reconcile]` wire-format change per V4 Q3.4. Triggers `predictionProtocolVersion` bump (Q1) and atomic peer deployment requirement. **Q0 + Q1 are coupled** — if Q0-A (defer mitigation) chosen, Q1 wire bump needed only if any other V5 wire change lands. If Q0-B/C chosen, Q1 must land alongside it.

**Q0-B implementation cost estimate:** uint field (4 bytes wire) + populate in motor reconcile producer (~3 LOC: read from `CommandBus.ImpulseChannel.LastConsumedId` or new tick-tracking field) + gate ConsumeReady drain on client (~5 LOC: skip entries where `EventTick ≤ LastConsumedImpulseTick`). Total ~10-15 LOC. Per L5 — must audit FishNet auto-serializer accepts the new uint.

**Q0-C implementation cost estimate:** harder — `_recentLogicalIds` is HashSet<uint> on the channel instance (channel.cs:41). Serializing a set across reconcile = cap-bounded array + length prefix; FishNet auto-serializer handles `uint[]` but per-tick state copy is hot-path overhead. Higher complexity than B; higher correctness fidelity (catches reorder regardless of tick).

**Recommendation lean:** Q0-A defer + Q1-A implement. Q1's wire bump is paid regardless (V4 already changed wire), so Q0-B can land in V6 if observed.

---

## 4. OnReconcile callback path + Q3-B reconcile-replay metric

| Item | Location | Note |
|---|---|---|
| `[Reconcile]` attribute | `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs:542` | Method receives `BuddahPredictedReconcileData` |
| Reconcile body | `:557-564` (per Explore) | Existing log call `LogPredictionIntroReconcileState` per-tick |
| HEARTBEAT emit site | grep `[D-LOC HEARTBEAT]` in motor.cs (V4 verify shows 132 + 107 rows produced) — exact line not yet read | Per V4 design diff |

**Q3-B insertion blueprint:** add `private uint _reconcileCallbackCount;` field + `_reconcileCallbackCount++;` at top of `[Reconcile]` method body (~:543) + emit `rec-cb=<count>` in HEARTBEAT log line. ~3 LOC.

**Why useful for V5:** Pre-LatencySim baseline reconcile-callback count = small (FishNet only fires reconcile when authority correction needed). LatencySim 100ms induces tick drift → more frequent reconcile callbacks. **If V5 smoke shows `rec-cb` count not significantly higher than V4 baseline = LatencySim NOT actually engaged** (Inspector toggle didn't persist, runtime API skipped, etc.). Cheap real-or-fake check.

---

## 5. LogicalId Recv + Q3-C double-apply gate

`Assets/Scripts/New_Buddah/Events/BuddahPredictionEventChannel.cs` fully read.

| Item | Location | Note |
|---|---|---|
| Dedup HashSet | `:41` | `_recentLogicalIds = new()` |
| Dedup ring (FIFO, cap 64) | `:42` + `:38` (`MaxRecentIds = 64`) | `_recentLogicalIdOrder` Queue + bounded eviction |
| Dedup check (recent) | `:75-80` | `Contains(logicalId)` → `[Channel]:DupReject reason=recent` LogWarning |
| Dedup check (pending linear scan) | `:82-90` | `_pending[i].LogicalId == logicalId` → `[Channel]:DupReject reason=pending` |
| `[CommandBus]:Recv` log | `BuddahPredictionCommandBus.cs:119` (per Explore) | Carries `eventTick` + `logicalId` post-V2b Step 1 |
| Dual-emit topology | `BuddahPredictionCombatAdapter.cs:97-98` | Server-local + RPC; same LogicalId, two channel instances, independent dedup sets |

**KEY FINDING — Q3-C is partially redundant with existing DupReject:**
The existing `[Channel]:DupReject` mechanism already detects same-peer-instance double-apply via LogicalId. V4 strict gate `DupReject = 0`. **Q3-C "0 LogicalIds consumed >1 time per peer" is already covered by elevating that existing gate.**

**However — Q3-C fundamentally CANNOT detect cross-peer Q0 double-apply via channel-internal state alone:** the Q0 race scenario is "server applies once at T_s + client receives reconcile-state at T_c showing applied + client RPC delivers + client applies AGAIN at T_c". From the CLIENT channel's perspective, this is a SINGLE Recv (LogicalId fires once → consumed once → no DupReject). The double-apply manifests as **rb-state divergence** (extra impulse magnitude), not as a duplicate consume. Detection requires either:
- Q0-B reconcile-state gate (`LastConsumedImpulseTick` skip) — preempts double-apply, no detection needed
- Comparing CLIENT post-RPC rb-velocity against expected reconcile-baseline + impulse-magnitude — heavy instrumentation, post-hoc correctness check

**Recommendation:** drop Q3-C as proposed (redundant with DupReject). Replace with **Q3-D (new)** = "rb-velocity-magnitude divergence probe for impulse Recvs under LatencySim" — but this is heavy and only meaningful if Q0-A defer + observation shows symptom. Defer Q3-D to V6 contingent on Q0-A observations.

---

## 6. V4 partial-pass spawn-window + V5 remediation

| Item | Location | Note |
|---|---|---|
| `MarkReady()` | `BuddahPredictionCombatAdapter.cs:43-46` | Flips `_initialized = true` |
| Latch flag in motor | `BuddahPredictedMotor.cs` (V4 design Q3.1 cited :143-149 + :382-396 wrapper-strip; field name `_combatAdapterInitialized`) | Confirmed surviving V4 strip per V4 verify L7 latch correctness proof |
| First-Recv trigger | Adapter `IsReady` gate + Bootstrap's first `[Replicate]` tick post-Switcher-double-ApplyMode | Per L7 lessons rule b |

**V4 partial-pass root cause:** user did not push within first ~5s of CLIENT buddah spawn → first Recv landed at eventTick=6460, first HB at T=474, distance = 5986 ticks (~100s) >> 60-tick gate. Per V4 Q3.2 contract = "partial-pass with note", not strict failure.

**V5 remediation (smoke procedure, not code):** explicit user directive in V5 smoke instructions = "peer-2 push CLIENT buddah within first 3-5s of CLIENT spawn (before lobby-setup chatter)". Adds 60-tick distance gate as **strict-met** rather than partial-pass for V5.

---

## Summary — what Q0–Q4 must answer (preview, not answers)

| Q | Topic | Recon-derived lean (preview only) | Coupling |
|---|---|---|---|
| Q0 | Reconcile-before-RPC mitigation | **A** (observe-first under 100ms; B retrofit cheap if observed; ~10-15 LOC for B) | Coupled with Q1 wire-bump if B/C chosen |
| Q1 | Lobby protocol-version handshake | **A** (implement; ~25-35 LOC at clean insertion points; SteamLobbyManager surface mapped) | Q0 wire-bump if any V5 wire change lands |
| Q2 | LatencySim profile breadth | **A→B** (100ms first, expand to 200ms if clean — fail-fast) | Independent |
| Q3 | LatencySim-specific gates | **B yes / C drop-or-replace** — DupReject already covers same-peer dup; Q3-C redundant; reconcile-callback-count (Q3-B, ~3 LOC) confirms LatencySim engagement | Q0-A increases value of Q3-B |
| Q4 | Phase 4b closeout actions | task #24 close + task #36 unblock + Phase 5 reconcile decision + lessons updates + Phase 7 KICKOFF gating | Independent |

**KEY FINDINGS for design phase:**
1. **Q1 insertion is structurally clean** — SteamLobbyManager already has KEY-pattern + version-match-gate + `IsJoinable` AND-clause. ~30 LOC validated.
2. **Q0-B retrofit is structurally cheap** — `LastConsumedImpulseTick` mirrors existing `LastConsumedTeleportId`/`LastConsumedHandoffId` shape. ~10-15 LOC validated.
3. **Q3-C as worded is redundant** — existing `[Channel]:DupReject` already enforces "0 LogicalIds consumed >1 time per peer instance"; cross-peer Q0 race is structurally invisible to channel dedup and requires Q0-B/C OR rb-velocity divergence probe.
4. **Wire-format coupling matters** — Q0-B + Q1 should land atomically if Q0-B selected (single `predictionProtocolVersion` bump covers both).
5. **V5 = end of Phase 4b** — Q4 closeout work has no recon surface (process-only).

---

## Out-of-scope confirmed (per contract)

- L9 ClampPlanarSpeed fix → Phase 8
- Phase 7 visual jitter investigation → unblocks on V5 merge but separate phase
- Phase 6 Teleport/Handoff cut-over → separate channel migration
- Adapter caching → Phase 8

---

## Working-tree state note

**V4 closeout is uncommitted on `feat/phase4b-v4-cleanup`:** archive moves (`active/v4-contract.md` → `archive/v4-contract.md`), README V4-row update, lessons-log L20 + L21 entries, `active/v5-contract.md` draft, plus modified `Docs/prediction-refactor-plan/{02,09,12}.md` and `Docs/phase-gates/README.md`. Per Methodology Rule 10 (commit hygiene), this should land before V5 IMPLEMENT cuts the new branch. RECON + DESIGN can proceed on current branch (no code changes).

---

Awaiting reviewer sign-off before writing Q0–Q4 design proposals.
