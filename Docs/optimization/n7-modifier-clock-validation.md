# N7 — modifier deadline clock

## Reproduction and cause

P2 and P3 skill matrices both reproduced SlowTrap anti remaining rooted on the pure client for the rest of a 21-second case, while the server completed the configured 1-second root and 2-second acceleration. This also invalidated duration checks for other modifiers despite correct peak values.

`ReconcileState` copied server deadline ticks directly into client working state. FishNet `TimeManager.LocalTick` is explicitly unsynchronized (server returns Tick; client returns its own counter), while modifier resolution uses LocalTick. The two editors started at different times, so the observed offset was about 1,828 ticks. The old code interpreted that offset as additional effect lifetime.

The fix translates the eight deadline fields on the pure client's working copy using the current estimated server tick and local tick. Server state, reconcile payload layout, RPCs and effect parameters are unchanged. Arithmetic widens before subtraction: the bundled `TickToLocalTick` implementation subtracts unsigned values before widening, which is unsuitable for future deadlines. Zero remains the unset sentinel; out-of-range results saturate.

## Validation

- R1: both Editor instances compiled with no C# errors before the test.
- `Tools/Validation/modifier-clock-cases.cs.txt`: 15 checks passed (seven deadline boundary/offset cases, eight deadline fields), with all nondeadline values and source state preserved.
- Focused real-transport matrix: all 16 cases passed on 2026-09-30 06:36 UTC. Four effect types × host/client caster × 0/100ms latency; both peers sampled each case. Includes acceleration, root followed by acceleration, scale and reverse steering, and checks return to baseline.
- Each peer observed all 16 casts (8 owned); the server queued and executed each exactly once. Expiry timestamps differed by at most 0.110 seconds across peers (sampling interval about 0.1 seconds). All effects returned to baseline; all four SlowTrap anti combinations entered their post-root acceleration. Host/client completed replay and room return, with 570/283 heartbeats and zero loc/tel/mod/hof divergence, registry mismatch, Error or Exception. The client summary `cases=0` is a host-only counter; its 16 `caseObserved` records and eight owned casts establish coverage.
- The existing P3 48-case evidence covers projectile origin/direction/hits, Feel brightness/shake and owner/observer paths; this focused test addresses the newly identified clock defect.
- Local host/client use Tugboat and ParrelSync. Steam lobby discovery, a second physical machine and a complete driven three-lap race are not covered by this fixture.

Logs: `Logs/n7-modifier-matrix/host.jsonl` and sibling clone `Logs/n7-modifier-matrix/client.jsonl`; analyzer: `Logs/analyze-n7-modifier.py`.
