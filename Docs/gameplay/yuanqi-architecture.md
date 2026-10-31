# 缘起架构设计

> **头注**
>
> - **这是什么**：缘起（技能组合）的代码架构：新代码放在哪里、模块之间谁能引用谁、配表怎么扩、三对已定缘起怎么落地，其中 #2 禅足冰封写到文件和时序的粒度。日期 2026-10-25；状态：**已实现**（2026-10-26，`feat/mechanics-update` 0dbc551），训练模式验证见 [训练模式验证记录](practice-verification.md)。
> - **依据**：机制规则以 [README §5](README.md) 为准，每对内容以 [缘起技能设计](yuanqi.md) 为准；本文不重复规则，只讲怎么构筑。类名、方法名、文件位置按 0dbc551 的代码核对。第 1 节的 1.2 记录改版前（dev 5f91e51）各技能的状况，是设计的出发点；其余各节写的是现在的代码。
> - **标记**：“已定”沿用 README 的用法（用户确认）；“建议（实现采用）”是本文的架构提案，代码已按它实现，用户没有逐条裁定；“待确认”还要问用户；“事实”是读代码得到的现状。
> - **和 README 7.1 的关系**：本文是 A5（缘起）的展开，也写了缘起需要 A3、A4 保证的接口。README 7.1 的步骤顺序不变。
> - **配表**：按 [design-tables README](../design-tables/README.md) 与 [excel-spec](../design-tables/excel-spec.md) 的规范写表；第 3 节的表在阶段 B 建成并导入（0 错误）。

## 0. 结论先行

| 问题 | 答案 |
|---|---|
| 新代码怎么写才不把现有模块弄脏 | 缘起是技能之外的第三方：目录 `ComboSkill/Yuanqi/`（三对在 `Yuanqi/Pairs/`），四个部件（状态板、登记、缘起模块、技能兼容接口），表现走 `SkillExecutor` 末尾追加的观察者 RPC 和事件，不新增 NetworkBehaviour。技能只多做两件事：告诉执行器自己的生效窗口、提供一个“可被消耗 / 可被改写”的句柄。`SkillExecutor` 不认识任何一对缘起。运动效果全部走预测修饰命令，不碰刚体 |
| 配表怎么变 | 阶段 A 不动表头，新参数先放缘起 SO 的序列化字段。阶段 B 按“Skill 宽表 + 强类型家族表”的现成模式加了 `Skill/Yuanqi`、`Skill/YuanqiEffectFrostLock`、`Skill/YuanqiEffectGiantCharge`，`__enums__` 登记 `YuanqiId`，映射走 `SkillDatabase` 现有的“Play 首次查表时写入 SO、退出 Play 恢复”那条路，不新建第二套时机 |
| 禅足冰封怎么构筑 | 预测马达新增一条 `Freeze` 修饰期限（定身的“限速清零 + 停止步”语义，再加转向扭矩归零、转向抑制和动作锁），服务器扫描半径内的所有 Buddah 并逐个下发；然后通过句柄消耗神足通（新的瞬时命令 `EndModifier(Acceleration)`）和缓流（销毁区域 + `StopVfxAllObserversRpc`）。详见第 4 节 |

## 1. 现状（事实）

### 1.1 技能分发链路

```text
玩家按键（Q/E/R、1/2/3）                       AI：AISkillCaster 直接调 CastSlotServer（A7）
  → SkillExecutor.RequestCast → CastSlotServerRpc   → OnSkillSlotTriggered → OnSlotTriggeredByCombo
  → CastSlotServer ◄──────────────────────────────────┘
  → TryResolveCast（结算区闸门、槽位、SkillCastRules.EvaluateGate：待确认施法 → 反噬锁 → 冻结 → 槽冷却）
  → ResolveCastVariant（按执念掷骰，只决定是否反噬，不换 SkillAction）
  → QueueCast（写槽冷却、算同招加价层数；PlayQueuedCastFeedbackObserversRpc 播前摇，反噬播统一反噬表现）
  → 1 秒后 ExecuteQueuedCastAfterDelay → SkillCastRules.ResolveConfirmOutcome
      反噬：ExecuteBackfireServer（不执行技能、不加执念，开始 10 秒惩罚）→ ServerCastChanged(Executed, IsBacklash)
      正常：executedSkill.ExecuteServer(caster, slot)   ← 技能真正生效的时刻
            → ObsessionFigure.AddServer(gain × 1.15^层数)
            → CastObserversRpc → ExecuteObservers / ExecuteLocal（纯表现）
            → BeginSkillStateServer → SkillStateBoard.Begin   ← 缘起在这里触发
            → ServerCastChanged(Executed)
```

状态板在 `CastObserversRpc` 之后才登记：缘起可能当场消耗某招，被消耗技能的收尾表现必须排在它的开始表现（拖尾、FOV）之后到达客户端。反噬不登记状态板，它没有效果。

### 1.2 改版前每个技能“生效中”的载体

技能 = `SkillAction` 子类（ScriptableObject，数据 + 三段执行）+ 运行时效果组件 + 预测修饰。缘起要能“中途消耗”，先要知道每个技能的剩余效果活在哪里、怎么结束、有没有提前结束的入口。下表是改版前（dev 5f91e51）的状况；改版后每个可被消耗的技能都有句柄，见 2.5。

| 技能 | 服务器侧的生效载体 | 怎么结束 | 改版前的提前结束入口 |
|---|---|---|---|
| 神足通 `acceleration` | 预测路径：`BuddahPredictedMotor._modifierState.AccelUntilTick`（服务器 + owner 各一份，TargetRpc 下发）；legacy 路径：owner 上的 `MovementAccelerationEffect` | tick 到期 | **没有按技能的取消**：马达只有 `ResetActiveSkillModifiers` 全清；legacy 有 `CancelAndRestore` |
| 禅定缓流 `slowtrap` | 施法者身上的 `MovementSlowTrapZoneEffect`（`Update` 用 `Time.deltaTime` 计时），触发体预制件挂在施法者下面；命中者通过 `target.ApplyAccelerationToOwner(负值)` 吃减速 | 组件销毁 | 复位时 `Destroy(trap)`；**区域特效 `PlayVfxAll` 播出去就停不下来**（`SkillVfxReplicator` 没有停止 RPC） |
| 法相金身 `giant` | 马达 `ScaleUntilTick / ScaleMultiplier / ScaleMassMultiplier / ScaleForwardForceMultiplier`；视觉与碰撞体尺寸由各端的 `PlayerScaleEffect` 自己计时（`CurrentScaleMultiplier` 是实时体型） | tick 到期 / 计时 | `PlayerScaleEffect.CancelAndRestore`（瞬间恢复，无缩回过渡）；马达无单项取消 |
| 如来神掌 `push_projectile_hands` | `BuddahHandControl._projectilePushModeUntilServer`（`Time.time`） | 到期 | `ResetSkillInputAndEffects` 全清 |
| 一念光明 `blackcurtain` | 不在施法者身上：每个 racer 的 `SkillPerceptionState.CurtainUntilTick`（服务器）+ 各端 `BlackCurtainViewController` | 计时 | 无 |
| 逆姻缘 `reverseturn` | 在受害者身上：各受害者马达的 `InvertTurnUntilTick` | tick 到期 | 无 |

### 1.3 预测修饰的边界

运动效果的唯一合法入口是修饰命令：`SkillExecutor.ApplyXToOwner` → `OwnerMovementEffectApplier` → `BuddahPredictionSkillMovementBridge` → `BuddahPredictionModifierBridge.TryApply` → `BuddahPredictedMotor.TryApplyModifierCommand`，服务器执行一份，再 TargetRpc 让 owner 执行一份（AI 与木桩没有 owner，只有服务器那份）。马达把命令写成 `BuddahPredictedModifierState` 里的 `*UntilTick` 期限，每 tick 由 `BuddahPredictedModifierResolver.Resolve` 折算成最终推力、限速、转向符号、`IsRooted` 等。

现在有 11 条期限：Root、Accel、PostRootAccel、Scale、InvertTurn、PushGrace、SuppressSteering、RoomBypass，以及机制改版（PR #63）加的 Freeze、Burst、MaxSpeedPenalty。所有期限随 reconcile 快照下发，纯客户端由 `BuddahTickMath.ModifiersToLocal` 逐条平移到本地时钟；只平移时间戳，`BurstDurationTicks` 这类时长不平移。客户端 reconcile 重放时，修饰按被重放的 tick 结算（`BuddahTickMath.ModifierResolveTick`），否则逐 tick 回落的 Burst 会在每个旧 tick 上按“现在”求值。每加一条期限都要按附录 A 的清单一次改齐，并把 `PredictionProtocol.Version` 加一（未发布的同一批改动只加一次）。这是加新运动效果的固定成本，不能绕。

马达里定身（`IsRooted`）的实际语义：`FinalForwardForce = FinalMaxSpeed = 0` → `ClampPlanarSpeed` 把水平速度压到上限 → `CompleteStoppedPredictionStep` 清掉待施加的力、速度和角速度归零后提前返回，不施加任何力和转向扭矩。禁锢就建在这个底座上。

