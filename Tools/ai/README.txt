A1/A2 driving checks (development only)

This is the A1 single-racer, no-skill, one-natural-lap checkpoint. It uses the
existing Practice host's sole racer. A2 observes the same sole racer through three natural laps. Neither changes
the product's required lap count. AIRacerDriver is disabled on the prefab until
the explicit harness enables it; ordinary Practice input is unchanged.

Editor: open MainMenu, enter Play, then Tools > AI > A1 Run From Playing MainMenu.
Player: make a Development Windows build containing MainMenu, PropertySelection,
and RaceMap. Launch with an absolute private output folder:
  BuddahGoA1.exe --ai-a1-output C:/Temp/a1-run -logFile C:/Temp/a1-player.log
The output directory must be absent or empty. Keep Player.log outside it.
An optional --ai-a1-profile C:/Temp/profile.json overwrites fields on the default
AIDifficultyProfile. Example JSON: {"HorizonSeconds":3.0,"TargetSpeed":65.0}.
Use valid bounded values shown in AIDifficultyProfile. These are planning
preferences, not Rigidbody force/speed changes. Only Normal is experimental;
Easy/Hard mappings and multi-racer tuning remain future work.

The harness starts a Solo Practice host, submits a legal loadout/skin, disables
skill input, and enables the prefab's server AIRacerDriver. The driver outputs
only digital steering through ISteeringOverride; normal motor throttle, forces,
launch handoff, checkpoints, lap crossings, and authoritative timing still run.
There is no custom teleport/recovery or synthetic Finish. It stops observation
after RaceTiming reports one real lap, then requests the usual session teardown.
summary.success means the A1 lap milestone, not a product Match completion.

Only collision and renderer components on these five objects are temporarily
disabled. Original component enabled states and active hierarchy are recorded;
components are restored on completion/interruption. Network roots stay intact:
  RaceMap/DebugBox/DebugboxCanPush
  RaceMap/DebugBox/DebugboxCanPush (1)
  RaceMap/DebugBox/DebugboxCanPush (2)
  RaceMap/DebugBox/DebugboxCanPush (3)
  RaceMap/DebugBox/DebugboxTriggerPush
The scene asset and all other track collisions are unchanged.

Evidence: trajectory.jsonl records actual server tick/clock, position, velocity,
yaw, steering, lap, next checkpoint, track progress, lateral error, sampled last
planning cost, gaps and discontinuities at about 10Hz. events.jsonl records GO,
checkpoint changes, side-contact enters, wrong-way/stall, restore, and 60-tick
model comparisons. configuration.json and racing-line.json capture the observed run.
summary.json uses the authoritative LapSeconds. Timeout/failure stays a failure.
Side-contact-enter counts are not unique collision episodes. Camera PNGs render
the current scene camera; overlay UI is not guaranteed to appear in this capture.

Render inspectable raw evidence using Python's standard library:
  python Tools/ai/plot_a1.py C:/Temp/a1-run
This writes trajectory.svg and diagnostics.json. The gray spline reference is
not a surveyed track boundary. The plot preserves equal world X/Z scale.

Model boundaries: shared BuddahLocomotionStep force rules, real mass/yaw inertia,
drag/turn release, speed caps, steering suppression/sign, flat sliding friction.
It does not forecast wall collision resolution, arbitrary slopes, or skills.
Ground friction is inferred from current supporting collider materials; normal
physics is never changed. Runtime model windows with side contacts are reported
separately. Declared 60-tick tolerances: 0.5m position, 0.5m/s velocity, 3deg yaw.

Checks: AIDrivingTests contains the 30/60Hz real PhysX parity cases, actual floor
sliding friction, line wrap, and disabled prefab behavior. A1DrivingTools.RunChecks
also runs AcceptedLapTimingTests, MatchClockTests, and SoloSessionFlowTests, with
durable receipts under Logs/ai-a1/tests across test-runner domain reloads.
No online/Steam two-client tests are required by this A1 checkpoint.

A2: Tools > AI > A2 Run From Playing MainMenu, or a private Development Player:
  BuddahGoA2.exe --ai-a2-output C:/Temp/a2-run --ai-a2-profile C:/path/to/normal.json -logFile C:/Temp/a2-player.log
A2 waits for three authoritative LapSeconds, Finished, ResultInteractive and
stopped AI input; then restores the five boxes and requests ordinary teardown.
It never calls synthetic Finish or substitutes three separate one-lap runs.
An ordinary non-Development Player has no CLI observation harness.

normal.json captures selected V5. Inspector context menu Restore Normal (V5)
resets pace, precision and reaction together. TargetSpeed/SpeedWeight tune pace;
CorneringFactor changes curve speed preference; BeamWidth/HorizonSeconds and
ControlSeconds trade search resolution against cost; LateralWeight and
LateralVelocityWeight tune tracking; ReplanTicks/ReactionTicks control cadence.
These knobs are not calibrated Easy/Hard mappings. ValidateConfiguration rejects
nonfinite/out-of-range values; use a fresh session after invalid CLI configuration.
The five-candidate A2 budget is exhausted; future tuning needs a new task scope.

A2 plot (Python + Pillow):
  python Tools/ai/plot_a2.py C:/Temp/a2-run
Writes equal-scale per-lap trajectory.png/svg and diagnostics.json, with contact
markers and discontinuity breaks. Raw samples remain the source of truth.
See Docs/single-player/a2-results.md and a2-results.json for measured outcomes.

