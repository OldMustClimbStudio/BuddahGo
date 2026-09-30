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
A Solo Match with zero AI Racers. Results show times only, with no placement.
_Avoid_: free roam, training mode, time trial

**Skip Spectating**:
The Human Player's choice, after crossing the finish line in a Solo Match, to end the race at once instead of watching the remaining AI Racers. Placement then follows the same rule as when the post-finish countdown runs out.
_Avoid_: skip, end race, forfeit

**Pause**:
A state, available only in a Solo Match, in which the race is frozen until the Human Player resumes it.

### Participants

**Racer**:
Anyone taking part in a race, whether a Human Player or an AI Racer. Progress, finishing, placement and results belong to a Racer, not to a network connection.
_Avoid_: player (when AI Racers are included), participant, entry

**Human Player**:
A person controlling a Buddah from their own device.
_Avoid_: user, client, connection

**AI Racer**:
A computer-controlled Buddah that races in a Solo Match, casts skills with its own loadout and is ranked alongside the Human Player. Its name comes from a configurable list.
_Avoid_: bot, NPC, CPU

### AI Behaviour

**Difficulty**:
The Easy, Normal or Hard setting of a Solo Match. It sets how precisely the AI Racers plan their steering and skill use; it is not a target lap time.
_Avoid_: AI level, skill level

**Steering Plan**:
An AI Racer's choice of which steering key to hold for the next few moments. It is made by predicting ahead with the same movement rules that govern every Buddah. AI Racers press the same left/none/right keys that a Human Player has.
_Avoid_: AI path, autopilot

**Fumble**:
An AI Racer's deliberate, occasional wrong key press, which keeps its driving comical. The Fumble rate is tuned separately from Difficulty.
_Avoid_: noise, error rate, mistake
