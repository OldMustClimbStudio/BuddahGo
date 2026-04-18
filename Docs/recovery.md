# Recovery - Retry and Rollback Strategy

## When to Use This File
Enter recovery when any validation check fails after an edit.

## Attempt 1 - Minimal Fix
1. Read the exact error or failed validation output fully.
2. Identify the single line, symbol, or assumption that caused the failure.
3. Revert only that local mistake.
4. Re-run the failed validation sequence immediately.
5. If the failure clears, continue the remaining validation steps.
6. If the failure remains, move to Attempt 2.

## Attempt 2 - Alternative Path
1. Revert the unsuccessful part of Attempt 1.
2. Re-read the original file and the owning system docs from `Docs/harness-manifest.json`.
3. Re-check the state owner and boundary rules.
4. Choose an alternative approach that stays inside the same system.
5. Apply the smaller and safer alternative.
6. Run the full validation plan again, including conditional validations returned by the helper.
7. If it still fails, move to Fallback.

## Fallback - Full Revert
1. Revert all edits from the task back to the pre-task state.
2. Confirm compile and baseline validation pass after the revert.
3. Report clearly:
   what was attempted
   what failed
   what was reverted
   what information is needed to proceed safely
4. Do not attempt a third edit pass without user guidance.

## Hard Stop Conditions
Stop immediately and report if:
- the failure mentions `reconcile`, `rollback`, `desync`, or `tick`
- the failure is in `GameNetworkManager`, `ConnectionManager`, or another protected owner file
- a previously loadable scene no longer loads
- two attempts failed on the same issue
- the fix now requires scope expansion beyond the declared manifest system

## Recovery Anti-Patterns
- hiding the error with `try/catch`
- adding `#if UNITY_EDITOR` to mask runtime issues
- commenting out failing logic instead of fixing the contract
- expanding scope into another system just to silence the first failure
- guessing at FishNet lifecycle or prediction behavior without reading the owning docs first
