# 已退休的玩法、网络、UI 与编辑器代码

特效相关的退休记录在 [../vfx/retired.md](../vfx/retired.md)。这里记录的是特效以外的代码：每一项写明原来做什么、为什么能删、在哪个提交删的、要等什么才能删。

状态有三种：

- **已删**：已经从工程里移除。
- **待删 [PR#63]**：确定没有用了，但所在文件在 PR #63（`feat/mechanics-update`）的改动范围里，等它合并后再删。
- **待定**：需要用户先做决定。

审查依据见 [../code-review/02-code-review.md](../code-review/02-code-review.md)，下面写的 A #n、B #n 是那份文档里删除表的编号。

## 1. 2026-11-03 代码审查清理（refactor/vfx-refactor，本地提交，未 push）

删除前的核实方法：先用符号扫描找出只在声明处出现过的名字；再用 `.cs.meta` 的 guid 在场景、prefab、asset 里搜引用；再搜 YAML 里的 `m_MethodName` / `functionName`；最后检查测试里的反射调用。删除的 RPC 会让所在 NetworkBehaviour 的 RPC 编号改变，所以客户端和服务器必须使用同一个构建。大厅已经用 `Application.version` 加 `PredictionProtocol` 做了版本校验，正常发包不会有问题。

| 内容 | 状态 | 原来做什么 / 为什么能删 | 提交 |
|---|---|---|---|
| `RoomStateManager.RequestReturnToRoomMenuKeepingSession`、`ReturnToRoomMenuKeepingSessionServerRpc`、无参 `ReturnToRoomMenuKeepingSessionServer()`、`PlayersSummaryText`、`RequireAllClientsReady`、SyncVar `_playersSummaryText`、`UpdatePlayersSummaryText`、`RoomRoster.BuildSummary`（A #1–#5） | 已删 | 由客户端发起"回房间菜单"的 RPC，而且没有校验发起方是不是房主；另外每次名单变化都会把一段摘要字符串同步给所有人。两者都没有调用方或读者。真正的回菜单流程走 `ResultDecisionManager` 调用的有参重载 | `4309977d` |
| `SteamLobbyManager.OnMemberJoined/OnMemberLeft/OnLobbyListRefreshed/OnDebugLog`、`PublishDebugLog`、`UpdateCurrentLobbySettings`；`GameNetworkManager.OnRemoteClientStateChanged`（A #6–#9） | 已删 | 供旧调试 UI 和旧大厅界面用的事件和日志转发，现在全工程没有订阅者 | `5d03b3d5` |
| 结算流程的死成员（A #10、#11、#19–#22、#24–#30）：`ResultPresentationTimelineBridge` 的 4 个事件、5 个属性和两个没接线的信号；`PlayerProgressReporter.EnterResultAreaServer` / `SyncResultAreaTeleportObserversRpc`；`RaceCompletionTracker`、`RaceFinishManager` 的只读属性和 `resultSceneName`；`ResolvedPropertySelectionCache` 的 MatchScene 与选择字典；`PropertiesSelectionManager.AllowDuplicateSkillSelections/GetFilledSkillLoadoutSlotCount`；`ResultDecisionManager`、`ResultAreaInteractionGate` 的属性；`RaceResultAreaManager` 的放置标记；`LocalFinishPresentationTimelineBridge` 的死属性和信号；`EndFieldSpawnPoint.debugLabel` | 已删 | 早期结算流程留下的状态和接口，只写不读，或者完全没有调用方。RaceMap 只接了 Reached / Complete 两个时间线信号；结果区传送现在只走 hidden 那条路径 | `eeb9cec9` |
| `IntroClientController.CompleteGoSequence`、`IntroPath.EvaluateRotation/GetTerminalPosition/GetTerminalForward`、`IntroSlot` 的三个属性和 `GetLaunchForward`、`SplineIntroPath.FindNearestT`、`IntroTimeUtility.HasReachedIntroStart`（A #13、#15–#18） | 已删 | 开场早期的姿态和时间辅助方法，没有调用方，也没有 override | `f6af7812` |
| `IntroSlot` 的序列化字段 `visualStyle`、`rootPositionOffset`、`rootEulerOffset` | 待定 | 读这三个字段的属性已经删了，现在没人读。RaceMap 里存着它们的值，删字段会在下次保存时丢掉这些数据（不影响功能）。要不要一起退休，请用户定 | — |
| `BuddahPredictionCommandBus` 的 Teleport / Modifier / Handoff 通道、`TryEnqueue*`、`Target_Enqueue*` RPC，`Events/Payloads/TeleportCmd.cs`、`ModifierCmd.cs`、`HandoffCmd.cs`；`BuddahPredictionEventChannel.TryDequeue/BuildPendingSummary/NextSequence/LastConsumedId`；`BuddahPredictionHandoffBridge.GetPredictionControlState/IsAuthoritativeLaunchHandoffConsumedOrActive`；`BuddahPredictionVisualRootBridge.GetVisualScaleMultiplier`；`BuddahPredictionBootstrap` 的三个属性（A #31–#35） | 已删 | 预测重构 Phase 2 的指令总线脚手架。teleport 和 handoff 实际走 motor 自己的队列，这三个 RPC 从来没有发送方。Impulse 通道和 A4 探针 `DebugA4FireTestImpulseToOwner` 保留 | `70764650` |
| `SelectionRuleRepository.HasAnyRules`、`LapProgress.HasStartedLap/TotalProgressPercent`、`TrackSplineRef.Progress01AtT`（A #38、#40、#42） | 已删 | 没人调用的便捷属性和方法 | `1298976f` |
| `Network/Match/PlaceholderPlayerController.cs`、`Assets/Editor/MatchScenePlaceholderSetup.cs`（A #43） | 已删 | 最早的联机原型：一个占位玩家控制器，加一个生成最小比赛场景的菜单工具。它生成的 `MatchScene.unity` 和 `PlaceholderPlayer.prefab` 早就不在了；两个 guid 在资产里都是 0 引用。运行这个工具还会替换掉当前场景并改写 Build Settings | `b96cf4b3` |
| `SplineProgressTracker.IsClearlyWrongWay`、`DesignTableSet.IsExported(DesignSheet)`、`SkillReleaseProto` 原型表里的 `skillId`（B #4、#16、#18） | 已删 | 分别用于按 forwardDot 判断逆行、判断某张表是否导出、在原型表里记录技能 id；全仓都没有读取方 | `ace0680b` |
| `SceneFadeController.QueueFadeOutOnNextSceneLoad`；`SkillWallSelectionController` 的 `RebuildFromManager/RebuildWall/TryReplaceSlot/HandleItemInspectRequested/SelectedSkillIds/PlayRejectedSelectionFeedback` 和 `ResetView` 的参数；`SkillInspectController.RequestToggleInspect/CurrentItem`；`SkillInspectableItem.CreateInspectVisualInstance/SanitizeInstance/AllowInspect/SkillId` 以及字段 `visualRoot`、`inspectVisualPrefab`；`SkillWallItemView` 的 6 个属性和 3 个字段；`PropertyGroupView.PropertyKey`（B #19–#24） | 已删 | 旧"选技能墙"交互留下的入口：按名字重建墙、替换槽位、从墙上直接进入检视、复制检视模型。现在检视走 MenuInspect 输入，这些方法没有调用方，或者只互相调用。`技能绘马.prefab` 里两个字段的值会在下次保存时丢掉（不影响功能） | `18aa33ae` |

## 2. 确定没用、等 PR #63 合并后再删

| 内容 | 原来做什么 / 为什么能删 |
|---|---|
| `IntroSequenceManager.HandleTimelineIntroStartSignal/HandleTimelineGoSignal/HandleTimelineCountdownSignal/PlayTimelineIfAssigned`（A #12），以及只被它们调用的 `IntroClientController` 三个空 handler | 没接线的时间线信号 |
| `RaceBodyIntroStateController._predictionHandoffBridge`（A #14） | 只写不读。`IntroHandoffContinuityTests.cs:44` 用反射写它，要一起删掉 |
| `BuddahPredictedModifierCommandData.CreatePushGrace/CreateSteeringSuppression/CreateRoomBypass`、`BuddahPredictedModifierState.HasAnyActive`、`SkillConfigRepository.GetDescription`、`ObsessionFigure.DrainPerSecond/ExtraRecoveryPerSecond`（A #36、#37、#39、#41） | 没有调用方的工厂方法和属性 |
| `PlayerCamera.SetRuntimeOffset/DampenRuntimeOffset/DampenRuntimeFieldOfViewOffset` 及 `_runtimeOffset` 一组（B #1） | 0 调用方，`_runtimeOffset` 恒为零 |
| `SkillExecutor.RequestResetActiveSkillEffectsForRespawn` + `RequestResetActiveSkillEffectsServerRpc`（B #2）、`SkillLoadout.SetSlotServer`（B #3） | 0 调用方 |
| `BuddahRespawn` 两个没人读的参数（B #5） | 方法体不读 |
| `BuddahHandControl` 的非蓄力投射物路径 + `HandPushProjectileRuntime.cs` 整个文件（B #6、#7，约 470 行） | 唯一的调用方总是传 `useChargedProjectileRuntime = true`，这条路径不可达。`Docs/vfx/retired.md` §4 说它"还在用"，这句要一起改 |
| `AISkillRacerSnapshot` 的 4 个只写字段与 `AISkillWorld` 的写入（B #13、#14），`DesignTableDomainRules` 的两张旧表回退分支（B #15），`DesignSheet.SourcePath`（B #17） | 只写不读，或者是已不存在的表 |

## 3. 等用户决定

- URP 示例代码：`SharedAssets/Benchmark/BenchmarkShortcutManager.cs` 会在正式游戏里自启，`SharedAssets/Scripts/Editor/DirectionalLightmapperCorrector.cs` 会改写 RaceMap 的方向光照贴图。这两个文件随 URP 示例目录（refactor-plan P7）一起处理，见 [../code-review/03-assets.md](../code-review/03-assets.md)。
- `Legacy/ResultDecisionUI` 与 `ResultDecisionPanel`：面板从 2026-04-08 起一直没激活，联机投票 UI 从来不显示。这是 bug 还是有意关掉的，需要用户确认（02 B §5.2）。
- `Match/Lobby.defaultVisibility` 列：读进来以后没有任何地方用，要接到建房 UI 还是从表里退休这一列，需要用户定（02 A D1）。
