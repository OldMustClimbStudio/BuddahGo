# P1 删除预检与验证

阶段基线：P0 `d6439a6`，包含独立联机修复 `be7c0fd`。按用户要求使用 stacked PR，改动后集中执行阶段构建/本机回归，各步骤保留独立提交。

## P1-1

Windows 下 refscan 原本用 POSIX 前缀过滤 os.walk 路径，会漏掉所有脚本；现将比较路径按 os.sep 归一化后再过滤。修复后实际运行脚本的结果如下：

| 类型 | YAML 引用 | 其他 C# 文件引用 | 启动钩子 |
|---|---:|---:|---|
| DebugNetworkTester | 0 | 0 | False |
| DebugRaceFinishButton | 0 | 0 | False |
| DebugLobbyTester | 0 | 0 | False |
| LobbyFlowDebugOverlay | 0 | 0 | False |
| PredictionApiSpike | 0 | 0 | False |
| BuddahPredictionGateAdapter | 0 | 0 | False |
| BuddahPredictionInputAdapter | 0 | 0 | False |
| BuddahPredictionIntroAdapter | 0 | 0 | False |
| BuddahPredictionRespawnAdapter | 0 | 0 | False |
| BuddahPredictionSkillAdapter | 0 | 0 | False |
| BuddahPredictionEventIds | 0 | 0 | False |
| LeaderboardUI | 0 | 0 | False |
| LobbyRoomListItemUI | 0 | 0 | False |

正向对照：BuddahPredictionCombatAdapter 的 C# 引用为 1，保留。额外搜索 Assets 的 scene/prefab/asset/playable/json 未发现待删除类型名绑定。删除文件同时删除 .meta；Testing/Sandbox 的空目录 meta GUID 无其他资源引用，因此一起移除，避免新增孤立 meta 警告。

I6：不适用（没有合并重复实现）。R1/R2：待 P1 阶段集中验证，静态检查不视为编译或运行通过。

## P1-2

预检：MiniMapPresenter 的 YAML/C# 引用为 0/0；MiniMapPlayerLocator、MiniMapWorldMapper 为 0/1，唯一引用方均为同一删除集合内的 MiniMapPresenter。没有启动钩子，Assets 中无三者的序列化类型名绑定。现用 MiniMapController 与该组无依赖且文件未修改。删除三个文件及其 meta。

I6：不适用。R1/R4（小地图）：待阶段集中验证。

## P1-3

预检：LobbyManager 与 ConnectionManager 的 GUID/YAML 引用均为 0；MainMenu 中 `_legacyLobbyManagerPrefab` 为 `{fileID: 0}`。LobbyManager 的 C# 调用来自旧 ConnectionManager 以及本步删除的 GameNetworkManager legacy spawn 分支。NetworkManagerValidator 不引用两者，无需修改。

refscan 对 ConnectionManager 的一个文本命中来自 Plugins/FishyFacepunch/Core/FishyConnectionManager.cs；该脚本编译在 firstpass，`using Steamworks`，继承 Steamworks.ConnectionManager 并 override OnMessage，不能引用 Assembly-CSharp 中的旧 UI ConnectionManager。因此不是待删除类型的调用者，第三方脚本保持原样。LobbyPlayer 仅在本旧链路使用，随其定义文件一并删除。

删除两个脚本及 meta、空 Legacy 目录 meta、GNM 的空 legacy 字段和 spawn 方法/调用；MainMenu 的旧 null YAML 条目保持原样。文档链路同步为现用 GameNetworkManager。I6 不适用，R1/R4 待阶段集中验证。

## P1-4（blocked，保留）

虽然 DebugRaceFinishButton 已删除，RaceMap.unity:6858 仍通过 UnityEvent 直接绑定 `RaceFinishManager, Assembly-CSharp` 的 `TriggerDebugFinishRaceFromLocalUi`，m_CallState=2。该函数继续调用 CanUseLocalDebugFinishButton / RequestDebugChampionFinishServerRpc，因此整个路径仍存活。按照本步骤“Inspector 或 UnityEvent 绑定的方法必须保留”的约束，未修改 RaceFinishManager 或其场景绑定。继续执行无依赖的后续清理。

