# P2 预检与验证

## P2-1

新增 GameLog、PlayerRegistry、PlayerIdentity、SceneNames/RaceRules，尚无产品调用点。GameLog 的单参数及 context 重载保持现有消息格式，双字符串重载用于新日志标签。两个 Conditional 属性使正式包调用和参数求值一起移除；Warning/Error 不经过此门面。

Registry 的契约是 Awake 注册、OnDestroy 注销；按 activeInHierarchy 过滤，保留 disabled 组件；首次查询补齐其他组件 Awake 尚未执行及关闭域/场景重载的情况，后续调用方保留自己的 owner/scene 条件。P2-4 接入时再逐点确认。

身份预检：三个 GetLocalClientId 除空白外完全相同，两个 ResolvePlayerName 的成员查找、本机 Steam、数字回退顺序相同。新实现接收 connection/Lobby，避免 Foundation 依赖会话单例；本步未替换旧函数。场景名和默认圈数原值不变。I6 的接入 diff 留到 P2-3。

R1：Unity 2022.3.55f1c1 完整刷新后，GameLog 已实际加载到 Assembly-CSharp，未出现 C# error。Console 的 Timings 来自既有 Burst 编译统计。资产 meta 由 Unity 生成。没有改动序列化字段、RPC 或预测结构。

## P2-2a — New_Buddah

替换 27 个 Debug.Log 的调用表达式，参数/context、消息文本及外层守卫逐字保留；Warning/Error 不动。所有 Editor/Dev/shadow/perf/visual/sergate 分支均纳入语法树扫描，参数内无赋值或自增。嵌套调用仅为场景/名单/技能只读查询、tick/时间读取和局部摘要格式化；已读对应实现。Network 的 NetLog.Info 加 Conditional，保留 SteamMP 前缀，避免正式包仍求值参数。I6：不合并日志内容。语法检查通过，R1/R3/R4/R7 待 P2 集中验证。

## P2-2b — Buddah

替换 77 个 Debug.Log 的调用表达式，参数/context、消息文本及外层守卫逐字保留；Warning/Error 不动。所有 Editor/Dev/shadow/perf/visual/sergate 分支均纳入语法树扫描，参数内无赋值或自增。嵌套调用仅为场景/名单/技能只读查询、tick/时间读取和局部摘要格式化；已读对应实现。Network 的 NetLog.Info 加 Conditional，保留 SteamMP 前缀，避免正式包仍求值参数。I6：不合并日志内容。语法检查通过，R1/R3/R4/R7 待 P2 集中验证。

## P2-2c — Network

替换 50 个 Debug.Log 的调用表达式，参数/context、消息文本及外层守卫逐字保留；Warning/Error 不动。所有 Editor/Dev/shadow/perf/visual/sergate 分支均纳入语法树扫描，参数内无赋值或自增。嵌套调用仅为场景/名单/技能只读查询、tick/时间读取和局部摘要格式化；已读对应实现。Network 的 NetLog.Info 加 Conditional，保留 SteamMP 前缀，避免正式包仍求值参数。I6：不合并日志内容。语法检查通过，R1/R3/R4/R7 待 P2 集中验证。

## P2-2d — RaceIntro

替换 52 个 Debug.Log 的调用表达式，参数/context、消息文本及外层守卫逐字保留；Warning/Error 不动。所有 Editor/Dev/shadow/perf/visual/sergate 分支均纳入语法树扫描，参数内无赋值或自增。嵌套调用仅为场景/名单/技能只读查询、tick/时间读取和局部摘要格式化；已读对应实现。Network 的 NetLog.Info 加 Conditional，保留 SteamMP 前缀，避免正式包仍求值参数。I6：不合并日志内容。语法检查通过，R1/R3/R4/R7 待 P2 集中验证。

## P2-2e — UI

替换 8 个 Debug.Log 的调用表达式，参数/context、消息文本及外层守卫逐字保留；Warning/Error 不动。所有 Editor/Dev/shadow/perf/visual/sergate 分支均纳入语法树扫描，参数内无赋值或自增。嵌套调用仅为场景/名单/技能只读查询、tick/时间读取和局部摘要格式化；已读对应实现。Network 的 NetLog.Info 加 Conditional，保留 SteamMP 前缀，避免正式包仍求值参数。I6：不合并日志内容。语法检查通过，R1/R3/R4/R7 待 P2 集中验证。

