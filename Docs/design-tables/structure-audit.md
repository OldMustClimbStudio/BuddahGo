# 配表结构缺陷盘点与代码考古

日期 2026-10-13。起因：`acceleration` 的持续时间在 `Skill/SkillEffectParam`，冷却与锁定在 `Skill/SkillBalance`，同一个技能的时间参数分在两张表。本文盘点工作簿里所有"同一概念被拆散、或不该在一起的放在一起"的问题，追溯每一处在代码史里的来源，并给出重组建议。基线工作簿是按"实际生效值 + 现有代码结构"生成的，所以代码里的历史分层原样进了表；本文盘点；2026-10-13 拍板后按第三节实施，处理结果见第五节。

> 2026-10-26 核对（代码 0dbc551）：机制更新阶段 B 之后，下列 2026-10-13 的说法不再成立，原文保留作记录。反噬变体退役（[decisions](decisions.md) 26）：A2 与第五节 A2 的"普通与反噬各一行"、A3 与第五节 A3 的 `basic / charged / charged_anti` 及"另两行映射到两个神掌 SO"、A5 的 `blackcurtain_anti` 行、A6 与第五节 A6 的反噬行 obsessionGain 规则、E1 的"含反噬行"、A1 与第五节 A1 的"锁定"（`castLockSeconds` 已删）、第三节里的"锁定 / 反噬 id"都已删除：家族表每个技能一行，`Push` 只剩 basic / charged（charged 映射到 Skill_PushProjectileHands），`Skill` 没有 `_anti` 行，也没有 `antiSkillId` / `castLockSeconds` 列（`SkillId` 枚举里的 6 个 anti 值保留）。A7：玩家搓招退役（单键释放），HUD 的按键 token 图已隐藏，不再需要由表生成；`ComboBinding` / `ComboInput` 已随玩法 A7（AI 适配，2026-10-28，ac3d5fe）删除，E1 与第三节里放搓招两键的 `ComboBinding` / `Skill/ComboInput` 也随之不存在。A8、C1 仍是代码待办。新增的缘起表沿用第三节"一概念一表"：`Skill/Yuanqi` 宽表 + `YuanqiEffect<对>` 家族表。

## 一、数据分层的形成史（git 考古）

| 时间 | 提交 | 发生了什么 | 留下的结构 |
|---|---|---|---|
| 2026-02-12 ~ 02-27 | e5cf3dd、7cda2d0、819f61d、428fb1a | 推掌（BuddahHandControl）、技能 SO（SkillAction + SkillDatabase）、执念（ObsessionFigure）、圈数（LapProgress）作为**角色 prefab 上的组件**诞生 | 每个数值都是组件 `[SerializeField]`，技能的冷却/锁定/执念增量写在 SO 基类，效果数值写在各技能子类 |
| 03-14 ~ 03-23 | cb300ff、8f2ff43 | 一念光明、神足通等技能按功能逐个加 VFX 与效果字段 | 技能 id 逐个起名：`anti_blackcurtain`（03-14）与后来的 `*_anti` 两种写法并存 |
| 03-25 ~ 03-29 | 2c58fc1、04d09b1、00beca6 | 房间倒计时（RoomStateManager）、属性选择阶段时长（PropertiesSelectionManager）、完赛倒计时与圈数（RaceFinishManager）、逆行阈值（RaceCompletionTracker）各自作为**场景/预制体字段**出现 | 比赛流程的时长分散在 4 个 manager 上，圈数同时在 RaceFinishManager（场景）和 LapProgress（角色 prefab）里 |
| **03-31** | **6d99ca2 "add project config database workflow"** | 引入 `ProjectConfigDatabase`：SkillDefinition / SkillBalance / SkillEffectParam / SkillSelectionRule / DefaultLoadout / GlobalRule 六类记录。**只接通了选择界面需要的身份信息和执行器需要的冷却/锁定/执念/反噬**；SkillEffectParam 只放了 4 行示例（id 还是旧的 `slow_trap`），没有读取方；GlobalRule 预留了圈数、倒计时、完赛窗口、追赶、执念恢复 6 个键，没有读取方 | **"平衡在配置、效果在 SO"的分裂就是这次半途而废的迁移**。作者把"配装/选择系统要用的"搬进了配置，把"执行技能的子类自己用的"留在了 SO |
| 04-06、04-15 | 215e280、2c63bc5 | 起跑交接字段加在旧 BuddahMovement 上；预测 V2 重写把运动参数复制到 BuddahPredictedMotorConfig | 运动参数两份；起跑时序留在已禁用的旧组件上 |
| 04-08 | f2139c0 | HUD 图标库 SkillUiSpriteLibrary 按 skillId 映射 | 与配置里的 `iconKey` 并存，后者从未被解析 |
| 09-29 ~ 09-30 | 8f02f68 P1-5、dc3bcda P2-1、694daed P3-4 | 清理未用 API、把圈数常量收进 RaceRules、加 SO 与配置的**漂移校验**（D5：不改数据） | 迁移没有完成，而是加了一个"报告漂移"的工具；如来神掌 18/15 对 12/10 就是这时被记录下来的 |
| 10-01 | 73305f8、354f10a | Solo 契约 IMatchRules（只放布尔开关，不放时长）；AI 驾驶档位 ScriptableObject | 模式规则和模式时长分家；AI 第一代数据（asset） |
| 10-03 | 559d57c、5afe638 | 推力规划器 + `Tools/ai/difficulty-*.json` 生成档位资产；技能人格 `SkillPersonalities.json` + 决策代码里的谓词常量 | AI 第二、三代数据（JSON、硬编码）两天内叠加 |
| 10-13 | 本分支 | 工作簿按上述结构逐一生成 | 分层原样进表 |

