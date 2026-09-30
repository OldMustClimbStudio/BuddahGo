# 提交与 PR

## 分支与标题

- 日常 PR 以 `dev` 为目标；`main` 用于稳定发布。
- Agent 新分支使用 `codex/<topic>`；团队现有 `feature/`、`fix/`、`chore/`、`docs/` 分支可继续使用。
- Commit 和 PR 标题使用 `feat:`、`fix:`、`refactor:`、`chore:` 或 `docs:`，后接具体变化，例如 `fix: restore movement after race countdown`。

## Worktree

为保持辨识度，后续 worktree 统一放在主项目根目录的 `.worktree/<topic>/` 下；从主项目创建时可用 `git worktree add .worktree/<topic> -b codex/<topic> dev`。
目录规则不改变上述分支命名，也不要求迁移已有 worktree。`.worktree/` 不提交到版本库。

## PR 内容

说明解决的问题、改后的行为，以及实际执行的验证。涉及场景、资源迁移或未完成事项时再补充；小改动几句话即可。

```markdown
## Summary
问题和改后的行为。

## Validation
实际运行的检查、结果，以及未验证的部分。
```

PR 描述与最终差异保持一致；只提交本次改动，包含必要的新文件和删除项。合并提交使用 GitHub 默认生成的消息。

## 按改动验证

- 文档：检查内容和链接；配置/工具：解析配置并运行受影响的命令。
- C# 或 Unity 包：编译并检查新错误；行为改动：运行相关复现或已有测试。
- 联机权限、RPC、预测：验证受影响的 host/client 路径；序列化/场景改动：检查引用和迁移。
- 检查通过后无需重复，除非后续改动影响结果。环境不具备时如实说明，静态检查不能写成运行通过。

## Unity 资源

资源与 `.meta` 一起提交，二进制遵循 `.gitattributes` 的 LFS 配置。保留 `Assets/`、`Packages/`、`ProjectSettings/` 的必要文件。
不提交缓存、生成的 `.csproj`/`.sln`、凭据、Blender 工作文件或无关插件示例；具体忽略项见 `.gitignore`。
删除资源前确认运行时加载引用，不能仅凭编辑器静态依赖报告判断未使用。
