# 文档索引

## 当前入口

| 需要了解 | 文档 |
|---|---|
| 系统职责、比赛流程、技能施法链路、参赛者身份与训练模式 | [architecture.md](architecture.md) |
| 房间名单、握手、配装与身份 helper | [session-helpers.md](session-helpers.md) |
| FishNet 权限、同步与场景要点 | [networking.md](networking.md) |
| 预测与回放的核心设计、技能 modifier 命令 | [prediction-design.md](prediction-design.md) |
| Unity MCP 启动、多编辑器连接、headless 测试与训练模式验证入口 | [unity-mcp.md](unity-mcp.md) |
| 术语表（比赛与 AI；技能与风险）/ 架构决策 | [../CONTEXT.md](../CONTEXT.md) · [../GLOSSARY.md](../GLOSSARY.md) · [adr/](adr/) |
| 单机模式（AI、离线流程）：现状、规格与事实 | [single-player/HANDOFF.md](single-player/HANDOFF.md) |
| 策划配表：Excel 工作簿、导入器、规范与筛查清单 | [design-tables/README.md](design-tables/README.md) |
| 技能特效：分层规范、流程、插槽、预算与档位；每个技能一篇专题 | [vfx/README.md](vfx/README.md) |
| 玩法深化：缘起、执念与反噬、单键施法与鼠标瞄准、W 推进、训练木桩（联机验证未做） | [gameplay/README.md](gameplay/README.md) |
| 缘起：每一对的设计；模块边界、配表扩展与禅足冰封的构筑；合入 dev 时还开着的项（精修特效分支的起点） | [gameplay/yuanqi.md](gameplay/yuanqi.md) · [gameplay/yuanqi-architecture.md](gameplay/yuanqi-architecture.md) · [gameplay/yuanqi-open-items.md](gameplay/yuanqi-open-items.md) |
| 训练模式验证：各场景的实测值与重跑方法（只覆盖本机 host） | [gameplay/practice-verification.md](gameplay/practice-verification.md) |
| 玩法改动的退役清单：删了什么、在哪个提交、哪些有意保留 | [gameplay/retired.md](gameplay/retired.md) |
| 渲染入口：改动前读什么、不变量、抓图—烘焙—对比流程 | [rendering/HARNESS.md](rendering/HARNESS.md) · [rendering/ARCHITECTURE.md](rendering/ARCHITECTURE.md) |
| 画质设置：选项与档位、运行时接口、未实现与占位清单 | [rendering/graphics-settings.md](rendering/graphics-settings.md) |
| 主菜单与房间流程：菜单里选技能 / 皮肤 / 地图、道场联机房间、开局与再来一局；选择场景和旧菜单的退役清单 | [ui/menu-integration.md](ui/menu-integration.md) · [ui/retired.md](ui/retired.md) |
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
