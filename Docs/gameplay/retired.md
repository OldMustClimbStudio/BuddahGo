# 机制更新退役记录

玩法机制更新（PR #63 `feat/mechanics-update`，规则见 [玩法深化](README.md)）退役的代码、资产、配表和文档都记在这里：删了什么、为什么、在哪个提交。用户 2026-10-31 要求做 PR 中途 review 时把已退役的内容一次清干净；格式参考 VFX 重构的退役记录（`refactor/vfx-refactor` 分支的 `Docs/vfx/retired.md`）。

状态：

- **已删**：已从工程移除。
- **已改**：内容还在，但改成和现行规则一致（数值、注释、表注释）。
- **保留**：看起来像旧东西，但有读取方或是有意留下的，不能删。
- **待定**：要用户决定。
- **已定**：用户已裁定（写明日期和裁定方式）。
- **已恢复**：曾经退役，后来按用户决定恢复（写明原因和提交）。

调查文档、修复记录和诊断探针不删，只加“已被取代”注（用户规则）。

> **2026-10-31 反噬变体恢复。** 用户推翻了“六种 `_anti` 变体由统一惩罚取代”：反噬改为统一惩罚 + 必定放出该技能自己的 `_anti` 变体，按 dev 原样恢复。下面凡是关于反噬变体的条目都已恢复，以第 8 节为准；其余退役内容（搓招、全局施法锁、W/↑ 推掌、旧起跑交接、HUD token 等）不变。

## 1. 各步实现时已经删掉的（2026-10-26 至 2026-10-30）

这些在实现提交里就删了，列出来便于追溯。

| 内容 | 退役原因 | 提交 |
|---|---|---|
| 6 个 `Skill_*_Anti` 类、资产，`SkillDatabase.antiSkills`、`SkillAction.antiSkillId`、`ExecuteObservers` / `ExecuteLocal` 的 `isAnti` 参数、`BuddahHandControl.FireChargedProjectileBurstReplicated`、`SkillExecutor.PlayChargedBurstVisualsServer` | 统一反噬取代各技能的反噬变体（A4，[ADR 0007](../adr/0007-unified-backfire-replaces-per-skill-anti-variants.md)） | 1ca7c50；**已恢复**（2026-10-31，第 8 节；`isAnti` 参数不恢复，`castLockSeconds` 不恢复） |
| 全局施法锁 `_castLockedUntil`、`SkillAction.castLockSeconds`、`SkillDatabase.GetCastLockSeconds`、`SkillConfigRepository.TryGetCastLock` | 去掉全局锁，改为施法串行（A3） | 1ca7c50 |
| 配表 `Skill.castLockSeconds`、`Skill.antiSkillId`、6 行 `_anti`，家族表与 `Push` 的反噬行和反噬列，`UI/UiTiming` 里 `SkillSlotUI` 的三行 | 同上（阶段 B） | 16121ed；反噬部分**已恢复**（2026-10-31，7a078eed），`castLockSeconds` 和 `UiTiming` 三行不恢复 |
| `LocalSkillUiEvent.CastLockSeconds`、HUD 2 秒假锁 | 同上（A6） | 6c4abee |
| `ComboSkillInput`（类、prefab 组件、`SkillExecutor` 的订阅与 `ClearCombo`）、`Skill/ComboInput`、`Skill/ComboBinding`、`ComboToken`、AI 搓招列、`backlashSeverity`、`mildStopProbability` | 单键释放（A2）后 AI 也改为直接施法（A7） | ac3d5fe |
| W/↑ 推掌的绑定、`TryResolvePushHand` 的 W/↑ 分支、`Global/InputBinding` 两行 | W 改为前进键（A8） | d6fc63b1 |
| 起跑交接的继承 0.2 秒、混合 0.32 秒、屏蔽转向 0.15 秒 | 完全物理交接（A8）；机制保留，配表值改为 0 | a5dc8808 |

## 2. 2026-10-31 清理：代码

