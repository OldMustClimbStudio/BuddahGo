# 策划配表前期筛查（Design Tables Screening）

筛查日期 2026-10-12，基线 dev 9ff6338，分支 `refactor/design-tables`。本目录是把 BuddahGo 全部策划可编辑内容迁入 Excel 配表之前的盘点结果：哪些数据需要化表、它们现在在哪、谁在读、哪些是重复或死数据、哪些决定必须先拍板。本阶段只盘点，不改代码、不改资产。

## 文件

| 文件 | 覆盖范围 |
|---|---|
| [inventory-skills.md](inventory-skills.md) | 技能定义与数值、技能效果参数、表现、VFX 注册、Feel 事件、搓招、推手、执念、现有配置仓库记录逐字段状态 |
| [inventory-movement.md](inventory-movement.md) | 预测马达、受击、状态效果规则、起跑交接、重生、圈数、输入绑定、全局物理与帧率 |
| [inventory-ai.md](inventory-ai.md) | 驾驶难度档位、技能难度、五种人格、机会谓词常量、承诺规则、AI 名单 |
| [inventory-match-flow.md](inventory-match-flow.md) | 比赛模式规则、赛道（起跑点、检查点、结果区、逆行）、大厅房间、结算流程、属性选择、开场演出、身份命名、Match/UI 文案 |
| [inventory-ui.md](inventory-ui.md) | UI 文案、HUD 与菜单参数、图标映射、小地图、过场 timeline、字体、编辑器工具、钉死数值的测试 |
| [inventory-assets.md](inventory-assets.md) | prefab 注册、角色外观、VFX/Feel 资产、音频（无）、场景列表、标签层、物理与输入资产、未引用资产 |
| [decisions.md](decisions.md) | 用户已拍板的决定（按日期追加） |
| [excel-spec.md](excel-spec.md) | Excel 结构规范草案：表头约定、类型语法、工作簿与 sheet 明细、直通车管线、校验规则、待同步清单 |

每份清单的格式：字段 — 位置 — 当前值（代码默认 / asset 或 prefab 实际值）— 消费方 — 建议表与列 — 备注。末尾各有「重复与遗留来源」「排除项」「待确认问题」。

## 筛查方法与覆盖

- 六个只读调查并行覆盖 `Assets/Scripts/**` 全部自有代码、`Assets/Skills_Assets`、`Assets/Data`、`Assets/Resources`、三个 Build 场景、本项目 prefab 的序列化值、`ProjectSettings` 的标签层物理输入、`Assets/Animation/CG` 的 timeline、`Assets/Editor` 工具与 `Assets/Tests/EditMode`。
- 未覆盖：第三方包内部（FishNet、Feel、Adobe Substance、AllIn1Vfx、Lofelt）、`Tools/ai` 除难度 JSON 以外的历史快照、`agent-exchange/`。
- 判定原则：运行时以序列化值（prefab / scene / asset）为准，代码默认值只作参考；凡是策划会想调的数值、文案、id、映射都列入，工程参数单独列在排除区，不删。

## 建议表目录（合并六份清单）

「状态」含义：已有 = 配置仓库里已有记录类型；部分 = 有数据结构但散在 SO/prefab；新建 = 目前全部硬编码或散落。

### A. 技能与施法
| 表 | 状态 | 主键 | 当前数据位置 |
|---|---|---|---|
| SkillDefinition | 已有 | skillId | ProjectConfigDatabase_Main.asset |
| SkillBalance | 已有 | skillId | 同上；需补 castConfirmDelay（SkillExecutor 写死 1 s）|
| SkillEffect_*（每技能家族一张）或 SkillEffectParam 键值表 | 部分 | skillId | 12 个 Skill_*.asset；SkillEffectParam 记录存在但无人读 |
| SkillPresentation | 部分 | skillId | Skill_*.asset 的 vfx*/feel*/fov* 字段 |
| VfxRegistry | 部分 | vfxId | SkillVfxDatabase.asset（2 条，缺 3 条）+ VfxStartupWarmup |
| FeelEvent | 部分 | eventId | Buddah.prefab 的 SkillFeelRouter.entries（缺 4 条）|
| ComboBinding | 部分 | slotIndex | Buddah.prefab 的 ComboSkillInput.bindings；HUD prefab 另存一份 |
| SkillSelectionRule / DefaultLoadout / GlobalRule | 已有 | — | MAIN；GlobalRule 7 键只有 1 个被读 |
| Obsession | 新建 | 全局一行 | Buddah.prefab ObsessionFigure |
| HandPush | 新建 | 全局一行 | Buddah.prefab BuddahHandControl + Cube.prefab |

