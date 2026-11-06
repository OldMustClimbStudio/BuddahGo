# PR #59 代码复核与清理记录（2026-10-04）

复核对象：`feat/single-player-mode` @ `3a3560c`，对比 `origin/dev` @ `fc7ab59`。本文只记录代码质量与结构问题；功能验收、自然赛与性能数据以 [实施记录](ai-skill-implementation-2026-10-03.md) 与 [实现复核](ai-skill-implementation-review-2026-10-03.md) 为准。行号对应 `3a3560c`。

## 1. 范围与规模

| 类别 | 文件 | 新增行 | 说明 |
|---|---|---:|---|
| AI 产品代码 `Assets/Scripts/AI/` | 15 | ≈2 200 | 跑线规划、技能人格、世界快照、配置 |
| AI 诊断 harness（`#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`） | 5 | ≈1 220 | A1/A2/A3、受控效果矩阵、生命周期检查 |
| `Assets/Scripts/Match/` + `Contracts/` | 25 | ≈890 | 会话、规则、计时、身份、服务定位 |
| Solo UI（代码生成） | 5 | ≈580 | 设置面板、结算、退出确认、工厂 |
| 其他新文件 | 3 | ≈220 | 头顶标记、Solo 呈现时间线、圈时暂存 |
| 既有运行时文件修改 | 49 | ≈+1 100 / −315 | 技能执行器、手部、预测 motor、排名/结算、房间流转 |
| EditMode 测试 | 32 | ≈4 160 | 含三份 Legacy 冻结参考实现 |
| Editor 工具 | 6 | ≈280 | 验收构建、资产生成、测试回执 |

PR 总差异 609 个文件 / +97 512 行中，约 86k 行是 `Tools/ai/` 的 JSON/CSV/JSONL 证据与字体/占位资源，不属于代码复核范围。

## 2. 总体结论

- **架构方向正确**：`Match/Contracts` 把"谁驾驭这辆车"（`RacerAuthority`）、"这是什么比赛"（`IMatchRules`）、"比赛服务"（`MatchServices`）抽成接口，联机路径通过 `OnlineMatchRules` 保持原值；AI 通过 `ISteeringOverride` / `ISkillCastContinuation` 两个窄接口接入 motor 与技能执行器，没有建立第二套权威。
- **没有真正意义的 god script**，但有三个"趋向"文件需要控制：`AISkillCaster`（299 行，7 种职责）、`AITuningHarness`（440 行，诊断）、`SkillExecutor`（AI 分支渗入共享执行器）。
- **重复与死代码可控**：约 60–80 行可直接去掉或合并，详见第 4 节；本次已处理其中不改变行为的部分。
- **诊断 harness 与产品代码同目录**是最影响"模块化到位"观感的问题，已移入 `AI/Diagnostics/`。
- 未发现会改变已验收行为的缺陷；第 3 节的 P1 项是运行时风险提示，不是已复现的 bug。

## 3. 需要关注的风险（P1，未改动，供决策）

1. **`GameLog.Verbose` 只有编译期开关**（`Assets/Scripts/Foundation/GameLog.cs:8`）：它带 `[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]`，正式包里整段调用连参数求值一起被编译掉，但在编辑器与 Development 包里无条件输出。本 PR 在热路径上逐条包的 `if (NetDebug.EnableVerboseLog)` 是为了省掉字符串拼接，属有意为之；运行时总开关放进 `Verbose` 内部并不能省这部分开销，因此不建议改。（修正：首版报告误写为"无开关"。）
2. **`AISkillWorld.RefreshRoster` 只按人数变化刷新**（`AISkillWorld.cs:141-154`）：同一局内若有车被替换而人数不变，缓存的组件引用会失效。Solo 固定六人且 Rematch 重建 `MatchSpawnManager`，当前不触发；联机复用前需改为按身份集合比较。
3. **`AISkillCatalog.Load` 在 FishNet 生成回调里抛异常**（`AISkillCatalog.cs:79-90` ← `MatchSpawnManager.SpawnSoloAI`）：配置缺失时 Solo 开局会在 `ServerManager.Spawn` 调用栈中断。fail-fast 可接受，但建议在 `SessionLauncher.StartSoloHost` 前置校验，把错误交给设置面板显示。
4. **`SoloUIBootstrap.BuildMenu` 用 UnityEvent 持久目标方法名识别联机按钮**（`SoloUIBootstrap.cs:48-63`）：按钮绑定改名即静默失效。占位 UI 可接受，正式 UI 应改为序列化引用。
5. **`SessionLauncher.IsOnlineAvailable` 每帧由 `SoloUIBootstrap.Update` 求值**，内部 `as Multipass` + `GetTransport(0)` 两次类型探测；成本低但无需每帧。建议改为事件或节流。

