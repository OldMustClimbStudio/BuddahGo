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

**Post-smoke event sanity check — sub-rule (D) (added 2026-05-02 from V3 Path A first-attempt finding):**
Before dumping the raw log to `raw/`, implementer/user MUST self-verify that
expected events actually landed in the session. Spawn-only artifacts (e.g.,
`[PushHitbox] Spawn` without `[PushHitbox] Hit`, or `[ProjectileSpawned]` without
contact) DO NOT count as test coverage.

Self-check pattern:
- Find a downstream marker confirming real event consumption (e.g., for Step 1+
  smoke: `[D-IMP LEG HEARTBEAT]` showing `leg-imp-compared >= contract minimum`).
- If marker absent or counter is 0, test is invalid — re-run, do NOT dump.
- "I ran PlayMode for 30s" is not enough. "I observed N events processed in
  console + visual confirmation of effect" is the bar.

Reviewer enforcement: if smoke digest claims "compared=N" but session has no
downstream evidence of N actual fires, halt verify and ask for re-run. Do NOT
proceed with strict-gate analysis on a session where 0 events landed.

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

**Trust hierarchy — downstream evidence over upstream stack (added 2026-05-02 from V3 Path A first-attempt finding):**
Stack frame absence in `Editor.log` does NOT prove code-path absence. Code paths
without explicit `Debug.Log/Error` calls (e.g., silent dispatch in
`BuddahPredictionRouter.RouteImpulse`) will never produce stack frames even
when actively executing — Unity only emits stack traces alongside log calls.

Reviewer's verification MUST prefer **downstream observable side effects** over
upstream stack-frame counts:
- Counter increments (e.g., `leg-imp-compared++`)
- HEARTBEAT field deltas across the session
- DebugState field writes
- rb position / velocity / WakeUp evidence
- Channel `[CommandBus]:Recv` events
- Visual smoke (cube actually moves)

**Anti-pattern**: "Router stack=0 in session window → router not called → test
invalid". This was the V3 Path A first-attempt false negative. The router has
zero log calls by design (silent dispatch decided in V3 Q1 sub-decision); stack
frame absence is the EXPECTED state of a working V3, not a failure mode.

**Correct pattern**: grep `leg-imp-compared` across HBs; if monotonic increase
matches expected event count, code path was active regardless of stack frame
visibility. Cross-confirm with at least one other downstream metric.

**Verification target — git plumbing over working tree (added 2026-05-03 from V4 closeout post-mortem, lesson L22):**
For ANY deletion-heavy phase (or any phase with negative claims of the form
"X was removed", "X has 0 references", "X is absent"), reviewer's verification
MUST use git plumbing as the source-of-truth, NOT Read/Grep on working tree.

Canonical commands (use these for negative-claim sign-off):
- `git show HEAD:<path>` — exact committed file content at branch tip
- `git status --short` — working tree drift from HEAD (any `M`/`??` in scope = phase NOT complete)
- `git diff origin/<base> -- <path>` — what this branch will land vs base when PR merges
- `git log --oneline -- <path>` — confirms file's last-touched commit
- `gh pr diff <pr#>` — exact change set the merge will introduce

**FORBIDDEN as primary negative-claim verification:** Read tool on working tree,
Grep tool with default path (which scans working tree). Working tree may
include unstaged edits that misrepresent the merged codebase. Read/Grep are
fine for navigation; they are NOT fine for "X was successfully removed"
sign-off.

**Stage 6 VERIFY pre-grep gate:** reviewer's first command in any deletion-
heavy verify pass MUST be `git status --short` filtered to phase scope. Any
`M` or `??` line in scope means the phase implementation is incomplete —
halt VERIFY and escalate to IMPLEMENT for completion before any smoke /
grep / sign-off. The L22 incident (V4 PR #37 merged with 6 files of
unstaged deletions) is the canonical failure mode.

**Cross-ref:** L22 in `Docs/lessons-log.md`.

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

Any rb force/torque write **on objects participating in reconcile replay**
(have `PredictionRigidbody` wrapper) MUST go through
`_predictionRigidbody.AddForce` / `_predictionRigidbody.AddTorque`, NEVER
`rb.AddForce` / `rb.AddTorque` directly. Direct rb writes silently break
reconcile replay determinism — failure manifests as visible jitter, not as FATAL.

**Scope clarification (added 2026-05-02 from V3 recon feedback):** Rule 7 only
applies to objects in the prediction stack. Debug-only objects without
`PredictionRigidbody` (e.g., `BuddahPredictionPushTargetBox`) are outside this
rule's scope; they may use direct Unity Rigidbody API. Future agents should not
false-positive flag `targetRigidbody.AddForce` calls in non-prediction debug code.

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

## Rule 11 — SMOKE driver hard-precondition for time-sensitive probes (added 2026-05-03 from V5 closeout, A2 third occurrence)

When a strict gate's pass criterion is **time-sensitive** — i.e., depends on
the user-driver doing a specific action within a narrow window after PlayMode
entry — the SMOKE driver MUST verify the precondition is achievable BEFORE
entering PlayMode, and ABORT the run if it is not.

**Canonical example — V5 spawn-window 60-tick L7 latch probe:**
- Gate: CLIENT first Recv `eventTick - first HB tick < 60` (~1 second)
- Driver precondition: peer-2 must push CLIENT buddah within ~3-5s of CLIENT
  spawn, or the spawn-window race condition is not actually stress-tested
- Failure mode: partial-pass with note (latch correctness can still be proven
  via Recv liveness + monotonic logicalId), BUT the gate becomes vacuous

**Why a rule and not just a checklist item:**
The V5 closeout review surfaced this gate has now partial-passed **3 times in
a row** (V4 Path B + V5 Path A baseline + V5 Path B). Each time the latch
correctness was independently provable from other evidence, so the run was
accepted. But the spawn-window stress-test was never actually applied. A
gate that never fails is not protecting against the failure mode it claims to.

**Driver action protocol (mandatory for time-sensitive probes):**
1. Before entering PlayMode, driver MUST be ready to execute the precondition
   action (e.g., finger on push key) — NOT in the middle of lobby setup
2. If lobby setup or LatencySim toggle interaction would consume the window,
   PlayMode entry MUST be deferred until setup is complete and driver is
   action-ready
3. If the driver realizes mid-run the window was missed, ABORT the run, do
   NOT dump the log, retry from clean state
4. Reviewer verifies precondition was met by checking first-action timestamp
   vs spawn timestamp at SMOKE digest stage; if not met, escalates to
   driver-rerun, NOT acceptance via "other evidence"

**Scope:** applies to spawn-window probes, race-condition probes, latency-
sensitive RPC ordering tests, any gate citing a specific tick or millisecond
window. Does NOT apply to long-window cumulative gates (e.g., counter
HEARTBEAT cadence, cumulative FATAL=0 across full session).

**Tradeoff acknowledged:** stricter driver discipline means more aborts and
reruns. Acceptable cost. The alternative — accepting partial-pass via
"other evidence" three runs in a row — is the failure mode this rule fixes.

## Rule 10 — Working tree commit hygiene (added 2026-05-02 from incident)

Phase Gate System docs and contracts in this folder MUST be committed to git
within the same PR cycle as the phase they govern. Untracked-only state
across multiple sessions risks accidental loss via `git clean`, branch switch,
or workspace corruption.

After contract edit / phase doc creation, commit immediately. Do NOT defer
commits to a "polish PR later" — that pattern caused total loss of Phase A
docs + V3 contract during V3 IMPLEMENT phase. Recovery cost: full re-write
from chat memory, plus integration churn into the active feature PR.