## P2-3

I6：Roslyn token 逐字比较（忽略空白）显示 GetLocalClientId 三份、ResolvePlayerName 两份、GetSteamIdForConnection 两份、IsHostConnection 两份各只有一种实现。合并前三者；IsHostConnection 保留会话中的 lobby-host 判定及仅在必要时读取本机连接的短路顺序，避免门面强迫提前求值。原调用方的网络单例空链保留，Foundation 只接收连接/Transport/Lobby。显示名按 lobby→有效本机 Steam→Player id 原顺序，原名字清洗与 Host 身份策略不变；数字回退统一为 FallbackName。

6 个场景名默认值改为同值常量，序列化字段留在原类。三个默认圈数位置（RFM 字段、RaceCompletionTracker 常量、PPR 返回值）均为 3，兜底条件保持。未编辑 prefab/scene/asset。R1/R4 待阶段验证。

## P2-4（符合语义的调用点接入，其余保留）

I5 逐点预检：ISM/ICC 查找 active BuddahMovement 为缺少 controller 的对象 AddComponent；MiniMap/LeaderboardTMPUI 为 owner 过滤并保留原 self GetComponent 顺序；LapAddLine 的泛型第一层查询 BuddahMovement 后才回退 PlayerCamera 和 LapProgress。以上五处改用每个调用者独立 List，保留原循环体、owner 检查、回退和未激活过滤。组件 Awake 注册、OnDestroy 注销；不在 OnDisable 注销。FindObjectsSortMode.None 本无排序契约，ISM 后续原有确定性排序仍保留。

与计划不符：LeaderboardManager/MRPC/RFM 实际查询 PlayerProgressReporter；RSM 的两处为包含 inactive 的 RaceBodyIntroStateController；SkillSlot/Obsession/Leaderboard 的技能、执念查询以及 Lap 的后续回退也不以 BuddahMovement 存在为前提。仅移动组件注册表不能证明返回集合一致，按 I5 保留原实现并标 blocked（部分完成）。业务代码显式 FindObjectsByType<BuddahMovement> 已为 0；Foundation 首次查询仍有一次 seed，避免查询先于 Awake/禁用域重载造成漏查，不能声称整个项目调用为 0。

I6：没有合并具有不同语义的查询。待集中 R1/R4/R5/R8 和实际 registry 集合对照验证；没有提前宣称性能下降。

## P2-5a

RoomUI 保留每帧/事件刷新入口，名单签名不变且逐元素 Equals 确认后跳过清空/重建。签名包含人数、顺序、id、SteamId、名字、Host/Ready；读取过 RoomPlayerEntryUI.Bind，三个显示项全部覆盖。对哈希碰撞做精确比较，并跟踪列表 root/prefab/空提示引用、丢失条目，OnDisable 失效。Header/Actions 仍按原顺序每帧刷新。I6 不适用，R 项待集中验证。

## P2-5b

读完整 BlackCurtainViewController/TrackEdgeVisibility：在 SetTrackEdgesVisible 和 BeginTrackEdgeFade 恢复被隐藏对象后刷新 edge/tagged-root/renderer 缓存，淡入淡出期间复用；目标变 null 或 tag/property 字符串改变时重查。保留 active 根过滤、disabled edge 组件、GetComponentsInChildren(true) 和 root/renderer 遍历顺序；Shader ID 仅配置变化时刷新。TrackEdge 日志已在 P2-2b 迁移。已有共享材质写入 B13 不在本重构修复。I6 不适用，R1/R4/R5/R7/R8 待阶段验证。

## P2-5c

