# 配表筛查清单：技能、搓招、推手、执念与现有配置仓库

筛查日期 2026-10-12，基线 dev 9ff6338。本文件只做盘点，不含实现。总览与跨域结论见 [README.md](README.md)。

> 2026-10-26 玩法深化之后（代码 0dbc551），反噬变体（`*_anti` 行、`antiSkillId`、`castLockSeconds`）已从表与代码退役，`Obsession` 加了五列，缘起有了三张新表，见第 10 节末尾与第 15 节。其余各节仍是 2026-10-12 基线的盘点记录，其中这些说法不再适用：第 1、3 节的 castLockSeconds / antiSkillId / `AntiCastConfirmDelaySeconds` / 反噬 id 推断（施法前摇在 `Skill.castConfirmDelaySeconds`，反噬共用它）；第 2、3 节的 6 行 `*_anti` 与第 2 节 `giant_anti` 显示名、`anti_blackcurtain` 的命名问题；第 4 节的 4.2、4.4、4.8 与 4.5、4.6、4.9 里的 Anti 部分（6 个 `Skill_*_Anti` 类与资产已删）；第 5 节的反噬表现条目（`accel_anti_vfx`、`slow_trap_anti_vfx`、`reverse_turn_trap_anti_vfx` 等）与第 6 节"SO 引用"里的两个 anti id（SO 已删，id 留在 `VfxRegistry`）；第 7 节的 `acceleration_anti_observers`、`blackcurtain_anti_observers`；第 8 节（`ComboSkillInput`、`Skill/ComboBinding`、`Skill/ComboInput` 已随 A7 删除）；第 9 节（技能键 Q/E/R 与 1/2/3，鼠标瞄准与鼠标左 / 右键推掌，见 [inventory-movement.md](inventory-movement.md) 表 10；2026-10-30 起 W 是前进键、按住才推进，“油门恒 1”不再成立，W/↑ 推掌已删除，↑ 不绑定）；第 11 节 `rotationSpeedDegPerSec` 的含义（改为鼠标转手速度上限，←/→ 只在没有鼠标时回退）；第 13 节的 Hidden 池 6 行（已删）；第 14 节的「（噬）」后缀（`skill.*_anti.*` 文案保留，没有行引用）；"重复与遗留来源"的 3、9、13 与 10 里的 Anti 部分，12 的计数现为 VFX id 1 个（`reverse_turn_trap_vfx`）、Feel 事件 2 个（`acceleration_local`、`blackcurtain_observers`）；待确认问题 2 已答（反噬的施法不加执念，玩法 README 6.2 已定），3 不再需要回答（玩家搓招已退役），7 里的黑幕 XOR 规则与神掌反噬规则随反噬变体删除。A7（2026-10-28，ac3d5fe）删除 `ComboSkillInput` 后，第 1 节 `GlobalRuleRecord` 的读取方 `ComboSkillInput.ApplyConfiguredGlobalRules` 与 `combo_input_window_seconds`、"重复与遗留来源"2 里的 `ComboSkillInput.Awake`、5、11 里的 `ComboSkillInput.debugHud` 也不再适用。

范围：`Assets/Scripts/Buddah/**`、`Assets/Scripts/VFX/**`、`Assets/Skills_Assets/**`、`Assets/Scripts/Config/**`、`Assets/Data/Config/`、`Assets/Resources/Config/`，以及 Buddah.prefab 上的相关组件。

缩写：CS=`Assets/Scripts/Buddah/ComboSkill/`；SA/X=`Assets/Skills_Assets/Skills/X.asset`；SDB=`Assets/Skills_Assets/Skill Database.asset`；BP=`Assets/Character/Prefab/Buddah.prefab`；MAIN=`Assets/Data/Config/ProjectConfigDatabase_Main.asset`；JSON=`Assets/Resources/Config/ProjectConfigDatabase.json`；RM=`Assets/Scenes/RaceMap.unity`。数值写作「代码默认 / asset 或 prefab 实际值」，运行时以后者为准。Buddah.prefab 在场景里没有任何 override。

## 结论先行

- 技能平衡值运行时以配置仓库为准，技能 SO 上的同名字段只是兜底（`SkillDataBase.cs:116-158`）。目前唯一漂移：如来神掌 SO 为 18/15，配置为 12/10，实际生效 12/10。
- `skillEffectParams` 这张表运行时没有任何代码读取。技能效果数值全部只存在 12 个技能 SO 里。
- JSON 兜底配置已损坏：缺结尾 `}`，id 过期（`slow_trap`），英文文案；`Assets/Data/SkillConfig/ProjectConfigSample.json` 与它逐字相同且无人引用。
- 移动参数有两份且值不同（`BuddahMovement` 与 `BuddahPredictedMotorConfig`，`turnInputMultiplier` 1 对 2）。详见 [inventory-movement.md](inventory-movement.md)。
- 代码默认值与 asset 实际值差别很大（弹速 18 对 600）。由表生成 SO 时必须以表为准。

## 1. 现有配置仓库记录总览（`Assets/Scripts/Config/**`）

加载链路：`ProjectConfigRuntime.EnsureInitialized(preferred)`（`Config/Runtime/ProjectConfigRuntime.cs:12-29`）优先用传入数据库，其次缓存；都没有时 `LoadDefaultDatabase`（:61-75）先 `Resources.Load<ProjectConfigDatabase>("Config/ProjectConfigDatabase")`，该路径下没有 asset，于是按 TextAsset 读 JSON。真正传入 MAIN 的是 `SkillDatabase.OnEnable/OnValidate`（`CS/SkillDataBase.cs:26-36`，`SDB:15` 引用 MAIN）。SDB 被 BP 和 PropertySelection.unity 引用。校验两层：`ProjectConfigValidator` 做结构校验；`Assets/Scripts/Editor/Config/SkillConfigCrossValidator.cs` 做 SO 与配置漂移比对，只报告不修改。

