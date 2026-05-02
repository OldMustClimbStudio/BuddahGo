# Phase Gate Methodology

Process discipline rules. Every agent (implementer, reviewer) must follow them.
Violations are merge blockers.

## Rule 1 — Raw log discipline

Every smoke test produces a **complete-session raw log on disk** at
`agent-exchange/console/raw/<date>-<phase>-<role>.log`.

- DO use Editor.log full-session dump or Player.log full-session dump.
- DO use convention `YYYY-MM-DD-<phase-id>-<role>.log` (role: single / host / client).
- DO NOT use Unity MCP console window dump as the authoritative artifact —
  it has a 2000-line / 30-min window limit and misses spawn-window evidence.
- DO NOT skip raw logs even if "the digest captured everything".

**Session-scoping (added 2026-05-02 from V2b Step 1 Path A verification finding):**
`Editor.log` does NOT auto-clear between PlayMode sessions — it appends. A naive
file-wide grep on a multi-session log will count historical entries from prior
phases (e.g., post-Step-1 dump showed 85 historical `[D-IMP INV ...]` lines from
3 prior sessions, polluting strict-mode metrics).

Mitigation (pick one per smoke):
- (A) Delete or rename `Editor.log` BEFORE entering PlayMode. Cleanest.
- (B) Note the Step session start line in the digest (find first occurrence of
      a Step-specific log marker, e.g., `[D-IMP LEG HEARTBEAT]` for Step 1+).
      Reviewer scopes greps to `offset:` past that line.
- (C) Restart Editor before the smoke run (forces a clean Editor.log on next
      launch, depending on Unity version's log rotation policy).

Reviewer MUST verify session boundary before reporting metrics. If digest
cites a session-scoping window, reviewer's independent grep MUST honor the
same scope.

The raw log is the immutable session record. The digest is derivative.

## Rule 2 — Independent verification

Reviewer agent independently greps raw logs, NOT trust implementer's digest values.

Mandatory greps per smoke (adjust prefix per active contract):
- `D-IMP INV FATAL` count (or post-Step 1: `D-IMP LEG FATAL`)
- `D-LOC FATAL` count
- `inv-imp-div=[1-9]` (post-Step 1: `leg-imp-div=[1-9]`) — must be 0 hits
- HEARTBEAT line counts
- `CommandBus.*ClearAll` count (must match `4 × N spawns`)
- `CommandBus.*FirstInvoke` count (≤1 per channel per session)
- `DropFull` count (must be 0)

If any digest value disagrees with grep, reviewer halts the PR until reconciled.

## Rule 3 — Strict gate definition lives in the contract

"FATAL=0" means different things in different phases. Active contract
(`Docs/phase-gates/active/<phase>-contract.md`) is the single source of truth
for what counts as PASS in this phase. Don't assume previous phase's gate applies.

## Rule 4 — Sign-off ledger is sequential

Each phase has 6 sign-off boundaries:
1. Kickoff
2. Recon SIGN-OFF
3. Design SIGN-OFF
4. Implementation SIGN-OFF
5. Smoke SIGN-OFF
6. Merge

You CANNOT skip ahead. Implementer waits for reviewer SIGN-OFF before moving
forward. SIGN-OFFs recorded in contract's sign-off ledger with date + signer.

## Rule 5 — Lessons-log update timing

Every PR includes new L# entries discovered DURING the phase, in the SAME
commit as implementation. Post-merge "I'll add the lesson next week" is
forbidden — context fades, entries don't get written.

If phase exposed no new failure modes, PR description must explicitly state
"No new lessons; existing rules covered all cases."

## Rule 6 — PRE-WORK persistence

PRE-WORK callouts (decisions affecting future phases) MUST be written into the
FUTURE phase's contract MD file under PRE-WORK section, NOT only in the task
tracker. The task tracker is volatile; the contract MD is durable.

## Rule 7 — PredictionRigidbody integrity (4b-specific, Step 1+)

Any rb force/torque write MUST go through `_predictionRigidbody.AddForce` /
`_predictionRigidbody.AddTorque`, NEVER `rb.AddForce` / `rb.AddTorque` directly.
Direct rb writes silently break reconcile replay determinism — failure
manifests as visible jitter, not as FATAL.

This is a code review hard-block.

## Rule 8 — No-skipping recon → design → implement

For high-risk phases (any authority-flip / data-flow-change phase, or any
phase with `risk: high` in contract), implementer MUST:
1. Submit recon report → reviewer SIGN-OFF
2. Submit design Q&A → reviewer SIGN-OFF
3. Then and only then implement

Skipping straight to implementation is a process violation regardless of how
"obvious" the design appears.

## Rule 9 — Carry-forward flag handling

If a phase audit identifies a question affecting future phases, document it:
- In current PR description under "Carry-forward flags"
- In future phase's contract under PRE-WORK section
- Optionally in task tracker for visibility

Carry-forward flags MUST NOT be silently actioned in the current PR. Each
flag is its own future decision.
