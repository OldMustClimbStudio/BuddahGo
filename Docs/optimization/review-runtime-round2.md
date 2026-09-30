# Review follow-up: local runtime evidence (2026-09-30)

This is a staged delivery for PRs #49/#50/#52/#56/#57/#58. It does not close all seven
new review comments. No historical commits are rewritten. All sessions use one source
version for both the local host and the separate pure client, Unity 2022.3.55f1c1,
Tugboat localhost, and latency configured on both peers before connection and GO.
These are two real Unity Editor processes, not helper-only tests or cross-machine Steam tests.

## Functional change and attribution

- #50: remove prediction/combo screen diagnostics as authorized; retain Editor/Development
  logs with a 0.5-second consumer-driven snapshot/health cadence. Release does not regain
  stripped Verbose logging. Gameplay HUD, prediction state and RPC order are unchanged.
- #57: pure-client impulse replay drains the event channel using FishNet's historical
  `ServerReplayTick`. Forward simulation retains the live server clock; server/host
  retain their local authoritative clock. Replays are not disabled.
- #58: four impulse clock/channel tests, diagnostic sampling/wiring tests, session-helper
  documentation, and this evidence record. Observation harness changes are not shipped
  in the gameplay assembly.

## N10 and first-second correction

The real original P6 `f7f6c7f` baseline skips client Blend at both 0 and 100 ms.
The translated working-copy handoff restores it. The authoritative configured windows
are Inherit 12 ticks and Blend 20 ticks. At 0 ms, the full corrected matrix observed
Blend for 20 client ticks, with Normal aligned to server tick 5905.

At 100 ms the corrected window is server 6122/6134/6154 -> client 2696/2708/2728.
The first client Blend observation is server tick 6135, one tick after the boundary:
at local 2708 it still uses the owner-anchored TargetRpc window (2705/2717/2737);
the next usable reconcile replaces it with the translated authoritative window. Normal
aligns to 6154. This is not evidence of identical observed phase duration on every peer.