## 4. 代码质量发现与处理

标记：✅ 本次已处理（不改变行为）；⏭ 建议后续，本次不动。

### 4.1 重复 / 死代码

| # | 位置 | 问题 | 处理 |
|---|---|---|---|
| D1 | `BuddahHandControl.cs:103-104` | 新增事件 `ServerPushAccepted` / `ServerPushSpawned` 全仓库无订阅者 | ✅ 删除事件与两处 `Invoke` |
| D2 | `LeaderboardManager.cs:364-366` | 同一 `TryGetResult` 调两次（先查有无再取值） | ✅ 合并为一次 |
| D3 | `SessionLauncher.cs:23-27, 45-56, 58-65, 88-92` | `IsOnlineAvailable` 与 `TryGetTransport` 各自探测 Multipass；前者用 `is FishyFacepunch`，后者用 `GetType().Name == "FishyFacepunch"`；`StartHost` 与 `StartOnlineClient` 的会话前置五行重复 | ✅ 抽 `TryGetMultipass`/`BeginSession`，统一类型判断 |
| D4 | `AISkillCaster.cs:163-170, 236-243` | 两段相同的 Stopwatch + `InputMarker` 计时包裹；`GetComponent<AIRacerDriver>()` 在 `Configure` 与 `OnEffectsReset` 各取一次 | ✅ 抽 `InjectInput`，缓存 `_driver` |
| D5 | `SplineRacingLine.cs:118-127, 158-168` | `Build` 与 `PrepareCurvatures` 各算一遍弦角曲率（无符号/有符号）；`Wrap` 已存在但 `WindowPace`/`TangentYaw` 重写取模 | ✅ `WindowPace`/`TangentYaw` 统一用 `Wrap`。⏭ 曲率合并已尝试并**回退**：数学上 `|SignedAngle| == Angle`，但 Mono 对单表达式 float 链使用更高中间精度，先存成有符号曲率再取绝对值会让 pace 差 1 ulp，`AICostEquivalenceTests` 三项逐位对比随即失败；已在代码注释中记录原因 |
| D6 | `AISkillCaster.cs:26-28` | `Marker` / `InputMarker` 为 public static，仅类内使用 | ✅ 改 private |
| D7 | `AITuningHarness.cs:405-410` 与 `AISkillRaceHarness.cs:260-261` | 两份 `Percentile`；两份命令行解析；两份 targetFrameRate/vSync 保存恢复 | ⏭ 诊断输出是证据格式，合并需同时验证 CSV/JSON 不变 |
| D8 | `ThrustVectorPlanner.cs:35-36` | `LastWobbleDegrees` / `LastSelectionWasMistake` 无读者（含测试） | ⏭ 保留为 EditMode 诊断入口，成本两行 |

### 4.2 结构 / 模块边界

| # | 位置 | 问题 | 处理 |
|---|---|---|---|
| S1 | `Assets/Scripts/AI/` | 5 个诊断 harness（1 220 行）与 15 个产品文件混在一个目录 | ✅ 移入 `AI/Diagnostics/`（保留 `.meta` GUID；仍受 `#if` 门控） |
| S2 | `SkillExecutor.cs:176-191` | `ResetActiveSkillEffectsForOwner` 内嵌 AI 专用清理块（手部、感知、修饰、陷阱），与真人路径交织 | ✅ 抽 `ResetServerAISkillState(bool)`，调用点一行；逻辑不变 |
| S3 | `AIRacerDriver.cs:123-124` | `_motor.IsServerInitialized && (_motor.IsOwner \|\| !_motor.Owner.IsValid)` 自行判定权威，与 `RacerAuthority.HasLocalControl` 并存 | ✅ 改用 `RacerAuthority.HasLocalControl`；两者仅在"无身份的无主对象"上不同，而驾驶器只在有身份的 AI 或 harness 的 owner 车上启用 |
| S4 | `AISkillCaster.cs` | 一个组件承担：组件装配、Tick 调度、搓招注入、决策、瞄准、统计计数、遥测 Emit | ⏭ 建议拆 `AISkillInput`（注入 + 计时）与 `AISkillTelemetry`（Observation/Emit）；需要场景级测试护航，留作独立 PR |
| S5 | `AIDifficultyProfile.cs:79-95` | `ValidateConfiguration` 一个 15 行布尔链；NaN 只检查 18 个字段 | ✅ 改为 `Range(value,min,max)` 表驱动，所有字段统一拒绝 NaN/∞（对现有三份资产无影响，`AIDifficultyProfilesTests` 覆盖） |
| S6 | `AIDifficultyProfile.cs` | 已退役 beam 参数（`BeamWidth`、`HorizonSeconds`、`DiverseSearch`…）与 thrust 参数混排 | ⏭ `ForwardSimPlanner` 为等价测试与 A1/A2 证据保留；可在证据归档后删 |
| S7 | `MatchSpawnManager.cs:528-542` | AI 组件判空在 `Spawn` 之后 | ✅ 先取齐组件再 Spawn（prefab 完整时行为相同，缺件时不再留下半个网络对象） |
| S8 | `Match/Contracts/SkillPerceptionState.cs:19` | Contracts 层静态方法依赖 `SkillExecutor` 具体类型 | ⏭ 可接受；注释已说明放这里是为了技能不引用 AI 实现 |
| S9 | `Docs/architecture.md` 系统表 | 缺 `AI/` 与 `Match/` 两行 | ✅ 补表 |

