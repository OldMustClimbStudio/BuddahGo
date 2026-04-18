# BuddahGo Agent Harness Entry Point

## Project Summary
- Engine: Unity 2022.3.55f1c1 LTS
- Network stack: FishNet 4.6.20 plus FishyFacepunch transport plus Steam lobby flow
- Prediction stack: `Assets/Scripts/New_Buddah/` with 85 files
- Skill stack: `Assets/Scripts/Buddah/ComboSkill/` with 70 files
- Network gameplay stack: `Assets/Scripts/Network/` with 79 files
- Config stack: `Assets/Scripts/Config/` with runtime repositories and validation
- Race orchestration: `Assets/Scripts/RaceIntro/`, `Assets/Scripts/Network/Room/`, `Assets/Scripts/Network/Session/Results/`
- MCP: `com.ivanmurzak.unity.mcp` v0.63.4 on `http://localhost:23290`

## Fast Path
Use the routing path by default:

| File | Purpose |
|---|---|
| [Docs/agent-quickstart.md](Docs/agent-quickstart.md) | Default low-token entry point and read policy |
| [Tools/Harness/Get-BuddahGoHarnessContext.ps1](Tools/Harness/Get-BuddahGoHarnessContext.ps1) | Emits the task-specific summary: owner, docs, protected files, validations, stops |

## Reference Docs
Read these on demand after routing, not all at once:

| File | Purpose |
|---|---|
| [Docs/harness-manifest.json](Docs/harness-manifest.json) | Machine-readable routing, owners, protected files, validation sequences |
| [Docs/architecture.md](Docs/architecture.md) | Layer model, system ownership, boundary rules |
| [Docs/system-map.md](Docs/system-map.md) | Runtime flow map and scene transition ownership |
| [Docs/workflow.md](Docs/workflow.md) | Required task routing behavior |
| [Docs/safety.md](Docs/safety.md) | Forbidden actions and stop conditions |
| [Docs/validation.md](Docs/validation.md) | Validation order and automation hooks |
| [Docs/recovery.md](Docs/recovery.md) | Retry and rollback strategy |
| [Docs/scoring.md](Docs/scoring.md) | Completion rubric |
| [Docs/lessons-log.md](Docs/lessons-log.md) | Append-only log of past agent failures and the rule each one created. Read before any edit pass. |
| [skills/debug-bug.md](skills/debug-bug.md) | Detailed debug playbook used by project skills |
| [skills/debug-log-isolation.md](skills/debug-log-isolation.md) | Rule for silencing non-target Debug.Log tags before a targeted investigation |
| [skills/safe-refactor.md](skills/safe-refactor.md) | Detailed refactor playbook used by project skills |
| [skills/feature-implementation.md](skills/feature-implementation.md) | Detailed feature playbook used by project skills |

## Registered Project Skills
- `buddahgo-debug-bug`
- `buddahgo-safe-refactor`
- `buddahgo-feature-implementation`

These skills are installed in both `.agents/skills/` and `.claude/skills/`.

## Global Rules
- Never edit before reading the target file and the files that own the same state.
- Always route the task through `Tools/Harness/Get-BuddahGoHarnessContext.ps1` first. Use `Docs/harness-manifest.json` directly when maintaining the harness or verifying helper output.
- Treat prediction, room flow, result flow, and config data as separate systems even when they interact at runtime.
- FishNet server authority is the source of truth for gameplay and scene state.
- `SkillExecutor` remains the only supported skill execution entry point.
- Config assets and repositories define legal selections and skill metadata. Do not hardcode around them as a shortcut.
- Every execution rule file under `skills/` and `Docs/` must stay under 100 lines. If a rule grows beyond that, split it into a sibling file and cross-link.

## Mandatory Execution Order
All 10 steps are mandatory and non-skippable. Do not claim completion, success, or readiness for handoff unless steps 1 through 10 have been completed in order.

1. Understand the task in at most 2 sentences.
2. Classify the intent: `debug`, `refactor`, or `feature`.
3. Resolve the affected system with `Tools/Harness/Get-BuddahGoHarnessContext.ps1`.
4. Use the helper output as the default working summary and open raw docs only when more detail is required. `Docs/lessons-log.md` is mandatory reading once per session before any edit - check whether any rule there applies to the current task.
5. Name the exact files that will change and keep the pass inside one manifest system.
6. Diagnose the root cause or insertion point before changing code.
7. Edit the minimum viable scope.
8. Run the validation sequences listed for the affected system.
9. If validation fails, follow [Docs/recovery.md](Docs/recovery.md).
10. Report completion only after the checklist passes.

## Hard Stops
- The task spans multiple manifest systems and cannot be split into separate passes.
- The task touches a protected file without explicit approval.
- The task mentions reconcile, rollback, desync, tick drift, or scene sync uncertainty.
- The task would rename serialized fields, public RPC entry points, or scene names used by orchestration systems.

## Helper Script
Use this helper before substantial work:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Harness/Get-BuddahGoHarnessContext.ps1 -Intent debug -Systems prediction
```

It prints the required docs, protected files, validation sequences, and stop conditions for the selected system.

If the system is not obvious yet, let the helper auto-detect from files or keywords:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Harness/Get-BuddahGoHarnessContext.ps1 -Intent feature -Files Assets/Scripts/Network/Session/PropertySelection/PropertiesSelectionManager.cs
```

or

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Harness/Get-BuddahGoHarnessContext.ps1 -Intent debug -Query "prediction desync when acceleration skill fires"
```
