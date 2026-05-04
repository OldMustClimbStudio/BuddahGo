> **MOVED — canonical phase6 contract is now at [`Docs/phase-gates/active/phase6-race-start-handoff-redesign-contract.md`](../../Docs/phase-gates/active/phase6-race-start-handoff-redesign-contract.md).** This file is preserved as the Stage 1 KICKOFF artifact (read-only audit trail). Stage 3+ sign-off ledger edits land on the active/ copy.

---

# Phase 6 — Race Start Handoff Redesign Contract

**Phase ID:** phase6-race-start-handoff-redesign
**Branch:** `feat/phase6-race-start-handoff-redesign` (cut from dev tip post Phase 7.5-A.1 ship)
**Risk:** MEDIUM-HIGH (cross-system: motor + spline driver + server orchestrator + Timeline + UI; supersedes existing Inherit/Blend handoff state machine)
**Status:** KICKOFF DRAFT (authored 2026-05-04, awaiting transfer to implementer Claude window)
**Predecessors:**
- Phase 4b (V1→V5) DONE — prediction stack mature, wire-format frozen
- Phase 7 RECON+SMOKE 找到 root cause: H1''' Handoff state capture variance (per Phase 7 stage5 SMOKE digest)
- Phase 7.5-A.1 retrofit ships in parallel (smoother config: `_extrapolation:0` + `_adaptiveInterpolation:0` + FrameRateLockGuard@120fps + optional vendor hack on `LocalTransformTickSmoother.cs:98`)

**Goal:** replace current velocity-inherit / BlendAlpha gradual handoff pattern with a stop-then-countdown pattern. All players decelerate to full stop at race-start position, sit static during 3s countdown (with letterbox cinematic Timeline playing), then unlock simultaneously at server-driven tick. Eliminates H1''' session-level jitter variance at the architectural level (no velocity continuity, no smoother baseline timing variance, no driver-mechanism mismatch).

---

## 1. Background & motivation

### 1.1 Current handoff issues (per Phase 7 RECON Surface 7 + SMOKE findings)

Current architecture transitions from spline-driven (kinematic) to prediction-driven (dynamic) via 3-stage state machine:
- **Inherit phase**: motor inherits spline-tangent velocity, player input × 0
- **Blend phase**: BlendAlpha 0→1 over `BlendDurationTicks`, player input gradually engaged
- **Normal phase**: full player control

This produces 3 architectural problems:

**M1 — Smoother baseline capture variance (session-level jitter root cause)**:
- FishNet's `LocalTransformTickSmoother` captures `_gfxInitializedLocalValues` / `_gfxPreSimulateWorldValues` at moments dependent on (spline position, tick boundary phase, rb.Sleep/WakeUp timing, render frame phase) — all variable per session
- Captured baseline persists for 60-90s of subsequent gameplay
- Different baseline per session → different jitter severity per session
- Yonezawa observation: 5 same-config sessions, 3 severe / 2 mild jitter (random distribution)

**M2 — Velocity discontinuity at handoff moment**:
- `SnapshotVelocity` taken from spline tangent at handoff tick may not match true instantaneous derivative
- Inherit phase character drifts from intended trajectory; player can't correct (input × 0)
- Blend phase player input scaled by BlendAlpha < 1 — sluggish "粘滞方向盘" feel

**M3 — Driver mechanism mismatch**:
- Spline driver: `rb.isKinematic = true`, external transform writes, path-following motion
- Prediction driver: `rb.isKinematic = false`, force-driven, free physics
- Hard-switch at handoff cannot be smooth — fundamentally heterogeneous integration mechanisms

### 1.2 Design rationale for stop-then-countdown pattern

By stopping all players at zero velocity before handoff:
- **Eliminates M1**: smoother captures static baseline (deterministic timing) — no per-session variance
- **Eliminates M2**: SnapshotVelocity = 0, no continuity issue — character can only sit still
- **Eliminates M3**: driver switch happens in a static state — both drivers agree on rb.position, rb.velocity = 0 trivially

Cost: 3 seconds of UX time (reasonable for racing-game start; matches genre convention of countdown-style start).

---

## 2. New 5-phase architecture

