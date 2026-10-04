# BuddahGo

佛像竞速派对游戏：玩家驾驶佛像跑圈，用搓招技能互相干扰。基于 Unity、FishNet、FishyFacepunch 和 Steam。

## 玩法模式

| 模式 | 说明 |
|---|---|
| 联机 | Steam 大厅建房/加入，房间就绪、属性与技能选择、比赛、结算与投票 |
| 单机（Solo） | 主菜单直接开局，离线 host 上 1 名玩家对 5 个 AI；AI 驾驶分 Easy / Normal / Hard 三档，五种技能人格各自配装与施法；结算后可 Rematch 或回主菜单 |

两种模式共用同一套运动、技能、计时与结算代码；差异集中在 `IMatchRules`（`Assets/Scripts/Match/`）。

## 开始开发

使用 Unity `2022.3.55f1c1`、Git 和 Git LFS：

```bash
git lfs install
git clone -b dev https://github.com/OldMustClimbStudio/BuddahGo.git
cd BuddahGo
git lfs pull
```

用指定 Unity 版本打开项目，等待包解析和资源导入。保留 `Assets/`、`Packages/`、`ProjectSettings/`；`Library/` 等缓存由 Unity 生成。构建场景为 `MainMenu`、`PropertySelection`、`RaceMap`。

## 代码地图

| 目录 | 内容 |
|---|---|
| `Assets/Scripts/Network/` | 连接、大厅、房间状态、比赛进度与结算（联机主流程） |
| `Assets/Scripts/Match/` | 模式规则、会话启动、比赛时钟与计时、参赛者身份；`Match/UI/` 为单机界面 |
| `Assets/Scripts/AI/` | AI 驾驶（`ThrustVectorPlanner`、赛车线、难度配置）与技能人格；`AI/Diagnostics/` 为仅开发构建的取证 harness |
| `Assets/Scripts/New_Buddah/`、`Buddah/` | 预测运动、手部推掌、技能执行 |
| `Assets/Scripts/RaceIntro/` | 1–6 人开场路径与交接 |
| `Assets/Tests/EditMode/` | EditMode 测试（`BuddahGo.Tests`） |
| `Tools/` | AI 难度配置与验收脚本（`Tools/ai/`）、单机 benchmark、交接校验、性能与资源审计脚本 |

## 测试

Unity Test Runner 运行 EditMode 的 `BuddahGo.Tests`。不开编辑器时可用批处理（同一工程不能同时被编辑器打开）：

```bash
Unity.exe -batchmode -nographics -projectPath <项目路径> -runTests -testPlatform EditMode -assemblyNames BuddahGo.Tests -testResults <结果.xml> -logFile <日志.log>
```

其余验证要求（联机双端、单机 benchmark 等）见 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 常见问题

- **缺贴图、模型或 DLL**：先确认 LFS 文件已下载完整（`git lfs pull`）。
- **特效显示洋红色或看不见**：通常是 ShaderGraph / VFX Graph 的本地导入缓存失效。运行 `Tools > BuddahGo > Repair Original VFX Imports`，详见 [Docs/vfx-asset-recovery.md](Docs/vfx-asset-recovery.md)。
- **排查未使用资源**：`Tools > Asset Audit > Report Unused Asset Candidates`；运行时动态加载（如 `Resources/AI`）仍需结合代码确认。

## 开发入口

- [harness.md](harness.md)：Agent 共用执行入口。
- [CONTRIBUTING.md](CONTRIBUTING.md)：分支、提交、PR、验证及资源规则。
- [Docs/README.md](Docs/README.md)：架构、网络、预测与单机模式文档索引。
- [CONTEXT.md](CONTEXT.md)：领域术语；[Docs/adr/](Docs/adr/)：架构决策。
- [Docs/unity-mcp.md](Docs/unity-mcp.md)：Unity MCP 启动方法。
