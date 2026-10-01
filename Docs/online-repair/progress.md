# 进度：联机预测与开场交接修复

## 当前状态与继续点（2026-10-01 暂停）

- 分支 `fix/online-prediction-handoff`，worktree `.worktree/online-prediction-handoff`，HEAD 含 EXP-0 探针、§1 修复与文档提交（见下表）。工作区干净；`Mono.Cecil.sln.meta` / `LiteNetLib.csproj.meta` 的删除是 Unity 清理被忽略文件的孤儿 meta，与本任务无关，不要提交。
- 已完成：EXP-0（探针）、EXP-1 / §1（Physics Mode → TimeManager）。用户单端实测：症状 A 消失，症状 B 不再强行拉回起点，但交接切换瞬间仍有一次小卡顿。双端实测与 0/100 ms 延迟矩阵**未做**。
- 先决条件：EXP-2 是否需要做，取决于一次**双端、100 ms** 的开场测试（纯 client 作 owner）。R3 只在有延迟的纯 client 上触发，host 单端看不到。若 client 开场不再被向后拉、位置单调向前，则 EXP-2 跳过，B 记为达标；切换瞬间的小卡顿若不可接受再做 EXP-3（R4/R5）。若 client 仍被向后拉，则做 EXP-2。
- 交接瞬间的小卡顿已静态分析为本地系统切换不连续（findings.md R7），只定位、未动手。它并入同一流程但排在最后：联机项（EXP-2/3/4/5）全部达标后再做 **EXP-6**（experiments.md，单端，五个单变量开关）→ **§6**（fix-spec.md）→ 验收「交接连续性」→ **D5**。硬约束：开场 spline 动画的播放效果不变。
- 若需要：按 experiments.md 做 **EXP-2**（§2.0 携带服务器消费 id + §2.A 确认门控，D1 已倾向方案 A，待用户确认），针对交接切换瞬间的小卡顿；之后 EXP-3（D2 倾向 D2-a）。实现前先在 100 ms 下看 `[HandoffDebug] consuming queued handoff` 之后 1 到 3 tick 内的 `[ReconcileDelta]`，确认预检序列。
- 环境恢复步骤：
  1. 用 2022.3.55f1c1 打开本 worktree（`Logs/online-repair/Editor.log`），在编辑器 "MCP for Unity" 标签页点 Connect，再 `set_active_instance online-prediction-handoff@41b28e58f7c667d2`。
  2. 双端测试用 ParrelSync 克隆 `.worktree/online-prediction-handoff_clone_0`（Clones Manager 打开，或 Hub 添加该目录）。
  3. 采样前把 `Buddah.prefab` → BuddahPredictionBootstrap → Debug Settings 的 `enableVerboseLogs`、`dumpReconcile` 置 1（已回退，不提交）；日志用 `Tools/Validation/OnlineRepair/summarize-reconcile-deltas.py` 汇总。
  4. 延迟模拟：`MainMenu.unity` NetworkManager → TransportManager → `_latencySimulator`，两端都设。

状态取值：`todo` / `doing` / `blocked` / `done` / `done-unverified`。实验与修复的定义分别见 [experiments.md](experiments.md) 与 [fix-spec.md](fix-spec.md)。

## 实验表

| 实验 | 针对 | 预检结果 | 状态 | 提交 | A 结果 | B 结果 | 决定 |
|---|---|---|---|---|---|---|---|
| EXP-0 基线（含 §0 探针） | A/B | `_physicsMode`=0 已确认；Editor 编译通过，EditMode 78/78 | done（无数值基线：用户在改动前只有目视记忆，两端都有 A/B） | 8046598 | 目视：改前有速度相关重影 | 目视：改前交接后被拉回起点重新加速 | 探针保留在 `dumpReconcile` 门控下；数值基线未采 |
| EXP-1 Physics Mode | A | 前提满足：`_physicsMode`=0、Rigidbody 插值 None、fixedDeltaTime=1/60（MCP 实测） | done（单端目视达标；双端与延迟矩阵未测） | 3a8b997, 28414ce | 用户 2026-10-01 单端实测：重影消失 | 不再强行拉回起点；交接切换瞬间仍有一次小卡顿 | 保留；A 的双端复核并入 EXP-2 的测试；B 残余进入 EXP-2 |
| EXP-2 消费后屏蔽窗口 | B | 待做：100 ms 下看 consume 后 1–3 tick 的 `[ReconcileDelta]` | todo（下一步） | | | | D1 倾向方案 A，待用户确认 |
| EXP-3 投影与回放时钟 | B | | todo | | | | |
| EXP-4 多写入者 | A/B | | todo | | | | |
| EXP-5 视觉参考系 | A | | todo | | | | |
| EXP-6 本地交接连续性（R7） | 切换卡顿 | 待做：单端复现；比较 GO tick 与 consume tick | todo（联机 A/B 达标后） | | | 子实验 1–5 待填 | 由结果选 §6.1 / §6.2（D5）；开场动画效果不变 |