```
Phase 0: 比赛 Pre-Race
  - Spline 驱动所有玩家 Buddah (existing intro behavior)
  - 每 buddah 独立 spline traversal (parallel splines per player slot)
  - rb.isKinematic = true
  - Intro Timeline 播放 (existing — 含上下黑边 + 摄像机 cinematic + intro 表现)
  - 单 buddah 总耗时: T (Yonezawa-input parameter, e.g. 18s — see Section 3.1)

Phase 1: Spline 减速段 (在 Phase 0 内, 末段)
  - Spline driver 让 buddah movement speed 缓缓降低
  - 减速 curve 见 Section 3.1 公式
  - 终点: spline 最后一个 point = 该 buddah 的起跑位
  - 终点速度 = 0
  - 每 buddah 独立完成 (各自 spline 各自 timing)

Phase 2: 全员到位 — Server 检测 + 触发 race-start 编排
  - Server 监测: 所有 buddah _splineCompleted = true (或类似 sync 信号)
  - Server broadcast "race_start_countdown_begin" event (TargetRpc to all clients)
    - 含 startTick (server-authoritative tick)
    - countdown duration = 3s (= Y ticks at 60Hz, 默认 180 ticks; configurable Section 4.7)
  - 所有 client 收到 event 后同 tick 转 Phase 3

Phase 3: Lock + Countdown (3 seconds)
  - Motor 进 Locked state:
    - rb.isKinematic = true (彻底冻结物理)
    - rb.position lockstep at 起跑位 (Phase 1 spline endpoint)
    - 玩家输入完全 disabled (按键无反应; buddah 不前进, 不旋转, 手不旋转, 不能 push)
  - Race-Start Timeline 触发 PlayableDirector.Play()
    - 含: 上下黑边 (移植自 Intro Timeline) + 3-2-1 倒计时 UI animation + 任何 cinematic
    - 持续约 3s (跟 countdown 对齐)
    - 黑边在 Phase 4 起跑瞬间消失 (Timeline 内部 fade-out 或瞬间消失, designer 决定)
  - Intro Timeline 在 Phase 2 触发瞬间立即结束 (clean cut, no overlap)
  - Smoother 在 Locked 期间 capture 静态 baseline (deterministic, session-stable)
  - 控制权从 spline driver → prediction motor 完成切换

Phase 4: Race Start — 同步起跑
  - Server tick + 3s 后 broadcast "race_start" event (同 tick 触发解锁)
  - 所有 client 同 tick:
    - Motor 解 Locked → Normal state
      - rb.isKinematic = false
      - rb.velocity 仍为 0
      - 玩家输入 channel 解锁
    - Auto-forward 逻辑生效 (= 现有的"自动向前 logic" — 见 CQ2)
    - 玩家输入 100% 立即生效 (no Inherit, no Blend, no BlendAlpha)
  - Race-Start Timeline 进 ending 阶段 (黑边 fade-out etc.)
```

---

## 3. Implementation tasks (5 areas)

### 3.1 Spline driver — deceleration formula (CQ6)

**Yonezawa input parameter:** total time `T` (seconds) for buddah to complete spline traversal. Default `T = 18s`. Designer-tunable per stage.

**Spline length:** `L` (meters) = sum of arc-lengths between spline points. Computed once at start.

**Constraint:**
- Buddah starts at spline point 0, time t=0
- Buddah arrives at spline last point, time t=T
- Buddah velocity at t=T must be exactly 0 (so Phase 2 lock is on still buddah)
- Velocity v(t) monotonically decreases (no acceleration mid-traversal)

**Recommended formula — Linear deceleration**:

```
v(t) = v_max × (1 - t/T)

Where:
  v_max = 2L / T   (derived: ∫₀ᵀ v(t) dt = v_max × T/2 = L → v_max = 2L/T)

Properties:
  v(0) = v_max     (max speed at start)
  v(T) = 0         (stop at end)
  Constant deceleration: dv/dt = -v_max/T
  Position s(t) = v_max × t × (1 - t/(2T)) = L × (2t/T - (t/T)²)

Example T=18s, L=360m:
  v_max = 2 × 360 / 18 = 40 m/s
  At t=9s (midpoint): v = 20 m/s, s = 270m (= 75% of length)
```

**Alternative formula — Quadratic ease-out** (more dramatic deceleration, slower crawl into endpoint):

```
v(t) = v_max × (1 - t/T)²

Where:
  v_max = 3L / T   (derived: ∫₀ᵀ v(t) dt = v_max × T/3 = L → v_max = 3L/T)

Properties:
  v(0) = v_max     (max speed at start; higher than linear)
  v(T/2) = v_max × 0.25  (rapid initial decel)
  v(T) = 0         (stop at end)
  Position s(t) = L × (1 - (1 - t/T)³)

Example T=18s, L=360m:
  v_max = 3 × 360 / 18 = 60 m/s
  At t=9s (midpoint): v = 15 m/s, s = 315m (= 87.5% of length)
```

**Recommendation:** start with **linear** deceleration. Predictable, easy to tune, natural "缓缓降低" feel matches Yonezawa intent. Switch to quadratic if linear feels too slow at start. Both formulas trivial to implement.

**Code skeleton (spline driver per buddah):**

```csharp
private float _splineTraversalTime = 18f;   // T, Yonezawa-input parameter
private float _splineLength;                 // L, computed at start
private float _splineV_max;                  // v_max, derived
private float _splineElapsed;                // t, accumulated

private void Awake()
{
    _splineLength = ComputeSplineArcLength();      // sum of segments
    _splineV_max = 2f * _splineLength / _splineTraversalTime;  // linear formula
    // For quadratic: _splineV_max = 3f * _splineLength / _splineTraversalTime;
}

private void FixedUpdate()
{
    if (_splineCompleted) return;
    
    _splineElapsed += Time.fixedDeltaTime;
    float t = Mathf.Min(_splineElapsed, _splineTraversalTime);
    
    // Linear formula
    float currentVelocity = _splineV_max * (1f - t / _splineTraversalTime);
    
    // Advance along spline by velocity * dt
    float stepDistance = currentVelocity * Time.fixedDeltaTime;
    AdvanceSplineByArcLength(stepDistance);
    
    if (_splineElapsed >= _splineTraversalTime)
    {
        _splineCompleted = true;
        rb.position = _splineEndPosition;       // snap to exact endpoint
        rb.velocity = Vector3.zero;
        NotifySplineComplete();                 // 给 server 信号
    }
}
```