### 1.4 清理路径

复活与重置会清掉进行中的技能：

- 真人的 owner 端走 `ResetActiveSkillEffectsForOwner`（取消四种 legacy 效果、拖尾、相机）；马达的 teleport 事件在 `ResetModifiers` 时也会回调它。
- 服务器控制的 racer（AI、木桩，`RacerAuthority.IsServerControlled`）走 `ResetServerControlledSkillState`：取消待确认施法、清神掌 buff、清感知、`ResetActiveSkillModifiers`、缓流区域 `Consume`（不是裸 `Destroy`，区域特效不挂在施法者下面，只有区域自己的停止 RPC 能在各端收掉它）。
- 状态板与缘起实例由 `SkillExecutor.ResetYuanqiStateServer`（`board.Clear` + `registry.ResetServer`）清空，它挂在所有服务器侧复位上：服务器控制的复位、真人的 `ResetActiveSkillEffectsServer`、真人复活 teleport 在服务器上被消费时（马达调用）、`OnStopServer`。否则复活后会留下“半触发”的状态。
- 反噬惩罚不是技能效果，任何复位都不清：极速 ×0.85 的修饰由 `BuddahPredictedModifierStateOps.PreservePersistentOnReset` 保留，锁和执念冻结看 `ObsessionFigure` 同步的结束 tick。

### 1.5 改版前已经存在、直接复用的部件

- `SteeringSuppression` 修饰：转向输入清零（开场交接用它；2026-10-30 起交接完全物理，交接后的屏蔽时长为 0，修饰本身保留）。
- `BuddahPredictionRouter.RouteImpulse(victim, impulse, torque, sourceType, source)`：服务器权威冲量，V2 预测的 Buddah 转到 `BuddahPredictionCombatAdapter.TryRouteImpulse`，owner 远端时自动 TargetRpc。#4 的撞击冲量走它（来源类型新增 `BodyImpact`）；突进本身是 `Burst` 修饰，不是冲量。
- `BuddahHandControl.GetPushScaleMultiplier()` 读 `PlayerScaleEffect.CurrentScaleMultiplier`：普通推掌 hitbox、蓄力弹碰撞体与出生点改版前就随体型缩放，只有冲量不随。所以 #1 金身巨掌在代码里原本就有一半（见 5.2 与 Q11）。
- `RacerDirectory.Current.All`：本局所有已登记的 racer（一念光明用它找受害者；木桩的 racer id 未分配，不在里面）。
- `SkillCombatEvents.Record(stage, kind, source, target, position, routed)`：诊断证据钩子，缘起触发也记（见 2.9）。
- `SkillExecutor.ServerCastChanged`：服务器施法事件，AI 在订阅。
- `TimedVfxInstance`、`SkillVfxReplicator`：特效实例带 id；改版补了停止接口（见 2.5）。

## 2. 新代码怎么写

### 2.1 目录与命名（建议（实现采用））

沿用“一个技能一个子目录、类名 `Skill_X`、资产 `Skill_X.asset`”的现有习惯，缘起平行放一套：

```text
Assets/Scripts/Buddah/ComboSkill/
  Yuanqi/
    SkillStateBoard.cs            每个 Buddah 一个（木桩除外），服务器独有，运行时添加
    SkillStateBoardCore.cs        纯 C# 的登记 / 到期 / 一招一条逻辑，无 Unity 依赖，供 EditMode 测试
    SkillStateEntry.cs            状态板上的一条生效项
    SkillActiveWindow.cs          生效窗口（开始偏移、时长）
    ISkillRuntimeHandle.cs        技能兼容接口：ISkillRuntimeHandle、IScaleRuntimeHandle、IHandsImpulseScale
    YuanqiAction.cs               缘起模块基类（ScriptableObject，像 SkillAction）
    YuanqiMode.cs                 YuanqiMode（Instant / Sustained）、YuanqiRetrigger（OncePerOverlap）
    YuanqiContext.cs              触发时交给缘起模块的上下文（施法者、两条生效项、tick、表现载荷、消耗）
    YuanqiRegistry.cs             每个 Buddah 一个：按配装登记、订阅状态板、驱动缘起实例
    YuanqiPairTable.cs            纯 C# 的匹配 / 一次重叠一次 / 持续型结束，供 EditMode 测试
    YuanqiCatalog.cs              某套配装能触发哪些缘起（服务器登记与 HUD 共用）
    YuanqiTargeting.cs            静态目标扫描（半径内的其他 Buddah，可含木桩）
    BuddahBodyImpact.cs           #4 的撞击（服务器，运行时挂到施法者上）
    YuanqiPresentationPayload.cs  缘起触发的表现载荷
    YuanqiImpactInfo.cs           撞击的表现载荷
    Pairs/
      Yuanqi_FrostLock.cs         #2 禅足冰封
      Yuanqi_GiantCharge.cs       #4 金足破阵
      Yuanqi_GiantPalm.cs         #1 金身巨掌
  Skill_Acceleration/AccelerationRuntimeHandle.cs
  Skill_Giant/GiantRuntimeHandle.cs
  Skill_PushProjectileHands/ProjectileHandsRuntimeHandle.cs   （缓流的句柄是 MovementSlowTrapZoneEffect 自己）
Assets/Scripts/Buddah/RacerSkillPresentationState.cs          冻结 / 突进的表现状态（每端每个 racer）
Assets/Skills_Assets/
  Yuanqi/
    Yuanqi_FrostLock.asset  Yuanqi_GiantCharge.asset  Yuanqi_GiantPalm.asset   （列在 Skill Database.asset 的 yuanqis）
```

命名：缘起 id 与技能 id 同风格（全小写 snake_case）：`frost_lock`、`giant_charge`、`giant_palm`；文案 key `yuanqi.<id>.name / .desc`；日志前缀 `[Yuanqi]`；命名空间与现有 ComboSkill 代码一致（全局命名空间，靠 `Yuanqi` 前缀区分）。英文名只用于代码与表，玩家看到的名字走 `UI/Text`（`SkillDatabase.GetYuanqiDisplayName`）。

### 2.2 部件与各自唯一的职责

| 部件 | 职责 | 不做什么 |
|---|---|---|
| **状态板 `SkillStateBoard`** | 记录本 Buddah 上正在生效的技能：每次正常执行一条 `SkillStateEntry`（`EntryId`、`SkillId`、`Slot`、`StartTick`、`UntilTick`、`Handle`）。提供 `Begin / End / TryGetActive / Clear`、只读的 `Entries`，以及 `Activated / Ended` 两个事件。服务器独有，不同步 | 不判断缘起，不知道“对”；不做每帧匹配（到期靠 `TimeManager.OnTick`，提前结束靠 `End`） |
| **开局登记 `YuanqiRegistry`** | 槽位定下后用 `YuanqiCatalog.ForLoadout` 从 `SkillDatabase.GetYuanqis()` 挑出两招都在本配装里、且启用的对，为每对建一个运行时实例（`Instantiate(so)`）；订阅状态板 `Activated`，某招生效时只查“已登记且含这招”的对，另一招也在生效、且这两条还没触发过就触发；`Ended` 时结束持续型实例 | 不知道任何一对的效果内容 |
| **缘起模块 `YuanqiAction` 子类** | 一对的效果：`OnTriggerServer(ctx)`，持续型再实现 `OnEndServer(ctx)`，各端表现 `PlayObservers`。通过 `ctx.ConsumeConfigured` 消耗技能、通过 `SkillExecutor.ApplyXToOwner` 系列下发运动效果、通过 `ctx.Presentation` 交出表现载荷 | 不引用任何 `Skill_*` 类，不直接改刚体 / transform / `BuddahMovement`，不自己发 RPC |
| **技能兼容接口** | `ISkillRuntimeHandle`：**消耗**（`Consume(reason)`：提前结束剩余效果并清理修饰与表现）；**参数改写**按需要的小接口：`IScaleRuntimeHandle.CurrentScale`（金身的实时体型）、`IHandsImpulseScale.ImpulseFollowsScale`（#1）。句柄由技能自己创建、自己实现 | 缘起模块只碰接口，不碰组件 |
| **表现接口（`SkillExecutor`）** | 观察者 RPC，按 id 从各端的 `SkillDatabase` 解析：`YuanqiObserversRpc` → `YuanqiAction.PlayObservers` 与事件 `YuanqiObserved`（owner 另发 `LocalYuanqiTriggered`）；`SkillConsumedObserversRpc` → `SkillAction.ExecuteConsumedObservers`；`YuanqiImpactObserversRpc` → `YuanqiImpactObserved`。都追加在类的末尾（FishNet 的 RPC 序号按声明顺序） | 不改游戏状态；不新增 NetworkBehaviour（prefab 上的 behaviour 序号不变） |

### 2.3 依赖方向（硬规则）

