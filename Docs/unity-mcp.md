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

## 排障

- 先启动 HTTP 服务端再打开编辑器。编辑器已开但 `GET http://127.0.0.1:8080/api/instances` 为空时，让编辑器窗口获得一次焦点（自动连接需要编辑器处于前台更新），仍不行再在 `Window > MCP for Unity` 点 Connect。
- 服务端列出了实例而客户端工具报告“没有实例”时，是客户端会话在启动时连接失败；重连客户端的 MCP 服务，或用一个直接 `POST /mcp` 的流式 HTTP 客户端（initialize → set_active_instance → 工具调用）。
- 多个编辑器可以同时连到一个服务端，每个注册为 `<目录名>@<sha1(dataPath) 前 16 位>`（如根目录 `BuddahGo@1f2674f7fe7e0a56`）；每次调用前 `set_active_instance`，用 `mcpforunity://project/info` 核对路径。
- 不要点 `Window > MCP for Unity` 里的 Configure：它会执行 `claude mcp remove --scope project unityMCP`，改掉已提交的 `.mcp.json`。
- `read_console` 只接受 error / warning / log / all，一次最多 40 条。URP 渲染状态异常（如切换管线资产后反射全黑）时，`EditorUtility.RequestScriptReload()` 后等编译结束再继续。
- 编辑器连不上时：`dotnet build Assembly-CSharp-Editor.csproj`（或 `BuddahGo.Tests.csproj`）几秒内可做编译检查（新文件要等 Unity 重新生成 csproj）；批处理测试只跑部分用 `-testFilter` 写完整的测试类名、以 `;` 分隔，正则排除写法不生效。
- 某个 checkout 编译结果和别的 worktree 不同，先查 `git status --ignored Assets/FishNet/Demos`：被忽略的残留脚本不会出现在普通 `git status` 里。
- `execute_code` 用 CodeDom 编译（C# 6）：不支持新语法，`Object` 要写全 `UnityEngine.Object`，字符串里不要放中文资源路径（按 GUID 解析）。复杂逻辑写成编辑器脚本里的静态方法再调用。
- `execute_code` 的安全检查会拦下 `EditorApplication.Exit`、`AssetDatabase.DeleteAsset` 等调用；确认是有意的操作才传 `safety_checks: false`。
- 耗时的调用（如在编辑器里 `BuildPipeline.BuildPlayer`）可能先返回空结果而编辑器仍在执行；等它的输出文件（构建回执、日志）出现再继续。
- 不要在编辑器打开某个场景时用文本方式改这个场景文件：下次刷新会弹出"场景已在外部修改"的模态对话框，所有 MCP 调用都会卡住，直到有人点 Ignore 或 Reload。
- `EditorApplication.delayCall` 里的退出要等编辑器获得焦点才执行；编辑器开着同一个项目时，批处理（测试、构建、导表）会因项目锁直接退出。
- `run_tests` 期间不要触发编译或资源刷新：domain reload 会让测试任务变成 orphaned，结果不可用。锁屏时编辑器失去焦点，`run_tests` 会停住，改用批处理运行（见根目录 README 的测试一节）。