**Spline progress tracker integration:** existing `_splineProgressTracker` likely handles arc-length advancement. New code only adjusts the speed/timing curve.

### 3.2 Motor — Locked state + Inherit/Blend deletion

**3.2.1 Delete velocity-inheritance logic (CQ5)**:

Files to modify:
- `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs`:
  - `ConsumePendingLaunchHandoffEvent` (currently around line 1937-1970): simplify — no longer set `rb.velocity = eventData.SnapshotVelocity`. velocity stays at 0 (set in handoff data Snapshot* fields = Vector3.zero).
  - `RefreshLaunchState` (around line 1976): remove Inherit / Blend / BlendAlpha handling. Replace with simple `Locked` / `Normal` switching.
  - `ApplyLaunchInheritedVelocity` (around line 2003): DELETE this method entirely.
  - Throttle / steering input gates (around line 1992-1998): remove `BlendAlpha` multiplication.
- `Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchHandoffData.cs`:
  - Keep struct but mark `InheritDurationTicks` / `BlendDurationTicks` deprecated (set to 0 always)
  - OR delete those fields and ReconcileData accordingly (wire format change — cross-check Phase 4b wire-format-frozen guarantee; if wire format must stay stable, keep fields but ignore at consume time)
- `Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchHandoffState.cs`:
  - Replace `BuddahPredictedLaunchState` enum with `Locked` / `Normal` (2 states only)
  - Remove `BlendAlpha`, `InheritEndTick`, `BlendEndTick` fields (or keep for compat, set to defaults)
- `Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchHandoffResolver.cs`:
  - `Advance()` simplified: 
    - if `state.IsActive == true && state.LockedUntilTick > currentTick` → return `Locked`
    - else → `Normal`, `IsActive = false`
  - `ProjectForArrivalTick()` may not be needed (no projection of stale handoff if handoff is just "lock until server signals start")

**3.2.2 Add Locked state behavior (CQ4)**:

In motor's tick logic (RunInputs / OnTick / equivalent):

```csharp
if (_handoffState.CurrentState == BuddahPredictedLaunchState.Locked)
{
    // 1. Freeze position at lock pose
    rb.isKinematic = true;
    rb.velocity = Vector3.zero;
    rb.angularVelocity = Vector3.zero;
    
    // 2. Discard all input — buddah doesn't move/rotate/push
    // (skip throttle, steering, hand rotation, push-skill firing)
    return;  // early return, no force application this tick
}
// else: Normal state, proceed with regular motor logic
```

**Critical**: Locked state must apply ALL of:
- 玩家按键无反应 (input ignored at gate)
- buddah 不前进 (no throttle/forward force)
- buddah 不旋转 (no steering torque)
- 手不旋转 (no hand rotation per CQ4 — confirm what component handles "hand" here)
- 不能 push (no PushHitbox skill firing during Locked)

The above bullet items map to all owner-driven motion. Server-driven state changes (e.g., pre-broadcast position correction during reconcile replay if needed) still allowed — but in this design, Locked state should not produce any state changes server-side either, just freeze.

### 3.3 Server orchestrator — countdown event broadcast

**3.3.1 Detection of all-buddah-spline-complete**:

Server-side coordinator (likely `RoomStateManager` or similar `GameNetworkManager` integration):

```csharp
private int _splineCompletedCount;
private int _expectedPlayerCount;

[ServerRpc]  // called by each buddah's spline driver on completion
public void NotifySplineComplete(NetworkConnection sender)
{
    _splineCompletedCount++;
    if (_splineCompletedCount >= _expectedPlayerCount)
    {
        BeginRaceStartCountdown();
    }
}

private void BeginRaceStartCountdown()
{
    uint countdownStartTick = TimeManager.Tick;
    uint raceStartTick = countdownStartTick + 180u;  // 3 seconds at 60Hz
    BroadcastCountdownBegin(countdownStartTick, raceStartTick);
}

[ObserversRpc]  // broadcast to all clients
private void BroadcastCountdownBegin(uint countdownStartTick, uint raceStartTick)
{
    // each client's local handler triggers Phase 3 setup
    OnCountdownBegin(countdownStartTick, raceStartTick);
}
```

Server is single source of truth for both `countdownStartTick` and `raceStartTick`. Clients schedule local actions on these specific ticks, achieving same-tick simultaneous start (per CQ3: "tick-driven").

**3.3.2 Race start broadcast**:

Server tick = `raceStartTick`:

```csharp
private void OnTimeManagerTick()
{
    if (TimeManager.Tick == _raceStartTick)
    {
        BroadcastRaceStart(_raceStartTick);
    }
}

[ObserversRpc]
private void BroadcastRaceStart(uint raceStartTick)
{
    OnRaceStart(raceStartTick);  // each client's local handler
}
```

Each client's local handler unlocks motor + transitions to Phase 4 at the broadcast tick.

**3.3.3 Late-join / out-of-order handling**:

If a player's CountdownBegin event arrives late (network jitter / packet loss), the local handler should still schedule actions based on the embedded `countdownStartTick` / `raceStartTick`, not local timing. This ensures all clients converge on same-tick unlock.

If `raceStartTick` has already passed when client receives the event, client immediately transitions (catches up).

### 3.4 Timeline trigger — race-start cinematic (CQ8 + CQ9)

**3.4.1 Intro Timeline ending (CQ8)**:

When server broadcasts `countdown_begin`:
- Client's Race-Start Timeline trigger handler runs
- Intro Timeline is forcibly stopped (immediate cut, no overlap)
- Race-Start Timeline starts playing

```csharp
public class RaceStartCinematicController : MonoBehaviour
{
    [SerializeField] private PlayableDirector raceStartTimeline;
    [SerializeField] private PlayableDirector introTimeline;  // optional: to stop it

    public void OnCountdownBegin(uint countdownStartTick, uint raceStartTick)
    {
        // Stop intro immediately
        if (introTimeline != null && introTimeline.state == PlayState.Playing)
            introTimeline.Stop();
        
        // Start race-start cinematic (含 letterbox + 3-2-1 UI per CQ9)
        raceStartTimeline.time = 0;
        raceStartTimeline.Play();
        
        // Schedule race start on local tick = raceStartTick
        // (separate handler triggered by server's BroadcastRaceStart event)
    }
    
    public void OnRaceStart(uint raceStartTick)
    {
        // Don't stop Timeline — let it play out its ending (黑边 fade-out etc.)
        // Per CQ9: Yonezawa's Timeline handles this internally
    }
}
```

**3.4.2 Letterbox black bars (CQ9)**:

- Yonezawa moves the existing letterbox Timeline asset (currently part of Intro Timeline) to standalone Race-Start Timeline asset
- Race-Start Timeline begins immediately when `OnCountdownBegin` fires
- Letterbox enters at Timeline t=0
- Designer-tunable timing for fade-out / countdown UI within Timeline asset
- Engineering side just calls `Play()` — no per-element scripting needed

**3.4.3 Time alignment**:

Race-Start Timeline duration ~3s (= 180 ticks at 60Hz) to align with countdown. If duration drifts due to designer changes, that's fine — server's `raceStartTick` is the authoritative unlock moment, Timeline is presentation-only and can run shorter or longer than 3s.

### 3.5 Auto-forward unlock — Phase 4 race start (CQ2)

When client receives `race_start` event at `raceStartTick`:

```csharp
public void OnRaceStart(uint raceStartTick)
{
    // Each owner client unlocks own motor; spectator clients unlock via reconcile/state sync
    if (predictedMotor.IsOwner)
    {
        predictedMotor.UnlockFromRaceStart(raceStartTick);
    }
}
```

Inside motor:

```csharp
public void UnlockFromRaceStart(uint raceStartTick)
{
    _handoffState.CurrentState = BuddahPredictedLaunchState.Normal;
    _handoffState.IsActive = false;
    rb.isKinematic = false;
    // rb.velocity stays at 0 (no inheritance per CQ5)
    // Auto-forward logic kicks in via existing motor tick loop
    // (the existing "自动向前" logic should already be in motor's normal tick path)
}
```

**CQ2 confirmed**: existing "auto-forward" logic in motor's normal-state tick path is what should activate. No new "auto-throttle" code needed — just unlock motor and let normal tick logic produce forward motion (whatever that mechanism is — likely a default forward force, default throttle = 1, or input-driven if player presses accelerate).

**CRITICAL implementation note**: confirm via code reading — what specifically produces "auto forward" in current motor when not under intro/external control? If normal motor requires player throttle input to move, then race start "auto-forward" might need a small initial throttle assist or default-to-full-throttle behavior. This is a clarification needed at Stage 2 RECON.

---

## 4. Out of scope

The following work is explicitly NOT part of Phase 6:

- **Phase 7.5-A.1 smoother config retrofit** (separate ship): `_extrapolation:0` + `_adaptiveInterpolation:0` + FrameRateLockGuard@120 + optional `LocalTransformTickSmoother.cs:98` vendor hack. These ship before or in parallel with Phase 6 — they fix smoother-config layer confounds independent of handoff redesign.
- **Phase 7 visual jitter measurement framework**: VisualShakeProbe, ReconcileSnapProbe, FrameTimeProbe, AnalyzeJitter.ps1 stay as-is (used to verify Phase 6 results).
- **Hitbox-visual desync (Phase 7.5-D)**: separate concern; PushHitbox prefab tree audit.
- **Phase 8 cleanup**: ClampPlanarSpeed (L9), Roslyn analyzer.
- **Teleport mid-race events**: re-spawn / mid-race teleport handoff continues using existing teleport code path (`motor.cs:1873` rb.position = TargetPosition for skill-driven teleports). Phase 6 only redesigns the LAUNCH handoff at race start.
- **Ranking / scoring / lap timing**: separate concern.
- **Disconnect handling during Phase 3 lock**: out of Phase 6 scope; existing disconnect/reconnect logic should handle.

---

## 5. Strict gates (success criteria)

### 5.1 Functional gates

