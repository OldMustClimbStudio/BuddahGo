# 架构优化方案（行为保持）

- 依据：[audit-report.md](audit-report.md)。执行流程与进度见 [HANDOFF.md](HANDOFF.md)、[progress.md](progress.md)。
- 基线：`dev` @ `ce5c1c2`。行号是定位提示，以符号名为准。

## 1. 目标与边界

**目标**
- 降低理解和修改成本：拆上帝类、消除复制、统一横切设施（日志、玩家查找、常量、身份）。
- 去掉死代码和热路径浪费。
- 让仓库可以放心克隆、可以正式打包。

**行为保持**
- 对玩家、联机和回放可观察到的结果与基线一致。
- 已知缺陷（audit §7 B3–B14）也原样保留，由团队在决策 D8 中逐个决定是否修复。
- 唯一例外：P0 中只影响编译产物的 B1、B2。

## 2. 不变式（每一步都必须满足）

- **I1 序列化**
  - 任何 `[SerializeField]` 或 public 序列化字段都保持原名、原类型、原所在类。
  - 不能把字段收进新的嵌套 struct 或 class。
  - 删除字段只允许用于确认无用的字段（YAML 里残留的旧条目无害）。
- **I2 脚本身份**
  - MonoBehaviour、NetworkBehaviour、ScriptableObject 保持类名、文件名、`.meta` GUID 不变。
  - 移动文件时连同 `.meta` 一起用 `git mv`。
  - 本轮不改 namespace。
  - `MainMenuUI`、`RaceFinishManager` 被 UnityEvent 以 `Assembly-CSharp` 限定名引用，尤其不能动。
- **I3 网络协议**
  - RPC 方法的名称、签名、所在类保持不变。
  - SyncVar、SyncList 的类型和声明保持不变。
  - replicate、reconcile 结构体的布局保持不变。
  - 只有步骤明确写出时，才允许删除**无调用者**的 RPC；删除会改变 RPC 编号，要求所有端使用同一构建。
- **I4 时序**
  - 预测 tick 内的调用顺序不变（`RunInputs`、`ReconcileState`、PostTick）。
  - 协程的等待时长和时间源不变。
  - 事件订阅与触发的先后不变。
- **I5 查找语义**
  - 用缓存或注册表替换 `Find*`、`GetComponent*` 时，返回集合必须与原调用一致：包括激活状态过滤、组件被禁用时是否仍返回、查找层级顺序（self → parent → children）。
- **I6 合并前先 diff**
  - 把"看起来重复"的 N 份代码合并成 1 份之前，逐字 diff。
  - 有任何差异（取整方式、判空、日志之外的分支）时，要么保留差异作为参数，要么停下来在 progress.md 记录。
- **I7 一步一提交**
  - 每个步骤 ID 对应一个提交，可以单独 revert。
  - 提交内只含该步骤的改动。

## 3. 目标架构

### 3.1 分层与依赖方向

```text
Presentation   UI/ · VFX/ · Camera · 调试 Overlay        只读状态，订阅事件或签名轮询
      ↓ 读
Gameplay       Race(进度/名次) · Skills · Prediction      服务器权威 + 客户端预测
      ↓ 调
Session/Net    Room · Lobby · Selection · Results         场景流转与握手
      ↓
Foundation     GameLog · PlayerRegistry · PlayerIdentity · SceneNames · RaceRules · 配置仓库
```

- 目标依赖方向是从上往下。
- 现有反向依赖（Session 调 UI 的 `SceneFadeController` 静态方法）本轮保留；只有决定做 D6 并拆出 UI 程序集时才需要反转（P6-3）。

### 3.2 新增的 Foundation 模块（全部是新增代码，不改变现有行为）