### B. 角色与运动
| 表 | 状态 | 主键 | 当前数据位置 |
|---|---|---|---|
| BuddahMovementBase | 新建 | 角色 | Buddah.prefab BuddahPredictedMotorConfig（legacy BuddahMovement 重复）+ Rigidbody |
| PushReaction | 新建 | 冲量来源 | 同上 pushGrace/pushExtraMaxSpeed；按来源分行目前无数据 |
| MovementModifierRules | 新建 | ModifierType | New_Buddah Events/Resolver 硬编码（下限 0.1、叠加规则）|
| LaunchHandoff | 新建 | 全局 | legacy BuddahMovement.launch*、RaceBodyIntroStateController、Config 缺省字段 |
| RespawnRules | 新建 | TeleportSource | Buddah.prefab BuddahRespawn + RespawnBridge 硬编码标志 |
| LapRules / RaceProgressTuning | 新建 | Track 或全局 | Buddah.prefab LapProgress、RaceCompletionTracker；RaceRules.DefaultLapsToFinish |
| Camera | 新建 | 全局 | Buddah.prefab PlayerCamera |
| RacerVisuals | 新建 | 角色 | 拖尾、速度粒子阈值、本地标记颜色 |
| PhysicsMaterials / GlobalPhysics | 新建（只读候选）| 材质 | Assets/Material/*.physicMaterial、DynamicsManager |

### C. AI 对手
| 表 | 状态 | 主键 | 当前数据位置 |
|---|---|---|---|
| AI_DrivingTier | 部分 | Difficulty | Resources/AI/{Easy,Normal,Hard}.asset（由 Tools/ai/*.json 生成）|
| AI_DrivingGlobal | 新建 | 全局 | ThrustVectorPlanner / AIDifficultyProfiles 常量 |
| AI_SkillDifficulty | 部分 | Difficulty | Resources/AI/SkillPersonalities.json |
| AI_Personality（+SkillWeight、SituationBias 子表）| 部分 | PersonalityId | 同上 JSON |
| AI_SkillOpportunity | 新建 | skillId | AISkillDecision 各谓词硬编码常量 + JSON BacklashSeverity |
| AI_SkillGlobal | 部分 | 全局 | JSON Tuning + AISkillWorld/Decision 硬编码 |
| AI_Roster | 新建 | 槽位 | RacerId、SoloSetupPanel、MatchSpawnManager |

### D. 比赛与流程
| 表 | 状态 | 主键 | 当前数据位置 |
|---|---|---|---|
| MatchModeRules | 新建 | ModeId | OnlineMatchRules / SoloMatchRules 硬编码 + RoomStateManager、RaceFinishManager、ResultDecisionManager、PropertiesSelectionManager 的时长字段 |
| Track（+Spawn、Checkpoint、ResultAnchor、MiniMapBounds 子表）| 新建 | trackId | RaceMap.unity 场景 Transform 与组件；MiniMapController |
| LobbyConfig / SessionConfig | 新建 | 全局 | SteamLobbyManager、SessionLauncher |
| ResultVoteRules / ResultPresentation | 新建 | ModeId | ResultDecisionManager 硬编码、MatchResultPresentationCoordinator |
| SelectionStage / SelectionOption | 新建 | stageKey / optionId | PropertiesSelectionManager 硬编码占位 |
| IntroConfig / IntroLayout | 新建 | playerCount | IntroSequenceManager、Placeholder/Intro/Layout*P.prefab、TransitionToRaceMap.playable |
| RacerNaming / SoloSetup | 新建 | 全局 | RacerIdentity、PlayerIdentity、SoloSetupPanel |

### E. UI 与表现
| 表 | 状态 | 主键 | 当前数据位置 |
|---|---|---|---|
| UI_Text（本地化）| 新建 | key | Scripts/UI 英文硬编码、Match/UI 中文硬编码、场景 prefab m_text |
| UI_Timing / SelectionPresentation / HUD_Obsession / UI_Layout | 新建 | 组件 | 各 UI 脚本 SerializeField（代码默认与 prefab 普遍不一致）|
| SkillIcon | 部分 | skillId | UIprefap.prefab SkillUiSpriteLibrary（iconKey 列未用）|
| MenuOptions | 新建 | option | CreateRoomUI 与 MainMenu 下拉框 |
| Presentation_Timeline | 只读 | asset | Animation/CG/*.playable 的时长与信号 |

### F. 只读 / 工程表
Tags_Layers、Scene_Flow、Input_Bindings、Prefab_Registry、Audio（预留，项目目前无音频）。

## 跨域结论（必须在建表前处理）

1. **配置仓库已存在但只接了一半。** `Assets/Scripts/Config` 的加载、仓库、校验三层可复用。但 `skillEffectParams` 运行时无人读，`GlobalRule` 7 个键只有 `combo_input_window_seconds` 被消费，`behaviorType`、`tags`、`isSelectable`、`weight`、`loadoutId`、`iconKey` 有字段无读取方。
2. **JSON 兜底配置已损坏。** `Assets/Resources/Config/ProjectConfigDatabase.json` 缺结尾 `}`，技能 id 过期（`slow_trap`），文案为英文；`Assets/Data/SkillConfig/ProjectConfigSample.json` 与它逐字相同且无人引用。`Resources.Load<ProjectConfigDatabase>` 的路径下没有 asset，谁先调用 `EnsureInitialized` 就可能先解析坏 JSON。
3. **同一数值多处定义是常态。** 移动参数两份（turnInputMultiplier 1 对 2）；默认配装 4 份（MAIN 第 3 槽 slowtrap，prefab 与场景 blackcurtain）；antiSkillId 4 层；combo 序列 3 份；圈数 5 处；场景名 5 处；"最多 6 人 / 5 AI" 9 处；驾驶档位 5 份；handoff lead 2 处；过场时长 3 处。
4. **代码默认值与 prefab/scene/asset 实际值普遍不一致**（弹速 18 对 600、执念上限 100 对 1000、pregame 15 对 18、intro 速度 20 对 60 等）。配表以序列化值为基线，由表生成 SO 后不能再依赖代码默认。
5. **大量规则写死在代码里**：黑幕可见规则（施法者 XOR 反噬）、逆因缘作用范围、神掌反噬的反向/自伤/穿墙、加速叠加规则、Giant 不缩放 maxSpeed、反噬 obsessionGain 按原技能取值、结算投票法定规则、排名平局顺序、AI 机会谓词全部阈值、ready 门槛。要么做枚举列，要么在表文档里定稿。
6. **结构性常量不应交策划改但要集中声明**：SlotCount=3（RPC 签名写死 3 参）、MaxAI=5 与数组容量 6、RacerId.AIBase=10000、车辆最高速 80 在 AI 侧三处耦合、tickRate 60 与 FrameRateLock 120、KeyMaxSeconds < stepWindowSeconds。
7. **没有本地化系统，字体是瓶颈。** 文案中英混用；SoloCJK 字体是 499 码位静态子集，中文文案进表后必须重生成子集并重烘；TMP 默认字体无 CJK 回退；检视面板与技能名标签引用的字体 GUID `a3e319c7…` 在工程里不存在。
8. **gameplay 时序有一部分由 timeline 资产决定**：TransitionToRaceMap 18 s 决定 GO 时刻，PropertySelectionEnd 2 s 决定加载比赛场景的等待，EndResultSwitchTimeline 信号决定结算流程。要么表只读显示，要么表覆盖 timeline。
9. **配置断链已存在**：vfxId 3 个、Feel eventId 4 个引用了但未注册；`MiniMapController.playerTag="Buddah"` 是 layer 名不是 tag；RaceMapEndField 场景不存在但两处仍引用；地图投票与皮肤选择结果无人读。
10. **死数据与遗留**：legacy BuddahMovement 与 4 个 legacy 效果组件、PlaceholderPlayerController、LapProgress 的 forwardWrap 三字段、CommandBus 未接线通道、MiniMap 8 个废弃字段、ReverseTurn/AccelerationAnti 的 vfx 字段、多个未引用 prefab 与贴图。配表时应一并清理或明确保留，避免策划对着无效字段调参。
11. **测试钉死了部分数值和文案**：LeaderboardHudTests 行格式、SixRacerResultsTests 时间格式、DefaultPlayerName "玩家"、AI 数量 0..5、combo 长度 3/3/4、5 种人格、6 个出生点、驾驶档位单调性。数值搬表后这些测试要改为读表。
12. **现成的导出范式**：`AIDifficultyAssetTool`（JSON → SO，FromJsonOverwrite）和 `SkillConfigCrossValidator`（SO 与配置漂移比对）可直接扩展为 Excel 导出与导入校验的骨架。

## 建表前必须拍板的决定

按影响范围排序。各域清单末尾还有更细的待确认项。

1. 数据管线：沿用 `ProjectConfigDatabase` 的记录列表 + GlobalRule 字符串键值，还是改为强类型生成代码？Excel 源表放仓库内还是在线表格？
2. 唯一真相：技能平衡值以 SkillBalance 为准并删除 SO 同名字段；移动参数以 BuddahPredictedMotorConfig 为准并删除 legacy 副本；默认配装以哪一份为准（slowtrap 还是 blackcurtain）。
3. 技能效果参数：每技能家族一张强类型表，还是接通现有的 SkillEffectParam 键值表？
4. 哪些硬编码规则升级为枚举列（见跨域结论 5），哪些只写进表文档。
5. 结构性常量（SlotCount、MaxAI、AIBase、最高速 80、tickRate）锁定为协议常量还是开放。
6. 赛道实体：是否建 Track 表并把 RaceMap 的起跑点、检查点、结果区、小地图边界、requiredCheckpointCount 迁入；多赛道时由场景导出还是表驱动。
7. 模式与数量：Practice 是否开放入口；Solo AI 数量是否可配 0–5；圈数按赛道还是按模式。
8. 本地化方案与字体策略：自建 UI_Text 还是 Unity Localization；字体子集重烘是否进导表流程；缺失字体 GUID 的替代。
9. timeline 时长是表只读还是表覆盖。
10. AnimationCurve 类字段（FOV、fade、move）在 Excel 中的表示：预设曲线 id 还是留在资产。
11. 断链与死数据的处理：补资源还是清字段；RaceMapEndField、legacy 组件、未引用资产何时删。
12. 音频、皮肤、per-racer 外观是否在本次预留表结构。

## 下一步

2026-10-12 用户已拍板：沿用 ProjectConfigDatabase、源表放仓库内、先定 Excel 结构；2026-10-13 决定表值以实际生效值为基线、能入表的都入表（见 [decisions.md](decisions.md)）。规范在 [excel-spec.md](excel-spec.md)。基线工作簿已生成在 `Assets/Design/Tables/`（6 本、62 张 sheet，含 CSV 镜像），生成脚本在 `Tools/design-tables/`。范围线定为"平衡与信息"（decisions 12），导出器已实现并接通消费方（spec 第 6 节）：40 张 sheet 进 ProjectConfigDatabase 并在运行时覆盖组件字段，20 张表现类 sheet 只校验不导出；导入按工作簿哈希分层、可按 tag 推送。剩余未接通项见 spec 6.3 末尾。

### 原计划步骤

1. 用户对上面 12 项拍板，结论记入本目录（建议 `decisions.md`）。
2. 据此定稿 Excel 工作簿结构：sheet 清单、表头三行（字段名 / 类型 / 注释）、外键关系、校验规则。
3. 修复 JSON 兜底或删除；接通或删除 6 个空 GlobalRule 键；补齐 3 个 vfxId 与 4 个 Feel eventId 或清掉引用。
4. 实现导出器（Excel → JSON/asset + 校验），复用 AIDifficultyAssetTool 与 SkillConfigCrossValidator 的模式。
5. 逐域迁移：先技能与执念（配置仓库已在），再运动与 AI（有 asset/JSON 源），最后比赛流程、UI 文案与赛道（全部硬编码或散在场景）。
6. 把钉死数值的测试改为读表，补 MiniMap、ObsessionUI、SkillSlotUI、选择阶段 UI 的回归测试。
