# 配表决策记录

用户拍板的决定按日期追加，每条附影响范围。用户没有裁定、由实现采用的做法标「建议（实现采用）」，与已定的决定区分。筛查阶段的待确认问题见各 inventory 清单末尾。

> 2026-10-26 核对（代码 0dbc551）：下列早期条目已被取代，原文保留作历史。决定 5、7 的工作簿名 SkillConfig / UIText 按决定 11 落为 `Skill/Skill.xlsx`、`UI/UI.xlsx`（文案在 `UI/UI.xlsx` 的 `Text`）；决定 8 的 `_reports/validation.md` 现为 `_Reports/validation.md`；决定 9 里如来神掌的"锁定 10"随 `castLockSeconds` 退役（决定 26）；决定 12 的 40 / 20 张是当时的数字，0dbc551 导出 37 张、presentationOnly 26 张，另有 `__enums__`、`__meta__`；决定 16 的 `Push` 三行现为 basic / charged 两行；决定 17 作废：反噬的施法失效，不加执念（玩法 README 6.2 已定）；决定 18 里 antiSkillId 的部分作废（决定 26）；决定 21 的"两个神掌 SO"现为一个：charged 行 → Skill_PushProjectileHands（`charged_anti` 已删，决定 26）。

## 2026-10-12

1. **数据管线沿用现有 `ProjectConfigDatabase`。** 继续使用 `Assets/Scripts/Config` 的记录列表 + `GlobalRule` 键值 + Repository + Validator 三层；新表作为新的记录类型加入同一数据库，不另起一套强类型代码生成。
   影响：Excel 每张 sheet 对应一个 `[Serializable]` 记录类；导出目标是 `ProjectConfigDatabase_Main.asset`（及 JSON 兜底）；全局标量继续走 `GlobalRule(ruleKey, ruleValue)`。
2. **源表放仓库内。** Excel 工作簿提交到仓库，不用在线表格。
   影响：需要 `.gitattributes` 把 `.xlsx` 标为二进制并走 LFS 或直接二进制；同时提交导出产物供 diff 与 review；约定一张表一个负责人减少合并冲突。
3. **先定 Excel 结构，再做导出器与迁移。** 结构定稿前不改代码与资产。
4. **任何"是否入表"未定的内容必须先与用户同步**，不得自行决定。待同步清单见 [excel-spec.md](excel-spec.md) 末尾。
5. **工作簿按域拆 6 本**：SkillConfig / Movement / AI / Match / UIText / Global，一本一责任人。
6. **源表放 `Assets/Design/Tables/`**，由 Unity 资源导入触发自动导表（AssetPostprocessor），不自建文件监视。
7. **文案走 key 引用**：数据表只放 key，文本集中在 `UIText.xlsx/Text`（zh/en）；字体子集重烘纳入导表流程的范围待定。
   影响：`SkillDefinitionRecord.displayName/description` 迁移为 key；需要新增 LocalizedTextRecord 与查询仓库。
8. **校验失败只出报告，不回写源 xlsx**：Unity Console 可点击定位 + `_reports/validation.md`。

## 2026-10-13

9. **表值基线 = Unity 内实际生效的序列化值。** 以 prefab / 场景 / asset / JSON 中的字段值为准，代码默认值一律忽略；唯一真相按运行时实际读取顺序定：默认配装第 3 槽 slowtrap（MAIN 优先于 prefab），如来神掌冷却/锁定 12/10（配置优先于 SO）。
10. **能入表的都入表。** 筛查清单中标记"偏工程"的内容也入表，用 `##group=adv` 标为高级列；结构性常量入 `Global/StructuralConstant` 但全列 `doc`（只记录不导出）。曲线字段入表，用关键帧列表 `time,value,inSlope,outSlope|…` 表示。过分复杂的内容不入表，见 excel-spec 第 9 节。
11. **命名与文件夹分层规范**：目录 `Assets/Design/Tables/<Domain>/<Domain>.xlsx`，镜像 `_Export/<Domain>/<Sheet>.csv`；工作簿与 sheet 用 PascalCase 单数且与记录类同名（去掉 Record 后缀），子表以父表名为前缀；字段 camelCase 与 C# 一致（`*Seconds`、`*Degrees`、`*01`）；id 全小写 snake_case；文案 key 为 `domain.entity.field` 点分小写；主键列最左、`doc` 列最右。
12. **范围线：只接管游戏内平衡与各类信息，不接管精确的特效/表现细节。** 导出器只把 `Assets/Design/Tables/scope.json` 的 `export` 列表（40 张 sheet）写入 ProjectConfigDatabase；`presentationOnly` 的 20 张 sheet 保留在工作簿里供查阅并参与校验，但不导出、不接消费方。
13. **导出器实现方式**：不引入第三方 DLL，用 .NET 的 ZipArchive + XmlReader 直接读 xlsx；记录类由 `Tools/design-tables/gen_records.py` 从表头生成；触发方式为 AssetPostprocessor（可在菜单关闭）+ 菜单 + batchmode CLI。
14. **消费方切换方式：覆盖而非重构。** 各组件在 Awake/OnEnable 用 `DesignTableOverlay.Apply` 把记录字段按名（或显式映射）写到自己的序列化字段，prefab/场景/SO/JSON 的值只作为无表行时的兜底；规则类（OnlineMatchRules/SoloMatchRules）按行读表。不新增组件，不改 RPC 与预测逻辑。
15. **分层导入与 tag 分类。** 导入先按工作簿文件哈希（`Library/DesignTables/import-state.txt`）判断是否变化，只重映射变化工作簿及通过 `#ref` 依赖它的 sheet，其余列表沿用当前资产；`scope.json` 的 `tags` 给每张 sheet 打标签（balance / info / text / track + 域名），菜单 `Import Tag/…` 与 CLI `-designTags` 只推送某一层。


