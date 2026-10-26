# 缘起架构设计

> **头注**
>
> - **这是什么**：缘起（技能组合）的代码架构设计稿：新代码放哪里、模块之间谁能引用谁、配表怎么扩、三对已定缘起怎么落地，其中 #2 禅足冰封写到可以直接开工的粒度。日期 2026-10-25，状态：**设计稿，尚未实现**。
> - **依据**：机制规则以 [README §5](README.md) 为准，每对内容以 [缘起技能设计](yuanqi.md) 为准；本文不重复规则，只讲怎么构筑。代码事实按 `feat/mechanics-update` ec486a9（等于 dev 5f91e51 的代码）核对，实现前再核对一次。
> - **标记**：“已定”沿用 README 的用法；“建议”是本文的架构提案，待用户确认；“事实”是读代码得到的现状。
> - **和 README 7.1 的关系**：本文是 A5（缘起）的展开，也给 A3、A4 提出了缘起需要它们保证的接口。README 7.1 的步骤顺序不变。
> - **配表**：按 [design-tables README](../design-tables/README.md) 与 [excel-spec](../design-tables/excel-spec.md) 的规范写表；决定 4 要求“是否入表”先同步，所以第 3 节的表结构全部是建议。

## 0. 结论先行

| 问题 | 答案 |
|---|---|
| 新代码怎么写才不把现有模块弄脏 | 缘起是技能之外的第三方：新目录 `ComboSkill/Yuanqi/`，五个部件（状态板、登记、缘起模块、技能兼容接口、缘起表现）。技能只多做两件事：生效时在状态板登记自己、提供一个“可被消耗 / 可被改写”的句柄。`SkillExecutor` 不认识任何一对缘起。运动效果全部走预测修饰命令，不碰刚体 |
| 配表怎么变 | 阶段 A 不动表头，新参数先放缘起 SO 的序列化字段。阶段 B 按“Skill 宽表 + 强类型家族表”的现成模式加 `Skill/Yuanqi` 与 `Skill/YuanqiEffect<对>`，`__enums__` 登记 `YuanqiId`，映射走 `SkillDatabase` 现有的“Play 首次查表时写入 SO、退出 Play 恢复”那条路，不新建第二套时机 |
| 禅足冰封怎么构筑 | 预测马达新增一条 `Freeze` 修饰期限（复用定身的“限速清零 + 提前返回”语义，再加角速度归零与动作锁），服务器扫描半径内的所有 Buddah 并逐个下发；然后通过句柄消耗神足通（新增“结束加速”命令）和缓流（销毁区域 + 新增“停止特效”RPC）。详见第 4 节 |

## 1. 现状（事实）

### 1.1 技能分发链路

```text
玩家按键 / AI 注入
  → SkillExecutor.RequestCast → CastSlotServerRpc → CastSlotServer
  → TryResolveCast（结算区闸门、槽位、全局锁 _castLockedUntil、槽冷却 _nextReadyTime）
  → ResolveCastVariant（按执念掷骰，可能换成 *_anti 的 SkillAction）
  → QueueCast（写全局锁与槽冷却；PlayQueuedCastFeedbackObserversRpc 播前摇）
  → 1 秒后 ExecuteQueuedCastAfterDelay
      → executedSkill.ExecuteServer(caster, slot)   ← 技能真正生效的时刻
      → ObsessionFigure.AddServer(gain)
      → CastObserversRpc → ExecuteObservers / ExecuteLocal（纯表现）
      → ServerCastChanged(Executed)                 ← 现成的服务器事件
```

A3 要去掉全局锁并加 `HasPendingCast` 检查（README 7.1），A4 要把 `ResolveCastVariant` 改成统一反噬。缘起的触发点就是 `ExecuteServer` 之后，这一点不受 A3 / A4 影响。

### 1.2 每个技能“生效中”的载体

技能 = `SkillAction` 子类（ScriptableObject，数据 + 三段执行）+ 运行时效果组件 + 预测修饰。缘起要能“中途消耗”，先要知道每个技能的剩余效果活在哪里、怎么结束、现在有没有提前结束的入口：

| 技能 | 服务器侧的生效载体 | 怎么结束 | 现有的提前结束入口 |
|---|---|---|---|
| 神足通 `acceleration` | 预测路径：`BuddahPredictedMotor._modifierState.AccelUntilTick`（服务器 + owner 各一份，TargetRpc 下发）；legacy 路径：owner 上的 `MovementAccelerationEffect` | tick 到期 | **没有按技能的取消**：马达只有 `ResetActiveSkillModifiers` 全清；legacy 有 `CancelAndRestore` |
| 禅定缓流 `slowtrap` | 施法者身上的 `MovementSlowTrapZoneEffect`（`Update` 用 `Time.deltaTime` 计时），触发体预制件挂在施法者下面；命中者通过 `target.ApplyAccelerationToOwner(负值)` 吃减速 | 组件销毁 | 复位时 `Destroy(trap)`；**区域特效 `PlayVfxAll` 播出去就停不下来**（`SkillVfxReplicator` 没有停止 RPC） |
| 法相金身 `giant` | 马达 `ScaleUntilTick / ScaleMultiplier / ScaleMassMultiplier / ScaleForwardForceMultiplier`；视觉与碰撞体尺寸由各端的 `PlayerScaleEffect` 自己计时（`CurrentScaleMultiplier` 是实时体型） | tick 到期 / 计时 | `PlayerScaleEffect.CancelAndRestore`（瞬间恢复，无缩回过渡）；马达无单项取消 |
| 如来神掌 `push_projectile_hands` | `BuddahHandControl._projectilePushModeUntilServer`（`Time.time`） | 到期 | `ResetSkillInputAndEffects` 全清 |
| 一念光明 `blackcurtain` | 不在施法者身上：每个 racer 的 `SkillPerceptionState.CurtainUntilTick`（服务器）+ 各端 `BlackCurtainViewController` | 计时 | 无 |
| 逆姻缘 `reverseturn` | 在受害者身上：各受害者马达的 `InvertTurnUntilTick` | tick 到期 | 无 |

### 1.3 预测修饰的边界

运动效果的唯一合法入口是修饰命令：`SkillExecutor.ApplyXToOwner` → `OwnerMovementEffectApplier` → `BuddahPredictionSkillMovementBridge` → `BuddahPredictionModifierBridge.TryApply` → `BuddahPredictedMotor.TryApplyModifierCommand`，服务器执行一份，再 TargetRpc 让 owner 执行一份（AI 与木桩没有 owner，只有服务器那份）。马达把命令写成 `BuddahPredictedModifierState` 里的 `*UntilTick` 期限，每 tick 由 `BuddahPredictedModifierResolver.Resolve` 折算成最终推力、限速、转向符号、`IsRooted` 等。

