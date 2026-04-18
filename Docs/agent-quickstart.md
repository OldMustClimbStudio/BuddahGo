# Agent Quickstart - Low-Token Routing

Use this page as the default entry point for agent work in this repository.
This page is a routing shortcut, not a replacement for the full required process in [CLAUDE.md](../CLAUDE.md). After routing, still complete all 10 mandatory steps there in order.

## Default Flow
1. Classify the task as `debug`, `refactor`, or `feature`.
2. Run `Tools/Harness/Get-BuddahGoHarnessContext.ps1` first.
3. Use the helper output as the working summary for:
   - owning system
   - recommended skill
   - protected files
   - validation sequences
   - stop conditions
4. Read raw docs only when they are needed to:
   - resolve an ownership ambiguity
   - understand a high-risk boundary
   - inspect a validation or recovery step in more detail
   - maintain the harness itself

## Raw Doc Policy
- `Docs/harness-manifest.json` is the source of truth for routing and validation metadata, but it is not the default first read for normal tasks.
- `Docs/architecture.md` and `Docs/system-map.md` are the first detailed reads when ownership or scene flow is unclear.
- `Docs/workflow.md`, `Docs/validation.md`, `Docs/safety.md`, and `Docs/recovery.md` should be opened on demand instead of front-loading them into every task.

## Default Commands
```powershell
powershell -ExecutionPolicy Bypass -File Tools/Harness/Get-BuddahGoHarnessContext.ps1 -Intent debug -Systems prediction
```

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Harness/Get-BuddahGoHarnessContext.ps1 -Intent feature -Files Assets/Scripts/Network/Session/PropertySelection/PropertiesSelectionManager.cs
```

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Harness/Get-BuddahGoHarnessContext.ps1 -Intent refactor -Query "reduce agent token usage for docs harness routing"
```

## When To Expand Reads
- The helper selects multiple systems and the owner is still unclear.
- The task touches prediction, room flow, result flow, config contracts, scene assets, or protected files.
- A validation step fails and you need the detailed recovery path.
