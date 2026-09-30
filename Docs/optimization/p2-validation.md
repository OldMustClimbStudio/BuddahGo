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
