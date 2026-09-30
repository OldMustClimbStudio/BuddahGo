# P3 技能系统验证记录

## P3-1 公共模板

预检逐字比较了 14 个 `vfxDurationSeconds > 0f ? vfxDurationSeconds : fallback` 和 10 个 `PlayFeelLocalTimed(start, stop, duration, skillId + "_observers")`。fallback 全为只读字段/局部值的加法或直接读取，未包含副作用。抽为受保护的 ResolveVfxDuration / PlayObserverFeel；保留每个调用点原来的执行顺序、duration 值、空值语义和序列化字段。

Reflection 的普通/反噬 Shared 与 Local 预施放反馈仍由 SkillExecutor 原入口触发；本步不改变 Bloom、白平衡、Cinemachine 震动、FOV 或任意资源配置。git diff --check 通过，R1/R5 按用户要求在阶段修改后集中执行。

## P3-2 保留序列化声明类的共享实现

I6 逐对 diff：BlackCurtain 两类的摄像机查找、屏幕中心、边缘判定与 Play 参数完全一致；差异是日志前缀、末句 player/caster 和默认 Feel id。仅前述共同行为下沉到 BlackCurtainPresentation，日志前缀参数化，默认值、末句与 Feel 调用顺序保留。Giant 共用 AddComponent/ApplyOrRefresh 和 children(true)→parent 的 camera reset；normal 使用 grow/shrink，anti 使用 shrink/restore，显式作为参数。normal 的 OnEnable/OnValidate EnsureAntiSkillId 和 server mass/force 倍率仍独有。ReverseTurn 的全体其他人 versus 自身目标不合并，其 duration/Feel 重复已由 P3-1 消除；Acceleration/SlowTrap/PushProjectileHands 的正常/反噬机制不同，保持各自实现。

直接改变 Anti 继承会把相同字段迁到父类而违反 I1。用户明确优先保留功能，因此保留所有原继承、字段声明、GUID、默认值，改用普通方法共享，无 asset 迁移。R1/R5/R10 待集中验证，变更前已保存 12 个 SkillAction 的实际序列化快照用于逐值核对。

## P3-3 SkillExecutor 下沉

I5 预检：Obsession 为 self→parent→children(true)，另外四个为 self→children(true)→parent；各自非空缓存守卫保留，共用带顺序参数的查找方法。OwnerMovementEffectApplier 接收 executor 外壳，四个 Apply server 入口仍先 server guard，再 bridge，后 Owner/null/TargetRpc；RPC 签名、声明位置及 Target 内 server/owner 分流保持，Legacy GetComponent/AddComponent、Feel/trail/scale 顺序不变。

FOV IEnumerator 移到普通 SkillCameraFov；仍由 executor 启动/停止，通过 getter 每次读取当前 playerCamera，保持 Unity null、Time.deltaTime、WaitForSeconds、所有提前退出和仅自然完成时置空 routine 的行为。ResetActiveSkillEffectsForOwner 清单原样。

CastSlotServerRpc 按原语句边界拆为 TryResolveCast、ResolveCastVariant、QueueCast。按 gate→slot/ref/id/lookup→Time.time→cast lock→cooldown→obsession/唯一随机 roll→delay/lock/cooldown/obs gain→feedback RPC→coroutine 的顺序；没有提前计算随机或变更日志。I6 没有合并四种 movement 不同的效果语义，只移动方法体。R1/R5/R6/R7 待阶段集中验证。
