# 机制更新退役记录

玩法机制更新（PR #63 `feat/mechanics-update`，规则见 [玩法深化](README.md)）退役的代码、资产、配表和文档都记在这里：删了什么、为什么、在哪个提交。用户 2026-10-31 要求做 PR 中途 review 时把已退役的内容一次清干净；格式参考 VFX 重构的退役记录（`refactor/vfx-refactor` 分支的 `Docs/vfx/retired.md`）。

状态：

- **已删**：已从工程移除。
- **已改**：内容还在，但改成和现行规则一致（数值、注释、表注释）。
- **保留**：看起来像旧东西，但有读取方或是有意留下的，不能删。
- **待定**：要用户决定。

调查文档、修复记录和诊断探针不删，只加“已被取代”注（用户规则）。

## 1. 各步实现时已经删掉的（2026-10-26 至 2026-10-30）

这些在实现提交里就删了，列出来便于追溯。

| 内容 | 退役原因 | 提交 |
|---|---|---|
| 6 个 `Skill_*_Anti` 类、资产，`SkillDatabase.antiSkills`、`SkillAction.antiSkillId`、`ExecuteObservers` / `ExecuteLocal` 的 `isAnti` 参数、`BuddahHandControl.FireChargedProjectileBurstReplicated`、`SkillExecutor.PlayChargedBurstVisualsServer` | 统一反噬取代各技能的反噬变体（A4，[ADR 0007](../adr/0007-unified-backfire-replaces-per-skill-anti-variants.md)） | 1ca7c50 |
| 全局施法锁 `_castLockedUntil`、`SkillAction.castLockSeconds`、`SkillDatabase.GetCastLockSeconds`、`SkillConfigRepository.TryGetCastLock` | 去掉全局锁，改为施法串行（A3） | 1ca7c50 |
| 配表 `Skill.castLockSeconds`、`Skill.antiSkillId`、6 行 `_anti`，家族表与 `Push` 的反噬行和反噬列，`UI/UiTiming` 里 `SkillSlotUI` 的三行 | 同上（阶段 B） | 16121ed |
| `LocalSkillUiEvent.CastLockSeconds`、HUD 2 秒假锁 | 同上（A6） | 6c4abee |
| `ComboSkillInput`（类、prefab 组件、`SkillExecutor` 的订阅与 `ClearCombo`）、`Skill/ComboInput`、`Skill/ComboBinding`、`ComboToken`、AI 搓招列、`backlashSeverity`、`mildStopProbability` | 单键释放（A2）后 AI 也改为直接施法（A7） | ac3d5fe |
| W/↑ 推掌的绑定、`TryResolvePushHand` 的 W/↑ 分支、`Global/InputBinding` 两行 | W 改为前进键（A8） | d6fc63b1 |
| 起跑交接的继承 0.2 秒、混合 0.32 秒、屏蔽转向 0.15 秒 | 完全物理交接（A8）；机制保留，配表值改为 0 | a5dc8808 |

## 2. 2026-10-31 清理：代码

| 内容 | 状态 | 说明 | 提交 |
|---|---|---|---|
| `SkillSlotUI.TokenUI`、`tokens` 列表、`HideRetiredTokens`、`SetGraphicVisible` | 已删 | 搓招 token 图只为运行时隐藏而留；图本身也删了（第 3 节） | 56ee57a5 |
| `BuddahHandControl.FireProjectileBurstImmediate`、`FireChargedProjectileImmediate`、`GetProjectileBurstAnchorPosition` | 已删 | 只服务反噬变体的反向弹幕，没有调用方（在本分支之前就已无调用） | 56ee57a5 |
| `ProjectileBurstParameters`、`ProjectileBurstPlanner.GetSpawnPosition` 和它的测试 `BurstIsCenteredAndSpacingScalesAlongRequestedSideDirection` | 已删 | 同上；`SanitizeColliderSize` 还在用，保留 | 56ee57a5 |
| `BuddahMovement.launchInheritTime` / `launchBlendTime` 的代码默认值 0.2 / 0.32 | 已改为 0 | 预测关闭时的旧运动组件，无表时的兜底要和 `Movement` 表的 0 一致 | 56ee57a5 |
| `SkillExecutor.ApplyFreezeToOwner` 注释里的“AI 搓招按键” | 已改 | AI 搓招 A7 已删 | 56ee57a5 |
| `MatchSpawnManager._trainingDummyStartOffsetMeters` 的 tooltip 里的 0.2 秒继承 + 0.32 秒混合 | 已改 | A8 完全物理交接 | 56ee57a5 |
| `SoloMatchRules.SpawnTrainingDummies` 注释“phase B may move this into the ModeRule row” | 已改 | 阶段 B 已做完，开关仍在代码，数量和摆放在 `Match/ModeRule` 的 Practice 行 | 56ee57a5 |
| `HandPushProjectileRuntime` / `ChargedHandProjectileRuntime` 的 `allowSelfHit` 参数 | 保留 | 原来只有反噬弹幕传 true，现在恒为 false；它是两个弹道运行时的通用参数，删掉要改两个运行时的签名，收益小 | — |
| `RaceBodyIntroStateController` 和预测马达里的继承 / 混合交接机制 | 保留 | 用户只把时长改成 0（已定，2026-10-30），机制还能按表重新打开 | — |
| `SkillExecutor` 的 `IsAnti` 动画触发器、`Anti_Reflection_*` Feel 事件 | 保留 | 统一反噬的前摇表现在用（VFX 重构另有计划） | — |