```text
SkillAction（窗口、句柄）──► SkillExecutor ──写──► SkillStateBoard ──事件──► YuanqiRegistry ──► Yuanqi_* (YuanqiAction)
       │ 实现                                                                                   │ 只经由
       ▼                                                                                        ▼
ISkillRuntimeHandle ◄────────────────────────────────────────────── YuanqiContext.ConsumeConfigured（handle.Consume）
                                                                     SkillExecutor.ApplyXToOwner（修饰命令）
                                                                     BuddahPredictionRouter.RouteImpulse（冲量）
                                                                     ctx.Presentation → SkillExecutor.PlayYuanqiObservers（表现 RPC）
```

- `Skill_*` 永远不引用 `Yuanqi_*`；`SkillExecutor` 不引用 `Yuanqi_*`，也不知道有哪些对。
- `Yuanqi_*` 之间互不引用；加一对缘起不改另一对。
- 运动效果只走修饰命令；画面只走观察者 RPC 或同步状态；判定只在服务器。
- 状态板由 `SkillExecutor` 在每次正常执行后统一 `Begin`（见 2.4），窗口和句柄由技能自己的 `SkillAction` 提供，实时参数（体型）从句柄读，别人只读。

### 2.4 状态板的数据契约（建议（实现采用））

```text
SkillStateEntry {
  int    EntryId          每个状态板内递增，“一次重叠只触发一次”靠 (EntryIdA, EntryIdB)
  string SkillId          普通技能 id（反噬不登记：反噬没有效果）
  int    Slot
  uint   StartTick        窗口开始 = 执行 tick + 开始偏移（服务器 LocalTick）
  uint   UntilTick        窗口结束 = 开始 + 名义时长；被缘起消耗时提前 End
  ISkillRuntimeHandle Handle
  bool   IsActivated      窗口已开始（Activated 已发）
  bool   IsEnded          已结束（Ended 已发），不会再生效
  string EndReason        结束原因（只用于日志）
}
```

**谁写 Begin**：`SkillExecutor.ExecuteQueuedCastAfterDelay` 在 `CastObserversRpc` 之后调用 `BeginSkillStateServer` → `board.Begin(skillId, slot, window, handle)`，是一处通用代码，没有按技能的分支；时长为 0 不登记。缘起的异常不会吞掉 `Executed` 事件（AI 的施法承诺在等它）。窗口与句柄来自 `SkillAction` 的虚方法，默认实现覆盖大多数技能，特殊的自己覆盖：

| `SkillAction` 成员 | 默认实现 | 覆盖者 |
|---|---|---|
| `virtual SkillActiveWindow GetActiveWindow(SkillExecutor)` | 开始偏移 0，时长 = `SkillExecutor.ResolveEffectDurationSeconds`：表 `Skill.effectDurationSeconds`，无表行时取 SO 主时长字段（`SkillDatabase.EffectDurationField`），都没有就是 0 | 一念光明：偏移 `expandDurationSeconds`、时长 `holdDurationSeconds`（README 已定：按保持期 10 秒算）；逆姻缘：默认即可（施法者窗口 = 反转时长） |
| `virtual ISkillRuntimeHandle CreateRuntimeHandle(SkillExecutor)` | `null`（不能被消耗） | 神足通、缓流、金身、神掌各返回自己的句柄 |
| `virtual void ExecuteConsumedObservers(SkillExecutor, int slot, bool localIsCaster)` | 空 | 被消耗时各端的收尾表现：神足通、金身、神掌 |

**谁写 End**：期限到点由状态板自己在 `OnTick`（订阅 `TimeManager.OnTick`，与 `AISkillCaster` 同款）结束；缘起消耗时由 `YuanqiContext` 在 `handle.Consume` 之后调 `board.End(entryId, reason)`；复位时 `board.Clear(reason)`。`Ended` 事件携带 entry，持续型缘起据此撤改写。

**一招一条生效项**：同一招在旧 entry 还生效时再次施放，状态板先以原因 `superseded` 结束旧 entry（照常发一次 `Ended`），再登记新 entry。神掌的 15 秒 buff 比 12 秒冷却长，没有这条，旧 entry 到期会在新一次施放仍生效时撤掉 #1 的冲量改写。

**比赛中换配装**：只有诊断工具会这样做；`SkillLoadout.ServerSlotsChanged` 触发时先 `board.Clear()`，再按新配装重新登记缘起。

窗口口径（已定规则的代码落点）：

| 技能 | 窗口 | 来源 |
|---|---|---|
| 神足通 / 缓流 / 金身 / 神掌 | 执行起，`effectDurationSeconds` | 表 |
| 一念光明 | 执行 + 展开 1 秒起，保持 10 秒 | `Skill_BlackCurtain.GetActiveWindow`（`expandDurationSeconds`、`holdDurationSeconds`，后者由表 `effectDurationSeconds` 覆盖） |
| 逆姻缘 | 执行起 10 秒 | 表 |

### 2.5 技能兼容接口与各技能的消耗语义（建议（实现采用））

```csharp
public interface ISkillRuntimeHandle
{
    string SkillId { get; }
    bool IsActive { get; }          // 底层效果还在（仅供参考；“生效中”以状态板窗口为准）
    void Consume(string reason);    // 服务器调用；幂等；不碰冷却和执念
}
public interface IScaleRuntimeHandle : ISkillRuntimeHandle { float CurrentScale { get; } }   // 金身：实时体型（≥ 0.1）
public interface IHandsImpulseScale { bool ImpulseFollowsScale { get; set; } }              // BuddahHandControl 实现，#1 设置
```

| 技能 | 句柄实现 | `Consume` 做什么 |
|---|---|---|
| 神足通 | `AccelerationRuntimeHandle` | `caster.EndAccelerationForOwner()`：服务器发瞬时命令 `EndModifier(Acceleration)` + TargetRpc 给 owner 同样执行；legacy 路径 `MovementAccelerationEffect.CancelAndRestore`。然后 `PlaySkillConsumedObservers` → 各端 `Skill_Acceleration.ExecuteConsumedObservers`：拖尾停止发射并淡出（`EndAccelerationTrailEarlyLocal`），背后特效停止（`StopVfxLocal`，余韵 `consumedVfxLingerSeconds` 1 秒），施法者相机 FOV 提前回落（`SettleCameraFovBoostEarlyLocal`，只在 FOV 槽仍属于神足通时） |
| 禅定缓流 | `MovementSlowTrapZoneEffect` 自己实现接口 | 停掉区域特效（`SkillVfxReplicator.StopVfxAll(vfxId, zoneEndVfxLingerSeconds)`），再关掉并销毁区域组件（`OnDisable` 销毁触发体与调试体） |
| 法相金身 | `GiantRuntimeHandle`（`IScaleRuntimeHandle`；`CurrentScale` 读服务器上的 `PlayerScaleEffect.CurrentScaleMultiplier`，没有它时读马达体型） | `caster.EndScaleForOwner()`：`EndModifier(Scale)` + TargetRpc；各端 `PlayerScaleEffect.EndEarly()` 走正常缩回过渡（不是瞬间的 `CancelAndRestore`） |
| 如来神掌 | `ProjectileHandsRuntimeHandle`；改写接口 `IHandsImpulseScale` 由 `BuddahHandControl` 实现 | `BuddahHandControl.EndProjectilePushModeServer()`（只收 buff，已在前摇的推掌照常打出）；各端 `EndProjectilePushModeLocal`，施法者 FOV 提前回落 |
| 一念光明 / 逆姻缘 | 不提供句柄（没有任何已定缘起会消耗它们） | — |

目前的三对只会消耗神足通和缓流（见 3.2 的 `consumeA / consumeB`）；金身和神掌的句柄已经实现，但还没有缘起消耗它们。

`EndModifier` 是瞬时命令，不是期限修饰：`ValueA` = 目标类型，把目标期限提前到当前 tick（`Min(期限, now)`，只缩短不延长，没设过的 0 仍是 0）。能结束 Acceleration、RootThenAcceleration、Scale、Freeze、Burst、MaxSpeedMultiplier。Acceleration 只在加成为正时结束：缓流的减速和神足通共用这个通道（幅度为负），消耗神足通不能顺手结束别人放的减速。

**缓流的特效**：区域特效跟随目标、不挂在施法者下面，按子物体搜索找不到，所以 `SkillVfxReplicator` 记下自己生成的每个实例（有无父物体都记）；`StopVfxAll(vfxId, linger)` 走 `StopVfxAllObserversRpc`，各端对匹配的实例调用 `TimedVfxInstance.StopNow(linger)`：立即停止发射，粒子淡出后销毁。区域的所有结束方式（到期、缘起消耗、服务器控制复位）都走同一个结束入口并停掉特效，所以画出来的区域不会比实际区域和状态板 entry 活得更久。SO 的 `vfxDurationSeconds` = 11 是区域 10 秒时期留下的旧值（表里区域已经是 6 秒，`SkillPresentation` 同为 11，归 design-tables A9），现在只是特效时长的上限。

