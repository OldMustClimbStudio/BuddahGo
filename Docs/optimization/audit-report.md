# BuddahGo 代码与仓库评估报告

- 基线：`dev` @ `ce5c1c2`（2026-09-29）。文中行号均以该提交为准；代码移动后按符号名重新定位。
- 方法：静态阅读全部自研脚本（`Assets/Scripts/`），按 `.meta` GUID 扫描 `.unity/.prefab/.asset` 引用，统计 C# 交叉引用、日志、单例与逐帧调用；抽查结论回到源码核实。未在 Unity 中运行。
- 本文是**证据**；执行方案见 [refactor-plan.md](refactor-plan.md)，执行入口见 [HANDOFF.md](HANDOFF.md)。

## 1. 结论

| 维度 | 评分 | 说明 |
|---|---|---|
| 项目阶段 | Alpha / 纵切片 | 一局完整循环可跑通；内容量早期（1 张赛道、6 个技能 + 各自 anti） |
| 屎山程度（整体） | 6.5 / 10 | 骨架与设计意图清楚，积累了 Legacy 并存、上帝类、复制粘贴、调试设施默认开启 |
| 复杂度（整体） | 7.5 / 10 | FishNet 预测/回放 + host/client 双路径 + 开场控制权交接，本质复杂，又被分散放大 |
| 仓库卫生 | 3 / 10 | 大量日志、样例场景、未用资源入库；`.gitattributes` 会损坏二进制 |
| 行为不变的优化空间 | 大 | 约 3–4k 行死代码 + 约 2k 行可合并重复（合计 15–20% 自研代码）；LFS 约 1.5GB、普通 git 约 99MB 可清 |

## 2. 项目进度

- 已实现链路：Steam 大厅 → 房间 → 属性/技能配装选择 → 入场动画与倒计时 → 预测运动竞速 + 技能 → 计圈/终点/名次 → 结算演出 → 投票再来一局或回房间。
- 占位或未生效：地图投票（`PropertiesSelectionManager.cs:65` 场景名写死 `"RaceMap"`）、皮肤阶段（`:1188-1199` Map_A/B/C 占位）。
- 投入分布：2026-03~05 共约 130 个提交，其中约 30 个 PR 属于预测系统 V2 重构（Phase 1→4b，已收尾）。
- 活跃度：2026-05-03 之后仅 2026-09-29 一次 harness/文档提交。
- 分支：`dev` 领先 `main` 133 个提交，`main` 停在 2026-03-14。
- 可发布性：**非 Development 的 Standalone 构建编译失败**（见 §7 B1），说明尚未打过正式包。

## 3. 规模指标

| 项 | 数值 |
|---|---|
| 自研 C# | 206 文件 / 约 37.4k 行 |
| 按目录 | Buddah 9.4k · Network 8.3k · UI 7.5k · New_Buddah 6.5k · RaceIntro 2.1k · Config 1.0k · 其他 2.6k |
| >1000 行文件 | `BuddahPredictedMotor` 2236 · `RoomStateManager` 1474 · `PropertiesSelectionManager` 1208 · `BuddahHandControl` 1180 |
| `Debug.Log*` | 381 处，绝大多数无开关 |
| `static Instance` 单例 | 14 个；`.Instance` 引用 225 处 |
| `FindObject(s)ByType` 等 | 40 处，多处在 Update / 轮询里 |
| asmdef / 测试 | 自研代码无 asmdef（全部进 Assembly-CSharp）；0 个测试程序集 |
| 第三方 | FishNet（671 文件）、Plugins 801 文件（Feel、Facepunch、FishyFacepunch、Piloto、TMP）、Adobe Substance |

## 4. 子系统评估

### 4.1 预测运动（`New_Buddah/` + `Buddah/` 顶层）— 屎山 7 · 复杂度 8

**新旧两条路径并存**
- 切换点：`BuddahMovementModeSwitcher.runtimeMode`。
  - 代码默认值是 Legacy。
  - `Assets/Character/Prefab/Buddah.prefab:1171` 设为 `1`（PredictionV2），且只有这个 prefab 挂了切换器。
  - 运行时可在 Inspector 切换（`BuddahMovementModeSwitcher.cs:32`）。
  - 结论：正式流程不会走 Legacy，但它是保留的 A/B 开关。
