# 交接：联机预测与开场交接修复

分支 `fix/online-prediction-handoff` 从 `dev` 拉出，用于修复联机中的 reconcile 撕裂/重影与开场 handoff 失调。本提交只有文档，没有代码改动。

- 观察与根因：[findings.md](findings.md)
- 目标效果与测试计划：[acceptance.md](acceptance.md)
- 进度的唯一来源：[progress.md](progress.md)

仓库通用规则仍以 [harness.md](../../harness.md) 和 [CONTRIBUTING.md](../../CONTRIBUTING.md) 为准。

## 开工前

1. 建 worktree：`git worktree add .worktree/online-prediction-handoff fix/online-prediction-handoff`，执行 `git lfs pull`。
2. `git merge origin/dev`，有冲突先解决并记入 progress.md。
3. 先完成 acceptance.md 的 P0 基线采集，再动代码。没有基线，后面的阶段无法证明收益。

## 执行顺序

P0 基线 → P1 Physics Mode → P2 handoff/teleport 回放安全 → P3 视觉参考系 → P4 回放时钟 → 收尾。
P1 是前提：P2–P4 的验收依赖回放真正执行物理。P3 可以与 P2 并行，但走单独提交。

每个阶段完成后在 progress.md 标记状态、填入提交 hash 与测试结果；静态检查不能写成运行通过；环境无法执行的项写“未执行 + 原因”。

## 必须停下来找人的情况

- acceptance.md 列出的决定项 D1–D4。
- 需要改 replicate/reconcile 数据结构或 RPC 签名。
- P1 之后 D-LOC 任一轴 div≠0 且找不到原因。
- 发现与本任务无关的新缺陷：记入 progress.md 的“新发现”，不在本分支顺手修。

## 范围之外

单机模式（Solo）、技能命中逻辑、房间与大厅流程。