| 记录 / 字段 | 定义位置 | 读取方 | 状态 |
|---|---|---|---|
| `ProjectConfigDatabase._defaultRuleSetId/_defaultModeTag` | `Definitions/ProjectConfigDatabase.cs:8-9` | Selection/GlobalRule Repository 构造 | MAIN=default/default，JSON=default/race |
| `SkillDefinitionRecord.skillId` | `ProjectConfigRecords.cs:30` | SkillConfigRepository 主键 | 在用 |
| `.displayName` | :31 | `SkillDatabase.GetResolvedDisplayName`(:105) ← PropertiesSelectionManager:1110、BuildSelectableSkillOptions | 在用 |
| `.description` | :32 | `BuildSelectableSkillOptions`(:189) | 在用（仅选择界面） |
| `.behaviorType` | :33 | 只写入缓存（SkillConfigRepository.cs:136,183），无 getter；校验要求非空 | 无运行时作用，行为由 SO 子类决定 |
| `.iconKey` | :34 | BuildSelectableSkillOptions:190 → SelectablePropertyOption → `SkillWallItemView.PreviewIconKey`(:76) | 只透传，无 key→Sprite 解析 |
| `.isSelectable` | :35 | 无读取方 | 无作用（选池看 poolType） |
| `.sortOrder` | :36 | `SkillDatabase.CompareSelectableSkills:318-319` | 在用 |
| `.tags` | :37 | 只写缓存 | 无作用 |
| `.unlockState` | :38 | :191 只判断是否等于 "locked" | 部分在用 |
| `SkillBalanceRecord.cooldownSeconds/castLockSeconds` | :45-46 | `SkillExecutor.QueueCast:352-353`（按实际执行技能 id，含反噬）、`CastObserversRpc:437-438` | 在用，优先于 SO |
| `.obsessionGain` | :47 | `SkillExecutor.QueueCast:358`（按请求技能 id） | 在用；反噬行上的值无效（见重复项 9） |
| `.antiSkillId` | :48 | `SkillExecutor.ResolveAntiSkillId:678-701` | 在用 |
| `SkillEffectParamRecord` 四字段 | :52-58 | `SkillConfigRepository.TryGetFloat/TryGetParam`(:109-121) 运行时无人调用，仅 Editor CrossValidator | 无作用；MAIN 为空，JSON 4 行过期 |
| `SkillSelectionRuleRecord.ruleSetId/skillId/isEnabled/poolType/modeTag/mapTag` | :61-71 | `SelectionRuleRepository.GetSkillIdsForPool` ← SkillDatabase:53,210、AISkillCatalog.cs:84 | 在用 |
| `.allowDuplicate` | :66 | `SkillDatabase.GetAllowDuplicateSkillSelections:160` ← PropertiesSelectionManager:1118 | 在用 |
| `.weight` | :67 | 无读取方 | 无作用（全 1） |
| `DefaultLoadoutRecord.slot0-2SkillId/ruleSetId/modeTag` | :74-91 | `SkillDatabase.TryGetDefaultLoadout` ← `SkillLoadout.ApplyDefaultSkillsServer:90,101` | 在用 |
| `.loadoutId` | :76 | 无读取方 | 无作用 |
| `GlobalRuleRecord` | :95-101 | `GlobalRuleRepository.TryGetFloat` ← `ComboSkillInput.ApplyConfiguredGlobalRules:157-164` | 仅 1 个 key 在用 |
| `ProjectConfigConstants.GlobalRule*` 7 个 key | :9-15 | 只有 `combo_input_window_seconds` 有数据行和读取方 | 其余 6 个（pregame_countdown、race_lap_count、result_scene_countdown、finish_window、catchup_recovery_a、obsession_recovery_baseline）是空定义 |

## 2. 表 SkillDefinition（已有记录，数据在 MAIN:17-149）

列：skillId、displayName（本地化 key）、description（本地化 key）、behaviorType:enum、iconKey、isSelectable:bool、sortOrder:int、tags[]、unlockState:enum。

| skillId | displayName | behaviorType | iconKey | sel | sort | tags | unlock |
|---|---|---|---|---|---|---|---|
| acceleration | 神足通 | Acceleration | icon_acceleration | 1 | 10 | movement,buff | unlocked |
| slowtrap | 禅定缓流 | SlowTrap | icon_slow_trap | 1 | 20 | trap,debuff | unlocked |
| blackcurtain | 一念光明 | BlackCurtain | icon_blackcurtain | 1 | 30 | special,vision | unlocked |
| giant | 法相金身 | Giant | icon_giant | 1 | 40 | body,buff | unlocked |
| push_projectile_hands | 如来神掌 | PushProjectileHands | icon_push_projectile_hands | 1 | 50 | projectile,attack | unlocked |
| reverseturn | 逆因缘 | ReverseTurn | icon_reverseturn | 1 | 60 | control,debuff | unlocked |
| acceleration_anti | 神足通（噬） | AccelerationAnti | icon_acceleration_anti | 0 | 110 | anti,hidden | hidden |
| slowtrap_anti | 禅定缓流（噬） | SlowTrapAnti | icon_slowtrap_anti | 0 | 120 | anti,hidden | hidden |
| anti_blackcurtain | 一念光明（噬） | BlackCurtainAnti | icon_blackcurtain_anti | 0 | 130 | anti,hidden | hidden |
| giant_anti | 法相金身 （噬） | GiantAnti | icon_giant_anti | 0 | 140 | anti,hidden | hidden |
| push_projectile_hands_anti | 如来神掌（噬） | PushProjectileHandsAnti | icon_push_projectile_hands_anti | 0 | 150 | anti,hidden | hidden |
| reverseturn_anti | 逆因缘（噬） | ReverseTurnAnti | icon_reverseturn_anti | 0 | 160 | anti,hidden | hidden |

重复来源：`SkillAction.skillId/displayName`（`CS/SkillAction.cs:8-9`，各 `SA/*:15-16`）；`SkillDatabase.normalSkills/antiSkills`（SDB:20-33）按 SO skillId 建索引；`SDB.skills: []` 是空旧列表。命名问题：`giant_anti` 显示名带多余空格（MAIN:118、SA/Skill_Giant_Anti:16）；反噬 id 命名 `anti_blackcurtain` 与 `*_anti` 并存。

## 3. 表 SkillBalance（已有记录，MAIN:150-210）

