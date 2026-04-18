---
name: buddahgo-feature-implementation
description: BuddahGo-specific feature implementation workflow for Unity gameplay, networked flows, selection systems, and UI additions. Use when adding a new feature in this repository and you need the feature scoped to the correct owner system with validation and stop conditions.
---

# BuddahGo Feature Implementation

1. Read `../../../Docs/agent-quickstart.md` first.
2. Read `../../../skills/feature-implementation.md` for the full feature playbook.
3. Route the task with:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Harness/Get-BuddahGoHarnessContext.ps1 -Intent feature -Systems selection-loadout
```

4. Replace `selection-loadout` with the actual owner system, or call the helper with `-Files` / `-Query` if the owner is unclear.
5. Use the helper output as the default summary, then state the insertion point and what the feature does not touch before editing.
6. Stop if the feature needs two manifest systems in one pass.
7. Run the returned validation sequence before reporting success.
