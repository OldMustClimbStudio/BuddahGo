# Racer identity is separate from network connection

Race progress, finishing, lap times, placement, result presentation, spectating and display names are keyed by a RacerId rather than by connection or owner id. AI Racers are server-owned objects whose `OwnerId` is always -1, so every system keyed by owner would merge all AI Racers into one entry. Patching each system with its own "if AI" branch would repeat the duplication the architecture audit flagged.

A Human Player's RacerId equals their connection's ClientId (0 or above). AI Racers use 10000 plus their spawn index; negative values are not used because -1 already means "invalid" throughout the code. In an Online Match, Racers and Human Players map one-to-one with the same numbers, so online behavior is unchanged.

## Consequences

- `RankEntry.ClientId` is renamed to `RacerId`. Its type and declaration order stay the same, so the FishNet wire layout is unchanged.
- Connection-scoped concerns (roster, ready state, votes, owner-targeted RPCs) stay keyed by connection. Only race data moves to RacerId.
