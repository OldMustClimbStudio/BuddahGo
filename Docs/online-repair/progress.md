# 进度：联机预测与开场交接修复

状态取值：`todo` / `doing` / `blocked` / `done` / `done-unverified`。实验与修复的定义分别见 [experiments.md](experiments.md) 与 [fix-spec.md](fix-spec.md)。

## 实验表

| 实验 | 针对 | 预检结果 | 状态 | 提交 | A 结果 | B 结果 | 决定 |
|---|---|---|---|---|---|---|---|
| EXP-0 基线（含 §0 探针） | A/B | `_physicsMode`=0 已确认；Editor 编译通过，EditMode 78/78 | done-unverified（基线数据待用户采集） | 8046598 | 待填 | 待填 | 探针保留在 `dumpReconcile` 门控下 |
| EXP-1 Physics Mode | A | 前提满足：`_physicsMode`=0、Rigidbody 插值 None、fixedDeltaTime=1/60（MCP 实测） | done-unverified（等待用户两端实测） | 3a8b997, 28414ce | 待填 | 待填 | 无论结果都保留 |
| EXP-2 消费后屏蔽窗口 | B | | todo | | | | |
| EXP-3 投影与回放时钟 | B | | todo | | | | |
| EXP-4 多写入者 | A/B | | todo | | | | |
| EXP-5 视觉参考系 | A | | todo | | | | |

## 正式修复表

| 章节 | 内容 | 前置实验 | 状态 | 提交 | 验收结果 |
|---|---|---|---|---|---|
| §1 | Physics Mode → TimeManager + 不变量断言 | EXP-1 | done-unverified | 3a8b997, 28414ce | 场景运行时 `PhysicsMode=TimeManager`、Buddah 刚体插值 None、graphical=VisualRoot（MCP execute_code 读取）；验收矩阵待跑 |
| §2 | handoff / teleport 回放安全（D1 选 A 或 B） | EXP-2 | todo | | |
| §3.1 | 投影锚点统一（D2） | EXP-3 | todo | | |
| §3.2 | 服务器侧远端身体 kinematic | EXP-4.1 | todo | | |
| §4 | 回放时钟 | EXP-3.2 | todo | | |
| §5 | 视觉参考系（D3） | EXP-5 | todo | | |
| 收尾 | 全矩阵回归、文档更新、探针移除 | 全部 | todo | | |

## 基线表（EXP-0 填写）

| 视角 / 延迟 | reconcile 位置校正 均值 / p95 / 最大 (u) | spectator 向后位移帧占比 | GO 后 1 秒最大校正 (u) | consume 到首个大校正的 tick 差 | motorVisualPosDelta 顶速 (u) |
|---|---|---|---|---|---|
| host-owner / 0 ms | | | | | |
| client-owner / 0 ms | | | | | |
| host 观察 client / 0 ms | | | | | |
| client 观察 host / 0 ms | | | | | |
| host-owner / 100 ms | | | | | |
| client-owner / 100 ms | | | | | |
| host 观察 client / 100 ms | | | | | |
| client 观察 host / 100 ms | | | | | |

前提确认：`_physicsMode` 实测值 = 0（Unity，`MainMenu.unity:6412`，2026-10-01 在本 worktree 读取，EXP-1 已改为 1）；现象观察端 = ____（host / 纯 client / 两者，待用户回报）。

## 决策表

| 编号 | 问题 | 决定 | 日期 |
|---|---|---|---|
| D1 | §2 采用确认门控（A）还是回放重放（B） | | |
| D2 | handoff 快照是否改为服务器直接采样（D2-b）或服务器同样投影（D2-a） | | |
| D3 | spectator 插值策略与 teleport 阈值 | | |
| D4 | acceptance.md 中标注"待定"的阈值 | | |

## 新发现

（与本任务无关、不在本分支修复的缺陷记在这里。）

## 备注

- 2026-10-01：本 worktree 专用 Unity 2022.3.55f1c1 实例已通过 MCP 连接（`online-prediction-handoff@41b28e58f7c667d2`）。ParrelSync 克隆位于 `.worktree/online-prediction-handoff_clone_0`（Assets/ProjectSettings 为符号链接，Library/Packages 为副本，参数 `client`）。
- 2026-10-01：FishNet `TimeManager.OnValidate` 在 Physics Mode 改为 TimeManager 后把 `Physics.simulationMode` / `Physics2D.simulationMode` 写成 Script 并持久化到 `ProjectSettings/DynamicsManager.asset`、`Physics2DSettings.asset`（28414ce）。游戏内 NetworkManager 为 DontDestroyOnLoad 且 Solo 也经 `GameNetworkManager` 启动 host，物理仍由 TimeManager 推进；但在 Editor 里直接 Play 不含 NetworkManager 的场景（如单独打开 RaceMap）时物理不会步进。
- 2026-10-01：`Buddah.prefab` 的 `enableVerboseLogs` / `dumpReconcile` 在工作区临时置 1 用于采样，不提交。

- 2026-10-01：分支创建，提交准备文档（findings / acceptance / experiments / fix-spec / HANDOFF）。审查为静态分析，未运行 Unity。