**神足通的消耗与共享槽（待确认，Q15）**：预测路径下 `EndModifier(Acceleration)` 只在加成为正时结束，所以消耗不会结束施法者身上的缓流减速；legacy 路径（预测关闭）的 `CancelAndRestore` 会连同合并进去的减速一起清掉。边界情况：施法者先放神足通，之后敌人的缓流命中，减速覆盖了共享槽的加成（后写者的幅度生效，期限取两者较晚的一个）。这时 #2 或 #4 消耗神足通，对移动没有任何作用：施法者一直减速到较晚的期限，往往就是神足通原本的期限；#4 的突进从减速后的上限起步（80 − 20 + 60 = 120，而不是 140）。

### 2.6 `SkillExecutor` 的改动面（刻意收窄）

只加同类方法，不加缘起逻辑：

- 运动：`ApplyFreezeToOwner(seconds)`、`EndAccelerationForOwner()`、`EndScaleForOwner()`、`ApplyBurstToOwner(extraMaxSpeed, seconds)`、`ApplyMaxSpeedMultiplierToOwner(multiplier, seconds)`，与现有 `ApplyRootThenAccelerationToOwner` 同构（服务器桥接 + owner TargetRpc，TargetRpc 追加在类末尾），实现放在 `OwnerMovementEffectApplier`。
- 动作锁：请求和 AI 的就绪判断共用 `SkillCastRules.EvaluateGate`（`TryResolveCast` 与 `IsSlotReadyServer`），依次检查待确认施法、反噬锁、冻结（`BuddahPredictedMotor.IsFrozen`）、本槽冷却。冻结和反噬锁是同一个检查点，没有两套锁。
- `ApplyFreezeToOwner` 在下发修饰前先打断前摇（`InterruptCastForFreezeServer`）：正在前摇的普通施法被取消，该槽冷却退还（`_nextReadyTime[slot] = now`，已定，Q10）；执念在执行时才入账，所以不用退；手部待处理的输入作废（`BuddahHandControl.CancelPendingServerInput`；A7 之前这里还清空服务器 AI 的搓招键）。已判定的反噬不被打断（`SkillCastRules.DropsPendingCast`，已定，2026-10-26）：冷却照常花掉，前摇结束时开始惩罚。
- `ExecuteQueuedCastAfterDelay` 里一行 `BeginSkillStateServer(...)`（见 2.4）。
- 复位路径里加 `ResetYuanqiStateServer(reason)`（`board.Clear` + `registry.ResetServer`，见 1.4）。
- 缘起与表现用的通用入口：`ResolveEffectDurationSeconds`、`ResolveActiveSlot`、`PlaySkillConsumedObservers`、`PlayYuanqiObservers`、`PlayYuanqiImpactObservers`，事件 `LocalYuanqiTriggered`、`YuanqiObserved`、`YuanqiImpactObserved`，属性 `StateBoard`、`YuanqiRegistry`（服务器；客户端和木桩上为 null）、`PresentationState`。

`BuddahHandControl` 加同样的动作锁：冻结时 `CanPushServer` / `TryPushServer` 拒绝推掌（改版前推掌没有任何定身检查），服务器丢掉 `SetHandYawServerRpc` 和 AI 的 `InjectServerRotation`，owner 忽略瞄准输入，解冻后 owner 补发一次手的朝向。

### 2.7 网络与预测规则（新效果一律照此）

| 效果类型 | 做法 |
|---|---|
| 作用于运动（禁锢、突进、取消加速 / 体型、反噬限速） | 新修饰命令，服务器 + owner 各执行一份，期限进 reconcile 快照；按附录 A 一次改齐 + 协议版本 +1。阶段 A 接受多次 +1（已定，Q13），但每条命令的改动必须齐备，History 注释写明加了什么，不留半截命令。这次四条命令和新 RPC 一起只加到 v2（建议（实现采用）：整批改动尚未发布） |
| 一次性冲量（撞击弹开） | `BuddahPredictionRouter.RouteImpulse`，不新建通道 |
| 只影响画面 | `SkillExecutor` 的观察者 RPC 和事件（见 2.2），表现层只读不写；受害者自己 HUD 上的冻结剩余时间从本地预测到的期限读（`BuddahPredictedMotor.FreezeSecondsLeft`），不另发 |
| 持续的服务器状态（反噬惩罚） | `ObsessionFigure` 同步结束 tick（SyncVar），各端按自己的时钟算剩余时间并发 `BackfireStateChanged`；中途加入的客户端也会收到一次开始 |
| 状态板 | 不同步。客户端需要知道的（本配装有哪些缘起）只依赖已同步的 `SkillLoadout.SlotSkillIds` + 本地 `SkillDatabase`，用同一个 `YuanqiCatalog.ForLoadout` 算出 |

### 2.8 测试

- 纯 C# 逻辑都进 `BuddahGo.Tests`（friend assembly）：`SkillStateBoardCoreTests`、`YuanqiPairTableTests`（登记 / 匹配 / 一次重叠只触发一次 / 任一招结束后重新重叠可再触发）、`YuanqiCatalogTests`、`YuanqiContextTests`、`YuanqiPairRulesTests`、`YuanqiPairConsumeTests`、`YuanqiPairAssetTests`、`SkillCastRulesTests`、`ObsessionBackfireRulesTests`。
- 马达：`BuddahPredictedModifierResolverTests`（Freeze、Burst、MaxSpeedMultiplier 分支）、`BuddahPredictedModifierStateOpsTests`、`BuddahTickMathTests`（含重放 tick）、`HandoffClockTests`（新期限的平移、时长不平移）。
- 表：`DesignTableTests`、`DesignTableRuntimeTests`（新映射的目标字段存在、产物值等于单元格，决定 23；`EveryYuanqiRowHasAnAssetOfItsFamily`）。
- 场景级：训练模式的 `MechanicsPracticeHarness`（13 个场景），结果和重跑方法见 [训练模式验证记录](practice-verification.md)。
- 0dbc551 上 headless EditMode 559/559 通过。

### 2.9 整洁守则（提交前自检）

- 加一对缘起 = 一个 `Yuanqi_*` 类（`Yuanqi/Pairs/`）+ 一个 `.asset`（列进 `Skill Database.asset` 的 `yuanqis`）+ `Skill/Yuanqi` 一行与文案；有参数时再加一张 `YuanqiEffect<对>` 家族表，并在 `SkillDatabase`（`YuanqiFamilyActionType`、`ApplyYuanqiFamily`）和 `SkillConfigCrossValidator` 各加一行。碰到 `SkillExecutor`、其他 `Yuanqi_*` 或任何 `Skill_*` 就说明边界画错了，先回头改接口。
- 消耗什么只由表的 `consumeA / consumeB` 决定；一对只决定时机（调用 `ctx.ConsumeConfigured`），不无条件 `ConsumeA / ConsumeB`。
- 新数字先是 SO 上的序列化字段带默认值（README 7.0，已定），进表后 SO 值只是无表时的兜底；不写字面量在方法里。
- 新修饰命令按附录 A 一次改齐，并在 `Movement/ModifierRule`（presentationOnly）与 `Global/__enums__.ModifierCommandType` 登记。
- 登记处每次触发自动记 `SkillCombatEvents.Record("yuanqi-trigger", yuanqiId, caster, target, pos, …)`（每个目标一条，没有目标时一条）；#4 的撞击另记 `hit / body-impact`。
- 反噬变体已删除：六个 `Skill_*_Anti` 类与资产、`FireChargedProjectileBurstReplicated`、`PlayChargedBurstVisuals*`、`ResolveAntiSkillId` 的后缀推断（2026-10-31 按用户决定全部恢复：反噬 = 统一惩罚 + 必定变体，变体不进状态板、不参与缘起，见 [README §6.2](README.md)）；`MovementRootThenAccelerationEffect` 留作 legacy 路径的禁锢。
- 日志走 `GameLog.Verbose` 并带 `[Yuanqi]` 前缀；不在表现层写游戏状态。

## 3. 配表怎么变

### 3.1 原则

- 阶段 A 不改任何表头与 sheet；缘起参数在 `Yuanqi_*` SO 上（README 7.0，已定）。阶段 B 按本节接表，SO 字段退为无表行时的兜底（决定 14：覆盖而非重构）。
- 结构沿用 v2 原则“一概念一表”：一张 `Yuanqi` 宽表放身份与收尾规则，一对一张强类型家族表放参数，和 `Skill` + `SkillEffect<家族>` 完全同构（决定 16、20）。
- 字段名按本节预留，表结构同时满足可延展与可读（已定，Q14）：新的一对只加一行 `Yuanqi` 与一张家族表，不在宽表上堆各对的专属列；家族表以对命名，`##comment` 写明每列对应 yuanqi.md 里的哪个字母参数（N、M、R）。表里每一列都有读取方（决定 22），没有参数的对不建家族表。
- 本节的表已在阶段 B（16121ed）建成并导入。

### 3.2 `Skill/Yuanqi`

