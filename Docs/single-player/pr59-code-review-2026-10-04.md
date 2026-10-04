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

1. **`GameLog.Verbose` 无开关**（`Assets/Scripts/Foundation/GameLog.cs:8`）：它直接 `Debug.Log`。本 PR 在 `SkillExecutor`、`ComboSkillInput`、`LeaderboardTMPUI` 等处逐条包了 `if (NetDebug.EnableVerboseLog)`，但仓库其余几十处 `GameLog.Verbose` 仍无条件输出（含 `Skill_*`、`RaceBodyIntroStateController` 每帧级日志）。建议后续把开关下沉到 `GameLog.Verbose` 内部并删掉散落的 if；这是跨模块行为变化，本次不做。
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

## 7. 行数结果

清理后的运行时 C# 为 +138 / −119（净 +19 行）：删掉的重复与死代码约 60 行，被 `ValidateConfiguration` 表驱动（+22）、新抽方法的签名和说明注释抵消。本 PR 真正的行数大户是 1 220 行诊断 harness 与 4 160 行测试（含三份 Legacy 冻结实现）；前者按仓库"诊断探针只门控不删除"的规则保留，后者是等价/回归契约。要继续压行数，下一步是 D7（harness 公共基类，需核对证据 CSV/JSON 不变）与 S4（`AISkillCaster` 拆分），都需要场景级测试护航，建议作为独立 PR。