现在有 8 条期限（Root、Accel、PostRootAccel、Scale、InvertTurn、PushGrace、SuppressSteering、RoomBypass）。所有期限随 reconcile 快照下发，纯客户端由 `BuddahTickMath.ModifiersToLocal` 逐条平移到本地时钟。因此**每加一条期限要改 7 处**（见附录 A），并把 `PredictionProtocol.Version` 加一。这是加 Freeze、Burst 这类新效果的固定成本，不能绕。

马达里定身（`IsRooted`）的实际语义：`FinalForwardForce = FinalMaxSpeed = 0` → `ClampPlanarSpeed(0)` 把水平速度清零 → `CompleteStoppedPredictionStep` 提前返回，不施加任何力和转向扭矩。这正是禁锢要的底座。

### 1.4 清理路径

复活与重置会清掉进行中的技能：真人走 `ResetActiveSkillEffectsForOwner`（owner 端取消四种 legacy 效果、拖尾、相机），AI 走 `ResetServerAISkillState`（取消待确认施法、清神掌 buff、清感知、`ResetActiveSkillModifiers`、销毁缓流区域），马达的 teleport 事件在 `ResetModifiers` 时也会回调 `ResetActiveSkillEffectsForOwner`。缘起的状态板与进行中的缘起实例必须挂进这两条路径，否则复活后会留下“半触发”的状态。

### 1.5 已经存在、可以直接用的部件

- `SteeringSuppression` 修饰：转向输入清零（开场交接在用）。
- `BuddahPredictionCombatAdapter.TryRouteImpulse(victim, impulse, torque, sourceType, source)`：服务器权威冲量，owner 远端时自动 TargetRpc。#4 的突进与撞击冲量都走它。
- `BuddahHandControl.GetPushScaleMultiplier()` 读 `PlayerScaleEffect.CurrentScaleMultiplier`：普通推掌 hitbox、蓄力弹碰撞体与出生点**已经随体型缩放**，只有冲量不随（`impulse = dir * pushImpulseStrength`、`impulse = outwardDir * projectileImpulseStrength`）。所以 #1 金身巨掌在代码里已经有一半（见 5.2 与 Q11）。
- `RacerDirectory.Current.All`：本局所有已登记的 racer（一念光明用它找受害者）。
- `SkillCombatEvents.Record(stage, kind, source, target, position)`：诊断证据钩子，缘起触发也应该记一条。
- `SkillExecutor.ServerCastChanged`：服务器施法事件，AI 已在订阅。
- `TimedVfxInstance`、`SkillVfxTag.VfxId`：特效实例上带 id，补一个“按 id 停止”的 RPC 就能让消耗的技能把特效收掉。

## 2. 新代码怎么写

### 2.1 目录与命名（建议）

沿用“一个技能一个子目录、类名 `Skill_X`、资产 `Skill_X.asset`”的现有习惯，缘起平行放一套：

```text
Assets/Scripts/Buddah/ComboSkill/
  Yuanqi/
    SkillStateBoard.cs            每个 Buddah 一个，服务器权威，技能登记“我在生效”
    SkillStateBoardCore.cs        纯 C# 的登记 / 匹配逻辑，无 Unity 依赖，供 EditMode 测试
    ISkillRuntimeHandle.cs        技能兼容接口：Consume（消耗）与 IsActive
    YuanqiAction.cs               缘起模块基类（ScriptableObject，像 SkillAction）
    YuanqiRegistry.cs             每个 Buddah 一个：开局登记本配装涉及的对、订阅状态板、驱动缘起实例
    YuanqiContext.cs              触发时交给缘起模块的上下文（施法者、两条登记项、两个句柄、tick）
    YuanqiTargeting.cs            静态目标扫描（半径内的所有 Buddah，排除施法者）
    YuanqiPresenter.cs            NetworkBehaviour：把缘起的表现事件推给观察者
    Yuanqi_FrostLock.cs           #2 禅足冰封
    Yuanqi_GiantCharge.cs         #4 金足破阵
    Yuanqi_GiantPalm.cs           #1 金身巨掌
Assets/Skills_Assets/
  Yuanqi/
    Yuanqi_FrostLock.asset  Yuanqi_GiantCharge.asset  Yuanqi_GiantPalm.asset
```

命名：缘起 id 与技能 id 同风格（全小写 snake_case）：`frost_lock`、`giant_charge`、`giant_palm`；文案 key `yuanqi.frost_lock.name / .desc`；日志前缀 `[Yuanqi]`；命名空间与现有 ComboSkill 代码一致（全局命名空间，靠 `Yuanqi` 前缀区分）。英文名只用于代码与表，玩家看到的名字走 `UI/Text`。

### 2.2 五个部件与各自唯一的职责

| 部件 | 职责 | 不做什么 |
|---|---|---|
| **状态板 `SkillStateBoard`** | 记录本 Buddah 上正在生效的技能：`skillId`、槽位、开始 tick、名义结束 tick、句柄、实时参数（金身的当前体型）。提供 `Begin / End / IsActive / TryGet` 与 `Activated / Ended` 两个事件。服务器独有，不同步 | 不判断缘起，不知道“对”；不做每帧轮询（结束靠期限到点与句柄回调） |
| **开局登记 `YuanqiRegistry`** | 槽位定下后，从 `SkillDatabase` 的缘起列表里挑出两招都在本配装里的对，为每对建一个运行时实例（`Instantiate(so)`），订阅状态板 `Activated`；某招生效时只查“已登记且含这招”的对，另一招也在生效就触发；`Ended` 时通知持续型实例撤掉改写 | 不知道任何一对的效果内容 |
| **缘起模块 `YuanqiAction` 子类** | 一对的效果：`OnTriggerServer(ctx)`，持续型再实现 `OnEndServer(ctx)`。通过句柄消耗技能、通过 `SkillExecutor.ApplyXToOwner` 系列下发运动效果、通过 `YuanqiPresenter` 下发表现 | 不引用任何 `Skill_*` 类，不直接改刚体 / transform / `BuddahMovement`，不自己发 RPC |
| **技能兼容接口 `ISkillRuntimeHandle`** | 每个技能对外的两种能力：**消耗**（`Consume(reason)`：提前结束剩余效果并清理修饰、特效、相机）与**参数改写**（按需要的小接口，如 `IHandsImpulseScale`）。句柄由技能自己创建、自己实现 | 缘起模块只碰接口，不碰组件 |
| **缘起表现 `YuanqiPresenter`** | 一个 ObserversRpc 一种画面：`PlayFrostLock(center, radius, targetIds, seconds)`…… 观察者按 `VfxRegistry` 的 id 播地面结冰、给受害者挂冰块 | 不改游戏状态 |