| `##var` | `##type` | 说明 |
|---|---|---|
| `yuanqiId` | `enum:YuanqiId#key` | 名单在 `Global/__enums__`，与 SO 的 `yuanqiId` 一致 |
| `pairNumber` | `int#range=1,15`（doc） | yuanqi.md 的固定编号 #1–#15，只为对照，无读取方 |
| `displayNameKey` | `string#ref=UI/Text.key` | `yuanqi.frost_lock.name`；HUD 缘起提示与选技能界面 |
| `descriptionKey` | `string#ref=UI/Text.key` | 选技能界面 |
| `skillA` | `enum:SkillId#ref=Skill.skillId` | 两招无序；`ctx.EntryA` 总是 `skillA` |
| `skillB` | `enum:SkillId#ref=Skill.skillId` | |
| `consumeA` | `bool` | 触发后 A 是否被消耗 |
| `consumeB` | `bool` | |
| `mode` | `enum:YuanqiMode` | `Instant`（触发一次）/ `Sustained`（两招都生效期间持续） |
| `retriggerPolicy` | `enum:YuanqiRetrigger` | `OncePerOverlap`（README 5.1，已定）；留枚举以便以后改 |
| `isEnabled` | `bool` | 临时关掉一对而不删行 |

tags：`skill, info`。`Skill` 表自身不加列：缘起窗口用的就是现有 `effectDurationSeconds`。

现有三行：

| yuanqiId | # | skillA | skillB | consumeA | consumeB | mode |
|---|---|---|---|---|---|---|
| `giant_palm` | 1 | `giant` | `push_projectile_hands` | false | false | Sustained |
| `frost_lock` | 2 | `acceleration` | `slowtrap` | true | true | Instant |
| `giant_charge` | 4 | `acceleration` | `giant` | true | false | Instant |

### 3.3 家族表：`Skill/YuanqiEffect<对>`

| sheet | 列（现值） | 对应 |
|---|---|---|
| `YuanqiEffectFrostLock` | `yuanqiId#key#ref=Yuanqi.yuanqiId`、`freezeSeconds`（N，1.5）、`radiusMeters`（R，12）、`affectsTrainingDummies`（true） | #2 |
| `YuanqiEffectGiantCharge` | `yuanqiId`、`burstExtraMaxSpeed`（限速增量，60）、`burstSeconds`（回落秒数，0.8）、`impactImpulseMultiplier`（M，3，基准 `Push.basic.impulseStrength`）。不设曲线列：马达的回落形状固定为 增量 × 剩余比例²，两端必须一致 | #4 |
| （#1 金身巨掌） | 无参数，不建表；`Yuanqi` 行已经足够 | #1 |

tags：`skill, balance`。

### 3.4 配套改动

| 位置 | 改动 |
|---|---|
| `Global/__enums__` | 新枚举 `YuanqiId`（`frost_lock`、`giant_charge`、`giant_palm`）、`YuanqiMode`、`YuanqiRetrigger`；`ModifierCommandType` 追加 `Freeze` 8、`EndModifier` 9、`Burst` 10、`MaxSpeedMultiplier` 11，已有值同时改成与 C# `BuddahPredictedModifierCommandType` 同值；`StackPolicy` 加 `Restart` |
| `Movement/ModifierRule`（presentationOnly） | 追加四行：`Freeze`（RefreshMaxDuration，技能重置时清）、`EndModifier`（瞬时命令，无叠加策略，不清）、`Burst`（Restart，技能重置时清）、`MaxSpeedMultiplier`（反噬惩罚的 ×0.85，RefreshMaxDurationOverwriteMagnitude，`resetOnSkillReset = false`），写清 ValueA/B 语义 |
| `Global/StructuralConstant` | `prediction_protocol_version` = 2 |
| `UI/Text` | `yuanqi.<id>.name / .desc` 六行。“起”等字不在 SoloCJK 字体子集里，子集还没有重烘（HUD 现在退回 id 或英文，见 README A6） |
| `scope.json` | `export.Skill` 追加三张 sheet 与 tags |
| `Tools/design-tables/gen_records.py` | 不改脚本，重跑生成 `YuanqiRecord`、`YuanqiEffectFrostLockRecord`、`YuanqiEffectGiantChargeRecord` 与 `ProjectConfigDatabase.yuanqis`、`yuanqiEffectFrostLocks`、`yuanqiEffectGiantCharges` |
| `Docs/design-tables/inventory-skills.md`、`excel-spec.md` §5 / §6.3 | 补表目录与消费方 |

### 3.5 映射（消费方接通，建议（实现采用））

缘起列表放进现有 `SkillDatabase`（`public List<YuanqiAction> yuanqis`，读取用 `GetYuanqis()` / `TryGetYuanqi(id)`），而不是另建数据库。理由：复用 `EnsureDesignTablesApplied` 的“每局 Play 首次查表时写入 SO、编辑器退出 Play 恢复资产”（决定 24），避免再出现一次 10 秒对 3 秒那种时机 bug。

| 表 | 目标 | 方式 |
|---|---|---|
| `Yuanqi` 行 | `Yuanqi_*` SO 的 `skillA / skillB / consumeA / consumeB / mode / retriggerPolicy / isEnabled` | `DesignTableOverlay.ApplyMapped`（`SkillDatabase.YuanqiRowMap` 显式列出，防止同名误写；决定 21） |
| `YuanqiEffect<对>` 行 | 那一对 `Yuanqi_*` 类的同名字段 | `DesignTableOverlay.Apply`（`ApplyYuanqiFamily`，类不匹配的行跳过并警告；对应关系在 `SkillDatabase.YuanqiFamilyActionType`） |
| 文案 | `SkillDatabase.GetYuanqiDisplayName(id)` → `DesignTableOverlay.Text(key, so.displayName 或 id)`；说明文字 `GetYuanqiDescription(id, fallback)` | 选技能界面与 HUD 用 |

`YuanqiRegistry` 开局登记时只读 SO（已被表覆盖），不直接读表，和 `SkillExecutor` 读 `SkillAction` 一个道理。

### 3.6 校验规则（建议（实现采用））

`DesignTableDomainRules`：

- `skillA / skillB / consumeA / consumeB / mode / retriggerPolicy / isEnabled` 必填（Error）：留空的招式导入成空串，`YuanqiCatalog.ForLoadout` 会静默丢掉这一对；留空的 bool 读成 false，留空的枚举沿用资产值。
- `skillA != skillB`；两招都引用 `Skill.skillId`（表里已经没有 `_anti` 行）。
- 无序唯一：`{skillA, skillB}` 作为集合在表内唯一。
- `mode = Sustained` 时 `consumeA / consumeB` 都必须是 false（持续型没有“被消耗”的说法；都 false 的 Instant 也合法 = 两招照常持续）。Sustained 按导入时的解析判断：名字不分大小写，或枚举值 `1`。
- 设计约束“避免三招两两互有效果”：对任何三个普通技能，若三对都在表里且启用，报 Warning（不是 Error，留给设计判断）。

家族表的 `yuanqiId` 必须存在于 `Yuanqi`（`#ref`），对应 SO 的类名与家族匹配：表规则看不到资产，所以由 `SkillConfigCrossValidator`（Warning）、EditMode 测试 `EveryYuanqiRowHasAnAssetOfItsFamily` 和运行时的 `ApplyYuanqiFamily`（跳过并警告）三处检查。

消耗什么只由 `consumeA / consumeB` 决定：一对的类只决定时机，调用 `ctx.ConsumeConfigured`（#4 在突进之前，#2 在禁锢之后），登记处在 `OnTriggerServer` 之后按同样的标志再调一次（幂等）。所以表里两招谁写在 A 列都行，改消耗标志也不会被代码忽略。

### 3.7 与其他步骤共享的表变化

README 7.2 列了 A1（木桩配置）、A3（删 `castLockSeconds`）、A4（删 `_anti` 行与列、`Obsession` 五个新列）的表改动，都已完成；A7（删 `ComboBinding / ComboInput`、AI 人格表加缘起决策参数）已随 A7 完成（ac3d5fe）。缘起对它们只有一个要求：`Skill.effectDurationSeconds` 继续是每个普通技能的主效果时长，状态板窗口靠它。

## 4. 禅足冰封（#2）的具体构筑

### 4.1 规格（摘自 yuanqi.md，已定）

神足通与禅定缓流重叠的一瞬间触发；以施法者为中心、半径 R 的圆形范围内，施法者以外的所有人（真人、AI、木桩）禁锢 N 秒：速度瞬间归零、不保留转向、不能放技能、不能推掌；一次性生成；触发后两招都被消耗。画面：地面瞬间结冰，受影响者冻成冰块，结束恢复（表现层这一轮只做接口，见 4.7）。冰块本身的碰撞效果以后再做。

### 4.2 时序

