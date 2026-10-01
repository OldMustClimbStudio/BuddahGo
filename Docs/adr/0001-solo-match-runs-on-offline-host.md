# Solo Match runs on the online stack through an offline host

A Solo Match starts FishNet as host (server and client in one process) on FishNet's bundled offline transport Yak. It reuses all server-authoritative code: rooms, selection, race, placement, results, skills and prediction. AI Racers are server-spawned network objects. We chose this so that solo and online always run the same gameplay code and solo cannot drift away from online over time.

## Considered Options

- **Separate non-networked solo code path**: rejected. It would duplicate the room, selection, race and result flow, and two implementations would have to be maintained forever.
- **Tugboat on a loopback socket**: rejected. It opens a real port, which can trigger firewall prompts, and offers nothing over Yak.

## Consequences

- The NetworkManager uses Multipass with FishyFacepunch (online) and Yak (solo), and the session launcher picks which one to start. Multipass initializes every child transport, so FishyFacepunch must tolerate a missing Steam client (ADR 0004).
- Anything keyed by connection or Steam identity (roster, names, placement, owner-targeted RPCs) has to work for owner-less AI Racers.