PlayerCamera.GetCameraScaleMultiplier 已具备 self→parent→children 缓存及 null 重试，保持原样。Respawn 缓存 self NetworkObject，null 时重试但不缓存 IsOwner 值。ResultArea 门面用弱 key 缓存 self 命中；父/子命中不缓存，保证后续 AddComponent(self) 仍优先，原查找顺序保持。LapProgress 复用静态起点碰撞器数组，起点变更、空集合或元素被销毁时重取；enabled/ClosestPoint 每次仍实时读取。两个碰撞器循环的细微判定差异不合并（I6）。R 项待集中验证。

## P2-5d

LeaderboardTMPUI 在 StringBuilder/插值/Split 之前比较全部显示输入快照，保留刷新间隔、事件订阅和所有格式文本。可见排名使用精确字段比较（原 RankEntry.Equals 的 Approximate 不适合作为文本缓存判据）；外部改写文本也会恢复。重复读取进度合并为同次刷新的一次无副作用读取。ObsessionUI 实际没有字符串拼接，SkillSlotUI 已按 skillId 跳过相同展示，不增加无意义缓存；它们按不同组件查找的 P2-4 例外保留。I6 不合并不同 UI 行为。R 项待集中验证。

## P2-5e

预检读 BuildModifierSummary、BuildPendingSummary 和六个摘要赋值：只生成字符串/DebugState，不改模拟。仅给六个赋值及 writer-relinquished 状态插值加门控。motor.Update 的 RefreshInputBridge、SyncModifierDebugState 的数值状态、UpdateHandoffDebug 内 RefreshLaunchState、ConsumeReady 及 tick 顺序全部原位保留，不能整段跳过。门控包含现有屏幕/verbose/dump 开关、全局 verbose，以及 Overlay 独立 mirrorSummaryToConsole（即使屏幕关闭仍保留镜像文本）。默认开关不改。I6 不适用；R1/R4/R5/R7/R8 待集中验证。

## P2-5f

10 个 LogSceneDiag 调用在构建参数前先检查原有两个开关；已读 BuildServerRaceReadinessSummary/HasOwnedRacePlayer，只有局部容器构建与只读查询。函数自身也保留守卫，并加 Editor/Dev Conditional。状态判断、返回分支和 RPC 不改。I6 不适用；R 项待集中验证。

## P2-5g

读完整 RefreshHealthReport/RefreshRegistry/BuildHighRiskSummary 及其 visual-root getter，除引用缓存与 DebugState 外无模拟写入。LateUpdate 仅在现有诊断开关/Overlay 镜像开启时刷新；Awake 与 public 显式刷新入口保持原样。bootstrap 缺失仍可重新获取。默认调试开关不改，R 项待集中验证。

## 构建与首轮回归（6ae3c72，2026-09-30）

- 集中编译修正了一处新增 NetDebug 命名空间限定后通过；新的诊断属性已实际加载，Console 无 C# error（保留已有 Burst Timings 日志）。修正前构建排队被编译错误拦截，未生成成功产物。
- R2：Development Windows，23.342 秒，0 error / 12 warning，1162.32 MB；实际启动截图确认主菜单，日志无 Exception。
- R3：Release Windows，19.443 秒，0 error / 11 warning，1134.44 MB；实际启动截图确认主菜单，日志无 Exception、无 SteamMP Info、无 D-Perf。
- 对两种 Assembly-CSharp.dll 用 FishNet 自带 Cecil 枚举 IL：Release 的 GameLog.Verbose/NetLog.Info 调用均为 0；Dev 分别为 211/18，证明正式调用及参数求值被编译器消除。
- 原 Editor + Clone 的正常连接、ready、配装、开赛与交接通过，随后有效运行 95 秒，Registry 与引擎查询集合逐次相等，传送前 D-LOC 非零窗口为 0。
- 用户要求扩展为全部六技能及 anti、双方角色、0/100ms 延迟的 48 组合专项，同时检查坐标/命中/画面，不仅检查 RPC。夹具先使用正常比赛流程，再在未保存的本机测试平面上通过正式传送入口安排位置；只在测试实例确定性设置反噬概率和病例间冷却、配装，保留病例内重复施放拦截检查，所有正式 asset 原样。
- 该专项在首个技能施放前的定位传送触发 N5：hof-div=1，真实 handoff EventId=0，shadow 仍为旧开场 EventId=1。host 共 138 条心跳后自动停止；client 随后手动停止。不能记为 R7/技能矩阵通过。
- BuddahHandoffStep.cs 的 origin/dev 与当前 blob 均为 2d7167491daa7b8cc38ac1f944a0d7a58c587d87；静态检查确认其遗漏真实 ConsumePendingTeleportEvent.ResetModifiers 对 handoff/pending 的清除。32 种输入组合在未修复版本有 4 种失败（7/15/23/31）。先独立修复诊断模型，再继续同一矩阵，不隐藏 divergence。