```text
[先放缓流，后放神足通]（更常见：缓流 6 秒，神足通 3 秒，重叠窗口 5 秒）
t0   缓流 ExecuteServer → CastObserversRpc → board.Begin(slowtrap, until t0+6, handle=zone)
     registry: Activated(slowtrap) → 含缓流的对 {#2} → 另一招 acceleration 不在板上 → 不触发
t1   神足通请求（1 秒前摇，须 t1 ≤ t0+5）
t1+1 神足通 ExecuteServer → 加速修饰下发 → CastObserversRpc → board.Begin(acceleration, until t1+4, handle=accel)
     registry: Activated(acceleration) → {#2} → slowtrap 在板上、这两条没触发过 → 触发
       Yuanqi_FrostLock.OnTriggerServer(ctx):
         1. YuanqiTargeting.CollectOthersWithin(caster, R, affectsTrainingDummies)
         2. 对每个目标 target.ApplyFreezeToOwner(N)
         3. ctx.Presentation = { 中心, R, N, 目标 id }
         4. ctx.ConsumeConfigured(...)   // 表：两招都消耗。神足通：加速结束、拖尾、FOV；缓流：区域销毁、特效停止
       registry: 按表再 ConsumeConfigured 一次（幂等）→ PlayYuanqiObservers → 每个目标记一条 yuanqi-trigger
     board: 两条 Ended；这一组 (EntryIdA, EntryIdB) 已触发，不会再触发
[先放神足通，后放缓流] 完全对称，只是重叠窗口只有 2 秒（3 秒 − 1 秒前摇）
```

神足通那 1 帧的加速会被当场消耗，这是规则决定的（yuanqi.md 收尾条目），不是 bug。

### 4.3 组件清单

| 文件 | 类 | 新 / 改 | 职责 |
|---|---|---|---|
| `Yuanqi/Pairs/Yuanqi_FrostLock.cs` | `Yuanqi_FrostLock : YuanqiAction` | 新 | 字段 `freezeSeconds` 1.5、`radiusMeters` 12、`affectsTrainingDummies` true（表 `YuanqiEffectFrostLock` 覆盖）；`OnTriggerServer` 如 4.2；自己不画任何东西 |
| `Yuanqi/YuanqiTargeting.cs` | 静态 | 新 | `CollectOthersWithin(caster, radius, includeTrainingDummies, buffer)`：遍历场上所有 `SkillExecutor`（逆姻缘的找法，木桩也带 `SkillExecutor`，A1 已定要保留），只取服务器已初始化的对象，水平距离（XZ）≤ R，排除施法者；判定 `IsEligible` 单独可测 |
| `ComboSkill/SkillExecutor.cs` | — | 改 | `ApplyFreezeToOwner(seconds)`（先打断前摇、退冷却）+ `ApplyFreezeTargetRpc`；施法闸门含冻结；`YuanqiObserversRpc` |
| `ComboSkill/SkillCastRules.cs` | `SkillCastRules`、`SkillRepeatSurcharge` | 新 | 施法闸门、前摇结束的结果、前摇丢弃规则（冻结不丢已判定的反噬）；同招加价 |
| `ComboSkill/OwnerMovementEffectApplier.cs` | — | 改 | `ApplyFreezeServer / ApplyFreezeOwner`：预测路径发 `Freeze` 命令；legacy 路径复用 `MovementRootThenAccelerationEffect.ApplyOrRestart(move, N, 0, 0, 0.05)` |
| `Buddah/BuddahHandControl.cs` | — | 改 | 冻结时拒绝推掌、手不转（见 2.6）；实现 `IHandsImpulseScale`（给 #1 用，与本对无关但同一批接口） |
| `New_Buddah/Core/BuddahPredictedModifierState.cs` 等（附录 A） | — | 改 | 新期限 `FreezeUntilTick`，命令 `Freeze`（ValueA=秒）与 `EndModifier`（ValueA=类型） |
| `New_Buddah/Core/BuddahPredictedModifierResolver.cs` | — | 改 | Freeze 生效时：`FinalForwardForce = FinalMaxSpeed = 0`、`FinalTurnTorque = 0`，`IsRooted`、`IsSteeringSuppressed`、`IsFrozen` 为真 |
| `New_Buddah/Core/BuddahPredictedModifierStateOps.cs` | 静态 | 新 | 纯状态写入：`ApplyFreeze`（两次取较晚期限）、`EndModifier`、`ClearActiveSkillModifiers`（清 `FreezeUntilTick`） |
| `New_Buddah/Core/BuddahPredictedMotor.Events.cs`、`.SkillState.cs` | — | 改 / 新 | `TryApplyModifierCommand` 的 `Freeze` 分支写期限后立即 `StopBodyForFreeze`（清掉待施加的力，速度与角速度归零）；读接口 `IsFrozen`、`FreezeSecondsLeft` |
| `Network/Gameplay/SkillVfxReplicator.cs` | — | 改 | 记下自己生成的实例；`StopVfxAll(vfxId, linger)` → `StopVfxAllObserversRpc`（缓流的消耗要用） |
| `Skill_Acceleration/…`、`Skill_SlowTrap/…` | — | 改 / 新 | `AccelerationRuntimeHandle`；`MovementSlowTrapZoneEffect` 实现接口；两个 SkillAction 的 `CreateRuntimeHandle` |
| `Buddah/RacerSkillPresentationState.cs` | `RacerSkillPresentationState` | 新 | 每端每个 racer：`FrozenChanged`、`IsFrozen`、`FreezeSecondsLeft`；冻结期间 Animator 停住 |
| `Network/PredictionProtocol.cs` | — | 改 | `Version = 2` |

### 4.4 触发判定细节

- 匹配只发生在 `Activated` 事件里，查的是“本 racer 已登记、且含这招”的对（配装里没有的组合连实例都没有，自然不检测）。
- “一次重叠只触发一次”（README 5.1，已定）：`YuanqiPairTable` 记住每对上次触发用的 `(EntryIdA, EntryIdB)`；同一组 entry 不再触发；任一招结束后重新施放会产生新的 entry id，就能再触发。#2 两招都被消耗，所以这条规则对它天然成立，但登记处统一实现。
- 被消耗的技能不返还冷却与执念（README 5.1，已定）：冷却在 `QueueCast` 已写、执念在执行时已加，`Consume` 不碰它们，无需额外代码。
- 施法者自己此刻被别人冻住：施法已被动作锁拦在前面，所以实际不会出现“冻住还能施法再触发”。

### 4.5 禁锢的实现

**服务器侧**。`target.ApplyFreezeToOwner(N)`：

1. 打断目标正在前摇的普通施法并退还该槽冷却（已判定的反噬除外，见 2.6）。
2. `MovementEffects.ApplyFreezeServer(N)` → 修饰命令 `Freeze`（服务器马达那份）。
3. 目标有 owner 连接 → `ApplyFreezeTargetRpc(conn, N)` → owner 马达执行同一条命令。AI、木桩没有 owner 连接，只执行服务器那份；host 自己的 racer 收到 TargetRpc 时同时是服务器，预测路径下不再执行第二遍。与现有 `ApplyRootThenAccelerationToOwner` 行为一致。

**马达侧**。`Freeze` 复用定身的底座再加三件事：

| 规格 | 落点 |
|---|---|
| 速度瞬间归零 | 命令落地的同一帧 `StopBodyForFreeze`（清掉待施加的力，速度与角速度归零）；之后每 tick rooted 的停止步（`CompleteStoppedPredictionStep`）保持不动 |
| 转向也不保留 | 角速度随上一条归零；`FinalTurnTorque = 0` 与 `IsSteeringSuppressed` 保证不再有扭矩；双手也不转（见 2.6） |
| 不能放技能 | `SkillCastRules.EvaluateGate` 读 `BuddahPredictedMotor.IsFrozen` 拒绝请求；冻结下发时打断正在前摇的施法（已定，Q10） |
| 不能推掌 | `BuddahHandControl.CanPushServer / TryPushServer` 同上；神掌正在蓄力的那一下也随 `_inputGeneration` 失效 |
| 冻结中受到冲量（被神掌打、被金身撞） | 推不动（已定，Q12）：rooted 的停止步每 tick 清掉待施加的力并把速度归零；“冰块能被推走”留到冰块效果 |

`IsRooted` 在冻结期间保持为 true，这样 AI 的 `CanContinueCast` 注释里那条“定身不取消已接受的施法”、表现层的 `_presentationWasRooted` 等既有消费者都不用改。`IsFrozen` 是额外的标志，给动作锁、HUD 和表现用。`AISkillWorld` 把冻结剩余时间算进“受控”估计（冻结会报 `IsRooted`，没有自己的期限时会被读成剩 0 秒的定身）。

**owner 侧 HUD**。owner 本地的修饰状态里就有 `FreezeUntilTick`（TargetRpc 写入，reconcile 快照维持），冻结倒计时直接从 `BuddahPredictedMotor.FreezeSecondsLeft` 读，不另发同步。

**legacy 路径**。`MovementRootThenAccelerationEffect` 已有定身 + `SetSkillRooted` 标志，给 0 加速即可当禁锢；legacy 只是预测关闭时的兜底，不单独新建组件。

