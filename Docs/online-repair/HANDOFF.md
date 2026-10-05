# 交接：联机预测撕裂与开场交接修复（给实现者）

你负责在分支 `fix/online-prediction-handoff`（从 `dev` 7389ec8 拉出）上，先按实验顺序确认根因，再按规格实现修复。本分支到交接时只有文档，没有代码或资源改动。诊断是静态分析，**没有在 Unity 中运行过**；所有假设都要先被你的实测确认，再决定修什么。

## 当前进度（2026-10-05 收尾）

EXP-0、EXP-1 / §1 与开场交接残余修复已完成并经用户双端实测「完全可玩」，本分支收尾，EXP-2 到 EXP-5 未执行。结论、已知残余和问题复现时的接续方式见 [progress.md](progress.md) 顶部「收尾」一节。

## 文档地图

| 你要做什么 | 读 |
|---|---|
| 了解现象、根因假设与每条证据的精确位置 | [findings.md](findings.md) |
| 按可能性顺序做最小干预实验、决定做不做下一项 | [experiments.md](experiments.md) |
| 实现被证实的修复，含字段、函数、边界、测试 | [fix-spec.md](fix-spec.md) |
| 验收阈值与回归矩阵 | [acceptance.md](acceptance.md) |
| 记录进度、基线、决策、新发现（唯一来源） | [progress.md](progress.md) |

仓库规则以 [harness.md](../../harness.md)、[CONTRIBUTING.md](../../CONTRIBUTING.md) 为准。预测系统的既有契约见 [prediction-design.md](../prediction-design.md)，FishNet 要点见 [networking.md](../networking.md)。

## 环境

- Unity 2022.3.55f1c1，FishNet 4.7.1（`Assets/FishNet/package.json`）。编译与 Console 可用 Unity MCP（[unity-mcp.md](../unity-mcp.md)）。
- 两端测试：host 用 Development Build、client 用 Editor 或 ParrelSync 克隆。两端 Assets 必须一致。
- 延迟模拟：`Assets/Scenes/MainMenu.unity` 的 NetworkManager → TransportManager → `_latencySimulator`（`_enabled`、`_latency` 毫秒、`_simulateHost: 1`）。连接前设置；两端都设。
- 日志开关（Editor / Development 才编译进来，Release 无 Verbose）：
  - `Buddah.prefab` → BuddahPredictionBootstrap → Debug Settings：`enableVerboseLogs`、`dumpReconcile`、`dumpReplicate`。
  - 全局：`NetDebug.EnableVerboseLog = true`。
  - Standalone 保留宏 `BUDDAH_PREDICTION_SHADOW` 可看 `[D-LOC HEARTBEAT]`。
- 严格测量可复用归档的运行夹具 [Tools/Validation/ReviewRuntimeRound2](../../Tools/Validation/ReviewRuntimeRound2/README.md)：它在 postTick/postReplay 记录每个 motor 的刚体位置，`summarize-runtime-stream.py` 输出 GO 后 1 秒的 `transformCorrection` 统计。它依赖一个观察探针补丁，仅用于一次性 worktree，不进入正式提交。

## 工作顺序

1. 建 worktree：`git worktree add .worktree/online-prediction-handoff fix/online-prediction-handoff`，`git lfs pull`，`git merge origin/dev`。
2. 做 experiments.md 的 **EXP-0**：加观察探针（见 fix-spec.md §0）、采基线、确认两个前提（Physics Mode 当前值；现象出现在 host 还是纯 client）。填 progress.md 的基线表。
3. 按 EXP-1 → EXP-5 顺序做实验。每项先预检，不满足就跳过并记录。某个症状达标就停止为它安排的后续实验。
4. 对被证实的假设，按 fix-spec.md 对应章节实现正式修复，跑 acceptance.md 的验收矩阵，更新 progress.md。
5. 每个阶段单独提交：实验探针 `chore: [EXP-n] …`，正式修复 `fix: …`，文档 `docs: …`。探针在正式修复前 revert。
6. 全部完成后向 `dev` 开 PR，用 CONTRIBUTING 的 Summary / Validation 模板，列出每项实验与验收的实测数字；未执行的写"未执行 + 原因"。

## 必须停下来找人

- acceptance.md 的决策项 D1–D4 尚未决定时，不要替团队决定。
- EXP-0 的两个前提与 findings.md 不符（例如 Physics Mode 已经是 TimeManager，或 host 自己的身体也有撕裂）：这意味着根因排序要重排，先回报再继续。
- 需要修改 RPC 签名或 `BuddahPredictedReconcileData` / `BuddahPredictedInputData` 以外的网络数据结构。
- EXP-1 之后 D-LOC 任一轴 div≠0 且找不到原因。
- 发现与本任务无关的缺陷：记进 progress.md 的"新发现"，不在本分支顺手修。

## 范围之外

单机模式（Solo）、技能命中与伤害逻辑、房间与大厅流程、性能优化。
