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

## P3-4 编辑器交叉校验

现有菜单接入 SkillConfigCrossValidator，按显式关联配置检查 SkillDatabase 的三张原始 action 列表与 config definition；比较 cooldown/castLock/obsession/anti id；逐项报告 config→SkillAction→后缀推断→无反噬的解析结果，并检查目标 id 实际存在。读取 vfxId 和库 prefab；空 id 仅为 Info（符合用户对 SlowTrap anti 的确认），未注册非空 id 为 Warning，并注明字段是否被执行路径使用仍需检查。effectParams 只匹配同名序列化字段，不猜别名。无显式配置关联时标出候选比较，不声称它必是运行时数据源。

菜单不显式调用运行时配置初始化/重建，不 ApplyModifiedProperties/SetDirty/保存资源；但 LoadAssetAtPath<SkillDatabase> 会触发 OnEnable，因此不能宣称加载过程没有运行时配置初始化副作用。已知 PushProjectileHands action 18/15 versus 配置 12/10 仅记录，D5 仍未决定，不自动改数值。R1/实际菜单报告与资源只读检查待执行。

## 首次集中验证

新文件需要完整 AssetDatabase 导入；第一次仅 scripts 刷新时出现 helper 尚未导入的 CS0246，随后 scope=all 导入后编译通过，无需源代码修正。主 Editor 的 12 个 SkillAction JsonUtility 序列化快照与 P3 前逐字相同，asset/prefab/scene diff 均为空。

实际执行 ValidateAllDatabases 菜单入口：errors=0、warnings=6，共 19 条交叉信息。两条数值漂移为 PushProjectileHands cooldown 18 vs 12、castLock 15 vs 10，留给 D5；另四条为 Acceleration/ReverseTurn 正常和反噬声明的旧 vfxId 没有库项，这些 action 的现有执行路径并不通过 SkillVfxReplicator 播放它们，不能据此认定运行时缺特效。SlowTrap anti 空 id 作为 Info 保留。菜单前后未保存或修改资源。

P2 全部警告复核还包含已有未配置的可选 Feel 事件 acceleration_local、acceleration_anti_observers、blackcurtain_observers、blackcurtain_anti_observers，以及 Animator/kinematic body/字体警告；它们与已配置且实测播放的 Reflection 全屏反馈不同。重构不凭空创建或替换这些可选美术反馈，后续报告保留此限制。

## 48 用例双端回归（694daed）

2026-09-30 06:17 UTC 完成。6 种技能 × 正常/反噬 × host/client 施法 × 0/100ms LatencySim，共 48 用例。每例服务器 QUEUE/CAST 各 1 次，两端观察者各收到 1 次，重复施法被冷却阻止。随后通过场景现有 EndMatch UnityEvent 进入结算，完成再来一局、第二轮开赛、客户端投票回房间。host/client 各施法 24 次，1236/617 条 D-LOC 心跳的 loc/tel/mod/hof div 均为 0；注册表与引擎查询无差异，采集无 Error/Exception。

全屏与相机：48 例施法者 Bloom 采样峰值正常 3.665–3.997、反噬 4.551–5.000（采样可能错过精确峰值）；另一端本地 Bloom 为 0，17 秒后均回到 0。全部反噬的本地 CinemachineImpulseSource 播放计数恰好增加 1，正常技能该计数不增；普通蓄力手掌的四种 owner/latency 组合 FOV offset 达到 30，后续恢复。黑幕在实际赛道截图中正常施法者可见白色边线、其他人不可见，反噬交换该关系。Shared/Local Reflection 入口、资产与反馈保留。

N6 修复回归：8 个普通/反噬弹体用例中，服务器、host 视觉、client 视觉的起点/方向/速度集合全部精确相同；正常 1 个、反噬 5 个视觉弹体，无重复。普通均命中对手，反噬均记录双方命中。网络传输仍带来时间上的播放延迟，这不等于任意墙钟帧位置完全重合。SlowTrap anti 空 VFX 不再报 Unknown vfxId。

**新发现 N7，R5 尚不标为完全通过**：进一步检查数值持续时间发现 SlowTrap anti 的服务器定身约 1 秒后为 force=60/speed=83，再恢复 50/80；客户端在该 21 秒用例内一直 rooted，未进入加速段。P2 原始记录同样存在，非 P3 引入。FishNet LocalTick 明确不跨客户端同步，而 modifier deadline 随 reconcile 直接从服务器复制，疑似时基错配。按用户技能正确性要求另开 fix PR 处理并复测，P3 重构不混入修复。完整三圈/外部 Steam 双机与普通非蓄力推击的完整 R6 矩阵仍未执行。

原始日志与截图：两端 Logs/p3-skill-matrix；全屏采样 Logs/p3-feel-samples.jsonl；分析器 Logs/audit-p3-presentation.py。R1 已通过；R7 与本机重开/回房间通过；R5 因 N7 保持 blocked。P4–P6 继续前先定位并修复 N7。