结论：**不是一次设计出来的，而是五轮功能开发各自就近放数据，03-31 的配置化只做了一半，09-30 的清理选择了"报告漂移"而不是"完成迁移"。**

## 二、问题清单

编号、现在在表里的样子、代码来源、建议。工作量：小 = 改表 + 重跑 `gen_records.py` + 改一两处读取；中 = 还要改记录类消费逻辑；大 = 涉及多个系统。

### A. 技能域

| # | 问题 | 表现 | 来源 | 建议 | 工作量 |
|---|---|---|---|---|---|
| A1 | **技能时间参数三分** | 冷却/锁定/前摇在 `SkillBalance`，持续时间、定身时长、buff 时长在 `SkillEffectParam`，VFX 时长与 FOV 时长在 `SkillPresentation` | SO 基类（02-18）放冷却，子类（03-23）放持续，6d99ca2 只搬了基类字段 | 合并为一张宽表 `Skill`：身份 + 冷却/锁定/前摇/执念/反噬 + `effectDurationSeconds` 等通用效果列；家族专有参数放 `SkillEffect_<家族>` 强类型表 | 中 |
| A2 | **SkillEffectParam 是键值表** | `paramKey/paramValue` 全是字符串，拼错 key 静默无效，没有类型与范围校验，单元格里混着 `60,90,20` 向量和 `true` | 6d99ca2 为"将来任意技能"设计的通用桶 | 改为每个技能家族一张强类型 sheet（Acceleration、SlowTrap、BlackCurtain、Giant、ProjectileHands、ReverseTurn，各含 normal/anti 两行），列即字段，可加 `#range` | 中 |
| A3 | **推掌数值两处** | 普通推掌 11 个字段在 `HandPush`，蓄力/弹幕推掌在 `SkillEffectParam` 的 ProjectileHands 家族，hitbox 尺寸还要去 Cube.prefab 看 | 推掌（02-12）早于技能系统；如来神掌（03-23）把蓄力模式钉在 BuddahHandControl 上 | `HandPush` 改名 `Push`，分 `basic / charged / chargedAnti` 三行，同一列集（冲量、冷却、偏移、碰撞盒、生命期） | 中 |
| A4 | **SkillDefinition 一半是死列** | `behaviorType`、`tags`、`isSelectable` 无人读；`iconKey` 从未解析；`isSelectable` 与 `SkillSelectionRule.poolType` 表达同一件事 | 6d99ca2 同时设计了两套可选机制，只有 poolType 接上 | 删 `behaviorType/tags/isSelectable`；`iconKey` 删除或让 `UI/SkillIcon` 以它为键（二选一） | 小 |
| A5 | **技能 id 命名不统一** | `anti_blackcurtain` 对 `*_anti`；`slowtrap` 对 `icon_slow_trap`、旧 JSON 的 `slow_trap`；贴图叫"逆姻缘"显示名叫"逆因缘"；`giant_anti` 显示名多一个空格 | id 按功能提交逐个起名（03-14、03-23、03-31），无注册表 | 统一为 `<skill>_anti`；一次性改 MAIN、SO、JSON、人格表、图标文件名；在 `__enums__` 增加 `SkillId` 作为唯一名单 | 小（改名）+ 需同步 AI 人格与 SO 资源名 |
| A6 | **反噬行的 obsessionGain 无效** | 表里 `anti_blackcurtain` 的 100 看起来是有效数值，实际执行器按请求技能取值 | SkillExecutor 02-23 的反噬实现按原技能加执念 | 要么删掉反噬行的该列（置 doc），要么改执行器语义；需拍板 | 小 |
| A7 | **ComboBinding 绑槽位不绑技能** | 搓招序列在 `ComboBinding(slotIndex)`，技能在 `DefaultLoadout` / 人格配装，两者无关联；HUD prefab 还另存一份 token | 搓招（02-18）设计为"第 N 槽固定第 N 套招"，与技能无关 | 维持绑槽位（设计如此），但把 HUD token 改为由表生成（已列入待办） | 小 |
| A8 | **执念参数在 Skill 工作簿但组件在角色 prefab** | `Obsession` 是全局规则，却因组件在 Buddah.prefab 上而每个角色各持一份 | ObsessionFigure（02-19）作为角色组件实现 | 表位置可留在 Skill（它决定反噬），组件层面应改为读全局单例；表不用动 | 无（代码待办） |
| A9 | **SkillPresentation 与效果时长隐式耦合** | `vfxDurationSeconds=0` 表示跟随效果时长，slowtrap 的 11 要手工比区域 10 多 1 秒 | 各技能子类自行约定 | 若做 A1 合并，VFX 时长改为相对值列 `vfxExtraSeconds` | 小 |

