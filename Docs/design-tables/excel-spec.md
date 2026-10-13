# BuddahGo 策划配表 Excel 结构规范（草案 v0.2）

日期 2026-10-12。依据 [decisions.md](decisions.md)：沿用 `ProjectConfigDatabase`，源表放仓库内，先定结构。本稿是结构定稿前的草案，带「待同步」标记的条目未经用户确认不进入实现。

## 1. 约束与目标

- 运行时数据容器不变：`ProjectConfigDatabase`（`Assets/Scripts/Config/Definitions/ProjectConfigDatabase.cs`）的记录列表 + `GlobalRule` 键值；Repository 与 Validator 继续使用。Excel 的每张数据 sheet 对应一个 `[Serializable]` 记录类，每行一条记录。
- Excel 是唯一编辑入口。`ProjectConfigDatabase_Main.asset` 与 `Resources/Config/ProjectConfigDatabase.json` 变为导出产物，不再手改。
- 程序与 Excel 直通：策划在 Excel 保存，Unity Editor 自动导入并校验；校验失败不写产物，错误定位到「工作簿 / sheet / 单元格」。
- 多人协作：按域拆工作簿，一簿一责任人；提交 xlsx 的同时提交文本导出，review 看文本。

## 2. 调研结论（行业做法与选型）

