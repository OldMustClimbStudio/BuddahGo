# BuddahGo 策划配表 Excel 结构规范（v0.3，基线工作簿已生成）

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
Assets/Scripts/Editor/DesignTables/                   导入器、校验器、CLI 入口（待实现，BuddahGo.Editor 程序集）
Assets/Plugins/ExcelDataReader/                       ExcelDataReader + .DataSet DLL（Editor only，待加入）
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
