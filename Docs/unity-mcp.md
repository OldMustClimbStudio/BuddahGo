# Unity MCP

使用 [CoplayDev MCP for Unity](https://github.com/CoplayDev/unity-mcp) v10.0.0，Unity 包与 Python 服务端均固定版本。

## 启动

1. 用 Unity 2022.3.55f1c1 打开目标 checkout，等待包解析和编译。
2. 安装 Python 3.10+ 和 `uv` 后，在仓库运行：

   ```powershell
   ./Tools/UnityMcp/Start-UnityMcp.ps1
   ```

3. 在 `Window > MCP for Unity` 选择 HTTP Local，地址 `http://127.0.0.1:8080`，连接 Unity session。
4. 重连客户端。Codex 使用 `.codex/config.toml`，Claude Code 使用 `.mcp.json`，服务名均为 `unityMCP`，端点为 `http://127.0.0.1:8080/mcp`。

编译或 domain reload 期间等待后再执行依赖操作。

## 多个编辑器

每个 worktree 的编辑器连上后登记为 `<项目文件夹名>@<哈希>`；`mcpforunity://instances` 或 `GET http://127.0.0.1:8080/api/instances` 列出当前连上的实例。会话自带的 `unityMCP` 可能把调用路由到另一个编辑器（例如根 checkout），所以先用 `set_active_instance` 钉住目标实例，再用 `mcpforunity://project/info` 核对项目路径。

会话的 `unityMCP` 启动失败，或钉不住实例时，用一个小的 streamable-HTTP JSON-RPC 客户端直接驱动服务端：对 `POST http://127.0.0.1:8080/mcp` 依次发 `initialize`、`notifications/initialized`、`tools/call` 调 `set_active_instance`，再调目标工具；后续请求都带上服务端返回的 `mcp-session-id` 头。

## 测试与验证入口

- EditMode 测试的常规入口（Test Runner 或 MCP `run_tests`）见[程序集与测试](architecture.md#程序集与测试)。
- 编辑器失焦（例如锁屏）时，play mode 和 `execute_code` 照常运行，但 `run_tests` 会停住，`get_test_job` 返回 `blocked_reason: editor_unfocused`。这时关掉打开该 worktree 的编辑器（同一项目只能有一个编辑器），改用 headless：

  ```powershell
  & '<Unity>/Editor/Unity.exe' -batchmode -nographics -projectPath '<worktree>' `
    -runTests -testPlatform EditMode -assemblyNames BuddahGo.Tests `
    -testResults '<worktree>/Logs/<topic>/editmode.xml' -logFile '<worktree>/Logs/<topic>/editmode.log'
  ```

- 训练模式验证：编辑器打开 `Assets/Scenes/MainMenu.unity`，进入 Play 并停在主菜单，执行菜单 `Tools/Mechanics/Run Practice Verification`，或用 `execute_code` 调 `BuddahGo.AI.MechanicsPracticeHarness.Begin(dir)`。之后轮询 `MechanicsPracticeHarness.Status`、`IsRunning`、`Summary`，或用菜单 `Tools/Mechanics/Log Practice Verification Status` 打到 Console。菜单运行的报告写在 `Logs/mechanics-practice/<时间戳>/`（`Logs/` 不进版本库），完整步骤见[训练模式验证记录](gameplay/practice-verification.md#重跑方法)。

## 配置说明

旧 AI Game Developer 包、DLL 和生成技能已移除。Codex 中同名禁用项只用于阻止继承全局旧服务；其他项目的全局配置不受影响。
新配置不需要旧 token。已有 checkout 若保留了旧的本地 `.mcp.json`，替换其中的 Unity 配置并保留其他服务。
服务端健康不等于 Unity 已连接；用项目路径和一次实际编辑器读取确认连接。
