# BuddahGo Feature Implementation Playbook

## Intent
Use this playbook when adding new gameplay behavior, a new flow, or a new UI capability.

## Required Feature Flow
1. Route the task through the helper first, using `-Files` or `-Query` when the owning system is not obvious.
2. State the single manifest system that will own the feature.
3. State the insertion point and the state owner before editing.
4. State what the feature explicitly does not touch.
5. Write new code in the owner system first.
6. Hook existing code last with the smallest possible integration diff.
7. Run the full validation sequence for the system.

## BuddahGo Feature Rules
- Prediction work needs explicit approval before touching protected prediction files.
- Skill features must plan the base and anti path together when backfire behavior exists.
- Selection or loadout features must respect config repositories and legality checks.
- Result or scene-flow features must keep ownership with the documented orchestration systems.

## Hard Stops
- The feature needs two manifest systems in one pass.
- The insertion point is a protected file and no explicit approval was given.
- The feature requires bypassing `SkillExecutor`, `RoomStateManager`, or config repositories.
