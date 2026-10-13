# BuddahGo 策划配表 Excel 结构规范（v0.5，消费方已接通，分层导入）

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
| ExcelDataReader | MIT，netstandard2.0/2.1 | xlsx/xls/csv | 否 | 备选。最终未引入：xlsx 只是 zip + XML，.NET 自带的 ZipArchive + XmlReader 足够（见 `XlsxReader.cs`），省去 DLL 与许可管理 |
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

## 3. 目录、文件与命名规范

```text
Assets/Design/Tables/                      源表与镜像（提交；xlsx 走 LFS，csv 为文本）
  Skill/Skill.xlsx                         技能域（责任人：技能策划）
  Movement/Movement.xlsx                   角色与运动域（手感/物理）
  AI/AI.xlsx                               AI 域（AI 策划）
  Match/Match.xlsx                         比赛与流程域（系统策划）
  UI/UI.xlsx                               文案、图标、HUD、菜单（UI/文案）
  Global/Global.xlsx                       全局规则、结构常量、物理、标签层、场景、输入、timeline、__enums__、__meta__（主程/主策）
  _Export/<Domain>/<Sheet>.csv             文本镜像，供 diff 与 review；由工具生成，禁止手改
  _Reports/validation.md                   最近一次校验报告（导出器实现后生成）
Assets/Data/Config/ProjectConfigDatabase_Main.asset   导出产物（提交）
Assets/Resources/Config/ProjectConfigDatabase.json    导出产物（提交，修复现有损坏）
Assets/Scripts/Editor/DesignTables/                   导入器、校验器、CLI 入口（BuddahGo.Editor 程序集）
Assets/Design/Tables/scope.json                       导出范围（export / presentationOnly）
Assets/Design/Tables/export-map.json                  sheet → 记录类 → 数据库列表（gen_records.py 生成）
Tools/design-tables/                                  基线生成脚本（从工程实际值反向生成工作簿，仅建基线时用）
```

`.gitattributes` 已追加 `*.xlsx filter=lfs diff=lfs merge=lfs -text`；`.gitignore` 已追加 `~$*.xlsx` 与 `*.xlsx#`。

### 3.1 命名规范
| 对象 | 规则 | 示例 |
|---|---|---|
| 目录 / 工作簿 | 域名 PascalCase，一词；目录与工作簿同名 | `Match/Match.xlsx` |
| Sheet | PascalCase 单数，与记录类同名去掉 `Record` 后缀；子表以父表名作前缀；保留名 `__enums__`、`__meta__` | `SkillBalance`、`TrackCheckpoint`、`PersonalitySkillWeight` |
| 字段 | camelCase，与 C# 字段名完全一致；时长 `*Seconds`、角度 `*Degrees`、归一化 `*01`、布尔 `is/allow/use/enable*`、引用 `*Key` / `*Id` / `*Path` | `cooldownSeconds`、`maxYawDegrees`、`displayNameKey` |
| 记录 id | 全小写 snake_case，稳定不改 | `push_projectile_hands`、`race_default`、`racemap` |
| 枚举值 | PascalCase，与 C# 枚举名一致 | `PackCentre`、`ReturnToRoom` |
| 文案 key | `domain.entity.field` 点分小写，自动提取的 `ui.<class>.<slug>` 需人工定稿 | `skill.giant.name`、`match.default_player_name` |
| 全局规则键 | snake_case | `combo_input_window_seconds` |
| 单行全局表 | 主键列固定为 `profileId`，值 `default` | `Obsession.profileId` |
| 列顺序 | 主键列最左，其后身份列、数值列组，`doc` 列最右 | — |

AI 驾驶档位（`AI/DrivingTier`）沿用 `AIDifficultyProfile` 的 PascalCase 字段名，是唯一例外，迁表时与代码一起改名。

## 4. Sheet 通用规范

### 4.1 表头
| 行 | A 列标记 | 内容 | 必填 |
|---|---|---|---|
| 1 | `##var` | 字段名，必须与记录类的 C# 字段名完全一致（区分大小写） | 是 |
| 2 | `##type` | 类型表达式，见 4.3 | 是 |
| 3 | `##group` | 导出分组：空=导出；`adv`=导出但为高级/工程参数，默认折叠；`doc`=仅文档列，不导出 | 否 |
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
| `curve` | `AnimationCurve` | 关键帧列表 `time,value,inSlope,outSlope\|…`，与 Unity 序列化一一对应（不含加权切线） |

类型行只做校验与转换，不生成代码；记录类由程序维护（沿用决定 1）。