| 内容 | 状态 | 说明 | 提交 |
|---|---|---|---|
| `SkillSlotUI.TokenUI`、`tokens` 列表、`HideRetiredTokens`、`SetGraphicVisible` | 已删 | 搓招 token 图只为运行时隐藏而留；图本身也删了（第 3 节） | 3cd7d5d1 |
| `BuddahHandControl.FireProjectileBurstImmediate`、`FireChargedProjectileImmediate`、`GetProjectileBurstAnchorPosition` | 已恢复 | 只服务反噬变体的反向弹幕，没有调用方（在本分支之前就已无调用） | 00472741；2026-10-31 回退（6f8fdb74），反噬神掌的反向弹幕要用 |
| `ProjectileBurstParameters`、`ProjectileBurstPlanner.GetSpawnPosition` 和它的测试 `BurstIsCenteredAndSpacingScalesAlongRequestedSideDirection` | 已恢复 | 同上；`SanitizeColliderSize` 还在用，保留 | 00472741；2026-10-31 回退（6f8fdb74），反噬神掌的反向弹幕要用 |
| `BuddahMovement.launchInheritTime` / `launchBlendTime` 的代码默认值 0.2 / 0.32 | 已改为 0 | 预测关闭时的旧运动组件，无表时的兜底要和 `Movement` 表的 0 一致 | 5fc675e7 |
| `SkillExecutor.ApplyFreezeToOwner` 注释里的“AI 搓招按键” | 已改 | AI 搓招 A7 已删 | f7f62017 |
| `MatchSpawnManager._trainingDummyStartOffsetMeters` 的 tooltip 里的 0.2 秒继承 + 0.32 秒混合 | 已改 | A8 完全物理交接 | f7f62017 |
| `SoloMatchRules.SpawnTrainingDummies` 注释“phase B may move this into the ModeRule row” | 已改 | 阶段 B 已做完，开关仍在代码，数量和摆放在 `Match/ModeRule` 的 Practice 行 | f7f62017 |
| `HandPushProjectileRuntime` / `ChargedHandProjectileRuntime` 的 `allowSelfHit` 参数 | 保留 | 原来只有反噬弹幕传 true，现在恒为 false；它是两个弹道运行时的通用参数，删掉要改两个运行时的签名，收益小 | — |
| `RaceBodyIntroStateController` 和预测马达里的继承 / 混合交接机制 | 保留 | 用户只把时长改成 0（已定，2026-10-30），机制还能按表重新打开 | — |
| `SkillExecutor` 的 `IsAnti` 动画触发器、`Anti_Reflection_*` Feel 事件 | 保留 | 统一反噬的前摇表现在用（VFX 重构另有计划） | — |

## 3. 2026-10-31 清理：资产

| 内容 | 状态 | 说明 | 提交 |
|---|---|---|---|
| `SkillSlot_1/2/3.prefab` 的搓招 token 图（W、W白、UP、UP白及其副本，共 20 个） | 已删 | 运行时一直隐藏；用 Unity 的 prefab 接口删，不手改 YAML | 3cd7d5d1 |
| `UIprefap.prefab` 里给 `SkillSlot_3` 的 `tokens` 覆盖 11 条，和一条目标已不存在的 `ObsessionUIRoot.m_SortingLayer` 覆盖 | 已删 | 指向已删字段或已删对象；保存时 Unity 还写入了 A6 新增的 `ObsessionUI` / `SkillSlotUI` 布局字段（都是代码默认值） | 3cd7d5d1 |
| `SkillSlot_1/2/3.prefab` 的 `successHoldSeconds`、`fallbackCastLockSeconds`、`previewTimeoutSeconds` | 已删 | 字段 A6 已删，YAML 键残留 | 3cd7d5d1 |
| 6 个 `Skill_*.asset` 的 `castLockSeconds` | 已删 | 字段 A3 已删，YAML 键残留 | dfacdeeb |
| `Buddah.prefab` 的 `BuddahMovement.launchInheritTime` / `launchBlendTime` 0.2 / 0.32 | 已改为 0 | 和 `Movement` 表一致 | 5fc675e7 |
| `Buddah.prefab` 的 NetworkBehaviour `_componentIndexCache` 第 7–15 项 | 已改 | 删 `ComboSkillInput` 后留了一个空号（8–16）；FishNet 运行时会重算，所以只是序列化噪音 | dfacdeeb |
| `Assets/Data/SkillConfig/ProjectConfigSample.json`（连同文件夹 meta） | 已删 | 设计表之前的旧格式样例，无人引用，仍写着 `castLockSeconds`、`antiSkillId` | 8e14bb94 |
| `Buddah.prefab` 的 FishNet `EmptyNetworkBehaviour`（第 3 项） | 保留 | 从工程最初的提交就在，不是这次退役的东西；删了会改动 NetworkBehaviour 编号 | — |
| 神足通、缓流、神掌 SO 资产上的时长和冷却（10 / 12、10 / 12、冷却 18） | 待定 | 和表不一致（表是 3 / 5、6 / 8、12），运行时以表为准。按 [design-tables 决定 14](../design-tables/decisions.md)，SO 值只是无表时的兜底，设计表 README 把“已被表接管的重复字段”列为待删的遗留，而不是要同步的值。这里没有改数值，见第 6 节 | — |

## 4. 2026-10-31 清理：配表

改 xlsx 后用 `tablemodel.py` 写回，`gen_records.py` 重新生成记录类，再无窗口强制导入：0 错误，3 条已知的 `HudObsession` 场景对象路径警告；导入后的 CSV 镜像和写表脚本的输出逐字节一致。

