# 文档索引

| 需要了解 | 文档 |
|---|---|
| 系统职责、比赛流程、技能施法链路、参赛者身份与训练模式 | [architecture.md](architecture.md) |
| 房间名单、握手、配装与身份 helper | [session-helpers.md](session-helpers.md) |
| FishNet 权限、同步与场景要点 | [networking.md](networking.md) |
| 预测与回放的核心设计、技能 modifier 命令 | [prediction-design.md](prediction-design.md) |
| Unity MCP 启动、多编辑器连接、headless 测试与训练模式验证入口 | [unity-mcp.md](unity-mcp.md) |
| 代码评估与架构优化计划（执行入口） | [optimization/HANDOFF.md](optimization/HANDOFF.md) |
| 联机预测撕裂与开场交接修复（执行入口） | [online-repair/HANDOFF.md](online-repair/HANDOFF.md) |
| 单机模式设计与实现入口 | [single-player/HANDOFF.md](single-player/HANDOFF.md) |
| AI 技能人格规格与本次实现（玩法深化去掉全局施法锁后，“任何两个技能不会同时生效”已不成立；技能管线与五人格已在 A7 AI 适配后按新规则改写，实施验证与试玩清单是旧规则的记录） | [技能管线](single-player/ai-skills.md) · [五人格](single-player/ai-personalities.md) · [实施验证](single-player/ai-skill-implementation-2026-10-03.md) · [试玩清单](single-player/ai-skill-playtest.md) |
| 1–6 人开场路径、idle、开场玩家标识与联机后续 | [single-player/intro-presentation-2026-10-03.md](single-player/intro-presentation-2026-10-03.md) |
| PR #59 代码复核与四轮清理记录 | [single-player/pr59-code-review-2026-10-04.md](single-player/pr59-code-review-2026-10-04.md) |
| 特效资源恢复与洋红/不可见特效修复 | [vfx-asset-recovery.md](vfx-asset-recovery.md) |
| 术语表（比赛与 AI；技能与风险）/ 架构决策 | [../CONTEXT.md](../CONTEXT.md) · [../GLOSSARY.md](../GLOSSARY.md) · [adr/](adr/) |
| 开场交接切换的本地卡顿修复（执行入口） | [handoff-continuity/HANDOFF.md](handoff-continuity/HANDOFF.md) |
| 策划配表系统：Excel 工作簿、导入器、分层导入、规范与筛查清单 | [design-tables/README.md](design-tables/README.md) |
| 玩法深化：缘起、执念与反噬、单键施法与鼠标瞄准、训练木桩（阶段 A/B 和 A7 AI 适配已实现；联机验证和用户试玩未做） | [gameplay/README.md](gameplay/README.md) |
| 缘起技能设计：每一对缘起的内容 | [gameplay/yuanqi.md](gameplay/yuanqi.md) |
| 缘起架构设计：模块边界、配表扩展、禅足冰封的构筑（已实现） | [gameplay/yuanqi-architecture.md](gameplay/yuanqi-architecture.md) |
| 训练模式验证记录：14 个场景的实测值与重跑方法（2026-10-26 起，2026-10-30 A8 加了 S13；只覆盖本机 host） | [gameplay/practice-verification.md](gameplay/practice-verification.md) |
| RaceMap 渲染：日落光照、手写天空、平面反射（原 SSR）、阴影距离、`BuddahGo/Lit`、复调清单与编辑器陷阱 | [rendering/racemap/README.md](rendering/racemap/README.md) |
| 渲染 shader 开源库调研与效果对比样板间（样板间工程仅在本地） | [rendering/shader-repo-survey.md](rendering/shader-repo-survey.md) · [rendering/showcase/README.md](rendering/showcase/README.md) |
| 画质设置：选项与档位规格（配表 `Graphics.xlsx`）、运行时模型与接口、菜单接入点、未实现与占位清单 | [rendering/graphics-settings.md](rendering/graphics-settings.md) |

执行入口见 [harness.md](../harness.md)，提交、PR 和验证规则见 [CONTRIBUTING.md](../CONTRIBUTING.md)。
旧阶段合同、详细设计原稿和复盘过程可从 Git 历史查阅，例如 `git log -- Docs/prediction-refactor-plan/`。
设计说明不代表功能已经实现；判断完成度时以目标分支代码和实际验证为准。
