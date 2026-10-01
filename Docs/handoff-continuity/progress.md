# 进度：开场交接切换的本地卡顿

状态取值：`todo` / `doing` / `blocked` / `done` / `done-unverified`。

## 当前状态与继续点（2026-10-01）

- 分支 `fix/launch-handoff-continuity`（从 `dev` 7389ec8 拉出），worktree `.worktree/launch-handoff-continuity`。本分支只有文档，没有代码改动；根因为静态分析，未在 Unity 中验证。
- 下一步：按 [experiments.md](experiments.md) 的 EXP-H 做五个单变量开关（单机即可），先验证 R7.1（相机）与 R7.2（死区）。
- 与联机分支的关系：`fix/online-prediction-handoff` 已把 FishNet Physics Mode 改为 TimeManager（其提交 3a8b997、28414ce）。本分支基线仍是 Unity 物理模式；若在联机 host 上验证本分支，建议先合入联机分支或在本地临时应用那两个提交，否则 reconcile 回跳会干扰观察。单机模式不受此影响。
- 环境：用 2022.3.55f1c1 打开本 worktree；MCP 连接方式与克隆规则同联机分支（编辑器 "MCP for Unity" 标签页点 Connect）。

## 实验表

| 实验 | 针对 | 预检结果 | 状态 | 提交 | 结果 | 决定 |
|---|---|---|---|---|---|---|
| EXP-H.0 探针与基线 | 全部 | | todo | | | |
| EXP-H.1 相机速度源 | R7.1 | | todo | | | |
| EXP-H.2 死区 | R7.2 | | todo | | | |
| EXP-H.3 视觉参考系 | R7.3 | | todo | | | |
| EXP-H.4 角速度 | R7.4 | | todo | | | |
| EXP-H.5 高度 | R7.5 | | todo | | | |

## 正式修复表

| 章节 | 内容 | 前置实验 | 状态 | 提交 | 验收结果 |
|---|---|---|---|---|---|
| §H.1 | 相机速度源与开场速度 | EXP-H.1/H.2 | todo | | |
| §H.2 | 死区与视觉参考系（DH1） | EXP-H.2/H.3 | todo | | |
| §H.3 | 角速度与高度 | EXP-H.4/H.5 | todo | | |

## 决策表

| 编号 | 问题 | 决定 | 日期 |
|---|---|---|---|
| DH1 | §H.2 最小方案（DH1-a）还是 spline 进 motor 状态（DH1-b） | 实现者建议先 §H.1 + DH1-a 验证，再评估 DH1-b | 待定 |

## 新发现

（与本任务无关的缺陷记在这里。）

## 备注

- 2026-10-01：分支创建，文档从联机分支拆出（原 R7 / EXP-6 / §6 / D5）。
