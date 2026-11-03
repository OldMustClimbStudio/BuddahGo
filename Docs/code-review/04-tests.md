# 04 · 测试精简（EditMode）

审查对象：`refactor/vfx-refactor` @ `ddb00807`，`Assets/Tests/EditMode/` 下 41 个测试类加 1 个辅助类（`SkillVfxTestRig.cs`）。
只读审查，没有改代码，也没有跑 Unity。用例数是静态数出来的（`[Test]`/`[UnityTest]` 算 1 个；带 `[TestCase]` 的方法有几行就算几个），合计 **403**，跟运行器报的 402–403 一致。

## 0. 结论

| | 现在 | 建议后 | 减少 | 其中真删（这部分覆盖会少） | 其中合并（断言全留，只是用例变少） |
|---|---:|---:|---:|---:|---:|
| 全部 | **403** | **147** | **−256（63.5%）** | 28 | 228 |
| 本次能落地（不在 PR #63 清单里的 31 个文件） | 296 | 102 | **−194** | 19 | 175 |
| 要等 PR #63（10 个文件，标 [PR#63]） | 107 | 45 | −62 | 9 | 53 |

要说清楚：**真正"没有必要"的只有 28 个**：钉死配表数值的、测常量或 Unity 自身行为的、被别的测试完整覆盖的、测非产品配置的。其余 228 个是把同一个函数的多组 `[TestCase]`、或同一套夹具上的几个小测试，合成一个循环断言的测试。断言一条不少，运行器里的数量会降下来。

合并有代价：一个循环里第一组输入失败，后面几组就不再报告。合并时用 `Assert.Multiple(() => { ... })` 包住循环，每条断言的 message 带上这组输入（例如 `$"case server={s} owner={o}"`）。这样一次运行仍然能看到所有失败的组合。联机、交接、技能时长这些受保护的区域，合并时把原来的 `[TestCase]` 输入表原样搬进数组，不要删行。

如果不合并、只做真删，403 → 375（−28，7%）。用户要的"删掉一大部分"只能靠合并做到。

另外，PR #63（`feat/mechanics-update`）合并以后用例会多出很多：那个分支新增 20 个测试文件，共 263 个用例；它改过的 10 个共用文件也比这里多 21 个。不做精简的话，合并后是 403 + 263 + 21 ≈ **687** 个。新增的那 20 个文件这次没审（不在本分支），PR #63 合并后应该按同一套规则再过一遍（见 §6）。

### 最大的 5 块减少

