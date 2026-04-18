---
name: buddahgo-debug-bug
description: BuddahGo-specific bug investigation workflow for Unity, FishNet, prediction, skill, selection, room, and result issues. Use when debugging a compile failure, sync bug, gameplay bug, prediction desync, selection bug, or result-flow issue inside this repository.
---

# BuddahGo Debug Bug

1. Read `../../../Docs/agent-quickstart.md` first.
2. Read `../../../skills/debug-bug.md` for the full debug playbook.
3. Route the task with:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Harness/Get-BuddahGoHarnessContext.ps1 -Intent debug -Systems prediction
```

4. Replace `prediction` with the actual affected system.
   If the system is unclear, call the helper with `-Files` or `-Query` first.
5. Use the helper output as the default summary and open raw docs only when more detail is needed.
6. Stay inside one manifest system unless the user explicitly approves a wider pass.
7. Run every validation sequence returned by the helper before reporting completion.