Actual transform correction is measured immediately before/after Rigidbody reconcile,
using records whose UTC is in [that peer's GO, GO + 1.000 seconds). The older `fixed-0`
rigidbody-position probe did not measure this correctly and is excluded.

| Source/run | Delay | Owner samples | Mean distance | Maximum distance |
| --- | ---: | ---: | ---: | ---: |
| original P6 / baseline-0 | 0 ms | 58 | 0.780975 | 6.978768 |
| fixed / replay-fixed-0 | 0 ms | 59 | 0.607804 | 5.905836 |
| original P6 / baseline-100 | 100 ms | 55 | 1.121102 | 7.304865 |
| N10 before impulse fix / fixed-100-matrix | 100 ms | 58 | 0.713265 | 16.934994 |
| fixed / replay-fixed-100 | 100 ms | 56 | 0.836957 | 4.154471 |

These are individual runs with different startup offsets, not a statistical claim that
peak corrections always improve. The first second precedes the controlled combat fixture.

## R6 / R7 and charged timing

The real-input matrix exercises collision, melee push, ordinary/charged/burst projectiles,
and RespawnToTrackProgress with host and client casters. Server projectile hits and
resulting impulse consumption are checked, not merely visual spawn counts. Burst visual
objects do not apply authoritative hits. All completed corrected runs have loc/tel/mod/hof
maximum divergence 0 and no captured runtime errors.

- 100 ms complete matrix: both casters hit via melee and all three projectile modes;
  both respawn requests route successfully; collision callbacks occur on both peers.
- 0 ms complete matrix: all projectile/collision/respawn cases complete; the original
  moving host-melee fixture missed. This is retained as a failed fixture assertion.
- `push-contact-0`: with only fixture forward acceleration disabled, both casters hit via
  the real keyboard -> RPC -> hitbox path. Each server victim consumes one impulse;
  the pure client's matching event 7904 is consumed at replay tick 7904, pass 1664,
  raw consumption sequence 1. No duplicate within that pass is present.

Before the impulse fix, the 100 ms matrix has 12 client consumptions occurring 1-7
historical ticks early. After the fix, the full 100/0 ms matrices have respectively
13/10 client consumptions at the matching event tick (earlyBy=0). Repeated application
in a distinct rollback pass would not by itself be classified as a duplicate.

True pre-P5 `fea4059` timing sessions and current sessions both configure charged action
delay 0.9 seconds and charged push cooldown 1.0 second. Across both casters and 0/100 ms,
pre-P5 measured animation delays are 0.900162-0.915515 seconds, current regular charged
cases 0.900131-0.911712. The 0.4-second retry is rejected and the later >1-second press
is accepted. Exclude high-speed screenshot cases from this comparison because recording
adds frame scheduling overhead. The first pre-P5 0 ms launch timed out because client
started before host; the retained retry starts host first and completed on both peers.

## R9 scope and remaining review items

Every reconcile is observed with its raw snapshot, translated window and current offset.
Startup/stable timing offsets do jump. The new 0 ms contact run also records a -2 tick
change during Blend at client local 2603/server 7382, clientStateTick 2591,
serverStateTick 7375, pass 1142. The translated deadline moves from 2609 to 2611;
Normal still occurs at server 7390. No expired-window resurrection was observed.
This is not proof that arbitrary clock jumps cannot revive an expired state. Keep the
100 ms active-window resurrection question open pending focused evidence; do not change
the mapping algorithm solely from a static suspicion.

An additional 100 ms session (`visual-corrected-100`) records a +1 offset change at
client local tick 8050/server 9367, snapshot client/server ticks 8028/9354, pass 1160.
The historical snapshot is still Blend; its translated end moves 8043 -> 8042.
Forward simulation immediately before and after remains Normal/inactive. Reading the
historical snapshot's active flag alone would falsely classify this as resurrection.
This observation does not exercise a negative jump crossing a just-expired deadline;
that specific boundary remains unobserved. No clock algorithm change is justified by
this sample. [Selected original records](evidence/review-round2/cycle-r9-selected.jsonl)
include both sides of the update and both peers' completed rematch/return-to-room flow.

## High-speed visual capture and shader environment

The original magenta rectangles are an actual import-cache error: affected fire/smoke
materials resolved to `Hidden/GraphErrorShader2` rather than their source ShaderGraph.
The main graph and its four SubGraphs contain valid text and retain their dependencies.
Force-reimporting only `Assets/Plugins/Piloto Studio/Shaders_Reforged/UberFXSG.shadergraph`
restores `Piloto Studio/UberFXSG` without modifying any source asset, renderer, color or
effect scale. The reimport does not reproduce the original graph error, so its original
cause is not established. `isSupported=true` on the error shader was not a valid check.

The next run still showed Unity's cyan asynchronous-compilation placeholder. The final
`visual-warm-100` run synchronously compiles all passes of the seven referenced materials
before capture, including a readiness barrier in RaceMap. Both peers completed; all
137 recorded frames have `shaderCompiling=false`. Inspected frames show the expected
orange/yellow fire, without the earlier magenta/cyan rectangles. Material, texture,
camera and frame records are in [visual evidence](evidence/review-round2/visual-warm-summary.json).

Case 12 follows host caster object 2 on the host (owner) and pure client (observer).
The authoritative caster stays at 80 u/s through all 31 owner frames; the observer has
38 frames. Simulation latency is enabled at 100 ms on both peers; recorded RTT is
240 ms. Case 13 follows client caster object 3; the first shot is near the 80 u/s segment,
but later frames slow down, so the entire clip must not be described as constant speed.
Observer Rigidbody velocity is not a measurement of interpolated visual-root speed.

The host-caster emission samples place the authoritative start about 20.00 units behind
the owner renderer's configured emission anchor; corresponding observer samples are
0.00 and 18.47 units ahead. These are measured at visual creation, relative to the
configured anchor after its authored offsets, not an animated hand-bone distance.
They include reconciliation/interpolation and recording overhead; the simplified
80 u/s * 100 ms = 8-unit estimate is not the measured result of this session.
The authority/world-space design remains unchanged. Recordings contain only game
rendering and remain local pending permission to publish; final visual acceptance is
the user's decision. Earlier magenta/cyan diagnostic clips are not acceptance footage.

R8 scene measurements are still in progress; no allocation or frame-time improvement
percentage is claimed from method tests.

## Reproduction and evidence

Local raw evidence is retained under the task-only `bgr2-results` directory. Compact
extracted events, source hashes and summary accompany this record when generated.
Main fixture sequence: natural start -> 4 seconds -> unsaved floor -> 14 seconds/case;
real virtual Input System keyboard presses at 5.0/5.4/6.15 seconds, real skill activation
at 2 seconds, then real owner respawn requests. No scene or user prefab is saved.

Integrated Unity compilation and EditMode: **75/75 passed**, no failures/skips,
2026-09-30 17:26:40 UTC. Includes all three diagnostic wiring tests and four new
impulse boundary/channel cases. [NUnit result](evidence/review-round2/editmode-results.xml),
[compact runtime evidence](evidence/review-round2/runtime-summary.json),
[charged timing comparison](evidence/review-round2/charged-timing-comparison.json).
The tested working copy differs from the publication runtime only by opt-in observation
probes; gameplay source changes are identical.
