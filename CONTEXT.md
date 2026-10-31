# BuddahGo

A party racing game where Buddah statues race laps and disrupt each other with skills. A match is played either online with friends or solo against AI.

## Language

### Matches

**Match**:
One full play-through, from property selection through the race to the results. It ends with a rematch or a return.
_Avoid_: game, session, round

**Online Match**:
A Match played through a Steam lobby by two or more Human Players.
_Avoid_: multiplayer mode, lobby game

**Solo Match**:
A Match with exactly one Human Player and zero to five AI Racers at a chosen Difficulty. It is played fully offline, and its results offer rematch/return buttons instead of a vote.
_Avoid_: single-player mode, offline mode, bot match

**Practice**:
A Solo Match with zero AI Racers. Three Training Dummies are spawned on the first straight as targets, and results show times only, with no placement.
_Avoid_: free roam, training mode, time trial

**Match Rules**:
The set of behaviours that differ between an Online Match and a Solo Match, such as voting versus rematch/return buttons, selection timeouts, early finish and Pause.
_Avoid_: mode flags, solo checks

**Match Clock**:
The single pausable clock for in-race timing, based on the server tick. Race timing and the end-of-race countdown use it from the start; the remaining in-race timers move onto it when Pause is built. It stops while the race is paused.
_Avoid_: game time, timer, real time

**Lap Time**:
The Match Clock duration a Racer needs for one lap. The sum of all Lap Times is that Racer's race time.
_Avoid_: split, lap duration

**Skip Spectating**:
The Human Player's choice, after crossing the finish line in a Solo Match, to end the race at once instead of watching the remaining AI Racers. Placement then follows the same rule as when the post-finish countdown runs out.
_Avoid_: skip, end race, forfeit

**Result Area**:
The walkable end-of-race zone where finished Racers are placed while results are shown. Finished AI Racers are parked there and stop driving.
_Avoid_: end field, podium

**Pause**:
A state, available only in a Solo Match, in which the race is frozen until the Human Player resumes it.

### Participants

**Racer**:
Anyone taking part in a race, whether a Human Player or an AI Racer. Progress, finishing, placement and results belong to a Racer, not to a network connection.
_Avoid_: player (when AI Racers are included), participant, entry

**RacerId**:
The identity of a Racer within one Match. For a Human Player it equals their connection's client id; AI Racers use 10000 plus their index.
_Avoid_: client id (for Racers), player id, owner id

**Human Player**:
A person controlling a Buddah from their own device: holding W drives it forward (releasing only stops the drive, there is no brake), A/D steer, the mouse aims and pushes the hands, Q/E/R or 1/2/3 cast. Forward is not automatic (since 2026-10-30); see Drive in [GLOSSARY.md](GLOSSARY.md).
_Avoid_: user, client, connection

**AI Racer**:
A computer-controlled Buddah that races in a Solo Match, casts skills with its own loadout and is ranked alongside the Human Player. It presses no keys: it keeps the same drive switch a Human Player holds with W on until it finishes. Its name comes from a configurable list.
_Avoid_: bot, NPC, CPU

**Training Dummy**:
A parked, ownerless Buddah that only Practice spawns as a target for skills, pushes and skill synergies (缘起, see [GLOSSARY.md](GLOSSARY.md)): three of them, 15 m apart along the first straight past the start line, left, centre and right. It is not a Racer: it stays parked and never casts, and its RacerId stays unassigned, so it never runs laps or timing or appears in the ranking and results. Normal physics, impulses and skill effects such as slows, inversion and freezes act on it, and an overhead readout shows the effects currently on it. Once displaced, it returns to its spawn point 3 s after the last skill effect, impulse or contact with a moving body.
_Avoid_: bot, target AI, NPC

### AI Behaviour

**Difficulty**:
The Easy, Normal or Hard setting of a Solo Match. Each tier is an AI profile that sets the AI Racers' target speed, whether they may brake (Hard never brakes), how much they avoid walls, their reaction delay and planning precision, their per-car randomness and their Fumble chance; together these give each tier its own race pace. Each tier also sets how AI Racers use skills: how much backfire risk they accept (one stop threshold per tier since the unified backfire), their decision and reaction time and their aim tolerance. AI Racers request casts through the same server entry as a Human Player's key press; combo key mistakes went away with combos (A7, 2026-10-28).
_Avoid_: AI level, skill level

**Steering Plan**:
An AI Racer's choice of which steering key to hold for the next few moments. It is made by predicting ahead with the same movement rules that govern every Buddah. AI Racers press the same left/none/right keys that a Human Player has.
_Avoid_: AI path, autopilot

**Racing Line**:
The path an AI Racer aims to follow: the track spline plus a configurable lateral offset per AI Racer.
_Avoid_: AI path, waypoints

**Catch-up**:
Adjusting AI Racers' decision quality by their distance to the leader: those behind fumble less and plan more precisely. It never changes the movement rules.
_Avoid_: rubber banding, boost

**Stuck Recovery**:
The server returning an AI Racer to the track after its progress along the track has stalled or it has driven the wrong way for too long.
_Avoid_: unstuck, AI respawn

**Fumble**:
An AI Racer's deliberate, occasional suboptimal choice (taking its second-best thrust candidate for one selection, plus a slow wobble on its thrust angle), which keeps its driving imperfect and varied. The Fumble chance is part of each Difficulty tier, not a separate setting.
_Avoid_: noise, error rate, mistake
