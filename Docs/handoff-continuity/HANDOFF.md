# 交接：开场交接切换的本地卡顿（给实现者）

你负责在分支 `fix/launch-handoff-continuity`（从 `dev` 7389ec8 拉出）上，先按实验顺序确认根因，再按规格实现修复。本分支到交接时只有文档，没有代码或资源改动。诊断是静态分析，**没有在 Unity 中运行过**。

## 范围

- 处理：GO 瞬间从 spline 驱动切到 motor 驱动的本地不连续（相机速度源、死区、视觉参考系、角速度、高度）。host 与单机共用这条路径，单机可直接验证与合入。
- 不处理：联机的 reconcile 回跳、handoff 回放安全、投影差，这些在 `fix/online-prediction-handoff`。
- 硬约束：开场 spline 动画的播放效果（路径、速度、时长、相机稳定模式）不变。

## 文档地图

| 你要做什么 | 读 |
|---|---|
| 了解现象、五个子根因与精确位置 | [findings.md](findings.md) |
| 按子项做单变量实验 | [experiments.md](experiments.md) |
| 实现被证实的修复 | [fix-spec.md](fix-spec.md) |
| 验收口径与不变项 | [acceptance.md](acceptance.md) |
| 记录进度、决策、新发现 | [progress.md](progress.md) |

仓库规则以 [harness.md](../../harness.md)、[CONTRIBUTING.md](../../CONTRIBUTING.md) 为准；预测系统契约见 [prediction-design.md](../prediction-design.md)。

## 工作顺序

1. 加 §H.0 探针，录基线。
2. EXP-H.1 → H.5 逐个开关测，填 progress.md。
3. 按被证实的章节实现，跑 acceptance.md，更新 progress.md。
4. 每阶段单独提交：探针 `chore: [EXP-H.n] …`，修复 `fix: …`，文档 `docs: …`。
5. 向 `dev` 开 PR；若联机分支先合入，先 `git merge origin/dev` 再提。

## 必须停下来找人

- DH1 未决定时不要替团队决定。
- 任何干预让开场动画观感改变。
- 需要改 RPC 签名或 `BuddahPredictedReconcileData` 以外的网络数据结构。
