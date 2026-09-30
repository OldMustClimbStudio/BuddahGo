---
name: unity-mcp
description: Use CoplayDev MCP for Unity to inspect or change this project's Unity Editor, assets, scenes, scripts and tests.
---

Use the `unityMCP` server configured in this repository. Setup is in
[Docs/unity-mcp.md](../../../Docs/unity-mcp.md).

- Check the available tools/resources and use their live schemas.
- Confirm the target editor/project through `mcpforunity://instances` before
  mutations, especially with multiple worktrees open; select it with `set_active_instance`.
- Inspect the relevant object or asset before editing. Wait for compilation/domain
  reload to finish before dependent operations, then inspect new console errors.
- Common tools are `manage_scene`, `find_gameobjects`, `manage_components`,
  `manage_asset`, `refresh_unity`, `read_console`, `run_tests` and `get_test_job`.
- Choose checks for the change; read-only inspection does not need a full test run.
