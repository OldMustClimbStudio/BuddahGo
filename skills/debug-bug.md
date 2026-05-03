# BuddahGo Debug Bug Playbook

## Intent
Use this playbook when a runtime bug, sync bug, or compile failure needs a scoped fix.

## First Step
Run the harness router before editing and use it as the default task summary:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Harness/Get-BuddahGoHarnessContext.ps1 -Intent debug -Systems prediction
```

Replace `prediction` with the actual affected system.
If the system is unclear, pass `-Files` or `-Query` and let the helper auto-detect first.

## Required Debug Flow
1. State the symptom in 1 sentence.
2. Name the state owner from the helper output.
3. Read the target files and only the raw docs needed to remove ambiguity or validate a risky boundary.
4. Choose the evidence path that best fits the symptom.
5. If the path is already clear, trace input, state write, state read, and presentation of the bug.
6. If runtime ordering or data is still ambiguous, use the log-first workflow:
   - add high-signal `Debug.Log` or gated prints at the suspected input, state write, state read, and presentation boundaries
   - if the console is noisy, apply [debug-log-isolation.md](debug-log-isolation.md) before the next run
   - run the repro, wait for the play-test to finish, then collect only the fresh logs
   - analyze the captured log output line by line and use it to identify the concrete failure point
7. State the root cause before editing.
8. Fix the root cause location with the smallest possible diff.
9. Run every validation sequence listed for the system.

## BuddahGo-Specific Debug Heuristics
- If the symptom includes movement, force, rollback, or desync, route to `prediction`.
- If the symptom includes lobby, ready state, or scene flow, route to `room-session` or `selection-loadout`.
- If the symptom includes cast timing, anti behavior, or loadout resolution, route to `skill-execution`.
- If the symptom appears after finish or in the result scene, route to `race-results`.
- If legal options, IDs, or missing data are involved, route to `config-rules`.

## Log Noise Isolation
When a targeted diagnostic component is deployed and its output is being
captured via `console-get-logs`, first silence every debug tag that is not
part of the current investigation. See [debug-log-isolation.md](debug-log-isolation.md).

## Verification Discipline (post Phase 4b lessons)
When verifying claims about code state — especially negative claims like "X was removed", "Y has 0 references", or "Z is no longer called" — apply these rules:

- **L21** — Risk:HIGH = full claim-by-claim grep, not sampled spot-check. For changes that touch authority, data flow, or wire format, grep every claim individually.
- **L22** — Negative claims = `git show HEAD:<path>` + `git status --short` + `git diff origin/<base>`, NOT Read/Grep on working tree. Working tree may include unstaged edits or mount artifacts that misrepresent the merged state. Stage 6 pre-grep gate: `git status --short` filtered to scope must be clean before sign-off.
- **Rule 1-D** — Downstream evidence (counter increments, HEARTBEAT field deltas, rb writes) > upstream stack frames. Code paths without `Debug.Log` calls produce no stack frames even when actively executing.

See `Docs/lessons-log.md` L20-L22 + `Docs/phase-gates/methodology.md` Rule 1-D + Rule 2 sub-clauses for full context.

## Hard Stops
- The fix needs two manifest systems in one pass.
- The root cause points at a protected file and the task did not explicitly authorize it.
- You are tempted to add fallback logic instead of repairing the owning contract.
