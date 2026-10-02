# 进度：开场交接切换的本地卡顿

状态取值：`todo` / `doing` / `blocked` / `done` / `done-unverified`。

## 当前状态与继续点（2026-10-01）

- 分支 `fix/launch-handoff-continuity`（从 `dev` 7389ec8 拉出），worktree `.worktree/launch-handoff-continuity`。
- 用户决定（2026-10-01）：不先跑 EXP-H 开关，按根因可能性顺序直接实现并由用户实测；每完成一项修复就交给用户测试，再根据反馈决定下一项。约束：只改必要函数，开场动画播放效果不变。
- §H.1（相机）已实现并实测：卡顿不变，相机不是主因。用户描述：GO 时玩家 prefab 原地停住很短时间，相机继续前进，之后直接满速。
- EXP-H.0 探针（提交 cb86e13）单机实测抓到停顿全貌，见「探针证据」。据此一次实现了 §H.2 的 DH1-a 最小方案 + 消费 tick 门控修复（R7.2/R7.3），用户单机实测「改善很多，已连续」，状态 `done`（定性验收；acceptance.md 的逐帧量化口径未用修复后数据复核，修复后那局的探针日志被控制台清掉）。R7.5（地面高度）未处理，用户决定先到此为止。
- 探针改为默认关闭（`logHandoffFrames`，Inspector 可开），保留在代码里。已向 `dev` 开 draft PR 供用户 code review。
- 未完成：R7.5 地面高度（GO 时 rb.y 3.30→3.92 的一次上浮，约 1 tick 水平减速）；host 模式与联机分支合入后的复测；修复后数据的逐帧量化验收。
- PR #60 code review（2026-10-02）处理：(1) `TryGetSplineDrivenVelocity` 改为用 `BuddahMovement.IsAuthoritativeLaunchHandoffPending`（motor 真实待消费状态）门控，消费后返回 false，纯函数 `ResolveSplineOwnsBody` 加测试；(2) GO 后外推加上限 `maxGoOvershootSeconds`（默认 0.15 s，Inspector 可调），超限时 park 并每序列告警一次，`IntroTimeUtility.GetGoOvershootSeconds` 纯函数加测试，注释写明前提（服务器 GO 时间 ≥ introStart + 最长 spline / 速度）；(3) 渲染采样滞后改为 `introVisualLagTicks`（默认 1，须与 Buddah.prefab NetworkObject Owner Interpolation 一致，FishNet 无运行时读取接口）；(4) 删除桥顶部描述已删 post-intro 锁的注释；(5) 探针相机查找改显式判空；(6) 新增 `IntroHandoffTimingTests`；(7) 纯 client 模式仍待用户实测。
- 与联机分支的关系：`fix/online-prediction-handoff` 已把 FishNet Physics Mode 改为 TimeManager（其提交 3a8b997、28414ce）。本分支基线仍是 Unity 物理模式；若在联机 host 上验证本分支，建议先合入联机分支或在本地临时应用那两个提交，否则 reconcile 回跳会干扰观察。单机模式不受此影响。
- 环境：用 2022.3.55f1c1 打开本 worktree；MCP 连接方式与克隆规则同联机分支（编辑器 "MCP for Unity" 标签页点 Connect）。

## 实验表

| 实验 | 针对 | 预检结果 | 状态 | 提交 | 结果 | 决定 |
|---|---|---|---|---|---|---|
| EXP-H.0 探针与基线 | 全部 | 单机可复现 | done | cb86e13 | 见「探针证据」：VisualRoot 在 GO 附近约 100 ms 内只前进 2.5 u（预期 7.5 u） | 按证据实现 §H.2 |
| EXP-H.1 相机速度源 | R7.1 | | done（以修复代替开关） | c77f26d | 用户实测卡顿不变 | R7.1 非主因，修复保留 |
| EXP-H.2 死区 | R7.2 | | done（探针证实） | | GO 前被钳在 spline 末端 2–3 帧；消费 tick `allowed=False` 把速度清零 1 tick | 实现 §H.2 |
| EXP-H.3 视觉参考系 | R7.3 | | done（探针证实） | | post-intro 锁吸附后平滑器再把 VisualRoot 倒退 1 u 并停 1 帧，之后滞后 2 tick | 实现 DH1-a |
| EXP-H.4 角速度 | R7.4 | | todo | | spline 末端为直线（rbVel 方向恒定），本局无影响 | |
| EXP-H.5 高度 | R7.5 | | done（探针证实） | | 消费后 rb.y 从 3.300 升到 3.916（5 tick），去穿透期间首个 tick 无水平位移 | 下一轮按残余决定 |

## 正式修复表