| 内容 | 状态 | 说明 | 提交 |
|---|---|---|---|
| `Global/__enums__` 的 6 个 `SkillId` `*_anti` 值 | 已恢复 | 统一反噬后没有读取方，见 4.1 | 6cf32706；2026-10-31 恢复（7a078eed），见第 8 节 |
| `UI/Text` 的 12 个 `skill.*_anti.name / .desc` | 已恢复 | 同上，见 4.1 | 6cf32706；2026-10-31 恢复（7a078eed），见第 8 节 |
| `Skill/VfxRegistry` 的 `accel_anti_vfx`、`reverse_turn_trap_anti_vfx` | 已恢复 | 库中本来就没有条目，见 4.1 | 6cf32706；2026-10-31 恢复（7a078eed），见第 8 节 |
| `Skill/Obsession.repeatWindowSeconds` 注释“SkillExecutor 每槽记层数” | 已改 | 同招加价按技能 id 记层数（`SkillRepeatSurcharge`） | bbda0a20 |
| `Skill/Yuanqi.retriggerPolicy` 注释“YuanqiRegistry 读 SO.retriggerPolicy” | 已改 | 代码不按值分支，一次重叠只触发一次是 `YuanqiPairTable` 固定的 | bbda0a20 |
| `Skill/SkillPresentation.vfxDurationSeconds` 注释“slowtrap 的 11 = 区域 10 s + 1 s 余韵” | 已改 | 区域现在 6 秒，11 只是上限，区域结束时特效随之停止 | bbda0a20 |
| `Skill/Skill.skillId` 注释里“枚举里保留的 *_anti 值” | 已改 | 枚举值已删 | 6cf32706；枚举值恢复后注释沿用现在的写法 |
| `Global/InputBinding.status` 注释与 `__meta__` 说明里的“W/↑ 推掌是废弃内容” | 已改 | A8 已删 | bbda0a20 |
| 指向本分支改过的文件的行号：`FeelEvent.usedBy`、`SkillConstant.source`（黑幕）、`AI/SkillOpportunity.source`、`AI/SkillTuning` 的 12 处注释、`AI/DrivingConstant.source`、`UI/HudObsession.flashFrequencyMin`、`UI/Text.context`（`RacerIdentity`、`MatchSpawnManager`、`PropertiesSelectorUI` 共 29 行）、`StructuralConstant.source`（`skill_slot_count`、`prediction_protocol_version`） | 已改 | 按基线 5f91e51 那一行的内容在 HEAD 里重新定位 | bbda0a20 |
| `Skill/FeelEvent` 的 `Anti_Reflection_*` 两行 | 保留 | 统一反噬的前摇表现在用 | — |
| `Movement.launchInheritSeconds` / `launchBlendSeconds` 两列 | 保留 | 值为 0，机制可调 | — |

### 4.1 `*_anti` 枚举值、文案和 VFX id 的删除（已被取代：2026-10-31 已恢复）

> **已被取代（2026-10-31）：** 同一天晚些时候用户决定恢复反噬变体（第 8 节），本节删除的枚举值、文案和 VFX id 都已恢复（7a078eed）。下面保留当时的记录。

**已定（2026-10-31）。** 用户答复“两种都可以”（保留或删除都要和 PR #68 对齐，原话：“既然都要对齐 那么就随意选择一个方式即可。但务必请写清楚”），由总控选择保留删除，不回退 6cf32706。阶段 B 原来按“建议（实现采用）”保留这批数据（[design-tables 决定 26](../design-tables/decisions.md)、[ADR 0007](../adr/0007-unified-backfire-replaces-per-skill-anti-variants.md)），这个说法由本节取代。

删除的原因：A4 用统一反噬取代了六个反噬变体，`Skill_*_Anti` 类、资产和表行已经在 1ca7c50、16121ed 删除；剩下这批枚举值、文案和 VFX id 没有任何行、代码或资产读取。

删除的内容（提交 6cf32706）：

| 表 | 删除的项 |
|---|---|
| `Global/__enums__` 的 `SkillId` | `acceleration_anti`=6、`slowtrap_anti`=7、`blackcurtain_anti`=8、`giant_anti`=9、`push_projectile_hands_anti`=10、`reverseturn_anti`=11 |
| `UI/Text` | `skill.acceleration_anti.name / .desc`、`skill.slowtrap_anti.name / .desc`、`skill.blackcurtain_anti.name / .desc`、`skill.giant_anti.name / .desc`、`skill.push_projectile_hands_anti.name / .desc`、`skill.reverseturn_anti.name / .desc`（12 个） |
| `Skill/VfxRegistry` | `accel_anti_vfx`、`reverse_turn_trap_anti_vfx`（库中本来没有条目） |