**木桩（A1）**。木桩带 `SkillExecutor` 才能被 `YuanqiTargeting` 扫到，和被逆姻缘找到是同一个理由；它没有 owner，走服务器单份路径。头顶读数（`TrainingDummyReadout`）直接读马达，显示冻结和剩余时间。

### 4.6 消耗两招

顺序固定：先下发禁锢、填好表现载荷，再消耗（这样即使消耗过程抛错，核心效果已经生效）。

| 句柄 | 服务器 | owner / 观察者 |
|---|---|---|
| 神足通 `AccelerationRuntimeHandle.Consume` | `EndModifier(Acceleration)` 命令 → `AccelUntilTick = now`（只在加成为正时） | TargetRpc 同样执行；legacy `MovementAccelerationEffect.CancelAndRestore`；`SkillConsumedObserversRpc`：拖尾淡出、背后特效停止、施法者 FOV 提前回落 |
| 缓流 `MovementSlowTrapZoneEffect.Consume` | 关掉并销毁区域组件（`OnDisable` 销毁触发体与调试体） | `SkillVfxReplicator.StopVfxAll(vfxId, zoneEndVfxLingerSeconds)` 停掉区域特效（资产 `vfxId` = `slowtrap_vfx`，余韵 1 秒） |

`YuanqiContext` 在每个 `handle.Consume` 之后调 `board.End(entryId)`，状态板发两次 `Ended`。

### 4.7 表现

表现层这一轮只做接口、不做新特效（已定，2026-10-26）。`Yuanqi_FrostLock` 不画任何东西，美术以后从下面这些接口挂特效，各端都能读到：

| 画面 | 接口 | 状态 |
|---|---|---|
| 地面瞬间结冰 | `SkillExecutor.YuanqiObserved(yuanqiId, payload, localIsCaster)`：载荷带中心、半径 R、秒数 N、目标 id | 接口已留，特效未做 |
| 受害者冻成冰块 | 载荷的 `TargetObjectIds`；每个被冻的 racer 的 `RacerSkillPresentationState.FrozenChanged` | 接口已留，特效未做；冻结期间 Animator 停住（动作层，已做） |
| 冻结期间的 HUD | owner 从 `BuddahPredictedMotor.FreezeSecondsLeft` 倒计时，`SkillSlotUI` 三个槽显示冻结 | 已做（A6） |
| 触发提示 | 施法者的 `LocalYuanqiTriggered` → `YuanqiToastUI` 弹缘起名 | 已做（A6） |

全部表现接口（三对共用）：