### 4.3 可读性 / 一致性

| # | 位置 | 问题 | 处理 |
|---|---|---|---|
| R1 | `MatchResultPresentationCoordinator.cs:282-285` | 花括号缩进错位 | ✅ |
| R2 | `LeaderboardManager.cs:364`、`SoloUIBootstrap.cs:95`、`PropertiesSelectorUI.cs:453,464` | 已 `using BuddahGo.Match` 仍写全限定名 | ✅ 去掉冗余限定 |
| R3 | `LocalPlayerOverheadMarker.cs`、`AcceptedLapTiming.cs` 在全局命名空间；UI 在 `SteamMultiplayer.UI` | 命名空间不统一 | ⏭ 与仓库既有风格一致，不单独处理 |
| R4 | 多处单行多语句（`a = 1; b = 2;`）与 300 字符长行 | 提高了"行数"指标却降低可读性 | ⏭ 不以格式改动稀释本次 diff |

## 5. 本次清理的边界

- 不改任何技能、驾驭、计时、结算的数值与分支；不改 harness 输出格式；不改序列化字段与 RPC 签名。
- 文件移动只涉及诊断 harness 与其 `.meta`；prefab/scene 不引用这些类型。
- 测试：清理前后分别在 Unity 2022.3.55f1c1 批处理模式运行 `BuddahGo.Tests` 全部 EditMode 用例，结果见第 6 节。

## 6. 验证

Unity 2022.3.55f1c1 批处理模式（`-batchmode -nographics -runTests -testPlatform EditMode -assemblyNames BuddahGo.Tests`，无编辑器实例，`.worktree/single-player-mode`）：

| 树 | 用例 | 通过 | 失败 | 编译错误 |
|---|---:|---:|---:|---:|
| 基线 `3a3560c` | 374 | 369 | 5 | 0 |
| 清理后 | 374 | 369 | 5 | 0 |

- 失败集合完全相同：`ThrustVectorPlannerTests.OfflineLapComparison`、`StraightOffsetConvergesWithoutExcessOvershoot(±1)`、`ThrustVectorSpecTests.F06RepresentativeCornerDemandIsNotPDSaturated`、`Grid162Once`（后两项为 Explicit；`Grid162Once` 需要外部输出路径）。这些在基线即失败，与本次清理无关，按 PR 既有说明属离线对比/网格用例。
- 中途一次尝试（D5 曲率合并）使 `AICostEquivalenceTests` 三项逐位对比失败（pace 相差 1 ulp），已回退并重跑，结果恢复到与基线逐项一致。
- 文件移动后 Unity 首次导入会报 5 条 `CS2001`（旧路径缓存），刷新后消失；最终日志 0 条编译错误。生成的 `AI/Diagnostics.meta` 已随提交入库。
- 未运行：非 Development 构建、自然赛与性能窗口。清理不涉及序列化字段、RPC、场景与 prefab，按 CONTRIBUTING 的"按改动验证"选择编译 + 全部 EditMode。

## 8. 第二、三轮：优化与退休（2026-10-04 晚）

用户追加两条指令：先"在不影响任何功能的情况下全部优化"，随后"测试如果是 legacy 就直接退休，只保留最小可运行切片"。两轮分别成为独立提交，均以批处理 EditMode 对照基线验证。

### 8.1 第二轮（行为不变）