## 2026-10-13（v2 重组）

16. **按 [structure-audit.md](structure-audit.md) 第三节的 v2 结构重组工作簿。** 技能→运动→AI→比赛每域一次提交（7ae8ea1、67ffe66、c51d32f、4830192），不碰 RPC 与预测；每处问题的处理见 structure-audit 第五节。
    影响：`Skill` = SkillDefinition + SkillBalance + 主效果时长；`SkillEffect<Family>` 强类型表；`Push` 三行；`Movement` / `Respawn` 各一行；LapRule 并入 `Match/Track`、WrongWayRule → `Match/TrackWrongWay`；AI 表 camelCase、单行 `DrivingTuning` / `SkillTuning`、权重进 Personality 三槽；RacerNaming 删除、占位阶段行屏蔽；`GlobalRule` 只留 `target_frame_rate`。
17. （2026-10-26 作废，见 26）**反噬行的 obsessionGain 保持现行语义。** 反噬仍按原技能加执念；anti 行该列留空，非 0 报 Warning（表级 `DesignTableDomainRules.SkillRowsAreConsistent`，资产级 `ProjectConfigValidator`）。
18. **技能 id 改名 `anti_blackcurtain` → `blackcurtain_anti`**，`Global/__enums__` 增加 `SkillId` 作为唯一名单（`Skill.skillId` 类型 `enum:SkillId`）；SO 资产的 skillId / antiSkillId 同步改，文案 key 随之改为 `skill.blackcurtain_anti.*`。
19. **逆转技能中文定为「逆姻缘」**（与 HUD 贴图文件一致），文案 key 不变。
20. **主效果时长进 `Skill.effectDurationSeconds`**，按技能类写入 SO 的主时长字段（`SkillDatabase.EffectDurationField`）；家族表只放其余参数；代码 / prefab 里的效果常量进 presentationOnly 的 `SkillConstant`。
21. **跨对象覆盖只用显式映射**（`DesignTableOverlay.ApplyMapped`）：Push 行 → BuddahHandControl / 两个神掌 SO，Movement 行 → BuddahMovement / RaceBodyIntroStateController，Track 行 → LapProgress。同名自动覆盖（`Apply`）只用于记录与组件一一对应的情况；AI 表的 camelCase 表头按名忽略大小写匹配 PascalCase 字段。
22. **可编辑性规则：导出表里每一列都必须有读取方，否则标 `doc` 或整表转 presentationOnly。** 2026-10-13 审计后：接通了 Movement 的胶囊半径/高度/中心（BuddahPredictedMotorConfig）、Track.trackSplineSamples（TrackSplineRef）、ModeRule.requiresReady（RoomStateManager）、Lobby.defaultVisibility / maxPlayerNameLength（SteamLobbyManager / RoomStateManager）、SelectionOption.iconKey / unlocked；标 doc 的列：Movement.autoThrottle / physicMaterial，ModeRule.minPlayersToStart / hostAutoReady，Track.minimapSpritePath / minimapImageWidth，Lobby.minPlayers / fishnetMaximumClients，SelectionStage 的 order / enabledOnline / enabledSolo / layoutMode / autoFillOnTimeout，Text.en，GlobalRule.description；转 presentationOnly 的表：ResultVoteRule、IntroLayout、SkillIcon。IMatchRules 暴露但游戏内尚无读取方的四个占位开关（allowSkipSpectating / allowPause / showRacerNameTags / showOpponentsOnMinimap）保留导出并在 comment 标明"占位"，因为它们是 Solo 契约的一部分。
23. **测试不钉死策划数值。** `DesignTableTests` 与 `DesignTableRuntimeTests` 把产物值和表里的单元格比，不与字面量比，策划改数不会让测试失败；每个显式映射的目标字段都用反射核对存在。