### 2.3 依赖方向（硬规则）

```text
Skill_* / 技能运行时组件 ──写──► SkillStateBoard ──事件──► YuanqiRegistry ──► Yuanqi_* (YuanqiAction)
       │ 实现                                                                        │ 只经由
       ▼                                                                             ▼
ISkillRuntimeHandle ◄───────────────────────────────────────────────── ISkillRuntimeHandle.Consume
                                                                       SkillExecutor.ApplyXToOwner（修饰命令）
                                                                       YuanqiPresenter（表现 RPC）
```

- `Skill_*` 永远不引用 `Yuanqi_*`；`SkillExecutor` 不引用 `Yuanqi_*`，也不知道有哪些对。
- `Yuanqi_*` 之间互不引用；加一对缘起不改另一对。
- 运动效果只走修饰命令；画面只走观察者 RPC 或同步状态；判定只在服务器。
- 状态板只由技能写自己那一项：由 `SkillExecutor` 在 `ExecuteServer` 之后统一 `Begin`（见 2.4），其余字段（实时体型等）由技能的句柄提供，别人只读。

### 2.4 状态板的数据契约（建议）

```text
Entry {
  int    EntryId          本局内递增，“一次重叠只触发一次”靠它
  string SkillId          普通技能 id（反噬不登记：A4 之后反噬没有效果）
  int    Slot
  uint   StartTick        窗口开始 = 执行 tick + 开始偏移
  uint   UntilTick        窗口结束 = 开始 + 名义时长；句柄 Consume 后提前 End
  ISkillRuntimeHandle Handle
}
```

**谁写 Begin**：`SkillExecutor.ExecuteQueuedCastAfterDelay` 在 `executedSkill.ExecuteServer` 之后调用 `board.Begin(skillId, slot, window, handle)`，是一处通用代码，没有按技能的分支。窗口与句柄来自 `SkillAction` 新增的两个虚方法，默认实现覆盖大多数技能，特殊的自己覆盖：

| `SkillAction` 新增 | 默认实现 | 覆盖者 |
|---|---|---|
| `virtual ActiveWindow GetActiveWindow(SkillExecutor)` | 开始偏移 0，时长 = 配表 `Skill.effectDurationSeconds`（经 `SkillDatabase.TryGetSkillRecord`，无表时取 SO 主时长字段） | 一念光明：偏移 `expandDurationSeconds`、时长 `holdDurationSeconds`（README 已定：按保持期 10 秒算）；逆姻缘：默认即可（施法者窗口 = 反转时长） |
| `virtual ISkillRuntimeHandle CreateRuntimeHandle(SkillExecutor)` | `null`（不能被消耗） | 神足通、缓流、金身、神掌各返回自己的句柄 |

**谁写 End**：期限到点由状态板自己在 `OnTick`（订阅 `TimeManager.OnTick`，与 `AISkillCaster` 同款）结束；句柄 `Consume` 成功后由句柄回调 `board.End(entryId)`；复位时 `board.Clear()`。`Ended` 事件携带 entry，持续型缘起据此撤改写。

窗口口径（已定规则的代码落点）：

| 技能 | 窗口 | 来源 |
|---|---|---|
| 神足通 / 缓流 / 金身 / 神掌 | 执行起，`effectDurationSeconds` | 表 |
| 一念光明 | 执行 + 展开 1 秒起，保持 10 秒 | 表 + SO 的 `expandDurationSeconds` |
| 逆姻缘 | 执行起 10 秒 | 表 |

实现落点（2026-10-26，wave 2 实现采用）：

- **一招一条生效项**：同一招在旧 entry 还生效时再次施放，状态板先以原因 `superseded` 结束旧 entry（照常触发一次 `Ended`），再登记新 entry。神掌的 15 秒 buff 比 12 秒冷却长，没有这条，旧 entry 到期会在新一次施放仍生效时撤掉 #1 的冲量改写。
- **时长兜底**：`SkillExecutor.ResolveEffectDurationSeconds` 先取表 `effectDurationSeconds`，无表行时取 SO 主时长字段，都没有就是 0（0 = 不登记），与上文一致。
- **比赛中换配装**：只有诊断工具会这样做；`SkillLoadout.ServerSlotsChanged` 触发时先 `board.Clear()`，再按新配装重新登记缘起。

### 2.5 技能兼容接口与各技能的消耗语义（建议）

```csharp
public interface ISkillRuntimeHandle
{
    string SkillId { get; }
    bool IsActive { get; }
    void Consume(string reason);   // 服务器调用；幂等
}
```

| 技能 | 句柄实现 | `Consume` 做什么 |
|---|---|---|
| 神足通 | 新类 `AccelerationRuntimeHandle(caster)` | `caster.CancelAccelerationForOwner()`：服务器发新修饰命令 `EndModifier(Acceleration)`（把 `AccelUntilTick` 设为当前 tick）+ TargetRpc 给 owner 同样执行；legacy 路径 `MovementAccelerationEffect.CancelAndRestore`；清拖尾 `ClearTrail`；相机 FOV 提前进入 settle（`SkillCameraFov` 加“提前收尾”入口） |
| 禅定缓流 | `MovementSlowTrapZoneEffect` 自己实现接口 | `Destroy(this)`（触发体随 `OnDisable` 销毁）+ `SkillVfxReplicator.StopVfxAll(vfxId)`（新增 ObserversRpc，按 `SkillVfxTag.VfxId` 找到实例停止） |
| 法相金身 | 新类 `GiantRuntimeHandle(caster)`，并暴露 `CurrentScale`（读 `PlayerScaleEffect.CurrentScaleMultiplier`） | `caster.CancelScaleForOwner()`：`EndModifier(Scale)` + TargetRpc；各端 `PlayerScaleEffect` 走正常缩回过渡（新增 `EndEarly()`，不是瞬间 `CancelAndRestore`） |
| 如来神掌 | `BuddahHandControl` 实现接口；另实现 `IHandsImpulseScale { float ImpulseMultiplier set; }` 供 #1 改写 | `ResetProjectilePushModeServer()`（只收 buff，不碰输入状态） |
| 一念光明 / 逆姻缘 | 暂不提供句柄（没有任何已定缘起会消耗它们） | — |

`EndModifier(type)` 是一条新的修饰命令类型（`ValueA` = 要结束的类型），只把对应期限设为当前 tick，不碰其他期限。它和 `Freeze` 一样要走附录 A 的 7 处改动。