| 模块 | 替代 | 要点 |
|---|---|---|
| `GameLog` | 381 处 `Debug.Log` 与 4 套开关 | `Verbose(tag, msg)` 标注 `[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]`：编辑器和 Dev 包行为不变，正式包里连参数都不求值。Warning/Error 保持直接调用 |
| `PlayerRegistry` | 8 处以上的 `FindObjectsByType<BuddahMovement>`、找本地玩家的 6 份复制 | 在 `Awake` 注册、`OnDestroy` 注销；查询时按 `gameObject.activeInHierarchy` 过滤，以复刻 `FindObjectsByType`（排除未激活的 GameObject，但仍返回被禁用的组件，见 I5）。不能用 OnEnable/OnDisable，因为 Legacy 隔离会禁用 `BuddahMovement` |
| `PlayerIdentity` | GetLocalClientId 3 份、Steam 身份/名字 4 份 | 合并前逐份 diff（I6） |
| `SceneNames` / `RaceRules` | 5 处场景名、3 处"默认 3 圈" | 值与原常量完全一致；各调用点原有的兜底语义保留 |

### 3.3 各上帝类的拆分形态

总原则：**NetworkBehaviour 保留为薄外壳**，SyncVar、RPC、Unity 回调都留在原类里，逻辑下沉到普通 C# 类。这样 I1–I3 天然成立，下沉的逻辑还能写 EditMode 测试。

| 原类 | 拆分后 |
|---|---|
| `RoomStateManager`（1474 行） | 外壳：SyncVar、RPC、协程入口。下沉 `RoomRoster`（名单与身份）、`RaceStartHandshake`（4 个 set + 3 个 dict，以及三件套参数化）、`RoomDiagnostics`（诊断摘要，惰性构建）。5 个 reset 函数保持各自原有的清理集合，只是改为调用具名的子重置 |
| `PropertiesSelectionManager`（1208 行） | 外壳 + `LoadoutRules`（校验与自动补全，纯函数）+ 合并两份"全员提交后推进"（先 diff） |
| `BuddahPredictedMotor`（2236 行） | `partial class` 拆文件：`.cs`（核心 tick）、`.Events.cs`（冲量、传送、handoff、modifier 的消费）、`.Rpc.cs`、`.DebugState.cs`、`.Shadow.cs`（全部 `#if` 影子成员）。只移动代码，逻辑不变 |
| `BuddahHandControl`（1180 行） | 外壳 + `ProjectileBurstPlanner`（纯计算，用一个内部参数 struct 替代 13–17 个参数；这个 struct 不序列化）+ `PushAttackTiming` |
| `SkillExecutor`（763 行） | 外壳 + `ResolveInHierarchy<T>`（保持各自原有的查找顺序）+ `SkillCameraFov`（普通类，协程仍由 executor 启动）+ `CastSlotServerRpc` 拆成按原顺序执行的私有步骤 |
| `SkillAction` 家族 | 基类加受保护的公共方法（vfx 时长解析、Feel 播放）；`X_Anti` 改为继承 `X`，保留原类、文件和 GUID，只覆写差异部分 |

## 4. 阶段与步骤

每步的格式是：**改** / **预检** / **完成标准** / **验证**（R 编号见 §5）/ **风险**。阶段必须按顺序完成；同一阶段内按编号顺序做。

### P0 安全网与只影响编译的修复

**P0-1 记录基线**
- 改：在 `dev` 基线上跑 R4–R9，记录到 `Docs/optimization/baseline.md`，内容包括：
  - 日期、提交号
  - D-LOC 心跳计数
  - V13 GC.Alloc、V3 visual shake 数值
  - 各技能表现的简述
- 完成标准：baseline.md 中 R4–R9 每项都有结果或"不可执行 + 原因"。
- 风险：无。

**P0-2 `.gitattributes` 修正二进制 .asset**
- 预检：用下面的命令列出所有不是 YAML 的 `.asset`：

  ```bash
  git ls-files '*.asset' | while read f; do head -c 5 "$f" | grep -q '%YAML' || echo "$f"; done
  ```

  要排除 LFS 指针文件。
