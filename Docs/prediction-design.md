# 预测系统核心设计

当前实现位于 `Assets/Scripts/New_Buddah/`。本文说明模拟、回放与表现的边界及已实现的时钟契约；阶段实测结果和未完成验收见[公开运行记录](optimization/review-runtime-round2.md)。

## 模拟与回放

```text
输入/外部效果 → bridge 或 adapter → tick 对齐的数据/事件
  → BuddahPredictedMotor.RunInputs / Simulation steps
  → PredictionRigidbody 模拟
  → PostTick reconcile 快照与表现通知
  → 服务端状态校正，必要时回放旧输入
```

- 影响回放结果的可变状态需要可以恢复，包括控制限制、modifier、计时器和事件消费状态。不要只同步位置而遗漏行为状态。
- 同一份输入、恢复状态和配置应产生一致结果。将一次性的技能/VFX 通知与可能重复执行的模拟分开，避免回放重复触发副作用。
- 保留现有 tick/reconcile 生命周期。新增数据时确认实际序列化能往返；编译通过不等于远端收到字段。改动 wire 格式时把 `PredictionProtocol.Version` 加一，规则见 [FishNet 要点](networking.md#协议版本)。
- 游戏运动由预测栈及 `PredictionRigidbody` 处理，视觉层不能用刚体修正来掩盖模拟分歧。
- 回放必须真实步进物理：TimeManager 的 Physics Mode 固定为 `TimeManager`（项目物理模拟模式随之为 Script），刚体插值为 None，`Time.fixedDeltaTime` 等于 `TickDelta`。`BuddahPredictedMotor.ValidatePredictionInvariants` 在 Editor/Development 下检查这几项。代价：Editor 里直接 Play 一个没有 NetworkManager 的场景时刚体不会步进。

## 外部效果与控制权交接

- Impulse、Teleport、Modifier、Handoff 分别表达冲量、位置重置、持续效果与控制权变化。通过对应 adapter/bridge 接入，避免旁路写入。
- 事件的发生 tick、逻辑标识与消费顺序需要在前向模拟和回放中一致；网络到包时间不能直接充当事件发生时间。
- 容量溢出、重复事件和过期事件需要明确处理，关键效果不能悄悄丢失。
- 开场、复活和结算交接需要考虑 owner/server/旁观者。请求成功返回只代表当前步骤被接受，不能证明 RPC 返回链路已完成。
- 模式切换清理旧输入、队列和控制标记，防止 Legacy 与预测运动同时驱动角色。
- 开场交接：本地 GO 到 motor 消费权威 handoff 之间，`RaceBodyIntroStateController` 继续沿 spline 末端切线驱动 kinematic 刚体和渲染采样（上限 `maxGoOvershootSeconds + maxHandoffPendingOvershootSeconds`），motor 在此期间不写刚体；Solo 物理时钟路径不外推。消费时 `ConsumePendingLaunchHandoffEvent` 把根落到碰撞体静止高度（`BuddahHandoffGroundSnap`），owner 与 server 同一路径。2026-10-30 起交接是完全物理的（玩法深化 §3）：消费那一 tick 设一次开场速度，Inherit / Blend 时长（`Movement.launchInheritSeconds` / `launchBlendSeconds`）和交接后的转向屏蔽都是 0，之后只有物理和输入；联机两端在这个设定下的交接表现没有重新验证。来龙去脉见 [联机修复](online-repair/progress.md) 与 [交接连续性](handoff-continuity/findings.md)。

## 技能 modifier 命令

技能的运动效果只经修饰命令进入马达：`SkillExecutor.ApplyXToOwner` → `OwnerMovementEffectApplier` → `BuddahPredictionSkillMovementBridge` → `BuddahPredictionModifierBridge` → `BuddahPredictedMotor.TryApplyModifierCommand`。服务器写一份，再用 TargetRpc 让 owner 写同一条；没有 owner 的 AI 和训练木桩只有服务器那份。命令落成 `BuddahPredictedModifierState` 里的 `*UntilTick` 期限，纯状态写入在 `BuddahPredictedModifierStateOps`，每 tick 由 `BuddahPredictedModifierResolver.Resolve` 折算成推力、极速、转向和各个标志位。

`BuddahPredictedModifierCommandType` 有 11 种命令：Acceleration=1、RootThenAcceleration=2、InvertTurn=3、Scale=4、PushGrace=5、SteeringSuppression=6、RoomBypass=7、Freeze=8、EndModifier=9、Burst=10、MaxSpeedMultiplier=11。编号显式写死：配表 `Global/__enums__` 的 `ModifierCommandType` 与 C# 同号，导入时整数按 C# 枚举值解析（[配表决定 29](design-tables/decisions.md)）。命令本身不经网络传输：每种效果有自己的 TargetRpc，只带秒数、倍率等参数，owner 在本端构造同一条命令。后四种来自玩法深化：

| 命令 | 状态字段 | 规则 |
|---|---|---|
| Freeze（禁锢） | `FreezeUntilTick` | 推力、极速和转向扭矩归零，`IsRooted` 与 `IsSteeringSuppressed` 同时为真。两次冻结取较晚的期限。生效时清掉待施加的力，速度归零 |
| EndModifier | 无，作用于目标命令的期限 | 目标限于 Acceleration、RootThenAcceleration、Scale、Freeze、Burst、MaxSpeedMultiplier。期限改为 `Min(期限, 当前 tick)`：未设置的 0 保持 0，永不延长。结束 Acceleration 时只清正向加速，同一通道里的缓流减速照常到期 |
| Burst（突进） | `BurstUntilTick`、`BurstDurationTicks`、`BurstExtraMaxSpeed` | 额外极速乘以 remaining01²（ease-out）衰减到 0。新的 Burst 整条曲线重来。生效时沿水平朝向按解析后的极速直接设速度；被定身或冻结时 `IsBurstActive` 为假 |
| MaxSpeedMultiplier（反噬减速） | `MaxSpeedPenaltyUntilTick`、`MaxSpeedPenaltyMultiplier` | 在所有加法加成之后相乘，倍率限制在 [0.1, 1]。期限取较晚者，倍率以后写为准 |

- 解析顺序：基础值 → 体型 → 加法加成（加速、定身后加速、突进）→ 反噬减速倍率 → 定身/冻结归零 → 下限 0。
- 重置：服务器控制 racer（AI、木桩）的技能重置 `ResetActiveSkillModifiers` 清掉定身与定身后加速、加速、体型、反转、冻结和突进，保留推掌宽限、转向抑制、房间旁路和反噬减速；带 `ResetModifiers` 的 teleport（复活）清空全部状态，只留下反噬减速（`PreservePersistentOnReset`）。所以反噬减速总是持续到自己的期限。
- 纯客户端 reconcile 回放时，modifier 按正在回放的 tick 解析：`BuddahTickMath.ModifierResolveTick` 在客户端回放中返回 `PredictionManager.ClientReplayTick`，其余情况返回 `LocalTick`。回放中 `LocalTick` 不动，用它会让 Burst 的逐 tick 衰减和所有期限都按“现在”计算。冲量消费和开场状态仍用 live tick。
- 新增一条修饰期限的改动清单（状态字段、命令、马达分支、解析、tick 平移、bridge、配表登记、协议版本）见[缘起架构设计附录 A](gameplay/yuanqi-architecture.md)。

## 表现与生命周期

- GraphicalObject、相机和 VFX 读取模拟/平滑结果，只修改自身表现，不反写游戏状态。
- 区分物理状态分歧与视觉插值问题；调整平滑参数前先定位发生分歧的层。
- 初始化和网络回调可能经历重入或重连；订阅与退订配对，避免重复初始化重置正在使用的状态。
- 控制权、RPC 或回放逻辑变化时验证相应远端路径；诊断只收集能解释问题的 tick、状态和事件证据。

## Reconcile 快照与本地期限

[ReconcileState](../Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs) 先从原始 `data.ModifierState` / `data.HandoffState` 建立工作副本，再调用 [BuddahTickMath.ReconcileToLocal](../Assets/Scripts/New_Buddah/Core/BuddahTickMath.cs)。纯客户端使用该次 reconcile 的 `PredictionManager.ServerStateTick` / `ClientStateTick` 配对，不能使用接收时刻的 `TimeManager.Tick` / `LocalTick`；后者的估计时钟更新会移动同一快照的期限。

- server/host 不平移。纯客户端平移十一个 modifier deadline（Root、Accel、PostRootAccel、Scale、InvertTurn、PushGrace、SuppressSteering、RoomBypass、Freeze、Burst、MaxSpeedPenalty）和六个 handoff tick；`BurstDurationTicks` 是时长，不平移。wire 数据和历史快照保留服务器时间戳，避免重复平移。
- 映射为 `ClientStateTick + 原时间戳 - ServerStateTick`，先用宽有符号整数计算，再饱和到 `[0, uint.MaxValue]`。事件/阶段边界的零是有效时间戳；deadline 的零保留未设置语义。空 handoff reset 保持为空，inactive 状态仍可携带需转换的 steering/bypass 尾部。
- FishNet 提供给 owner 的 `data.GetTick()` 已是 `ClientStateTick`，不再平移；observer 的 `ServerStateTick` 转到同一本地时钟，用于工作状态的诊断推进。
- 原始历史快照在 replay 对应 tick 上仍可呈现当时有效的阶段；不能仅凭历史快照的 active 标志判断前向状态“复活”。后续权威快照也可合法延长期限，不能用永久过期标记或单调截断缓存压掉更新。

[HandoffClockTests](../Assets/Tests/EditMode/HandoffClockTests.cs) 包含快照配对、十一个 modifier deadline 的平移、接收时钟回退后的过期边界、历史 Blend 回放、合法期限延长及零值/饱和语义测试。边界用例使用合成输入；不是新增的实测日志。饱和运算不构成跨完整 uint 回绕周期的时钟协议。

## 冲量消费时钟

[ConsumePendingImpulseEvents_Authoritative](../Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.Events.cs) 通过 `BuddahTickMath.ImpulseEventClock` 选择比较时钟；冲量队列继续保存服务器时间戳。

| 执行路径 | 消费资格所用时钟 |
|---|---|
| server/host | 调用方的权威 `currentTick` |
| 纯客户端前向模拟 | `TimeManager.Tick` |
| 纯客户端正在 reconcile/replay | `PredictionManager.ServerReplayTick` |

[事件通道](../Assets/Scripts/New_Buddah/Events/BuddahPredictionEventChannel.cs) 仅将 `EventTick <= 比较时钟` 的项交给消费回调；回调成功才移除并记录去重标识。历史 replay 不得借用已经超过事件时刻的 live server tick 提前消费。回放本身仍然保留，不能把不同 rollback pass 的重演一律视为重复应用。[BuddahTickMathTests](../Assets/Tests/EditMode/BuddahTickMathTests.cs) 覆盖未来事件仍待消费、到达事件 tick 后消费及前向/host 分支。

这些规则分别服务于 reconcile 期限和冲量队列；不要把冲量消费时钟直接替换成快照配对，也不要把收到 teleport 或 owner 锚定 handoff RPC 时的队列转换改成 reconcile 转换。

## 诊断与验收边界

预测/技能输入的屏幕 debug 已移除绘制入口，普通 HUD 和日志保留原有构建门控。采样开关、复现命令和已公开 R8 范围见[诊断日志说明](diagnostic-log-sampling-r8.md)。

玩法深化的四种新命令和回放 tick 解析由 EditMode 测试覆盖（[BuddahPredictedModifierResolverTests](../Assets/Tests/EditMode/BuddahPredictedModifierResolverTests.cs)、[BuddahPredictedModifierStateOpsTests](../Assets/Tests/EditMode/BuddahPredictedModifierStateOpsTests.cs)、`BuddahTickMathTests`、`HandoffClockTests`），实机只在训练模式（本机 host）验证过（[训练模式验证记录](gameplay/practice-verification.md)）。冻结、突进和反噬减速在远端 owner 上的预测与 reconcile 还没有联机验证。

时钟修复不保证迟到客户端完整呈现配置的 Inherit/Blend 时长（原 12/20 tick；2026-10-30 起两段都配为 0，见上文开场交接）：以后重新配成非零时，完整呈现与权威结束时刻如何协调仍需协议决策。高速视觉接受也仍由用户决定。公开运行记录中的历史 75/75 及旧会话观测只证明对应版本，不作为快照配对提交的实测结果；未公开日志、实测数值和视频不在本文中补录。
