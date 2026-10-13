# design-tables 生成脚本

把工程中实际生效的序列化值（prefab / 场景 / asset / JSON / ProjectSettings）抽取成 `Assets/Design/Tables/` 下的六本工作簿和 `_Export/` CSV 镜像。只在建立基线或重建基线时使用；日常流程是策划改 Excel、导出器写回工程。

```bash
python -m pip install --user openpyxl pyyaml
python Tools/design-tables/build_tables.py <仓库根> <输出目录>
```

- `unity_yaml.py`：Unity YAML 多文档读取，脚本 GUID 解析为类名。
- `extract.py`：组件字段、世界变换、曲线、枚举数组、timeline 时长、文案提取等辅助函数。
- `build_tables.py`：各 sheet 的列定义与取值来源；命名规则见 `Docs/design-tables/excel-spec.md`。

## 其他脚本

- `gen_records.py <仓库根>`：按 `Assets/Design/Tables/scope.json` 的 export 列表，从 `_Export` 表头生成 `ProjectConfigRecords.Generated.cs`、`ProjectConfigDatabase.Generated.cs` 与 `export-map.json`。表头改动后必须重跑。
- `add_metas.py <仓库根> [子目录…]`：为新增的文件夹、脚本、xlsx、csv、json 补 Unity .meta，避免提交无 meta 的资源。