| skillId | MAIN cd/lock/gain/anti | SO (SA/*:17-20) | 一致 |
|---|---|---|---|
| acceleration | 12/10/300/acceleration_anti | 同 | 是 |
| slowtrap | 12/10/300/slowtrap_anti | 同 | 是 |
| blackcurtain | 16/14/200/anti_blackcurtain | 同 | 是 |
| giant | 8/6/200/giant_anti | 同 | 是 |
| push_projectile_hands | 12/10/300 | 18/15/300 | 否，运行时 12/10 |
| reverseturn | 12/10/200/reverseturn_anti | 同 | 是 |
| acceleration_anti | 8/6/0 | 同 | 是 |
| slowtrap_anti | 3/1/0 | 同 | 是 |
| anti_blackcurtain | 16/14/100 | 同 | 是，gain 无效 |
| giant_anti | 8/6/0 | 同 | 是 |
| push_projectile_hands_anti | 10/10/0 | 同 | 是 |
| reverseturn_anti | 12/10/0 | 同 | 是 |

写死在代码、建议进表的施法时序：
- `CastConfirmDelaySeconds` 1 — `CS/SkillExecutor.cs:51` — `QueueCast:349` — 施法前摇，所有技能共用；cooldown 与 lock 从「请求时刻+前摇」起算（:350-356）；AI 也读（ConfirmationSeconds）。建议 SkillBalance.castConfirmDelay 或 GlobalRule。
- `AntiCastConfirmDelaySeconds` 1 — :52 — 建议 GlobalRule.anti_cast_confirm_delay。
- 反噬判定阈值 0.0001 — :323 — 技术常量，不进表。
- 反噬 id 推断后缀 `{skillId}_anti` — :693 — 第 3 层兜底，建议删除，统一由表指定。
- `Skill_Giant.DefaultAntiSkillId="giant_anti"` — `CS/Skill_Giant/Skill_Giant.cs:6,58-62` — OnValidate 写回 SO，第 4 层兜底。
- `SkillLoadout.SlotCount=3` — `CS/SkillLoadout.cs:9` — 建议 GlobalRule.skill_slot_count；同时决定 combo 绑定与 `_nextReadyTime` 数组长度；RPC 签名写死 3 参数（见 match-flow）。

## 4. 技能效果参数（每技能家族一张表，或通用 SkillEffectParam 键值表）

全部只存在 SO。值写作「代码默认 / asset」。

### 4.1 Acceleration（`CS/Skill_Acceleration/Skill_Acceleration.cs`，SA/Skill_Acceleration）
- `extraForwardForce` :7/asset:21 — 10 / 50 — `ExecuteServer:28` → `SkillExecutor.ApplyAccelerationToOwner:447` → `OwnerMovementEffectApplier`(:13-58) → 旧路径 MovementAccelerationEffect，新路径 PredictionBridge。JSON 过期值 10。
- `extraMaxSpeed` :8/:22 — 3 / 40。JSON 过期值 3。
- `durationSeconds` :9/:23 — 10 / 10 — 效果、拖尾 `ShowAccelerationTrailLocal:47`、FOV 保持 :62。
- 叠加规则写死 — `MovementAccelerationEffect.cs:38-46` — 力与速度取绝对值更大者，持续取更长者。建议枚举列 stackRule。

### 4.2 AccelerationAnti（`Skill_Acceleration_Anti.cs`）
- `slowForwardForce` :14/asset:21 — 10 / 45（内部转负，:35-38）；`slowMaxSpeed` :17/:22 — 3 / 40；`durationSeconds` :19/:23 — 3 / 6。

### 4.3 SlowTrap（`CS/Skill_SlowTrap/Skill_SlowTrap.cs`）
- `zoneDurationSeconds` :15/asset:27 — 5 / 10 — `MovementSlowTrapZoneEffect.ApplyOrRefresh`(:28-75)。JSON 过期 5。
- `perTargetReapplyCooldown` :16/:28 — 0.75 / 0.5 — `HandleTriggerTouch:93-101`。
- `affectCaster` :17/:29 — false / 0。`targetLayers` :18/:30-32 — ~0 / 全部 — 技术参数。
- `slowForwardForce/slowMaxSpeed/slowDurationSeconds` :22,24,25/:33-35 — 实际 40 / 20 / 2 — `HandleTriggerTouch:96-98` → ApplyAccelerationToOwner（负值）。
- 区域大小不在 SO：`triggerColliderPrefab`=`Assets/Character/SkillComponents/SlowTrapZone.prefab`（:32 localScale (15,0.5,15)，:105-106 胶囊 r 0.5 h 2）→ 世界半径约 7.5。建议列 zoneRadius。区域挂在施法者身上跟随（`MovementSlowTrapZoneEffect.cs:133-138`）。
- `debugReference*` :10-14/:23-26 — 调试，不进表。`MovementSlowTrapZoneEffect` 上的 `_zoneTimeLeft` 等是运行时状态。

### 4.4 SlowTrapAnti（`Skill_SlowTrap_Anti.cs`）
- `rootDurationSeconds` :11/:21 — 1 / 1；`extraForwardForce/extraMaxSpeed` :14-15/:22-23 — 10/3；`accelerationDurationSeconds` :16/:24 — 4 / 2。
- 消费：`SkillExecutor.ApplyRootThenAccelerationToOwner:465` → MovementRootThenAccelerationEffect（写死最小值 0.01、0.05，:43,46）。

### 4.5 BlackCurtain / Anti（字段与值相同：`Skill_BlackCurtain.cs`、`Skill_BlackCurtain_Anti.cs`）
- `expandDurationSeconds` :13/:25 — 1.25 / 1；`holdDurationSeconds` :14/:26 — 0.75 / 10；`fadeOutDurationSeconds` :15/:27 — 0.35 / 2；`maxOpacity` :16/:28 — 1。
- 三段之和 13 s 传给 `SkillPerceptionState.ApplyCurtain`(:38，AI 感知)；同时 `BlackCurtainPresentation.TryPlay` → `BlackCurtainViewController.Play`(:119-188) → BlackCurtainScreenEffect。JSON 过期 `screen/durationSeconds=2.5`。
- 规则写死：谁能看到赛道边缘 = `localIsCaster ^ isAnti`（`BlackCurtainPresentation.cs:35`）；受害者隐藏其他玩家，恢复点 = expand+hold（ViewController:178-182）。
- 屏幕效果常量（`BlackCurtainScreenEffect.cs`）：ExpansionDelaySeconds 0.5 (:8)、MaxBlackHoleStrength 15 (:7)、MaxExpansionProgress 2 (:9)、属性名 StrengthBlackHole/_StrengthBlackHole (:5-6)。建议 BlackCurtainPresentation 全局表。
- 场景组件 `BlackCurtainViewController`（RM）：trackEdgeFadeInDelay 0.5、hideOtherPlayersForVictim 1、disableOutdoorVolumeDuringBlackCurtain 1、trackEdgeTag TrackEdge、trackEdgeVisibilityProperty _EdgeVisible、defaultRendererIndex 0、controlCameraRenderer 0；`outdoorVolumeObjectName` 场景 Volume / 代码 "Outdoor Volume"(:29)；`blackCurtainActiveVolumeObjectName` 场景 Volumn_Dark / 代码 "BlackCurtain Active Volume"(:32)。
- `TrackEdgeVisibility`（RM）：材质 SG_MapEdge、属性 _EdgeVisible、hidden 0、visible 1。

### 4.6 Giant / GiantAnti（`CS/Skill_Giant/`）
- Giant：`scaleMultiplier` :9/:21 — 5 / 4；`durationSeconds` 6/6；`grow/shrinkDurationSeconds` 0.35/0.35；`massMultiplier` 1 / 3；`forwardForceMultiplier` 1 / 3.5（:10-14/:22-26）。消费 `SkillExecutor.ApplyScaleToOwner:501` → PredictionBridge 或 PlayerScaleEffect；表现 `ScaleSkillPresentation.ApplyScaleEffect`。
- GiantAnti：`scaleMultiplier` :7 (Range 0.1–0.99)/:21 — 0.5 / 0.3；`durationSeconds` 4 / 6；`shrink/restoreDurationSeconds` 0.25/0.25；写死 mass=1、force=1（`Skill_Giant_Anti.cs:18`）。
- `PlayerScaleEffect` 隐含规则：旧路径 turnTorque 随体型倍率缩放（:132）；MinScaleMultiplier 0.1 (:6)；smoothstep 过渡 (:106)；最短持续 0.05 (:51)。
- 体型联动：推手偏移与碰撞盒（`BuddahHandControl.GetPushScaleMultiplier:661`、`PushHitbox.ApplyScaleMultiplier`）、相机距离（`PlayerCamera.cs:162-168`）。

### 4.7 ProjectileHands（`CS/Skill_PushProjectileHands/Skill_PushProjectileHands.cs`）
消费：`ExecuteServer:47-61` → `BuddahHandControl.ActivateProjectilePushMode:706-739`；`ExecuteObservers:74-83` → `ConfigureProjectilePushModeLocal:741`。
- `buffDurationSeconds` :7/:21 — 8 / 15；`buildUpSeconds` :9/:22 — 0.5 / 1；`chargedPushCooldownSeconds` :10/:23 — 0.6 / 1；`delayedPushActionSeconds` :11/:24 — 0 / 0.9。
- `projectileForwardOffset` 0/0；`projectileHeightOffset` :15/:26 — 0 / 30；`projectileSpeed` :16/:27 — 18 / 600；`projectileLifetimeSeconds` :17/:28 — 1.2 / 3；`projectileImpulseStrength` :18/:29 — 12 / 500；`projectileColliderSize` :19/:30 — (1.25,1.1,1.8) / (60,90,20)。
- `ignoreSolidWorld` :20/:31 — 无效字段：`ChargedHandProjectileRuntime.TryStopAtSolidWorld`(:380) 从未被调用。
- 写死：`useChargedProjectileRuntime=true`(:59,82)；常态推手命中扭矩 0、allowSelfHit=false（`BuddahHandControl.cs:637-638`）。
- 飞行物常量（`ChargedHandProjectileRuntime.cs`）：ProgressCompletionFraction 0.8 (:37)、FlightEndSpeedFraction 0.2 (:38，速度从 100% 线性降到 20%，手感参数建议进表)、最短飞行 0.05 (:190)、发射特效最短存活 0.1 (:304)。

### 4.8 ProjectileHandsAnti（`Skill_PushProjectileHands_Anti.cs`）
消费：`BuddahHandControl.FireChargedProjectileBurstReplicated:953-979` → SpawnChargedProjectileBurst。
- `buildUpSeconds` 0.3/0.3；`projectileCount` :8/:22 — 3 / 5 (int)；`projectileSpacing` 2 / 30；`additionalForwardSpawnOffset` 2 / 100；`additionalHeightSpawnOffset` 0 / 30；`projectileSpeed` 18 / 600；`projectileLifetimeSeconds` 1.2 / 2；`projectileImpulseStrength` 12 / 600；`projectileColliderSize` (1.25,1.1,1.8) / (60,50,20)；`hitTurnTorqueImpulse` :19/:30 — 12 / 50。
- 写死：reverseDirection=true、allowSelfHit=true、ignoreSolidWorld=true（`BuddahHandControl.cs:967-969`）。
- 魔数：找到手骨时锚点高度 `pushHeightOffset*0.1`(:1077)，否则完整值 (:1082)。排布公式 `ProjectileBurstPlanner.cs:31-32`。Feel 时长 = buildUp+lifetime，最少 0.05 (:54)。

### 4.9 ReverseTurn / Anti（`CS/Skill_ReverseTurn/`）
- `invertDurationSeconds` :8/:21 — 1.25 / 10；Anti 同 1.25 / 10（`Skill_ReverseTurn_Anti.cs:12`）。
- 消费 `ApplyInvertTurnInputToOwner:483` → MovementInvertTurnInputEffect，`turnInputMultiplier=-|saved|`(:63-64)，最短 0.05。
- 规则写死：正常版作用于「所有其他 SkillExecutor」(`Skill_ReverseTurn.cs:26-39`)，反噬只作用自己。建议枚举列 targetScope。

## 5. 表 SkillPresentation（每技能一行）

列：vfxId、vfxLocalOffset、vfxLocalEuler、vfxDurationSeconds（0=跟随效果）、vfxStopBeforeEndSeconds、observersFeelEventId、observersFeelStopEventId、fovBoost、fovRampIn、fovSettle、fovRampInCurve、fovSettleCurve。

- Acceleration（SA/Skill_Acceleration:24-80）：vfxId accel_back_vfx，offset (0,0,-2)，euler (0,180,0)，vfxDur 0，stopBefore 1；`observersFeelEventId` 为空（asset:29，代码默认 "acceleration_shared" :17）→ 服务端走 `SkillVfxReplicator.PlayVfxAll`(:32-37)；FOV boost 6/10，rampIn 0.15/1，settle 0.25/1，曲线 asset:34-80。
- AccelerationAnti：vfxId accel_anti_vfx 及 offset/euler（asset:25-27）无效：ExecuteServer 不播 VFX，库里无此 id；`observersFeelEventId` 未序列化，取默认 "acceleration_anti_observers"(:29)，FeelRouter 未注册。
- SlowTrap：vfxId slowtrap_vfx（asset:38；代码默认 "slow_trap_vfx"，不一致），vfxDur 11（比区域多 1 s），stopBefore 0。
- SlowTrapAnti：vfxId 空（asset:27；代码默认 "slow_trap_anti_vfx"）。
- BlackCurtain/Anti（asset:21-37）：材质 `Assets/VFX/一念光明/SG_BlackCurtain.shadergraph`，后备名 SG_BlackCurtain / Shader Graphs/SG_BlackCurtain；shader 属性 _Expansion、_Opacity、_ElapsedTime、_EffectActive、_Center；centerWorldOffset (0,1,0)；Feel 事件 blackcurtain_observers / blackcurtain_anti_observers 均未注册。建议单独 ScreenEffectPresentation 表或留在 SO。
- Giant/GiantAnti：Feel 为空；ExecuteLocal 重置相机（`ScaleSkillPresentation.cs:17-31`）。
- ProjectileHands：FOV boost 8/30，rampIn 0.15/1，settle 0.2/1，保持=buff 时长 (:99)，曲线 asset:42-89；蓄力 VFX `Assets/VFX/如来神掌/如来神掌VFXGraph.prefab`，发射特效 `.../射击特效.prefab`，进度属性 Progress。
- ReverseTurn/Anti：vfxId reverse_turn_trap_vfx / reverse_turn_trap_anti_vfx 及相关字段全部无效，代码从不播放，库里无 id。
- 共用（SkillExecutor）：Feel 事件 Skill_Reflection_Shared/Local、Anti_Reflection_Shared/Local (:53-56)；反噬动画 trigger IsAnti (:57)；"acceleration_local"（`OwnerMovementEffectApplier.cs:25,52`）未注册；`SkillCameraFov` 默认曲线 EaseInOut (:14-15)；`TimedVfxInstance` 实现提前停止发射。

## 6. 表 VfxRegistry（vfxId → prefab）

- `Assets/Skills_Assets/SkillVfxDatabase.asset`：accel_back_vfx → `Assets/VFX/神足通/Acceleration粒子效果.prefab`；slowtrap_vfx → `Assets/VFX/禅定缓流/slowtrap_vfxZone.prefab`。
- 消费 `SkillVfxDatabase.TryGetPrefab`（`Buddah/SkillVFXDatabase.cs:37`）← `Network/Gameplay/SkillVfxReplicator.cs`（BP:873-876：prewarmOnStart 1、prewarmAliveSeconds 0.05、revealDelayFrames 2）。
- SO 引用但库里没有的 id：accel_anti_vfx、reverse_turn_trap_vfx、reverse_turn_trap_anti_vfx。
- 未走注册表、直接引用的资源：蓄力 VFX、发射特效、SlowTrapZone、黑幕材质、地图边缘材质、推手 hitbox（`Assets/Character/Prefab/Cube.prefab`）；`BuddahHandControl.projectilePrefab` 为 null。
- `VfxStartupWarmup`（`Scripts/VFX/VfxStartupWarmup.cs`，RM）：runOncePerApp 1、dontDestroyOnLoad 1、keepAliveSeconds 0.1；shader 预热 `Assets/VFX/PreloadShader.shadervariants`；`vfxPrefabs` 只有 slowtrap_vfxZone，与注册表重复且不完整。建议 VfxRegistry.warmup:bool 列。

## 7. 表 FeelEvent（eventId → MMF_Player 层级路径）

- `SkillFeelRouter.entries`（BP:919-927）：acceleration_shared → null player；Anti_Reflection_Shared/Local、Skill_Reflection_Local/Shared → prefab 内 MMF 组件。
- 播放器必须是场景实例（`SkillFeelRouter.cs:41-48`），表里只能放 eventId 与层级路径。
- 用到但未注册：acceleration_local、acceleration_anti_observers、blackcurtain_observers、blackcurtain_anti_observers。

## 8. 表 ComboBinding（`CS/ComboSkillInput.cs`，BP:801-812）

> 已删除（A7）：`ComboSkillInput` 连同 `Skill/ComboBinding`、`Skill/ComboInput` 和 `__enums__.ComboToken` 一起退役，AI 和玩家都走 `SkillExecutor.CastSlotServer`。下文是基线记录。

| name | slot | sequence |
|---|---|---|
| Skill_1 | 0 | W, Up, W |
| Skill_2 | 1 | Up, W, Up |
| Skill_3 | 2 | W, W, Up, Up |

- 列：name、slotIndex:int、sequence:Token[]（Token 枚举只有 {W, Up}，:29）。消费 `PushToken`、`FindExactMatchOnSuffix`；`TryGetSequenceForSlot` 被 `UI/InGameUI/SkillSlotUI.cs:156`、`AI/AISkillCaster.cs:59` 使用。combo 绑定在槽位上，不在技能上。
- `stepWindowSeconds` 代码 :45 0.35、prefab BP:801 0.35；Awake 被 GlobalRule combo_input_window_seconds=0.35（MAIN:341-344）覆盖（:157-164）→ 重复来源，建议只留 GlobalRule。
- `MinimumStepWindowSeconds=0.01` (:13) 技术下限。`maxBuffer` :48/BP:802 — 6 → GlobalRule.combo_max_buffer。
- Awake 写死兜底绑定 2 条 (:75-79)，旧数据。

## 9. 表 InputKeyMap（全部写死）

- Combo 与推手：W→Token.W、UpArrow→Token.Up（ComboSkillInput.cs:121-126；BuddahHandControl.cs:443,452）。手部旋转 LeftArrow/RightArrow（BuddahHandControl.cs:412-418）。转向 A/D（`Buddah/Input/PlayerBuddahInputSource.cs:45-48`）。油门恒 1 (:31)。推手动画 trigger LeftW/RightUp（BP:645-646）。
- 两层来源：先读 InputSystem_Actions 的 action，再按 keyCode 二次过滤。详见 [inventory-movement.md](inventory-movement.md) 表 10。

## 10. 表 Obsession（`GameData/ObsessionFigure.cs`，BP:774-784）

| 字段 | 代码位置 | 代码默认 / prefab | 用途 |
|---|---|---|---|
| maxValue | :10 | 100 / 1000 | 上限 |
| minValue | :11 | 0 / 0 | 下限 |
| drainPerSecond | :18 | 1 / 10 | 每秒自然衰减 |
| catchupRecoveryA | :22 | 0.01 / 0.01 | 追赶加速衰减系数 |
| initialValue | :25 | 0 / 0 | 出生初值 |
| backfirePMaxPercent | :30 | 75 / 95 | 反噬概率上限 |
| backfireK | :34 | 0.012 / 0.008 | sigmoid 陡度 |
| backfireMidpointX | :37 | 500 / 500 | sigmoid 中点 |
| backfireKeepPMaxAtOrAboveX | :40 | 1000 / 1000 | 达到即取上限 |

公式：衰减每秒 = drain + a·gap²，gap 为与领先者完成度差（百分比，:77-85）；增长 `AddServer(obsessionGain)` 在施法执行时（SkillExecutor.cs:394）；反噬概率 `P = Pmax/(1+e^(-k(x-mid)))`，x≥阈值直接 Pmax (:157-170)；掷骰 `Random.value*100 < P`（`SkillExecutor.ResolveCastVariant:313-344`），AISkillCaster.cs:163 调同一函数。GlobalRule 的 catchup_recovery_a、obsession_recovery_baseline 只有常量定义，ObsessionFigure 不读配置。

2026-10-26 阶段 B：`Skill/Obsession` 加五列，初值是阶段 A 的 `ObsessionFigure` 序列化默认值，`ObsessionFigure.Awake` 的同名覆盖直接接上（序列化值退为兜底）：

| 列 | 初值 | 读取方 | 含义 |
|---|---|---|---|
| repeatWindowSeconds | 5 | ObsessionFigure → SkillExecutor（`SkillRepeatSurcharge` 按技能 id 记层数） | 冷却结束后多少秒内重放同一技能算同招加价 |
| repeatGainMultiplier | 1.15 | 同上 | 每层的执念倍率，复利 1.15 → 1.3225 → 1.52 |
| backfireLockSeconds | 10 | ObsessionFigure；SkillExecutor 也用它做限速惩罚的时长 | 反噬惩罚时长：锁全部技能、执念停止自然下降、限速打折 |
| backfireMaxSpeedMultiplier | 0.85 | ObsessionFigure → 预测修饰 MaxSpeedMultiplier | 惩罚期间限速倍率，复活不清 |
| backfireEndKeepRatio | 0.8 | ObsessionFigure | 惩罚结束时执念保留比例 |

五列都不能留空（空格导入为 0，range 检查跳过空格），由 `DesignTableDomainRules.BackfireAndSurchargeCellsAreFilled` 报 Error。

## 11. 表 HandPush（`Buddah/BuddahHandControl.cs`，BP:644-661）

| 字段 | 行 | 代码默认 / prefab | 用途 |
|---|---|---|---|
| rotationSpeedDegPerSec | :26 | 180 / 180 | 手部旋转速度，AI 也读 |
| pushWindupSeconds | :43 | 0.1 / 0.1 | 前摇 |
| pushLifetimeSeconds | :46 | 0.12 / 0.3 | hitbox 存活 |
| pushForwardOffset | :49 | 0.9 / 7 | 前向偏移 |
| pushSideOffset | :52 | 0.25 / 4 | 侧向偏移 |
| pushLeftSideBias / pushRightSideBias | :55/:58 | -0.05 / -0.1；0.05 / 0.1 | 左右偏置 |
| pushHeightOffset | :61 | 1 / 1 | 高度 |
| pushImpulseStrength | :64 | 6 / 100 | 冲量 |
| pushCooldownSeconds | :67 | 0.25 / 0.4 | 冷却 |
| projectileVisualLocalEuler / Scale | — | 0 / 1 | 飞行物视觉 |
| replicateToOthers | — | 1 | 动画同步开关 |

- 蓄力模式下冷却由 `PushAttackTiming.GetCooldown`（`Buddah/PushAttackTiming.cs:10-13`）切换为技能的 chargedPushCooldownSeconds。
- hitbox 形状在 `Assets/Character/Prefab/Cube.prefab`：BoxCollider (1.64,1,1.42)(:65) × scale (3.54,3.65,10.47)(:33)。`PushHitbox.OverlapPadding=0.02`(:16) 技术参数。写死默认飞行物碰撞盒 (1.25,1.1,1.8)(:89,271)。手部朝向同步节奏 0.05 s / 0.1°(:309-313) 技术参数。
- 受击方参数 `pushGraceSeconds` 0.25、`pushExtraMaxSpeed` 6（BP:620-621，legacy）及 V2 同名字段，见 movement。

## 12. 其他 prefab 与场景参数（Buddah 侧）

- Respawn（`Buddah/BuddahRespawn.cs:14-22`，BP:941-947）：respawnFloorTag RespawnFloor、continuousContactRespawnSeconds 2、postRespawnCooldownSeconds 1.5、respawnHeightOffset 3、respawnClearanceCheckRadius 0.75、additionalRespawnLiftStep 1.5、maxAdditionalRespawnLift 6。详见 movement 表 5。
- Camera（`Buddah/PlayerCamera.cs:20-45`，BP:694-706）：directionalOffsetPerSpeed (0.5,0,0.4)、maxDirectionalOffset (15,0,15)、baseFieldOfView 40（被 Cinemachine lens 覆盖 :294，无效）、zoomBySpeed 1、speedForMaxZoomOut 80、minSpeedFieldOfView 30、maxSpeedFieldOfView 35、maxSpeedDistanceOffset 10、speedZoomSmoothTime 0.1、adaptToPlayerScale 1、cameraDistanceScaleFactor 1、cameraFovPerExtraScale 0、offsetSmoothTime 0.75；stableIntroExitSmoothTime 0.6、stableIntroExitBlendDuration 1.2 未序列化，取代码默认。
- 速度粒子（`Buddah/BuddahSpeedParticleEffect.cs`，类名 PlayerSpeedParticleEffect）：play/stop 阈值代码 20/18，prefab 30/28（BP:968-969）。
- 拖尾：PlayerAccelerationTrail（BP:2694-2697）颜色 (1,0.7,0)、宽 1、time 0.6、fade 1；PlayerBlackCurtainTrail（BP:195-198）cyan、宽 0.5、time 0.6、fade 1；minVertexDistance 0.05 写死（两脚本 :134/:170）。
- 头顶标记 LocalPlayerOverheadMarker（BP:4303-4306）：高度 8.5、像素高 32、缩放 1.25–8；ArrowHeight=2 常量 (:17)。
- 纯技术（不进表）：SplineProgressTracker（BP:901-905）jumpMetersThreshold 8、windowRadiusT 0.2（代码 0.1）、windowSteps 40、maxProjectionDistance 30、maxStepFactor 1.5；常量 :8-12、速度阈值 25/15 (:79-82)、回绕 0.2/0.8 (:221-222)。

## 13. 选池与默认配装（已有记录 SkillSelectionRule / DefaultLoadout）

- 选池（MAIN:212-332）：Selectable(0) 6 个普通技能；Fallback(1) acceleration、push_projectile_hands、blackcurtain；Hidden(3) 6 个反噬；全部 weight 1、allowDuplicate 0，modeTag/mapTag 空。
- 默认配装 `race_default` = acceleration / push_projectile_hands / slowtrap（MAIN:334-339）。重复：`SkillLoadout._defaultSkillIds`（`CS/SkillLoadout.cs:16`，BP:830-833）= acceleration / push_projectile_hands / blackcurtain，第 3 槽不同。`AI/AISkillCatalog.cs:76-77` 另写死 6 个技能 id。
- 上下文：`SkillDatabase._activeRuleSetId` default、`_activeModeTag` default、`_activeMapTag` 空（SDB:17-19）。

## 14. 多语言文本

显示名与描述同时存在于 MAIN、SO displayName、JSON（英文且过期）。「（噬）」后缀是约定，建议改规则或独立 key。日志文本不进表。

## 重复与遗留来源

1. 技能平衡值配置优先；SO 字段是遗留兜底，仅如来神掌漂移。`SkillConfigCrossValidator.cs:50-57` 能报出。
2. 配置数据库两份：MAIN（SDB:15 注入，实际生效）与 JSON（损坏、过期、英文、modeTag=race）；`ProjectConfigSample.json` 逐字相同且无人引用。风险：谁先调 `EnsureInitialized` 决定先加载哪份；若 `ComboSkillInput.Awake` 或 `AISkillCatalog.Load` 早于 SkillDatabase，会去解析坏 JSON，之后 `SkillDatabase.OnEnable` 再换回 MAIN。
3. antiSkillId 4 层来源：配置 → SO → `{id}_anti` 推断（SkillExecutor.cs:693）→ Skill_Giant 写死默认。
4. 默认配装 3 层来源：属性选择缓存 → MAIN.defaultLoadouts → SkillLoadout._defaultSkillIds（第 3 槽不同）。
5. combo 窗口两处：prefab 0.35 与 GlobalRule 0.35。
6. skillEffectParams 运行时无读取方。
7. 移动参数两份（见 movement）。
8. 配置有字段无人读：behaviorType、tags、isSelectable、weight、loadoutId；iconKey 无解析器；unlockState 只识别 locked；6 个 GlobalRule 常量；`GlobalRuleRepository.GetString`。
9. 反噬技能的 obsessionGain 实际无效：增长量按请求技能 id 取（SkillExecutor.cs:358），反噬时加的仍是原技能值；反噬的 cooldown/lock 按实际执行 id 取，作用到原槽位。
10. SO 无效字段：ReverseTurn/Anti 全部 vfx*；Acceleration_Anti 的 vfx*；`Skill_PushProjectileHands.ignoreSolidWorld`；SlowTrap.debugReference*。
11. 无效代码：`BuddahHandControl.FireProjectileBurstImmediate`(:865)、`FireChargedProjectileImmediate`(:908) 无调用方；`ChargedHandProjectileRuntime.TryStopAtSolidWorld`(:380)；`BuddahRespawn.RespawnToTrackProgress` 的 resetSkillEffects、preserveObsession 参数未用 (:132)；SDB.skills 空旧列表；ComboSkillInput.debugHud。
12. 引用但未注册：VFX id 3 个、Feel 事件 4 个。
13. 黑幕 normal/anti 两 SO 表现字段完全重复，可共用一行。
14. 代码默认值普遍与 asset/prefab 不一致。
15. `PlayerCamera.baseFieldOfView` 被 lens 覆盖，无效。

## 待确认问题

1. 确认以 SkillBalance 为准并删除 SO 同名字段？如来神掌取 18/15 还是 12/10？
2. 反噬触发时执念加原技能 gain 还是反噬技能 gain？
3. combo 继续绑槽位，还是每技能自带出招序列？
4. JSON 兜底修好保留还是删除？`Resources.Load<ProjectConfigDatabase>` 路径下目前没有 asset。
5. iconKey 需要 key→Sprite 表吗？
6. ruleSetId/modeTag/mapTag 只用到 default，Excel 里是否保留三列？
7. 写在代码里的规则是否做成枚举列：黑幕可见规则（施法者 XOR 反噬）、逆因缘作用范围、神掌反噬的反向/自伤/穿墙、加速叠加规则。
8. FOV AnimationCurve 在 Excel 中如何表示：关键帧数组还是预设曲线 id？
9. Buddah.prefab 上的参数（移动、推手、相机、复活、执念）是否一起迁表？迁则 ObsessionFigure 需改为读配置。
10. 慢速陷阱区域半径目前在 SlowTrapZone.prefab 的 scale 上，是否改为表列？
11. AI 侧 SkillPersonalities、AISkillCatalog.SkillIds、SkillVfxReplicator 都依赖同一组技能 id，是否纳入本次重构？

## 15. 2026-10-26 阶段 B：反噬变体退役与缘起表

删除（代码读取方一起删）：

| 位置 | 删除内容 | 代码 |
|---|---|---|
| Skill/Skill | `castLockSeconds`、`antiSkillId` 两列；6 行 `*_anti` | `SkillAction.castLockSeconds`、`SkillDatabase.GetCastLockSeconds`、`SkillConfigRepository.TryGetCastLock`、`ProjectConfigValidator` 与 `DesignTableDomainRules` 的反噬规则、`SkillConfigCrossValidator` 的锁定比较 |
| SkillEffectAcceleration | `acceleration_anti` 行；`slowForwardForce`、`slowMaxSpeed` 列 | 无（类已在阶段 A 删除） |
| SkillEffectSlowTrap | `slowtrap_anti` 行；`extraForwardForce`、`extraMaxSpeed`、`accelerationDurationSeconds` 列 | 无 |
| SkillEffectBlackCurtain | `blackcurtain_anti` 行 | 无 |
| SkillEffectGiant | `giant_anti` 行；`restoreDurationSeconds` 列 | 无 |
| Push | `charged_anti` 行；`projectileCount`、`projectileSpacing`、`hitTurnTorqueImpulse` 列 | 无（`PushChargedAntiMap` 已在阶段 A 删除） |
| SkillPresentation / SkillConstant / SkillSelectionRule | 各自的 `*_anti` 行（SkillSelectionRule 是 6 行 Hidden） | 无 |
| FeelEvent | `acceleration_anti_observers`、`blackcurtain_anti_observers` | 无 |

保留：`FeelEvent` 的 `Anti_Reflection_Shared / Local`（统一反噬反馈）、`VfxRegistry` 的反噬 id、`Global/__enums__` 里 `SkillId` 的 6 个 anti 值、`UI/Text` 的 `skill.*_anti.*` 文案；`ComboInput / ComboBinding` 已在 A7 删除（AI 不再逐键搓招，`ComboSkillInput` 删除）。

新增缘起表（结构见 [yuanqi-architecture.md](../gameplay/yuanqi-architecture.md) §3，字段名就是 SO 字段名）：

| sheet | 列 | 读取方 |
|---|---|---|
| Skill/Yuanqi | yuanqiId（`enum:YuanqiId#key`）、pairNumber（doc）、displayNameKey、descriptionKey、skillA、skillB、consumeA、consumeB、mode、retriggerPolicy、isEnabled | SkillDatabase：`YuanqiRowMap` 显式映射到 `Skill Database.asset` 的 `yuanqis` 里同 id 的 Yuanqi_* SO；名称经 `GetYuanqiDisplayName`（HUD 缘起提示、选技能界面）；描述经 `GetYuanqiDescription`，没有界面显示 |
| Skill/YuanqiEffectFrostLock | yuanqiId、freezeSeconds（N=1.5）、radiusMeters（R=12）、affectsTrainingDummies（true） | SkillDatabase 同名覆盖 Yuanqi_FrostLock |
| Skill/YuanqiEffectGiantCharge | yuanqiId、burstExtraMaxSpeed（60）、burstSeconds（0.8）、impactImpulseMultiplier（M=3） | SkillDatabase 同名覆盖 Yuanqi_GiantCharge |

三行：giant_palm（#1，giant + push_projectile_hands，Sustained，不消耗）、frost_lock（#2，acceleration + slowtrap，Instant，两招都消耗）、giant_charge（#4，acceleration + giant，Instant，只消耗 acceleration）。#1 没有参数，不建家族表。写入时机与技能 SO 相同：每局 Play 首次查表时写入，编辑器退出 Play 时恢复资产。

校验（`DesignTableDomainRules`）：两招不同、无序唯一、Sustained 不消耗（`mode` 按导入时的解析判断，写 `Sustained` 或枚举值 `1` 都算）、`skillA / skillB / consumeA / consumeB / mode / retriggerPolicy / isEnabled` 必填（Error；留空的招式会让 `YuanqiCatalog` 静默丢掉这一对）；三个技能两两都有启用的缘起时报 Warning。家族行的资产类名不由表规则检查（表规则看不到资产）：类名不符时 `SkillDatabase` 跳过该行并打 Warning，`SkillConfigCrossValidator` 报告，EditMode 测试 `EveryYuanqiRowHasAnAssetOfItsFamily` 失败。

文案：`UI/Text` 新增 `yuanqi.<id>.name / .desc` 六个 key。名称里的巨、冰、封、破、阵不在 SoloCJK 子集里（描述还缺更多字），字体重烘之前 HUD 退回显示 id。

机制更新新增、没有进表的参数（组件序列化字段，Buddah.prefab 没有覆盖，代码默认即实际值）：`BuddahHandControl` 的 `useMouseAim` true、`mouseAimSmoothingSeconds` 0.06、`mouseAimDeadRadius` 1.5（乘体型）、`mouseAimPlaneHeightOffset` 0、`yawResendAfterFreezeSeconds` 0.3；`RacerSkillPresentationState` 的 `releaseDelaySeconds` 0.1、`holdAnimatorWhileFrozen` true；`SkillVfxReplicator.defaultStopLingerSeconds` 1。技能与缘起 SO 上也有新字段没有进表，各自的 .asset 都没有序列化它们，代码默认即实际值：`Yuanqi_GiantCharge` 的施法者镜头 FOV 冲击 `casterFovKickDegrees` 8、`casterFovKickHoldSeconds` 0.15、`casterFovKickSettleSeconds` 0.65；`Skill_Acceleration.consumedVfxLingerSeconds` 1；`Skill_SlowTrap.zoneEndVfxLingerSeconds` 1。是否入表按决定 4 先与用户同步（待确认）。