- `BuddahMovement` 已从驱动者变成外观/身份组件：
  - 6 处以上用 `FindObjectsByType<BuddahMovement>` 找玩家。
  - Intro/Finish 通过它转发给 HandoffBridge。
- 双写入者：组件禁用后，外部调用仍会走到 `RefreshLocalControlState`（`BuddahMovement.cs:450-471`），改写 `rb.isKinematic` 和碰撞检测模式；motor（`:591`、`:785`）也写同样的属性。

**上帝类 `BuddahPredictedMotor`（2236 行）**
- 约 10 项职责：
  - 输入采集、门控、replicate/reconcile
  - modifier、冲量、传送、handoff 事件
  - 4 组 RPC、质量缩放、拖尾重置
  - 调试状态（126 处）、日志（约 80 处）、影子对比
- 影子对比代码约 573 行（占 25%）。
- 最长方法：

| 方法 | 位置 | 约行数 |
|---|---|---|
| `Shadow_CompareAndReport` | `:1262-1653` | 390 |
| `RunInputs` | `:354-545` | 190 |
| `ConsumePendingTeleportEvent` | `:1798-1895` | 98 |
| `ReconcileState` | `:548-623` | 76 |

- `RunInputs` 的 3 个提前返回分支重复同一段收尾（`:444-481`）。
- 传送的 13 个参数在 4 处原样透传（`:796/:969/:1057/:1125`）。

**上帝类 `BuddahHandControl`（1180 行）**
- 职责：
  - 自建第 3 份 `InputSystem_Actions`（`:137`）
  - 手骨旋转、yaw 同步
  - 推击 hitbox
  - 投射物模式（服务端 13 个字段、本地 9 个字段镜像维护）
  - 普通、蓄力、连发投射物
  - 动画复制
- 方法参数达 13–17 个（`SpawnChargedProjectileBurst :991-1058`）。
- 连发与蓄力逻辑成对重复：`:857-873` 对 `:1021-1057`，`:876-918` 对 `:754-793`。

**热路径开销**（与调试开关无关，每次都会发生）
- 每个 tick 都会执行：
  - `BuildReplicateData → SyncModifierDebugState → BuildModifierSummary` 新建 StringBuilder（`:2210`）
  - `enum.ToString()`（`:2170`）
  - replicate/reconcile 摘要（`:744`、`:606`），回放时也会执行
- 每帧都会执行：
  - `Update` 拼字符串（`:216`）
  - `RuntimeHealthReport.LateUpdate` 做 List、`string.Join`、GetComponent
- 每 tick 或每帧的 GetComponent：
  - `ResultAreaInteractionGate.ResolvePresentation`：motor `:261/:327`，HandControl `:201`
  - `PlayerCamera.GetCameraScaleMultiplier`：`:291-298`
  - `BuddahRespawn.IsLocalOwner`：`:227`，在 OnCollisionStay 中调用
- `BuddahRespawn :57/:98`：每次碰撞都拼一条日志，而 prefab 里开关是开的。

**死代码与协议残留**
- 5 个空类 `Integration/BuddahPrediction{Gate,Input,Intro,Respawn,Skill}Adapter.cs`。
- `DirectHeadingControl` 分支（`BuddahMovement.cs:489-522`）：两个输入源都写死为 false。
- replicate/reconcile 里有永远为默认值的字段，却每 tick 序列化：
  - `InputData:14-18` 的 `LastConsumed*Id`、`OwnerControlMask`
  - `ReconcileData:18-29` 的 `Pending*`、`ServerForward`
  - 代码里用 `_ = data.X` 掩盖“未使用”（motor `:382-386`、`:581-588`）。

**重复工具**
- 秒转 tick 有 3 份：`:1662`、`:2031`、`:2040`。
- `NotifyTeleportTrailRebases` 有 2 份：motor `:2068`、Respawn `:258`。
- `InputSystem_Actions` 被 new 了 3 份：`BuddahMovement:80`、`HandControl:137`、`OwnerInputBridge:24`。

**加分项**
- tick 对齐的事件、去重，确定性计算与副作用分离。
- 影子对比（D-LOC）提供了现成的确定性回归判据。

### 4.2 网络、房间与比赛流程（`Network/`、`RaceIntro/`）— 屎山 6 · 复杂度 7