- 改：在 `*.asset text eol=lf` 之后逐个加上 `<path> binary`（或按文件名模式，例如 `LightingData.asset`）。
- 完成标准：新克隆后 `git status` 干净；`git check-attr -a Assets/Scenes/RaceMap/LightingData.asset` 显示 binary。
- 验证：R1。
- 风险：低。

**P0-3 修复正式包编译（B1）**
- 改：把 `BuddahPredictedMotor.cs:550` 的 `_reconcileCallbackCount++;` 包进 `:90` 那个 `#if`。
- 预检：列出所有 `#if (UNITY_EDITOR || DEVELOPMENT_BUILD)` 块里声明的成员，逐个确认它们的使用点也在同一个条件里：

  ```bash
  grep -n "_reconcileCallbackCount\|_shadow\|_dLoc" BuddahPredictedMotor.cs
  ```

- 完成标准：R3 构建成功。
- 验证：R1、R3、R7。
- 风险：低。

**P0-4 调试探针只进 Editor/Dev（B2）**
- 改：对 `BUDDAH_PREDICTION_VISUAL_PROBE`、`BUDDAH_PREDICTION_PERF_PROBE` 以及其他没有 Dev 限定的 `BUDDAH_PREDICTION_*` 守卫，统一改为 `#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && <原宏>`。已知位置：
  - `BuddahPredictionPerfProbe.cs:1`
  - `BuddahPredictionVisualShakeProbe.cs:1`
  - `BuddahPredictionBootstrap.cs:77`
  - 其余用 `grep -rn "#if BUDDAH_PREDICTION" Assets/Scripts` 找全
- 不改 `ProjectSettings` 里的宏，编辑器行为因此不变。
- 完成标准：
  - grep 结果里不再有未限定的探针守卫。
  - R3 构建中不包含探针类型（在 Player.log 里看不到探针输出）。
- 验证：R1、R2、R3、R7。
- 风险：低。

**P0-5 仓库减负（不改历史）**
- 改：
  - `git rm --cached -r .VSCodeCounter agent-exchange/console/raw`
  - 在 `.gitignore` 中加上这两个路径
  - 其余 `agent-exchange` 的 digest 和 handoff 保留，它们是 harness 认定的证据
- 完成标准：两个目录不再被跟踪，工作区文件仍在。
- 风险：低。改写历史属于 D7。

### P1 删除死代码（行为中性）

