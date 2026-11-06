# 归档

已收尾的修复、计划、复核和阶段记录。这里是证据，不是当前指令或任务队列；判断现状以 `dev` 的代码和当前入口文档为准（[../README.md](../README.md)）。问题复现时，从对应目录的 progress / HANDOFF 接续。

| 目录 | 内容 |
|---|---|
| [optimization/](optimization/HANDOFF.md) | 架构优化计划 P0–P7（PR #47–#58） |
| [online-repair/](online-repair/HANDOFF.md) | 联机预测撕裂与开场交接修复（PR #61） |
| [handoff-continuity/](handoff-continuity/HANDOFF.md) | 开场交接切换的本地卡顿（PR #60） |
| [single-player/](single-player/) | 单机模式开发期（PR #59）的复核、一次性修复与阶段证据 |
| [rendering/](rendering/) | 渲染评审记录（PR #64） |
| [vfx-asset-recovery.md](vfx-asset-recovery.md) · [diagnostic-log-sampling-r8.md](diagnostic-log-sampling-r8.md) | 特效资源恢复；R8 诊断日志采样 |

**2026-11-05 移出仓库的原始证据**（文档里的链接已改成永久链接；纯文本提到的路径用 `git show cc7bc106:<路径>` 读取）：

- `agent-exchange/`：2026-04/05 预测重构 phase3/4 的控制台日志与交接（68 个文件）
- `Docs/optimization/evidence/`：R8 CSV 压缩包与运行时 JSON
- `Tools/ai/thrust-vector/rollout-2026-10-02/`：三档难度验收比赛的输出
- `Tools/Validation/ReviewRuntimeRound2/`、`Tools/Validation/*-cases.cs.txt`（`modifier-clock-cases.cs.txt` 除外，机制更新仍在维护）、`Tools/Performance/`：重构 R 项的一次性驱动脚本
- `Docs/` 下没有被任何文档直接链接的逐视角帧与候选图（120 张）

更早的历史在 Git 里，例如 `git log -- Docs/prediction-refactor-plan/`。
