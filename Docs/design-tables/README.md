# 策划配表系统（Design Tables）

BuddahGo 的平衡数值与各类信息由 Excel 维护，程序直接读取：策划在 `Assets/Design/Tables/` 下的工作簿里改数，保存后 Unity 自动校验并写入 `ProjectConfigDatabase`，运行时各组件从数据库取值。范围线是"游戏内平衡与信息"；精确的特效与表现细节仍由美术在 prefab / timeline 里调，表里只做登记。

状态（2026-10-13）：筛查、规范、基线工作簿、导入器、消费方接通、分层导入与 tag 过滤全部完成；PR [OldMustClimbStudio/BuddahGo#62](https://github.com/OldMustClimbStudio/BuddahGo/pull/62)。

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
| 让组件读新表 | 在组件 Awake/OnEnable 调 `DesignTableOverlay.Apply(this, 记录)`（同名字段自动覆盖，列名不同用 `"记录字段=目标字段"` 映射）；文案用 `DesignTableOverlay.Text(key, 兜底)`；全局标量用 `DesignTableOverlay.GlobalFloat(ruleKey, 兜底)` |
| CI / pre-commit | `Unity.exe -batchmode -nographics -projectPath . -executeMethod BuddahGo.DesignTables.DesignTableCli.ImportAndValidate -quit [-designForce] [-designTags skill,ai]`，退出码非 0 即失败 |
| 重建基线工作簿 | `python Tools/design-tables/build_tables.py . <输出目录>`：从工程实际值反向生成，只在重做基线时用 |
| 回归测试 | EditMode `BuddahGo.Tests.DesignTableTests`（读表、语法、校验、干跑导出、增量判定） |

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
Tools/design-tables/                  build_tables.py、gen_records.py、add_metas.py、unity_yaml.py、extract.py
Library/DesignTables/import-state.txt 每台机器的工作簿哈希（不提交）
```

## 文档索引

| 文件 | 内容 |
|---|---|
| [excel-spec.md](excel-spec.md) | 规范正文：表头约定、类型语法、命名与分层、工作簿与 sheet 目录、直通车管线与分层导入规则、校验等级、消费方接通表、未入表清单 |
| [decisions.md](decisions.md) | 15 条已拍板决定（管线、位置、文案、校验、范围线、覆盖方式、分层导入） |
| [inventory-skills.md](inventory-skills.md) | 筛查清单：技能、搓招、推手、执念、现有配置仓库逐字段状态 |
| [inventory-movement.md](inventory-movement.md) | 筛查清单：预测马达、受击、状态效果、起跑、重生、圈数、输入、全局设置 |
| [inventory-ai.md](inventory-ai.md) | 筛查清单：驾驶档位、技能难度、人格、谓词常量、名单 |
| [inventory-match-flow.md](inventory-match-flow.md) | 筛查清单：模式规则、赛道、大厅、结算、选择阶段、开场、命名、文案 |
| [inventory-ui.md](inventory-ui.md) | 筛查清单：UI 文案、HUD、图标、timeline、字体、编辑器工具、钉死数值的测试 |
| [inventory-assets.md](inventory-assets.md) | 筛查清单：prefab 注册、外观、VFX/Feel、场景、标签层、物理与输入资产 |

筛查清单记录的是 2026-10-12 基线 dev 9ff6338 的状态（字段 — 位置 — 代码默认 / 实际值 — 消费方 — 建议表 — 备注），用于追溯每个表值的来源；之后的改动以工作簿和 `__meta__` sheet 为准。

## 设计要点

- **值的基线是实际生效值**：工作簿由 prefab / 场景 / asset / JSON 的序列化值生成，代码默认值一律不用；由此发现并修正了如来神掌 12/10 与 SO 18/15、默认配装第 3 槽等多处漂移。
- **覆盖而非重构**：组件保留原字段作兜底，无表行时行为不变；不新增组件，不改 RPC 与预测逻辑。
- **只写成功的结果**：任一 Error 都不写产物；JSON 兜底由导出器生成（原文件曾缺结尾大括号）。
- **分层导入**：按工作簿 SHA1 判断变化，只重映射变化的工作簿及通过 `#ref` 依赖它的 sheet，其余列表从当前资产复制；无变化直接跳过。
- **没有第三方依赖**：xlsx 用 .NET 自带的 ZipArchive + XmlReader 读取，避开 NPOI 与 Unity 自带 SharpZipLib 的冲突和 EPPlus 的商用授权。

## 未完成与注意事项

- 表中仅作说明、未接运行时的结构性内容：ModifierRule 叠加策略与下限、ResultVoteRule 法定规则、RespawnRule 的 teleport 标志、IntroLayout、SkillIcon 的资源引用。
- `UI/Text` 里 `ui.<class>.<slug>` 形式的自动 key 需要策划定稿命名后，再替换代码里的 UI 字面量。
- 技能 SO、Buddah.prefab 与 `Tools/ai/difficulty-*.json` 上已被表接管的重复字段尚未删除；`AIDifficultyAssetTool` 待退役。
- 中文文案进表后若新增字符，`Assets/Fonts/SoloCJK` 的静态字体子集需要重烘。
- 覆盖路径尚未做联机双端与单机实机试玩；自动化验证为 EditMode 310/310 与批处理导入全流程。
- 工作簿必须在 Unity 工程内（`Assets/Design/Tables/`）才会自动导入；Excel 的 `~$` 锁文件已在 `.gitignore` 并被导入器忽略。
