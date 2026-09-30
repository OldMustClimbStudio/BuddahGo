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