| 章节 | 内容 | 前置实验 | 状态 | 提交 | 验收结果 |
|---|---|---|---|---|---|
| §H.1 | 相机速度源与开场速度 | EXP-H.1/H.2（跳过，用户直接实测修复） | done（非主因，保留） | c77f26d | 用户实测单独无效，保留为 GO 后相机柔化。改动：`RaceBodyIntroStateController.TryGetSplineDrivenVelocity`（只读查询，intro 期间与 GO→消费死区内返回 spline 速度）；`PlayerCamera` 动态模式改读 `ResolveFollowVelocity`（刚体 kinematic 且 spline 仍持有身体时用 spline 速度，否则 `rb.velocity`）；退出稳定相机后 1.2 s 内 FOV/距离/方向偏移的平滑时间从 0.6 s 线性回落到原值（`stableIntroExitSmoothTime` / `stableIntroExitBlendDuration`，Inspector 可调，未改 prefab）。稳定模式本身逐帧行为未变（仍 reset + base FOV），开场动画不受影响。EditMode 测试 `PlayerCameraIntroExitTests` |
| §H.2 | 死区与视觉参考系（DH1-a） | EXP-H.2/H.3 | done（定性） | 96d85f2 | 待用户实测。改动：(1) `IntroTimeUtility.GetDriveIntroNetworkTime` 去掉 GO 时间上钳；`SampleSnapshotAtTime` 在预定 GO 时间之后沿末端切线以 intro 速度外推，`CompleteGoTransition` 用当前时间采样快照，GO 延迟期间身体不再停在 spline 末端（预定 GO 前完全不变）。(2) `TrySampleVisualPoseAtRenderTime` 改为采样 `renderTime − TickDelta`，与交接后 FishNet owner 平滑器的 1 tick 滞后一致。(3) 删除 `BuddahPredictionVisualRootBridge` 的 post-intro 视觉锁及其帧跟踪。(4) `BuddahPredictedMotor.Replicate` 的 blocked 判断增加 `_computedStats.IsRoomBypassActive`，消费 tick 不再清零继承速度 |
| §H.3 | 角速度与高度 | EXP-H.4/H.5 | todo | | R7.4 本 spline 无曲率；R7.5 待本轮实测后决定 |

## 决策表

| 编号 | 问题 | 决定 | 日期 |
|---|---|---|---|
| DH1 | §H.2 最小方案（DH1-a）还是 spline 进 motor 状态（DH1-b） | 实现者建议先 §H.1 + DH1-a 验证，再评估 DH1-b | 待定 |
| DH2 | §H.1 的速度源：fix-spec 原案是在 `DriveSplinePose` 对 kinematic 刚体写 `rb.velocity` | 改为相机通过 `TryGetSplineDrivenVelocity` 读取 spline 速度。原因：PhysX 对 kinematic 刚体的 setLinearVelocity 不生效且 Unity 会每次告警（"Setting linear velocity of a kinematic body is not supported"），而且 fix-spec 里「稳定模式期间更新内部状态但不应用」会让 GO 当帧 FOV 直接跳到 33.75，违反 acceptance 的 0.5°/帧。现方案：稳定模式照旧，退出时从 base 值用更长平滑时间过渡 | 2026-10-01 |

## 探针证据（2026-10-01 单机，提交 cb86e13 + c77f26d，修复前）

`[HandoffFrame]` 逐帧（帧率约 100 fps，tick 50 Hz，intro 60 u/s，VisualRoot 每帧应前进约 0.55 u）：

| 帧 | t | tick | 事件 | VisualRoot x | 物理根 x | 备注 |
|---|---|---|---|---|---|---|
| 5541 | 59.5200 | 2874 | HandoffWindow | 228.341 | 227.830 | 正常 |
| 5542 | 59.5295 | 2874 | 到达 spline 末端 | 228.460 (+0.12) | 227.830 | 采样时间被钳在 GO 时间 |
| 5543–5544 | 59.5403–59.5494 | 2875 | 等 GO | 228.460 (+0) | 228.460 | 预定 GO 已过，authoritative GO 未到 |
| 5545 | 59.5598 | 2876 | go=True consumed=True | 228.460 (+0) | 228.460 | 消费 tick `allowed=False gateBlocked=True`，rbVel=0 |
| 5546 | 59.5779 | 2877 | Inherit | 228.460 (+0) | 228.460，y 3.300→3.467 | 速度 60 但无水平位移，碰撞体恢复后去穿透 |
| 5547 | 59.6117 (dt 34 ms) | 2879 | post-intro-visual-lock | 230.355 (+1.90) | 230.355 | 吸附到物理根 |
| 5548 | 59.6223 | 2880 | lock 再次 | 231.317 (+0.96) | 231.317 | |
| 5549 | 59.6330 | 2880 | 平滑器恢复 | 230.355 (−0.96) | 232.294 | 倒退 1 u |
| 5550 | 59.6441 | 2881 | | 230.355 (+0) | 232.294 | 停 1 帧 |
| 5551+ | | | 正常 | 落后物理根约 2 u（2 tick） | | y 在 2881 稳定到 3.916 |

结论：VisualRoot 在 59.5295–59.6543（约 125 ms）只前进 2.47 u，预期 7.5 u。相机 FOV/偏移全程平滑（40.00→39.9，无台阶），确认 R7.1 非主因。另有一帧 34 ms 的长帧（5547），原因未查。

## 新发现

（与本任务无关的缺陷记在这里。）

## 备注

- 2026-10-01：分支创建，文档从联机分支拆出（原 R7 / EXP-6 / §6 / D5）。
- 2026-10-01：实现 §H.1。首次在本 worktree 用 Unity 2022.3.55f1c1 batchmode 生成 Library 并跑 EditMode 测试（结果见提交说明）。
- 2026-10-01：用户实测 §H.1 无效；加 EXP-H.0 探针抓到证据；实现 §H.2（DH1-a）+ 消费 tick 门控修复。DH1 按实现者建议先走 DH1-a，用户尚未明确拍板 DH1-b 是否需要。
