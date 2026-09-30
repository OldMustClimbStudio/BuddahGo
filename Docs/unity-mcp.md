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

通过 `mcpforunity://instances` 和项目信息确认目标 worktree；多开 Unity 时用 `set_active_instance` 选择实例。编译或 domain reload 期间等待后再执行依赖操作。

## 配置说明

旧 AI Game Developer 包、DLL 和生成技能已移除。Codex 中同名禁用项只用于阻止继承全局旧服务；其他项目的全局配置不受影响。
新配置不需要旧 token。已有 checkout 若保留了旧的本地 `.mcp.json`，替换其中的 Unity 配置并保留其他服务。
服务端健康不等于 Unity 已连接；用项目路径和一次实际编辑器读取确认连接。
