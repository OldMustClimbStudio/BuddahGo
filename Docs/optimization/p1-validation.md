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

## P1-6

预检：IBuddahInputSource 仅有 PlayerBuddahInputSource 与 DisabledBuddahInputSource 两个 sealed 实现，UseDirectHeadingControl 都是字面量 false。三个 directHeading 序列化字段只被不可达的 ApplyDirectHeadingControl 使用，外部只有 Buddah.prefab 的旧值，无配置/反射调用。

移除该方法、不可达分支、RotationMode 私有枚举/字段/赋值；TorqueSteering 的恒真条件简化，现用施力、扭矩、衰减和调用顺序保持原样。按 I1 的“确认无用字段”例外删除三个专属字段，prefab 的旧 YAML 值不改写。输入接口及两个实现保持原样。Roslyn 语法解析通过；I6 不适用，R1/R4 待阶段集中验证。

## P1-7（四包移除，SoftMask 保留）

在 Assets 源码/asmdef 中未找到五个目标包的调用，但进一步用 PackageCache 的真实 GUID 检查了 4,811 个 Assets 序列化/元数据文件，发现 SoftMask 是活跃依赖：RaceMap.unity:5581/5789 引用 UI/UIprefap.prefab（GUID b3daf86579f2d46419a926f54d2c5d7c），该 prefab 使用 SoftMask 包脚本 GUID 385b7d1277b6c4007a84c065696e0f8c / 97bc2ebab6563400c95b036136d26ea6。两个 UISoftMaskProjectSettings asset 也引用包资源。因此保留 com.coffee.softmask-for-ugui 及资源，标记该部分 blocked。

其余四包（visualscripting/collab-proxy/ide.vscode/probuilder）的 GUID 集合分别为 1,840/927/16/832 个，Assets 中引用数为 0；没有其他保留包依赖它们。移除其 manifest/lock 条目。ParrelSync 固定到原 lock 的 610157ad762084380380148ba8ce14e266a6da97，实际版本不变。JSON 解析/依赖闭包检查通过，R1/R2/R4 待阶段验证。I6 不适用。

## P1-8

预检使用 --no-ignore/完整目录扫描，覆盖 gitignore 中已跟踪的 Demo 资源。Demo 及目录 meta 共 187 个 GUID；目录外的真实资源引用仅来自 DefaultPrefabObjects。外部代码命中只有 validator 的 Demo 排除路径和 FishNet 的 friend-assembly 名字常量，未发现运行时 Resources/路径加载或类型消费者。306 个目录内文件全部已跟踪，没有未跟踪用户文件。

当前默认表为 15 项（12 Demo + 3 游戏），与方案中的约 30 Demo 数字不同，按实际 GUID 清理。删除 Demo 目录及 meta，并手动只移除已识别的 12 条 Demo 引用，保留以下条目的原 GUID/fileID/顺序：

| 游戏 prefab | GUID |
|---|---|
| Assets/Character/MenuPlayer.prefab | 7a09426a99e26dc4d8f9a0b402532397 |
| Assets/UIMenus/Lobby/RoomStateManager.prefab | 054af5abe930c124e9c745e84e9e4e1c |
| Assets/Character/Prefab/Buddah.prefab | c2b8c569b26585545904610506c355cd |

已读 NetworkManager.Awake → SpawnablePrefabs.InitializePrefabRange → ManagedObjects.InitializePrefab：运行时会根据新表赋 PrefabId，所有端须使用同一表/构建。场景对象 sceneId 不改动。I6 不适用，R1/R2/R4/R5/R6 待阶段集中验证。

## 阶段集中验证（2026-09-30，71e63d5）

- R1：重启编辑器并完成包解析后，C# 编译 0 error、10 个已有 warning（移除 Demo 后比基线少 1 个）。旧进程残留的 PlasticSCM/VisualScripting 类型加载问题通过重启解除。现有字体缺字、空动画、LightingData 告警仍保留。
- R2：Development Windows 构建成功，53.172 秒，0 error / 17 warning，1162.31 MB；实际启动到主菜单，启动日志无 Exception。
- R3：非 Development Windows 构建成功，28.893 秒，0 error / 13 warning，1134.47 MB；实际启动到主菜单，启动日志无 Exception、无 D-Perf 探针输出。
- 本机集中回归：原 Editor host + ParrelSync Editor client，临时未保存 Tugboat 127.0.0.1:17845；两端均为本阶段程序集和 3 项 prefab 表。正常连接、ready、属性/技能选择、RaceMap 倒计时和控制权交接；两端各施放 6 次技能。首轮有效比赛约 214 秒，95 秒后启用 100ms LatencySim；通过场景已有 UnityEvent 结束按钮进入结算，两端投票再来一局，第二轮再次正常交接，之后由 client 投票返回房间，两端均到 MainMenu/InRoom 并完成 10 秒观察。
- R7：host 336 / client 167 条 D-LOC 心跳，loc/tel/mod/hof 的非零 div 窗口均为 0，FATAL、SceneId、Exception 匹配均为 0。两端结果 completed=true、secondRaceSeen=true、returnedToRoom=true；测试结束后均停止 Play。
- R10 补充：两个结算入口均通过实际绑定 RaceFinishManager.TriggerDebugFinishRaceFromLocalUi 的 Button.onClick 执行；证明 P1-4 所保留的绑定仍可用。DefaultPrefabObjects 保持全部 3 个游戏 prefab 的原 GUID/fileID/顺序，无额外生成差异。

验证边界：这是两个 Editor 的本机辅助回归，未执行 Steam 双账号/Dev host 的完整 R4，也没有真实跑完 3 圈；结束使用已有调试按钮。R5 未逐一人工确认全部 anti、反噬与冷却表现；R6 的完整复活/碰撞/推击/普通、蓄力、连发矩阵未执行。小地图显示未单独做可视化断言。R8 没有可比基线，不宣称性能改进。R9 验证了 100ms 下比赛、结算和后续流程，未覆盖正式 R4 全部环节。

本机原始证据（不提交生成日志）：Logs/p1-editor.log、p1-dev-player.log、p1-release-player.log、p1-regression-host.jsonl；克隆 Logs/p1-client-editor.log、p1-regression-client.jsonl。以上结论补充各步骤当时记录的“待阶段验证”，不把未覆盖项记作通过。
