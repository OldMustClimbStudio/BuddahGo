A1 driving check (development only)

This is the A1 single-racer, no-skill, one-natural-lap checkpoint. It uses the
existing Practice host's sole racer. It does not create an A2/A3 race or change
the product's required lap count. AIRacerDriver is disabled on the prefab until
the explicit harness enables it; ordinary Practice input is unchanged.

Editor: open MainMenu, enter Play, then Tools > AI > A1 Run From Playing MainMenu.
Player: make a Development Windows build containing MainMenu, PropertySelection,
and RaceMap. Launch with an absolute private output folder:
  BuddahGoA1.exe --ai-a1-output C:/Temp/a1-run -logFile C:/Temp/a1-run/Player.log
Create the folder before launch. Do not reuse an existing evidence directory.
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

Only these five objects are temporarily disabled; original activeSelf values
are recorded and restored on completion/interruption:
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
Side-contact-enter counts are not unique collision episodes. Screenshots from
hidden Players may be black; do not treat them as visual driving evidence.

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
