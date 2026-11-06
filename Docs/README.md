# 文档索引

## 当前入口

| 需要了解 | 文档 |
|---|---|
| 系统职责与比赛流程 | [architecture.md](architecture.md) |
| 房间名单、握手、配装与身份 helper | [session-helpers.md](session-helpers.md) |
| FishNet 权限、同步与场景要点 | [networking.md](networking.md) |
| 预测与回放的核心设计 | [prediction-design.md](prediction-design.md) |
| Unity MCP 启动与连接 | [unity-mcp.md](unity-mcp.md) |
| 术语表 / 架构决策 | [../CONTEXT.md](../CONTEXT.md) · [adr/](adr/) |
| 单机模式（AI、离线流程）：现状、规格与事实 | [single-player/HANDOFF.md](single-player/HANDOFF.md) |
| 策划配表：Excel 工作簿、导入器、规范与筛查清单 | [design-tables/README.md](design-tables/README.md) |
| 技能特效：分层规范、流程、插槽、预算与档位；每个技能一篇专题 | [vfx/README.md](vfx/README.md) |
| 玩法改动的退休清单 | [gameplay/retired.md](gameplay/retired.md) |
| 渲染入口：改动前读什么、不变量、抓图—烘焙—对比流程 | [rendering/HARNESS.md](rendering/HARNESS.md) · [rendering/ARCHITECTURE.md](rendering/ARCHITECTURE.md) |
| 画质设置：选项与档位、运行时接口、未实现与占位清单 | [rendering/graphics-settings.md](rendering/graphics-settings.md) |
| 2026-11 全项目代码复核：结论与待你决定的事 | [code-review/README.md](code-review/README.md) |

## 历史（追查旧问题时再看）

已收尾的修复、计划和复核放在 [archive/](archive/)，其余在 Git 历史（例如 `git log -- Docs/prediction-refactor-plan/`）。

| 内容 | 文档 |
|---|---|
| 架构优化计划 P0–P7（PR #47–#58 已合入） | [archive/optimization/](archive/optimization/HANDOFF.md) |
| 联机预测撕裂与开场交接修复（PR #61，已收尾；问题复现时从 progress 接续） | [archive/online-repair/](archive/online-repair/HANDOFF.md) |
| 开场交接切换的本地卡顿修复（PR #60） | [archive/handoff-continuity/](archive/handoff-continuity/HANDOFF.md) |
| 单机模式开发期的复核、修复与阶段证据（PR #59） | [archive/single-player/](archive/single-player/) |
| 特效资源恢复与洋红/不可见特效修复 | [archive/vfx-asset-recovery.md](archive/vfx-asset-recovery.md) |
| RaceMap 渲染各阶段的过程与评审 | [rendering/racemap/history.md](rendering/racemap/history.md) · [archive/rendering/](archive/rendering/) |

执行入口见 [harness.md](../harness.md)，提交、PR 和验证规则见 [CONTRIBUTING.md](../CONTRIBUTING.md)。
设计说明不代表功能已经实现；判断完成度时以目标分支代码和实际验证为准。
