# Validation - Completion Checklist

Run this checklist in order after every edit.

## 1. Resolve the validation plan
- Run `Tools/Harness/Get-BuddahGoHarnessContext.ps1` first to get the merged validation sequence for the task.
- Read the affected system entry in `Docs/harness-manifest.json` only when you need to inspect the source metadata directly.
- Confirm the changed files still belong to the intended manifest system.
- Include any conditional validations returned by the helper, not just the base system sequence.

## 2. Compile and console
- Clear logs if needed to isolate new errors.
- Confirm Unity reports no new compile errors.
- Confirm edited files did not introduce new warnings that change runtime behavior.

## 3. Scope and boundary integrity
- Confirm the actual changed files match the declared scope.
- Confirm no protected file was changed accidentally.
- Confirm no new cross-system dependency was introduced.
- Confirm no serialized field or scene name was renamed without explicit intent.

## 4. System-specific validation
- `prediction-integrity`: confirm reconcile structures, force routing, and bridge expectations remain valid.
- `network-authority`: confirm writes still happen from the owning server path and RPC ownership rules remain correct.
- `skill-pairing`: confirm base and anti variants still resolve correctly and still route through `SkillExecutor`.
- `config-runtime`: confirm repository reads and config validation still agree.
- `scene-orchestration`: confirm scene ownership stayed with `RoomStateManager`, `PropertiesSelectionManager`, or other documented owners.

## 5. Behavior check
- State the expected post-change behavior in 1 or 2 sentences.
- Confirm that behavior through logs, MCP inspection, or a focused test.
- Spot-check one adjacent runtime flow from `Docs/system-map.md`.

## 6. Scene and prefab check
- If serialized fields, scene hooks, or component references changed, inspect the affected scene or prefab.
- Confirm references still resolve and no missing scripts appeared.
- Save only after verification.

## 7. Final gate
- No unresolved TODO or placeholder left behind.
- Validation sequences for the system all passed.
- Only then report completion.