## 3. 2026-10-31 清理：资产

| 内容 | 状态 | 说明 | 提交 |
|---|---|---|---|
| `SkillSlot_1/2/3.prefab` 的搓招 token 图（W、W白、UP、UP白及其副本，共 20 个） | 已删 | 运行时一直隐藏；用 Unity 的 prefab 接口删，不手改 YAML | 56ee57a5 |
| `UIprefap.prefab` 里给 `SkillSlot_3` 的 `tokens` 覆盖 11 条，和一条目标已不存在的 `ObsessionUIRoot.m_SortingLayer` 覆盖 | 已删 | 指向已删字段或已删对象；保存时 Unity 还写入了 A6 新增的 `ObsessionUI` / `SkillSlotUI` 布局字段（都是代码默认值） | 56ee57a5 |
| `SkillSlot_1/2/3.prefab` 的 `successHoldSeconds`、`fallbackCastLockSeconds`、`previewTimeoutSeconds` | 已删 | 字段 A6 已删，YAML 键残留 | 56ee57a5 |
| 6 个 `Skill_*.asset` 的 `castLockSeconds` | 已删 | 字段 A3 已删，YAML 键残留 | 56ee57a5 |
| `Buddah.prefab` 的 `BuddahMovement.launchInheritTime` / `launchBlendTime` 0.2 / 0.32 | 已改为 0 | 和 `Movement` 表一致 | 56ee57a5 |
| `Buddah.prefab` 的 NetworkBehaviour `_componentIndexCache` 第 7–15 项 | 已改 | 删 `ComboSkillInput` 后留了一个空号（8–16）；FishNet 运行时会重算，所以只是序列化噪音 | 56ee57a5 |
| `Assets/Data/SkillConfig/ProjectConfigSample.json`（连同文件夹 meta） | 已删 | 设计表之前的旧格式样例，无人引用，仍写着 `castLockSeconds`、`antiSkillId` | 56ee57a5 |
| `Buddah.prefab` 的 FishNet `EmptyNetworkBehaviour`（第 3 项） | 保留 | 从工程最初的提交就在，不是这次退役的东西；删了会改动 NetworkBehaviour 编号 | — |
| 神足通、缓流、神掌 SO 资产上的时长和冷却（10 / 12、10 / 12、冷却 18） | 待定 | 和表不一致（表是 3 / 5、6 / 8、12），运行时以表为准。按 [design-tables 决定 14](../design-tables/decisions.md)，SO 值只是无表时的兜底，设计表 README 把“已被表接管的重复字段”列为待删的遗留，而不是要同步的值。这里没有改数值，见第 6 节 | — |

## 4. 2026-10-31 清理：配表

改 xlsx 后用 `tablemodel.py` 写回，`gen_records.py` 重新生成记录类，再无窗口强制导入：0 错误，3 条已知的 `HudObsession` 场景对象路径警告；导入后的 CSV 镜像和写表脚本的输出逐字节一致。

