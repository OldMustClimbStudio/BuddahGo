# 进度：开场交接切换的本地卡顿

状态取值：`todo` / `doing` / `blocked` / `done` / `done-unverified`。

## 当前状态与继续点（2026-10-01）

- 分支 `fix/launch-handoff-continuity`（从 `dev` 7389ec8 拉出），worktree `.worktree/launch-handoff-continuity`。
- 用户决定（2026-10-01）：不先跑 EXP-H 开关，按根因可能性顺序直接实现并由用户实测；每完成一项修复就交给用户测试，再根据反馈决定下一项。约束：只改必要函数，开场动画播放效果不变。
- 已实现 §H.1（R7.1 相机速度源 + GO 退出稳定相机的平滑过渡），状态 `done-unverified`，等待用户单机/host 实测。实现细节见「正式修复表」与「决策表」DH2。
- 下一步：用户实测 §H.1。若仍有位置停顿（非相机），按 §H.2 处理 R7.2/R7.3（需要 DH1）；若 spline 末端有曲率再看 §H.3。
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
| §H.1 | 相机速度源与开场速度 | EXP-H.1/H.2（跳过，用户直接实测修复） | done-unverified | 待提交 | 待用户实测。改动：`RaceBodyIntroStateController.TryGetSplineDrivenVelocity`（只读查询，intro 期间与 GO→消费死区内返回 spline 速度）；`PlayerCamera` 动态模式改读 `ResolveFollowVelocity`（刚体 kinematic 且 spline 仍持有身体时用 spline 速度，否则 `rb.velocity`）；退出稳定相机后 1.2 s 内 FOV/距离/方向偏移的平滑时间从 0.6 s 线性回落到原值（`stableIntroExitSmoothTime` / `stableIntroExitBlendDuration`，Inspector 可调，未改 prefab）。稳定模式本身逐帧行为未变（仍 reset + base FOV），开场动画不受影响。EditMode 测试 `PlayerCameraIntroExitTests` |
| §H.2 | 死区与视觉参考系（DH1） | EXP-H.2/H.3 | todo | | |
| §H.3 | 角速度与高度 | EXP-H.4/H.5 | todo | | |

## 决策表

| 编号 | 问题 | 决定 | 日期 |
|---|---|---|---|
| DH1 | §H.2 最小方案（DH1-a）还是 spline 进 motor 状态（DH1-b） | 实现者建议先 §H.1 + DH1-a 验证，再评估 DH1-b | 待定 |
| DH2 | §H.1 的速度源：fix-spec 原案是在 `DriveSplinePose` 对 kinematic 刚体写 `rb.velocity` | 改为相机通过 `TryGetSplineDrivenVelocity` 读取 spline 速度。原因：PhysX 对 kinematic 刚体的 setLinearVelocity 不生效且 Unity 会每次告警（"Setting linear velocity of a kinematic body is not supported"），而且 fix-spec 里「稳定模式期间更新内部状态但不应用」会让 GO 当帧 FOV 直接跳到 33.75，违反 acceptance 的 0.5°/帧。现方案：稳定模式照旧，退出时从 base 值用更长平滑时间过渡 | 2026-10-01 |

## 新发现

（与本任务无关的缺陷记在这里。）

## 备注

- 2026-10-01：分支创建，文档从联机分支拆出（原 R7 / EXP-6 / §6 / D5）。
- 2026-10-01：实现 §H.1。首次在本 worktree 用 Unity 2022.3.55f1c1 batchmode 生成 Library 并跑 EditMode 测试（结果见提交说明）。