- **G1**: All buddahs reach spline endpoint at velocity = 0, position = spline last point. Tolerance ≤ 5cm position, ≤ 0.1 m/s velocity.
- **G2**: After spline complete, all buddahs sit absolutely static for 3s (rb.position unchanged frame-to-frame within float epsilon).
- **G3**: Player input during Phase 3 Lock has zero effect (按键 no response confirmed via subjective + Debug.Log of input handler).
- **G4**: All buddahs unlock at exact same tick (verified via `[D-LOC HEARTBEAT]` cross-peer sync evidence — both peer logs show motor state transition at same tick).
- **G5**: After unlock, auto-forward logic produces forward motion (player can confirm subjectively + Debug.Log of motor's first-tick post-unlock motion).
- **G6**: Race-Start Timeline starts playing at countdown_begin event, plays through to ending (Yonezawa subjective verification).
- **G7**: No regression in Phase 7.5-A.1 smoother improvements (existing `pos-dp99` measurements should match or improve).

### 5.2 Subjective gates (Yonezawa-driver)

- **SC1**: Visual transition Phase 0→Phase 1→Phase 2→Phase 3 feels smooth (deceleration into stop is natural, no abrupt freeze)
- **SC2**: Lock state is decisive — no input bleed-through, no buddah micro-motion, no hand jitter
- **SC3**: Race-Start Timeline cinematic plays cleanly (letterbox fade in/out, 3-2-1 UI animations sync with countdown)
- **SC4**: Race start moment feels simultaneous and decisive — all peers' buddahs start moving at same visible moment
- **SC5**: ★ Session-level jitter variance ELIMINATED — multiple sessions all feel similarly smooth (validates H1''' fix). 5+ sessions with consistent perceptual quality.

### 5.3 Compatibility gates

- **G8**: Phase 4b prediction stack still functional (V1-V5 work not regressed). Verify via existing D-LOC / D-IMP HEARTBEAT digests showing FATAL=0 + leg-imp-div=0 across 60s gameplay post-handoff.
- **G9**: Wire format unchanged (Phase 4b post-V5 frozen). If Inherit/Blend duration fields stay in `BuddahPredictedLaunchHandoffData` for compat (set to 0 always), wire format preserved. If fields removed, MUST be coordinated with `BuddahPredictedReconcileData` payload structure to avoid Phase 6 introducing wire format breakage.
- **G10**: Existing teleport code path (skill-driven mid-race teleport at `motor.cs:1873`) still works — verify via teleport skill smoke test post-implementation.

---

## 6. PRE-WORK questions (must answer at Stage 2 RECON / Stage 3 DESIGN-QA)

Edge cases + implementation specifics:

- **Q1** Wire format: keep `InheritDurationTicks` / `BlendDurationTicks` as deprecated zero-value fields (preserve V5 wire format) OR remove + bump protocol version? Tradeoff: backward compat (Steam build) vs cleaner code.
- **Q2** Spline `T` parameter location: Inspector field on `BuddahMovement` (per-prefab default) OR per-stage Configuration asset (designer tuneable per race) OR runtime calculated (auto-derived from spline length)?
- **Q3** Detection of "all buddahs spline-complete": (a) per-buddah ServerRpc + server counter (Section 3.3.1 sketch), (b) server polls each buddah's `_splineCompleted` flag at fixed interval, (c) server tracks via `OnTickEvents` integration. Pick simplest.
- **Q4** What is the existing "auto-forward" logic in motor when not under intro/external control? Code reference + behavior clarification needed at Stage 2 RECON. (Confirm whether throttle = 1 default, or input-driven, or some other mechanism.)
- **Q5** Race-Start Timeline asset path + naming convention. New asset to create at `Assets/Cinematics/RaceStart/RaceStartTimeline.playable` (TBD with designer).
- **Q6** Server-side late-join handling: if a player joins between countdown_begin and race_start, do they (a) skip to race_start immediately, (b) get countdown synced with embedded ticks, (c) wait for next race?
- **Q7** Camera behavior during Phase 3 Lock: stays on owner buddah? Switches to overview? Locked at race-start angle?
- **Q8** What if a buddah fails to complete spline (network drop / scene load issue) — does Phase 2 detection require ALL buddahs or quorum? What's timeout behavior?
- **Q9** Reconcile + replay edge case: if client reconciles to a tick that's during Phase 3 Lock, does the replay correctly handle Locked state (no input applied during replay window)?
- **Q10** `motor.cs:1873` teleport code path interaction: handoff event ID space — should race-start LaunchHandoffEvent share ID space with mid-race teleports or separate? Affects DupReject logic.

---

## 7. Test plan

### 7.1 Path A — Single-machine 30s sanity (developer-facing)

- Start single PlayMode session
- Buddah completes spline in `T=18s`
- Buddah static for 3s (visually static, position unchanged)
- Race-Start Timeline plays during the 3s
- Race start triggers, buddah starts moving
- Manual subjective verification of all 5 phases
- Verify in Console: relevant log markers (`[IntroHandoff]`, `[RaceStart]` etc.) at expected ticks

### 7.2 Path B — 2-peer LAN 60s (multiplayer sync)

- Host + client both running, both buddahs complete spline (potentially at different real times since each is independent)
- Server detects both spline-complete → broadcasts countdown_begin
- Both clients enter Phase 3 Lock at same tick (verify via cross-peer log timestamps)
- Both Race-Start Timelines start at same tick (verify via subjective on both screens)
- Race start broadcast → both buddahs unlock at same tick
- Verify cross-peer same-tick unlock via `[D-LOC HEARTBEAT]` / motor state transition logs

### 7.3 Subjective jitter verification (re-validate H1''' fix)

- 5+ host-only PlayMode sessions, full race start → 60s gameplay each
- Yonezawa rates each session 1-10 jitter severity
- Expected: tight clustering of subjective scores (low variance), should resolve the previous 3/5 severe vs 2/5 mild distribution

### 7.4 Existing Phase 4b regression check

- Run existing V5 Path B SMOKE protocol post-Phase 6 implementation (host + client + 100ms LatencySim + 60s gameplay post-handoff)
- Verify D-LOC FATAL=0, D-IMP LEG FATAL=0, leg-imp-div=0, DropFull=0, DupReject=0
- Cross-peer Tier 1 push chain still works (impulse delivery byte-identical across peers)

---

## 8. Carry-forward flags

- **Phase 7.5-A.1** smoother config retrofit ships independently — coordinate so Phase 6 branch builds on Phase 7.5-A.1 tip
- **Phase 8** ClampPlanarSpeed (L9) — once Phase 6 lands, may need re-audit if race-start auto-forward triggers ClampPlanarSpeed at low speeds
- **Phase 7.5-D** hitbox-visual desync — independent of Phase 6; runs parallel
- **Future game-feel tuning**: `T` (spline traversal time) and 3s countdown duration are Yonezawa-tunable. Document for designers.
- **Letterbox black bars implementation**: Yonezawa moves Timeline assets pre-implementation; engineer waits for asset reference at Stage 4

---

## 9. Sign-off ledger

| Stage | Date | Signer | Notes |
|---|---|---|---|
| Kickoff | 2026-05-04 | cowork-reviewer | Contract drafted 2026-05-04 by cowork-reviewer based on Yonezawa CQ1-CQ9 dispositions. Stamped to authorize implementer Stage 2 RECON to begin in parallel with Phase 7.5-A.1 retrofit ship. Branch `feat/phase6-race-start-handoff-redesign` is NOT cut yet — Stage 2 RECON is read-only (git plumbing + code reading), no branch / no commits required. Stage 4 IMPLEMENT branch cuts from dev tip POST Phase 7.5-A.1 ship (per Section 4 out-of-scope rule). Reviewer-raised pre-RECON observations recorded in Section 11 (added 2026-05-04 alongside this stamp). |
| Recon | 2026-05-04 | cowork-reviewer | See `agent-exchange/handoff/2026-05-04-phase6-recon-verify.md` for independent grep evidence (Methodology Rule 12 honored). RECON report at `agent-exchange/handoff/2026-05-04-phase6-recon.md`. All 3 KEY FINDINGs verified via git plumbing on HEAD `8314b5d` (caller chain race-start ONLY → Section 11.1 option (a) firm; auto-forward = motor.cs:339 hardcoded throttle=1f → Q4 RESOLVED; existing `_gameplayMovementUnlocked` SyncVar chain ~90% covers Section 3.3). Pre-grep gate CLEAN for Phase 6 scope. Stage 3 DESIGN-QA authorized. 5 Open Questions disposed in verify report Stage D; OQ5 (contract location at `Docs/phase-gates/active/` vs `agent-exchange/handoff/`) routed to Yonezawa. |
| Design | ⏸ | | Q1-Q10 PRE-WORK questions resolved + spline formula choice (linear vs quadratic) + wire format decision + auto-forward clarification |
| Implementation | ⏸ | | 5 areas: spline / motor / server / Timeline / unlock |
| Smoke | ⏸ | | Path A single + Path B 2-peer + subjective re-validation |
| Verify | ⏸ | | Cross-peer same-tick unlock + G8/G9/G10 regression + SC1-SC5 driver perception |
| Merge | ⏸ | | |

---

## 10. Notes for implementer (handoff to next Claude window)

You are receiving this contract to implement Phase 6 race-start handoff redesign for the BuddahGo project (Unity 2022.3.55f1c1 + FishNet 4.6.20 + FishyFacepunch + Steam P2P).

**Priors you should know**:
- Phase 4b (V1→V5) prediction stack is mature, wire-format frozen. Don't break it.
- Phase 7 found that current handoff logic produces session-level random jitter (root cause = handoff state capture variance + driver mechanism mismatch). Yonezawa's decision: replace velocity-inherit/blend with stop-then-countdown to eliminate the root cause architecturally.
- Phase 7.5-A.1 smoother config retrofit (`_extrapolation:0` + `_adaptiveInterpolation:0` + FrameRateLockGuard@120 + vendor hack) is ALSO shipping. It fixes smoother-config-layer confounds independently of handoff redesign. Both phases ship together for full effect.
- Yonezawa is implementer-domain (driver + integrator); cowork-reviewer is review-domain.

**Recommended work order**:
1. Stage 2 RECON: read motor handoff code (see Section 1.1 for file references) + spline driver code + RoomStateManager existing event flow + existing Timeline integration patterns. Produce surface inventory.
2. Stage 3 DESIGN-QA: dispose Q1-Q10 PRE-WORK questions. Decide wire format approach (Q1), auto-forward mechanism (Q4), late-join behavior (Q6), Q9 reconcile-replay edge case.
3. Stage 4 IMPLEMENT: 5 areas in parallel where possible. Branch from dev tip post Phase 7.5-A.1 ship.
4. Stage 5 SMOKE: Path A + Path B per Section 7.
5. Stage 6 VERIFY: G1-G10 functional + SC1-SC5 subjective + Phase 4b regression check.

**Key references**:
- Existing handoff code: `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs` (key lines 1884 / 1948 / 1976 / 2003 at HEAD `8314b5d` per Stage 2 RECON verify; original draft cited 1873 / 1937 / 1976 / 2003 — uniform +11 drift on the rb-write lines from phase7 commits; methods `ConsumePendingLaunchHandoffEvent` ≈ :1908, `RefreshLaunchState` ≈ :1976, `ApplyLaunchInheritedVelocity` ≈ :2003)
- Handoff data structures: `Assets/Scripts/New_Buddah/Core/BuddahPredictedLaunchHandoffData.cs` + `BuddahPredictedLaunchHandoffState.cs` + `BuddahPredictedLaunchState.cs` + `BuddahPredictedLaunchHandoffResolver.cs`
- Bridge: `Assets/Scripts/New_Buddah/Integration/BuddahPredictionHandoffBridge.cs`
- Spline-side driver: `Assets/Scripts/Buddah/BuddahMovement.cs` (method `SyncPredictionHandoffStateFromBridge` and surrounding logic)
- Existing Phase 7 RECON Surface 7 audit (codebase architecture): `agent-exchange/handoff/2026-05-03-phase7-recon.md` Section 7
- Phase 7 SMOKE digest with H1''' identification: latest digest in `agent-exchange/handoff/`
- Phase 7.5-A.1 design framework: `agent-exchange/handoff/2026-05-03-phase7-5-design-framework.md`

**Discipline reminders** (per project methodology):
- Rule 7: PredictionRigidbody integrity — all rb.AddForce / AddTorque go through `_predictionRigidbody`, NOT direct rb. Locked state's `rb.isKinematic = true` setting is allowed (this is config not force).
- Rule 12: implementer pre-fills only Implementation + Smoke rows; reviewer fills Recon + Design + Verify rows independently.
- Rule 2 sub-clauses: any "X file unchanged" claim verified via `git diff origin/<base> -- <X>`, not via Read on working tree. Tooling changes get execution test, not just content review.
- Rule 1: raw logs at `agent-exchange/console/raw/<date>-<phase>-<role>.log` for SMOKE.
- Rule 5: lessons-log entries land in same commit as implementation that exposed them.
- Rule 11: SMOKE driver hard-precondition for time-sensitive probes — Phase 6's same-tick unlock (G4) is time-sensitive; verify via raw log evidence not just subjective.

**Open communication**: implementer at any Stage may raise questions / blockers / find unexpected edge cases. Surface to Yonezawa for disposition. Don't proceed past sign-off boundary without explicit reviewer stamp.

---

## 11. Reviewer pre-RECON observations (added 2026-05-04 with Kickoff stamp)

These are reviewer-side observations recorded at Kickoff time. They are NOT sign-offs and do NOT pre-fill any reviewer-owned row (Methodology Rule 12 honored — Recon / Design / Verify rows remain ⏸ for reviewer's independent finding after Stage 2/3/6 work). They are surfaced here so implementer's Stage 2 RECON can include them in its surface inventory and Stage 3 DESIGN-QA can dispose them alongside Q1–Q10.

### 11.1 — L12 architectural connection (Section 1.2 amendment candidate)

`Docs/lessons-log.md` L12 documents the **owner→server→owner two-hop predicted-motor RPC chain** silent-drop visibility foot-gun. The lesson explicitly names `RequestAuthoritativeLaunchHandoffFromOwner` (motor.cs:768-817) as having the SAME two-hop pattern as the L12 teleport-side method, with "Phase 6 PRE-WORK: verify same silent-return pattern and plan the same L12 fix shape there" promoted to `Docs/prediction-refactor-plan/phase-6-prerequisites.md`.

**Reviewer observation:** the new stop-then-countdown architecture (Section 2 Phase 2/3) makes race-start handoff **server-originated, single-hop ObserversRpc broadcast** (Section 3.3.1/3.3.2 sketch). This **architecturally eliminates the L12 two-hop visibility risk at race-start**, because the owner no longer initiates the handoff request — the server detects all-buddah-spline-complete and broadcasts directly. The L12 fix that Phase 6 was originally chartered to plan is not strictly needed FOR race-start under the new design.

**Implication for Stage 2 RECON:** surface inventory MUST explicitly cover `RequestAuthoritativeLaunchHandoffFromOwner` (motor.cs:768-817) AND every caller of it (likely in `BuddahPredictionHandoffBridge.cs` and/or `BuddahMovement.cs`'s spline-completion path). Determine:
1. Is this method called ONLY at race-start (spline complete) — in which case Phase 6 should DELETE the method along with its callers (the new server-driven path replaces it)?
2. OR is it also called for mid-race skill-driven handoffs (besides teleport)? — in which case the L12 fix must still be planned for the residual call sites and Phase 6 scope must explicitly bracket which callers are removed vs. retained.
3. If retained for mid-race callers, the L12 fix shape (return-leg confirmation TargetRpc, OR "don't commit to primary path on owner") should land in Phase 6 even though the renamed scope no longer requires it for race-start.

**Implication for Stage 3 DESIGN-QA:** add as Q-disposition outcome — record decision on `RequestAuthoritativeLaunchHandoffFromOwner` lifecycle (delete vs. retain) with explicit code-surface impact list. This should also resolve Section 6 Q10 cleanly (race-start LaunchHandoffEvent in new design lives entirely server-side — separate ID space from mid-race teleport events; DupReject doesn't need to span both).

### 11.2 — Q9 reconcile-replay during Locked state — promote to strict gate sub-clause

Section 6 Q9 ("if client reconciles to a tick that's during Phase 3 Lock, does the replay correctly handle Locked state — no input applied during replay window?") is currently a PRE-WORK question. Reviewer observation: this is **not a design-time question, it is a determinism-correctness gate**. Under FishNet CSP reconcile replay (Methodology Rule 7 + L16), the motor's Locked-state branch (Section 3.2.2 sketch) MUST produce **bit-identical state across N replays of the same tick range**, otherwise reconcile divergence is structurally guaranteed regardless of the rest of the design.

**Recommendation:** elevate Q9 from PRE-WORK to a strict gate sub-clause under Section 5.3 G8 (Phase 4b prediction-stack regression check). Concretely:
> **G8b** — During reconcile replay across any tick T inside `[countdownStartTick, raceStartTick)`, motor produces zero force application (`_predictionRigidbody.AddForce` count = 0 across all replays of T) AND `rb.position` / `rb.velocity` / `rb.angularVelocity` are bit-identical at end-of-replay across all N replays of T. Verify via existing `[D-LOC HEARTBEAT]` cross-replay snapshot evidence + a temporary `#if UNITY_EDITOR` Locked-state replay assertion inside motor.

**Stage 3 DESIGN-QA owns the disposition** of whether to elevate (preferred) or keep as Q9 with explicit "pass criterion = reconcile replay determinism = strictly required" annotation.

### 11.3 — Section 3.2.1 file list completeness

Section 3.2.1 lists 4 files for handoff-data simplification (`BuddahPredictedLaunchHandoffData.cs`, `BuddahPredictedLaunchHandoffState.cs`, `BuddahPredictedLaunchState.cs`, `BuddahPredictedLaunchHandoffResolver.cs`) plus motor.cs. **Missing from the list:**
- `Assets/Scripts/New_Buddah/Integration/BuddahPredictionHandoffBridge.cs` — bridge between motor and spline driver; almost certainly carries owner-initiated `RequestAuthoritativeLaunchHandoffFromOwner` plumbing per L12 Promoted-to note.
- `Assets/Scripts/Buddah/BuddahMovement.cs` `SyncPredictionHandoffStateFromBridge` (~line 370) — spline-side driver that currently writes handoff state into the bridge; new design's server-driven flow may bypass this entirely or repurpose it.
- `Assets/Scripts/New_Buddah/Config/BuddahPredictedMotorConfig.cs` — `maxSpeed` field referenced by spline-decel formula's `v_max` derivation; if formula reads from config, RECON should confirm read path.

Stage 2 RECON should produce a **complete** surface inventory across motor, bridge, spline driver, server orchestrator, and config — not just the motor-side files explicitly named in Section 3.2.1. Section 3.2.1 is illustrative, not exhaustive.

### 11.4 — Phase 7.5-A.1 confound layering

Section 1.1 M1 names "smoother baseline capture variance" as the H1''' root-cause hypothesis. Phase 7.5-A.1 retrofit (smoother config: `_extrapolation:0` + `_adaptiveInterpolation:0` + FrameRateLockGuard@120 + optional vendor hack) attacks the same M1 confound at the smoother-config layer. Phase 6 attacks M1 at the architectural layer (lock makes capture timing deterministic).

**Reviewer observation:** if Phase 7.5-A.1 ships first and ALONE eliminates the session-level jitter variance, Phase 6's M1-elimination claim becomes redundant. This does NOT invalidate Phase 6 — M2 (velocity discontinuity) and M3 (driver mechanism mismatch) remain valid problems that Phase 6 also fixes — but the SC5 "5+ session perceptual consistency" gate (Section 5.2) may already be satisfied pre-Phase-6 by 7.5-A.1 alone.

**Implication for Stage 6 VERIFY:** SC5 success rate should be measured against TWO baselines:
1. Post-Phase-7.5-A.1, pre-Phase-6 (validates 7.5-A.1's M1 fix in isolation)
2. Post-Phase-6 (validates the architectural M1+M2+M3 elimination)

If both show SC5 PASS, Phase 6's marginal contribution is in M2/M3 (velocity/driver issues) — measurable via SC1/SC2 driver subjective scores rather than SC5. This is a **scope/value-clarification concern**, NOT a blocker. Stage 6 verify report should explicitly call out which jitter axis each ship eliminated.

---

End of contract.