| 内容 | 状态 | 说明 | 提交 |
|---|---|---|---|
| `Global/__enums__` 的 6 个 `SkillId` `*_anti` 值 | 已删 | 统一反噬后没有任何行或代码引用；[ADR 0007](../adr/0007-unified-backfire-replaces-per-skill-anti-variants.md) 当时决定保留，这次按用户“清理所有被退休的内容”删除 | 0982bc33 |
| `UI/Text` 的 12 个 `skill.*_anti.name / .desc` | 已删 | 同上，没有读取方 | 0982bc33 |
| `Skill/VfxRegistry` 的 `accel_anti_vfx`、`reverse_turn_trap_anti_vfx` | 已删 | 库中本来就没有条目 | 0982bc33 |
| `Skill/Obsession.repeatWindowSeconds` 注释“SkillExecutor 每槽记层数” | 已改 | 同招加价按技能 id 记层数（`SkillRepeatSurcharge`） | 0982bc33 |
| `Skill/Yuanqi.retriggerPolicy` 注释“YuanqiRegistry 读 SO.retriggerPolicy” | 已改 | 代码不按值分支，一次重叠只触发一次是 `YuanqiPairTable` 固定的 | 0982bc33 |
| `Skill/SkillPresentation.vfxDurationSeconds` 注释“slowtrap 的 11 = 区域 10 s + 1 s 余韵” | 已改 | 区域现在 6 秒，11 只是上限，区域结束时特效随之停止 | 0982bc33 |
| `Skill/Skill.skillId` 注释里“枚举里保留的 *_anti 值” | 已改 | 枚举值已删 | 0982bc33 |
| `Global/InputBinding.status` 注释与 `__meta__` 说明里的“W/↑ 推掌是废弃内容” | 已改 | A8 已删 | 0982bc33 |
| 指向本分支改过的文件的行号：`FeelEvent.usedBy`、`SkillConstant.source`（黑幕）、`AI/SkillOpportunity.source`、`AI/SkillTuning` 的 12 处注释、`AI/DrivingConstant.source`、`UI/HudObsession.flashFrequencyMin`、`UI/Text.context`（`RacerIdentity`、`MatchSpawnManager`、`PropertiesSelectorUI` 共 29 行）、`StructuralConstant.source`（`skill_slot_count`、`prediction_protocol_version`） | 已改 | 按基线 5f91e51 那一行的内容在 HEAD 里重新定位 | 0982bc33 |
| `Skill/FeelEvent` 的 `Anti_Reflection_*` 两行 | 保留 | 统一反噬的前摇表现在用 | — |
| `Movement.launchInheritSeconds` / `launchBlendSeconds` 两列 | 保留 | 值为 0，机制可调 | — |

## 5. 2026-10-31 清理：文档

调查文档、评审、实现记录只在顶部或相关段落加一条 `已被取代（2026-10-31）` 注，正文不动；现行参考文档改成现在的行为。

| 文档 | 处理 |
|---|---|
| `Docs/README.md` | AI 技能文档的索引说明、训练模式场景数 13 → 14；链接全部有效 |
| `Docs/design-tables/README.md`、`excel-spec.md` | 语法例子不再用搓招 `W,Up,W` 和 `combo_input_window_seconds` |
| `Docs/design-tables/inventory-skills.md`、`inventory-ui.md`、`inventory-movement.md`、`inventory-assets.md`、`structure-audit.md` | 开头的“不再适用”说明补上 A7（搓招表删除）、A8（W 前进、W/↑ 推掌删除、交接 0） |
| `Docs/single-player/design.md`、`ai-testing.md` | AI 逐键搓招改写为直接施法（A7） |
| `Docs/gameplay/yuanqi-architecture.md`、`Docs/prediction-design.md` | A7 已完成；屏蔽转向与继承 / 混合时长为 0 |
| `Docs/gameplay/README.md`、`Docs/single-player/practice-playtest.md` | 状态行补上 A8 和 14/14；试玩清单的“自动前进”标为不适用 |
| 历史记录：`ai-skill-design-review-2026-10-03.md`、`ai-skill-implementation-2026-10-03.md`、`ai-skill-implementation-review-2026-10-03.md`、`ai-skill-playtest.md`、`skill-acceptance.md`、`pr59-code-review-2026-10-04.md`、`optimization/p3-validation.md`、`optimization/audit-report.md`、`optimization/skill-presentation-validation.md`、`diagnostic-log-sampling-r8.md`、`vfx-asset-recovery.md`、`online-repair/acceptance.md`、`handoff-continuity/findings.md` | 各加一条“已被取代”注 |
| `GLOSSARY.md`、`CONTEXT.md`、`architecture.md`、`ai-skills.md`、`ai-personalities.md`、`HANDOFF.md`、`decisions.md`、ADR 0007 | 已经是现行描述或已标注，没改 |

## 6. 留给用户决定

- 神足通、缓流、神掌 SO 资产的时长和冷却和表不一致（第 3 节）。可选做法：把 SO 改成表值，或者按设计表 README 的遗留清单删除这些已被表接管的重复字段。两种都会改 SO，没有替用户定。
- `Docs/vfx/retired.md` 计划删除 `Anti_Reflection_*` Feel 事件和 `IsAnti` 前摇；那是 VFX 重构的事，这里保留。