### B. 运动与赛道域

| # | 问题 | 表现 | 来源 | 建议 | 工作量 |
|---|---|---|---|---|---|
| B1 | **同一组件拆成三张表** | `MovementBase`、`PushReaction`、`LaunchHandoff` 的字段多数都在 `BuddahPredictedMotorConfig` 上，起跑时序还要从旧 `BuddahMovement` 读 | V2 重写（04-15）复制旧字段，起跑时序（04-06）留在旧组件 | 合并为 `Movement` 一张（运动 + 受击 + 起跑），`PushReaction` 按来源分行的设想目前没有数据支撑，先去掉 | 小 |
| B2 | **圈数两处、赛道规则在两本工作簿** | `Movement/LapRule.lapsToFinish` 与 `Match/Track.lapsToFinish` 都存在；`LapRule`、`WrongWayRule` 以 trackId 为键却放在 Movement 本 | LapProgress/RaceCompletionTracker 是角色组件（02-27、03-29）却存赛道常量；RaceFinishManager 场景字段（03-29）；RaceRules 常量（09-30）只统一了兜底 | `LapRule`、`WrongWayRule` 移到 `Match` 本并入 `Track`（或作为 Track 子表），`lapsToFinish` 只留一处；组件改为按 trackId 读 | 小 |
| B3 | **handoffLeadSeconds 两本工作簿各一份** | `Match/Intro.handoffLeadSeconds` 与 `Movement/LaunchHandoff.handoffLeadSeconds` 同义 | IntroSequenceManager（04-06）与 RaceBodyIntroStateController 各自序列化 | 只留 `Match/Intro` 一处，组件两边都读它 | 小 |
| B4 | **RespawnRule 按三种传送来源分行，但数值只有一套** | 三行完全相同，flags 列代码写死 | 传送来源枚举是 V2 的工程结构，数值在 BuddahRespawn（02 月） | 合并为一行；flags 列转 doc 或删除 | 小 |
| B5 | **ModifierRule 是说明不是数据** | `stackPolicy`、`minScaleMultiplier` 无读取方 | 叠加规则硬编码在 Events/Resolver | 移到 `presentationOnly` 或删除，避免策划误以为可调 | 小 |

