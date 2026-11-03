# 01 模块化与配表（静态审查）

审查基线：`refactor/vfx-refactor` @ `ddb00807`（PR #68）。只读审查，没启动 Unity，没改配表。
标记 **[PR#63]**：涉及的文件在 `feat/mechanics-update`（PR #63）的改动清单里，等 PR #63 合并后再动。注意 `Assets/Design/Tables/` 下 7 本 xlsx 里除 `Graphics.xlsx` 外都在清单里，所以几乎所有"改表"的建议都是 [PR#63]。
严重程度：高 = 会让策划改表不生效或数值跑偏；中 = 结构性问题，阻碍后续拆分或容易误改；低 = 清理项。

## 0. 方法

| 用途 | 做法 |
|---|---|
| 程序集 | 读 3 个 asmdef、`Assets/Scripts/AssemblyInfo.cs`；`find` 统计 Scripts 以外的 `.cs`；用 meta GUID 在 3 个出包场景里 grep 是否被挂载 |
| 模块依赖 | 自写脚本（共享笔记目录 `code-review-scripts/r01/deps2.py`）：去掉注释和字符串后，收集 `Assets/Scripts` 下（不含 `Editor/`）每个顶层 `class/struct/enum/interface` 的声明目录；嵌套类型和同名多处声明的类型不参与（排除了 `Settings`、`Stage`、`Entry`、`Override`、`Context` 等通用名，避免误连）；再统计每个目录的文件里出现了哪些别的目录的类型名。表里 `t/f` = 被引用的类型数 / 发生引用的文件数。粗算：不区分"真正调用"与"`nameof`/类型签名"，但注释与字符串里的提及已去掉。可疑的边都回到源码看过 |
| 无读取方的列 | 脚本（`r01/cols.py`、`cols2.py`）：对 `scope.json` 的 `export` 里每个 sheet 的非 `doc` 列，在 `Assets/Scripts`（排除生成的记录类与 `Editor/DesignTables`）里找 `.列名` 访问；找不到的，再找 `DesignTableOverlay.Apply` 目标组件上的同名字段（忽略大小写，与 `FindField` 一致）以及 `ApplyMapped` 映射串里的列名；三者都没有的逐个人工核实 |
| 重复定义 | 脚本（`r01/so.py`、`pf.py`、`ai.py`、`names.py`）：按 `SkillDataBase.ApplyDesignTablesToSkills` / 各组件 `Awake` 里的实际映射，把 CSV 镜像的值与技能 SO、`Buddah.prefab` 组件、`Resources/AI/*.asset`、`Tools/ai/difficulty-*.json`、`SkillPersonalities.json` 的序列化值逐字段比对 |
| 硬编码数值 | grep `const float/int`、`WaitForSeconds(字面量)`、非 0/1 的浮点字面量（排除 `[SerializeField]`、`Range/Min/Tooltip`、`sqrMagnitude`/`1e-4` 类阈值、Debug/Diagnostics/Gizmo），再与 `SkillConstant`、`DrivingConstant`、`StructuralConstant`、各表 `doc` 列的"代码常量"登记对照 |

## 1. 程序集与目录

### 1.1 现状

| 程序集 | 内容 | 引用 | 备注 |
|---|---|---|---|
| `BuddahGo.Runtime`（`Assets/Scripts/BuddahGo.Runtime.asmdef`） | `Assets/Scripts` 下除 `Editor/` 外全部：284 个 `.cs`、约 4.8 万行；16 个顶层目录（New_Buddah 64、Buddah 51、Network 35、Match 30、UI 30、AI 21、Config 11、VFX 11、RaceIntro 10、Rendering 9、Foundation 5、GameData 3、Debug/GlobalSettings/InputSystem 各 1、根目录 `AssemblyInfo.cs`） | FishNet.Runtime、Unity.InputSystem、Cinemachine、Unity.Splines、Unity.TextMeshPro、Unity.VisualEffectGraph.Runtime、MoreMountains.Tools、Unity.Mathematics、Unity.Timeline、URP Core/Universal Runtime、UnityEngine.UI、FishyFacepunch | `autoReferenced: true`，`overrideReferences: false`（Steamworks 等预编译 DLL 自动引用）。诊断 harness（`AI/Diagnostics`）和预测探针（`New_Buddah/Debug`）也在这里，用 `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` 裁掉 |
| `BuddahGo.Editor`（`Assets/Scripts/Editor/BuddahGo.Editor.asmdef`） | 14 个文件：`DesignTables/`（12，配表导入器）、`Config/`（2，含 `SkillConfigCrossValidator`） | BuddahGo.Runtime | 仅 Editor 平台 |
| `BuddahGo.Tests`（`Assets/Tests/EditMode/BuddahGo.Tests.asmdef`） | 42 个文件、约 5.5k 行 | BuddahGo.Runtime、BuddahGo.Editor、FishNet.Runtime、InputSystem、TMP、Splines、Mathematics、URP Core/Universal | Editor-only，`autoReferenced: false`，`UNITY_INCLUDE_TESTS`。Cinemachine 用反射取类型（`SixRacerResultsTests.cs:38`） |
| `InternalsVisibleTo` | `Assets/Scripts/AssemblyInfo.cs:3`：只对 `BuddahGo.Tests` 开放 | — | Runtime 内 `internal` 出现 198 处（AI 44、Network 44、Buddah 42、VFX 34、New_Buddah 19、Match 11、Rendering 3、RaceIntro 1） |
| Assembly-CSharp-Editor（默认） | `Assets/Editor/` 23 个文件：AI 工具（`A1DrivingTools`、`AIDifficultyAssetTool`、`AISkillAcceptanceTools`、`ThrustVectorAcceptanceBuild`、`TrackWallFixtureTool`）、网络校验（`NetworkManagerValidator`、`NetworkObjectValidator`）、`MatchScenePlaceholderSetup`、`AssetUsageReport`、`VfxImportRecovery`、`Rendering/`（5，命名空间 `BuddahGo.Rendering.Editor`）、`VFX/`（8，`BuddahGo.VFX.EditorTools`，含 `SkillVfxTimingSync`、`SkillVfxTimingInspector`、`PalmMeshTripo`、两个 Proto）；另有 URP 样例的 9 个 `Editor/` 脚本 | 自动引用 BuddahGo.Runtime / BuddahGo.Editor | |
| Assembly-CSharp（默认） | `Assets/SharedAssets/**`、`Assets/Scenes/{Cockpit,Garden,Oasis,Terminal}/**` 的 58 个运行时脚本（URP 3D 样例） | — | 3 个出包场景里只有 `Assets/Scenes/Oasis/Scripts/Runtime/OasisFog.cs` 被 `RaceMap.unity` 挂着（按 meta GUID grep `MainMenu/PropertySelection/RaceMap.unity`），其余 57 个只服务样例场景 |

持久 UnityEvent 里写死了程序集名：`MainMenu.unity` 3 处 `SteamMultiplayer.UI.MainMenuUI, BuddahGo.Runtime`，`RaceMap.unity` 1 处 `RaceFinishManager, BuddahGo.Runtime`、2 处 `SteamMultiplayer.Network.Results.ResultPresentationTimelineBridge, BuddahGo.Runtime`（YAML 折行，grep `m_TargetAssemblyTypeName` 核实；与 `Docs/optimization/p6-validation.md` 记录的 6 处一致）。项目里没有 `[SerializeReference]`。

### 1.2 发现

**M1. 游戏专用编辑器工具分在两个 Editor 程序集，测试够不到一半** — 中，确定
- 位置：`Assets/Editor/VFX/SkillVfxTimingSync.cs`、`SkillVfxTimingInspector.cs`、`SkillVfxCharacterLayer.cs`、`Proto/*`、`Assets/Editor/AIDifficultyAssetTool.cs`、`AISkillAcceptanceTools.cs`、`A1DrivingTools.cs`、`TrackWallFixtureTool.cs`、`MatchScenePlaceholderSetup.cs`、`Rendering/RaceMap{LightProbeLayout,TemporalNoise,RenderCost}.cs` 等约 14 个文件引用游戏类型（去掉 namespace 行后 grep `using BuddahGo|SkillAction|SkillVfx|AIDifficulty|ProjectConfig|Skill_|AISkill…` 粗算；`NetworkManagerValidator` 的命中可能只是 FishNet 的 `NetworkManager`）。
- 问题：配表导入器在 `BuddahGo.Editor`，VFX 时长同步、AI 资产生成在默认 `Assembly-CSharp-Editor`。`BuddahGo.Tests` 只能引用 asmdef 程序集，所以 `SkillVfxTimingSyncTests` 没有调用 `SkillVfxTimingSync`，而是用自己的 `CollectAtTableValues()` 按表重算（grep 该测试文件，`SkillVfxTimingSync` 类名 0 次出现）；`Assets/Editor/VfxImportRecovery.cs` 用了命名空间 `BuddahGo.Editor`，与程序集名同名，易混。
- 怎么修：把 `Assets/Editor` 里引用游戏类型的工具迁到 `Assets/Scripts/Editor/<子目录>`（进 `BuddahGo.Editor`），URP 样例与通用资产工具留在原处。见 6.1 步骤 1。

**M2. Runtime 里的全局命名空间与目录对不上** — 低，确定
- 位置：`Buddah/`（51 个文件全无 namespace）、`Config/`（11）、`Foundation/`（5）、`GameData/`（3）、`RaceIntro/`（10）、`Network/` 7 个、`UI/` 7 个、`InputSystem/` 1 个、`VFX/` 1 个；`Match/UI/` 5 个文件用的是 `SteamMultiplayer.UI`；`Network/` 用 `SteamMultiplayer.Network*`；`New_Buddah/` 用 `NewBuddah.PredictionV2.*`。
- 为什么：拆程序集不依赖命名空间，但拆分前要靠目录判断归属；全局命名空间让 `using` 无法说明依赖，也是 1.1 那 6 处 UnityEvent 类型名的一部分（`RaceFinishManager` 无命名空间）。
- 怎么修：拆程序集时按新程序集统一命名空间（`BuddahGo.<模块>`）；改 MonoBehaviour 的命名空间不改脚本 GUID，但会改 UnityEvent 的类型名，须一并迁移（同 6.1 风险）。

**M3. 57 个 URP 样例脚本留在 Assembly-CSharp，出包场景只用 `OasisFog`** — 低，确定（是否删样例归资产审查 03，这里只记程序集边界）
- 怎么修：`OasisFog` 若长期使用，挪进 `Assets/Scripts/Rendering`（改命名空间时注意 RaceMap 里的组件引用按 GUID，不受影响）；样例目录保持默认程序集或另设 `Samples.asmdef`，与游戏代码隔开。

## 2. 模块依赖

### 2.1 依赖表（`t/f`；粗体 = 这一对互相引用）

| 引用方 \ 被引用 | Fnd | Cfg | GD | Mat | NB | Bud | AI | RI | Net | VFX | Rnd | UI | GS |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Foundation | — |  |  | **1/1** |  | **1/1** |  |  |  |  |  |  |  |
| Config |  | — |  | **2/1** |  |  |  |  |  |  |  |  |  |
| GameData | 1/1 | 3/3 | — | 4/2 |  | **1/1** |  |  | **4/2** |  |  |  |  |
| Match | **2/3** | **3/5** |  | — |  | **1/1** |  |  | **9/3** |  |  | **2/3** |  |
| New_Buddah | 1/13 | 3/1 |  | 4/4 | — | **9/7** |  | **2/4** | **3/3** |  |  |  |  |
| Buddah | **2/27** | 11/7 | **2/3** | **6/12** | **10/10** | — |  | **2/4** | **13/17** | **3/2** |  |  |  |
| AI | 1/1 | 9/2 | 3/9 | 14/13 | 6/8 | 9/8 | — |  | **11/9** |  |  |  |  |
| RaceIntro | 2/3 | 2/2 |  | 3/2 | **4/1** | **1/3** |  | — | **1/3** |  |  | 1/1 |  |
| Network | 4/18 | 11/10 | **1/2** | **10/12** | **1/1** | **8/8** | **6/1** | **2/3** | — | 2/1 |  | **2/1** |  |
| VFX |  | 2/2 |  |  | 1/1 | **3/2** |  |  |  | — | 2/2 |  |  |
| Rendering |  | 5/6 |  |  |  |  |  |  |  |  | — |  |  |
| UI | 2/5 | 3/2 | 2/3 | **4/4** |  | 6/6 |  |  | **19/19** |  |  | — |  |
| GlobalSettings |  | 1/1 |  |  |  |  |  |  |  |  |  |  | — |

`Debug/`、`InputSystem/` 没有引用别的目录。原始数据：共享笔记目录 `code-review-scripts/r01/deps_top.json`、`deps_split.json`（后者把 Network、Buddah、Match 拆到二级目录）。

### 2.2 循环

**M4. 有引用关系的 13 个顶层目录里，除 GlobalSettings 外的 12 个是同一个强连通分量** — 高（对"拆程序集"而言），确定
- 16 对直接互引：AI↔Network、Buddah↔{Foundation, GameData, Match, Network, New_Buddah, RaceIntro, VFX}、Config↔Match、Foundation↔Match、GameData↔Network、Match↔Network、Match↔UI、Network↔{New_Buddah, RaceIntro, UI}、New_Buddah↔RaceIntro。
- Tarjan 求强连通分量（脚本在仓库外的共享笔记目录 `code-review-scripts/r01`）：顶层只有 `GlobalSettings`（以及不引用别人的 `Debug`、`InputSystem`）不在环里；二级目录粒度下只有 `AI`、`Network/Match`、`Match/UI`、`Buddah/Input`、`Network/Utility` 等少数节点独立。
- 为什么：在这种结构上拆任何一个程序集都会编译失败；"底层"的 Config、Foundation 被个别边拖进了环（见 M5–M7），只要切掉 13 条边，`Config`、`Foundation`、`Match/Contracts`、`Rendering`、`AI` 就都成为无环的层（脚本试算过），剩下的玩法大环是 Buddah / New_Buddah / Network / RaceIntro / UI / VFX / Match / GameData。

### 2.3 越层引用（确定，逐条看过源码）

| # | 位置 | 越层 | 严重 | 建议 |
|---|---|---|---|---|
| M5 | `Assets/Scripts/Config/Runtime/DesignTableOverlay.cs:102-116`（`CurrentModeId`、`CurrentModeRule`） | Config → Match（`BuddahGo.Match.MatchRules.Current`） | 中 | 配表层只做"按 modeId 取行"，"当前模式是哪个"移到 Match（如 `MatchRules.CurrentModeId`），调用方改为 `DesignTableOverlay.ModeRule(MatchRules.CurrentModeId)`。这一条边就把 Config 拉进大环 |
| M6 | `Assets/Scripts/Foundation/PlayerRegistry.cs:13-54`（`List<BuddahMovement>`）；`Foundation/PlayerIdentity.cs:8,38`（`MatchRules.Current.DefaultPlayerName`） | Foundation → Buddah、Foundation → Match | 中 | `PlayerRegistry` 移到 Buddah（它本来就是玩家组件注册表）或改为注册接口；`PlayerIdentity` 的默认名由调用方传入 |
| M7 | `Match/Contracts/MatchRules.cs:7,13`（`new OnlineMatchRules()`）；`Match/Contracts/SkillPerceptionState.cs:19`（参数 `SkillExecutor`）**[PR#63]**；`Match/Contracts/RacerIdentity.cs:24`（`DesignTableOverlay.Text`）**[PR#63]** | Contracts → Match 实现、→ Buddah/ComboSkill、→ Config | 中 | 契约层只放接口与纯数据：默认规则实例由 `SessionLauncher` 注入；`ApplyCurtain` 参数改为 `NetworkObject`/`RacerId`；显示名兜底文案由调用方给 |
| M8 | `GameData/ObsessionFigure.cs:109-122`（`LeaderboardManager.Instance.Rankings`）**[PR#63]**；`GameData/LapProgress.cs:78`（`RaceFinishManager.Instance.LapsToFinish`） | GameData → Network/Gameplay | 中 | 执念的追赶值需要名次：通过 `MatchServices`/`RacerDirectory` 一类契约取；圈数直接读 `Match/Track.lapsToFinish`（表已是唯一来源，见 B2） |
| M9 | `New_Buddah/Core/BuddahPredictedMotor.Events.cs:697`（`RoomStateManager.Instance?.ReportLocalGameplayLive`）**[PR#63]**；`New_Buddah/Integration/BuddahPredictionMovementGateBridge.cs:13`（`ResultAreaInteractionGate`） | 预测核心 → Network/Room、Network/Session | 中 | 预测核心发事件（已有 `BuddahPredictionEventChannel`），由 Network 侧订阅；移动门控改为注入的 `Func<bool>` |
| M10 | `Network/Session/PropertySelection/PropertiesSelectionManager.cs:664-686`（`SceneFadeController`、`PropertySelectionMatchTransitionTimeline`）；`Match/SessionLauncher.cs:143,177`（`SceneFadeController.ResetSessionTransition`）；`Match/UI/SoloSetupPanel.cs:13` **[PR#63]**、`SoloUIBootstrap.cs:21`（`MainMenuUI`） | Network/Match → UI | 低 | `p6-validation.md` 明确"保留既有 Session 到 SceneFadeController 的调用"，是已知取舍。拆 UI 程序集前改为事件/接口（`ISceneTransition`） |
| M11 | `VFX/PalmInkTimeline.cs:45,63,95`（`Skill_PushProjectileHands flightSource`）；VFX → `ChargedProjectileFlight`、`SkillVfxDatabase` | VFX → Buddah/ComboSkill | 低 | 这是 `retired.md` 6.x 里"速度曲线由技能方继承"的用户决定的结果，方向合理（表现读玩法）。只是 VFX 同时被 Buddah 引用（`SkillVfxPhaseController`），形成 Buddah↔VFX 环；拆时把 `ChargedProjectileFlight`（纯函数）放到无环的公共层即可 |
| M12 | `Buddah/**` → Network 13 个类型（`LeaderboardManager`、`RaceCompletionTracker`、`RaceFinishManager`、`RoomStateManager`、`MatchResultPresentationCoordinator`…，17 个文件） | 角色/技能 → 比赛流程与网络管理 | 中 | 拆分时的主要工作量所在；按"角色读比赛状态"走 `Match/Contracts` 接口 |

## 3. 配表：无读取方的列、无人引用的行、整表无人读

范围：`scope.json` 的 `export` 共 38 个 sheet、约 430 列。`presentationOnly` 的 25 个 sheet 按设计不导出、不需要读取方，不计。

### 3.1 整表无人读
没有。38 个导出 sheet 对应的数据库列表在 Runtime 里都有读取方（grep `export-map.json` 的每个 `list` 名）。`Match/SelectionOption` 被 `PropertiesSelectionManager.cs:1158` 读，但 6 行全以 `##` 屏蔽，导出 0 行（结构审计 C2 的既定处理，非新发现）。

### 3.2 非 doc 列没有读取方

| # | 列 | 现状 | 严重/确定 | 建议 |
|---|---|---|---|---|
| T1 | `Skill/VfxRegistry.prefabPath`、`antiOf` | 运行时没有读取方，只有 `Assets/Tests/EditMode/SkillVfxTests.cs` 用它们校验与 `SkillVfxDatabase` 一致（comment 也这么写）。决定 22 要求"非 doc 列都有读取方" | 低，确定 | 二选一：标 `doc`（测试仍可从 CSV/xlsx 读），或在规范里把"EditMode 校验"写成合法读取方。同表 `followTargetPosition/inheritTargetRotation/inheritTargetScale` 三个 doc 列已登记在 `retired.md` §3 / §6.3，仍未删。**[PR#63]**（Skill.xlsx） |
| T2 | `Match/ModeRule.allowSkipSpectating`、`allowPause`、`showRacerNameTags`、`showOpponentsOnMinimap` | 只被 `OnlineMatchRules` / `SoloMatchRules` 转成 `IMatchRules` 属性（`IMatchRules.cs:19-23`），这四个属性在 Runtime 里 0 个调用方（只有 `MatchRulesTests`）。comment 自己写着"占位：游戏内尚无读取方" | 低，确定 | 按决定 22 标 `doc`，等功能实现再改回；`IMatchRules` 上四个属性同时删或保留为占位二选一。**[PR#63]** |
| T3 | `Skill/Skill.unlockState` 的 `hidden` 值 | 代码只判断 `"locked"`（`Buddah/ComboSkill/SkillDataBase.cs:191`），`hidden` 等同 `unlocked`；6 个反噬行都填 `hidden`，但反噬是否出现在选择界面实际由 `SkillSelectionRule` 决定 | 低，确定 | 要么让 `hidden` 生效（不进 `BuildSelectableSkillOptions`），要么把枚举收成 unlocked/locked 并在 comment 说明反噬行不看此列。**[PR#63]** |
| T4 | `Skill/DefaultLoadout.loadoutId` | 主键列，手写记录 `ProjectConfigRecords.cs:39`；`SelectionRuleRepository.TryGetDefaultLoadout` 按 ruleSetId/modeTag 取行，不看 loadoutId | 低，确定 | 主键列可以没有读取方；只记一笔，不必改 |
| T5 | `IMatchRules.RequiresReady`（代码侧） | 列 `ModeRule.requiresReady` 有读取方（`RoomStateManager.cs:218` 直接读记录），但同义属性 `IMatchRules.RequiresReady` 只有测试读 | 低，确定 | 统一一个入口：`RoomStateManager` 改读 `MatchRules.Current.RequiresReady`，或删属性。**[PR#63]**（IMatchRules.cs） |
| T6 | `Global/GlobalRule.target_frame_rate` 与 `Graphics/GraphicsOptionLevel.frameRate`（默认档） | 同一个值两处，靠 `DesignTableDomainRules.cs:161-225` 校验相等 | 低，确定 | 让 `FrameRateLock` 读 Graphics 默认档，GlobalRule 删此键（GlobalRule 就只剩空表，可整表转 doc）。**[PR#63]**（Global.xlsx、`DesignTableDomainRules.cs`） |

其余非 doc 列都找到了读取方：多数通过 `DesignTableOverlay.Apply` 写到同名字段（如 `AISkillTuning`、`AIDifficultyProfile` 的 PascalCase 字段按忽略大小写匹配；脚本第一次把 `public float A = 1, B = 2;` 这种多变量声明漏掉了，人工逐个确认 `SpeedDropMinSpeed` 等 30 个 Tuning 字段在 `AISkillWorld`/`AISkillDecision` 里都有使用）。

### 3.3 已是 doc、但记录的是"代码里的常量"（转入第 5 节处理）
`Movement.launchSuppressTurnSeconds`（comment 写 `RaceBodyIntroStateController.cs:560`，现在是 `:567`，行号已过期）、`Movement.minPreHandoffBypassSeconds`、`Movement.clearAngularVelocity`、`AI/SkillTuning.scaleShrunkThreshold`、`AI/SkillTuning.trackTableCellMeters`、`Match/SelectionStage` 5 个占位列、`Match/Lobby.minPlayers`。

### 3.4 无人引用的行（抽查）

抽查范围：`Skill/VfxRegistry` 全部 14 行；`UI/Text` 全部 341 个 key。其他表（SkillSelectionRule、Personality、TrackSpawn 等）没查。

- **VfxRegistry：0 行孤儿。** 14 个 vfxId 在技能 SO、`SkillVfxDatabase.asset`（14 条，与表一一对应）或代码里都有引用（grep `"id"` 与 `vfxId: id`）。旧行 `accel_anti_vfx`、`reverse_turn_trap_vfx`、`slowtrap_vfx` 等已按 `retired.md` §3 / §6.1 删除。
- **Text：341 个 key 里 251 个在代码、其他表 CSV、场景/prefab、技能 SO、Resources JSON 里都找不到**（脚本 `r01/text.py`；排除了 Text.csv 自身和生成的 `ProjectConfigDatabase.json`）：

| 类别 | 个数 | 状态 |
|---|---|---|
| `ui.<class>.<slug>` 自动 key（`ui.static.*` 43、`ui.propertiesselectorui.*` 25、`ui.steamlobbymanager.*` 24、`ui.propertiesselectionmanager.*` 23 …） | 228 | 已知待办（README "未完成"第 4 条 / 结构审计 E2：等策划定稿 key 再替换代码字面量）。其中 3 个 key 是中文 slug（`ui.static.技能描述文本`、`提示文本`、`标题文本`），`ui.static.4`、`ui.static.option_a` 的 zh 值就是占位文本 "4"、"Option A"，定稿时一并清掉 |
| `settings.graphics.group.*`（4）、`.title`、`.action.*`（3）、`.preset.ultra` | 9 | 画质菜单尚未实现（`Docs/rendering/graphics-settings.md` "Not implemented yet"），保留 |
| `selection.stage.map/skin`、`selection.option.{map,skin}_{a,b,c}.{name,desc}` | 14 | 对应行已 `##` 屏蔽（C2），保留或随 C2 一起删 |

结论：除定稿前的自动 key 外，没有发现"退休后忘删"的行。**[PR#63]**（UI.xlsx）

## 4. ScriptableObject / prefab / JSON 与配表重复定义

README 末尾"技能 SO、Buddah.prefab 与 `Tools/ai/difficulty-*.json` 上已被表接管的重复字段尚未删除"的具体清单如下。比对方法见第 0 节；"相等"指当前值相同，删字段前不用迁数据。

### 4.1 技能 SO（`Assets/Skills_Assets/Skills/*.asset`，12 个）**[PR#63]**
接管路径：`SkillDataBase.ApplyDesignTablesToSkills`（`Buddah/ComboSkill/SkillDataBase.cs:339-366`，Play 时写入 SO，退出 Play 由 `RestoreSkillAssetsOnExit` 还原）与 `SkillDatabase.Get*` 的"表优先、SO 兜底"（`SkillDataBase.cs:106-159`）。

| 字段（SO） | 表来源 | 个数 | 与表不一致的 |
|---|---|---|---|
| `cooldownSeconds`、`castLockSeconds`（`SkillAction.cs:12-13`） | `Skill.cooldownSeconds/castLockSeconds` | 12 × 2 | `Skill_Acceleration` 12/10 对表 5/3；`Skill_Acceleration_Anti` 8/6 对 5/3；`Skill_SlowTrap` 12/10 对 8/6；`Skill_PushProjectileHands` 18/15 对 12/10（如来神掌这一条 2026-10-12 筛查时就有，另外三条是之后改表产生的新漂移） |
| `obsessionGain`（`SkillAction.cs:17`） | `Skill.obsessionGain` | 12 | `Skill_BlackCurtain_Anti` = 100，表为空（反噬行不生效，执行器按原技能取值，A6），是死值 |
| `antiSkillId`（`SkillAction.cs:24`） | `Skill.antiSkillId` | 12 | 全部相等。注意 `SkillExecutor.ResolveAntiSkillId`（`SkillExecutor.cs:660-675`）在表为空时还会退到 SO、再退到 `"{id}_anti"` 推断，三层兜底 |
| `displayName`（`SkillAction.cs:9`） | `Skill.displayNameKey` → `UI/Text` | 12 | `giant_anti` SO 为"法相金身 （噬）"（多一个空格）；`reverseturn` / `reverseturn_anti` SO 为"逆因缘"，表为决定 21 的"逆姻缘" |
| 主时长字段（`durationSeconds` / `zoneDurationSeconds` / `rootDurationSeconds` / `holdDurationSeconds` / `buffDurationSeconds` / `invertDurationSeconds`） | `Skill.effectDurationSeconds`（`SkillDataBase.EffectDurationField`） | 11 | `Skill_Acceleration` 10 对 3；`Skill_Acceleration_Anti` 6 对 3；`Skill_SlowTrap` 10 对 6 |
| 家族字段（`extraForwardForce`、`extraMaxSpeed`、`slowForwardForce`、`slowMaxSpeed`、`perTargetReapplyCooldown`、`affectCaster`、`slowDurationSeconds`、`accelerationDurationSeconds`、`expandDurationSeconds`、`fadeOutDurationSeconds`、`maxOpacity`、`scaleMultiplier`、`shrinkDurationSeconds`、`growDurationSeconds`、`massMultiplier`、`forwardForceMultiplier`、`restoreDurationSeconds`） | `SkillEffectAcceleration/SlowTrap/BlackCurtain/Giant` | 27 | 全部相等 |
| 神掌弹道（`buildUpSeconds`、`chargedPushCooldownSeconds`、`delayedPushActionSeconds`、`projectileLifetimeSeconds`、`projectileImpulseStrength`、`projectileSpeed`、`projectileForwardOffset`/`additionalForwardSpawnOffset`、`projectileHeightOffset`/`additionalHeightSpawnOffset`、`projectileCount`、`projectileSpacing`、`projectileColliderSize`、`hitTurnTorqueImpulse`） | `Push` 的 `charged` / `charged_anti` 行（`PushChargedMap`/`PushChargedAntiMap`） | 19 | 全部相等 |

合计约 117 个字段实例，其中 15 个与表不一致（数值 12 个、显示名 3 个，上表列出）。这些不一致只在数据库缺失时才生效（表行存在就覆盖），但 `SkillConfigCrossValidator` 会持续报出来，Inspector 里看到的也是旧值，容易误导。

### 4.2 `Assets/Character/Prefab/Buddah.prefab` **[PR#63]**

| 组件 | 字段 | 表 | 结果 |
|---|---|---|---|
| `BuddahHandControl` | 10（`rotationSpeedDegPerSec`、`pushWindupSeconds`、`pushCooldownSeconds`、`pushLifetimeSeconds`、`pushImpulseStrength`、`pushForwardOffset/HeightOffset/SideOffset`、`pushLeft/RightSideBias`） | `Push.basic` | 全部相等 |
| `ObsessionFigure` | 9 | `Obsession` | 全部相等 |
| `BuddahRespawn` | 7（`continuousContactRespawnSeconds` …`respawnFloorTag`） | `Respawn` | 全部相等 |
| `LapProgress` | 8（另 `fallbackCheckpointProgresses01` 未序列化） | `Match/Track` | 全部相等 |
| `RaceCompletionTracker` | 8 | `TrackWrongWay` | 全部相等 |
| `BuddahPredictedMotorConfig` | 9（`forwardForce`、`maxSpeed`、`turnTorque`、`turnDecayPerSecond`、`turnInputMultiplier`、`pushGraceSeconds`、`pushExtraMaxSpeed`、`handoffGroundSnapMaxDistance`、`handoffGroundProbeHeight`） | `Movement` | 全部相等 |
| `Rigidbody`、实体 `CapsuleCollider` | `m_Mass/m_Drag/m_AngularDrag`、`m_Radius/m_Height/m_Center` | `Movement.mass/linearDrag/angularDrag/collider*`（`BuddahPredictedMotorConfig.cs:19-35` 在 Awake 写入） | 全部相等（引擎组件，不能删字段，只能接受重复） |
| `ComboSkillInput` | `stepWindowSeconds`、`maxBuffer` | `ComboInput` | 全部相等 |
| `RaceBodyIntroStateController` | `defaultHandoffLeadTime`、`velocitySampleDeltaSeconds` | `Intro.handoffLeadSeconds`、`Movement.velocitySampleDeltaSeconds` | 全部相等 |
| `BuddahMovement`（旧运动） | `launchInheritTime`、`launchBlendTime` | `Movement.launch*` | 相等 |
| `BuddahMovement`（旧运动） | `forwardForce` 50、`turnTorque` 30、`turnDecayPerSecond` 3、`maxSpeed` 80、`turnInputMultiplier` **1**、`pushGraceSeconds`、`pushExtraMaxSpeed` | 无覆盖（`BuddahMovement.cs:79` 只映射 launch 两列） | 与 `Movement` 是第二份副本，`turnInputMultiplier` 1 ≠ 表 2。预测模式下 `BuddahPredictionLegacyIsolationBridge.cs:19` 把 `BuddahMovement.enabled` 关掉，所以比赛不读；但 `MovementInvertTurnInputEffect.cs:25` 仍从它保存/还原 `turnInputMultiplier` |

### 4.3 AI 数据（不在 PR #63 清单的：`Resources/AI/*.asset`、`Tools/ai/*.json`、`AIDifficultyAssetTool.cs`）

| 副本 | 内容 | 与表 |
|---|---|---|
| `Assets/Resources/AI/{Easy,Normal,Hard}.asset` | `AIDifficultyProfile` 35 个字段 × 3 | 与 `AI/DrivingTier` 全部相等（`rolloutTail` 以 0/1/2 存，表以枚举名存，`AIDifficultyProfiles.cs:58-60` 转换） |
| `Tools/ai/difficulty-{easy,normal,hard}.json` | 同上 35 字段 × 3，是 `Assets/Editor/AIDifficultyAssetTool.cs` 的生成源 | 全部相等。README 已写"`AIDifficultyAssetTool` 待退役"：表已是权威，这条 JSON → asset 的生成链可以删，asset 本身作为 `Resources.Load` 的兜底可保留或改为纯代码默认值 |
| `Assets/Resources/AI/SkillPersonalities.json` **[PR#63]** | `Personalities`（5）、`Difficulties`（3）、`Tuning`（15 个键） | 与 `Personality`、`PersonalitySituationBias`、`SkillDifficulty`、`SkillTuning`、`SkillOpportunity` 全部相等（`target` 以枚举序号存）。`AISkillCatalog.Load`（`AISkillCatalog.cs:94-105`）先读这个 JSON 再叠表，所以它不能直接删：要先让 catalog 能只从表构造 |

### 4.4 其他
- `Assets/Data/SkillConfig/ProjectConfigSample.json`：无任何引用（代码、资产 GUID 都 grep 过），内容是旧 v1 结构（`slow_trap`、`behaviorType`、`modeTag: race`），`inventory-skills.md:13` 早已记为"无人引用"。**[PR#63]**（清单里有它）。低，确定。
- 场景 / manager 上的时长字段（`RoomStateManager._pregameCountdownSeconds`、`PropertiesSelectionManager._stageDurationSeconds`、`RaceFinishManager.postFirstFinishCountdownSeconds`、`ResultDecisionManager.decisionDurationSeconds`）与 `ModeRule` 重复，是 README 的 C1 代码待办；本次没有逐项比对场景值。
- 代码兜底字面量与表重复：`RaceRules.DefaultLapsToFinish = 3`（`Foundation/RaceRules.cs:3`，被 `LapProgress.cs:78`、`PlayerProgressReporter.cs:229`、`RaceCompletionTracker.cs:11` 用）对 `Track.lapsToFinish`；`AIDifficultyProfiles.cs:51` 的 20/80 对 `DrivingTuning.targetSpeedMin/Max`；`SessionLauncher.cs:66,81` 的 30/20 对 `Lobby.*TimeoutSeconds`。都是"无表时兜底"，值目前一致，保留可以，但兜底值应与表同改（见 V1 的反例）。

## 5. 写死在代码里、该进配表的平衡数值

只列影响玩法的时长/速度/距离/概率。`ThrustVectorPlanner` 的其余 `[Eng]` 常量、`SplineProgressTracker` 投影步长、网络重试间隔、`0.05f/0.01f` 下限钳制、`1e-4` 阈值不列。

| # | 位置 | 值 | 问题 | 建议表 | 严重/确定 |
|---|---|---|---|---|---|
| V1 | `Assets/Scripts/Buddah/ComboSkill/SkillExecutor.cs:54-55` **[PR#63]** | `CastConfirmDelaySeconds = 1f`、`AntiCastConfirmDelaySeconds = 1f` | 无表行时的前摇兜底。表里反噬行已是用户定的 2 s（`Skill.csv` comment："反噬行固定 2 s…不要改回 1"），兜底仍是 1 s；`ConfirmationSeconds`（`:47-48`）在没有表时也返回 1，AI 时机估计会偏短 | 删兜底改为"无表即报错"，或把常量改成 2 与表一致；值的唯一来源是 `Skill.castConfirmDelaySeconds` | 中，确定 |
| V2 | `Assets/Scripts/AI/ThrustVectorPlanner.cs:29`（改表部分 **[PR#63]**） | `TopSpeed = 79f, TopPace = 78f` | 等于"`Movement.maxSpeed`(80) 减一点"，已登记在 `AI/DrivingConstant`（presentationOnly，`[Eng] ≈ maxSpeed 关联`）但没接。策划改 `Movement.maxSpeed` 后 AI 的"到顶加速"判定不跟着变 | 不单独进表，改为由 `BuddahPredictedMotorConfig.MaxSpeed` 推导（如 `maxSpeed - 1`、`maxSpeed - 2`），或在 `AI/DrivingTuning` 加 `topSpeedMargin` | 中，确定（数值耦合确定；对 AI 表现的影响程度未实测） |
| V3 | `Assets/Scripts/RaceIntro/RaceBodyIntroStateController.cs:567` **[PR#63]**；默认参数 `Assets/Scripts/Buddah/BuddahMovement.cs:231` **[PR#63]** | `0.15f`（起跑交接后压住转向的秒数） | 已作为 doc 列 `Movement.launchSuppressTurnSeconds` 记录（comment 行号 `:560` 已过期），未接 | `Movement`：把该列改为非 doc，`RaceBodyIntroStateController` 读它 | 低，确定 |
| V4 | `Assets/Scripts/New_Buddah/Core/BuddahPredictedMotor.Events.cs:173` **[PR#63]** | `Mathf.Max(bypassRoomStateSeconds, 0.5f)` | 交接前 room-state 旁路下限，已作为 doc 列 `Movement.minPreHandoffBypassSeconds` 记录，未接 | `Movement`（同上） | 低，确定 |
| V5 | `Assets/Scripts/Buddah/ComboSkill/Skill_Giant/Skill_Giant_Anti.cs:22` **[PR#63]** | `ApplyScaleToOwner(..., 1f, 1f)` | 反噬缩小时的质量 / 推力倍率写死 1（登记在 `SkillConstant` 的 `giant_anti.massMultiplier/forwardForceMultiplier`，presentationOnly）。`SkillEffectGiant` 已有这两列，但反噬行留空、反噬类也没有这两个字段 | `SkillEffectGiant` 的 `giant_anti` 行：给 `Skill_Giant_Anti` 加 `massMultiplier`、`forwardForceMultiplier` 字段，表填 1 | 低，确定 |
| V6 | `Assets/Scripts/Buddah/ComboSkill/Skill_BlackCurtain/BlackCurtainScreenEffect.cs:7-9`；改表部分 **[PR#63]** | `MaxBlackHoleStrength = 15`、`ExpansionDelaySeconds = 0.5`、`MaxExpansionProgress = 2` | 黑幕的遮挡时序：受害者在施法后 0.5 s 才开始被遮，`SkillEffectBlackCurtain` 已管 `expandDurationSeconds/fadeOutDurationSeconds/maxOpacity`，这个延迟却在代码里，总时长 = 延迟 + 展开 + 停留 + 淡出 的关系也不透明 | `SkillEffectBlackCurtain` 加 `expandDelaySeconds`；强度/进度两个是 shader 表现参数，可留代码或进 `BlackCurtainPresentation`（presentationOnly） | 低，可能（是否算"平衡"取决于设计是否把遮挡开始时刻当玩法；没实测） |
| V7 | `Assets/Scripts/AI/AISkillWorld.cs:94` **[PR#63]** | `Mathf.Max(5f, speed)` | 算直道剩余秒数时的最低速度，与 `SkillTuning.speedDropMinSpeed`（同为 5）意思接近但没用它 | `AI/SkillTuning`：复用 `speedDropMinSpeed` 或新增 `straightMinSpeed` | 低，确定 |
| V8 | `Assets/Scripts/AI/AISkillWorld.cs:76`、`Assets/Scripts/AI/AISkillDecision.cs:15` **[PR#63]**；`AISkillWorld.cs:167` | `.99f`（判定"被缩小"）、`4f`（赛道采样格米数） | 已作为 `SkillTuning.scaleShrunkThreshold`、`trackTableCellMeters` 两个 doc 列记录（comment："代码常量，未接 AISkillTuning"） | `AI/SkillTuning`：接上，`AISkillTuning` 加两个字段 | 低，确定 |
| V9 | `Assets/Scripts/Buddah/ComboSkill/Skill_PushProjectileHands/ChargedHandProjectileRuntime.cs:45,258` | `ProgressCompletionFraction = 0.8f` | 蓄力进度在前摇的 80% 处到满（登记在 `SkillConstant.progressCompletionFraction`）；`VfxRegistry.chgRatio`（"蓄力层窗口占蓄力时长的比例"，`SkillVfxPhaseController.cs:137`）是相近概念，两者都在调"蓄力表现占前摇多少" | 归 `VfxRegistry` 一列（表现），或明确两者分工后在 comment 写清 | 低，可能（是否重复需要看蓄力层实际效果） |
| V10 | prefab 里的玩法尺寸：`SlowTrapZone.prefab` 的 scale 15（半径 7.5 m）、`Cube.prefab` 的推手碰撞盒；改表部分 **[PR#63]**（Skill.xlsx） | — | 登记在 `SkillConstant`（presentationOnly），值在 prefab 变换/碰撞体上，策划改不了 | 禅定缓流半径进 `SkillEffectSlowTrap.zoneRadius`（运行时设 scale），推手碰撞盒进 `Push.basic.colliderSize`（charged 行已有同名列） | 中，确定（登记确定；改动需要 prefab 配合） |

另记：`Assets/Scripts/Network/Gameplay/SkillVfxReplicator.cs:25` `SlotTailSeconds = 1.5f` **[PR#63]** 是特效收尾时长（纯表现），归 VFX 审查，不算平衡数值。

## 6. 大改动方案（只写方案）

### 6.1 拆程序集

目标分层（下层不引用上层）：

```text
L0  BuddahGo.Core        Foundation（去掉 PlayerRegistry）、Config（Definitions/Repositories/Runtime/Validation）、Match/Contracts、ChargedProjectileFlight 这类纯函数
L1  BuddahGo.Rendering   Rendering/（只依赖 Core）
L2  BuddahGo.Gameplay    Buddah、New_Buddah、GameData、RaceIntro、Match（实现）、Network、VFX、UI —— 现在的大环，先整体放一起
L3  BuddahGo.AI          AI/（AI/Diagnostics 另拆 BuddahGo.AI.Diagnostics，defineConstraints 取代 #if）+ Network/Match/MatchSpawnManager（它是唯一引用 AI 的 Network 文件）
    BuddahGo.Editor      现 Scripts/Editor + Assets/Editor 里的游戏工具
```

先后顺序（每步单独提交、能编译、跑全量 EditMode）：
1. **Editor 合并**（风险最低）：`Assets/Editor` 中约 14 个引用游戏类型的工具（M1）迁入 `Assets/Scripts/Editor/{AI,VFX,Rendering,Network}`。只动编辑器，不涉及 UnityEvent。好处：`SkillVfxTimingSync` 等可被 `BuddahGo.Tests` 直接测。
2. **切 L0 的 13 条越层边**（M5–M9、M11 中 VFX→Buddah 的纯函数部分）：纯代码重构，不改程序集，先把 Config / Foundation / Contracts 变成无环叶子。可用 2.1 的脚本复跑确认环消失。
3. **建 BuddahGo.Core**：在 `Foundation/`、`Config/`、`Match/Contracts/` 放 asmdef（或把它们挪到 `Assets/Scripts/Core/`）。需要 `InternalsVisibleTo("BuddahGo.Tests")` 和对上层的 `InternalsVisibleTo`（或把跨层用到的 internal 改 public）。
4. **建 BuddahGo.Rendering、BuddahGo.AI**：AI 需先处理 `LoadoutRules`（`Network/Session/PropertySelection/LoadoutRules.cs:8`，internal，被 `AISkillCatalog` 用）；`AI→Network` 的其余 11 个类型（`RaceFinishManager`、`PropertiesSelectionManager`、`ResultDecisionManager`…）改走 Contracts，否则 AI 只能放在 Gameplay 之上。
5. **（可选，后期）拆 Gameplay 大环**：顺序建议 VFX（切 `VFX→New_Buddah` 的 `BuddahPredictionVisualRootBridge`）→ UI（切 M10）→ Network 与 Buddah/New_Buddah 分开（M9、M12，工作量最大）。`New_Buddah/Visual/SoloPresentationTimeline.cs:8` 是 internal 且被 RaceIntro 用，拆时要处理。

风险与对策：
- **UnityEvent 程序集限定名**：`MainMenuUI`（3）、`RaceFinishManager`（1）、`ResultPresentationTimelineBridge`（2）落在哪个新程序集，就要改场景里 6 处 `m_TargetAssemblyTypeName`，并按 R10 实际点按钮、跑 Timeline 回调（`p6-validation.md`）。把这三个类放进同一个程序集可以只改一次。
- **改命名空间**会同时改 UnityEvent 类型名，与上一条一起做。MonoBehaviour/SO 的脚本引用按 GUID，不受影响；FishNet 的 RPC/SyncVar 代码生成按程序集处理，每个含 `NetworkBehaviour` 的新程序集都要确认 FishNet codegen 生效（做一次 host/client 冒烟）。
- **`internal` 跨程序集**：198 处 internal，跨目录使用的 internal 类型至少有 `LoadoutRules`、`SoloPresentationTimeline`；成员级的没统计，拆时编译器会逐个报出来。
- **反射**：`DesignTableOverlay.FindField` 按字段名反射，跨程序集不受影响；`SixRacerResultsTests` 用 `Type.GetType("…, Cinemachine")`，程序集名不变即可。
- **PR #63**：它的清单里 `Assets/Scripts` 下有 188 项（含 .meta），包括 `SkillExecutor`、`SkillDataBase`、`AISkillWorld`、`BuddahPredictedMotor.Events` 等越层点。步骤 2 以后全部等 PR #63 合并再做。步骤 1 迁移的都是 `Assets/Editor` 里现有文件，不在清单里，可以先做；但 PR #63 会在 `Assets/Editor` 新增 `MechanicsPracticeTools.cs`、`MenuEnvironmentImporter.cs`，并改 `Scripts/Editor` 的 `SkillConfigCrossValidator.cs`、`DesignTables/DesignTableDomainRules.cs`、`DesignTableSchema.cs`，合并后要把两个新文件一并归类。

### 6.2 挪数值进配表（按表分组）

前置：除 `Graphics.xlsx` 外的 xlsx 都在 PR #63 清单里，**全部等 PR #63 合并后做**；每组按 README"新增或改列"流程（改表头 → `gen_records.py` → 编译 → 导入 → `DesignTableTests` / `DesignTableRuntimeTests`）。

| 表 | 改动 | 删除的重复来源 |
|---|---|---|
| `Skill/Skill` | 无新列。`SkillExecutor` 删前摇兜底常量（V1），无表时报错 | 技能 SO 的 `cooldownSeconds`、`castLockSeconds`、`obsessionGain`、`antiSkillId`、`displayName`、主时长字段（4.1），连同 `SkillConfigCrossValidator` 的对应比较；`SkillDatabase.Get*` 的 SO 兜底分支 |
| `Skill/SkillEffect*`、`Skill/Push` | `SkillEffectGiant` 反噬行填 `massMultiplier/forwardForceMultiplier`（V5）；`SkillEffectBlackCurtain` 加 `expandDelaySeconds`（V6）；`SkillEffectSlowTrap` 加 `zoneRadius`、`Push.basic` 的 `colliderSize` 接到推手碰撞盒（V10） | SO 的家族字段与神掌弹道字段（27 + 19，值都相等，可直接删）；`SkillConstant` 里对应的登记行 |
| `Skill/VfxRegistry` | `prefabPath`、`antiOf` 标 doc 或在规范里认可"测试读取"（T1）；删三个已退休 doc 列（`retired.md` §6.3） | — |
| `Movement/Movement` | `launchSuppressTurnSeconds`、`minPreHandoffBypassSeconds` 从 doc 改为读取列（V3、V4） | `BuddahMovement` 旧运动的 7 个字段（4.2，前提是确认非预测模式已不再使用；`MovementInvertTurnInputEffect` 改读 `BuddahPredictedMotorConfig`）；`Buddah.prefab` 上 `BuddahPredictedMotorConfig`、`BuddahRespawn`、`ObsessionFigure`、`BuddahHandControl`、`ComboSkillInput`、`RaceCompletionTracker`、`LapProgress` 的被接管字段可改为 `[HideInInspector]` 兜底或删（删会丢 prefab 数据，但值与表相等，丢了也能从表恢复） |
| `AI/SkillTuning`、`AI/DrivingTuning` | 接 `scaleShrunkThreshold`、`trackTableCellMeters`（V8）、直道最低速度（V7）；`TopSpeed/TopPace` 改为由 `Movement.maxSpeed` 推导（V2） | `Tools/ai/difficulty-*.json` + `AIDifficultyAssetTool.cs`（4.3）；`SkillPersonalities.json` 在 `AISkillCatalog` 能只从表构造后删 |
| `Match/ModeRule` | 4 个占位列转 doc（T2）；四个 manager 只读 ModeRule（C1） | 四个 manager 的序列化时长字段；`IMatchRules` 的 4 个占位属性与 `RequiresReady` 二选一入口（T5） |
| `UI/Text` | 定稿 228 个 `ui.*` 自动 key 并替换代码字面量（E2）；3 个中文 slug key 改英文 | 技能 SO `displayName` |
| `Global/GlobalRule` / `Graphics` | `target_frame_rate` 只留 Graphics 默认档一处（T6） | `GlobalRule` 行 |
| 其他 | `Assets/Data/SkillConfig/ProjectConfigSample.json` 删除（4.4） | — |

按 `retire-before-adding` 的约定，每删一组重复字段都在对应 `retired.md` 登记一行（当前只有 `Docs/vfx/retired.md`；玩法侧没有 `Docs/gameplay/retired.md`，需要新建）。