## 2026-10-14

24. **编辑器 Play 中的技能表写入时机**：写入技能 SO 的值在每局 Play 首次查技能时写入，编辑器退出 Play 时恢复资产（2d34e2a）。起因：实测加速时长改为 3 秒后仍为 10 秒。
25. **PR #62 review 定下的读取规则**（d38bc31）：按赛道的表只认组件所在场景（其次活动场景）的 Track 行，没有行就沿用场景值，不借用第一行；`text` 列保留首尾空格（"Player " 这类前缀）；空单元格就是 0，不回退代码默认值，`SkillOpportunity` 每个技能只读自己的列；向量 / 颜色分量数不符是错误；CreateRoomUI 的下拉选项以 `UI/MenuOption` 为准。

## 2026-10-26（玩法深化阶段 B，[Docs/gameplay/README.md](../gameplay/README.md) §7.2）

26. **反噬变体从表里退役**（已定：玩法 README 6.2「六种 `_anti` 变体由统一惩罚取代」、7.0「退役的行和列先留在表里，代码不再读取，阶段 B 再删」）。反噬改为统一惩罚（本次施法失效、不加执念，本槽照常进冷却，惩罚参数在 `Obsession`），`Skill` 删 `castLockSeconds`、`antiSkillId` 两列与 6 行 `_anti`，家族表、`Push`、`SkillPresentation`、`SkillConstant`、`SkillSelectionRule`、`FeelEvent` 的反噬行与反噬专用列一并删除，读取它们的代码同时删掉。`FeelEvent` 的 `Anti_Reflection_Shared / Local` 保留：统一反噬的确认窗口播的就是它（已定，玩法 README 6.2）。VfxRegistry 的反噬 id、`SkillId` 枚举的 6 个 anti 值与 `skill.*_anti.*` 文案也保留，没有行引用它们（建议（实现采用））。决定 17 作废，决定 18 里 antiSkillId 的部分作废。**2026-10-31 补记：** 用户要求 PR 中途 review 时清理所有已退役内容，这三类没有读取方，已删除，只留 `FeelEvent` 的 `Anti_Reflection_*`（[退役记录](../gameplay/retired.md)）。
27. **缘起入表：一张 `Skill/Yuanqi` 宽表 + 每对一张 `YuanqiEffect<对>` 家族表**，与 `Skill` + `SkillEffect<家族>` 同构；字段名就是 SO 字段名，没有参数的对不建家族表，`pairNumber` 只为对照、标 doc（已定：[缘起架构](../gameplay/yuanqi-architecture.md) Q14，2026-10-25「预留字段名；表要可延展、可读」，结构见该文 §3.1–3.3）。参数初值是 [缘起设计](../gameplay/yuanqi.md) 的阶段 A 占位值（2026-10-25 已定，试玩后在表里调）。以下是建议（实现采用）：`Yuanqi` 行显式映射（`SkillDatabase.YuanqiRowMap`），家族行同名覆盖并按 `YuanqiFamilyActionType` 校验类名。
28. **训练木桩配置并入 `Match/ModeRule` 的 Practice 行**（建议（实现采用）：玩法 README 7.2 的原计划（66b3602 及之前）给了"新建 Match 表"和"并进 ModeRule 的 Practice 行"两种做法，用户未裁定；并进 ModeRule 比新建 `Match/TrainingDummy` 表小，木桩只在 Practice 出现）；摆放的工程值留在 `MatchSpawnManager`。数量 3、间距 15 m、复位 3 s 是玩法 README A1 的已定值；起点距离 90 m、横向 10 m 是建议（实现采用）（用户定的是"起跑后第一段直道，偏左 / 居中 / 偏右"）。
29. **`ModifierCommandType` 枚举值与 C# 同号**（建议（实现采用））：导入校验接受 value 整数，消费方把整数当 C# 枚举值；原来从 0 起编，`1` 在表里是 RootThenAcceleration、在代码里是 Acceleration。