### C. 比赛流程域

| # | 问题 | 表现 | 来源 | 建议 | 工作量 |
|---|---|---|---|---|---|
| C1 | **ModeRule 是四个 manager 的时长 + 一组开关的拼盘** | pregameCountdown（RoomStateManager prefab）、selectionStage（PropertiesSelectionManager 场景）、postFirstFinish（RaceFinishManager）、resultDecision（ResultDecisionManager）各来一处，再加 IMatchRules 的 16 个开关与 aiCount | 四个 manager 03-25~03-29 各自序列化；IMatchRules（10-01）只放布尔 | 结构本身是对的（按模式一行），问题在消费方分散；保持表不变，后续让这四个 manager 都只读 ModeRule 并删掉自己的序列化字段 | 无（代码待办） |
| C2 | **占位阶段进了表** | `SelectionStage/SelectionOption` 的地图与皮肤三选项是占位，结算结果无人读 | 属性选择 3D 流程（03-29）留的占位 | 标 doc 或删除 map/skin 行，待真正有地图/皮肤再加 | 小 |
| C3 | **Lobby 与 Session 超时混在一张** | 大厅默认值与连接超时同一行 | SteamLobbyManager 与 SessionLauncher 本是两处 | 可接受；或拆 `Session` 单行表 | 小/不动 |
| C4 | **RacerNaming 是键值桶** | 5 条互不相关的键 | 命名散在 RacerIdentity、MatchSpawnManager、SoloSetupPanel | 并入 `ModeRule`（默认名、前缀）或 `UI/Text`，删掉此表 | 小 |

### D. AI 域

| # | 问题 | 表现 | 来源 | 建议 | 工作量 |
|---|---|---|---|---|---|
| D1 | **同一难度分在两张表** | `DrivingTier` 与 `SkillDifficulty` 都以 Easy/Normal/Hard 为键 | 驾驶档位资产（10-01/10-03）与技能人格 JSON（10-03）是两条工作线 | 保留两张（驾驶与技能确实是两套子系统），但 `ImpairedReactionTicks` 一列两用应拆成驾驶/技能各一列 | 小 |
| D2 | **DrivingTier 字段 PascalCase，其余 camelCase** | 唯一不符合命名规范的表 | 直接沿用 AIDifficultyProfile 字段名 | 统一改 camelCase，同时改 AIDifficultyProfile 字段（或在覆盖时映射） | 小 |
| D3 | **三代常量三张表** | `DrivingGlobal`、`SkillGlobal` 是键值桶，`SkillOpportunity` 是按技能的稀疏宽表（多数格子空） | 规划器常量、JSON Tuning、谓词字面量三代叠加 | `SkillGlobal` 的 Tuning 部分改成单行宽表（列=字段）；谓词常量并入 `SkillOpportunity` 已做；`DrivingGlobal` 中 `[Eng]` 项移到 presentationOnly | 小 |
| D4 | **人格权重与配装脱节** | `PersonalitySkillWeight` 30 行里 15 行是不在配装内的死权重 | JSON 用按枚举索引的 6 元数组 | 改为 `Personality` 表内 `slot0Weight/slot1Weight/slot2Weight` 三列，删子表 | 中 |
| D5 | **Roster 隐含顺序** | 人格分配靠 JSON 顺序，表里只是把它显式化 | MatchSpawnManager 按 index 取 | 已显式化，可接受 | 无 |

### E. 全局与文案

