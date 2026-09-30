# P4 会话与比赛流程拆分

## P4-1 预检与重置矩阵

7 个普通集合（4 set、3 dictionary）移入 RaceStartHandshake；SyncList/SyncVar、序列化字段、RPC 原声明与顺序不变。RoomRoster 直接引用原 SyncList 的 IList 接口，保留遍历、插入、ready 复位、摘要和姓名处理规则。身份解析继续使用 Foundation PlayerIdentity；没有缓存名单副本。

| 入口 | managers set | assignment set | visual set | gameplay set | assignment dict | visual dict | gameplay dict | 其他 |
|---|---|---|---|---|---|---|---|---|
| BeginRacePreparationServer | 清空 | 清空 | 清空 | 清空 | 清空 | 清空 | 清空 | 原旗标与评估顺序 |
| CleanupMatchSessionButKeepRoomServer | 清空 | 清空 | 清空 | 清空 | 清空 | 清空 | 清空 | 先清配置 cache、调用 ResetRaceFlow，再重置 ready |
| ResetRaceFlowStateServer | 保留 | 清空 | 清空 | 清空 | 清空 | 清空 | 清空 | StopRaceCountdown 在先 |
| ResetPlayersReadyStateForRoomReturn | 保留 | 保留 | 保留 | 保留 | 保留 | 保留 | 保留 | 仅 host ready=true，其余 false；仅变化项写回 |
| CompleteReturnToRoomMenuServer | 保留 | 清空 | 清空 | 清空 | 清空 | 清空 | 清空 | 通过 ResetRaceFlow，再改 phase/summary |

RemovePlayer 仍先找到并移除名单项，然后仅从全部 7 集合移除此 id。OnStopClient 与本地 scene-ready 初始化只复位本地 report 标记，保持原样。其他调用 ResetRaceFlow 的入口沿用同一矩阵；重复 clear 未被优化掉。

RoomDiagnostics 仅移动摘要与探测的方法体，原日志门控、IncludeInactive 查询、IsSceneObject/IsSpawned 的区别、Unity null 和 getter 求值位置不变。I6：两处 7 集合 clear 完全相同，6 集合 clear 和按 id remove 为不同具名函数。

R1：主 Editor 编译成功，0 新 C# error/warning；R4/R9 待 P4 阶段集中验证。

## P4-2 三件套参数化

I6：3 个本地 report 仅 marker/RPC 名不同；共享 client/负序号/重复序号检查与 marker 更新。3 个 RPC 共享 caller/auth 检查、set.Add→dictionary 赋值，保留各自之后的日志、assignment readiness 评估、gameplay 解锁逻辑。没有新增负序号 RPC 检查。实际只有 2 个 AreAll sequence 查询，共享 server/负序号检查与遍历，空名单仍返回 true。public/RPC 名称、签名、声明顺序不变。R4/R9 待集中验证。

## P4-3 配装规则下沉

I6：两份提交完成推进块逐字比较，仅日志内容及门控不同；共享函数保留 StopAllCoroutines→countdown=false→seconds=0→Raise→各自日志→Advance→return，未全员完成仍只 Raise。验证/完整性/自动补全下沉为 LoadoutRules，策略 getter 仍在原循环位置求值。

保留输入数组原地 Trim（失败时保留已处理前缀）、空槽与重复策略；option 候选原样不 Trim，配置与本地 fallback 才 Trim；原候选顺序、序号比较和 cache 重建调用点保持。没有修改 RPC/Sync/序列化布局。

R1：P4-1–3 主 Editor 编译无新增 C# error/warning。48 个辅助规则检查全部通过（7 集合的三种清理模式、序号 guards、名单 ready、合法/非法/空槽/重复技能、自动补全顺序）。完整 R4 待两轮与断线专项。

## P4-4 结算文件分区

仅给 PlayerProgressReporter 的 5 个 server 入口、进度依赖、5 个结算 RPC/本地演出添加 region。去除 region/空白后源码 token 完全相同；RPC/UnityEvent 名称与顺序、Update 重复计算未变。R4 随阶段回归验证。

## 阶段运行验证（0d56d02）

2026-09-30 06:53 UTC，两端主流程完成：真实 ready→全员提交配装→开赛→95s 普通/100s 100ms 延迟→场景 EndMatch UnityEvent→再来一局→客户端故意不提交配装→资源配置的 60s 超时自动补全→第二轮开赛→客户端投票回房间。两端各施放 6 次；336/166 条心跳全部 loc/tel/mod/hof div=0，未采集到 Exception 或 Unknown vfxId。主 Editor 与 clone 均编译成功，48 项辅助规则检查各自通过。

两端首轮配装完全相同。第二轮 host 已提交配置保持 slowtrap/blackcurtain/push_projectile_hands，client 自动补成 acceleration/slowtrap/blackcurtain，两端 cache 一致。回房间后两端名单 host ready=true/client ready=false，go/movement/waiting/countdown 全 false。服务器 cacheCount=0、客户端仍为 2；原清理入口只在服务器执行，客户端沿用原选择场景退出时保留缓存的逻辑，本步未改清理时机，不把它报告为两端均已清空。

2026-09-30 06:57 UTC，独立断线专项完成：客户端在技能选择开始约 3s 调用真实 ClientManager.StopConnection。服务器约 0.12s 后 Participants 仅剩 host，已移除离开者选择记录；保持原计时器而不新增立即推进。60s 到时自动补全后进入 RaceMap，约 100.77s（含开场流程）解锁 gameplay，仅有 host 名单/配装，最终 completed=true。

R4/R9 是本机 Tugboat + ParrelSync 两 Editor 的替代验证；未验证外部 Steam 大厅、跨机器连接、Dev Build 对端以及实际跑满三圈，因此各 P4 步骤仍为 done-unverified。结算调用的是真实场景绑定按钮，不是绕过状态机直接改 phase。

本机原始证据：两端 Logs/p4-regression-*.jsonl、Logs/p4-disconnect-*.jsonl；辅助检查源码 Tools/Validation/session-helper-cases.cs.txt。