**单例耦合与循环依赖**
- `GameNetworkManager ⇄ RoomStateManager`：GNM `:297` 对 RSM `:635/:653/:1226`。
- RSM 用 `FindFirstObjectByType` 探测比赛场景里的管理器（`:1303-1369`），这些管理器又回调 `RoomStateManager.Instance`。
- 运行时调用环：`PlayerProgressReporter → RaceFinishManager.TryRegisterFinish → MatchResultPresentationCoordinator → PlayerProgressReporter`。

**`RoomStateManager`（1474 行）**
- 6 块职责：
  - 名单与身份：`:581-724`
  - ready/start：`:256-440`
  - 场景流转：`:495-545`、`:726-907`
  - 开赛门控：`:943-1173`
  - 输入门控：`:84-124`
  - 诊断：约 250 行
- 状态表示：
  - 有 `MatchSessionPhase` 枚举，但比赛内子状态靠 7 个 bool SyncVar（`:44-52`）。
  - 另有 4 个 HashSet 和 3 个 Dictionary（`:58-64`）。
- 5 份重置逻辑，各自清的内容不同：`:241-253`、`:757-773`、`:884-896`、`:899-907`、`:910-924`。其中 `:910` 不清 `_raceSceneManagersReadyClientIds`。
- 三件套复制粘贴：
  - `AreAllClients*ForSequenceServer`（`:307-350`）
  - `ReportLocal*`（`:280-305`）
  - `Report*ServerRpc`（`:1134-1173`）
- 无外部调用的 API：
  - `AreAllClientsIntroAssignmentsReadyServer`
  - `AreAllClientsIntroVisualsReadyServer`
  - `AreAllClientsGameplayLiveServer`
  - `AreAllClientsIntroAssignmentsReadyForSequenceServer`
  - `IsWaitingForAuthoritativeGameplayLive`
  - `CanPlayersUseGameplayInput`
  - `ShouldBlockRaceGameplayInput`

**`PropertiesSelectionManager`（1208 行）**
- 技能配装的特殊判断至少 9 处。
- “全员提交后推进”写了两份：`:456-465` 与 `:504-513`。
- 名单与身份代码复制自 RSM：`:710-744` 对 `:633-675`。
- `GetLocalClientId` 有 3 份。
- 每次 SyncList 变化或每秒倒计时，都在服务端和客户端整体重建静态缓存（`:893-922`）。

**比赛进度重叠**
- `SplineProgressTracker` 的 Update 无 IsOwner 保护，每个客户端上的所有车身每帧都在算。
- `LapProgress` 每帧调用 `GetComponentsInChildren`（`:219`、`:246`）。
- `RaceCompletionTracker` 同一份数据算 3 次：
  - RCT.Update `:87`
  - PPR.Update `:44`（每 0.1 秒）
  - 服务端 RPC `PPR:88`
- 名次存了三份：RCT、LeaderboardManager、RaceFinishManager 的 `_finishedClientIds`。
- 默认 3 圈写了三处：`RaceCompletionTracker:10`、`PlayerProgressReporter:212`、`RaceFinishManager:15`。
- `LeaderboardManager.TryAdvanceCheckpoint`（`:107`）无人调用，排序键 Checkpoints 永远为 0。

**魔法值与日志**
- 场景名散在 5 处：RSM `:32/:37-39`、PSM `:65`、ResultDecisionManager `:21-22`、RFM `:19`、LobbyManager `:17`。
- 日志体系有 4 套：NetLog、`NetDebug.EnableVerboseLog`、各类自己的开关、常量。
- `LogSceneDiag` 的参数在调用前就求值：`BuildServerRaceReadinessSummary()` 每 0.25 秒遍历每个玩家的 `conn.Objects`，再做 3 次 GetComponent（`:950`、`:1035-1086`）。
- 倒计时用 `WaitForSeconds(1f)`，走缩放时间（RSM `:985`、PSM `:593`）；其他代码用 unscaledTime，两者不一致。

**加分项**
- `SteamLobbyManager`、`GameNetworkManager` 写得干净。
- 有序列号与防重入保护。

### 4.3 技能与配置（`Buddah/ComboSkill/`、`Config/`）— 屎山 6 · 复杂度 7