| # | 问题 | 表现 | 来源 | 建议 | 工作量 |
|---|---|---|---|---|---|
| E1 | **GlobalRule 是跨域杂物桶** | `combo_input_window_seconds`、`combo_max_buffer` 属搓招，`cast_confirm_delay` 属技能，`target_frame_rate` 属工程 | 6d99ca2 的 GlobalRule 是唯一"无类型"容器，后续顺手往里放 | 搓招两键并入 `ComboBinding`（加一行全局或单列），前摇并入 `Skill` 宽表，帧率留 Global；GlobalRule 只放真正跨域的开关 | 小 |
| E2 | **文案 key 三种来源** | 技能名/人格名/默认玩家名是定稿 key，`ui.<class>.<slug>` 是自动 key，场景静态标签是 `ui.static.*` | 文案本来就散在三类地方 | 策划定稿 key 后统一替换；表结构不变 | 中（人工） |
| E3 | **枚举列以字符串存储** | 记录类里 enum 列是 `string`，强类型靠 `__enums__` 校验 | 为避免生成大量 C# 枚举 | 可接受；若要强类型，让 `gen_records.py` 同时生成枚举 | 小 |

## 三、建议的目标结构（v2，待拍板）

```text
Skill.xlsx
  Skill                 身份 + 冷却/锁定/前摇/执念增量/反噬 id + 通用效果时长（取代 SkillDefinition + SkillBalance + SkillEffectParam 的通用部分）
  SkillEffect_Acceleration / _SlowTrap / _BlackCurtain / _Giant / _ProjectileHands / _ReverseTurn   家族专有参数，normal 与 anti 各一行
  Push                  basic / charged / chargedAnti 三行（取代 HandPush + 神掌家族里的弹道参数）
  Obsession、ComboBinding（含窗口与缓冲两列）、SkillSelectionRule、DefaultLoadout、SkillPresentation（表现，只校验）
Movement.xlsx
  Movement              运动 + 受击 + 起跑交接（取代 MovementBase + PushReaction + LaunchHandoff）
  Respawn               单行
  （LapRule / WrongWayRule 迁到 Match）
AI.xlsx
  DrivingTier（camelCase）、SkillDifficulty（拆 impaired 两列）、Personality（含三槽权重）、PersonalitySituationBias、SkillOpportunity、SkillTuning（单行宽表）、Roster
Match.xlsx
  ModeRule（并入 RacerNaming）、Track（并入 lapsToFinish、LapRule、WrongWayRule）、TrackSpawn、TrackCheckpoint、TrackResultAnchor、Lobby、Intro（唯一的 handoffLeadSeconds）、SelectionStage/Option（去占位）
UI.xlsx  不变
Global.xlsx  GlobalRule 只留 target_frame_rate 等真正全局项
```

重组的代价：改表头 → `gen_records.py` → 调整 `DesignTableOverlay` 调用处的列名映射（已接通的消费方大多按同名覆盖，改列名即改映射串）→ `build_tables.py` 同步 → 重跑全量测试。预计一个工作日内可完成，不碰 RPC 与预测逻辑。

## 四、根因总结

1. **数据跟着组件走**：每个功能把数值放在自己组件的 `[SerializeField]` 上，组件在角色 prefab、场景、SO、JSON 四种宿主里，表就跟着分成四种粒度。
2. **配置化只做了一半**：03-31 搬了"别的系统要读的"字段，没搬"自己才读的"字段；预留的键和键值桶成了后来者的默认落脚点。
3. **重写时复制而不是迁移**：预测 V2、Solo 契约、AI 三代数据都在旧结构旁边新建，旧结构没有退役。
4. **清理阶段选择了"校验漂移"**：09-30 的 P3-4 用工具报告不一致，没有消灭不一致。
5. **没有 id 注册表**：技能 id、文案 key、tag 名都是提交时现起的名，所以 `anti_blackcurtain`、`slow_trap`、`逆姻缘` 这类不一致一直留到现在。

## 五、处理结果（2026-10-13）