| 项 | 处理 |
|---|---|
| Solo 圈时暂存队列 | 删除 `AcceptedLapTiming`（59 行）及其 12 项测试；`LapProgress.RecordCompletedLap` 在过线被验证的同一时刻用权威时钟直接记录，`RaceTiming` 本就拒绝重复、倒退与跳圈，保留按 `RaceFinishManager.LapsToFinish` 的截断。周期上报路径不变，联机仍只走周期观察 |
| `AISkillCaster` 拆分 | 计数器、`Observation`/`Emit` 与每帧耗时统计移入 `AISkillTelemetry`；caster 只剩调度、决策、瞄准与注入。harness 改读 `Caster.Telemetry` |
| harness 公共底座 | `HarnessSupport`：命令行取值、测量帧率保存恢复、Solo 启动→配装→皮肤的 stage 机，两份重复各删约 40 行。`Percentile` 与事件名保持各自原样，证据格式不变 |
| `ThrustVectorPlanner` | 20 个裸数字改为命名常量；`Rollout` 的 20 个参数改为 `RolloutContext`，每次选择只构造一次。无算术改动 |
| 结果 | 362 项 / 357 通过 / 5 失败，失败集合与基线相同；减少的 12 项即被删队列的测试 |

### 8.2 第三轮（legacy 退休，最小切片）

删除：`ForwardSimPlanner`（beam 规划器）、三份 `Legacy*` 冻结参考、五份 `ThrustVectorRevision*Fixture`、整个 `ThrustVectorPlannerTests`（类级 `[Explicit]`，自述为 revision-six 历史实验）、`AICostEquivalenceTests` 中三项对照、`AIDrivingTests` 中四项 beam 用例、`ThrustVectorSpecTests.F06`（依赖 RevisionSix）。产品侧随之去掉 `ISteeringPlanner`、`PlanObservation` 的 12 个 beam 字段、`SplineRacingLine` 的三参 `PreparePace`，以及 `AIDifficultyProfile` 的 13 个 beam 专用成员；三份 `difficulty-*.json` 去掉对应键并用 `AIDifficultyAssetTool.Build` 重新生成资产（仅删字段，Easy/Normal/Hard 的 target 67/74/80、reaction 8/3/0、Hard maxAngle 90 不变）。保留并迁移：`SplineRacingLineTests`（几何快照、Capture 共享、pace 窗口三项）、`AIDrivingTests.ProfileRejectsNonFiniteAndOutOfRangeValues`。

| 树 | 用例 | 通过 | 失败 | 编译错误 |
|---|---:|---:|---:|---:|
| 第二轮后 | 362 | 357 | 5 | 0 |
| 第三轮后 | 297 | 296 | 1 | 0 |

唯一失败 `ThrustVectorSpecTests.Grid162Once` 为 Explicit 的 162 次参数网格，需要 `--thrust-offline-output`，在基线即如此；其余四项基线失败全部随 legacy 退休。减少的 81 项全部是对照/历史用例，新增 4 项通过。

**保留且说明理由**：五个诊断 harness 仍是 A1–A4 验收的取证工具且受 `#if` 门控，按仓库"探针只门控不删除"的既有规则保留；`Tools/ai/` 下的证据数据与 `plot_spin.py` 只读历史轨迹，本轮未动（第四轮已删除 legacy 部分，见 §9）。

**未做**：五个静态服务定位器合并、`SoloUIBootstrap` 改序列化引用（需改场景）、推掌协程改 tick 队列（行为敏感）。

## 7. 行数结果

清理后的运行时 C# 为 +138 / −119（净 +19 行）：删掉的重复与死代码约 60 行，被 `ValidateConfiguration` 表驱动（+22）、新抽方法的签名和说明注释抵消。本 PR 真正的行数大户是 1 220 行诊断 harness 与 4 160 行测试（含三份 Legacy 冻结实现）；前者按仓库"诊断探针只门控不删除"的规则保留，后者是等价/回归契约。要继续压行数，下一步是 D7（harness 公共基类，需核对证据 CSV/JSON 不变）与 S4（`AISkillCaster` 拆分），都需要场景级测试护航，建议作为独立 PR。

## 9. 第四轮：最终 legacy 清理（2026-10-04 夜）

用户指令："最后一轮清理，重点看新增代码里可以清理的 legacy 内容，保持完整但干净"，并要求先确认技能（特别是特效）没有丢失 LFS 资源、功能无回归。三个提交：