- **保留**：`Skill/FeelEvent` 的 `Anti_Reflection_Shared`、`Anti_Reflection_Local` 和动画触发器 `IsAnti`。统一反噬的 1 秒确认前摇播的就是它们（`SkillExecutor.PlayQueuedCastFeedbackObserversRpc`）。
- ~~**编号 6–11 不复用。**~~ 作废（2026-10-31）：6–11 已恢复给六个反噬变体，新技能照常从 12 开始编号。
- **恢复方法**：如果以后要恢复，单独回退 6cf32706，再按第 7 节的方式无窗口导表。

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
- 已被取代：`*_anti` 枚举值、文案和 VFX id 曾“保留删除（已定）”，同一天按用户决定恢复（第 8 节）。
- 已答复：反噬变体与惩罚的边界（第 8 节，已定 2026-10-31）。
- `Docs/vfx/retired.md` 计划删除 `Anti_Reflection_*` Feel 事件和 `IsAnti` 前摇；那是 VFX 重构的事，这里保留。

## 7. 和 PR #68 合并时怎么对齐

> **已执行（2026-11-03）。** PR #68 先以 c34d0d11 squash 进 dev，本分支随后合入 dev，按下面的原则处理：`_anti` 类、资产和三个反噬 VFX id 两边都保留；`cast` / `cast_anti` 通用前摇插槽接到 `PlayQueuedCastFeedbackObserversRpc`，反噬前摇取 `_anti` 行的 2 秒；旧特效路径（`PlayerAccelerationTrail`、`TimedVfxInstance`、`SkillVfxTag` / `SkillVfxFollowTarget` / `SkillVfxAttachmentSettings`、`SkillVfxReplicator` 的 `StopVfxLocal(id, linger)`、技能 SO 上的 `vfxLocalOffset / vfxLocalEuler / vfxStopPlayingBeforeEndSeconds / consumedVfxLingerSeconds / zoneEndVfxLingerSeconds`）随 dev 退役，缘起消耗和缓流区域结束改调插槽管线的 `StopSlotLocal` / `StopVfxAll`（`StopVfxAllObserversRpc` 名字不变，`PredictionProtocol` 的表现列表仍对）；配表按 CSV 镜像先合并再重建 `Skill.xlsx`，`castLockSeconds` 没有回来，`Skill/VfxRegistry` 按 dev 转为导出表；两边的 `Docs/gameplay/retired.md` 合成本文件（dev 的记录在最后一节）。验证见 PR #63 描述。

PR #68（`refactor/vfx-refactor` → dev）从 dev 9629c317 切出，那时 dev 还有六个反噬变体。它改过的文件里有下面这些引用了 `_anti` 技能、`antiSkillId` 或这批 id（2026-10-31 在 `origin/refactor/vfx-refactor` 4c81b6fa 上用 `git grep` 查，只列 #68 相对 merge-base 改过的文件）。#63 和 #68 谁后进 dev，谁处理冲突。

| 类别 | #68 里的文件 |
|---|---|
| 反噬变体的类和资产（#63 已在 1ca7c50 删除，#68 的 a6d81701 等提交改过它们） | `Assets/Scripts/Buddah/ComboSkill/Skill_*/Skill_*_Anti.cs`（6 个），`Assets/Skills_Assets/Skills/Skill_*_Anti.asset`（Acceleration、BlackCurtain、Giant、PushProjectileHands、ReverseTurn 5 个） |
| 引用反噬变体的代码 | `SkillExecutor.cs`（`ResolveAntiSkillId`、`isAnti`、`CastAntiVfxId`、`AntiCastConfirmDelaySeconds`）、`SkillAction.cs`、`Skill_Giant.cs`、`BuddahHandControl.cs`、`ChargedHandProjectileRuntime.cs`、`ProjectConfigRecords.Generated.cs`；编辑器工具 `Assets/Editor/VFX/Proto/SkillReleaseProto.cs`、`SkillVfxTimingSync.cs`、`VfxPackVendor.cs`；测试 `SkillVfxTests.cs`、`SkillVfxTimingSyncTests.cs` |
| 普通技能资产上的 `antiSkillId` 键 | `Skill_Acceleration / BlackCurtain / Giant / PushProjectileHands / ReverseTurn / SlowTrap.asset` |
| 配表与产物 | `_Export/Skill/VfxRegistry.csv`（新增 `palm_release_anti`、`palm_bullet_anti`、`cast_anti` 和 `antiOf` 列）、`SkillPresentation.csv`（6 行 `_anti`）、`Push.csv`（`charged_anti` 行），以及 `ProjectConfigDatabase_Main.asset`、`Resources/Config/ProjectConfigDatabase.json` |
| VFX 资产 | `Assets/Skills_Assets/SkillVfxDatabase.asset`（三个 `_anti` 条目），`VFX_Palm_Release_Anti.prefab`、`VFX_Palm_Bullet_Anti.prefab`、`VFX_Common_Cast_Anti.prefab`，`FeelPrefab/PalmAnti_Launch_Local.prefab` |
| 文档 | `Docs/vfx/README.md`、`anti-palette.md`、`asset-sources.md`、`push-projectile-hands.md`、`push-projectile-hands-implementation.md`、`retired.md`、`slots.md` |

处理原则：