### 4.4 文案与本地化
文案字段在数据表里只放 key（`string#ref=UIText/Text.key`），文本本体在 `UIText.xlsx/Text`（key、zh、en、comment）。现有 `SkillDefinitionRecord.displayName/description` 直接存中文，迁移时改为 key 还是保留中文列见待同步。

### 4.5 全局标量
继续用 `GlobalRule(ruleKey, ruleValue, ruleSetId, description)`。Excel 侧在 `Global.xlsx/GlobalRule` 增加一列 `valueType`（`##group=doc`，不导出）供校验器按类型检查 `ruleValue`，并增加 `consumer`（doc）列记录读取方，杜绝再出现「定义了无人读」的键。

### 4.6 上下文列
`ruleSetId / modeTag / mapTag` 是现有仓库的查询维度。目前只用 `default`。保留列但允许留空（空=default），避免策划每行都填。

## 5. 工作簿与 Sheet 明细

v0.3 起以生成的工作簿为准：`Global/Global.xlsx` 的 `__meta__` sheet 列出全部 62 张 sheet 的记录类、实际值来源与备注；每张 sheet 的 `##comment` 行写明字段含义与来源行号。各域 sheet 清单：

| 工作簿 | Sheet |
|---|---|
| Skill | SkillDefinition、SkillBalance、SkillEffectParam、SkillPresentation、VfxRegistry、FeelEvent、SkillSelectionRule、DefaultLoadout、ComboBinding、HandPush、Obsession、BlackCurtainPresentation |
| Movement | MovementBase、PushReaction、ModifierRule、LaunchHandoff、RespawnRule、LapRule、WrongWayRule、Camera、RacerVisual、SplineTracking |
| AI | DrivingTier、DrivingGlobal、SkillDifficulty、Personality、PersonalitySkillWeight、PersonalitySituationBias、SkillOpportunity、SkillGlobal、Roster |
| Match | ModeRule、Track、TrackSpawn、TrackCheckpoint、TrackResultAnchor、Lobby、ResultVoteRule、ResultPresentation、SelectionStage、SelectionOption、Intro、IntroLayout、IntroSlotStyle、RacerNaming |
| UI | SkillIcon、MenuOption、HudObsession、UiTiming、MiniMap、Text |
| Global | GlobalRule、StructuralConstant、GlobalPhysics、PhysicsMaterial、Tag、Layer、Scene、InputBinding、Timeline、__enums__、__meta__ |

已有记录类直接对应的 sheet：SkillDefinition（displayName/description 改为 key）、SkillBalance（加 castConfirmDelaySeconds）、SkillEffectParam（接通运行时）、SkillSelectionRule、DefaultLoadout、GlobalRule。其余为新增记录类，按 `__meta__` 的 recordClass 列实现。

## 6. 直通车管线（已实现，`Assets/Scripts/Editor/DesignTables/`）

```text
策划在 Excel 保存  →  Unity 重新导入 Assets/Design/Tables/<Domain>/<Domain>.xlsx
  → DesignTablePostprocessor（过滤 ~$ 锁文件，0.5 s 防抖，编译/刷新中延后）
  → DesignTableSet.Load：XlsxReader（ZipArchive + XmlReader，FileShare.ReadWrite，Excel 开着也能读）读全部工作簿
  → DesignSheet.Parse：##var/##type/##group/##comment → ColumnSpec；A 列 ## 行跳过；A1 不带 ## 的 sheet 忽略
  → DesignTableValidator：主键唯一/必填、类型、range、enum（Global/__enums__）、ref 外键、path 存在；DesignTableDomainRules：跨表规则
  → DesignTableRecordMapper：按 ##var 反射填充 {Sheet}Record（export-map.json 决定 sheet → 记录类 → 数据库列表）
  → ProjectConfigValidator（现有结构校验）
  → 任一 Error：不写产物；Console 输出「Skill.xlsx/SkillBalance!F7 :: …」并写 _Reports/validation.md
  → 全部通过：写 ProjectConfigDatabase_Main.asset、Resources/Config/ProjectConfigDatabase.json、_Export/*.csv 镜像
```

