# 配表决策记录

用户拍板的决定按日期追加，每条附影响范围。筛查阶段的待决清单见 [README.md](README.md)。

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