## 正式修复表

| 章节 | 内容 | 前置实验 | 状态 | 提交 | 验收结果 |
|---|---|---|---|---|---|
| §1 | Physics Mode → TimeManager + 不变量断言 | EXP-1 | done（验收矩阵未跑） | 3a8b997, 28414ce | 场景运行时 `PhysicsMode=TimeManager`、Buddah 刚体插值 None、graphical=VisualRoot（MCP execute_code 读取）；验收矩阵待跑 |
| §2 | handoff / teleport 回放安全（D1 选 A 或 B） | EXP-2 | todo | | |
| §3.1 | 投影锚点统一（D2） | EXP-3 | todo | | |
| §3.2 | 服务器侧远端身体 kinematic | EXP-4.1 | todo | | |
| §4 | 回放时钟 | EXP-3.2 | todo | | |
| §5 | 视觉参考系（D3） | EXP-5 | todo | | |
| §6 | 本地交接连续性（相机速度源、死区、视觉参考系、角速度、高度；D5） | EXP-6 | todo（§1–§5 之后） | | |
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

前提确认：`_physicsMode` 实测值 = 0（Unity，`MainMenu.unity:6412`，2026-10-01 在本 worktree 读取，EXP-1 已改为 1）；现象观察端 = 两者（用户回报：改动前 host 与纯 client 都有 A 和 B）。

## 决策表

| 编号 | 问题 | 决定 | 日期 |
|---|---|---|---|
| D1 | §2 采用确认门控（A）还是回放重放（B） | 实现者建议 A（改动小、对 RTT 不敏感），待用户确认 | 2026-10-01 待定 |
| D2 | handoff 快照是否改为服务器直接采样（D2-b）或服务器同样投影（D2-a） | 实现者建议先 D2-a，有残余再 D2-b，待用户确认 | 2026-10-01 待定 |
| D3 | spectator 插值策略与 teleport 阈值 | | |
| D4 | acceptance.md 中标注"待定"的阈值 | | |
| D5 | §6 采用最小方案（D5-a）还是 spline 进 motor 状态（D5-b） | 实现者建议先 §6.1 + D5-a 验证，再评估 D5-b | 2026-10-01 待定 |

## 新发现

（与本任务无关、不在本分支修复的缺陷记在这里。）

- 2026-10-01：交接瞬间小卡顿的本地根因候选记为 findings.md R7；其中 R7.1（相机以 `rb.velocity` 为速度源，开场期间恒为 0）与 R7.2（GO 到下一 tick 消费之间的 1–2 帧死区）在 host 与单机上都成立，属本分支范围内的交接问题，不是无关缺陷。用户决定：先修联机，再修它（EXP-6/§6）。

- 2026-10-01：findings.md 假设 host 自己的身体不会出现 A/B，但用户回报改动前 host 端也有，且 EXP-1 后在单端测试中消失。说明 host 本地 client 侧的 reconcile（`_createLocalStates` 的本地状态回退）在 Unity 物理模式下同样是真实回跳；根因排序不变，EXP-4 的"多写入者"暂不需要提前。
- 2026-10-01：Physics Mode 改为 TimeManager 后，FishNet 把 `Physics.simulationMode` 持久化为 Script。Editor 里直接 Play 不含 NetworkManager 的场景时刚体不会步进，属已知代价，不在本分支处理。

## 备注

- 2026-10-01：本 worktree 专用 Unity 2022.3.55f1c1 实例已通过 MCP 连接（`online-prediction-handoff@41b28e58f7c667d2`）。ParrelSync 克隆位于 `.worktree/online-prediction-handoff_clone_0`（Assets/ProjectSettings 为符号链接，Library/Packages 为副本，参数 `client`）。
- 2026-10-01：FishNet `TimeManager.OnValidate` 在 Physics Mode 改为 TimeManager 后把 `Physics.simulationMode` / `Physics2D.simulationMode` 写成 Script 并持久化到 `ProjectSettings/DynamicsManager.asset`、`Physics2DSettings.asset`（28414ce）。游戏内 NetworkManager 为 DontDestroyOnLoad 且 Solo 也经 `GameNetworkManager` 启动 host，物理仍由 TimeManager 推进；但在 Editor 里直接 Play 不含 NetworkManager 的场景（如单独打开 RaceMap）时物理不会步进。
- 2026-10-01：`Buddah.prefab` 的 `enableVerboseLogs` / `dumpReconcile` 在工作区临时置 1 用于采样，不提交。

- 2026-10-01：分支创建，提交准备文档（findings / acceptance / experiments / fix-spec / HANDOFF）。审查为静态分析，未运行 Unity。