- **缘起触发**：`SkillExecutor.YuanqiObserved(yuanqiId, payload, localIsCaster)`，由缘起的 ObserversRpc 发出。`YuanqiPresentationPayload` 带中心、半径、秒数、目标 id，以及水平方向 `Direction`（#4 的突进方向）。施法者自己另有 `LocalYuanqiTriggered`。
- **撞击**：`SkillExecutor.YuanqiImpactObserved(YuanqiImpactInfo)`（`YuanqiId`、`VictimObjectId`、`Point`、`Impulse`），`BuddahBodyImpact` 每记一次撞击发一次。
- **冻结与突进**：`RacerSkillPresentationState`（运行时挂到每端每个 racer 上，`SkillExecutor.PresentationState` 或 `RacerSkillPresentationState.GetOrAdd(racer)`）提供 `FrozenChanged`、`BurstChanged`、`IsFrozen`、`FreezeSecondsLeft`、`IsBurstActive`、`BurstSecondsLeft`。开始立即报告，结束要持续 0.1 秒（`releaseDelaySeconds`）才报告，防止快照抖动。
- **反噬惩罚**：`ObsessionFigure.BackfireStateChanged(bool)`，服务器和每个客户端各发一次，由同步的结束 tick 驱动，中途加入的客户端也会收到一次开始。
- **准星地面点**：`BuddahHandControl.TryGetAimGroundPoint(out Vector3)`（owner，每帧采样一次），配 `IsInsideMouseAimDeadRadius` 和 `LocalOwner`。2026-10-31 起由占位图标 `AimGroundIndicator` 画出（圆环加十字，死区内隐藏），等手部朝向指引设计，见 [README A6](README.md#a6-hud)。

不需要美术的“动作层”直接实现：冻结期间该 racer 的 Animator 速度设为 0，解冻后恢复（各端）；双手停止转动（见 2.6）。#4 突进时施法者自己的相机有一次短 FOV 抖动（`Yuanqi_GiantCharge.casterFovKickDegrees` 8 度，保持 0.15 秒，0.65 秒回落，待试玩调）。

### 4.8 参数

| 字段 | 位置 | 值 | 备注 |
|---|---|---|---|
| `freezeSeconds`（N） | 表 `YuanqiEffectFrostLock`，兜底 `Yuanqi_FrostLock` SO | 1.5 秒 | 阶段 A 占位值（已定，yuanqi.md 文末），试玩后在表里调；控制叠加风险见 README §9 |
| `radiusMeters`（R） | 同上 | 12 米 | 占位值（已定）；缓流区域半径 7.5 米，“较大圆形范围”应大于它 |
| `affectsTrainingDummies` | 同上 | true | 训练模式验证要靠它 |

### 4.9 清理与边界

- 复活 / 重置：`ResetActiveSkillModifiers`（经 `BuddahPredictedModifierStateOps.ClearActiveSkillModifiers`）清 `FreezeUntilTick`；`ResetYuanqiStateServer` 清状态板并把每个实例的“上次触发”清零。木桩没有状态板，服务器控制复位里这一步对它是空操作。
- 比赛结束 / 结算区：`ResultAreaInteractionGate` 已挡施法与推掌；冻结到点自然解除，不需要特殊处理。
- 两次禁锢叠加同一个受害者：期限取较晚的（`ApplyFreeze`，与其他修饰一致），不累加。这是 README §9 “受害者保护”待设计的一部分，先按最简单的来。
- 开场交接期间不会有施法，禁锢不会出现在 handoff 阶段；`IsFrozen` 与 `IsSteeringSuppressed`（开场交接的 `SuppressSteeringUntilTick`）共存时语义相同，没有冲突。
- 纯客户端 reconcile：`FreezeUntilTick` 由 `ModifiersToLocal` 平移（`HandoffClockTests` 覆盖）。

### 4.10 验证清单

训练模式（README 7.3 第一级），结果见 [训练模式验证记录](practice-verification.md) 的 S6、S9：

- 范围内的木桩在第二招执行的那一帧冻住，N 秒后恢复；范围外木桩不受影响。（S9：9.3、9.1 米的冻 1.5 秒，26.7 米的不冻）
- 触发后施法者身上没有加速，身后没有缓流区域，状态板上两条都已结束。（S9）
- 触发后再放神足通：没有缓流在板上，不触发；重新形成重叠，再次触发。（S9）
- 被冻结的施法者：普通施法被打断并退冷却，已判定的反噬照常进入惩罚。（S6）
- 没有覆盖：先神足通、后缓流只验证了触发和再次冻结（S9 重新触发那次），没查消耗；冻结中的木桩头顶读数：`TrainingDummyReadoutText` 有冻结行，但 S9 没有检查读数；复活后状态板为空、缘起能再触发；冻结中被推。

联机（第三级，未做）：远端真人被冻：owner 端立即停下且 HUD 显示冻结；reconcile 不出现“解冻又冻回去”的抖动；两端画面一致；协议版本不一致的客户端被大厅拒绝。

### 4.11 实现顺序（#2 内部，已完成）

| 步 | 内容 | 完成标准 | 落在 |
|---|---|---|---|
| F1 | 马达：`Freeze` + `EndModifier`（以及同批的 `Burst`、`MaxSpeedMultiplier`），附录 A 全部改齐、协议 v2、resolver 与 tickmath 测试 | EditMode 测试通过 | 7ebcc5c |
| F2 | `SkillExecutor.ApplyFreezeToOwner`、动作锁（施法 + 推掌）、legacy 兜底 | 冻结中按技能键和推掌都被拒绝 | 1ca7c50 |
| F3 | 状态板 + `SkillAction` 的虚方法 + 四个句柄 + `StopVfxAll` | 任一技能生效期间板上有 entry；`Consume` 能收掉加速与缓流 | 5dc0e43 |
| F4 | `YuanqiAction` 基类、`YuanqiRegistry` 开局登记、`SkillDatabase.yuanqis` | 配装含两招时登记出 #2 实例；不含时没有 | 5dc0e43 |
| F5 | `Yuanqi_FrostLock` + `YuanqiTargeting`（#4、#1 同批） | 4.10 训练模式清单 | b2031ff |
| F6 | 阶段 B 接表（3.2–3.6） | `DesignTableTests` / `RuntimeTests` 通过，`validation.md` 无错误 | 16121ed |

## 5. 另两对的落点

### 5.1 #4 金足破阵

- **突进**：修饰命令 `Burst`（ValueA = 额外限速，ValueB = 回落秒数，叠加策略 Restart：后到的命令同时覆盖期限和幅度）。resolver 在 Burst 生效期间把 `FinalMaxSpeed` 加上 增量 × 剩余比例²（`BurstShape`，形状写死在代码里，两端按整数 tick 求值，结果一致；不从表读）。命令落地那一刻 `LaunchBodyForBurst` 把水平速度沿车头直接设到新上限（基础 + 各项加成，乘过反噬限速；被定身或冻结时为 0），竖直速度保留，之后 `ClampPlanarSpeed` 每 tick 跟着回落的上限走。`IsBurstActive` = 期限未到且没被定身。
- **撞击**：`Yuanqi_GiantCharge` 在施法者身上挂 `BuddahBodyImpact`（服务器）并 `Arm(M × BuddahHandControl.BasicPushImpulseStrength, burstSeconds, yuanqiId)`。在 `OnCollisionEnter / Stay` 里，只有窗口未过且施法者马达 `IsBurstActive` 时，向碰到的 Buddah（任何带 `SkillExecutor` 的对象，木桩也算）发 `BuddahPredictionRouter.RouteImpulse`（来源 `BodyImpact`），方向是从施法者指向被撞者的水平方向（已定，Q9）；同一次 `Arm` 每个受害者只撞一次（`BuddahBodyImpactRule`）。被冻住的受害者推不动。每次撞击记 `SkillCombatEvents` 的 `hit / body-impact`，并经 `PlayYuanqiImpactObservers` 发表现事件。
- **收尾**：突进前调 `ctx.ConsumeConfigured`，表行 `consumeA = true / consumeB = false` 决定只消耗神足通（与两招在表里的先后无关，见 3.6），所以突进从基础上限起步，不是基础 + 加速；金身不动。`Yuanqi_GiantCharge` 不需要 `OnEndServer`。
- **句柄改写**：不需要改写金身参数，撞击倍率由缘起模块自己持有。
- **表现**：载荷带突进方向；施法者自己的相机有一次短 FOV 抖动（见 4.7）。

### 5.2 #1 金身巨掌

- **持续型**：`mode = Sustained`。`OnTriggerServer` 把 `BuddahHandControl` 的 `IHandsImpulseScale.ImpulseFollowsScale` 设为 true：蓄力弹的冲量乘上生成那一刻给尺寸用的同一个体型采样（`GetPushScaleMultiplier`），不设上限；`OnEndServer`（任一招 `Ended`）设回 false，`ResetSkillInputAndEffects` 也会清掉它。
- **事实与决定**：尺寸、出生点、碰撞体改版前就随体型缩放，和有没有缘起无关。保留这个联动（已定，Q11）；缘起触发时掌的一切都按体型等比例放大，缘起真正新增的是冲量倍率，冲量与尺寸用同一个体型采样，不设上限。
- 没有参数，不建家族表。

## 6. 与 README 7.1 的对接

| 步 | 本文给它的要求 | 状态 |
|---|---|---|
| A1 木桩 | 保留 `SkillExecutor`；木桩没有状态板和登记（`SkillExecutor.SetUpYuanqiServer` 跳过），但是缘起的目标；头顶读数显示冻结 | 已做 |
| A3 节奏 | 去掉全局锁后状态板上才可能同时有两条 entry；闸门的待确认施法检查保证执念先入账再接受下一招 | 已做 |
| A4 反噬 | 反噬不再有效果，也就不登记状态板；统一惩罚的“10 秒锁技能”和禁锢的动作锁是同一个检查点（`SkillCastRules.EvaluateGate`） | 已做 |
| A5 缘起 | 本文第 2、4、5 节 | 已做 |
| A6 HUD | 冻结倒计时、缘起触发提示、选技能界面的“本配装有哪些缘起”（本地用 `SkillLoadout` + `SkillDatabase.GetYuanqis()` 经 `YuanqiCatalog.ForLoadout` 算） | 已做 |
| A7 AI | `AISkillCaster` 直接调 `CastSlotServer`；缘起决策 `AIYuanqiPlanner` 读 `YuanqiRegistry.Registered`（服务器按配装登记的同一份 `SkillDatabase.GetYuanqis()` + `YuanqiCatalog.ForLoadout`）；被冻住的 AI 不需要特殊处理（无扭矩、无施法） | 已完成（ac3d5fe，2026-10-28）：先后、跟进、起手规则见 [AI 技能](../single-player/ai-skills.md) 的“8. 缘起决策” |

## 7. 问题与答复

Q10–Q14 于 2026-10-25 答复，结论已写进对应章节；这里留对照，便于追溯。新问题从 Q16 编号。

| # | 涉及 | 问题 | 答复 | 落点 |
|---|---|---|---|---|
| Q9 | #4 | 被撞者弹开的方向（yuanqi.md 的问题） | 已定：沿碰撞方向往两边弹开，即从施法者指向被撞者的水平方向 | 5.1 |
| Q10 | #2 | 受害者被冻住时，正在前摇的施法怎么办 | 已定：直接取消，打断施法；该槽冷却退还（2026-10-25 补充）。已判定的反噬不被打断，冷却不退，前摇结束照常进入惩罚（2026-10-26 补充） | 2.6、4.5 |
| Q11 | #1 | 金身生效时推掌尺寸已随体型变大，与有无神掌无关，保留吗 | 已定：保留；缘起触发时掌的一切等比例放大 | 5.2 |
| Q12 | #2 | 冻结中受到冲量是推不动还是被推走 | 已定：推不动 | 4.5 |
| Q13 | 全部 | 阶段 A 是否接受协议版本多次 +1 | 已定：接受，但每条命令一次改齐，保证干净 | 2.7、附录 A |
| Q14 | 配表 | `Skill/Yuanqi` 与家族表结构是否按此建 | 已定：预留字段名；表要可延展、可读 | 3.1–3.3 |
| Q15 | #2、#4 | 施法者放神足通后被敌人的缓流命中，减速覆盖了共享的加速槽：这时“消耗神足通”对移动没有作用（减速持续到较晚的期限），#4 的突进从减速后的上限起步。这样可以吗，还是消耗时连同这段减速一起结束？ | 待确认 | 2.5 |

## 附录 A：新增一条修饰期限的改动清单

以 `Freeze` 为例，`Burst`、`MaxSpeedMultiplier` 同样；`EndModifier` 是瞬时命令，没有期限，只走第 2、3、4、8、9、10 项。下面是 0dbc551 上一条期限实际要改的位置：

1. `BuddahPredictedModifierState`：加 `FreezeUntilTick`（以及幅度、时长字段），`HasAnyActive` 加一项。它在 reconcile 线上格式里。
2. `BuddahPredictedModifierCommandType` / `BuddahPredictedModifierCommandData`：加枚举值（显式数字，TargetRpc 按 int 传）与 `CreateFreeze(seconds, source)`。
3. `BuddahPredictedModifierStateOps`：纯状态写入 `ApplyFreeze`（叠加规则）；`EndModifier` 的分支（能被提前结束时）；`ClearActiveSkillModifiers`（技能重置时清）；`PreservePersistentOnReset`（要活过复活 teleport 时，如反噬限速）。补 `BuddahPredictedModifierStateOpsTests`。
4. `BuddahPredictedMotor.Events.TryApplyModifierCommand`：加分支；命令落地时的一次性刚体写入和读接口放在 `BuddahPredictedMotor.SkillState.cs`（`StopBodyForFreeze`、`LaunchBodyForBurst`；`IsFrozen`、`FreezeSecondsLeft` 等）。
5. `BuddahPredictedModifierResolver.Resolve`：加判定与输出（`IsFrozen`，并落到 `IsRooted / FinalTurnTorque / IsSteeringSuppressed`）；`BuddahPredictedMotorComputedStats` 加字段（也在 reconcile 线上格式里）。补 `BuddahPredictedModifierResolverTests`。
6. `BuddahTickMath.ModifiersToLocal`：时间戳加一行平移，时长不平移；补 `BuddahTickMathTests` / `HandoffClockTests` 用例。
7. 诊断：`BuddahPredictedMotor.Shadow` 的 modifier 对比加新的 ComputedStats 标志；`BuddahPredictedMotor.DebugState.SyncModifierDebugState` 与 `BuddahPredictionDebugState` 加字段。
8. 路由：`BuddahPredictionSkillMovementBridge.TryApplyFreeze`；`OwnerMovementEffectApplier.ApplyFreezeServer / ApplyFreezeOwner`（legacy 兜底，或明确记一条“legacy 路径忽略”）；`SkillExecutor.ApplyFreezeToOwner` 与 TargetRpc（追加在类末尾）。
9. 配表登记：`Global/__enums__.ModifierCommandType` 加值（与 C# 同值）；`Movement/ModifierRule` 加行（presentationOnly，写叠加策略与 `resetOnSkillReset`）；`Global/StructuralConstant.prediction_protocol_version` 跟着改。
10. `PredictionProtocol.Version` 加一（同一批未发布的改动只加一次），并在 History 注释补一行。

另外检查马达之外直接读 `_modifierState` 期限的地方：`AISkillWorld`（受控时间估计）就为 Freeze 补了一行。
