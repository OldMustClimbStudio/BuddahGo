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

## Hard Stops
- More than one manifest system is required.
- Behavior would change even slightly.
- The refactor would rename serialized fields or public hooks used by scenes or prefabs.
