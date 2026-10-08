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
- 保留现有 tick/reconcile 生命周期。新增数据时确认实际序列化能往返；编译通过不等于远端收到字段。
- 游戏运动由预测栈及 `PredictionRigidbody` 处理，视觉层不能用刚体修正来掩盖模拟分歧。
- 回放必须真实步进物理：TimeManager 的 Physics Mode 固定为 `TimeManager`（项目物理模拟模式随之为 Script），刚体插值为 None，`Time.fixedDeltaTime` 等于 `TickDelta`。`BuddahPredictedMotor.ValidatePredictionInvariants` 在 Editor/Development 下检查这几项。代价：Editor 里直接 Play 一个没有 NetworkManager 的场景时刚体不会步进。

## 外部效果与控制权交接

- Impulse、Teleport、Modifier、Handoff 分别表达冲量、位置重置、持续效果与控制权变化。通过对应 adapter/bridge 接入，避免旁路写入。
- 事件的发生 tick、逻辑标识与消费顺序需要在前向模拟和回放中一致；网络到包时间不能直接充当事件发生时间。
- 容量溢出、重复事件和过期事件需要明确处理，关键效果不能悄悄丢失。
- 开场、复活和结算交接需要考虑 owner/server/旁观者。请求成功返回只代表当前步骤被接受，不能证明 RPC 返回链路已完成。
- 模式切换清理旧输入、队列和控制标记，防止 Legacy 与预测运动同时驱动角色。
- 开场交接：本地 GO 到 motor 消费权威 handoff 之间，`RaceBodyIntroStateController` 继续沿 spline 末端切线驱动 kinematic 刚体和渲染采样（上限 `maxGoOvershootSeconds + maxHandoffPendingOvershootSeconds`），motor 在此期间不写刚体；Solo 物理时钟路径不外推。消费时 `ConsumePendingLaunchHandoffEvent` 把根落到碰撞体静止高度（`BuddahHandoffGroundSnap`），owner 与 server 同一路径。来龙去脉见 [联机修复](online-repair/progress.md) 与 [交接连续性](handoff-continuity/findings.md)。

## 表现与生命周期

- GraphicalObject、相机和 VFX 读取模拟/平滑结果，只修改自身表现，不反写游戏状态。
- 区分物理状态分歧与视觉插值问题；调整平滑参数前先定位发生分歧的层。
- 初始化和网络回调可能经历重入或重连；订阅与退订配对，避免重复初始化重置正在使用的状态。
- 控制权、RPC 或回放逻辑变化时验证相应远端路径；诊断只收集能解释问题的 tick、状态和事件证据。

## Reconcile 快照与本地期限

[ReconcileState](../Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.cs) 先从原始 `data.ModifierState` / `data.HandoffState` 建立工作副本，再调用 [BuddahTickMath.ReconcileToLocal](../Assets/Scripts/New_Buddah/Core/BuddahTickMath.cs)。纯客户端使用该次 reconcile 的 `PredictionManager.ServerStateTick` / `ClientStateTick` 配对，不能使用接收时刻的 `TimeManager.Tick` / `LocalTick`；后者的估计时钟更新会移动同一快照的期限。

- server/host 不平移。纯客户端平移八个 modifier deadline 和六个 handoff tick；wire 数据和历史快照保留服务器时间戳，避免重复平移。
- 映射为 `ClientStateTick + 原时间戳 - ServerStateTick`，先用宽有符号整数计算，再饱和到 `[0, uint.MaxValue]`。事件/阶段边界的零是有效时间戳；deadline 的零保留未设置语义。空 handoff reset 保持为空，inactive 状态仍可携带需转换的 steering/bypass 尾部。
- FishNet 提供给 owner 的 `data.GetTick()` 已是 `ClientStateTick`，不再平移；observer 的 `ServerStateTick` 转到同一本地时钟，用于工作状态的诊断推进。
- 原始历史快照在 replay 对应 tick 上仍可呈现当时有效的阶段；不能仅凭历史快照的 active 标志判断前向状态“复活”。后续权威快照也可合法延长期限，不能用永久过期标记或单调截断缓存压掉更新。

[HandoffClockTests](../Assets/Tests/EditMode/HandoffClockTests.cs) 包含快照配对、接收时钟回退后的过期边界、历史 Blend 回放、合法期限延长及零值/饱和语义测试。边界用例使用合成输入；不是新增的实测日志。饱和运算不构成跨完整 uint 回绕周期的时钟协议。

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

时钟修复不保证迟到客户端完整呈现配置的 Inherit/Blend 12/20 tick：完整呈现与权威结束时刻如何协调仍需协议决策。高速视觉接受也仍由用户决定。公开运行记录中的历史 75/75 及旧会话观测只证明对应版本，不作为快照配对提交的实测结果；未公开日志、实测数值和视频不在本文中补录。