### 2.1 表头约定
国内主流导表工具（Luban、tabtoy、各自研工具）共同点是「标记行 + 数据行」：首列放标记，前几行声明字段名、类型、分组与注释，之后是数据。Luban 的约定最成熟、辨识度最高，本规范直接采用其标记写法：A 列为 `##var` 的行是字段名行，`##type` 是类型行，`##group` 是可选的导出分组行，`##` 或 `##comment` 是注释行；A1 不以 `##` 开头的 sheet 被忽略；数据行 A 列以 `##` 开头即整行注释掉；字段名以 `#` 开头或为空的列不导出；list 用 `list,int` 并可用 `#sep=` 指定分隔符；`int#ref=表名` 做引用校验。来源：[Luban wiki: excel](https://github.com/focus-creative-games/luban/wiki/excel)、[Datable: Excel Basic Header Conventions](https://www.datable.cn/en/docs/excel/basics)、[Luban README](https://github.com/focus-creative-games/luban)。
客户端/服务端分组（c / s / cs）是通用做法（[社区经验](https://gwb.tencent.com/community/detail/132947)）。BuddahGo 的 host 在客户端进程内，没有独立服务端构建，因此分组行只用于「运行时 / 仅文档」两档。

### 2.2 Unity 侧读写库
| 库 | 许可 | 读 | 写 | 结论 |
|---|---|---|---|---|
| ExcelDataReader | MIT，netstandard2.0/2.1 | xlsx/xls/csv | 否 | 采用。只读、轻量、无 SharpZipLib 依赖；本项目 API 级别为 .NET Standard 2.1（`ProjectSettings.asset` apiCompatibilityLevel 6）。CodePages 注册只在读 BIFF2-5 的旧 xls 时需要，本项目只用 xlsx |
| NPOI | Apache 2.0 | 是 | 是 | 不采用。Unity 2022.3 下与自带 ICSharpCode.SharpZipLib 版本冲突抛 TypeLoadException，Unity 判定为 by design（Issue UUM-68201） |
| ClosedXML | MIT | 是 | 是 | 备选，仅当需要在 Unity 内回写 Excel 时评估；依赖 OpenXML SDK，体积大 |
| EPPlus | Polyform NC，商用需付费 | 是 | 是 | 不采用 |
来源：[ExcelDataReader](https://github.com/ExcelDataReader/ExcelDataReader)、[C# Excel 库对比](https://hackernoon.com/c-excel-library-in-depth-comparison-tested-for-2026)、[NPOI 与 Unity 2022 冲突](https://issuetracker.unity3d.com/issues/unity-is-loading-internal-version-of-sharpziplib-instead-of-local-version)。

### 2.3 自动导入触发
把 xlsx 放在 `Assets/` 下，Unity 把它当 DefaultAsset 管理；Excel 保存后 Unity 重新导入该文件，`AssetPostprocessor.OnPostprocessAllAssets` 收到路径即可触发导表。这比自建 FileSystemWatcher 稳（Unity 自己的 watcher 有 macOS 大量文件崩溃与 .NET Standard 下未实现的记录）。注意：Excel 打开文件时会生成 `~$xxx.xlsx` 锁文件，必须在 `.gitignore` 与导入过滤中排除；Excel 一次保存会触发多次事件，需要防抖并在文件被占用时以 `FileShare.ReadWrite` 重试。现成范例：[UnityExcelImporterX](https://openupm.com/packages/net.nayaku.unity-excel-importer-x/)、[AssetPostprocessor.OnPostprocessAllAssets](https://docs.unity3d.com/ScriptReference/AssetPostprocessor.OnPostprocessAllAssets.html)、[Unity 外部编辑导入失败记录](https://issuetracker.unity3d.com/issues/failed-file-import-when-editing-files-externally)。

### 2.4 版本控制
xlsx 是 zip，git 无法 diff；行业做法是 xlsx 作为编辑源提交，同时提交文本导出（CSV/JSON）供 diff 与 review，并约定稳定主键而非依赖行序，减少合并冲突。cell 级合并工具（xlgit）尚不成熟，不采用。来源：[xlgit](https://pypi.org/project/xlgit/)、[DataTable Merge 的三方合并策略](https://csaf.itch.io/datatable-merge)、[CSV diff 经验](https://floppy.org.uk/blog/2014/05/23/csvhub-github-diffs-for-csv-files/)。

### 2.5 校验结果回写
用 openpyxl 等工具往策划的 xlsx 回写批注或标红有损坏文件、丢失数据验证规则与合并单元格的记录（[openpyxl 批注导致文件不可读](https://foss.heptapod.net/openpyxl/openpyxl/-/work_items/554)、[数据验证扩展被丢弃](https://developer.aliyun.com/article/833655)）。本规范默认不回写源表：校验结果输出到 Unity Console（可点击）与 `_reports/validation.md`。是否另做回写标红见待同步项。

## 3. 目录与文件布局

```text
Assets/Design/Tables/                      源表（提交，二进制走 LFS）
  SkillConfig.xlsx                         技能域：定义、数值、效果参数、表现、选池、配装、搓招
  Match.xlsx                               比赛域：模式规则、赛道、大厅、流程时长、选择阶段、开场
  Movement.xlsx                            角色域：运动、受击、状态效果、起跑、重生、圈数、执念、推手、相机
  AI.xlsx                                  AI 域：驾驶档位、技能难度、人格、机会参数、名单
  UIText.xlsx                              文案域：本地化文本、图标映射、菜单选项、HUD 参数
  Global.xlsx                              全局规则 GlobalRule、枚举定义 __enums__、元信息 __meta__
  _export/                                 文本镜像（提交，供 diff/review；由导出器生成，禁止手改）
    SkillConfig.SkillDefinition.csv
    ...
  _reports/validation.md                   最近一次校验报告（提交或忽略，待同步）
Assets/Data/Config/ProjectConfigDatabase_Main.asset   导出产物（提交）
Assets/Resources/Config/ProjectConfigDatabase.json    导出产物（提交，修复现有损坏）
Assets/Scripts/Editor/DesignTables/                   导入器、校验器、CLI 入口（BuddahGo.Editor 程序集）
Assets/Plugins/ExcelDataReader/                       ExcelDataReader + ExcelDataReader.DataSet DLL（Editor only）
```

`.gitattributes` 追加 `*.xlsx filter=lfs diff=lfs merge=lfs -text`；`.gitignore` 追加 `~$*.xlsx` 与 `*.xlsx#`。

## 4. Sheet 通用规范

### 4.1 表头
| 行 | A 列标记 | 内容 | 必填 |
|---|---|---|---|
| 1 | `##var` | 字段名，必须与记录类的 C# 字段名完全一致（区分大小写） | 是 |
| 2 | `##type` | 类型表达式，见 4.3 | 是 |
| 3 | `##group` | 导出分组：空=导出；`doc`=仅文档列，不导出 | 否 |
| 4 | `##comment` | 中文说明，导出到记录类的 Tooltip/文档 | 是 |
| 5 起 | 空 | 数据行；A 列以 `##` 开头的行整行忽略 | — |

- A1 不以 `##` 开头的 sheet 整表忽略，策划可以放草稿、曲线计算、备注。
- 字段名以 `#` 开头或为空的列忽略，用于中间公式列。
- 不允许合并单元格、隐藏列参与导出；公式允许，导出读取计算值。
- 一个记录类型只对应一张 sheet，不跨 sheet 拼接（减少合并冲突，责任清晰）。

### 4.2 主键与引用
- 每张 sheet 至少一列标 `#key`；多列 `#key` 构成复合主键，导出时校验唯一。
- `#ref=Sheet.field` 声明外键，导入时校验目标存在。跨工作簿引用写 `#ref=Workbook/Sheet.field`。
- 主键列固定放在最左数据列，便于 diff。

### 4.3 类型表达式
| `##type` | C# 类型 | 单元格写法 |
|---|---|---|
| `int` / `float` / `bool` / `string` | 同名 | bool 接受 true/false/1/0/是/否 |
| `string#key` | string | 主键 |
| `string#ref=SkillDefinition.skillId` | string | 外键 |
| `float#range=0,10` | float | 闭区间校验 |
| `enum:SkillSelectionPoolType` | 对应 C# 枚举 | 写枚举名或整数，名单在 Global.xlsx 的 `__enums__` |
| `list,string#sep=\|` | `List<string>` / `string[]` | `a\|b\|c`，空单元格=空列表 |
| `vector3` | `Vector3` | `x,y,z` |
| `text` | string | 允许换行，`\n` 转义 |
| `curve` | 预设曲线 id（string） | 见待同步：AnimationCurve 的表示 |

类型行只做校验与转换，不生成代码；记录类由程序维护（沿用决定 1）。

### 4.4 文案与本地化
文案字段在数据表里只放 key（`string#ref=UIText/Text.key`），文本本体在 `UIText.xlsx/Text`（key、zh、en、comment）。现有 `SkillDefinitionRecord.displayName/description` 直接存中文，迁移时改为 key 还是保留中文列见待同步。

### 4.5 全局标量
继续用 `GlobalRule(ruleKey, ruleValue, ruleSetId, description)`。Excel 侧在 `Global.xlsx/GlobalRule` 增加一列 `valueType`（`##group=doc`，不导出）供校验器按类型检查 `ruleValue`，并增加 `consumer`（doc）列记录读取方，杜绝再出现「定义了无人读」的键。

### 4.6 上下文列
`ruleSetId / modeTag / mapTag` 是现有仓库的查询维度。目前只用 `default`。保留列但允许留空（空=default），避免策划每行都填。

## 5. 工作簿与 Sheet 明细

「状态」：已有 = 现有记录类，字段不变；扩展 = 现有记录类加列；新增 = 新记录类（待同步确认后再建）。

### 5.1 SkillConfig.xlsx（责任人：技能策划）
| Sheet | 记录类 | 状态 | 主键 | 列 |
|---|---|---|---|---|
| SkillDefinition | SkillDefinitionRecord | 已有 | skillId | skillId、displayName、description、behaviorType(enum)、iconKey、isSelectable、sortOrder、tags(list)、unlockState(enum) |
| SkillBalance | SkillBalanceRecord | 扩展 | skillId | cooldownSeconds、castLockSeconds、obsessionGain、antiSkillId(ref) + 新列 castConfirmDelaySeconds（当前代码写死 1）|
| SkillEffectParam | SkillEffectParamRecord | 已有（接通运行时） | skillId+effectType+paramKey | skillId(ref)、effectType、paramKey、paramValue + doc 列 valueType、unit。承载 12 个技能 SO 的全部效果数值（筛查清单 skills 第 4 节）|
| SkillPresentation | 新增 | skillId | vfxId(ref)、vfxLocalOffset(vector3)、vfxLocalEuler、vfxDurationSeconds、vfxStopBeforeEndSeconds、observersFeelEventId(ref)、fovBoost、fovRampIn、fovSettle、fovRampInCurve(curve)、fovSettleCurve(curve) |
| VfxRegistry | 新增 | vfxId | vfxId、prefabPath、followPos、inheritRot、inheritScale、warmup |
| FeelEvent | 新增 | eventId | eventId、playerPath、comment |
| SkillSelectionRule | SkillSelectionRuleRecord | 已有 | ruleSetId+skillId+modeTag+mapTag | 现有 8 列；weight 列标 doc 直到有读取方 |
| DefaultLoadout | DefaultLoadoutRecord | 已有 | loadoutId | 现有 6 列 |
| ComboBinding | 新增 | slotIndex | name、slotIndex、sequence(list,enum:ComboToken#sep=,) |
| HandPush | 新增 | 全局单行 | BuddahHandControl 的 11 个字段 + hitboxSize(vector3) |
| Obsession | 新增 | 全局单行 | ObsessionFigure 的 9 个字段 |

### 5.2 Movement.xlsx（责任人：手感/物理）
| Sheet | 记录类 | 状态 | 主键 | 列 |
|---|---|---|---|---|
| MovementBase | 新增 | profileId（先只有 default） | forwardForce、maxSpeed、turnTorque、turnDecayPerSecond、turnInputMultiplier、autoThrottle、mass、linearDrag、angularDrag、maxAngularVelocity |
| PushReaction | 新增 | impulseSource(enum) | graceSeconds、extraMaxSpeed、impulseMultiplier |
| ModifierRule | 新增 | modifierType(enum) | stackPolicy(enum)、minScaleMultiplier、scalesMaxSpeed(bool)、comment |
| LaunchHandoff | 新增 | 全局单行 | inheritSeconds、blendSeconds、suppressTurnSeconds、roomBypassSeconds、minPreHandoffBypassSeconds、clearAngularVelocity、groundSnapMaxDistance、groundProbeHeight |
| RespawnRule | 新增 | teleportSource(enum) | contactSeconds、cooldownSeconds、heightOffset、clearanceRadius、liftStep、maxLift、snapProgress、zeroVelocity、resetModifiers、resetImpulseQueue、resetPushGrace、rebaseTrails |
| LapRule | 新增 | 全局单行（或 trackId） | lapsToFinish、crossCooldownSec、maxProgressJump01、minForwardDot、armDistance、useSequentialCheckpoints、autoAdvanceSplineCheckpoints、requiredCheckpointCount、fallbackCheckpointProgresses(list,float) |
| WrongWayRule | 新增 | 全局单行 | RaceCompletionTracker 8 个阈值 |
| Camera | 新增 | 全局单行 | PlayerCamera 13 个字段 |

### 5.3 AI.xlsx（责任人：AI 策划）
| Sheet | 记录类 | 状态 | 主键 | 列 |
|---|---|---|---|---|
| DrivingTier | 新增（替代 Tools/ai/*.json → Resources/AI/*.asset） | difficulty(enum) | AIDifficultyProfile 的 35 个字段（不含 NoiseSeed） |
| DrivingGlobal | 新增 | 全局单行 | targetSpeedMin、targetSpeedMax、recoverySpeed、recoveryVelErrDeg |
| SkillDifficulty | 新增（替代 SkillPersonalities.json Difficulties） | difficulty(enum) | 13 个字段 |
| Personality | 新增 | personalityId | displayName(key)、slot0/1/2(ref SkillDefinition)、patience、tempoSeconds、target(enum)、impairedTargetMultiplier、handsShots |
| PersonalitySkillWeight | 新增 | personalityId+skillId | weight |
| PersonalitySituationBias | 新增 | personalityId+situation(enum) | bias |
| SkillOpportunity | 新增 | skillId | backlashSeverity、blockWhenGlobalVisible + 各谓词常量列（minStraightSec、scoreBase、scoreSlope、maxValiditySec、euclidMul、maxYawDeg、yawPenalty、scorePerContact、minValiditySec、packBonus；不适用留空） |
| SkillGlobal | 新增 | 全局单行 | Tuning 14 项 + 快照派生 6 项 + 目标评分 5 项 + packMinNeighbours、maxComboRetries、severeSeverityThreshold、maxWindowSec、minRelSpeed、maxElapsedSec |
| Roster | 新增 | slotIndex | personalityId(ref)、displayNameOverride(key) |

### 5.4 Match.xlsx（责任人：系统策划）
| Sheet | 记录类 | 状态 | 主键 | 列 |
|---|---|---|---|---|
| ModeRule | 新增 | modeId(enum: Online/Solo/Practice) | IMatchRules 的 16 个开关 + pregameCountdownSec、selectionStageSec、postFirstFinishCountdownSec、resultDecisionSec、minPlayersToStart、hostAutoReady、aiCount、defaultPlayerName(key) |
| Track | 新增 | trackId | sceneName、laps、resultOverflowOffset(vector3)、missingPlayerOffset、minimapSprite、minimapWorldMinX/MaxX/MinZ/MaxZ、minimapImageWidth |
| TrackSpawn | 新增 | trackId+order | pos(vector3)、rotY |
| TrackCheckpoint | 新增 | trackId+checkpointId | pos、rotY、size |
| TrackResultAnchor | 新增 | trackId+rank | pos、rotY |
| Lobby | 新增 | 全局单行 | maxPlayers、minPlayers、defaultName(key)、defaultVisibility(enum)、maxSearchResults、clientConnectTimeoutSec、hostStartTimeoutSec |
| ResultVoteRule | 新增 | modeId | vetoChoice、unanimousChoice、timeoutChoice、disconnectChoice(enum) |
| ResultPresentation | 新增 | 全局单行 | personalFinishDissolveOutSec、forcedFinishDissolveOutSec、resultAreaDissolveInSec、allowMovementInResultArea、allowSkillsInResultArea |
| SelectionStage | 新增 | stageKey | displayName(key)、mode(enum)、order、enabledOnline、enabledSolo、layoutMode |
| SelectionOption | 新增 | stageKey+optionId | displayName(key)、description(key)、iconKey、unlocked |
| Intro | 新增 | 全局单行 | fallbackIntroDuration、introSpeedMetersPerSecond、handoffLeadSeconds、introLeadInSeconds、randomizeSlots、shuffleSeedMode(enum) |
| IntroLayout | 新增 | playerCount | slotIds(list,string) |

### 5.5 UIText.xlsx（责任人：UI/文案）
| Sheet | 记录类 | 状态 | 主键 | 列 |
|---|---|---|---|---|
| Text | 新增 LocalizedTextRecord | key | zh、en、comment、context(doc) |
| SkillIcon | 新增 | skillId | hudIdleSprite、hudActiveSprite、previewSprite |
| MenuOption | 新增 | optionGroup+index | value、labelKey(ref Text)、isDefault |
| HudObsession | 新增 | 全局单行 | smoothSpeed、colorSmoothSpeed、fadeSpeed、maxShakeStrength、shakeSpeed、flashColor、normalColor、lowThresholdRatio、highThresholdRatio、tierSprite0/1/2 |
| UiTiming | 新增 | componentKey+field | value（键值式，承载 SelectionPresentation、SkillSlot、Carousel 等零散时长） |

### 5.6 Global.xlsx（责任人：主程 / 主策）
| Sheet | 记录类 | 状态 | 主键 | 列 |
|---|---|---|---|---|
| GlobalRule | GlobalRuleRecord | 已有 | ruleKey+ruleSetId | ruleKey、ruleValue、ruleSetId、description + doc 列 valueType、consumer |
| __enums__ | 不导出 | enumName+name | enumName、name、value、alias、comment。校验器据此检查所有 enum 列 |
| __meta__ | 不导出 | workbook | schemaVersion、owner、lastReviewed、notes |

## 6. 直通车管线

```text
策划在 Excel 保存  →  Unity 重新导入 Assets/Design/Tables/X.xlsx
  → OnPostprocessAllAssets 过滤 .xlsx（跳过 ~$ 锁文件，300 ms 防抖）
  → DesignTableImporter 用 ExcelDataReader 读全部工作簿（FileShare.ReadWrite，占用时重试 3 次）
  → 解析表头（##var/##type/##group/##comment）→ 按类型转换 → 填充记录类实例
  → 校验：① 结构（主键唯一、必填）② 类型/范围/枚举/外键 ③ ProjectConfigValidator ④ SkillConfigCrossValidator（过渡期）
  → 任一 Error：不写产物；Console 输出「SkillConfig.xlsx / SkillBalance / C7: antiSkillId 'x' 不存在」并写 _reports/validation.md
  → 全部通过：写 ProjectConfigDatabase_Main.asset（SetDirty + SaveAssets）、ProjectConfigDatabase.json、_export/*.csv
```

- 手动入口：菜单 `Tools/Design Tables/Import All`、`Validate Only`、`Open Report`。
- 批处理入口：`Unity -batchmode -executeMethod DesignTables.Cli.ImportAndValidate`，供 pre-commit 与 CI 使用；退出码非 0 即失败。
- 回退策略：导入器只在校验全部通过时覆盖产物，失败保留上一次产物，游戏仍可运行。
- 运行时不读 Excel。Player 构建不包含 ExcelDataReader（DLL 设为 Editor only）。
- 与现有 `AIDifficultyAssetTool`、`SkillConfigCrossValidator` 的关系：前者在 AI 表迁入后退役；后者在 SO 字段删除前作为漂移检查保留。

## 7. 校验规则清单

| 级别 | 规则 |
|---|---|
| Error | 表头缺 `##var`/`##type`；字段名与记录类不匹配；主键重复或为空；类型转换失败；枚举名不在 `__enums__`；外键目标不存在；range 越界；`list` 分隔符后出现空元素 |
| Error | ProjectConfigValidator 现有规则（antiSkillId 存在、selection rule 引用技能存在等） |
| Warning | 列在记录类中存在但 Excel 缺失（使用默认值）；`doc` 列被运行时读取；GlobalRule 键无 consumer；SO 与表漂移（过渡期） |
| Info | 被忽略的 sheet 与列清单 |

## 8. 待与用户同步（未定是否入表 / 结构待拍板）

按决定 4，以下内容未经确认不实现。

**结构类**
1. ~~工作簿划分~~ 已定：按域拆 6 本（decisions 5）。
2. ~~源表位置~~ 已定：`Assets/Design/Tables/`（decisions 6）。
3. AnimationCurve（FOV、fade、move 曲线）：预设曲线 id 表（`curve`）还是留在 prefab 不入表？
4. ~~文案方案~~ 已定：key 引用 + UIText zh/en（decisions 7）。待定子项：字体子集重烘是否纳入导表流程。
5. `ruleSetId / modeTag / mapTag` 三列在 Excel 中保留（允许留空）还是隐藏？
6. ~~校验回写~~ 已定：只出报告（decisions 8）。
7. `_reports/validation.md` 是否提交到仓库？

**是否入表类**（筛查清单中标记为"偏工程"或"规则"的内容）
8. 结构性常量：SlotCount=3、MaxAI=5、RacerId.AIBase=10000、tickRate 60、FrameRateLock 120。本稿不入表，只在 `__meta__` 记录；是否同意？
9. 代码规则升级为枚举列：黑幕可见规则、逆因缘作用范围、神掌反噬的反向/自伤/穿墙、加速叠加规则、Giant 是否缩放 maxSpeed、反噬 obsessionGain 取值、投票法定规则、排名平局顺序。本稿把其中 ModifierRule.stackPolicy、ResultVoteRule、SkillOpportunity.blockWhenGlobalVisible 入表，其余不入；是否同意？
10. Track 系列（出生点、检查点、结果区锚点、小地图边界）入表还是留在场景、只导出校验？入表意味着场景物体由表驱动生成或校验一致。
11. AI 驾驶档位里偏工程的列（LateralDamping、PredictionGain、Attitude*、Rollout* 权重、RolloutTail、SwitchPenalty）是否全部暴露给策划？本稿全部入表但分「高级」列组。
12. AI_DrivingGlobal 的规划器常量（TVP 的 [Eng] 项）不入表；是否同意？
13. 输入绑定（InputBindings）入表需先去掉三处硬编码 KeyCode；本次是否纳入？
14. 物理材质与全局物理（重力、BounceThreshold、MaxAngularSpeed）是否入表？
15. 网络/预测参数（NetworkObject 插值、tick 相关）不入表；是否同意？
16. 音频、皮肤、per-racer 外观：本次是否预留空表？
17. 死数据处理：ReverseTurn/AccelerationAnti 的 vfx 字段、LapProgress forwardWrap 三字段、MiniMap 废弃字段、SkillDefinition 的 tags/isSelectable/behaviorType：入表还是随迁移删除？
18. timeline 时长（TransitionToRaceMap 18 s、PropertySelectionEnd 2 s、EndResultSwitch 信号）：表只读显示、表覆盖、还是不入表？
19. Practice 模式与 Solo AI 数量可配：ModeRule 是否按 Practice 独立一行并开放 aiCount？
20. 默认配装唯一真相：MAIN 的 slowtrap 还是 prefab/场景的 blackcurtain？
21. 如来神掌 cooldown/lock：12/10（配置）还是 18/15（SO）？