实现落点（2026-10-26，wave 2）：缓流的特效不是在施法者的子物体里按 `SkillVfxTag.VfxId` 搜索（区域特效跟随目标、不挂在施法者下面，搜不到），而是由 `SkillVfxReplicator` 记下自己生成的每个实例（有无父物体都记）；`StopVfxAll(vfxId, linger)` 走新增的 `StopVfxAllObserversRpc`，各端对匹配的实例调用 `TimedVfxInstance.StopNow(linger)`：立即停止发射，粒子淡出后销毁。

- **缓流区域的所有结束方式都停特效**：到期、缘起消耗、服务器控制复位（AI 重生、AI 完赛）都走 `MovementSlowTrapZoneEffect` 的同一个结束入口，停掉区域特效，淡出时长用 SO 的 `zoneEndVfxLingerSeconds`（默认 1 秒）。SO 的 `vfxDurationSeconds` = 11 是区域 10 秒时期留下的旧值，表里区域已经是 6 秒（`SkillPresentation` 同为 11，归 design-tables A9，B 阶段再改）。现在它只是特效时长的上限，画出来的区域不会比实际区域和状态板 entry 活得更久。服务器控制复位原来直接 `Destroy(trap)`，现在改成 `trap.Consume(...)`。
- **神足通的消耗与共享槽**：预测路径下 `EndModifier(Acceleration)` 只在加成为正时结束，所以消耗不会结束施法者身上的缓流减速。legacy 路径（预测关闭）的 `CancelAndRestore` 会连同合并进去的减速一起清掉。真正的边界情况（**待用户定**）：施法者先放神足通，之后敌人的缓流命中，减速覆盖了共享槽的加成（后写者生效，期限取两者较晚的一个）。这时 #2 或 #4 消耗神足通，对移动没有任何作用：施法者一直减速到较晚的期限，往往就是神足通原本的期限；#4 的冲刺从减速后的上限起步（80 − 20 + 60 = 120，而不是 140）。

### 2.6 `SkillExecutor` 的改动面（刻意收窄）

只加同类方法，不加缘起逻辑：

- `ApplyFreezeToOwner(seconds)`、`CancelAccelerationForOwner()`、`CancelScaleForOwner()`，与现有 `ApplyRootThenAccelerationToOwner` 同构（服务器桥接 + owner TargetRpc），实现放在 `OwnerMovementEffectApplier`。
- `TryResolveCast` 增加动作锁：`motor.CurrentComputedStats.IsFrozen` 为真时拒绝（和冷却一样返回 false）。
- `ApplyFreezeToOwner` 在下发修饰前先 `CancelPendingCastServer()`：正在前摇的施法被打断（已定，Q10）。执念在执行时才入账，所以不用退；槽冷却在 `QueueCast` 已写，建议一并退还（`_nextReadyTime[slot] = now`），被打断的施法不算放过。
- `ExecuteQueuedCastAfterDelay` 末尾一行 `board.Begin(...)`。
- 复位路径里加 `board.Clear()` 与 `registry.ResetServer()`。

`BuddahHandControl.TryPushServer` 增加同样的动作锁检查（现在没有任何定身检查，是事实缺口）。

### 2.7 网络与预测规则（新效果一律照此）

| 效果类型 | 做法 |
|---|---|
| 作用于运动（禁锢、突进、取消加速） | 新修饰命令，服务器 + owner 各执行一份，期限进 reconcile 快照；附录 A 的 7 处 + 协议版本 +1。阶段 A 接受多次 +1（已定，Q13），但每条命令的 7 处改动与版本号必须在同一个提交里齐备，History 注释写明加了什么，不留半截命令 |
| 一次性冲量（撞击弹开） | `BuddahPredictionCombatAdapter.TryRouteImpulse`，不新建通道 |
| 只影响画面（地面结冰、冰块、巨掌尺寸） | `YuanqiPresenter` 的 ObserversRpc；受害者自己 HUD 上的“冻结剩余时间”从本地预测到的 `IsFrozen` 期限读，不另发 |
| 状态板 | 不同步。客户端需要知道的（本配装有哪些缘起）只依赖已同步的 `SkillLoadout.SlotSkillIds` + 本地 `SkillDatabase` 即可算出 |

### 2.8 测试策略（建议）

- `SkillStateBoardCore`、`YuanqiPairTable`（登记 / 匹配 / 一次重叠只触发一次 / 任一招结束后重新重叠可再触发）写成纯 C#，进 `BuddahGo.Tests`（friend assembly 现有做法）。
- `BuddahPredictedModifierResolver` 的 Freeze 分支、`BuddahTickMath.ModifiersToLocal` 新增期限的平移，各补一条测试（已有 `BuddahTickMathTests` 可照抄）。
- 场景级验证沿用 README 7.3 的训练模式清单；`AISkillSceneTests` 是现成的场景测试样板。
- 表接通后，`DesignTableRuntimeTests` 用反射核对新映射的目标字段存在、产物值等于单元格（决定 23）。

### 2.9 整洁守则（提交前自检）

- 加一对缘起 = 一个 `Yuanqi_*` 类 + 一个 `.asset` + （阶段 B）表行与文案。碰到 `SkillExecutor`、其他 `Yuanqi_*` 或任何 `Skill_*` 就说明边界画错了，先回头改接口。
- 新数字先是 SO 上的 `[SerializeField]` 带默认值（README 7.0 建议），不写字面量在方法里。
- 新修饰命令按附录 A 一次改齐，并在 `Movement/ModifierRule`（presentationOnly）与 `Global/__enums__.ModifierCommandType` 登记。
- 每条新的服务器效果都 `SkillCombatEvents.Record("yuanqi-trigger", yuanqiId, caster, target, pos)`。
- A4 退役 `*_anti` 之后，删除死代码：六个 `Skill_*_Anti` 类与资产、`FireChargedProjectileBurstReplicated`、`PlayChargedBurstVisuals*`、`ResolveAntiSkillId` 的后缀推断；留下 `MovementRootThenAccelerationEffect` 作为 legacy 路径的禁锢。
- 日志走 `GameLog.Verbose` 并带 `[Yuanqi]` 前缀；不在表现层写游戏状态。

## 3. 配表怎么变

### 3.1 原则

- 阶段 A 不改任何表头与 sheet；缘起参数在 `Yuanqi_*` SO 上（README 7.0 建议）。阶段 B 再按本节接表，SO 字段退为无表行时的兜底（决定 14：覆盖而非重构）。
- 结构沿用 v2 原则“一概念一表”：一张 `Yuanqi` 宽表放身份与收尾规则，一对一张强类型家族表放参数，和 `Skill` + `SkillEffect<家族>` 完全同构（决定 16、20）。
- 下面所有表与列都是**建议**，按决定 4 入表前先确认；表里每一列都要有读取方（决定 22），没有参数的对不建家族表。
- 字段名按本节预留（已定，Q14），表结构要同时满足可延展与可读：新的一对只加一行 `Yuanqi` 与一张家族表，不在宽表上堆各对的专属列；家族表以对命名，`##comment` 写明每列对应 yuanqi.md 里的哪个字母参数（N、M、R）。

