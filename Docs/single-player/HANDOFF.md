# 交接：单机模式实现

你负责在分支 `feat/single-player-mode` 上实现 Solo Match（单机对局）：一个 Human Player 对 0–5 个 AI Racer，完全离线，流程与 Online Match 一样完整。

| 你需要什么 | 看哪里 |
|---|---|
| 术语（讨论、命名、注释都用这套词） | [CONTEXT.md](../../CONTEXT.md) |
| 设计与现有代码的坑 | [design.md](design.md) |
| 各阶段的目标和完成标准 | [phases.md](phases.md) |
| 为什么这样定 | [Docs/adr/](../adr/) |
| 进度的唯一来源 | [progress.md](progress.md) |

仓库通用规则以 [harness.md](../../harness.md) 与 [CONTRIBUTING.md](../../CONTRIBUTING.md) 为准。

## 开工条件

- **本分支在重构计划合入 `dev` 之后才开始实现**（团队决定：重构先行）。
- 开工前确认 `refactor/architecture-optimization` 中团队决定执行的阶段已经合入 `dev`，然后从 S0 开始。

## 每次开工

1. 进入 worktree（没有就创建）：

   ```bash
   git worktree add .worktree/single-player-mode feat/single-player-mode
   ```

2. 拉取依赖：`git lfs pull`，再 `git merge origin/dev`。
3. 打开 progress.md，从第一个不是 `done` 的阶段继续。

## 每个阶段的做法

1. 在 progress.md 中把该阶段标为 `doing`。
2. 读完 phases.md 里该阶段的目标和完成标准，以及 design.md 中相关的章节。
3. 先读受影响的代码，再动手。design.md 第 4 节列出的位置是起点，不是完整清单。
4. 按 CONTRIBUTING 的格式小步提交，标题示例：`feat: [S3] spawn owner-less AI racers`。
5. 跑该阶段要求的验证项 V*，并在 progress.md 记录结果。当前环境跑不了的项写"未执行 + 原因"，不能写成通过。
6. 完成标准全部满足后，标为 `done`，然后向 `dev` 开 PR。PR 的 Validation 一节列出各 V 项的结果。

**阶段完成**：完成标准逐条满足，要求的 V 项有实际结果，progress.md 已更新。

## 需要停下来找团队的情况

- 需要改变 CONTEXT.md 中某个术语的含义，或者需要推翻某个 ADR。
- S2 之后联机回归（V2）与基线不一致，而且找不到原因。
- AI 的决策层必须修改运动组件本身才能工作。这违反 ADR 0003。
- 调参场景的结果需要团队看过才能定下难度数值（S4 的验收本来就要求团队确认）。

决策结论记进 progress.md 的决策表；属于长期决策的，新增一份 ADR。

## 必须知道的事实

- **单机里不会出现预测回滚问题**：纯 host 会话里，FishNet 不执行 reconcile 和回放。如果单机里出现抖动，先查视觉平滑层（V7），不要去改预测逻辑。
- **AI 的 owner 信息是固定的**：AI 的 `OwnerId` 恒为 -1，`Owner` 是 EmptyConnection，不是 null。任何按 owner 做的判断或键，都会把所有 AI 当成同一个对象。
- **比赛是自动全油门的**：油门固定为 1，AI 只需要转向和施法。
- **运动没有线性阻力**：所以 AI 必须往前推演才能稳定走线（design.md §3.4）。不要为了让 AI 好控制而改动运动参数。
- **会改第三方插件**：FishyFacepunch 的 Steam 容错是本地修改（ADR 0004），升级插件时要保留。
- **注意和重构分支的冲突**：重构分支和本分支都改过 `Docs/README.md` 与 `harness.md` 的索引行，合并时两行都保留。
