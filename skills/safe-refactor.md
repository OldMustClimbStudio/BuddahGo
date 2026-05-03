# BuddahGo Safe Refactor Playbook

## Intent
Use this playbook when code already works and the goal is restructuring without behavior change.

## Required Refactor Flow
1. Route the task through the helper first, using `-Files` or `-Query` when the owning system is not obvious.
2. Keep the refactor inside one manifest system.
3. Inventory all references to the symbol or class being changed.
4. Stop if serialized fields, scene names, RPC entry points, or public API contracts would change.
5. Prefer moving or extracting code inside the same owner system.
6. Validate compile, scope, and adjacent behavior after the refactor.

## BuddahGo Refactor Priorities
- Keep scene ownership inside the current orchestrators.
- Keep prediction and skill integration boundaries intact.
- Keep config repositories as the runtime contract.
- Do not refactor around anti skill pairing unless the task explicitly targets that contract.

## Verification Discipline (post Phase 4b lessons)
After a refactor that renames or removes a symbol, verify completion via git plumbing:

- **L22** — "Old name has 0 references" MUST be verified via `git grep <old-name> HEAD -- <scope>`, NOT Read/Grep on working tree (working tree may have unstaged edits showing "0" while HEAD still has references). Stage 6 pre-grep gate: `git status --short` filtered to refactor scope must be clean before sign-off.
- **L21** — Risk:HIGH refactors (public API, serialized fields, RPC entry points) require full claim-by-claim grep, not sampled spot-checks. Negative claims demand independent positive verification.

See `Docs/lessons-log.md` L21+L22 + `Docs/phase-gates/methodology.md` Rule 2.

## Hard Stops
- More than one manifest system is required.
- Behavior would change even slightly.
- The refactor would rename serialized fields or public hooks used by scenes or prefabs.