### 3.2 新 sheet：`Skill/Yuanqi`

| `##var` | `##type` | 说明 |
|---|---|---|
| `yuanqiId` | `enum:YuanqiId#key` | 名单在 `Global/__enums__`，与 SO 的 `yuanqiId` 一致 |
| `pairNumber` | `int` | yuanqi.md 的固定编号 #1–#15，只为对照 |
| `displayNameKey` | `string#ref=UI/Text.key` | `yuanqi.frost_lock.name` |
| `descriptionKey` | `string#ref=UI/Text.key` | 选技能界面显示 |
| `skillA` | `enum:SkillId#ref=Skill.skillId` | 两招无序；校验保证同一对不重复登记 |
| `skillB` | `enum:SkillId#ref=Skill.skillId` | |
| `consumeA` | `bool` | 触发后 A 是否被消耗（#2 两个 true；#4 神足通 true、金身 false；#1 都 false） |
| `consumeB` | `bool` | |
| `mode` | `enum:YuanqiMode` | `Instant`（触发一次）/ `Sustained`（两招都生效期间持续） |
| `retriggerPolicy` | `enum:YuanqiRetrigger` | `OncePerOverlap`（README 5.1 建议）；留枚举以便以后改 |
| `isEnabled` | `bool` | 临时关掉一对而不删行 |

tags：`skill, info`。`Skill` 表自身不加列：缘起窗口用的就是现有 `effectDurationSeconds`。

### 3.3 家族表：`Skill/YuanqiEffect<对>`

| sheet | 列 | 对应 |
|---|---|---|
| `YuanqiEffectFrostLock` | `yuanqiId#key#ref=Yuanqi.yuanqiId`、`freezeSeconds`（N）、`radiusMeters`、`affectsTrainingDummies`（bool，默认 true） | #2 |
| `YuanqiEffectGiantCharge` | `yuanqiId`、`burstExtraMaxSpeed`（限速增量，默认 60）、`burstSeconds`（回落秒数，默认 0.8）、`impactImpulseMultiplier`（M，基准 `Push.basic.impulseStrength`）。不设曲线列：马达的回落形状固定为 增量 × 剩余比例²，两端必须一致 | #4 |
| （#1 金身巨掌） | 无参数，不建表；`Yuanqi` 行已经足够 | #1 |

tags：`skill, balance`。

### 3.4 配套改动

| 位置 | 改动 |
|---|---|
| `Global/__enums__` | 新枚举 `YuanqiId`（`frost_lock`、`giant_charge`、`giant_palm`）、`YuanqiMode`、`YuanqiRetrigger`；`ModifierCommandType` 追加 `Freeze`、`EndModifier`、`Burst` |
| `Movement/ModifierRule`（presentationOnly） | 追加 `Freeze`、`Burst` 两行，写清 ValueA/B 语义与 `resetOnSkillReset=true` |
| `UI/Text` | `yuanqi.<id>.name / .desc`；中文新字符出现时 SoloCJK 字体子集重烘（design-tables README 已记） |
| `scope.json` | `export.Skill` 追加三张 sheet 与 tags |
| `Tools/design-tables/gen_records.py` | 不改脚本，重跑即生成 `YuanqiRecord`、`YuanqiEffectFrostLockRecord`、`YuanqiEffectGiantChargeRecord` 与 `ProjectConfigDatabase.yuanqis` 等列表 |
| `Docs/design-tables/inventory-skills.md`、`excel-spec.md` §5 / §6.3 | 补表目录与消费方 |

### 3.5 映射（消费方接通）

把缘起列表放进现有 `SkillDatabase`（`public List<YuanqiAction> yuanqis`），而不是另建数据库。理由：复用 `EnsureDesignTablesApplied` 的“每局 Play 首次查表时写入 SO、编辑器退出 Play 恢复资产”（决定 24），避免再出现一次 10 秒对 3 秒那种时机 bug。

| 表 | 目标 | 方式 |
|---|---|---|
| `Yuanqi` 行 | `Yuanqi_*` SO 的 `skillA / skillB / consumeA / consumeB / mode / retriggerPolicy / isEnabled` | `DesignTableOverlay.ApplyMapped`（显式列出，防止同名误写；决定 21） |
| `YuanqiEffect<对>` 行 | 同名字段 | `DesignTableOverlay.Apply`（与 `ApplyFamily` 同款） |
| 文案 | `SkillDatabase.GetResolvedYuanqiName(id)` → `DesignTableOverlay.Text(key, so.displayName)` | 选技能界面与 HUD 用 |

`YuanqiRegistry` 开局登记时只读 SO（已被表覆盖），不直接读表，和 `SkillExecutor` 读 `SkillAction` 一个道理。

### 3.6 校验规则（`DesignTableDomainRules`，建议）

- `skillA != skillB`；两招都是普通技能（不是 `_anti`，A4 之后 `_anti` 行本来就删了）。
- 无序唯一：`{skillA, skillB}` 作为集合在表内唯一。
- `consumeA / consumeB` 至少有定义（都 false 合法 = 两招照常持续）；`mode = Sustained` 时两者必须都 false（持续型没有“被消耗”的说法）。
- 每张家族表的 `yuanqiId` 必须存在于 `Yuanqi`，且对应 SO 的类名与家族匹配（像 `EffectDurationField` 那样按类型表驱动）。
- 设计约束“避免三招两两互有效果”：对任何三个普通技能，若三对都在表里且 `isEnabled`，报 Warning（不是 Error，留给设计判断）。

### 3.7 阶段 A 期间与其他步骤共享的表变化

README 7.2 已经列了 A3（删 `castLockSeconds`）、A4（删 `_anti` 行与列、`Obsession` 五个新列）、A7（删 `ComboBinding / ComboInput`、AI 人格表加缘起决策参数）的表改动，本文不重复。缘起对它们只有一个要求：`Skill.effectDurationSeconds` 继续是每个普通技能的主效果时长，状态板窗口靠它。

## 4. 禅足冰封（#2）的具体构筑

### 4.1 规格（摘自 yuanqi.md，已定）

神足通与禅定缓流重叠的一瞬间触发；以施法者为中心、半径 R 的圆形范围内，施法者以外的所有人（真人、AI、木桩）禁锢 N 秒：速度瞬间归零、不保留转向、不能放技能、不能推掌；一次性生成；触发后两招都被消耗。画面：地面瞬间结冰，受影响者冻成冰块，结束恢复。冰块本身的碰撞效果以后再做。