| # | 位置 | 减少 | 方式 |
|---|---|---:|---|
| 1 | `HandoffClockTests` + `BuddahTickMathTests` [PR#63] | −37 | 合并：真值表、时钟换算的多组 TestCase 改成循环；`TranslationSaturates` 跟 `ServerEventsMap` 测的是同一个 `ServerEventToLocalTick`，合成一个 |
| 2 | `SkillVfxTests` | −17 | 删 5 个（常量、单个 prefab 的命名、被 TimingSync 覆盖的、shader 预载、Feel 调参），合 12 个 |
| 3 | `RaceEndPolicyTests` | −17 | 合并成 3 个：联机倒计时、单机结束、非法输入守卫 |
| 4 | `RaceTimingTests` | −15 | 删 1 个（被 `SixRacerResultsTests` 覆盖），其余按"单调性 / 非法输入 / 重置"合成 7 个 |
| 5 | `IntroHandoffContinuityTests` | −14 | 合并：7 组距离采样、5 组归属组合各成一个循环 |

## 1. 口径和核实方法

- 计数：用脚本解析每个方法上方（含同一行）的 `[Test]`/`[UnityTest]`/`[TestCase(` 属性，按方法汇总。脚本和逐方法计数在仓库外的共享笔记目录 `code-review-scripts/`（`count.py`、`counts.txt`），不进仓库。
- 回归测试：对每个测试类跑 `git log --diff-filter=A` 找添加它的提交，对有疑问的方法跑 `git log -S<方法名> -- Assets/Tests`。由 `fix:` 提交添加、或紧跟某个修复的 `test:` 提交添加的，都按回归测试处理：只合并，不删。
- "已覆盖"：每条都打开覆盖方的测试代码，确认断言确实覆盖到，§3 写了覆盖到哪一行。
- "钉死配表数值"：先查生产代码的值是不是从表来的（例如 `OnlineMatchRules.cs:3` 写着 "Values come from Match/ModeRule"），再看测试是不是拿字面量去比。`Docs/design-tables/inventory-ui.md` §10 "会因数值搬进表而失败的测试" 已经列了其中几条。
- PR #63：用 `pr63-files.txt` 对文件名。PR #63 分支里的版本是用 `git show feat/mechanics-update:<path>` 读出来的，只用来数用例，对那里的版本没有逐个下结论。

## 2. 按测试类汇总

"删"＝覆盖会少；"并"＝断言保留，用例数减少。

| 测试类 | 现在 | 建议后 | 删 | 并 | PR#63 | 测什么 / 主要理由 |
|---|---:|---:|---:|---:|:-:|---|
| AIDifficultyProfilesTests | 6 | 2 | 1 | 3 | | AI 难度档资产；档位"顺序"是钉死 DrivingTier 表的设计意图 |
| AIDrivingTests | 10 | 2 | 1 | 7 | | 运动模型对齐 PhysX（保留）；prefab 检查和 SixRacer 重复 |
| AISkillCommitmentTests | 8 | 5 | 0 | 3 | ✓ | AI 出招状态机 |
| AISkillDecisionTests | 21 | 13 | 1 | 7 | ✓ | AI 出招机会评分；同一函数的 TestCase 合并 |
| AISkillSceneTests | 3 | 1 | 1 | 1 | ✓ | 场景/prefab 数据 |
| BuddahTickMathTests | 20 | 4 | 0 | 16 | ✓ | 预测时钟换算（保留全部断言） |
| ChargedProjectileFlightTests | 6 | 2 | 2 | 2 | | 蓄力掌飞行曲线；删掉钉死调参数值的两条 |
| DesignTableRuntimeTests | 4 | 4 | 0 | 0 | ✓ | 配表运行时通道，核心，全留 |
| DesignTableTests | 11 | 6 | 1 | 4 | ✓ | 配表导入，核心；三个"读真实工作簿"的合成一个 |
| GraphicsSettingsTests | 16 | 9 | 3 | 4 | | 画质设置；删 PlayerPrefs 包装、命令行解析、无订阅者的事件 |
| HandoffClockTests | 28 | 7 | 0 | 21 | ✓ | 交接时钟（保留全部断言） |
| HandoffGroundSnapTests | 5 | 1 | 0 | 4 | | 交接落地（回归 26742dd6），一个纯函数 5 组输入 |
| InkFluidVolumeTests | 7 | 2 | 0 | 5 | | 墨流体预算 |
| IntroHandoffContinuityTests | 19 | 5 | 0 | 14 | | 开场→交接（回归 ec2bac2e / 525600e8） |
| IntroHandoffTimingTests | 12 | 4 | 0 | 8 | | 交接时间（回归 b4e34e4d / 26742dd6） |
| LeaderboardHudTests | 3 | 2 | 0 | 1 | | 排行榜（回归 73f29b37） |
| LoadoutRulesTests | 6 | 2 | 0 | 4 | | 选技能校验 |
| MatchClockTests | 8 | 1 | 0 | 7 | | tick→秒，只有一层包装 |
| MatchRulesTests | 7 | 1 | 5 | 1 | ✓ | 13 个 getter，值全来自 ModeRule 表 |
| PlayerCameraIntroExitTests | 3 | 1 | 0 | 2 | | 相机平滑（回归 c77f26db），一个纯函数 |
| PlayerIdentityTests | 5 | 2 | 0 | 3 | | 连接→SteamId/名字 |
| PredictionDiagnosticSamplingTests | 7 | 1 | 2 | 4 | | 诊断采样；删诊断文本格式 |
| PredictionDiagnosticWiringTests | 3 | 2 | 1 | 0 | ✓ | 诊断接线；删"某方法不存在"的反射断言 |
| ProjectileBurstPlannerTests | 12 | 2 | 0 | 10 | | 纯数学小函数 |
| RaceEndPolicyTests | 20 | 3 | 0 | 17 | | 结束判定 |
| RaceStartHandshakeTests | 5 | 3 | 0 | 2 | | 联机握手（保留全部断言） |
| RaceTimingTests | 22 | 7 | 1 | 14 | | 圈时 |
| RacerIdTests | 9 | 1 | 1 | 7 | | 赛车手 id 值类型 |
| SixRacerFoundationTests | 4 | 3 | 0 | 1 | | 注册表、RaceMap 出生点（回归 5e202551） |
| SixRacerResultsTests | 3 | 2 | 1 | 0 | | 结果表 |
| SkillVfxAnchorTests | 11 | 5 | 0 | 6 | | VFX 锚点 |
| SkillVfxTests | 23 | 6 | 5 | 12 | | VFX 规则与数据 |
| SkillVfxTimingSyncTests | 3 | 3 | 0 | 0 | | 插槽时长同步，受保护，全留 |
| SkillVfxTimingTests | 22 | 9 | 0 | 13 | | 插槽时长缩放，受保护；只合并 TestCase |
| SoloMatchSettingsTests | 6 | 1 | 0 | 5 | | 构造参数校验 |
| SoloPresentationTimelineTests | 12 | 5 | 0 | 7 | | 单机表现时间轴（回归 cb4a9ad9） |
| SoloSessionFlowTests | 2 | 2 | 0 | 0 | ✓ | 端到端 Play Mode 流程，全留 |
| SoloTransportTests | 1 | 1 | 0 | 0 | | Yak 主机；PR #63 后可并入 SoloSessionFlow |
| SplineProgressTrackerTests | 14 | 6 | 0 | 8 | | 赛道进度（回归 a9c9a174） |
| SplineRacingLineTests | 5 | 3 | 0 | 2 | | AI 赛车线 |
| ThrustVectorSpecTests | 11 | 6 | 2 | 3 | | AI 推力控制器；删无墙配置的整圈（产品三档都开墙） |
| **合计** | **403** | **147** | **28** | **228** | | |
| 其中非 PR#63 | 296 | 102 | 19 | 175 | | |
| 其中 PR#63 | 107 | 45 | 9 | 53 | | |

## 3. 真删的 28 个：原来测什么、现在谁覆盖

| # | 测试 | 原来测什么 | 为什么删 / 由谁覆盖 | 确定度 |
|---|---|---|---|---|
| 1 | `AIDifficultyProfilesTests.TiersAreOrderedByBoldnessAndReaction`（:17） | Easy≤Normal≤Hard 的目标速度、反应 tick、墙权重、推力角 | 钉死配表：档位参数来自 `AI/DrivingTier`（`DesignTableRuntimeTests.cs:88` 把 `DrivingTierRecord` 映射到 `AIDifficultyProfile`），策划调一档就挂。真要保证顺序，应该写成 `DesignTableValidator` 的表规则，不是 C# 测试 | 确定 |
| 2 | `AIDrivingTests.DriverIsSerializedDisabledWithoutChangingMotorConfiguration`（:95） | Buddah.prefab 上 `AIRacerDriver` 存在且关闭，刚体 mass=2、drag=0 | 前两条 = `SixRacerFoundationTests.SerializedRacerContainsIdentityAndDisabledNormalDriver`（:91-97），一字不差。mass/drag 是钉死 prefab 数值；模型测试自己搭刚体（`AIDrivingTests.cs:27`），不依赖 prefab | 确定 |
| 3 | `AISkillDecisionTests.EvidenceIncludesActualMotorEffectState`（:12）[PR#63] | 一个 `[Serializable]` 结构经 `JsonUtility` 来回一趟字段不丢 | 测的是 Unity 的序列化，不是项目代码 | 确定 |
| 4 | `AISkillSceneTests.ProductRaceHasActiveTrackDataBeforeAIStarts`（:36）[PR#63] | RaceMap 里恰好 1 个 `TrackSplineRef`，激活，样条点 >3 | `SplineProgressTrackerTests.SavedRaceMapPositionsDoNotPlateauOrAccumulateCatchup`（:141-170）对同一场景做 `.Single()` 并整段回放，场景缺样条会直接挂。只有 `activeInHierarchy` 这条没被覆盖：删之前把这一行搬进那个测试 | 确定（搬一行） |
| 5 | `ChargedProjectileFlightTests.DefaultShapeReproducesTheInkPalmRange`（:14） | 默认曲线 × 889.77 × 3 s ≈ 1070 m | 钉死调参：889.77/1070 是 `VFX_Palm_Bullet` 上调出来的数。射程公式本身由 `RangeIsPeakSpeedTimesFlightTimeTimesTheShapesArea` 覆盖 | 确定 |
| 6 | `ChargedProjectileFlightTests.PalmSkillCarriesTheShapeAndTheTableSpeed`（:68） | SO 上存的 `projectileSpeed` == Push 表 charged 行 | 在拿 SO 里的副本去比表：策划改表、SO 没同步就挂，而运行时本来就用表里的值。表值确实写进 SO 由 `DesignTableRuntimeTests.ImportedDatabaseReachesRepositoriesAndSkillAssets`（:145）覆盖。"曲线至少 2 个关键帧" 这条搬进 Range 测试 | 确定 |
| 7 | `DesignTableTests.LocalizedTextRepository_FallsBackToKey`（:256）[PR#63] | 缺 key 时返回 key 本身 | 一行 getter 的回退值，挂了在游戏里一眼就能看到 | 确定 |
| 8 | `GraphicsSettingsTests.PlayerPrefsStoreKeepsTheChoiceUnderItsKey`（:162） | `PlayerPrefs.SetString/GetString` 包装 | 测 Unity API 的薄包装 | 确定 |
| 9 | `GraphicsSettingsTests.ACommandLineWindowKeepsTheSettingsOffTheWindow`（:394） | 命令行里有 `-screen-width` 等参数时不覆盖窗口 | 纯字符串匹配，坏了启动时立刻可见（ParrelSync 双开窗口大小不对）。风险低 | 可能（低风险） |
| 10 | `GraphicsSettingsTests.VfxQualityBroadcastsChanges`（:497） | `VfxQuality.Set` 同值不重复广播 | 生产代码里没有订阅者（`VfxQuality.cs:13` 写着 "nothing reacts to it yet"；`grep "VfxQuality.Changed"` 在 `Assets/Scripts` 里 0 处），测的是一个三行的 setter | 确定 |
| 11–15 | `MatchRulesTests.OnlineRetainsExistingRoomSelectionAndResultPolicies`（:13）、`SoloRulesRemoveWaitsAndOnlyPracticeEndsOnHumanFinish` ×4（:32）[PR#63] | 联机/单机规则对象的 13 个布尔属性、默认名 "玩家" | 钉死配表：`OnlineMatchRules.cs:3`、`SoloMatchRules.cs:15` 全部从 `Match/ModeRule` 读，`inventory-ui.md` §10 已经把 :46 列为"会因数值搬进表而失败"。真正的逻辑（单机 0 AI 时人类到终点就结束，`SoloMatchRules.cs:25`）由 `RaceEndPolicyTests.PracticeEndsAtHumanFinish…` / `SoloWithOtherRacers…` 覆盖。删后辅助方法 `AssertDeferredFeaturesDisabled` 没人用了 | 确定 |
| 16 | `PredictionDiagnosticSamplingTests.ReplicateUsesLatestEventAndClearsOldWriterReason`（:52） | 诊断摘要文本 "tick=28 … active" | 只测诊断日志的文本格式。探针代码不动（`BuildReplicateSummary` 仍由 `BuddahPredictedMotor.DebugState.cs:113` 调用），只是不再给文本钉格式 | 可能（如果用户要保留探针的测试，就留） |
| 17 | `PredictionDiagnosticSamplingTests.ReconcileRetainsItsOwnTimeAndValuesAcrossLaterReplicates`（:72） | 诊断摘要文本 "tick=10 speed=7.00 posDelta=1.000…" | 同上 | 可能 |
| 18 | `PredictionDiagnosticWiringTests.DiagnosticComponentsCannotDrawEvenWithOldSerializedFlags`（:50）[PR#63] | 用反射断言两个类没有 `OnGUI` 方法 | 测的是代码里没有某个方法；有人加回来，代码审查就能看到。prefab 上屏显开关关闭仍由同类的 `PrefabDisablesScreenFlagsAndKeepsConsoleMirror` 守着 | 确定 |
| 19 | `RaceTimingTests.SeparateRacersUseOneStartAndIndependentCrossings`（:135） | 两个赛车手共用起跑、各自冲线 | `SixRacerResultsTests.SixIndependentClocksAndDnfRowsPreserveActualCompletedLaps`（:47-61）六个赛车手共用 `Begin(10)`、各自计圈，覆盖更全。"没 Observe 直接 Finish" 由 `ResetAndBeginClearPriorMatchAndRequireAStart` 覆盖 | 确定 |
| 20 | `RacerIdTests.ValueEqualityAllowsIndependentLookupsAndDistinctRacers`（:43） | `==`/`Equals`/`GetHashCode`/字典查找 | 值类型相等性。`RacerRegistry`（`RacerRegistry.cs:10`）和 `RaceTiming`（`RaceTiming.cs:16`）都用 `Dictionary<RacerId,…>`，`SixRacerFoundationTests.RegistryRejectsDuplicates…` 和整个 `RaceTimingTests` 都在走这条路径 | 确定 |
| 21 | `SixRacerResultsTests.LegacyFollowCameraCreatesOneMissingSpectatorResolver`（:33） | 旧跟随相机 `Awake` 调两次只补一个 `RaceSpectatorTargetResolver` | 遗留组件 `Awake` 幂等的小细节，是重构提交 f9c56d89 顺手加的，不是修 bug | 可能 |
| 22 | `SkillVfxTests.EmissionScalesFollowTheTiers`（:35） | 0.4 / 0.7 / 1 | 测常量 | 确定 |
| 23 | `SkillVfxTests.PrefabCoversItsLayerSheetPhasesWithNamedLayers`（:77，只剩 Buf 一行） | `VFX_Palm_Buff` 的层名符合正则、Core 层优先级 1 | 编辑器命名细节，只剩一个首版 prefab（retired.md §6.1 已删 VFX_Palm/Anti 的两行）。插槽 id 的命名规则由 `EveryVfxIdFollowsTheNamingRuleAndLoadsItsPrefab` 守着 | 确定 |
| 24 | `SkillVfxTests.SkillsDatabaseAndTableAgreeOnTheVfxIds`（:259） | 掌法 SO 上 5 个 vfx id 都在 `SkillVfxDatabase` 里，anti 的 antiOf 指向正常 id | `SkillVfxTimingSyncTests.EverySlotASkillPlaysHasAReferenceLength`（:22-43）对所有技能的每个插槽 id 断言 "在 SkillVfxDatabase 里且有 prefab"；`SlotLengthsFollowTheSkillTable`（:71-75）点名了 palm_release / palm_bullet / palm_buff / *_anti 五个键。antiOf 配对由 `VfxTableRowsPairAntiEffects…`（:160-164）覆盖 | 确定 |
| 25 | `SkillVfxTests.FxShadersArePreloaded`（:286） | `PreloadShader.shadervariants` 里有两个 shader 名 | 资产细节，后果只是第一次放 buff 时卡一下 | 可能（低风险） |
| 26 | `SkillVfxTests.LaunchPostPulseIsRoutedOnTheCharacter`（:303） | 两个 Feel 反馈的 InitialDelay=0、震动 ≤0.15 s | 钉死 Feel 调参值，测的是第三方组件的序列化字段。如果用户认为 "InitialDelay=0" 属于技能时长同步，就留下 | 可能 |
| 27 | `ThrustVectorSpecTests.F07OfflineLapMeetsAllLimits` | 无墙配置跑一整圈（240 s 模拟），完成、max\|e\|≤120、换边≤36 | 测的不是产品配置：三档资产都是 `UseWallCorridor: 1`（`Assets/Resources/AI/{Easy,Normal,Hard}.asset:27`，`AIDifficultyProfilesTests.cs:13` 也断言了），产品配置由 `F10WallCorridorLapCompletesWithoutWinding` 跑整圈。阈值是 "pinned to the 2026-10-02 run9 result"（测试注释原话），整个类里最慢的就是这一圈 | 确定 |
| 28 | `ThrustVectorSpecTests.F08InvariantsHoldForEntireCompletedLap` | 同一圈无墙的整圈不变量为 0 | `F10` 对有墙这一圈断言 `invariantViolations==0`。删 F07/F08 后 `DesignLap()`/`_designRun` 成死代码 | 确定 |

## 4. 逐类逐方法决定

写法：`方法（×用例数，行号）→ 决定`。"并入 A" 指把断言（和 TestCase 输入表）搬进 A，A 变成循环断言。合并后的测试名可以重新起，下面给建议名。

### AIDifficultyProfilesTests（6 → 2）
- `EveryTierLoadsAValidThrustVectorProfile` ×3（:9）→ 合成一个循环三档的测试，建议叫 `TierProfilesLoadAndValidate`
- `TiersAreOrderedByBoldnessAndReaction`（:17）→ **删**（§3 #1）
- `ResolveGivesEachRacerItsOwnInstanceWithLineVariety`（:26）→ 保留（每个赛车手一份实例，防共享资产被改）
- `ProfileRejectsNonFiniteAndOutOfRangeValues`（:38）→ 并入 `TierProfilesLoadAndValidate`（都是 `ValidateConfiguration`）

### AIDrivingTests（10 → 2）
- `ModelMatchesSixtyRealPhysicsTicks` ×8（:20）→ 合成一个循环 4 个按键序列 × 2 个频率的测试（AI 预测模型对齐 PhysX，核心）
- `ModelIncludesExistingFloorSlidingFriction`（:60）→ 保留
- `DriverIsSerializedDisabledWithoutChangingMotorConfiguration`（:95）→ **删**（§3 #2）

### AISkillCommitmentTests [PR#63]（8 → 5）
- `CatchUpTicksCannotEvaluateTwoRacersInOneFrame`（:7）+ `SnapshotFrameCannotAlsoEvaluateCandidates`（:14）→ 合成 `DecisionScheduleGivesEachTickOneEvaluation`
- `CommitmentEmitsSeparatedKeysAndWaitsForRealExecution`（:23）→ 保留
- `MistakeCreatesTimeoutThenOnlyOneRetry`（:40）→ 保留；`ExpiryAndCancellationNeverEmitALateKey`（:52）并入它（都是"不会多出一个键"）
- `IndependentRandomAndCommitmentStateReplaysWithoutCrossTalk`（:59）→ 保留（回放确定性）
- `InversionStartAndEndBothDelayCognitiveSign`（:73）+ `CancelledInversionDoesNotApplyAStalePendingSign`（:84）→ 合成一个

### AISkillDecisionTests [PR#63]（21 → 13）
- `EvidenceIncludesActualMotorEffectState`（:12）→ **删**（§3 #3）
- `ProductCatalogUsesSelectablePoolAndFiveDistinctCompleteLoadouts`（:40）+ `CatalogRejectsUnknownDuplicateAndEmptyLoadouts`（:48）→ 合成一个。同时去掉 "5 种人格 / 6 个技能" 的字面量（inventory-ui §10 已列为钉死），改成和表的行数比
- `AccelerationNeedsUsableStraightAndNoExistingBoost`（:57）、`TrapNeedsApproachingSameLaneOpponentBehind`（:65）、`GiantMeasuresRealNearbyPackAndNotDistantRank`（:76）、`HandsRejectRearUnreachableAndProtectedTargets`（:86）→ 各保留（每个技能一条机会规则）
- `VisibleGlobalEffectSuppressesDuplicateOpportunity` ×2（:97）→ 合成 1 个
- `TargetIdentityDoesNotChangeOpportunity` ×3（:104）→ 合成 1 个
- `RootAndInactiveRaceGateAllOpportunities` ×3（:112）→ 合成 1 个
- `ShortOpportunityCannotOutliveReactionAndCombo`（:117）→ 并入 `EasyIgnoresRiskButHardStopsBeforeDangerousCast`（都在断言 `Utility` 返回 −∞ 的门槛）
- `EasyIgnoresRiskButHardStopsBeforeDangerousCast`（:124）→ 保留（作为合并目标）
- `OpportunistOnlyGetsBonusWhileStateWillStillExistAtExecution`（:137）→ 保留；`* 1.6f` 是人格表里的数，建议改成从 `_catalog` 读
- `ExponentialSamplingHasSameProbabilityAcrossDecisionRates`（:145）、`PackAndPursuitDependOnProgressAndClosingSpeed`（:152）、`ImpairedDriverProfileDoesNotMutateAssetOrPerRacerBase`（:160）→ 保留

### AISkillSceneTests [PR#63]（3 → 1）
- `NetworkedTestObstaclesHaveCompleteSerializedBehaviours`（:11）→ 保留（NetworkBehaviour 缓存为空时会在初始化/despawn 报错）
- `ProductRaceHasActiveTrackDataBeforeAIStarts`（:36）→ **删**，`activeInHierarchy` 一行搬进 `SplineProgressTrackerTests.SavedRaceMap…`（§3 #4）
- `RacerPrefabProvidesDisabledCasterAndThreeCompleteBindings`（:50）→ 并入 `SixRacerFoundationTests.SerializedRacerContainsIdentityAndDisabledNormalDriver`（同一个 prefab 的组件检查）。"slot 2 长度 4" 跟 `DesignTableTests.cs:164` 的 `comboBindings[2].sequence.Count == 4` 重复，可以只留一处

### BuddahTickMathTests [PR#63]（20 → 4）
- `ReplayDoesNotConsumeAnImpulseFromItsFuture` ×3（:12）→ 合成 1 个，循环 3 组 tick（含 `uint.MaxValue` 边界）
- `ForwardAndHostImpulseClocksRetainTheirCanonicalTicks`（:31）→ 保留
- `ServerEventsMapToLocalClockWithoutUnsignedUnderflow` ×8（:46）→ 合成 1 个；同时吸收 `HandoffClockTests.TranslationSaturatesRatherThanUnsignedWrapping` ×5（两者都测 `ServerEventToLocalTick`，后者还测 `ServerDeadlineToLocalTick`），建议叫 `ServerTicksTranslateWithSaturation`，13 组输入全留
- `DurationRoundsUpUsingExistingFloatAndMinimumDeltaSemantics` ×8（:59）→ 合成 1 个

### ChargedProjectileFlightTests（6 → 2）
- `DefaultShapeReproducesTheInkPalmRange`（:14）→ **删**（§3 #5）
- `DistanceGrowsFromZeroToTheWholeArea`（:22）→ 保留，改名 `DistanceFractionIsMonotonicAndClamped`
- `RangeIsPeakSpeedTimesFlightTimeTimesTheShapesArea`（:40）→ 保留；吸收 `PalmSkillCarries…` 里"SO 曲线至少 2 个关键帧"这一条
- `ANegativeShapeNeverMovesThePalmBackwards`（:49）→ 并入 `DistanceFractionIsMonotonicAndClamped`
- `TimeOutsideTheFlightIsClamped`（:60）→ 并入 `DistanceFractionIsMonotonicAndClamped`
- `PalmSkillCarriesTheShapeAndTheTableSpeed`（:68）→ **删**（§3 #6）

### DesignTableRuntimeTests [PR#63]（4 → 4）
全部保留：`EveryMappedColumnHasAFieldOnItsConsumer`（:66）、`ImportedDatabaseReachesRepositoriesAndSkillAssets`（:95）、`SkillTablesApplyWhenTheLookupWasBuiltBeforePlayMode`（:158，回归 2d34e2af）、`SavingAWorkbookQueuesItAndTheImportRemapsOnlyDependentSheets`（:196）。这个类本来就只跟表格单元格比，不比字面量（类注释 :14）。

### DesignTableTests [PR#63]（11 → 6）
- `XlsxReader_ReadsSheetsAndHeaderMarkers`（:29）→ 保留
- `TypeSpec_ParsesTagsAndLists`（:44）→ 保留；`ValueConverter_RoundTripsCurvesAndVectors`（:68）并入它（表头语法和单元格解析同属"格式"），建议叫 `HeaderGrammarAndCellValuesParse`
- `DesignTableSet_LoadsAllWorkbooksWithoutLoadErrors`（:80）→ 并入 `Exporter_DryRun…`。核实过：`DesignTableExporter.Run` 把 `DesignTableValidator.Validate(set)` 的结果放进 `Issues`（`DesignTableExporter.cs:64`），而 Validator 会把 `set.LoadErrors` 转成错误（`DesignTableValidator.cs:28`），所以干跑"0 个错误"已经包含"0 个加载错误"。另外几条 ExportTargets 包含/不包含的断言要搬过去
- `Validator_RealWorkbooksHaveNoErrors`（:95）→ 并入 `Exporter_DryRun…`（同上）
- `Validator_ReportsBadCellsAndDuplicateKeys`（:104）→ 保留
- `Exporter_DryRunBuildsDatabaseFromRealWorkbooks`（:130）→ 保留。建议顺手去掉行数字面量（`12` 个技能、`2` 个 Giant、`3` 个 Push、`3` 档、`5` 个人格、`3` 个绑定、`3` 条 ModeRule、`1` 个阶段、`0` 个选项、`6` 个出生点等，:139-176）：策划加一行就挂，跟 a8543728 "stop pinning design numbers in tests" 的方向相反
- `RemapSet_FollowsChangedWorkbooksAndReferences`（:189）→ 保留
- `ImportState_DetectsUnchangedAndChangedWorkbooks`（:214）→ 保留；`Exporter_NothingChangedSkipsWhenHashesMatch`（:231）并入它（同一个 import state，后者的断言 `NothingChanged || !File.Exists` 很弱）
- `LocalizedTextRepository_FallsBackToKey`（:256）→ **删**（§3 #7）

### GraphicsSettingsTests（16 → 9）
- `TablesDefineACompleteCatalog`（:60）→ 保留，改名 `TablesMatchTheProjectsQualityAssets`；去掉 option id 列表、preset 列表、默认 "high" 这些钉死表内容的断言，留"无错误、文本 key 都在、quality level 存在"
- `EachPresetDefaultReproducesItsQualityLevel`（:90）→ 并入上一条（都是"表 vs URP 资产"）
- `OverridesMakeTheChoiceCustomUntilTheyMatchThePreset`（:104）→ 保留；`RevertWithoutASavedChoiceReturnsToTheStartupChoice`（:403）并入它
- `StateRoundTripsAndDropsStaleEntries`（:136）→ 保留
- `PlayerPrefsStoreKeepsTheChoiceUnderItsKey`（:162）→ **删**（§3 #8）
- `MappingWritesOnlyTheClone`（:179）→ 保留（共享资产不能被写脏）；把 `150` / `2` / `0.2` / `1024` 这些表里的值改成从 `levels` 读出来再比
- `ComparingAPresetKeepsTheActiveRenderPipeline`（:229）→ 保留（回归 3f3bc29f，review M1）
- `ApplyingAgainKeepsAMatchingClone`（:261）→ 保留（回归 3f3bc29f）
- `RestorePutsBackEveryQualityLevelItChanged`（:311）→ 保留（Play Mode 改的 QualitySettings 会留在编辑器里）
- `VSyncLevelSetsVSyncAndTheCapOnlyAppliesWithoutIt`（:360）→ 保留，`ReflectionLevelsSetThePlanarReflectionScale`（:439）并入它（同一套 apply/restore 夹具）；去掉 `{0, 0.5, 1}` 这个钉死表值的断言
- `ACommandLineWindowKeepsTheSettingsOffTheWindow`（:394）→ **删**（§3 #9）
- `OcclusionNeedsItsFeatureAndReflectionsAreAlwaysAvailable`（:424）→ 并入 `TablesMatchTheProjectsQualityAssets`
- `PolicyNeverPutsTaaOnTopOfMsaaButAllowsFxaa`（:470）→ 保留
- `VfxQualityBroadcastsChanges`（:497）→ **删**（§3 #10）

### HandoffClockTests [PR#63]（28 → 7）——联机交接，断言全部保留
- `ConsumeTickHonorsNewGoBypassWithoutRevivingOtherBlockedInput` ×8（:17）→ 1 个，8 行真值表放进数组
- `HistoricalSnapshotKeepsPhaseLengthsAndBlendProgress` ×3（:39）→ 1 个，循环 3 个时钟偏移（含负偏移 −600）
- `ZeroStartAndZeroDurationEndpointsAreTimestamps` ×3（:73）→ 1 个
- `ResetStaysEmptyButInactiveSuppressionAndBypassTailsTranslate`（:89）→ 保留
- `ReconcileUsesRoleCorrectClock` ×4（:104）→ 1 个，循环 server × owner
- `SnapshotPairKeepsExpiredHandoffAndModifierExpiredAfterReceiptClockMovesBack` ×2（:116）→ 1 个（回归，e66e59dd）
- `LaterAuthoritativeSnapshotCanStillExtendADeadline`（:141）→ 保留（回归，e66e59dd）
- `TranslationSaturatesRatherThanUnsignedWrapping` ×5（:159）→ 并入 `BuddahTickMathTests.ServerTicksTranslateWithSaturation`
- `ModifierDeadlinesTranslateAndNonTimeFieldsStayUnchanged`（:166）→ 保留

### HandoffGroundSnapTests（5 → 1）
5 个方法测的都是 `BuddahHandoffGroundSnap.TryResolveRestY` 的不同输入（:9 穿模抬起、:17 悬空落下、:24 超限不管、:31 已着地不动、:38 未知/关闭）。合成一个表驱动测试，5 组输入和期望全留，包括 :12 的探针实测值 3.300→3.916。这个类是回归测试（26742dd6），只合并不删。

### InkFluidVolumeTests（7 → 2）
- `EveryVolumeSimulatesWhileTheyFitTheBudget`（:32）、`OverTheBudgetTheNearestSimulate`（:40）、`TheSmallestBudgetApplies`（:49）、`WithoutACameraTheOldestSimulate`（:58）、`ASimulatingVolumeKeepsItsGridsUntilANewcomerIsClearlyNearer` ×2（:69）→ 合成一个 `BudgetPicksTheNearestUnderTheStrictestLimit`，5 个场景都留
- `WithoutComputeAWiredVolumeHoldsNoGridsAndLogsNothing`（:78）→ 保留（无头服务器 / `-nographics` 不能报错）

### IntroHandoffContinuityTests（19 → 5）——回归测试，断言全部保留
- `GoReleasesSplineSamplerEvenWhileMotorEventIsPending` ×2（:65）+ `NonPendingBodyDoesNotSupplyOldGoPose` ×2（:94）→ 合成 1 个（4 种状态都断言 `TrySampleVisualPoseAtRenderTime == false`）
- `SoloIntroNeverSuppliesSecondDelayedSplinePose`（:77）→ 保留
- `LateSnapshotExtrapolatesOnlyTheNetworkPath` ×2（:104）→ 1 个
- `SampleUsesNormalizedDistanceRatherThanConvertingArcLengthTwice` ×7（:133）→ 1 个，7 个时间点放进数组
- `PostIntroLockDoesNotOvertakeAuthoritativeSmootherBuffer` ×5（:147）→ 1 个（夹具每次都要新建 NetworkManager，循环里要保留 try/finally）

### IntroHandoffTimingTests（12 → 4）
- `DriveTimeClampsOnlyTheStart`（:10）+ `DriveTimePassesThroughInvalidTiming`（:19）→ 1 个
- `OvershootIsZeroBeforeScheduledGo`（:26）+ `OvershootIsLinearAfterScheduledGoAndCapped`（:33）+ `OvershootIsZeroWithoutResolvedGoTime`（:42）→ 1 个
- `SplineOwnsBodyFollowsMotorHandoffState` ×6（:53）→ 1 个，6 行真值表
- `OvershootCapCoversThePendingRoundTripOnlyAfterLocalGo`（:59）→ 保留

### LeaderboardHudTests（3 → 2）
- `OnlineSnapshotDisplaysRankLapCompletionAndFinishWithoutTelemetry`（:38）+ `OnlineListFallbackDisplaysCurrentStandingsAndHonorsRowLimit`（:47）→ 合成 1 个
- `PracticeClearsPreviousRankingAndOnlineRestoresItOnSameView`（:63）→ 保留（回归 73f29b37）

### LoadoutRulesTests（6 → 2）
- `SubmissionTrimsAndAllowsBlankSlotsForAutofill`（:12）、`DuplicatePolicyAppliesToSubmissionAndCompletion` ×2（:23）、`InvalidOptionStopsNormalizationAtTheFailingSlot`（:31）、`WrongSlotCountAndNullSubmissionAreRejectedBeforePolicyCalls`（:40）→ 合成 `ValidateSubmissionNormalizesAndRejects`
- `AutofillPreservesCandidateOrderAndSeparateTrimPolicies`（:48）→ 保留

### MatchClockTests（8 → 1）
`ReadsCurrentServerTickWithoutLocalTimeOrPauseOffset`（:10）、`LargeTicksAreConvertedWithoutIntegerOverflow`（:23）、`RejectsMissingTickSource`（:30）、`RejectsInvalidTickDelta` ×5（:37）→ 合成一个 `ConvertsServerTicksAndRejectsBadInput`。`Docs/single-player/v3-timing-repair.md:29` 点名要跑 `MatchClockTests`，所以保留类名。

### MatchRulesTests [PR#63]（7 → 1）
- `OnlineRetainsExistingRoomSelectionAndResultPolicies`（:13）→ **删**（§3 #11）
- `SoloRulesRemoveWaitsAndOnlyPracticeEndsOnHumanFinish` ×4（:32）→ **删**（§3 #12–15）
- `ResetAfterSoloRestoresOnlineRules`（:51）+ `NullRulesAndSettingsAreRejected`（:63）→ 合成 1 个。`ReturnTarget == Room` 也是表值，只留 `Is.TypeOf<OnlineMatchRules>()` 和 `IsSolo == false`
- 注意：PR #63 分支里这个文件有 11 个用例（这里是 7），合并后要按 PR #63 的版本重新过

### PlayerCameraIntroExitTests（3 → 1）
三个方法都测 `PlayerCamera.BlendStableIntroExitSmoothTime`（:8、:15、:23），合成 1 个，7 组输入全留（回归 c77f26db）。

### PlayerIdentityTests（5 → 2）
- `TransportAddressRequiresConnectionAndPreservesNonBlankAddress`（:13）→ 保留
- `FallbackNamesPreserveClientId` ×3（:40）+ `MissingConnectionAndTransportUseExistingSentinels`（:55）→ 合成 1 个；nullable-Lobby 重载的反射检查保留

### PredictionDiagnosticSamplingTests（7 → 1）
- `NoConsumerDoesNotAdvanceClock`（:10）、`BuildGateMatchesVerboseAvailability`（:18）、`UnchangedTextStillConsumesInterval`（:31）、`InvalidSerializedIntervalHasMinimum`（:43）、`ReleaseStripsCaptureArgumentEvaluation`（:95）→ 合成 `SamplingGateAndReleaseStripping`
- `ReplicateUsesLatestEventAndClearsOldWriterReason`（:52）→ **删**（§3 #16）
- `ReconcileRetainsItsOwnTimeAndValuesAcrossLaterReplicates`（:72）→ **删**（§3 #17）
- 诊断探针代码本身不动（用户规则：探针只关不删）

### PredictionDiagnosticWiringTests [PR#63]（3 → 2）
- `HealthOnlyRefreshesForItsLogConsumerAndAtSampleIntervals`（:14）→ 保留
- `DiagnosticComponentsCannotDrawEvenWithOldSerializedFlags`（:50）→ **删**（§3 #18）
- `PrefabDisablesScreenFlagsAndKeepsConsoleMirror`（:58）→ 保留

### ProjectileBurstPlannerTests（12 → 2）
- `BurstIsCenteredAndSpacingScalesAlongRequestedSideDirection` ×4（:12）+ `ColliderSizeUsesMagnitudeAndMinimumOnEachAxis`（:30）→ 合成 `BurstGeometry`
- `CooldownUsesPositiveChargedValueOrNonnegativeNormalFallback` ×4（:42）+ `OnlyActiveChargedModeAddsNonnegativeActionDelay` ×3（:50）→ 合成 `PushAttackTimingFallbacks`

### RaceEndPolicyTests（20 → 3）
- 联机：`OnlineWaitsFifteenSecondsEvenWhenEveryoneFinished` ×3（:12）、`OnlineDoesNotEndWithoutFirstFinisher`（:19）、`OnlineCountdownSurvivesRosterChanges` ×3（:25）、`ZeroCountdownStillRequiresReachingFirstFinishTime`（:70）→ 合成 `OnlineEndsOnlyAfterTheCountdownFromTheFirstFinish`
- 单机：`PracticeEndsAtHumanFinishWithoutWaitingForCountdownOrRosterUpdate`（:32）、`SoloWithOtherRacersWaitsForAllOrCountdown` ×2（:41）→ 合成 `SoloEndsOnHumanFinishOnlyInPractice`（同时接过 MatchRulesTests 删掉的那条逻辑）
- 守卫：`EmptyOrInconsistentRosterIsNotAllFinished` ×4（:53）、`InvalidClockOrCountdownCannotExpire` ×4（:61）、`NullRulesAreAConfigurationError`（:78）→ 合成 `InvalidInputNeverEndsTheRace`

### RaceStartHandshakeTests（5 → 3）——联机握手，断言全部保留
- `SequenceResetRetainsSceneReadinessButClearsSequenceState`（:22）、`DisconnectRemovesOnlyDepartingPlayerFromAllReadiness`（:34）、`FullResetClearsAllReadiness`（:47）→ 合成 1 个（同一个 `Seed()`，三种清除方式分段断言）
- `LocalReportRejectsDuplicateInvalidAndUninitializedButAcceptsDifferentSequence`（:59）→ 保留
- `ReadyRequiresExactSequenceForCurrentRosterAndIgnoresDepartedEntries`（:73）→ 保留

### RaceTimingTests（22 → 7）
- `FinalCrossingCompletesLapTimesWhoseSumIsTotal`（:13）→ 保留
- `ObservedFinalCrossingIsNotOverwrittenByLateOrDuplicateFinish`（:29）、`FinishCannotRegressAnAlreadyRecordedLapCount`（:58）、`DuplicateAndRegressiveLapReportsDoNotMoveCrossingTime`（:71）、`CrossingTimesCannotRegressEvenWhenAfterTheStart`（:122）→ 合成 `CrossingsAndFinishNeverRegress`（每段各用一个新 `RaceTiming`）
- `ZeroCompletedLapsDoNotMoveTheAuthoritativeGoTime`（:45）+ `ZeroLapObservationAndSameTickCrossingStayNonNegative`（:210）→ 合成 1 个
- `MissingCrossingsDoNotFabricateLapTimesOrFinishedResults`（:85）→ 保留
- `InvalidObservationAndFinishTimesCannotCreateOrChangeResults` ×5（:106）+ `InvalidFinishLapCountCannotFinish` ×2（:201）→ 合成 1 个
- `SeparateRacersUseOneStartAndIndependentCrossings`（:135）→ **删**（§3 #19）
- `ReturnedArraysCannotMutateTimingAndSnapshotsDoNotGrow`（:151）→ 保留
- `ResetAndBeginClearPriorMatchAndRequireAStart`（:169）+ `InvalidBeginDoesNotClearValidMatch` ×4（:190）→ 合成 1 个

### RacerIdTests（9 → 1）
- `HumanIdsPreserveConnectionIdentityWithoutEnteringReservedAIInterval` ×2（:12）、`InvalidClientIdsCannotCollideWithAI` ×3（:20）、`WireIdentityRejectsInvalidValues` ×2（:26）、`AIIdsAreUniqueAndOutsideHumanNamespace`（:32）→ 合成 `HumanAndAIIdsStayInSeparateRanges`
- `ValueEqualityAllowsIndependentLookupsAndDistinctRacers`（:43）→ **删**（§3 #20）

### SixRacerFoundationTests（4 → 3）
- `RegistryRejectsDuplicatesAndUnregistersOnlyTheSameBody`（:17）→ 保留
- `SerializedRaceMapHasSixDistinctSpawnsAndCompleteLayouts`（:36）+ `TrackSpawnTableKeepsTheSceneSpawnAndLaunchHeight`（:70，回归 5e202551）→ 合成 1 个（两者都打开 RaceMap，合起来少开一次场景）
- `SerializedRacerContainsIdentityAndDisabledNormalDriver`（:91）→ 保留；PR #63 合并后吸收 `AISkillSceneTests.RacerPrefab…`

### SixRacerResultsTests（3 → 2）
- `FiveOwnerlessObjectsKeepIndependentIdentities`（:14）→ 保留
- `LegacyFollowCameraCreatesOneMissingSpectatorResolver`（:33）→ **删**（§3 #21）
- `SixIndependentClocksAndDnfRowsPreserveActualCompletedLaps`（:47）→ 保留（同时接过 RaceTiming 删掉的那条）

### SkillVfxAnchorTests（11 → 5）
- `CasterRelativeLayerStaysOnTheCasterAtAnyAngleAndScale` ×5（:18）→ 1 个，5 组角度×缩放
- `CasterRelativeFrameIgnoresTheLaunchPitch`（:56）→ 保留
- `CasterAttachedLayerFollowsTheCasterAndStartsChildTrails`（:79）→ 保留；`CasterAttachedLayerKeepsFollowingAfterItsWindow`（:147）并入它（加一段 EndPhase 后再移动施法者）
- `CasterFollowLayerFollowsThePositionButNotTheFacing`（:110）→ 保留
- `BurstSharedLayerShowsOnceAtTheBurstCentre` ×2（:171）→ 1 个

### SkillVfxTests（23 → 6）
- `TierKeepsPrioritiesAndNeverDropsGameplayInformation` ×8（:28）→ 1 个，8 行表
- `EmissionScalesFollowTheTiers`（:35）→ **删**（§3 #22）
- `WindowUnitsConvertFromGameplayAndTableValues`（:43）→ 保留，改名 `RulesConvertWindowsAlphaAndScale`；`WindowAlphaFadesInsideTheWindowOnly`（:55）和 `BodyFollowsTheColliderAtItsOwnProportions`（:65）并入它（都是 `SkillVfxRules` 的纯函数）
- `PrefabCoversItsLayerSheetPhasesWithNamedLayers`（:77）→ **删**（§3 #23）
- `EverySlotStaysWithinItsParticleSystemCeilingAndUsesNoRealtimeLight`（:113）→ 保留（2026-10-31 按用户要求加的预算闸，fc7cc27e）；`BuffPrefabCastsAndReceivesNoShadows`（:136）并入它
- `VfxTableRowsPairAntiEffectsAndPointAtTheirPrefabs`（:153）→ 并入 `EveryVfxIdFollowsTheNamingRuleAndLoadsItsPrefab`（都在遍历 VFX 表的行）
- `EveryVfxIdFollowsTheNamingRuleAndLoadsItsPrefab`（:181）→ 保留（表 ↔ SkillVfxDatabase ↔ prefab ↔ 控制器 id）
- `EmptyTableCellsFallBackToTheDefaults`（:227）→ 保留（配表空格子的回退）
- `SkillsDatabaseAndTableAgreeOnTheVfxIds`（:259）→ **删**（§3 #24）
- `OneCastOfTheEffectFitsInTheCastLock` ×2（:275）→ 1 个，循环正常/反噬两行（技能时长规则，保留）
- `FxShadersArePreloaded`（:286）→ **删**（§3 #25）
- `LaunchPostPulseIsRoutedOnTheCharacter`（:303）→ **删**（§3 #26，可能）

### SkillVfxTimingSyncTests（3 → 3）
全留：`EverySlotASkillPlaysHasAReferenceLength`（:22）、`SlotLengthsFollowTheSkillTable`（:46，d19e2826 修过）、`VfxDurationOnTheSoOverridesTheGameplayLengthAndIsFlagged`（:81）。

### SkillVfxTimingTests（22 → 9）——受保护，只把 TestCase 变成循环
- `TimeScaleIsTheLiveWindowOverTheReferenceWindow`（:12）、`LayerWindowMovesWithThePhaseOnlyInPhaseFraction`（:23）→ 保留
- `LayerTimeScaleOnlyRescalesMovingWindowsWithAReference` ×5（:50）→ 1 个
- `LayerContentFollowsTheGameplayLength` ×4（:71）→ 1 个
- `ParticlesDieNaturallyKeepsTheSlotUntilTheLastParticleDies` ×2（:105）→ 1 个
- `EndWithWindowCapsTheLongLivedSystemsTail` ×2（:127）→ 1 个
- `SlotLivesUntilItsLongestWindowEnds` ×2（:151）→ 1 个
- `DissipationWaitsForTheLongestDisLayerAndTheTailFade` ×2（:169）→ 1 个
- `EndingAgeKeepsTheParticlesAlpha` ×3（:192）→ 1 个
- 每次循环都要新建 `SkillVfxTestRig`（`using` 放进循环体里），不能跨组复用控制器

### SoloMatchSettingsTests（6 → 1）
`NamesAreAnImmutableCopyOfTheRequest`（:11）、`ValidBoundaryCountsAllowMissingNames` ×2（:23）、`RejectsUnsupportedRacerCounts` ×2（:31）、`RejectsUnknownDifficulty`（:38）→ 合成 1 个。注意 0..5 的上限是 inventory-ui §10 列的钉死项；场景只有 6 个出生点，这个上限算硬约束，可以留。

### SoloPresentationTimelineTests（12 → 5）——回归 cb4a9ad9
- `EmptyHistoryDoesNotInventPose`（:12）、`DuplicateTimeReplacesObservationButOlderSamplesCannotRewind`（:47）、`UnderflowClampsWithoutExtrapolatingLaunchVelocity`（:59）、`ResetForTeleportControlOrRematchDiscardsAllOldMotion`（:71）、`BoundedBufferRetainsNewestSamples`（:83）→ 合成 `BufferEdgeCases`
- `DrivingKeepsIntroDelayAndSignedSpeedAcrossGo`（:18）、`RealRepeatedEndpointIsPreservedAndDetectedAsStop`（:34）、`RotationSamplesTheSameTimeAsPosition`（:106）→ 保留
- `GoEpochUsesOneFixedBoundaryAndNeverRoundsEarlier` ×4（:96）→ 1 个

### SoloSessionFlowTests [PR#63]（2 → 2）
两个 `[UnityTest]` 端到端流程都留（启动回滚是回归 73f29b37）。

### SoloTransportTests（1 → 1）
`YakOnlyHost_InitializesManagersAndStopsCleanly`（:23）保留。它独有的断言只有"Steam 服务器从不启动"（:44）。PR #63 合并后可以把这一条搬进 `SoloSessionFlowTests.Practice_…`，然后删掉这个类，省一次进 Play Mode。这样再 −1。

### SplineProgressTrackerTests（14 → 6）——回归 a9c9a174
- `ClampKeepsDistanceParameterAndWrapConsistent` ×4（:61）→ 1 个
- `RejectedSampleClearsOldWrapAndUpdatesDirection` ×2（:77）→ 1 个
- `FirstOutOfCorridorSampleDoesNotSeedProgress`（:93）→ 保留
- `ParallelOrElevatedGlobalRoadCannotReplaceHistoryBranch` ×3（:101）+ `CrossingKeepsContinuousRoadInsteadOfCloserDistantBranch`（:113）→ 合成 `ContinuousBranchWinsOverCloserRoads`（每段重新 `Configure`）
- `TrackerDirectionPreservesSequentialCheckpointGate` ×2（:121）→ 1 个
- `SavedRaceMapPositionsDoNotPlateauOrAccumulateCatchup`（:141）→ 保留；加上从 AISkillScene 搬来的 `activeInHierarchy` 断言

### SplineRacingLineTests（5 → 3）
- `GeometrySnapshotCannotBeMutatedAndPaceIsPerLine`（:12）+ `CapturesShareOnlyMatchingGeometryAndKeepIndependentPace`（:22）→ 合成 1 个
- `PaceWindowIncludesInteriorSamplesAndWraps`（:50）→ 保留
- `RacingLineProjectionWrapsAcrossLastSegment`（:70）+ `CurvaturePaceUsesAvailableForceAndTighterCorners`（:78）→ 合成 1 个

### ThrustVectorSpecTests（11 → 6）
- `F01RecordedOverspeedUsesBrakeAtLeast120` → 保留
- `F02SideSwitchPassesTangentWithoutTail` + `F03ExternalThreeHundredTurnsForwardSixty` → 合成 1 个（都在测航向误差不 wrap + 姿态键）
- `F04RecordedTargetSeamRemainsContinuous` → 保留
- `F05StraightOffsetSettlesInEightSeconds` ×2 → 1 个，循环 ±1 转向符号
- `F09CircleKeepsLineAndTurnsTowardCentre` ×2 → 1 个，循环左右
- `F07OfflineLapMeetsAllLimits` → **删**（§3 #27）
- `F08InvariantsHoldForEntireCompletedLap` → **删**（§3 #28）
- `F10WallCorridorLapCompletesWithoutWinding` → 保留（产品配置的整圈）
- 文档引用：`Docs/single-player/thrust-vector-controller-2026-10-02.md:451` 提到 F07/F08 是当时的验收项，那是历史记录，不用改；`thrust-vector-controller-spec.md` 第一行的 2026-10-04 更新说明里可以补一句"F07/F08 已删，见 F10"

## 5. 测试辅助和"专为测试开的"生产代码

### 5.1 删后变成死代码的（都在测试里）
| 位置 | 原因 | 处理 |
|---|---|---|
| `MatchRulesTests.AssertDeferredFeaturesDisabled`（:69）[PR#63] | 只有被删的两个测试调用 | 跟着删 |
| `ThrustVectorSpecTests.DesignLap()` 和静态字段 `_designRun` | 只被 F07/F08 调用（`grep DesignLap` 只命中这两处和定义）；`Design()`、`Lap()`、`WallLap()` 还在用 | 跟着删 |
| `ChargedProjectileFlightTests.SkillAsset` 常量 | 如果曲线断言不搬进 Range 测试，就没人用了 | 搬就留，不搬就删 |
| `SkillVfxTests` 的 `using BuddahGo.Rendering` | 合并后 `VfxQualityTier` 仍然在 Tier 测试里用 | 留 |
| `Tools/ai/fixtures/*.json` | `spin-entry.json`、`track-walls.json`（WallLap / SplineRacingLine）、`saturated-braking.json`（F01）、`heading-side-change.json`（F02）、`target-seam-revision-five.json`（F04）在本方案下都还在用 | 留；只有走 §7 第二档删 F01–F04 时，后三个才变死文件 |

`SkillVfxTestRig.cs`、`PlayerIdentityTests` 里的 `IdentityTestTransport`、`GraphicsSettingsTests` 里的 `MemoryStore`/`SmallCatalog()`，在本方案下都还有人用。

### 5.2 生产代码里专为测试开的口子（本方案不会让它们变成死代码，列出来备查）
核实方法：`grep -rF <符号> Assets --include=*.cs`，排除 `Assets/Tests/` 后数命中。只有定义本身、没有别处调用的，算"只给测试用"。

| 符号 | 位置 | 生产代码里有没有调用 | 本方案之后 |
|---|---|---|---|
| `InkFluidVolume.PooledCount`（public static） | `InkFluidVolume.cs:130` | 没有，只有测试读 | 还被 `WithoutCompute…` 用 |
| `InkFluidVolume.ForceNoCompute`（internal static） | `:694`，`ComputeUsable` 里读（:692） | 只有测试会把它设成 true | 还被用（测试钩子） |
| `InkFluidVolume.InBudget`（internal） | `:286` | 没有，只有测试读 | 还被合并后的预算测试用 |
| `InkFluidVolume.PickSimulating` / `RankScore`（internal） | `:243` / `:262` | 有（`:238`、`:252`），设成 internal 是为了让测试能调 | 正常 |
| `ThrustVectorPlanner.CaptureCandidates`（internal static）、`LastCandidateThetas` / `LastCandidateCosts`（internal） | `ThrustVectorPlanner.cs:38`、`:54-55`，`:290` 按开关分配数组 | 只有 `ThrustVectorSpecTests.Lap()` 用，用来往离线 CSV（`--thrust-offline-output`）里写候选角 | 还被 WallLap 用。**可能**的后续：如果不再需要离线 CSV 的候选角那一列，可以连同 `Candidates()` 一起删，planner 每帧少一个分支（低） |
| `SkillDatabase.IsPlaying`（public static Func） | `SkillDataBase.cs:252` | 默认就是 `Application.isPlaying`，只有测试替换它 | 还被 `SkillTablesApplyWhenTheLookupWasBuiltBeforePlayMode` 用（回归测试，留） |
| `PlayerPrefsGraphicsSettingsStore(string key = DefaultKey)` 的 key 参数 | `GraphicsSettingsModel.cs:173` | 生产只用默认值（`GraphicsSettingsService.cs:36`） | 删掉 `PlayerPrefsStore…` 测试后，这个参数只剩默认值在用；无害，可以不管 |
| `VfxQuality.Changed` 事件 | `VfxQuality.cs:19` | 0 个订阅者（文件注释 :13 写明"还没人响应"） | 删掉 `VfxQualityBroadcastsChanges` 后整个仓库没有订阅者。它是给特效分档预留的接口，**不建议删** |
| 私有方法反射：`RaceBodyIntroStateController.GetNormalizedDistanceT` / `SampleSnapshotAtTime`、`BuddahPredictionVisualRootBridge.ShouldLockVisualRootDuringIntroOrPresentation`、`LeaderboardTMPUI.RenderRankings`、`SplineProgressTracker.UpdateProgress`、`LapProgress.TryAdvanceSplineCheckpoints` | 各自文件 | 生产里都有调用 | 正常；这些测试靠方法名反射，重命名不会编译报错，只会运行时挂，记一下 |

## 6. PR #63 的影响

- 标 [PR#63] 的 10 个文件：AISkillCommitment、AISkillDecision、AISkillScene、BuddahTickMath、DesignTableRuntime、DesignTable、HandoffClock、MatchRules、PredictionDiagnosticWiring、SoloSessionFlow。本分支版本是 107 个用例，建议做完是 45 个。
- PR #63 分支里这 10 个文件的版本不一样（例如 AISkillDecision 27 个、MatchRules 11 个、DesignTable 15 个、DesignTableRuntime 8 个），等它合并以后，要在合并后的版本上按 §4 的思路重新下结论，不能直接套这里的行号。
- 跨文件的合并有三处牵涉 PR#63：
  - `HandoffClock.TranslationSaturates` → `BuddahTickMath`：两边都是 PR#63 文件，一起做。
  - `AISkillScene.RacerPrefab…` → `SixRacerFoundation`（非 PR#63）：目标测试现在就可以先加断言，源测试等 PR #63 再删。
  - `AISkillScene.ProductRace…` 的 `activeInHierarchy` → `SplineProgressTracker`（非 PR#63）：同上。
- PR #63 新增 20 个测试文件，共 263 个用例，最多的是 `TrainingDummyTests` 34 个、`BuddahPredictedModifierStateOpsTests` 29 个、`SkillCastRulesTests` 23 个。用 `git show feat/mechanics-update:<path>` 数出来的，没有逐个审。合并后应该按本文规则再审一遍。

## 7. 可选的第二档（覆盖会少，风险中低，主会话自己定）

做完 §4 后（147 个），还可以再删下面这些，大约 −8：
- `MatchClockTests`（合并后 1 个）：`MatchClock` 只是 `tick × delta` 的包装。但 v3-timing-repair.md 点名要跑它，删之前改文档。
- `SoloMatchSettingsTests`（合并后 1 个）：构造参数校验。
- `PlayerIdentityTests` 的回退名测试（合并后 1 个）："Player N" 字符串格式。
- `LeaderboardHudTests` 的联机格式测试（合并后 1 个）：钉死行文本格式（inventory-ui §10）。
- `ThrustVectorSpecTests` 的 F01、F02/F03、F04（合并后 3 个）：AI 控制器开发阶段的固定样本检查，F10 整圈兜底。删的话三个 fixture json 一起删。
- `PredictionDiagnosticSamplingTests`（合并后 1 个）：诊断采样门槛。

## 8. 建议的落地顺序（按"小而独立的提交"规则，每个提交单独能编译、全量测试能过）

1. `test: drop tests that pin design-table values`：§3 #1、#5、#6，加上 GraphicsSettings 里去掉字面量的改动（不改用例数，只改断言）
2. `test: drop tests covered elsewhere`：§3 #2、#19、#20、#24，加上覆盖方补的一行断言
3. `test: drop tests of constants, wrappers and engine behaviour`：§3 #8、#10、#22、#23、#25、#26、#9、#21、#16、#17
4. `test(ai): drop the wall-less offline lap`：§3 #27、#28 + `DesignLap`
5. 合并按领域分开提交，每个提交只动一个领域：交接/时钟（非 PR#63 部分：IntroHandoff*、HandoffGroundSnap、PlayerCamera、SoloPresentationTimeline）、比赛规则（RaceEnd、RaceTiming、RacerId、MatchClock、SoloMatchSettings、Loadout、PlayerIdentity、Handshake）、VFX（SkillVfx*、InkFluid、ChargedProjectile）、AI（AIDriving、AIDifficulty、SplineRacingLine、ThrustVector、SplineProgressTracker）、画质设置
6. PR #63 合并后：在合并后的版本上处理 [PR#63] 文件和 SoloTransport

每一步之后跑 EditMode 全量，确认数量按预期下降，而且没有新增失败。

## 9. 顺带发现

- `Docs/vfx/retired.md` §3 第 73 行还把 "SkillVfxTests 里测旧 prefab 的两个用例 `PalmPrefab`/`AntiPrefab`" 标成"待删"，但代码里已经没有了（`grep PalmPrefab Assets/Tests` 0 处），§6.1 第 127 行也记了删除。§3 那一行是过期登记，可以改成"已删（见 §6.1）"。确定，低。
- `Docs/design-tables/inventory-ui.md` §10 列的"会因数值搬进表而失败的测试"，本方案处理了 MatchRules :46（删）和 AISkillDecision :43（去字面量）；SoloSessionFlowTests :127（"玩家"）和 LeaderboardHud、SixRacerResults 的文本格式没动。处理完以后可以更新 §10。