**抽象与扩展**
- `SkillAction` 是 abstract ScriptableObject，SkillExecutor 按多态分发。
- 但加一个技能要动 5 处：
  1. 写子类，建 asset
  2. 在 `Skill Database.asset` 登记
  3. 在 `ProjectConfigDatabase_Main.asset` 补 3 张表
  4. 有新移动效果时，在 SkillExecutor 加一对 `ApplyXToOwner` + `TargetRpc`（`:326-498`，每对都有预测/旧路径两套分支）
  5. 在 `ResetActiveSkillEffectsForOwner` 的硬编码清单补一行（`:150-164`）

**复制粘贴**
- `Skill_BlackCurtain` 与 `_Anti`：只差 CreateAssetMenu、一个默认值和日志前缀（已 diff 核实）。
- `Skill_Giant` 与 `_Anti`：`ApplyScaleEffect`、`ApplyLocalCameraEffect` 逐字复制。
- `ReverseTurn` 与 `_Anti`：字段块一模一样。
- `vfxDurationSeconds > 0f ? …` 出现 14 次；Feel 调用模板出现在 10 个类里。

**三份数据源，已经漂移**
- 来源：
  1. SO 自身字段（`SkillAction.cs:8-24`）
  2. `SkillBalanceRecord`、`SkillDefinitionRecord`（`ProjectConfigRecords.cs:28-49`），冲突时配置优先
  3. `Resources/Config/ProjectConfigDatabase.json` 兜底
- 漂移实例：PushProjectileHands 的 SO 是 18/15，配置是 12/10。
- JSON 兜底坏了：格式本身有错（第 94 行，已核实），且 id 过期（`slow_trap` 对 `slowtrap`）。
- `antiSkillId` 有 4 层回退；默认配装有 3 层。
- `skillEffectParams` 整张表 0 调用。

**逐帧开销**
- BlackCurtain 淡入淡出期间每帧执行（已核实）：
  - `FindObjectsByType<TrackEdgeVisibility>`（`BlackCurtainViewController.cs:450`）
  - `FindGameObjectsWithTag`、`GetComponentsInChildren<Renderer>`、`Shader.PropertyToID`（`:467-483`）
  - 每条赛道边一条插值日志（`TrackEdgeVisibility.cs:71`）
- `ComboSkillInput.debugHud` 默认 true（`:61`，prefab 也是 1），OnGUI 里每个事件都有分配。
- `ObsessionFigure.cs:83`：服务端每帧写 SyncVar。

**上帝类**
- `SkillExecutor` 混了：施放流程、4 对效果 RPC、FOV 协程（`:660-721`）、5 个同构的 `Resolve*`（`:511-608`）。
- `CastSlotServerRpc` 一个方法 80 行（`:174-253`）。

### 4.4 UI（`UI/`）— 屎山 6 · 复杂度 5

**耦合方向**
- UI 持有业务状态：技能草稿 `_selectedSkillIds`、`_draftDirty`、`_draftLocked`（`SkillWallSelectionController.cs:50-63`）。
- UI 直接发网络请求：`ResultDecisionUI.cs:91/96` 直接调 ServerRpc。
- 网络层反向调用 UI 的静态方法：`SceneFadeController`（PSM `:655/:660`、ISM `:545`）。

**复制**
- “找 manager”复制 9 份。
- “找本地玩家”复制 6 份。
- 贝塞尔飞行动画完全重复：`SkillWallItemView :188-213` 与 `SkillInspectController :207-231`。

**轮询**
- 18 个 Update/LateUpdate。
- **`RoomUI.Update`（`:63-69`）每帧 Destroy 并重新 Instantiate 全部玩家条目（`:253-269`，已核实）。**
- `PropertiesSelectorUI` 每帧 3 次字符串插值。
- `LeaderboardTMPUI` 每秒 10 次 StringBuilder 和 Split；其查找条件是 `||`，prefab 少一个组件就会永远每帧 Find。

**命名误导**
- `UI/InGameUI/Legacy/` 下的 `LeaderboardTMPUI`、`ResultDecisionUI` 挂在 `RaceMap.unity` 上，仍在使用。

## 5. 死代码（GUID + C# 双重零引用）

扫描脚本见 [HANDOFF.md](HANDOFF.md) 的“死代码复核”一节；删除前必须重跑。