### 4.2 时序

```text
[先放缓流，后放神足通]（更常见：缓流 6 秒，神足通 3 秒，重叠窗口 5 秒）
t0   缓流 ExecuteServer → board.Begin(slowtrap, until t0+6, handle=zone)
     registry: Activated(slowtrap) → 含缓流的对 {#2} → 另一招 acceleration 不在板上 → 不触发
t1   神足通请求（1 秒前摇，须 t1 ≤ t0+5）
t1+1 神足通 ExecuteServer → 加速修饰下发 → board.Begin(acceleration, until t1+4, handle=accel)
     registry: Activated(acceleration) → {#2} → slowtrap 在板上 → 触发
       Yuanqi_FrostLock.OnTriggerServer(ctx):
         1. 扫描半径 R 内的其他 Buddah
         2. 对每个目标 target.ApplyFreezeToOwner(N)
         3. presenter.PlayFrostLock(center, R, targetIds, N)
         4. ctx.HandleA.Consume("frost_lock")   // 加速结束、拖尾、FOV
            ctx.HandleB.Consume("frost_lock")   // 区域销毁、特效停止
         5. SkillCombatEvents.Record(...)
     board: 两条 Ended → registry 把 #2 的实例标记为“本次重叠已用”
[先放神足通，后放缓流] 完全对称，只是重叠窗口只有 2 秒（3 秒 − 1 秒前摇）
```

神足通那 1 帧的加速会被当场消耗，这是规则决定的（yuanqi.md 收尾条目），不是 bug。

### 4.3 组件清单

| 文件 | 类 | 新 / 改 | 职责 |
|---|---|---|---|
| `Yuanqi/Yuanqi_FrostLock.cs` | `Yuanqi_FrostLock : YuanqiAction` | 新 | 字段 `freezeSeconds`、`radiusMeters`、`affectsTrainingDummies`、表现 id；`OnTriggerServer` 如 4.2 |
| `Yuanqi/YuanqiTargeting.cs` | 静态 | 新 | `CollectOthersWithin(caster, radius, includeDummies, buffer)`：遍历所有 `SkillExecutor`（逆姻缘的找法，木桩也带 `SkillExecutor`，A1 已定要保留），水平距离 ≤ R，排除施法者 |
| `Yuanqi/YuanqiPresenter.cs` | `YuanqiPresenter : NetworkBehaviour` | 新 | `PlayFrostLockServer(...)` → ObserversRpc：地面结冰特效按 `VfxRegistry` id 在 `center` 生成（世界坐标，不跟随），每个目标挂冰块预制件 N 秒（`TimedVfxInstance`） |
| `ComboSkill/SkillExecutor.cs` | — | 改 | `ApplyFreezeToOwner(seconds)` + `ApplyFreezeTargetRpc`；`TryResolveCast` 动作锁 |
| `ComboSkill/OwnerMovementEffectApplier.cs` | — | 改 | `ApplyFreezeServer / ApplyFreezeOwner`：预测路径发 `Freeze` 命令；legacy 路径复用 `MovementRootThenAccelerationEffect.ApplyOrRestart(move, N, 0, 0, 0.05)` |
| `Buddah/BuddahHandControl.cs` | — | 改 | `TryPushServer` 开头：`IsFrozen` 拒绝；实现 `ISkillRuntimeHandle`（给神掌用，与本对无关但同一批接口） |
| `New_Buddah/Core/BuddahPredictedModifierState.cs` 等 7 处 | — | 改 | 新期限 `FreezeUntilTick`，命令 `Freeze`（ValueA=秒）与 `EndModifier`（ValueA=类型），见附录 A |
| `New_Buddah/Core/BuddahPredictedModifierResolver.cs` | — | 改 | Freeze 生效时：`IsRooted = true`、`FinalTurnTorque = 0`、`IsSteeringSuppressed = true`、新增 `IsFrozen = true` |
| `New_Buddah/Core/BuddahPredictedMotor.Events.cs` | — | 改 | `TryApplyModifierCommand` 的 `Freeze` 分支在写期限后立即 `SetPredictionVelocitiesSafely(0, 0)`；`ResetActiveSkillModifiers` 清 `FreezeUntilTick` |
| `Network/Gameplay/SkillVfxReplicator.cs` | — | 改 | `StopVfxAll(vfxId)` ObserversRpc（缓流的消耗要用） |
| `ComboSkill/Skill_Acceleration/…`、`Skill_SlowTrap/…` | — | 改 | 各自 `CreateRuntimeHandle`；`MovementSlowTrapZoneEffect` 实现接口 |
| `SteamMultiplayer.Network.PredictionProtocol` | — | 改 | `Version = 2` |

### 4.4 触发判定细节

- 匹配只发生在 `Activated` 事件里，查的是“本 racer 已登记、且含这招”的对（配装里没有的组合连实例都没有，自然不检测）。
- “一次重叠只触发一次”（README 5.1 建议）：缘起实例记住上次触发用的 `(EntryIdA, EntryIdB)`；同一组 entry 不再触发；任一招结束后重新施放会产生新的 entry id，就能再触发。#2 两招都被消耗，所以这条规则对它天然成立，但状态板仍统一实现。
- 被消耗的技能不返还冷却与执念（README 5.1 建议）：冷却在 `QueueCast` 已写、执念在执行时已加，`Consume` 不碰它们，无需额外代码。
- 施法者自己此刻被别人冻住：不影响触发（触发在第二招 `ExecuteServer` 之后，而施法已被动作锁拦在前面，所以实际不会出现“冻住还能施法”）。

### 4.5 禁锢的实现

**服务器侧**。`target.ApplyFreezeToOwner(N)`：

1. `MovementEffects.ApplyFreezeServer(N)` → 修饰命令 `Freeze`（服务器马达那份）。
2. 目标有 owner（远端真人）→ `ApplyFreezeTargetRpc(conn, N)` → owner 马达执行同一条命令。AI、木桩、host 自己没有 owner 连接或 owner 就是 host，只执行第 1 步，与现有 `ApplyRootThenAccelerationToOwner` 行为一致。

**马达侧**。`Freeze` 复用定身的底座再加三件事：