- **（2026-10-31 改）保留 `_anti` 引用。** 反噬是统一惩罚 + 必定变体（[ADR 0007](../adr/0007-unified-backfire-replaces-per-skill-anti-variants.md) 修订段、第 8 节），#63 已按 dev 原样恢复六个变体，所以 #68 对 `Skill_*_Anti` 类和资产、`antiSkillId`、反噬弹幕和这些 id 的改动和引用都保留，按内容合并。差别只有两处：#63 的 `ExecuteObservers` / `ExecuteLocal` 没有 `isAnti` 参数（变体类自己知道是变体），#68 里依赖这个参数的地方改成变体类自己的分支；#63 的反噬不再换成变体去执行，而是先开始统一惩罚、再执行变体（`SkillExecutor.ExecuteBackfireServer` → `RunBackfireVariantServer`），#68 里 `ResolveCastVariant` 换技能的写法以 #63 为准。
- **`Anti_Reflection_*` 和 `IsAnti` 保留**，统一反噬的确认前摇在用。
- **#68 新增的三个反噬 VFX id**：变体恢复后都有播放方：`cast_anti`（反噬通用前摇）对应统一反噬的确认前摇，`palm_release_anti`、`palm_bullet_anti` 对应恢复的反噬神掌弹幕，合并时保留，由 VFX 侧接到 #63 的播放路径上。
- **配表冲突**：按 [design-tables README](../design-tables/README.md) 的做法，先合并 `_Export` 下的 CSV 镜像（#63 已恢复反噬行和列，两边的反噬行都保留，`castLockSeconds` 不回来），再按合并后的镜像构建 `Tools/design-tables/tablemodel.py` 的 `Sheet` / `Model`，用 `write_workbook_xlsx` 重建冲突的 xlsx（本分支合并 dev 时的 e91a977 就是这样做的），`gen_records.py` 重新生成记录类，最后无窗口强制导入（`DesignTableCli.ImportAndValidate -designForce`）。导入必须 0 错误，导入后镜像不应再变。
- **合并后验证**：无窗口 EditMode，加训练模式 harness（S5 反噬）。

## 8. 2026-10-31 反噬变体恢复

**已定（2026-10-31，用户）。** 用户原话：“我去 不对 反噬反效果变体不应该删除”、“应该保留”。用户选定：反噬时**统一惩罚 + 必定变体**，变体内容**按改版前 dev 上的原样恢复**。这推翻了 README §6.2 从 29af5cf 设计初稿起记的“六种 `_anti` 变体由统一惩罚取代（已定）”，也推翻了第 4.1 节的删除。只恢复反效果变体本身；搓招、全局施法锁、W/↑ 推掌等其他退役内容不恢复。

恢复的内容（以 dev a3315eaa / 16121ed^ 为准）：

| 内容 | 状态 | 说明 | 提交 |
|---|---|---|---|
| `ProjectileBurstParameters`、`ProjectileBurstPlanner.GetSpawnPosition`、弹幕锚点和两个 `Fire*Immediate` 方法及测试 | 已恢复 | 回退 00472741 | 6f8fdb74 |
| 配表：`Skill.antiSkillId` 与 6 行 `_anti`；`SkillEffectAcceleration / SlowTrap / BlackCurtain / Giant` 的反噬行和反噬专用列；`Push` 的 `charged_anti` 行与 `projectileCount`、`projectileSpacing`、`hitTurnTorqueImpulse`；`SkillPresentation`、`SkillSelectionRule`（Hidden）、`SkillConstant`、`FeelEvent`、`VfxRegistry` 的反噬行；`SkillId` 6–11；12 个文案 key | 已恢复 | 从 16121ed^ 的工作簿合并回来，值不改；`castLockSeconds`、搓招表、`UiTiming` 三行和 AI 严重度列不恢复。无窗口导表 0 错误 | 7a078eed |
| `BlackCurtainPresentation.TryPlay` 的 `isAnti`、`SkillPerceptionState.ApplyCurtain` 的 `backlash` | 已恢复 | 一念光明反噬只遮施法者自己；普通黑幕传 false，行为不变 | 69e80978 |
| 6 个 `Skill_*_Anti` 类与资产（原 GUID）、`SkillDatabase.antiSkills` 与资产列表、`SkillAction.antiSkillId`（六个普通资产上也恢复）、`GetAntiSkillId`、`SkillConfigRepository.TryGetAntiSkillId`、反噬行的主时长字段、`PushChargedAntiMap`、`Skill_Giant` 的默认反噬 id、反向弹幕 `FireChargedProjectileBurstReplicated` 及其表现 RPC | 已恢复 | 变体类沿用现在的 `ExecuteObservers` / `ExecuteLocal` 签名，不恢复 `isAnti` 参数；资产里的 `castLockSeconds` 键去掉 | 7a15e7e5 |
| 校验：`DesignTableDomainRules.SkillRowsAreConsistent`、`ProjectConfigValidator` 的 `antiSkillId` 检查、`SkillConfigCrossValidator` 的反噬资产、`antiSkillId` 漂移、“Anti resolution”报告和 `charged_anti` 映射 | 已恢复 | 提示文字改成“反噬不加执念” | 45eadb63 |
| 接线：`ExecuteBackfireServer` 先开始统一惩罚，再按 dev 的顺序解析变体（表 `antiSkillId` → SO `antiSkillId` → `<id>_anti`）并在服务器执行一次；`BackfireVariantObserversRpc`（追加在最后，预测协议 v2 未发布） | 新增 | 统一惩罚规则不变 | b688b5e5 |
| 测试：`BackfireVariantTests`（每个技能对应 dev 的变体类、变体不进选择、变体不再套变体、不加执念）；训练模式 S5 / S6 检查变体照常放出 | 新增 | 无窗口 EditMode 615/615；训练模式 14/14；单人赛 Normal 自然结算 | c369d584 |