| 文件（相对 `Assets/Scripts/`） | 行 | 证据 |
|---|---|---|
| `Debug/DebugNetworkTester.cs` | 193 | GUID 0、C# 0 |
| `Debug/DebugRaceFinishButton.cs` | 64 | GUID 0、C# 0；连带 `RaceFinishManager :38-50/:147-207` 调试结束路径 |
| `Testing/DebugLobbyTester.cs` | 297 | GUID 0、C# 0 |
| `Testing/LobbyFlowDebugOverlay.cs` | 203 | GUID 0、C# 0 |
| `Sandbox/PredictionApiSpike.cs` | 138 | GUID 0、C# 0，文件自述一次性 |
| `New_Buddah/Integration/BuddahPrediction{Gate,Input,Intro,Respawn,Skill}Adapter.cs` | 5×6 | 空类 |
| `New_Buddah/Events/BuddahPredictionEventIds.cs` | 6 | 空类 |
| `UI/InGameUI/Legacy/LeaderboardUI.cs` | 81 | GUID 0、C# 0 |
| `UI/MenusUI/LobbyRoomListItemUI.cs` | 55 | GUID 0、C# 0 |
| `UI/InGameUI/MiniMapPresenter.cs` + `MiniMapPlayerLocator.cs` + `MiniMapWorldMapper.cs` | 约 259 | 仅 Presenter 引用后两者，Presenter 自身 0 引用 |
| `Network/Legacy/LobbyManager.cs` + `Network/Core/ConnectionManager.cs` | 615 | 两者 GUID 0；`MainMenu.unity:6287` 的 `_legacyLobbyManagerPrefab: {fileID: 0}`，GNM `:314` 直接返回 |
| `Network/Match/PlaceholderPlayerController.cs` | 150 | 仅 `Assets/Editor/MatchScenePlaceholderSetup.cs` 使用 |

以下文件看似零引用，**实为存活**，不要删：
- `FrameRateLock`、`BuddahPredictionPerfProbe`：通过 `RuntimeInitializeOnLoadMethod` 启动。
- `InputSystem_Actions`：类名带 `@` 前缀，扫描器漏匹配。
- `SceneFadeController`、各 `*Effect`、Validation 组件：运行时 AddComponent 创建。
- `ProjectConfigValidationMenu`：编辑器菜单。

## 6. 仓库与资源卫生 — 3 / 10

资源大小按 LFS 指针记录的真实大小统计（本机未装 git-lfs）。

| 项 | 规模 | 情况 |
|---|---|---|
| `.gitattributes` `*.asset text eol=lf` | — | 二进制 `LightingData.asset` 被当文本处理；新克隆即显示为已修改（已核实），下次提交会被改写 |
| `agent-exchange/` | 99MB 被跟踪（其中 `raw/host-player-log-2026-04-19.log` 为 88.8MB） | 历史日志 |
| `.VSCodeCounter/` | 3.4MB | 生成物 |
| `Assets/Scenes/Oasis` | 约 471MB | URP 样例；RaceMap 实际使用约 20 个文件、约 30MB（天空盒、HeightFog、`OasisFog.cs`、Volume、太阳光晕，光晕又引用 Terminal 的 3 张贴图） |
| `Assets/Scenes/Garden`、`Cockpit` | 7MB | 无引用，但脚本仍编进 Assembly-CSharp |
| `Assets/Scenes/RaceMapEndField` | 1.2MB | 孤立的烘焙数据，对应场景不在 Build Settings |
| `Assets/Sources/Skin_Subs`、`Buddah` | 453MB + 85MB | 0 引用，可能是美术源文件 |
| 41 张 4K 未压缩 TGA | 约 1.3GB | 只用到 15 张 |
| `Plugins/Feel/NiceVibrations` 等 | 约 68MB | 游戏未使用；Feel 主体（MMF_Player、Shaker）在用 |
| `FishNet/Demos` | 306 个文件 | 通过 `DefaultPrefabObjects.asset` 把 Demo prefab 注册成网络可生成对象（30 个），会进构建 |
| 未使用的包 | 5 个 | visualscripting、collab-proxy、ide.vscode、probuilder、softmask-for-ugui |
| 未锁定版本的 git 包 | 2 个 | softmask、ParrelSync |
| `Agent.md` / `AGENTS.md` / `CLAUDE.md` | — | 内容完全相同（均指向 `harness.md`） |