| 规格 | 落点 |
|---|---|
| 速度瞬间归零 | 写期限的同一帧 `SetPredictionVelocitiesSafely(0, 0)`；之后每 tick `ClampPlanarSpeed(0)` 与 `CompleteStoppedPredictionStep` 保持不动（这就是定身的现有行为） |
| 转向也不保留 | 角速度随上一条归零；`FinalTurnTorque = 0` 与 `IsSteeringSuppressed` 保证不再有扭矩 |
| 不能放技能 | `SkillExecutor.TryResolveCast` 读 `motor.CurrentComputedStats.IsFrozen` 拒绝请求；冻结下发时 `CancelPendingCastServer()` 打断正在前摇的施法（已定，Q10） |
| 不能推掌 | `BuddahHandControl.TryPushServer` 同上；神掌正在蓄力的那一下也随 `_inputGeneration` 失效 |
| 冻结中受到冲量（被神掌打、被金身撞） | 推不动（已定，Q12）：`ClampPlanarSpeed(0)` 在下一 tick 清掉冲量，零代码；“冰块能被推走”留到冰块效果 |

`IsRooted` 在冻结期间保持为 true，这样 AI 的 `CanContinueCast` 注释里那条“定身不取消已接受的施法”、表现层的 `_presentationWasRooted` 等既有消费者都不用改。`IsFrozen` 是额外的标志，只给动作锁与 HUD 用。

**owner 侧 HUD**。owner 本地的修饰状态里就有 `FreezeUntilTick`（TargetRpc 写入，reconcile 快照维持），冻结倒计时直接从 `CurrentComputedStats` 读，不另发同步。

**legacy 路径**。`MovementRootThenAccelerationEffect` 已有定身 + `SetSkillRooted` 标志，给 0 加速即可当禁锢；legacy 只是预测关闭时的兜底，不单独新建组件。

**木桩（A1）**。木桩需要 `SkillExecutor` 才能被 `YuanqiTargeting` 扫到，和被逆姻缘找到是同一个理由；它没有 owner，走服务器单份路径。A1 的“头顶读数”可以直接显示 `IsFrozen` 的剩余 tick。

### 4.6 消耗两招

顺序固定：先下发禁锢与表现，再消耗（这样即使消耗过程抛错，核心效果已经生效）。

| 句柄 | 服务器 | owner / 观察者 |
|---|---|---|
| 神足通 `AccelerationRuntimeHandle.Consume` | `EndModifier(Acceleration)` 命令 → `AccelUntilTick = now` | TargetRpc 同样执行；legacy `MovementAccelerationEffect.CancelAndRestore`；`ClearTrail`；FOV 提前 settle |
| 缓流 `MovementSlowTrapZoneEffect.Consume` | `Destroy(this)`（`OnDisable` 销毁触发体与调试体，清 `_nextApplyAt`） | `SkillVfxReplicator.StopVfxAll("slowtrap_vfx")` 停掉 11 秒的区域特效 |

两条 `Consume` 各自回调 `board.End(entryId)`，状态板发两次 `Ended`，`YuanqiRegistry` 据此结束本次重叠。

### 4.7 表现

| 画面 | 做法 | 资产 |
|---|---|---|
| 地面瞬间结冰 | `YuanqiPresenter.PlayFrostLock` ObserversRpc，在 `center` 生成世界坐标特效，寿命 = N + 余韵 | `VfxRegistry` 新 id `frost_lock_ground_vfx`，prefab 由美术做（范围线：表里只登记 id 与路径） |
| 受害者冻成冰块 | 同一条 RPC 带 `targetObjectIds[]`，每个观察者本地找到对象，挂冰块预制件 N 秒后销毁；结束不另发 RPC，靠寿命 | `VfxRegistry` id `frost_lock_block_vfx` |
| 冻结期间的 HUD | owner 自己从 `IsFrozen` 倒计时；A6 的 `SkillSlotUI` 在冻结时显示锁 | — |
| 触发提示 | A6 范围：施法者 HUD 弹缘起名（`yuanqi.frost_lock.name`） | — |

### 4.8 参数与阶段 A 默认值

| 字段 | 位置 | 阶段 A 占位值 | 备注 |
|---|---|---|---|
| `freezeSeconds`（N） | `Yuanqi_FrostLock` SO | 占位，试玩定 | 控制叠加风险见 README §9，建议先取短的 |
| `radiusMeters`（R） | 同上 | 占位，试玩定 | 缓流区域半径现值 7.5 米可作参照（“较大圆形范围”应大于它） |
| `affectsTrainingDummies` | 同上 | true | 训练模式验证要靠它 |
| 表现 id 与偏移 | 同上 | — | 范围线：表现细节不进平衡表 |

yuanqi.md 约定本阶段不定具体数字，这里不写数值；进表后这些字段退为兜底。

### 4.9 清理与边界

- 复活 / 重置：`ResetActiveSkillModifiers` 清 `FreezeUntilTick`；`board.Clear()`；`registry.ResetServer()` 把每个实例的“上次触发”清零。木桩的服务器侧清理（A1）同样调用。
- 比赛结束 / 结算区：`ResultAreaInteractionGate` 已挡施法与推掌；冻结到点自然解除，不需要特殊处理。
- 两次禁锢叠加同一个受害者：期限取 `Max`（与其他修饰一致），不累加。这是 README §9 “受害者保护”待设计的一部分，先按最简单的来。
- 开场交接期间不会有施法，禁锢不会出现在 handoff 阶段；但 `IsFrozen` 与 `SuppressSteering` 共存时语义相同，没有冲突。
- 纯客户端 reconcile：`FreezeUntilTick` 由 `ModifiersToLocal` 平移；`HandoffClockTests` 的思路可以直接套用一条测试。

### 4.10 验证清单

训练模式（README 7.3 第一级）：

- 两种先后各一次：范围内的木桩在第二招执行的那一帧冻住，N 秒后恢复；范围外木桩不受影响。
- 冻结中的木桩头顶读数显示冻结；推它不动（Q12 定了再改）。
- 触发后施法者身上没有加速（拖尾、FOV 一起收掉），身后没有缓流区域，区域特效已停。
- 触发后再放神足通：没有缓流在板上，不触发；重新放缓流再放神足通，再次触发。
- 复活后状态板为空，缘起能正常再触发。

联机（第三级）：远端真人被冻：owner 端立即停下且 HUD 显示冻结；reconcile 不出现“解冻又冻回去”的抖动；两端画面一致；协议版本不一致的客户端被大厅拒绝。

### 4.11 实现顺序（#2 内部）