R4/R5/R6/R8/R9 完整判据目前均未完成；性能没有可比结论。阶段暂受 N5 阻塞，后续只在其修复后集中复测。

## N5 修复后的 48 用例回归（74c1ea8）

本机原 Editor + ParrelSync，6 技能 × 正常/反噬 × host/client × 0/100ms LatencySim。每个用例 21 秒，施放后 0.25 秒重试检验锁定/冷却；host 48 个 QUEUE/CAST，每个用例各一个；两端每例各收到一次 CastObserversRpc，各 owner 施放 24 次。全程每次 Registry 对照均相等，无夹具异常、捕获 Error/Exception 或非零 D-LOC。主机 1232 条、客户端 615 条心跳；再来一局、第二局约 20 秒游戏运行、client 投票回房间与清理通过。实际结束时间 05:33:13 UTC。

- Acceleration：正常 force 50→100→50，反噬 50→5→50，两种 owner 和延迟组均观察到。
- SlowTrap：普通对对手施加减速，两端 slowtrap_vfx 跟随误差采样为 0；反噬短暂 root 后 force 60，再恢复 50。用户确认反噬本就没有特效，因此空 vfxId 不是漏配美术，后续独立修复只跳过无效播放。
- BlackCurtain：两端正常/反噬的 seeEdge 标志严格交换；每次都激活并恢复。当前平台夹具不能证明实际赛道路缘画面，后续补实际场景取景。
- Giant：正常 scale 1→4→1、mass 2→6→2、force 50→175→50；反噬 scale 1→0.3→1。owner/observer 都观察到对应变更，不能将此预测路径结果推广到 B3 的 Legacy 路径。
- ReverseTurn：普通仅对手反转，反噬仅自身反转；注意病例边界会短暂保留上例的客户端旧采样，判据在本次施放后窗口中取值。
- PushProjectileHands：普通实际命中对手；反噬 5 弹能命中自身和对手，4 组角色/延迟均记录到服务端 hit 集合。发现 N6：反噬视觉使用各端重算的起点，示例 server z=182.537445 与 client z=174.201874，相差 8.335571；普通弹体的同一发 start/direction 精确相同。故 R5 不能宣称所有视觉一致，须先独立修复。
- 用户追加的全屏变亮/相机反馈：在最后一组客户端施放中采样 Reflection 对应的实际 Volume，普通 Bloom 峰值约 4、反噬约 5、Tint 约 100，随后均归零；观察者保持 0。普通弹体技能本地 FOV offset 0→30→0。MMF 配置包含反噬 CinemachineImpulseSource。此采样从 case 37 才开始，未覆盖全部 owner/技能；后续重构回归增加完整采样，不以配置存在替代运行结果。

RoomUI 补充检查：真实两人名单，隔离的 inactive UI 夹具连续刷新 10000 次，两个条目的 instanceId 均不变；销毁条目、改 parent 后均重建正确。GC.GetAllocatedBytesForCurrentThread 的 Mono 计数器在 1MB 校准分配上也返回 0，故不采用其“0 allocation”读数。R8 缺少同场景可比基线，仍未验证；没有声称性能改善已实测。

R7 完成。R4/R9 验证了本机流程、开赛、技能、结算按钮、再来一局和回房间，但未跑真实三圈或真实 Steam 跨账号。R6 验证了本矩阵的普通/反噬弹体命中，尚不是全部物理交互。所有资源值保持；Unity 自动删除的两项孤立 meta 恢复。原始日志和截图保存在本机 Logs/p2-skill-matrix 及克隆同路径，未提交原始日志。