| 提交 | 内容 |
|---|---|
| `118ded0` | 死成员：`GameNetworkManager` 三个无调用者的转发方法、`IRacerDirectory.TryGetByObject`、`IRaceTiming.Reset`（及 `RaceTimingSync` 的显式实现）、`SplineRacingLine.ProjectSmall`、规划器只写不读的 `LastWobbleDegrees`/`LastSelectionWasMistake`（不改算术与随机数顺序）；`LeaderboardTMPUI` 状态遥测只在 verbose 时拼接；harness 去掉恒真的 `TryDisable` 分支与 `% 1` 条件；`A1DrivingTools` 只保留 harness 菜单（Build/RunChecks 与 `ThrustVectorAcceptanceBuild`/`AISkillAcceptanceTools` 重复）；验收过滤器去掉不存在的 `PushAttackTimingTests`。测试：退休 Explicit 的 F11–F13 扫参与 `Grid162Once`；三项错放用例归位到 `SplineRacingLineTests`/`AIDifficultyProfilesTests`；六人重复用例并入 `RacerIdTests`/`RaceEndPolicyTests` |
| `a205d46` | `Tools/ai` 删除 227 个 beam 时代与被取代迭代的证据文件（V5 `normal.json`、A2/spin 结果与画图脚本、`performance/`、11 个变体 profile、18 次被取代试跑、`superseded-*`、`wall*` 扫参输出、revision-six/lateral/diagnostics/spec-fixtures 目录、两个无读者 fixture）。保留三档 `difficulty-*.json`、在用 fixture、`scripts/`、`race4`–`race6` 难度验收与技能分析脚本。文档全部保留：22 个指向已删文件的链接改为 `20401e5` 的 GitHub 永久链接，历史报告中的纯路径以 `20401e5` 为准 |
| `fcb9d97` | RaceMap 中 Layout4P/5P/6P 三个实例逐 knot 的覆盖（约 2.4 万行）Apply 回 prefab，prefab 不再存过期路径。批处理导出整场景全部序列化属性（38 754 行，含 7 275 行 knot 字段），Apply 前后排除实例 ID 后逐行一致 |

| 树 | 用例 | 通过 | 失败 | 编译错误 |
|---|---:|---:|---:|---:|
| 基线 `20401e5` | 297 | 296 | 1 | 0 |
| `118ded0` | 293 | 293 | 0 | 0 |
| `fcb9d97` | 293 | 293 | 0 | 0 |

用例集合逐项对比：删除 4 项（F11–F13、`Grid162Once`），8 项因迁移/合并改名（`SoloWithOtherRacersWaitsForAllOrCountdown` 参数化为 3/5 个 AI，覆盖原六人用例），其余结果不变。PR 相对 `dev` 的新增行：本轮前 95 743，本轮后约 60 700。

**技能与特效资源核查**（只读，`dev` 与本分支对照）：从三个场景、`DefaultPrefabObjects` 与全部 `Resources` 出发的 GUID 闭包共 450 个资产（技能/VFX 114 个），被追到的 LFS 文件全部是实际内容、0 个指针；PR 在 `Assets/` 下引入的唯一 LFS 文件 `Flare00.PNG` 本地为真实 PNG，远端对象齐全；PR 没有让任何资源掉出构建，反而补上了 dev 缺失的加速特效。逐项检查 `Buddah.prefab`、`如来神掌/` 全部 prefab、`沙砾.prefab` 与全部技能资产：LFS 指针 0。两个悬空 GUID 均不影响画面：材质 `b739a3f02ff77bf48b7636e64c3e3b4c` 只挂在 `沙砾`、`如来神掌`、`射击特效`、`slowtrap_vfxZone` 四个 prefab 的根粒子系统上，这四个根发射器的 Emission 均关闭、无 burst，从不产生粒子；其余 31 个可见子发射器材质完好。贴图 `1c0a28d7a8db4e93bc4f05b6b878cd79` 只存在于 Piloto 火焰材质的 `_diff` 属性，当前 `UberFXSG` 着色器没有该属性。两者在全部分支与 60 个 PR ref 的历史、本机磁盘和 12 个 Asset Store 缓存包中均未找到，无法从 GitHub 恢复。反复出现的洋红/不可见是 ShaderGraph/VFX Graph 本地导入缓存失效，用 `Tools > BuddahGo > Repair Original VFX Imports` 修复（见 [VFX 恢复记录](../vfx-asset-recovery.md)）。

**未做（需用户决定，不属于 legacy）**：`IMatchRules` 五个暂无读者的标志（design.md 列为合同）；`BuddahGoSoloCJK.asset` 8.66 MB 静态图集仅用 32%，可重烘焙；VFX 恢复拆为独立 PR；`Skill_ReverseTurn` 执念增益 200 仍是平衡实验值。