| 步 | 内容 | 完成标准 |
|---|---|---|
| F1 | 马达：`Freeze` + `EndModifier` 两条命令（附录 A 全部 7 处）、协议 +1、resolver 与 tickmath 测试 | EditMode 测试通过；训练模式用一个临时入口给自己下 Freeze，停下且解除 |
| F2 | `SkillExecutor.ApplyFreezeToOwner`、动作锁（施法 + 推掌）、legacy 兜底 | 冻结中按技能键和推掌都被拒绝 |
| F3 | 状态板 + `SkillAction` 两个虚方法 + 四个句柄 + `StopVfxAll` | 任一技能生效期间板上有 entry；手动调用 `Consume` 能收掉加速与缓流 |
| F4 | `YuanqiAction` 基类、`YuanqiRegistry` 开局登记、`SkillDatabase.yuanqis` | 配装含两招时登记出 #2 实例；不含时没有 |
| F5 | `Yuanqi_FrostLock` + `YuanqiTargeting` + `YuanqiPresenter` | 4.10 训练模式清单全过 |
| F6 | 阶段 B 接表（3.2–3.6） | `DesignTableTests` / `RuntimeTests` 通过，`validation.md` 无错误 |

README 7.1 A5 给的整体顺序是“状态板 → 登记 → #4 → #2 → #1”。若按本文先做 #2，F1–F4 同样是 #4、#1 的前置，只是 #4 还要再加 `Burst` 命令与撞击组件（5.1）。

## 5. 另两对的落点（概要）

### 5.1 #4 金足破阵

- **突进**：新修饰命令 `Burst`（ValueA = 初始额外限速或初速，ValueB = 回落秒数；曲线从 SO / 表读，两端配置一致即可确定性）。resolver 在 Burst 生效期间把 `FinalMaxSpeed` 加上按剩余比例衰减的增量；起步那一下的速度用 `TryRouteImpulse(self)` 或在命令里直接 `SetPredictionVelocitiesSafely` 给到目标速度（二选一，实现时定）。
- **撞击**：racer 上新组件 `BuddahBodyImpact`（服务器）监听 `OnCollisionEnter`，仅当本机 `IsBurstActive` 且对方是 Buddah 时，向对方 `TryRouteImpulse(M × Push.basic.impulseStrength)`，方向待 Q9；同一次突进每个受害者只记一次（`HashSet<int>` 随 Burst 开始清空）。
- **收尾**：`ctx.HandleA(acceleration).Consume()`；金身不动。`Yuanqi_GiantCharge` 不需要 `OnEndServer`。
- **句柄改写**：不需要改写金身参数，撞击倍率由缘起模块自己持有。

### 5.2 #1 金身巨掌

- **持续型**：`mode = Sustained`。`OnTriggerServer` 时给 `BuddahHandControl` 的 `IHandsImpulseScale.ImpulseMultiplier` 绑定一个“跟随当前体型”的来源（读 `GiantRuntimeHandle.CurrentScale`，不设上限）；`OnEndServer`（任一招 `Ended`）解绑，恢复 1。
- **事实与决定**：尺寸、出生点、碰撞体现在已经随体型缩放，和有没有缘起无关。保留这个联动（已定，Q11）；缘起触发时掌的一切都按体型等比例放大，缘起真正新增的是冲量倍率，`ImpulseMultiplier` 与尺寸用同一个 `CurrentScale`，不设上限。
- 没有参数，不建家族表。

## 6. 与 README 7.1 的对接

| 步 | 本文给它的要求 |
|---|---|
| A1 木桩 | 保留 `SkillExecutor`；服务器侧清理调用 `board.Clear()`；头顶读数可读 `IsFrozen` |
| A3 节奏 | 去掉全局锁后状态板上才可能同时有两条 entry；`HasPendingCast` 检查保证执念先入账再接受下一招 |
| A4 反噬 | 反噬不再有效果，也就不登记状态板；统一惩罚的“10 秒锁技能”和禁锢的动作锁应是同一个检查点（`TryResolveCast` 一处），避免两套锁 |
| A5 缘起 | 本文第 2、4、5 节 |
| A6 HUD | 冻结倒计时、缘起触发提示、选技能界面的“本配装有哪些缘起”（本地用 `SkillLoadout` + `SkillDatabase.yuanqis` 算） |
| A7 AI | `AISkillCaster` 直接调 `CastSlotServer` 之后，缘起决策读同一份 `SkillDatabase.yuanqis`；被冻住的 AI 不需要特殊处理（无扭矩、无施法） |

## 7. 已答复的问题

Q10–Q14 于 2026-10-25 答复，结论已写进对应章节；这里只留对照，便于追溯。

| # | 涉及 | 问题 | 答复（已定） | 落点 |
|---|---|---|---|---|
| Q10 | #2 | 受害者被冻住时，正在前摇的施法怎么办 | 直接取消，打断施法；该槽冷却退还（2026-10-25 补充，已定） | 2.6、4.5 |
| Q11 | #1 | 金身生效时推掌尺寸已随体型变大，与有无神掌无关，保留吗 | 保留；缘起触发时掌的一切等比例放大 | 5.2 |
| Q12 | #2 | 冻结中受到冲量是推不动还是被推走 | 推不动 | 4.5 |
| Q9 | #4 | 被撞者弹开的方向（yuanqi.md 的问题） | 沿碰撞方向往两边弹开：从施法者指向被撞者的水平方向 | 5.1 |
| Q13 | 全部 | 阶段 A 是否接受协议版本多次 +1 | 接受，但每条命令一次改齐，保证干净 | 2.7、附录 A |
| Q14 | 配表 | `Skill/Yuanqi` 与家族表结构是否按此建 | 预留字段名；表要可延展、可读 | 3.1–3.3 |

目前没有新的待确认项；新问题继续从 Q15 编号。

## 附录 A：新增一条修饰期限的改动清单（7 处 + 协议）

以 `Freeze` 为例，`EndModifier` 与 `Burst` 同样：

1. `BuddahPredictedModifierState`：加 `FreezeUntilTick`，`HasAnyActive` 加一项。
2. `BuddahPredictedModifierCommandType` / `BuddahPredictedModifierCommandData`：加枚举值与 `CreateFreeze(seconds, source)`。
3. `BuddahPredictedMotor.Events.TryApplyModifierCommand`：加分支；`ResetActiveSkillModifiers` 加清理。
4. `BuddahPredictedModifierResolver.Resolve`：加判定与输出（`IsFrozen`，并落到 `IsRooted / FinalTurnTorque / IsSteeringSuppressed`）；`BuddahPredictedMotorComputedStats` 加字段。
5. `BuddahTickMath.ModifiersToLocal`：加一行平移；`BuddahTickMathTests` 加用例。
6. `BuddahPredictionSkillMovementBridge`：加 `TryApplyFreeze`；`OwnerMovementEffectApplier` 与 `SkillExecutor` 加对应方法与 TargetRpc。
7. 配表登记：`Global/__enums__.ModifierCommandType` 加值；`Movement/ModifierRule` 加行（presentationOnly，只记录）。

然后 `PredictionProtocol.Version` 加一，并在其 History 注释补一行。