Corner-entry spin fix (2026-10-02): see Docs/single-player/ai-spin-fix.md.
Normal profile is unchanged. The planner rejects extra heading windings relative
to the locally turning route. Real three-lap average 126.044s is slower than V5;
do not call this a pace improvement or resume the exhausted tuning search.
Harness additionally writes plans.jsonl: tick/clock, target/tangent, segment,
progress/lateral/pace, first-key costs, selected yaw change/rate, winding rejection
count and viable-first-key count. Nonserializable in-memory MotionState and
MotionParameters fields are not emitted by JsonUtility; real body pose/velocity
and steering remain in trajectory.jsonl, with separate visualYaw/cameraYaw.
plot_spin.py aligns each real sample to the latest plan without synthesizing
physics samples. It requires matplotlib and a root containing evidence/baseline
and evidence/fixed. Example: python Tools/ai/plot_spin.py <task-18-root>.

---- 2026-10-03 thrust-vector controller and difficulty tiers ----
Profiles: difficulty-{easy,normal,hard}.json are the shipped tiers (generated into
Assets/Resources/AI/*.asset by AIDifficultyAssetTool.Build). thrust-vector-*.json are the
raced variants (design = line follower without walls; wall* = wall corridor; nobrake* = Hard
lineage). normal.json remains the V5 beam baseline (UseThrustVector absent/false).
Harness flags: --ai-count N (server AI), --ai-profiles "p0;p1;..;p5" (one profile per car,
p0 = this racer), --ai-product-profiles (keep the spawner's difficulty profiles),
--ai-difficulty easy|normal|hard, GO+2..11 s frame window in summary.json, race-results.json
per racer. Scripts and evidence: thrust-vector/scripts/README.md, thrust-vector/rollout-2026-10-02/.


---- 2026-10-03 five skill personalities (A3) ----
Product Solo is fixed to one human and all five configured personalities. Their
stable ids, display names, fixed three slots, opportunity parameters and skill
behaviour by difficulty live in Assets/Resources/AI/SkillPersonalities.json.
Driving assets remain independent. Rematch keeps personality/name/loadout;
new racers get fresh independent commitment and random state.

A3 natural evidence uses a separate operator, without taking over the human:
  BuddahGoSoloDev.exe --ai-skill-race-output C:/Temp/skill-race \
    --ai-difficulty Hard --ai-skill-seed 243 -screen-fullscreen 0 \
    -screen-width 1280 -screen-height 720 -logFile C:/Temp/skill-race-player.log
Use a fresh, absolute output directory. Optional --ai-skill-window-seconds 95 stops after an explicitly labelled diagnostic
window (not a natural finish). Performance records use 30s warmup and 60s capture,
with raw skill-perf.csv and mean/median/p95/p99/max. skill-perf.csv also carries ai_skill_input_ms (the AI.Skill.Input share: key injection, pushes, spawns) and summary.json reports SkillInputMeanMs / SkillInputMaxMs / SkillInputShareOfPeaks (input share of AI.Skill time in frames over 0.2 ms). --ai-skills-off supplies the
same-match-load comparison without casting; --ai-skill-quiet disables per-decision
and combat event observers. The diagnostic seed affects per-racer NoiseSeed;
physics is not claimed to replay bit-for-bit. Each resulting driving profile
and the full skill catalog are saved with the run.

Outputs: trajectory.jsonl (10Hz, six racers, actual motor effect state),
skills.jsonl (decision/candidate/key/request/accept/execute/cancel), combat.jsonl
(projectile launch, detected/routed hits and trap requests), events.jsonl and
summary.json. A DNF has null FinishSeconds, final lap/checkpoint/distance and a
reason; it never receives the race cutoff time as a completion time. The human
receives no injected steering/skills, remains a physical racer and can be hit.
Keep the product's 15-second end rule. The operator returns Home after the actual
result panel, never via synthetic Finish. AI.Skill timing is a per-frame sum;
CaptureDetails and measured FPS/vSync are explicit in the summary.

Editor controlled fixtures, starting from Play mode at idle MainMenu:
  AISkillEffectHarness.Begin(absoluteDirectory, backlash: false/true)
  AISkillLifecycleHarness.Begin(absoluteDirectory)
These intentionally use runtime-only 0/100 backlash probability, authoritative
fixture teleports/direct casts, or synthetic completion for boundary tests.
Their output is explicitly labelled controlled and cannot qualify natural racing.
The lifecycle fixture invokes the real Rematch and quit button callbacks.
  python Tools/ai/analyze_skill_matrix.py C:/Temp/effects --output C:/Temp/effects/analysis.json

A1/A2 --ai-profiles comparisons keep AI casting off by default; --ai-skills-on
opts into the skill layer for diagnosis, but an A1/A2 human takeover is not A3.
AISkillAcceptanceTools.RunTests(absoluteXmlPath) writes a persistent XML receipt
through Test Runner domain reloads. The set contains Solo and affected shared
logic only, not Steam/online runs. ThrustVectorAcceptanceBuild supports
THRUST_BUILD_OUTPUT, THRUST_BUILD_NAME (an .exe filename) and THRUST_BUILD_RELEASE=1.
Non-Development builds contain no CLI/effect/lifecycle harness.
