# AI Racers steer by predicting ahead with the real movement rules

A Buddah moves with no linear drag. Its forward force always follows its facing, and steering applies yaw torque that keeps building until the key is released. Lateral velocity therefore persists, and simply "aiming at a point ahead" snakes badly. AI Racers press the same left/none/right keys as a Human Player. Every few ticks they predict a short distance ahead with the same force, torque, inertia, turn-decay and speed-cap rules the motor uses, and hold the key sequence that tracks the racing line best. The movement itself is never changed for AI, so the comical sliding is kept.

Difficulty only sets how precisely the AI predicts: horizon, replanning interval, reaction delay and candidate resolution. Comical mistakes come from a separate Fumble rate.

## Considered Options

- **Heuristic steering (heading error plus counter-steer)**: deferred. With zero drag it is brittle, and skill effects that change force or mass break its tuning. The AI decision-making layer stays behind a replaceable interface, so Easy may use this approach later.
- **Analog steering or a car-like mover for AI**: rejected. The AI would drive unnaturally smoothly and lose the comedy of the game.