拍板：按 v2 全部重组；A6 保持现行语义、反噬行清空；A5 改名并加 `SkillId` 枚举；逆转技能中文定为「逆姻缘」（decisions 16–21）。提交：技能 7ae8ea1、运动 67ffe66、AI c51d32f、比赛 4830192；每次提交都经过无头 `DesignTableTests` 与 `ImportAndValidate -designForce`。

| # | 处理 | 说明 |
|---|---|---|
| A1 | 已做 | `Skill` 一张表：身份 + 冷却/锁定/前摇 + 执念 + `effectDurationSeconds`（按技能类写入 SO 主时长字段） |
| A2 | 已做 | `SkillEffectAcceleration / SlowTrap / BlackCurtain / Giant` 强类型表，普通与反噬各一行；ReverseTurn 与 ProjectileHands 无家族表（只剩主时长与弹道，分别在 Skill 与 Push） |
| A3 | 已做 | `Push`：basic / charged / charged_anti 共享列集；basic 行显式映射到 BuddahHandControl，另两行显式映射到两个神掌 SO；hitbox 尺寸进 SkillConstant |
| A4 | 已做 | behaviorType / iconKey / isSelectable / tags 删除；图标仍按 skillId 走 SkillUiSpriteLibrary / UI/SkillIcon |
| A5 | 已做 | `anti_blackcurtain` → `blackcurtain_anti`（表、文案 key、SO 资产）；`__enums__` 增加 `SkillId`，`Skill.skillId` 类型 `enum:SkillId` |
| A6 | 已做 | 反噬行 obsessionGain 留空，非 0 报 Warning；执行器语义不变 |
| A7 / A8 | 未动 | 代码待办（HUD token 生成、ObsessionFigure 全局化），表不变 |
| A9 | 未动 | SkillPresentation 是 presentationOnly，只改了 comment 说明 0 = 跟随 Skill.effectDurationSeconds |
| B1 | 已做 | `Movement` 一张表；impulseMultiplier 删除；三个无读取方的起跑常量转 doc 列 |
| B2 | 已做 | LapRule 并入 `Match/Track`（圈数唯一来源），WrongWayRule → `Match/TrackWrongWay`；LapProgress 改为显式映射，顺带接通 v1 从未匹配到字段的 maxProgressJumpWithoutCheckpoint01 |
| B3 | 已做 | handoffLeadSeconds 只在 `Match/Intro`，RaceBodyIntroStateController 改读 Intro |
| B4 | 已做 | `Respawn` 单行，flags 转 doc |
| B5 | 已做 | ModifierRule 转 presentationOnly |
| C1 | 未动 | 表结构正确；四个 manager 删除自身序列化字段是代码待办 |
| C2 | 已做 | SelectionStage / SelectionOption 的 map / skin 行以 `##` 屏蔽，代码兜底 |
| C3 | 未动 | 可接受 |
| C4 | 已做 | RacerNaming 删除；defaultDifficulty 进 ModeRule 并由 SoloSetupPanel 读取；命名前缀 key 本就在 UI/Text |
| D1 | 已做 | SkillDifficulty 拆出 impairedDrivingReactionTicks（AIRacerDriver），impairedReactionTicks 只供 AISkillCaster；JSON 兜底同步加字段 |
| D2 | 已做 | DrivingTier / SkillDifficulty 表头 camelCase；覆盖按名忽略大小写，代码字段不改名；SkillDifficulty 的重复 Difficulty 列删除 |
| D3 | 已做 | `SkillTuning` 单行宽表（两个未接键转 doc 列）；`DrivingTuning` 单行（4 个有读取方的键），其余 [Eng] 项进 presentationOnly 的 `DrivingConstant` |
| D4 | 已做 | 权重进 Personality 的 slot0Weight..slot2Weight，PersonalitySkillWeight 删除 |
| D5 | 不变 | 已显式化 |
| E1 | 已做 | 搓招两键 → `Skill/ComboInput`；前摇逐技能在 Skill（含反噬行）；GlobalRule 只留 target_frame_rate |
| E2 | 未动 | 待策划定稿 key |
| E3 | 未动 | 枚举列仍以字符串存储，由 `__enums__` 校验 |