| 入口 | 位置 | 说明 |
|---|---|---|
| 自动 | `DesignTablePostprocessor` | 保存工作簿后 0.5 s 内触发，只重映射保存的工作簿及其依赖方；菜单 `Tools/Design Tables/Auto Import On Save` 可关 |
| 菜单 | `Tools/Design Tables/Import Changed` | 按文件哈希只导入变化的工作簿（日常用） |
| 菜单 | `Import All (Force)` / `Validate Only` / `Open Report` / `Open Tables Folder` | 全量导入、只校验、打开报告与目录 |
| 菜单 | `Import Tag/balance|info|text|track|skill|movement|ai|match|ui|global` | 只推送带该 tag 的 sheet（忽略哈希） |
| CLI | `Unity.exe -batchmode -nographics -projectPath . -executeMethod BuddahGo.DesignTables.DesignTableCli.ImportAndValidate -quit [-designForce] [-designTags skill,ai]` | 退出码 0/1；不带参数时按哈希增量导入；`ValidateOnly` 只校验 |
| 记录类生成 | `python Tools/design-tables/gen_records.py .` | 表头或 `scope.json`（范围、tags）改动后运行，再让 Unity 重新编译 |
| 基线重建 | `python Tools/design-tables/build_tables.py . <out>` | 从工程实际值反向生成工作簿，仅建基线用 |

### 6.1 分层导入规则
1. 校验总是覆盖全部 sheet（跨表外键需要完整数据），开销很小。
2. 变化判定：每本工作簿的 SHA1 与 `Library/DesignTables/import-state.txt` 比较（每台机器各自记录，不提交）；AssetPostprocessor 另外把本次保存的工作簿名直接传入。
3. 重映射集合 = 变化工作簿内的导出 sheet ∪ 通过 `#ref` 引用了变化工作簿的 sheet（例如改了 `UI/Text`，`Skill/SkillDefinition` 与 `Match/ModeRule` 会跟着重映射）∩ tag 过滤。其余列表从当前 `ProjectConfigDatabase_Main.asset` 原样复制。
4. 没有任何变化时直接返回"nothing changed"，不写任何产物。
5. 产物写入成功后才更新哈希；带 tag 的局部导入只对"该工作簿全部导出 sheet 都已重映射"的工作簿更新哈希，避免遗漏其它层。
6. CSV 镜像只重写被重映射 sheet 所在工作簿的文件。

### 6.2 tag 约定（`scope.json` → `export-map.json`）
每张导出 sheet 自动带所属域名（skill / movement / ai / match / ui / global），再加一个性质标签：`balance`（数值平衡）、`info`（规则与映射信息）、`text`（文案）、`track`（随赛道）。匹配任一 tag 即入选。新增 sheet 时在 `scope.json` 的 `tags` 里登记，否则只带域名。

### 6.3 消费方接通（2026-10-13）
运行时通过 `DesignTableOverlay`（`Assets/Scripts/Config/Runtime/`）把记录覆盖到现有组件字段，prefab/SO/JSON 值仅在表无行时生效：

| 表 | 消费方 |
|---|---|
| SkillDefinition / SkillBalance / SkillSelectionRule / DefaultLoadout / GlobalRule | 原有仓库；SkillBalance.castConfirmDelaySeconds 与 GlobalRule cast_confirm_delay_seconds / anti_cast_confirm_delay_seconds 由 SkillExecutor 读取；combo_max_buffer 由 ComboSkillInput 读取；target_frame_rate 由 FrameRateLock 读取 |
| SkillEffectParam | SkillDatabase 在运行时按 paramKey 反射写入各技能 SO（仅 Play 模式） |
| ComboBinding / HandPush / Obsession | ComboSkillInput / BuddahHandControl / ObsessionFigure |
| MovementBase / PushReaction / LaunchHandoff | BuddahPredictedMotorConfig（含 Rigidbody 质量阻尼）、BuddahMovement 起跑时序、RaceBodyIntroStateController |
| RespawnRule / LapRule / WrongWayRule | BuddahRespawn、LapProgress、RaceCompletionTracker、RaceFinishManager（圈数） |
| DrivingTier / DrivingGlobal | AIDifficultyProfiles.Resolve（档位覆盖、速度 clamp、ThrustVectorPlanner 恢复阈值） |
| SkillDifficulty / Personality / PersonalitySkillWeight / PersonalitySituationBias / SkillOpportunity / SkillGlobal | AISkillCatalog.ApplyDesignTables；谓词常量进入 AISkillTuning 字段并替换了 AISkillDecision / AISkillWorld / AISkillCaster / AISkillCommitment 的字面量 |
| Roster | MatchSpawnManager 按槽位取人格与名字 |
| ModeRule | OnlineMatchRules / SoloMatchRules 按行读取；时长写入 RoomStateManager、PropertiesSelectionManager、ResultDecisionManager、RaceFinishManager；aiCount 由 SoloSetupPanel 读取 |
| Track / TrackSpawn / TrackCheckpoint / TrackResultAnchor | MiniMapController 边界、RaceResultAreaManager 偏移；MatchSpawnManager、Checkpoint、EndFieldSpawnPoint 在 Awake 把场景物体移到表中世界坐标 |
| Lobby | SteamLobbyManager 默认值、SessionLauncher 超时 |
| SelectionStage / SelectionOption | PropertiesSelectionManager 构建地图/皮肤阶段 |
| Intro | IntroSequenceManager |
| RacerNaming / Text / MenuOption | RacerIdentity、MatchSpawnManager、CreateRoomUI 下拉框；文案经 `ProjectConfigRuntime.Texts` |

