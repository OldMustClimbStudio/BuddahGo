# AI Racers steer by predicting ahead with the real movement rules

A Buddah moves with no linear drag. Its forward force always follows its facing, and steering applies yaw torque that keeps building until the key is released. Lateral velocity therefore persists, and simply "aiming at a point ahead" snakes badly. AI Racers press the same left/none/right keys as a Human Player. Every few ticks they predict a short distance ahead with the same force, torque, inertia, turn-decay and speed-cap rules the motor uses, and hold the key sequence that tracks the racing line best. The movement itself is never changed for AI, so the comical sliding is kept.

Difficulty only sets how precisely the AI predicts: horizon, replanning interval, reaction delay and candidate resolution. Comical mistakes come from a separate Fumble rate.

## Considered Options

- **Heuristic steering (heading error plus counter-steer)**: deferred. With zero drag it is brittle, and skill effects that change force or mass break its tuning. The AI decision-making layer stays behind a replaceable interface, so Easy may use this approach later.
- **Analog steering or a car-like mover for AI**: rejected. The AI would drive unnaturally smoothly and lose the comedy of the game.

## Amended (2026-10-03)

The shipped planner is `ThrustVectorPlanner` (`559d57c`); it still predicts with the shared motion model, presses only left/none/right and never changes the movement rules. The shipped Difficulty tiers (`Tools/ai/difficulty-{easy,normal,hard}.json` -> `Assets/Resources/AI/{Easy,Normal,Hard}.asset`) vary more than prediction precision: target speed 67 / 74 / 80 m/s, maximum thrust angle 170 / 170 / 90 degrees (Hard never brakes), wall-contact weight 0.7 / 0.2 / 0, reaction delay 8 / 3 / 0 ticks plus jitter, rollout length and replanning interval, and per-car speed, line-offset and thrust-angle noise. Fumble is no longer a separate rate: each tier carries its own `MistakeProbability` 0.25 / 0.1 / 0.03 (per selection, take the second-best candidate). Difficulty therefore sets the race pace band. See [the thrust-vector spec](../single-player/thrust-vector-controller-spec.md), section 13.