## P1-5（29 个声明已删除；SetInputSource 保留）

先运行 refscan 确认 8 个 owner 类均存活，所有 owner、GUID、字段和结构布局保留。随后全 Assets C# 名称搜索并区分接收类型；UnityEvent 的 m_MethodName（含 getter 形式）在 scene/prefab/asset/playable 中未找到目标绑定；没有发现外部字符串反射调用。逐项证据：

| Owner | 成员 | 调用证据 |
|---|---|---|
| RoomStateManager | AreAllClientsIntroAssignmentsReadyServer | 仅声明，0 调用 |
| RoomStateManager | AreAllClientsIntroVisualsReadyServer | 仅声明，0 调用 |
| RoomStateManager | AreAllClientsGameplayLiveServer | 仅声明，0 调用 |
| RoomStateManager | AreAllClientsIntroAssignmentsReadyForSequenceServer | 仅声明，0 调用 |
| RoomStateManager | IsWaitingForAuthoritativeGameplayLive | 仅声明，0 调用 |
| RoomStateManager | CanPlayersUseGameplayInput | 仅声明，0 调用 |
| RoomStateManager | ShouldBlockRaceGameplayInput | 仅声明，0 调用 |
| LeaderboardManager | TryAdvanceCheckpoint | 本类 0 调用；PlayerProgressReporter 同名调用的接收者是 LapProgress，保留后者 |
| SkillAction | HasAnti | 仅声明，0 调用 |
| SkillLoadout | RequestSetSlot | 仅声明，0 外部调用 |
| SkillLoadout | SetSlotServerRpc | 唯一调用为同组删除的 RequestSetSlot |
| SkillDatabase | TryGetBalance | 0 调用此 wrapper；SkillConfigRepository 的同名方法及内部调用保留 |
| SkillDatabase | GetResolvedDescription | 仅声明，0 调用 |
| SkillDatabase | GetResolvedIconKey | 仅声明，0 调用 |
| SkillDatabase | TryGetEffectFloat | 仅声明，0 调用 |
| SkillConfigRepository | GetSkillIdsByTag | 仅声明，0 调用 |
| SkillConfigRepository | GetSkillIdsByBehaviorType | 方案 ByBehaviorType 对应的实际名称；仅声明，0 调用 |
| SkillConfigRepository | GetBehaviorType | 仅声明，0 调用 |
| SkillConfigRepository | GetAllParams | 仅声明，0 调用 |
| SkillConfigRepository | GetFloat | 仅声明，0 调用本 repository 方法；TryGetFloat 保留 |
| SkillVfxReplicator | PlayVfxAll(string,float) | 0 调用；SlowTrap 两个调用使用 5 参数重载，保留 |
| SkillVfxReplicator | PlayVfxLocal（2/4/5 参数） | 三个重载均只有声明，无外部调用 |
| SkillVfxReplicator | ForcePrewarmNow | 仅声明及自身日志标签；没有 ContextMenu/UnityEvent 入口 |
| BuddahMovement | IsUsingAutoInput | 仅声明，0 调用 |
| BuddahMovement | RestorePlayerInputSource | 仅声明，0 调用 |
| BuddahMovement | DisableAllInput | 仅声明，0 调用 |
| BuddahMovement | ApplyPushImpulseTargetRpc | 仅声明；PushHitbox 中另一命中是历史注释 |
| BuddahMovement | SetInputSource（保留） | RefreshInputSourceForCurrentControlState 在运行时调用，删除前提不成立 |

使用 Unity 自带 Roslyn 按 owner、名称和参数个数移除精确声明（含文档/属性）；每个修改后文件重新语法解析均无诊断错误。SkillVfxReplicator 未列入删除清单的 ObserversRpc 保留，不额外改变其 RPC 编号。仅移除计划明确列出的 SkillLoadout.SetSlotServerRpc 与 BuddahMovement.ApplyPushImpulseTargetRpc；所有测试/运行端必须同构建。I6 不适用。语法解析不等于语义编译，R1/R4/R5 待阶段集中验证。
