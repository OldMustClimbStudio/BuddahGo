# 进度：联机预测与开场交接修复

状态取值：`todo` / `doing` / `blocked` / `done` / `done-unverified`。

| 阶段 | 内容 | 状态 | 提交 | 测试结果 |
|---|---|---|---|---|
| P0 | 基线采集 | todo | | |
| P1 | Physics Mode → TimeManager（R1） | todo | | |
| P2 | handoff / teleport 回放安全（R3、R4、R6） | todo | | |
| P3 | 视觉参考系（R2） | todo | | |
| P4 | 回放时钟（R5） | todo | | |
| 收尾 | 全矩阵回归与文档更新 | todo | | |

## 基线表（P0 填写）

| 视角 / 延迟 | reconcile 位置校正 均值 / p95 / 最大 | spectator 向后位移帧占比 | GO 后 1 秒最大校正 | consume 到首个大校正的 tick 差 |
|---|---|---|---|---|
| host-owner / 0 ms | | | | |
| client-owner / 0 ms | | | | |
| host 观察 client / 0 ms | | | | |
| client 观察 host / 0 ms | | | | |
| host-owner / 100 ms | | | | |
| client-owner / 100 ms | | | | |
| host 观察 client / 100 ms | | | | |
| client 观察 host / 100 ms | | | | |

## 决策表

| 编号 | 问题 | 决定 | 日期 |
|---|---|---|---|
| D1 | P2 采用确认门控还是回放重放 | | |
| D2 | handoff 快照是否改为服务器直接采样 | | |
| D3 | spectator 插值策略与 teleport 阈值 | | |
| D4 | 待定阈值的最终数值 | | |

## 新发现

（与本任务无关、不在本分支修复的缺陷记在这里。）

## 备注

- 2026-10-01：分支创建，提交准备文档。审查为静态分析，未运行 Unity。
