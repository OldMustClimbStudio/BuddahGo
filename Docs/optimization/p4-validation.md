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
