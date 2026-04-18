# Debug Log Isolation Rule

## Intent
When running a targeted runtime investigation, the console log window is a
finite buffer. Unrelated heartbeat logs drown the target signal. Before the
next play-test, silence every log category that is not the current debug
target. Restore them when the investigation closes.

## When to Apply
- A targeted diagnostic component (for example `VisualJitterDiagnostic`) is
  deployed and its output must be captured cleanly.
- The MCP `console-get-logs` window returns fewer target lines than expected
  because another tag dominates the buffer.
- The user asks to isolate a symptom or "cut down log noise".

## Rule
1. Identify the target debug tag. That is the only one that stays live.
2. Scan recent logs and list tags by volume. Silence the top spammers first.
3. Prefer routing through an existing gate (`NetDebug.EnableVerboseLog`,
   a component `_enableDebugLogs` bool, or an equivalent). Only add a new
   gate if none exists on the call path.
4. Never delete the `Debug.Log` call. Wrap it so it stays recoverable.
5. Leave low-volume signal logs (for example `[PredictionOverlay]`,
   `[BuddahRespawn]`) alone when they aid the current investigation.
6. Record the silenced tags in the debug note so they can be restored.

## Minimum Viable Pattern
```csharp
// Before
Debug.Log($"[Leaderboard] SyncList updated ...");

// After
if (NetDebug.EnableVerboseLog)
{
    Debug.Log($"[Leaderboard] SyncList updated ...");
}
```

## File Size Constraint
Every execution rule file in this repository must stay under 100 lines.
If a rule grows beyond that, split it into a sibling file and cross-link
rather than inlining more detail.

## Restore Step
When the investigation closes, either:
- flip `NetDebug.EnableVerboseLog = true` at runtime to re-enable gated
  output without reverting files, or
- revert the gate wrappers with `git diff` and a single patch.

## Related
- [debug-bug.md](debug-bug.md) — master debug playbook.
- [../Docs/validation.md](../Docs/validation.md) — validation sequences.
