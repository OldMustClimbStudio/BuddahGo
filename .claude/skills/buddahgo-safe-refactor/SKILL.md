---
name: buddahgo-safe-refactor
description: BuddahGo-specific safe refactor workflow for existing Unity gameplay, networking, prediction, config, and UI code. Use when restructuring code in this repository without changing behavior and you need strict boundary control.
---

# BuddahGo Safe Refactor

1. Read `../../../Docs/agent-quickstart.md` first.
2. Read `../../../skills/safe-refactor.md` for the full refactor playbook.
3. Route the task with:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Harness/Get-BuddahGoHarnessContext.ps1 -Intent refactor -Systems skill-execution
```

4. Replace `skill-execution` with the real system being refactored, or call the helper with `-Files` / `-Query` if the owner system is unclear.
5. Use the helper output as the default summary and keep the change inside one manifest system.
6. Stop if the refactor would rename serialized fields, scene names, or public RPC entry points.
7. Run the returned validation sequence before reporting success.