每个删除步骤都要先跑 [HANDOFF.md](HANDOFF.md#死代码复核) 的复核脚本，确认目标仍是"GUID 0 引用 + C# 0 引用"。结果不一致就停下来，记录到 progress.md。

**P1-1 删除零引用脚本**
- 删除以下文件及其 `.meta`：
  - `Debug/DebugNetworkTester`
  - `Debug/DebugRaceFinishButton`
  - `Testing/DebugLobbyTester`
  - `Testing/LobbyFlowDebugOverlay`
  - `Sandbox/PredictionApiSpike`
  - 5 个 `BuddahPrediction*Adapter`
  - `BuddahPredictionEventIds`
  - `UI/InGameUI/Legacy/LeaderboardUI`
  - `UI/MenusUI/LobbyRoomListItemUI`
- 完成标准：复核脚本对这些类型输出 0；R1 通过。
- 验证：R1、R2。

**P1-2 删除 MiniMap 半成品三件套**
- 删除 `MiniMapPresenter`、`MiniMapPlayerLocator`、`MiniMapWorldMapper`。
- 预检：`MiniMapController` 不引用这三者。
- 验证：R1、R4（小地图显示正常）。

**P1-3 删除旧大厅链路**
- 改：
  - 删除 `Network/Legacy/LobbyManager.cs`、`Network/Core/ConnectionManager.cs`。
  - 删除 `GameNetworkManager` 中的 `_legacyLobbyManagerPrefab` 字段和它的 spawn 分支（`:297-330` 一带）。
- 预检：
  - `MainMenu.unity` 中该字段是 `{fileID: 0}`。
  - `Assets/Editor/NetworkManagerValidator.cs` 是否引用这两个类；有引用就一并删掉对应的检查代码。
- 验证：R1、R4（创建/加入大厅）。
- 风险：低。

**P1-4 删除 `RaceFinishManager` 的调试结束路径**
- 删除 `:38-50`、`:147-207`。前提是 P1-1 已删掉唯一的调用方 `DebugRaceFinishButton`。
- 预检：路径中的方法和字段没有其他调用者；如果是被 Inspector 或 UnityEvent 绑定的方法，必须保留。
- 验证：R1、R4。

**P1-5 删除无调用者的 API**
- 删除清单（逐个 grep 确认 0 调用，并确认没有被 UnityEvent、`m_MethodName` 或 Timeline 信号引用）：

  | 类 | 成员 |
  |---|---|
  | RoomStateManager | `AreAllClientsIntroAssignmentsReadyServer`、`AreAllClientsIntroVisualsReadyServer`、`AreAllClientsGameplayLiveServer`、`AreAllClientsIntroAssignmentsReadyForSequenceServer`、`IsWaitingForAuthoritativeGameplayLive`、`CanPlayersUseGameplayInput`、`ShouldBlockRaceGameplayInput` |
  | LeaderboardManager | `TryAdvanceCheckpoint` |
  | SkillAction | `HasAnti` |
  | SkillLoadout | `RequestSetSlot`、`SetSlotServerRpc`（无调用者的 RPC，同时关闭 B8 的口子） |
  | SkillDatabase | `TryGetBalance`、`GetResolvedDescription`、`GetResolvedIconKey`、`TryGetEffectFloat` |
  | SkillConfigRepository | `GetSkillIdsByTag`、`ByBehaviorType`、`GetBehaviorType`、`GetAllParams`、`GetFloat` |
  | SkillVfxReplicator | 2 参数的 `PlayVfxAll`、3 个 `PlayVfxLocal`、`ForcePrewarmNow` |
  | BuddahMovement | `IsUsingAutoInput`、`SetInputSource`、`RestorePlayerInputSource`、`DisableAllInput`、`ApplyPushImpulseTargetRpc` |

- 完成标准：R1 通过，且清单中每个成员都记录了 grep 证据（写进提交说明）。
- 验证：R1、R4、R5。
- 风险：低到中，因为删除 RPC 会改变 RPC 编号（I3），所有端必须使用同一构建。

**P1-6 删除 `DirectHeadingControl` 分支**
- 删除 `BuddahMovement.cs:489-522`。
- 预检：两个输入源（`Buddah/Input/*.cs:6/10`）的 `UseDirectHeadingControl` 都是字面量 false。
- 保留接口属性，或者把它一并删掉并同步两个输入源。
- 验证：R1、R4。

**P1-7 移除未使用的包**
- 移除 `com.unity.visualscripting`、`com.unity.collab-proxy`、`com.unity.ide.vscode`、`com.unity.probuilder`、`com.coffee.softmask-for-ugui`。
- 预检：在 `Assets/Scripts` 和 3 个构建场景及其依赖 prefab 中，grep 命名空间和组件（`ProBuilderMesh`、`SoftMask`、`ScriptMachine`）为 0。
- 顺带把 ParrelSync 的 git URL 锁到当前 lock 里的 hash。
- 验证：R1、R2、R4。
- 风险：低。

**P1-8 移除 FishNet Demos，重新生成 `DefaultPrefabObjects`**
- 改：
  - 删除 `Assets/FishNet/Demos`。
  - 让 FishNet PrefabGenerator 重新生成 `DefaultPrefabObjects.asset`，或手动移除 30 个 Demo 条目。
- 完成标准：列表里只剩游戏的 prefab，且游戏 prefab 一个不少（与基线逐个对比）。
- 验证：R1、R2、R4、R5、R6。
- 风险：中。prefab 索引会变化，所有端必须同一构建；场景内对象用 sceneId，不受影响。

### P2 横切基础设施与热路径（行为中性）

**P2-1 新增 Foundation 模块**
- 新增 §3.2 的四个模块，此时不接入任何调用点。
- 完成标准：R1 通过；为 `PlayerRegistry` 的过滤语义写好注释。

**P2-2 日志迁移**
- 改：
  - 把 Info 级 `Debug.Log` 按目录迁移到 `GameLog.Verbose`。
  - Warning/Error 不动。
  - 已经有开关守卫的日志，保留原开关作为外层条件。
  - 迁移顺序：`New_Buddah` → `Buddah` → `Network` → `RaceIntro` → `UI`，每个目录一个提交（P2-2a…e）。
- 预检：日志参数里不能有副作用调用（例如 `x++`、会修改状态的方法）；有的话先把副作用提到日志调用之外。
- 完成标准：编辑器 Console 输出与基线一致（抽查 R4 流程）；R3 包里没有 Info 日志。
- 验证：R1、R3、R4、R7。

**P2-3 常量与身份收敛**
- 用 `SceneNames`、`RaceRules`、`PlayerIdentity` 替换散落的副本（遵守 I6）。
- 场景名是 Inspector 字段的（例如 PSM 的 `_resolvedMatchSceneName`）保持为序列化字段（I1），只把代码里的默认值改为引用常量。
- 验证：R1、R4。

**P2-4 接入 `PlayerRegistry`**
- 按调用点逐个替换 `FindObjectsByType<BuddahMovement>` 以及"找本地玩家"的复制代码：
  - LeaderboardManager `:334`、MRPC `:250`、RFM `:273`、ISM `:476`、ICC `:340`、RSM `:1346/:1374`
  - SkillSlotUI、MiniMapController、ObsessionUI、LapAddLineUITrigger、LeaderboardTMPUI
- 每个调用点确认 I5。
- 完成标准：`grep -rn "FindObjectsByType<BuddahMovement>" Assets/Scripts` 为 0。
- 验证：R1、R4、R5、R8。
- 风险：中，风险在于注册时机。如果调用发生在 `Awake` 之前（例如 OnStartClient 的顺序），要逐点确认。

**P2-5 热路径去浪费**（每项一个提交）
- a. **RoomUI**：保留每帧检查，但先计算名单签名（人数，以及每个玩家的 id、ready、名字的哈希），签名不变就跳过 `RefreshPlayerList`。显示结果与每帧重建相同。
- b. **BlackCurtain**：在淡入淡出开始时缓存 `TrackEdgeVisibility[]` 和带 tag 的 Renderer；`PropertyToID` 缓存到字段；遇到 null 元素时重新获取。`TrackEdgeVisibility.cs:71` 改用 `GameLog.Verbose`。
- c. **GetComponent 缓存**：`ResultAreaInteractionGate.ResolvePresentation`、`PlayerCamera.GetCameraScaleMultiplier`、`BuddahRespawn.IsLocalOwner`、`LapProgress :219/:246`。组件可能在运行时才被 AddComponent，所以缓存为 null 时要能重新查找，不能永久缓存 null。
- d. **LeaderboardTMPUI、ObsessionUI、SkillSlotUI**：查找改用 `PlayerRegistry`；文本拼接只在值变化时执行。
- e. **motor 调试摘要**：`BuildModifierSummary`、`BuildPendingSummary`、replicate/reconcile 摘要以及 `Update :216`，只在 DebugOverlay 或对应调试开关开启时构建。预检：确认这些函数只写 DebugState，不写模拟状态。
- f. **RSM 诊断**：`LogSceneDiag(BuildServerRaceReadinessSummary())` 改为先判断开关再构建。
- g. **RuntimeHealthReport.LateUpdate**：只在调试开关开启时运行。
- 完成标准：R8 的 GC.Alloc 相对基线下降，其余 R 项与基线一致。
- 验证：R1、R4、R5、R7、R8。

### P3 技能系统整理

**P3-1 基类公共方法**
- 在 `SkillAction` 中加 `protected` 方法：vfx 时长解析（14 处 `vfxDurationSeconds > 0f ? …`）、Feel 播放模板（10 处）。
- 子类改为调用这些方法；**序列化字段仍留在子类**（I1）。
- 验证：R1、R5。

**P3-2 Anti 类改为继承**
- 改：`Skill_BlackCurtain_Anti : Skill_BlackCurtain`，对 Giant、ReverseTurn 等同样处理：
  - 删除重复的方法体
  - 差异部分用 `override` 实现
  - 日志前缀改用 `GetType().Name`
  - 新建 asset 时的默认值用 `Reset()` 设置
- 基类 private 字段改为 protected。访问修饰符不影响序列化，但字段名不能改（I1）。
- 预检：逐对 diff，把差异清单写进提交说明。
- 完成标准：
  - 每个 anti asset 在 Inspector 中的值与基线一致。
  - `git diff` 中 `.asset` 文件没有值的变化（字段顺序变化可以接受）。
- 验证：R1、R5、R10。
- 风险：中。

**P3-3 SkillExecutor 下沉**
- 改：
  - 5 个 `Resolve*` 合并为 `ResolveInHierarchy<T>`，参数化查找顺序以保持各自原有顺序（I5）。
  - FOV 协程移入 `SkillCameraFov`。
  - `CastSlotServerRpc` 拆成按原顺序执行的私有方法。
  - 4 对 `ApplyXToOwner` 的方法体下沉到 `OwnerMovementEffectApplier`，RPC 保留在原位。
  - `ResetActiveSkillEffectsForOwner` 的清单保持原样。
- 验证：R1、R5、R6、R7。
- 风险：中。

**P3-4 配置交叉校验（仅在编辑器中运行）**
- 在 `ProjectConfigValidationMenu` 里增加检查：SO 字段 vs 配置表、SkillDatabase vs 配置里的 id、anti id 的 4 层回退结果、vfxId 是否在 VFX 库中存在。
- 只输出报告，不修改数据。
- 已知差异（例如 PushProjectileHands 的 18/15 对 12/10）写进 progress.md，交给 D5。
- 验证：R1。

### P4 会话与比赛流程拆分

**P4-1 `RoomStateManager` 下沉**
- 按 §3.3 拆分。
- 写一张 reset 矩阵（5 个 reset × 被清理的集合）放进提交说明，拆分后逐格核对。
- SyncVar、RPC、public API 保留在原位，改为委托调用。
- 验证：R1、R4（完整跑两轮：再来一局、回房间）、R9。
- 风险：中。

**P4-2 三件套参数化**
- `AreAllClients*ForSequenceServer`、`ReportLocal*`、`Report*ServerRpc` 的内部实现合并为同一个私有实现。
- 对外方法名和 RPC 保留（I3）。
- 验证：R4、R9。

**P4-3 `PropertiesSelectionManager` 下沉**
- 抽出 `LoadoutRules`；合并两份推进逻辑（先 diff）。
- 缓存的重建时机不变。
- 验证：R1、R4（全员提交、超时自动补全、中途有人退出）。

**P4-4 `PlayerProgressReporter` 瘦身**
- 结算区传送和演出的 5 组 RPC 只做文件内分区（`#region` 或 partial），不迁移到别的类，否则会触及 I3。
- 重复计算（`PPR.Update :44`）保持不变，见"不做清单"。
- 验证：R4。

### P5 预测运动整理

**P5-1 motor 拆成 partial 文件**
- 按 §3.3 拆分，只移动代码。
- 完成标准：
  - `git diff --color-moved=zebra` 显示只有移动。
  - R7 的 D-LOC 各项 div 为 0，reconcile 回调计数量级与基线一致。
- 验证：R1、R3、R7、R9。

**P5-2 `RunInputs` 收尾抽取**
- 3 个提前返回分支的公共收尾（`:444-481`）抽成私有方法，调用顺序逐行保持。
- 验证：R7（必须）、R9。
- 风险：中。

**P5-3 工具函数去重**
- 秒转 tick（3 份）、`NotifyTeleportTrailRebases`（2 份）按 I6 处理：取整方式不同时保留为参数，或者不合并。
- 验证：R7。

**P5-4 `BuddahHandControl` 下沉**
- 按 §3.3 拆分；两对重复循环按 I6 处理。
- RPC、SyncVar 与投射物相关的序列化字段原位保留。
- 验证：R1、R6（推击、普通/蓄力/连发投射物，host 和 client 都要测）。

**P5-5 影子对比表驱动**（可选）
- 仅限 `.Shadow.cs`，只在宏开启时编译。
- 完成标准：R7 心跳输出格式与基线一致。

### P6 工程化（需要先有 D6 决策）

**P6-1 程序集拆分的前置**
- 为 `Plugins/FishyFacepunch` 和 `Plugins/Feel/MMFeedbacks` 创建 asmdef，或者把它们移出 `Plugins/`。原因是 asmdef 程序集引用不到 firstpass；这两者分别被 `GameNetworkManager` 和 UI 引用。

**P6-2 `BuddahGo.Runtime` / `BuddahGo.Editor` asmdef**
- Runtime 覆盖 `Assets/Scripts`；Editor 覆盖 `Assets/Scripts/Editor`。
- 依赖：FishNet.Runtime、Facepunch.Steamworks、InputSystem、Cinemachine、Splines、TMP、VFX、MoreMountains.*、FishyFacepunch。
- `Assets/Editor/*` 留在 Assembly-CSharp-Editor（它会自动引用 asmdef 程序集）。
- 验证：
  - R1、R2、R3、R4
  - R10：主菜单 3 个按钮和 `RaceFinishManager` 的 UnityEvent 可用
  - FishNet 织入正常（RPC 能到达）

**P6-3 UI 程序集与依赖反转**（可选）
- 先把 Session 对 `SceneFadeController` 的静态调用改为 Foundation 中的门面，再拆 `BuddahGo.UI`。

**P6-4 EditMode 测试**
- 覆盖下沉出来的纯逻辑：`LoadoutRules`、`RaceStartHandshake`、`ProjectileBurstPlanner`、秒转 tick、`PlayerIdentity`。

### P7 资源瘦身（需要先有 D7 决策，独立轨道，可与 P2–P5 并行）
- 从 Oasis 中迁出 RaceMap 实际用到的约 20 个资源，再删除其余部分。
- 删除 Garden、Cockpit、RaceMapEndField 的孤立数据。
- `Sources/` 里未引用的美术源文件移出仓库。
- 未使用的 4K TGA 删除；用到的转为 PNG 或降分辨率。
- 删除 Feel 的 NiceVibrations 和 MMTools/Accessories（先确认 Feel 内部没有程序集依赖）。
- 删除任何资源前，必须确认没有运行时加载（Resources、字符串路径）。
- 验证：R2、R4、R10，并对比 RaceMap 的画面截图。

## 5. 验证矩阵

| ID | 内容 | 判据 |
|---|---|---|
| R1 | 编辑器编译 | 0 error；没有新增 warning（可用 Unity MCP 读取 Console，见 `Docs/unity-mcp.md`） |
| R2 | Development Standalone 构建 | 构建成功，能启动到主菜单 |
| R3 | 非 Development Standalone 构建 | 构建成功，能启动到主菜单（P0-3 之后始终必须通过） |
| R4 | 联机主流程 | host 用 Dev Build、client 用 Editor 或 ParrelSync，依次完成：大厅创建/加入 → 房间 ready → 属性/技能选择 → 进入 RaceMap → 开场倒计时 → 控制权交接 → 跑完 3 圈 → 排行榜/名次 → 结算 → 投票再来一局 → 再投票回房间。每一步的结果与 baseline.md 一致 |
| R5 | 技能矩阵 | 6 个技能及各自 anti，在 host 和 client 各施放一次：owner 和 observer 的表现、移动效果、冷却、反噬判定与基线一致（包括 B3/B4 这类已知缺陷的表现） |
| R6 | 物理交互 | 复活、碰撞、推击、普通/蓄力/连发投射物，host 和 client 都测 |
| R7 | 预测确定性 | 开启 `BUDDAH_PREDICTION_SHADOW`，在 2 端跑 90 秒以上：`[D-LOC HEARTBEAT]` 的 loc/tel/mod/hof div 均为 0，没有 FATAL |
| R8 | 性能 | V13 GC.Alloc、V3 visual shake 不比基线差；P2 之后 GC.Alloc 应下降 |
| R9 | 延迟 | 开启 100ms LatencySim，重跑 R4 的开赛到结算部分，再加 R7 |
| R10 | 序列化与资源 | 改动涉及的 prefab/scene/asset 在 `git diff` 中没有意外的值变化；Inspector 抽查通过；UnityEvent 按钮可用 |

各步骤列出的 R 项是**最低要求**；改动触及其他系统时，要加测对应的 R 项。

## 6. 需要团队决策的事项（执行者不能自行决定）

| ID | 事项 | 影响 |
|---|---|---|
| D1 | `debugHud`、`enableOnScreenDebug`、`enableVerboseRespawnLogs`、UI 的 `enableDebugLogs` 等调试开关，在 prefab 中默认关闭 | 改变开发时看到的内容，但玩法不变 |
| D2 | 移除 Legacy 运动路径（`BuddahMovement` 缩成外观，删除 SkillExecutor、Router、Respawn 的旧分支和 `Movement*Effect`） | 会失去 Inspector 的 A/B 回退开关 |
| D3 | 删除 replicate/reconcile 中的死字段 | 改变协议布局，按 prediction-design.md 验证序列化能往返 |
| D4 | 用 `RaceGateState` 枚举 SyncVar 替代 RSM 的 7 个 bool | 改变 SyncVar 布局；大厅只校验 `Application.version`，新旧版本无法互联 |
| D5 | JSON 兜底配置：修复或删除；SO 与配置表的漂移以哪边为准 | 数据决策 |
| D6 | 为第三方插件建 asmdef，并拆分程序集（P6） | 需要修改第三方目录结构 |
| D7 | 资源瘦身（P7），以及是否用 filter-repo 改写历史 | 需要美术确认；所有克隆都要重新拉取 |
| D8 | 修复 audit §7 中的 B3–B14 | 每个修复都是行为改变，单独开 `fix:` PR，不混进重构 |

## 7. 不做清单（本轮保持原样）

- namespace、类名、序列化字段一律不改名。
- 不共享 `InputSystem_Actions` 实例。各组件各自 Enable/Disable，共享会改变启停语义。
- 不改协程的时间源。缩放时间的问题属于 B14，归 D8。
- 不去掉 `PPR.Update` 的重复计算。它会改变 host 上的计算顺序。
- PSM 的缓存不改成只在服务端重建。
  - 目前只看到服务端的 `SkillLoadout.TryApplyResolvedSelectionServer` 读取这份缓存，但 RSM 在客户端也会清空它，而且收益很小。
  - 如果要做，先证明纯客户端上没有任何读取点，再作为独立步骤提出。
- 能用"签名比较"消除轮询开销的 UI，不改成事件驱动。
