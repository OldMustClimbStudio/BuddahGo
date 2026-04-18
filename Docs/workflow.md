# Workflow - Standard Agent Behavior

## Pre-Edit Protocol
- Classify the task as `debug`, `refactor`, or `feature`.
- Resolve the affected manifest system with the helper before editing.
- Use the helper output as the default summary for routing and validation.
- Read raw docs only when the helper summary is not enough for the task.
- Read the target file and the files that own the same state.
- Keep the pass inside one manifest system whenever possible.

## Routing Command
Use this command before substantial work:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Harness/Get-BuddahGoHarnessContext.ps1 -Intent debug -Systems prediction
```

It prints:
- required docs
- recommended project skill
- protected files
- high-risk files
- validation sequences
- conditional validation
- stop conditions

If you do not know the system yet, provide `-Files` or `-Query` and let the helper auto-detect the likely systems first.

## Diagnosis Rules
- Reproduce the issue in reasoning before touching code.
- Name the state owner before proposing a fix.
- If the state owner is unclear, read `Docs/architecture.md` and `Docs/system-map.md` again.
- If the task touches prediction, room flow, results flow, config contracts, or protected files, expand reads before edits.

## Edit Rules
- Change the minimum lines needed.
- Do not mix behavior changes, refactors, and bug fixes in one pass.
- Do not rename serialized fields, scene names, or public RPC entry points unless explicitly asked.
- Do not add fallback logic that bypasses repositories or orchestrators.
- Keep presentation-only fixes out of authoritative gameplay systems.

## Scope Enforcement
- Prefer at most 3 changed files for debug or feature work inside one system.
- If a task requires two manifest systems, split it into two passes or stop for confirmation.
- Treat `config-rules` as its own system even when the user asked for a gameplay change.
- Treat `race-and-results` as separate from general UI work because scene flow and sync state live there.

## Required Validation Behavior
- Run the validation sequences returned by the helper for the affected system.
- Run any conditional validations returned by the helper, especially `scene-prefab` when serialized hooks or scene assets are involved.
- If compile or log validation fails, switch to [Docs/recovery.md](Docs/recovery.md) before changing more code.
- Do not report success until the validation plan is complete.