未接通（结构性，表中为说明）：ModifierRule 的叠加策略与下限、ResultVoteRule 的法定规则、RespawnRule 的 teleport 标志、IntroLayout、SkillIcon（资源引用留在 prefab）、`ui.<class>.<slug>` 自动 key 的 UI 字面量（待策划定稿 key 后再替换）。

- 文案桥接：列名以 `Key` 结尾且引用 `UI/Text` 时，若记录类只有去掉 `Key` 的字段（如 `SkillDefinitionRecord.displayName`），导出器把 key 解析成 zh 文本写入，运行时读法不变；生成的记录类保留 `*Key` 字段，消费方通过 `ProjectConfigRuntime.Texts.Get(key)` 取文本。
- 枚举列在记录类里是 `string`，由 `__enums__` 校验，避免为每个表新增 C# 枚举；需要强类型时在消费方解析。
- 作用域：`Assets/Design/Tables/scope.json` 的 `export` 决定哪些 sheet 进数据库；`presentationOnly` 的 sheet 只校验不导出。
- 运行时不读 Excel；全部代码在 `BuddahGo.Editor` 程序集且以 `#if UNITY_EDITOR` 包裹。

## 7. 校验规则清单

| 级别 | 规则 |
|---|---|
| Error | 表头缺 `##var`/`##type`；字段名与记录类不匹配；主键重复或为空；类型转换失败；枚举名不在 `__enums__`；外键目标不存在；range 越界；`list` 分隔符后出现空元素 |
| Error | ProjectConfigValidator 现有规则（antiSkillId 存在、selection rule 引用技能存在等） |
| Warning | 列在记录类中存在但 Excel 缺失（使用默认值）；`doc` 列被运行时读取；GlobalRule 键无 consumer；SO 与表漂移（过渡期） |
| Info | 被忽略的 sheet 与列清单 |

## 8. 已同步的决定

2026-10-13 用户决定：表值以 Unity 内实际生效值为基线；能入表的都入表；曲线入表。原第 8 节 21 项全部按此关闭：结构类 3、5、7 按本稿执行（曲线为关键帧列表；上下文三列保留允许留空；校验报告提交）；入表类 8–19 全部入表，其中工程向列标 `adv`、结构常量标 `doc`、timeline 只读；唯一真相 20=slowtrap、21=12/10。字体子集重烘纳入导出器范围，待实现。

## 9. 过分复杂、本次未入表的内容（需另行决定）

1. **Feel 反馈的嵌套 prefab 覆盖参数**（Bloom、WhiteBalance、CinemachineImpulse 的强度/时长，被 Buddah.prefab 的嵌套覆盖改写）。表里只登记 eventId → 播放器；参数本身是嵌套 prefab 的 m_Modifications，扁平化需要重做 FeelPrefab 结构。
2. **VFX prefab 内部粒子参数**（ParticleSystem duration、lifetime、burst；VFX Graph 属性）。属于美术资产，表里只登记 prefab 路径与挂接方式。
3. **开场样条与 Cinemachine 镜头**（SplineContainer 的控制点、timeline 轨道内容）。表里只登记 timeline 时长与信号（只读）和槽位引用。
4. **Match/UI 的运行时布局常量**（SoloUIFactory 的坐标、尺寸、字号、颜色、Canvas 排序）。属于 UI 布局而非策划数据。
5. **小地图 UV 映射规则**（u↔Z、v↔X 反向写死）与 HUD 槽位号从节点名解析的约定。
6. **代码里的流程规则**：排名平局顺序、ready 门槛逻辑、黑幕隐藏其他玩家的恢复时点、承诺取消原因列表。表里只有 ModeRule / ResultVoteRule 等可枚举的部分。
7. **Legacy 组件副本**（BuddahMovement 的运动字段、4 个 legacy 效果组件）：不入表，迁移时删除。
8. **死字段**：LapProgress 的 forwardWrap 三字段、MiniMap 8 个废弃字段、SkillUiSpriteLibrary 的 *SpriteName：不入表，迁移时删除。