用户答复（已定，2026-10-31）：

1. 反噬本身就是该技能的 `_anti` 变体；统一惩罚改为 5 秒持续时间惩罚（`Skill/Obsession.backfireLockSeconds` 10 → 5，8f375ac0）：三槽全锁、执念冻结、极速 ×0.85 和变体自带的减速相乘、结束时执念 ×0.8。
2. 变体按自己的时长结束，和惩罚互不影响。
3. 反噬判定后在前摇里被禁锢，变体照常放出，和惩罚同时开始。
4. 变体不进技能状态板，不能触发也不能参与缘起；本应触发缘起时反噬，缘起不触发，正在生效的那一招按普通版本跑完（训练模式 S14 覆盖）。
5. 复活时变体效果按普通技能效果的复位规则清掉，惩罚照旧持续。
6. AI 先保持统一 `backfireCost`，不加回按技能的严重度，试玩后再调。
7. “反作用”由变体取代。
8. 变体不另算冷却和执念。

## 9. 2026-11-06 缘起实装补齐（`feat/yuanqi`）

| 内容 | 状态 | 说明 | 提交 |
|---|---|---|---|
| 蓄力掌、反噬弹幕的尺寸直接等于体型倍率（`GetPushScaleMultiplier`，金身 ×4、反噬 ×0.3） | 已改 | 用户新定：普通 ×0.5、金身 ×2、反噬 ×0.5（`PalmSizeRule`）；出生点、普通推掌和准星死区仍用体型倍率 | 5997da94、988a15e7 |
| #1 金身巨掌冲量乘体型倍率（×4） | 已改 | 改乘掌倍率（金身 ×2），用户选 | 988a15e7 |
| `SkillPresentationRulesTests` 的“frost lock draws nothing itself”（2026-10-26 只留接口） | 已改 | 用户要求先补冰封占位，改为检查占位可开关 | 62dc6269 |
| S14 固定 0.9 秒后请求反噬金身 | 已改 | 改为按 `giant_anti` 行的 2 秒窗口算请求时刻 | bfa14498 |
| 冰封占位 `FrostPlaceholder`、`FrostGroundPlaceholder`、`FrozenIceShellPlaceholder` | 已删 | 正式插槽 `frost_lock_release` / `frost_lock_hit` 从同样的接口接上（[../vfx/retired.md](../vfx/retired.md) 第 6.6 节）；`SkillPresentationRulesTests` 那条改为检查冰封播的两个插槽 | 7ad81a9c 加，2a8734f3 删 |

## 代码审查清理的退役记录（dev，PR #68，2026-11-03）

