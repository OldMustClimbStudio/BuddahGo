# 策划配表系统（Design Tables）

BuddahGo 的平衡数值与各类信息由 Excel 维护，程序直接读取：策划在 `Assets/Design/Tables/` 下的工作簿里改数，保存后 Unity 自动校验并写入 `ProjectConfigDatabase`，运行时各组件从数据库取值。范围线是"游戏内平衡与信息"；精确的特效与表现细节仍由美术在 prefab / timeline 里调，表里只做登记。

状态（2026-10-13）：筛查、规范、基线工作簿、导入器、消费方接通、分层导入与 tag 过滤全部完成，并已按 [structure-audit.md](structure-audit.md) 的 v2 结构重组（一概念一表：Skill 宽表 + 强类型家族表 + Push、Movement / Respawn 合一、圈规则并入 Track、AI 单行调参表、权重进三槽）；PR [OldMustClimbStudio/BuddahGo#62](https://github.com/OldMustClimbStudio/BuddahGo/pull/62)。

2026-10-26 玩法深化阶段 B（[Docs/gameplay/README.md](../gameplay/README.md) §7.2，分支 feat/mechanics-update，PR #63）：反噬改为统一惩罚，`Skill` 表删掉 `castLockSeconds`、`antiSkillId` 两列和 6 行 `_anti`，家族表、`Push`、`SkillPresentation`、`SkillConstant`、`SkillSelectionRule`、`FeelEvent` 里的反噬行和反噬专用列一并删除，`UI/UiTiming` 删掉 SkillSlotUI 的三行（含 `fallbackCastLockSeconds`）；`Obsession` 加同招加价与反噬惩罚五列；新增缘起表 `Skill/Yuanqi`、`YuanqiEffectFrostLock`、`YuanqiEffectGiantCharge`；训练木桩配置在 `Match/ModeRule` 的 Practice 行；`Movement/ModifierRule` 加 Freeze / EndModifier / Burst / MaxSpeedMultiplier 四行，`__enums__` 的 `ModifierCommandType` 改为与 C# 同号；`Global/InputBinding` 记录 Q/E/R + 1/2/3 技能键与鼠标推掌；`StructuralConstant.prediction_protocol_version` 为 2。阶段 A 的序列化默认值从此只作无表行时的兜底。工作簿现有 65 张 sheet：导出 37、只校验 26、保留 2（`__enums__`、`__meta__`）。

## 快速上手

### 策划：改一个数值
1. 用 Excel 打开 `Assets/Design/Tables/<域>/<域>.xlsx`（Unity 菜单 `Tools/Design Tables/Open Tables Folder`）。六本：`Skill`（技能）、`Movement`（运动与规则）、`AI`、`Match`（比赛流程与赛道）、`UI`（文案、图标、菜单）、`Global`（全局规则、枚举、元信息）。
2. 只改第 5 行起的数据行。前四行是表头：`##var` 字段名、`##type` 类型与校验、`##group`（`adv` 高级列、`doc` 只记录不导出）、`##comment` 说明与来源。A 列以 `##` 开头的数据行会被整行忽略，可用来临时屏蔽。
3. 保存。Unity 在半秒内自动导入（可在菜单 `Auto Import On Save` 关闭，改用 `Import Changed`）。
4. 看 Console：出错会定位到「工作簿/sheet!单元格」，并且不写任何产物，游戏仍用上一次的数据。报告在 `Assets/Design/Tables/_Reports/validation.md`（菜单 `Open Report`）。
5. 提交时连同 xlsx 一起提交 `_Export/` 下的 CSV 镜像、`ProjectConfigDatabase_Main.asset` 和 `Resources/Config/ProjectConfigDatabase.json`，review 看 CSV 的 diff 即可。

### 策划：只推某一层
菜单 `Tools/Design Tables/Import Tag/…`：`balance`（数值）、`info`（规则与映射）、`text`（文案）、`track`（随赛道），或按域 `skill / movement / ai / match / ui / global`。tag 在 `scope.json` 登记。

### 程序：常用操作
| 任务 | 做法 |
|---|---|
| 新增或改列 | 改 xlsx 表头 → `python Tools/design-tables/gen_records.py .` 重新生成记录类与 `export-map.json` → Unity 重新编译 → 导入 |
| 新增 sheet | 在 `Assets/Design/Tables/scope.json` 的 `export`（导出）或 `presentationOnly`（只校验）登记，补 `tags`，再按上一行走 |
| 让组件读新表 | 在组件 Awake/OnEnable 调 `DesignTableOverlay.Apply(this, 记录)`（同名字段自动覆盖，列名不同用 `"记录字段=目标字段"` 映射）；一行要写到别的对象、或记录里有同名但不归它的字段时，用 `ApplyMapped` 只写列出的字段。取行：单行表用 `DefaultRow`，按赛道的表用 `CurrentTrack(this)` / `ForCurrentTrack(list, …, this)`，按模式用 `ModeRule(modeId)` / `CurrentModeRule()`，大厅用 `Lobby()`。文案用 `Text(key, 兜底)`，全局标量用 `GlobalFloat(ruleKey, 兜底)` |
| CI / pre-commit | `Unity.exe -batchmode -nographics -projectPath . -executeMethod BuddahGo.DesignTables.DesignTableCli.ImportAndValidate -quit [-designForce] [-designTags skill,ai]`，退出码非 0 即失败 |
| 重建基线工作簿 | **已过时，勿用（2026-10-26 机制更新阶段 B 之后）**：`build_tables.py` / `v2_layout.py` 仍按退役的结构生成（`_anti` 行、`antiSkillId`、`castLockSeconds`），也不认识 `Skill/Yuanqi*`、`Obsession` 五个惩罚列、`ModeRule` 的 `trainingDummy*` 列和 `InputBinding.status`；而且 `_anti` 资产已删，脚本一启动就因找不到文件中止。生成器追上提交的工作簿之前，不要用它重做基线 |
| 改表结构 | 在 `Tools/design-tables/v2_layout.py` 写**新的**变换函数（登记进 `v2_layout.PHASES`，scope 改动登记进 `migrate_v2.SCOPE_EDITS`），`python Tools/design-tables/migrate_v2.py . --phase <新阶段>` 就地改写 xlsx + CSV 镜像 + scope.json，再 `gen_records.py`；值只搬不改，review 看 CSV diff。已有的 `skill / movement / ai / match / editability` 是 2026-10-13 的一次性 v1→v2 迁移，不要再跑。小改动（加列、加行、改注释）也可以直接用 `tablemodel.py`（`read_tables` 读入，改完写回 xlsx 与 CSV 镜像，CSV 与导出器逐字节一致） |
| 回归测试 | EditMode `BuddahGo.Tests.DesignTableTests`（读表、语法、校验、干跑导出、增量判定）与 `DesignTableRuntimeTests`（映射字段存在、产物值等于单元格值、保存回调只重映射依赖表）；两者都与表格单元格比值，策划改数不会让测试失败 |
| 验证自动导入 | `Unity.exe -batchmode -nographics -projectPath . -executeMethod BuddahGo.DesignTables.DesignTableCli.ProbeAutoImport -quit`：真实触发 xlsx 的资源导入回调并执行导表，退出码 0 即通 |

## 修改手册

### 单元格怎么写
`##type` 行决定每列的写法与校验，常见类型：

| `##type` | 写法 | 例子 |
|---|---|---|
| `int` / `float` | 数字，可带 `#range=最小,最大` | `12`、`0.35` |
| `bool` | `true` / `false`（也接受 `1` / `0` / `是` / `否`） | `true` |
| `string` / `text` | `string` 去掉首尾空格；`text` 原样保留（含首尾空格与换行），用于显示文案 | `acceleration`、`Player ` |
| `enum:名称` | 写枚举名，名单在 `Global.xlsx` 的 `__enums__` sheet；也接受该成员的 `value` 整数（与 C# 枚举同号的，如 `ModifierCommandType`、`YuanqiMode`，写数字和写名字等价） | `Selectable`、`Hard` |
| `list,类型#sep=|` | 用分隔符连接，默认 `\|`，搓招用 `,` | `movement\|buff`、`W,Up,W` |
| `vector3` / `color` | 逗号分隔，分量数必须正好 3 / 4，少写报错 | `0,0,-2`、`1,0.7,0,1` |
| `curve` | 关键帧列表 `time,value,inSlope,outSlope` 用 `\|` 连接 | `0,0,2,2\|1,1,0,0` |
| `xxx#key` | 主键列，同表不可重复；多列 `#key` 组成复合键 | `skillId` |
| `xxx#ref=表.列` | 外键，值必须存在于目标表；跨工作簿写 `UI/Text.key` | `acceleration` |
| `string#path` | 工程内相对路径，不存在只给警告 | `Assets/UI/…png` |

- 留空：数字视为 0，bool 视为 false，列表视为空，外键留空表示"无"。`ruleSetId` / `modeTag` / `mapTag` 留空等于 default / 全部。空格就是 0：不会退回代码里的默认值，所以"该技能用的列"必须填值（例如 `AI/SkillOpportunity` 每个技能只读自己那几列，其余列留空也不会被读）。
- 公式可以用，导入读取的是计算结果；不要合并单元格、不要在表头行上方插行。
- 单行全局表（`profileId` = `default`）只改值不加行；按行的表（技能、人格、出生点）加行即新增记录，主键必须唯一。
- 临时屏蔽一行：在 A 列写 `##`。删除一行即删除记录。
- 文案：数据表只放 key（如 `skill.giant.name`），文本在 `UI.xlsx/Text` 的 `zh` 列改；新增文案先在 Text 加 key，再在数据表引用，否则报"text key not found"。
- 技能：身份、冷却/前摇、执念、主效果时长都在 `Skill` 一张表，每个技能一行（反噬不再有变体行：本槽照常进本行冷却，惩罚参数在 `Obsession`）；家族专有参数在 `SkillEffect<家族>`，每个技能一行；推掌与神掌弹道在 `Push`（basic / charged；basic 的 `rotationSpeedDegPerSec` 是鼠标转手速度上限）；新技能先在 `Global/__enums__` 的 `SkillId` 登记。
- 缘起：一对一行在 `Skill/Yuanqi`（两招、谁被消耗、Instant / Sustained、是否启用），有参数的对另有 `YuanqiEffect<对>` 一行（#2 的 N、R，#4 的突进与 M）。`skillA / skillB / consumeA / consumeB / mode / retriggerPolicy / isEnabled` 必须填：留空的招式导入成空串（这一对直接不登记），留空的 bool 等于 false（`isEnabled` 留空就关掉了这一对）；两招不能相同，同一对只能登记一次，Sustained 不能消耗（`mode` 写名字或写枚举值 `1` 都按 Sustained 校验）。消耗什么完全看 `consumeA / consumeB`，两招谁写在 A 列都行，代码不会另外硬消耗。新的一对：策划在 `__enums__` 的 `YuanqiId` 登记、在 `Yuanqi` 加一行、在 `UI/Text` 加 `yuanqi.<id>.name / .desc`；程序加 `Yuanqi_*` 类和 `.asset`，并把资产放进 `Skill Database.asset` 的 `yuanqis` 列表（没有资产的行运行时被 `SkillDatabase` 跳过，EditMode 测试 `EveryYuanqiRowHasAnAssetOfItsFamily` 会失败）。这一对有参数时再加 `YuanqiEffect<对>` sheet（`scope.json` 登记 → `gen_records.py`），并在 `SkillDatabase.YuanqiFamilyActionType` 和 `ApplyDesignTablesToSkills` 里各加一行，在 `SkillConfigCrossValidator.CompareYuanqis` 加一行 `CompareYuanqiFamily(…, config.yuanqiEffect<对>s)`（否则这一家族不比对，类名不符也不报）。`yuanqi.<id>.desc` 没有界面显示（`SkillDatabase.GetYuanqiDescription` 已提供，选技能界面只列名称和两招）。
- 训练木桩：数量、间距、起点距离、横向距离、复位秒数在 `Match/ModeRule` 的 Practice 行（Online / Solo 填 0）；数量大于 0 时其余四格必须填。`MatchSpawnManager` 只读 Practice 行，是否生成看 `IMatchRules.SpawnTrainingDummies`（只在 Practice 为真）。
- `##group` 为 `doc` 的列只是说明，改了不会进游戏；`adv` 是高级/工程参数，改之前确认影响。规则（决定 22）：导出表里不是 `doc` 的列都有读取方，comment 写明是谁读；无人读的列必须标 `doc`，整表无人读的转 presentationOnly。
- Excel 保存后的数字可能带二进制噪音（0.008 存成 8.0000000000000002E-3），导入器会按 15 位有效数字归一，镜像 diff 只显示你改过的格子。
- 想改一个表里没有的参数：先按"程序：常用操作"加列并生成记录类，再让对应组件读取；没有读取方的列不会生效。

### 改完怎么进游戏
1. **保存 xlsx。** Unity 检测到文件变化后 0.5 秒自动导入（Excel 可以继续开着）。没有自动导入时，菜单 `Tools/Design Tables/Import Changed`。
2. **导入做了什么**：读全部工作簿 → 校验所有 sheet（主键、类型、范围、枚举、外键、路径、跨表规则，如检查点数量必须等于圈规则要求、AI 人格数等于名单数）→ 只重映射有变化的工作簿及引用它的 sheet → 写 `ProjectConfigDatabase_Main.asset`、`Resources/Config/ProjectConfigDatabase.json`、`_Export/*.csv` 镜像、`_Reports/validation.md`。
3. **看结果**：Console 一行 `[DesignTables] changed=… remapped=… kept=… errors=0`；有错时每条形如 `Skill.xlsx/Skill!G7 :: '12x' is not a number`，按单元格改回即可。只要有一条 Error，所有产物都不写，游戏继续用上一次数据。
4. **什么算"变化"**：按工作簿文件内容哈希（记在本机 `Library/DesignTables/import-state.txt`）。改了 `UI.xlsx`，引用了 `UI/Text` 的 `Skill/Skill`、`Skill/Yuanqi`、`Match/ModeRule` 等也会跟着重映射；没改的列表原样保留。日志出现 `nothing changed` 说明文件内容没变。
5. **什么时候用 Force / Tag**：换了机器或删过 `Library`、怀疑产物不一致时用 `Import All (Force)`；只想推某一层（比如只推文案）用 `Import Tag/text`，它忽略哈希只重映射带该 tag 的 sheet。
6. **进游戏生效时机**：各组件在 Awake/OnEnable 读表，所以进入场景或重新开局时生效；编辑器里正在 Play 时改表需要重新 Play。技能的主时长、家族参数、Push 弹道，以及缘起行（`Yuanqi`）和缘起参数（`YuanqiEffect<对>`），在每局 Play 第一次查技能时写入技能 SO 与 `Yuanqi_*` SO；编辑器退出 Play 后这些 SO 自动恢复原值，所以 Inspector 里看到的始终是资产本身的值，不是表值。
7. **提交**：把 xlsx、`_Export` 镜像、主资产、JSON 一起提交；review 看镜像 CSV 的行级 diff。回退就 `git checkout` 这几个文件再导入一次。

### 常见问题
- **保存后没有反应**：确认文件在 `Assets/Design/Tables/` 内且 Unity 窗口已获得焦点触发刷新；或菜单 `Auto Import On Save` 被关掉了；或 Unity 正在编译，导入会在编译结束后自动补做。
- **报 "column 'xxx' has no field on …Record"**：表头加了新列但没有重新生成记录类，运行 `gen_records.py` 并等 Unity 编译。
- **报 "not a member of enum"**：枚举名拼写或大小写不对，去 `Global/__enums__` 核对。
- **报 "duplicate key"**：主键重复，或同一技能需要进多个选池时主键列不够（`SkillSelectionRule` 的主键含 `poolType`）。
- **Excel 提示文件被占用**：导入是只读共享打开，不会锁文件；是另一个 Excel 实例或 `~$` 锁文件残留。
- **改了值但游戏没变**：看报告是否 `outputs written: yes`；再确认该列不是 `doc` 列、该表在 `scope.json` 的 `export` 里、对应组件已接表（见 excel-spec 6.3）。
- **新赛道 / 新场景没吃到表**：按赛道的表（Track、TrackWrongWay、TrackSpawn 等）只认 `Match/Track.sceneName` 与组件所在场景（其次活动场景）同名的行，没有行就沿用场景 / prefab 里的值，不会借用别的赛道。先在 Track 加一行。

## 目录

```text
Assets/Design/Tables/
  <Domain>/<Domain>.xlsx        源表（LFS）
  _Export/<Domain>/<Sheet>.csv  文本镜像，工具生成，禁止手改
  _Reports/validation.md        最近一次校验报告
  scope.json                    导出范围、presentationOnly、tags
  export-map.json               sheet → 记录类 → 数据库列表（gen_records.py 生成）
Assets/Scripts/Editor/DesignTables/   XlsxReader、Schema、Validator、DomainRules、RecordMapper、Exporter、ImportState、Menu、Postprocessor、Cli
Assets/Scripts/Config/                ProjectConfigDatabase（+ Generated 记录类与列表）、Repositories、DesignTableOverlay（运行时覆盖助手）
Tools/design-tables/                  build_tables.py、v2_layout.py、migrate_v2.py、tablemodel.py、gen_records.py、add_metas.py、unity_yaml.py、extract.py
Library/DesignTables/import-state.txt 每台机器的工作簿哈希（不提交）
```

## 文档索引

| 文件 | 内容 |
|---|---|
| [excel-spec.md](excel-spec.md) | 规范正文：表头约定、类型语法、命名与分层、工作簿与 sheet 目录、直通车管线与分层导入规则、校验等级、消费方接通表、未入表清单 |
| [decisions.md](decisions.md) | 29 条决定，按日期追加：管线、位置、文案、校验、范围线、覆盖方式、分层导入（2026-10-12/13）、v2 重组与读取规则（2026-10-13/14）、机制更新阶段 B（2026-10-26）；用户未裁定的实现做法标「建议（实现采用）」 |
| [structure-audit.md](structure-audit.md) | 表结构缺陷盘点与代码考古：同一概念被拆散的 26 处问题、每处的来源提交、v2 结构与第五节的处理结果（2026-10-13 已实施；开头注明 2026-10-26 阶段 B 之后不再成立的条目） |
| [inventory-skills.md](inventory-skills.md) | 筛查清单：技能、搓招、推手、执念、现有配置仓库逐字段状态；第 15 节是 2026-10-26 阶段 B 的反噬退役与缘起表 |
| [inventory-movement.md](inventory-movement.md) | 筛查清单：预测马达、受击、状态效果、起跑、重生、圈数、输入、全局设置 |
| [inventory-ai.md](inventory-ai.md) | 筛查清单：驾驶档位、技能难度、人格、谓词常量、名单 |
| [inventory-match-flow.md](inventory-match-flow.md) | 筛查清单：模式规则、赛道、大厅、结算、选择阶段、开场、命名、文案 |
| [inventory-ui.md](inventory-ui.md) | 筛查清单：UI 文案、HUD、图标、timeline、字体、编辑器工具、钉死数值的测试 |
| [inventory-assets.md](inventory-assets.md) | 筛查清单：prefab 注册、外观、VFX/Feel、场景、标签层、物理与输入资产 |

筛查清单记录的是 2026-10-12 基线 dev 9ff6338 的状态（字段 — 位置 — 代码默认 / 实际值 — 消费方 — 建议表 — 备注），用于追溯每个表值的来源；之后的改动以工作簿和 `__meta__` sheet 为准。2026-10-26 机制更新改到的内容在各清单开头的说明里列出（哪些基线条目不再适用、现在看哪里）。

## 设计要点

- **值的基线是实际生效值**：工作簿由 prefab / 场景 / asset / JSON 的序列化值生成，代码默认值一律不用；由此发现并修正了如来神掌 12/10 与 SO 18/15、默认配装第 3 槽等多处漂移。
- **覆盖而非重构**：组件保留原字段作兜底，无表行时行为不变；不新增组件，不改 RPC 与预测逻辑。
- **只写成功的结果**：任一 Error 都不写产物；JSON 兜底由导出器生成（原文件曾缺结尾大括号）。
- **分层导入**：按工作簿 SHA1 判断变化，只重映射变化的工作簿及通过 `#ref` 依赖它的 sheet，其余列表从当前资产复制；无变化直接跳过。
- **没有第三方依赖**：xlsx 用 .NET 自带的 ZipArchive + XmlReader 读取，避开 NPOI 与 Unity 自带 SharpZipLib 的冲突和 EPPlus 的商用授权。

## 未完成与注意事项

- 只记录、不接运行时：SkillConstant / DrivingConstant（代码与 prefab 常量）、ModifierRule 叠加策略、Respawn 的 teleport 标志列、Movement 的三个起跑常量列、ResultVoteRule 法定规则、IntroLayout、SkillIcon 的资源引用。
- 代码待办（结构审计 A8 / C1）：ObsessionFigure 改为读全局单例而非每角色一份；四个 manager 的时长字段只读 ModeRule 后删除自己的序列化字段。结构审计 A7（HUD 按键 token 由表生成）随玩家搓招退役不再需要：token 图已隐藏，`Skill/ComboInput`、`Skill/ComboBinding` 只剩 AI 逐键搓招（`ComboSkillInput.InjectServerKey`）在读，按玩法 README 7.2 在 A7（AI 适配，未做）之后删除。
- 逆转技能 HUD 贴图文件名 `逆姻缘.png` 与中文一致（2026-10-13 定为「逆姻缘」）；`giant_anti` 行已在阶段 B 删除（16121ed），`skill.giant_anti.*` 文案 key 保留。
- `UI/Text` 里 `ui.<class>.<slug>` 形式的自动 key 需要策划定稿命名后，再替换代码里的 UI 字面量。
- 技能 SO、Buddah.prefab 与 `Tools/ai/difficulty-*.json` 上已被表接管的重复字段尚未删除；`AIDifficultyAssetTool` 待退役。阶段 B 接上的 `ObsessionFigure` 五个同招加价与反噬惩罚字段、`Yuanqi_*` SO 字段和 `MatchSpawnManager` 的五个木桩字段同样保留，只作无表行时的兜底。
- 中文文案进表后若新增字符，`Assets/Fonts/SoloCJK` 的静态字体子集需要重烘。2026-10-26 新增的缘起文案（`yuanqi.*`）用到子集外的字：名称缺巨、冰、封、破、阵，描述还缺冻、突等；HUD 在字体缺字时把缘起名称退回显示 id。A6 HUD 标签（`InGameHudLabels`，可选 key `hud.*`，`UI/Text` 里还没有这些行）里「冻结」「缘起」缺冻、起，退回英文。
- 自动化验证（PR #63 记录）：无头 EditMode `BuddahGo.Tests` 559/559，批处理导入 0 错误（`_Reports/validation.md`：重映射 37 张、402 条记录、3 条 HudObsession 路径 Warning），训练模式 `MechanicsPracticeHarness` 13/13（见 [practice-verification.md](../gameplay/practice-verification.md)，本机 host）。联机双端、单机带 AI 和用户试玩都还没做。
- 工作簿必须在 Unity 工程内（`Assets/Design/Tables/`）才会自动导入；Excel 的 `~$` 锁文件已在 `.gitignore` 并被导入器忽略。
