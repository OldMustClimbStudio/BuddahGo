# 架构优化基线

- 采集日期：2026-09-29（America/New_York）。
- 基线：`dev` / `origin/dev` @ `ce5c1c2`；执行分支初始 HEAD `84d3a4e`。两者的差异仅为本次优化文档，运行时代码一致。
- Unity：2022.3.55f1c1，Windows Editor；独立 worktree `C:/Users/dwh88/.codex/worktrees/architecture-optimization/BuddahGo`。
- 开工准备：`git lfs pull` 成功；`git fetch origin dev` 成功；`git merge origin/dev` 返回 Already up to date，无冲突。
- 测试环境：用户确认仅本机，先做可执行的验证；没有第二个 Steam 测试账号/测试机器。不能把单端运行或历史日志当作本次双端基线。

## 实际采集

首次导入时 C# 编译成功（Tundra build success），编译错误 0。11 条不同的已有 C# warning：

| 位置 | warning | 数量 |
|---|---|---|
| FishNet ObserverManager.LevelOfDetail | CS0162，不可达代码 | 1 |
| FishNet Demos MoveRandomlyNonPhysics | CS0414，未使用字段 | 1 |
| LeaderboardManager | CS0162，不可达代码 | 1 |
| BuddahPredictionVisualRootBridge | CS0618，PredictionSmoother 已过时 | 7 |
| IntroSequenceManager | CS0414，未使用字段 | 1 |

原始本机证据：`Logs/architecture-editor.log`（忽略的生成日志）。这仅证明 C# 编译成功，不能替代后续运行和构建验证。

## R4–R9

| ID | 结果 | 未执行原因 / 待补证据 |
|---|---|---|
| R4 联机主流程 | 未执行 | 缺少可用的第二个 Steam 测试端；大厅到比赛、三圈结算及两种投票路径尚未采集。 |
| R5 技能矩阵 | 未执行 | 缺少双端；6 技能及 anti 的 owner/observer 表现、移动效果、冷却和反噬均未实际采集。已知 B3/B4 仍按 audit 保留，不声称已复现。 |
| R6 物理交互 | 未执行 | 缺少双端；host/client 复活、碰撞、推击及普通/蓄力/连发投射物未实际采集。 |
| R7 预测确定性 | 未执行 | 缺少双端 90 秒以上会话；D-LOC 心跳数、loc/tel/mod/hof div、rec-cb 均为未采集，不能记为 0。 |
| R8 性能 | 未执行 | 尚无可比较的双端比赛负载；V13 GC.Alloc、V3 pos-davg-mean 均为未采集。主菜单或导入期数值不能作为比赛性能基线。 |
| R9 100ms 延迟 | 未执行 | 缺少双端；未运行 LatencySim 下开赛到结算及 R7。 |

P0-1 状态为 `done-unverified`。后续补跑时记录两端构建提交号、宏、时长和同一负载下的原始日志；历史 `agent-exchange` 仅用于格式参考，不充当本次测试结果。

## 不变式检查

本步骤只增加基线与进度记录，未修改代码或序列化资源。I6 不适用（没有合并重复实现）。