> 合入 dev（c34d0d11）时从 dev 的同名文件并入；它记录的是特效以外的代码退休，原标题“已退休的玩法、网络、UI 与编辑器代码”。下面各节的“待删 [PR#63]”指等 PR #63 合并后再删的项。


特效相关的退休记录在 [../vfx/retired.md](../vfx/retired.md)。这里记录的是特效以外的代码：每一项写明原来做什么、为什么能删、在哪个提交删的、要等什么才能删。

状态有三种：

- **已删**：已经从工程里移除。
- **待删 [PR#63]**：确定没有用了，但所在文件在 PR #63（`feat/mechanics-update`）的改动范围里，等它合并后再删。
- **待定**：需要用户先做决定。

审查依据见 [../code-review/02-code-review.md](../code-review/02-code-review.md)，下面写的 A #n、B #n 是那份文档里删除表的编号。

### 1. 2026-11-03 代码审查清理（refactor/vfx-refactor，本地提交，未 push）

删除前的核实方法：先用符号扫描找出只在声明处出现过的名字；再用 `.cs.meta` 的 guid 在场景、prefab、asset 里搜引用；再搜 YAML 里的 `m_MethodName` / `functionName`；最后检查测试里的反射调用。删除的 RPC 会让所在 NetworkBehaviour 的 RPC 编号改变，所以客户端和服务器必须使用同一个构建。大厅已经用 `Application.version` 加 `PredictionProtocol` 做了版本校验，正常发包不会有问题。

| 内容 | 状态 | 原来做什么 / 为什么能删 | 提交 |
|---|---|---|---|
| `RoomStateManager.RequestReturnToRoomMenuKeepingSession`、`ReturnToRoomMenuKeepingSessionServerRpc`、无参 `ReturnToRoomMenuKeepingSessionServer()`、`PlayersSummaryText`、`RequireAllClientsReady`、SyncVar `_playersSummaryText`、`UpdatePlayersSummaryText`、`RoomRoster.BuildSummary`（A #1–#5） | 已删 | 由客户端发起"回房间菜单"的 RPC，而且没有校验发起方是不是房主；另外每次名单变化都会把一段摘要字符串同步给所有人。两者都没有调用方或读者。真正的回菜单流程走 `ResultDecisionManager` 调用的有参重载 | `4309977d` |
| `SteamLobbyManager.OnMemberJoined/OnMemberLeft/OnLobbyListRefreshed/OnDebugLog`、`PublishDebugLog`、`UpdateCurrentLobbySettings`；`GameNetworkManager.OnRemoteClientStateChanged`（A #6–#9） | 已删 | 供旧调试 UI 和旧大厅界面用的事件和日志转发，现在全工程没有订阅者 | `5d03b3d5` |
| 结算流程的死成员（A #10、#11、#19–#22、#24–#30）：`ResultPresentationTimelineBridge` 的 4 个事件、5 个属性和两个没接线的信号；`PlayerProgressReporter.EnterResultAreaServer` / `SyncResultAreaTeleportObserversRpc`；`RaceCompletionTracker`、`RaceFinishManager` 的只读属性和 `resultSceneName`；`ResolvedPropertySelectionCache` 的 MatchScene 与选择字典；`PropertiesSelectionManager.AllowDuplicateSkillSelections/GetFilledSkillLoadoutSlotCount`；`ResultDecisionManager`、`ResultAreaInteractionGate` 的属性；`RaceResultAreaManager` 的放置标记；`LocalFinishPresentationTimelineBridge` 的死属性和信号；`EndFieldSpawnPoint.debugLabel` | 已删 | 早期结算流程留下的状态和接口，只写不读，或者完全没有调用方。RaceMap 只接了 Reached / Complete 两个时间线信号；结果区传送现在只走 hidden 那条路径 | `eeb9cec9` |
| `IntroClientController.CompleteGoSequence`、`IntroPath.EvaluateRotation/GetTerminalPosition/GetTerminalForward`、`IntroSlot` 的三个属性和 `GetLaunchForward`、`SplineIntroPath.FindNearestT`、`IntroTimeUtility.HasReachedIntroStart`（A #13、#15–#18） | 已删 | 开场早期的姿态和时间辅助方法，没有调用方，也没有 override | `f6af7812` |
| `IntroSlot` 的序列化字段 `visualStyle`、`rootPositionOffset`、`rootEulerOffset` | 待定 | 读这三个字段的属性已经删了，现在没人读。RaceMap 里存着它们的值，删字段会在下次保存时丢掉这些数据（不影响功能）。要不要一起退休，请用户定 | — |
| `BuddahPredictionCommandBus` 的 Teleport / Modifier / Handoff 通道、`TryEnqueue*`、`Target_Enqueue*` RPC，`Events/Payloads/TeleportCmd.cs`、`ModifierCmd.cs`、`HandoffCmd.cs`；`BuddahPredictionEventChannel.TryDequeue/BuildPendingSummary/NextSequence/LastConsumedId`；`BuddahPredictionHandoffBridge.GetPredictionControlState/IsAuthoritativeLaunchHandoffConsumedOrActive`；`BuddahPredictionVisualRootBridge.GetVisualScaleMultiplier`；`BuddahPredictionBootstrap` 的三个属性（A #31–#35） | 已删 | 预测重构 Phase 2 的指令总线脚手架。teleport 和 handoff 实际走 motor 自己的队列，这三个 RPC 从来没有发送方。Impulse 通道和 A4 探针 `DebugA4FireTestImpulseToOwner` 保留 | `70764650` |
| `SelectionRuleRepository.HasAnyRules`、`LapProgress.HasStartedLap/TotalProgressPercent`（A #38、#40） | 已删 | 没人调用的便捷属性和方法 | `1298976f` |
| `TrackSplineRef.Progress01AtT`（A #42） | 已恢复 | 在 `1298976f` 里作为无人调用的方法删掉；PR #72 改的赛道进度测试用它把录制位置换算成新样条上的完成度，合并 dev 后恢复，不再是死代码 | 删 `1298976f`；恢复见提交 "fix(cleanup): restore Progress01AtT for the spline progress test" |
| `Network/Match/PlaceholderPlayerController.cs`、`Assets/Editor/MatchScenePlaceholderSetup.cs`（A #43） | 已删 | 最早的联机原型：一个占位玩家控制器，加一个生成最小比赛场景的菜单工具。它生成的 `MatchScene.unity` 和 `PlaceholderPlayer.prefab` 早就不在了；两个 guid 在资产里都是 0 引用。运行这个工具还会替换掉当前场景并改写 Build Settings | `b96cf4b3` |
| `SplineProgressTracker.IsClearlyWrongWay`、`DesignTableSet.IsExported(DesignSheet)`、`SkillReleaseProto` 原型表里的 `skillId`（B #4、#16、#18） | 已删 | 分别用于按 forwardDot 判断逆行、判断某张表是否导出、在原型表里记录技能 id；全仓都没有读取方 | `ace0680b` |
| `SceneFadeController.QueueFadeOutOnNextSceneLoad`；`SkillWallSelectionController` 的 `RebuildFromManager/RebuildWall/TryReplaceSlot/HandleItemInspectRequested/SelectedSkillIds/PlayRejectedSelectionFeedback` 和 `ResetView` 的参数；`SkillInspectController.RequestToggleInspect/CurrentItem`；`SkillInspectableItem.CreateInspectVisualInstance/SanitizeInstance/AllowInspect/SkillId` 以及字段 `visualRoot`、`inspectVisualPrefab`；`SkillWallItemView` 的 6 个属性和 3 个字段；`PropertyGroupView.PropertyKey`（B #19–#24） | 已删 | 旧"选技能墙"交互留下的入口：按名字重建墙、替换槽位、从墙上直接进入检视、复制检视模型。现在检视走 MenuInspect 输入，这些方法没有调用方，或者只互相调用。`技能绘马.prefab` 里两个字段的值会在下次保存时丢掉（不影响功能） | `18aa33ae` |

### 2. 确定没用、等 PR #63 合并后再删

| 内容 | 原来做什么 / 为什么能删 |
|---|---|
| `IntroSequenceManager.HandleTimelineIntroStartSignal/HandleTimelineGoSignal/HandleTimelineCountdownSignal/PlayTimelineIfAssigned`（A #12），以及只被它们调用的 `IntroClientController` 三个空 handler | 没接线的时间线信号 |
| `RaceBodyIntroStateController._predictionHandoffBridge`（A #14） | 只写不读。`IntroHandoffContinuityTests.cs:44` 用反射写它，要一起删掉 |
| `BuddahPredictedModifierCommandData.CreatePushGrace/CreateSteeringSuppression/CreateRoomBypass`、`BuddahPredictedModifierState.HasAnyActive`、`SkillConfigRepository.GetDescription`、`ObsessionFigure.DrainPerSecond/ExtraRecoveryPerSecond`（A #36、#37、#39、#41） | 没有调用方的工厂方法和属性 |
| `PlayerCamera.SetRuntimeOffset/DampenRuntimeOffset/DampenRuntimeFieldOfViewOffset` 及 `_runtimeOffset` 一组（B #1） | 0 调用方，`_runtimeOffset` 恒为零 |
| `SkillExecutor.RequestResetActiveSkillEffectsForRespawn` + `RequestResetActiveSkillEffectsServerRpc`（B #2）、`SkillLoadout.SetSlotServer`（B #3） | 0 调用方 |
| `BuddahRespawn` 两个没人读的参数（B #5） | 方法体不读 |
| `BuddahHandControl` 的非蓄力投射物路径 + `HandPushProjectileRuntime.cs` 整个文件（B #6、#7，约 470 行） | 唯一的调用方总是传 `useChargedProjectileRuntime = true`，这条路径不可达。`Docs/vfx/retired.md` §4 说它"还在用"，这句要一起改 |
| `AISkillRacerSnapshot` 的 4 个只写字段与 `AISkillWorld` 的写入（B #13、#14），`DesignTableDomainRules` 的两张旧表回退分支（B #15），`DesignSheet.SourcePath`（B #17） | 只写不读，或者是已不存在的表 |

### 3. 等用户决定

- URP 示例代码：`SharedAssets/Benchmark/BenchmarkShortcutManager.cs` 会在正式游戏里自启，`SharedAssets/Scripts/Editor/DirectionalLightmapperCorrector.cs` 会改写 RaceMap 的方向光照贴图。这两个文件随 URP 示例目录（refactor-plan P7）一起处理，见 [../code-review/03-assets.md](../code-review/03-assets.md)。
- `Legacy/ResultDecisionUI` 与 `ResultDecisionPanel`：面板从 2026-04-08 起一直没激活，联机投票 UI 从来不显示。这是 bug 还是有意关掉的，需要用户确认（02 B §5.2）。
- `Match/Lobby.defaultVisibility` 列：读进来以后没有任何地方用，要接到建房 UI 还是从表里退休这一列，需要用户定（02 A D1）。