- 全部删除前都要确认没有运行时加载。已查过：
  - `Resources.Load` 仅 `ProjectConfigRuntime.cs:63-67` 一处
  - 场景均按名字加载
  - 未使用 Addressables
- 合计：约可省 1.5–1.6GB LFS 存储（总量约 2.3GB），普通 git 约 99MB。

## 7. 行为缺陷清单（修复会改变行为，不属于“行为不变”的重构）

| ID | 缺陷 | 证据 | 状态 |
|---|---|---|---|
| B1 | 非 Development 的 Standalone 构建编译失败 | `BuddahPredictedMotor.cs:550` 的 `_reconcileCallbackCount++` 未包 `#if`，字段只在 `(UNITY_EDITOR\|\|DEVELOPMENT_BUILD)&&BUDDAH_PREDICTION_SHADOW` 下声明（`:90-100`） | 已核实；修复只影响编译，纳入计划 P0 |
| B2 | 调试探针与影子代码进入正式包 | `ProjectSettings.asset:827` Standalone 宏包含 `BUDDAH_PREDICTION_SHADOW;VISUAL_PROBE;PERF_PROBE`；Perf/VisualShake 探针只由宏守卫 | 已核实；计划 P0 收紧为仅 Editor/Dev |
| B3 | Giant 在 owner 端被应用两次，第二次把 mass/force 倍率重置为 1 | `SkillExecutor.cs:495` 与 `Skill_Giant.cs:67`；`PlayerScaleEffect.cs:46-47` | 非预测路径已确认；预测路径待验证 |
| B4 | Acceleration 拖尾在 owner 端播放两次 | `SkillExecutor.cs:349/376` 与 `Skill_Acceleration.cs:39` | 待验证 |
| B5 | 三种效果各自快照并恢复 `forwardForce`，叠加时可能永久改写 | `MovementAccelerationEffect.cs:33`、`PlayerScaleEffect.cs:40`、`MovementRootThenAccelerationEffect.cs:38` | 推断 |
| B6 | `MatchResultPresentationCoordinator` 为空时比赛卡住 | `RaceFinishManager.cs:240-243` | 静态推断 |
| B7 | host 上 `OnPropertiesSelectorTransitionRequested` 触发两次 | RSM `:735/:871` + ObserversRpc `:1197` | 静态推断 |
| B8 | `SetSlotServerRpc` 不校验 skillId，可装任意技能，含 anti | `SkillLoadout.cs:135` | 已核实无校验 |
| B9 | 圈数由客户端上报，服务端直接信任 | `PlayerProgressReporter` RPC | 设计问题 |
| B10 | JSON 兜底配置损坏且过期 | `Resources/Config/ProjectConfigDatabase.json:94` | 已核实 |
| B11 | `SlowTrap_Anti` 的 vfxId 为空，每次施放都报 `Unknown vfxId ''` | asset 数据 | 待验证 |
| B12 | `SkillVfxReplicator.FindExisting` 只查子节点，找不到已移到根下的 VFX，每次施放都新建实例 | `SkillVfxReplicator.cs:193/:293` | 静态推断 |
| B13 | `TrackEdgeVisibility` 在编辑器里对共享材质 `SetFloat`，会改写 .mat | `TrackEdgeVisibility.cs:58` | 已核实调用点 |
| B14 | 倒计时走缩放时间 | RSM `:985`、PSM `:593` | 仅当 timeScale≠1 时有影响 |

## 8. 优化空间量化

- **代码**：
  - 死代码约 3–4k 行（§5，加上 motor 影子代码的隔离）
  - 可合并的重复约 2k 行
  - 目标是使 >1000 行的文件从 4 个降到 0 个，且 RPC、SyncVar、序列化字段保持不变
- **运行时**：GC 分配与逐帧、逐 tick 的查找集中在以下几处，P0/P1 可消除大部分：
  - RoomUI
  - BlackCurtain
  - motor 的调试摘要
  - Leaderboard
  - RSM 的诊断
- **仓库**：LFS 约 1.5GB，普通 git 约 99MB（彻底清除需要改写历史，属团队决策）。
- **工程化**：
  - 拆 asmdef 后，改 UI 不再重新织入整个程序集，也能开始写 EditMode 测试。
  - 日志统一后，正式包没有日志开销。
