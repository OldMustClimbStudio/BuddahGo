# 配表筛查清单：UI 文案、HUD、菜单选项、图标、过场时长、字体、编辑器工具与测试

筛查日期 2026-10-12，基线 dev 9ff6338。本文件只做盘点，不含实现。总览与跨域结论见 [README.md](README.md)。

范围：`Assets/Scripts/UI/**`（InGameUI、InGameUI/Legacy、MenusUI）、`Assets/UI/**`、`Assets/UIMenus/**`、`Assets/UI Toolkit/**`、`Assets/Fonts`、UI 与开场引用的 `Assets/Animation`，`Assets/Scripts/Editor/**`、`Assets/Editor/**`，`Assets/Tests/**`（只记录钉死可调值的测试）。`Match/UI/**` 的中文文案在 [inventory-match-flow.md](inventory-match-flow.md) 字符串表一节。

## 结论先行

- 没有任何本地化系统：`Packages/manifest.json` 无 `com.unity.localization`，无 I2 等插件。
- 文案语言混用：`Scripts/UI/**` 全英文硬编码；`Scripts/Match/UI/**` 全中文硬编码；技能名与描述是中文，来自 ProjectConfigDatabase_Main.asset。
- UI Toolkit 实际未使用：Assets 下无 UXML，USS 只有 Feel 插件自带一个；`Assets/UI Toolkit/PanelSettings.asset` 无引用。
- 没有按赛车手序号的颜色表：`ShowOpponentsOnMinimap` 恒 false，小地图只显示本地玩家。

## 1. 表 UI_Text（Strings / Localization）

格式：字段 — 位置 — 当前值 — 备注。建议列 key、text（带 {0} 占位）、语言列。

### 1a. 菜单 / 大厅（MainMenu.unity）
- CreateRoomUI 状态文案 — `UI/MenusUI/CreateRoomUI.cs:70,78,112,117` — "SteamLobbyManager not found."、"Creating room..."、"Room created: {lobbyId}"、"Create failed: {reason}" — `{reason}` 来自 SteamLobbyManager（Network）。`HandleFlowStateChanged`(120-128) 直接显示 SteamLobbyManager 的 message。
- LobbyBrowserUI — `LobbyBrowserUI.cs:43,73,77,86,90,128,147,152` — "No rooms loaded."、"SteamLobbyManager not found."（2 次）、"Refreshing rooms..."、"Joining room {id}..."、"No joinable rooms found."、"Found {n} rooms."、"Join failed: {reason}"。
- LobbyBrowserListItemUI.Bind — `:40,43,46,49` — "Unnamed Room"、"{cur} / {max}"、"Host" / "Host: {steamId}"、"Lobby ID: {id}"。
- RoomJoinUI — `:63,70,74,106` — "SteamLobbyManager not found."、"Please enter a valid Lobby ID."、"Joining room..."、"Join failed: {reason}"。
- RoomPlayerEntryUI — `:15,18,21` — "Unknown Player"、"HOST"、"Leader" / "Ready" / "Waiting"。
- RoomUI — `:170,183,234,248,250,253,254,256,359,361` — "Host has left the room."、"Opening Properties Selector..."（2 次）、"Room #{id}"、"Waiting for room state manager..."、"Host can start the game."、"Waiting for players to get ready."、"Waiting for host to start the game."、"Cancel Ready" / "Ready"。

### 1b. 属性选择（PropertySelection.unity）
- PropertiesSelectorUI 页面标题 `:163` — "Practice"（Solo）/ "Properties Selector"。
- 阶段/状态 `:173,205,206,213,215,218,222,223,228,229,236,237` — "Waiting for PropertiesSelectionManager..."、"Loading Match"、"Loading {sceneName}..."、"Choose {SlotCount} skills"、"Step {i}/{n}: {label}"、"Time Remaining: {s}s"、"Syncing stage data"、"Waiting for host stage configuration..."、"All stages complete"、"Waiting to enter match..."、"Resolved Map: {id}" / "Resolved Map: pending" — 显示的是内部 id 而非显示名。
- 按选择模式说明 `:245-250` — Vote "Everyone is voting on the current stage option."；HostOnly "Only the host can choose during this stage."；Multi "Choose all desired options before the timer ends." / "Choose all desired options."；默认 "Each player chooses one option before the timer ends." / "Each player chooses one option." — key 按 PropertySelectionMode × timeoutEnabled 组织。
- 玩家选择列表 `:257,264,283,284,285,296,299` — "No active stage."、"Choosing..."、"{n}/{N} [{ids}]"、"{player}: {sel}"、"No players connected." — 技能阶段显示 skillId 而非显示名 (:279)。
- PropertyCarouselView `:333,339,340,341,348,349` — "Focused: None"、"Confirmed: None"、"Confirmed: {id}"、"Focused: {id}\n{confirmed}"、"Votes: {n}" / "Selected: {n}" — 显示 OptionId。
- PropertyGroupView `:49,50,105-108,115,119,120` — "Selected: {name}" / "Selected: None"、"Mode: Vote" / "Mode: Host Only" / "Mode: Multi Select" / "Mode: Single Select"、"Locked"、"Votes: {n}" / "Players: {n}"。
- `SkillInspectPanelUI.defaultHintText` — 代码 `:18`，场景 `PropertySelection.unity:4025` — "Esc to close"（SerializeField）— 与输入绑定重复。
- 技能名与描述显示：`SkillInspectPanelUI.cs:43,46`、`SkillWallItemView.cs:74-82`、`PropertyOptionItemView.cs:51,54`，来自 `SelectablePropertyOption.DisplayName/Description` ← ProjectConfigDatabase_Main.asset:18-143（12 行）。已在 Config 表，不重复建；只保留回退规则（回退到 OptionId）。

### 1c. 比赛 HUD（RaceMap.unity，Legacy 目录，仍在使用）
- LeaderboardTMPUI — `UI/InGameUI/Legacy/LeaderboardTMPUI.cs:237,240,258,259,263` — 标题 "Leaderboard"；"(no data)"；行格式 "{rank}. {name} - Lap {lap} - {pct:0.0}%"，完赛追加 " - Finished #{order}"；"(empty)" — 消费 outputText（RaceMap.unity:733）— 与 `Network/Gameplay/LeaderboardManager.cs:284-288` 重复；被测试锁定。遥测文案 `:195-224`（"Race Status: ..."、"Your Obsession: ..."、"Backfire Chance: ..."）只写日志，不进表。
- ResultDecisionUI — `Legacy/ResultDecisionUI.cs:110,111,117,121,122,167,169,172` — "Countdown: --"、"Waiting for ResultDecisionManager..."、"Next Decision In: {s}s"、"All players agreed. Starting a new game..."、"Returning everyone to the room..."、"Choose whether to start a new game or return to the room."、"Your Choice: {enum}"（直接输出枚举名）、"Waiting for your decision state..." — playerChoicesText 显示 `ResultDecisionManager.BuildDecisionSummary()`（Network）。

### 1d. 场景 / prefab 静态文案（m_text）
- MainMenu.unity 有意义的按钮与标签："Lobby Id Input":303、"Room Visibility":899、"Max Player":2222、"Create":2694、"Create Room":2962、"Room Name":5644/6172/8574、"Browse Rooms":5778、"Ready":6038、"Join Room":6516、"Enter text...":7032/10769、"Start Game":7523、"Return":8097、"Refresh":8231、"Back":8440/9385、"Join":8708、"PlayerNum:":8843、"Leave Room":9779、"Your Role:":10272、"Quit":11104。下拉框：可见性 m_Options "Public"、"FriendsOnly"、"Private"（:9674-9678）；最大人数 "4"、"5"、"6"（:11449-11453）。运行时被覆盖的占位（只需清理）："New Text" ErrorText:1033、MemberSteamIdsText:2052、StatusLog:2828、StatusText:3383、EmptyStateText:6776、StatusText:8958、EventLog:11840；"NewText" PlayerNum:5308；"Option A"、"​"、"Public":4800、"4":7299。MainMenu 37 个 TMP 文本全用 LiberationSans SDF。
- PropertySelection.unity："Confirm":2238；中文占位 "技能描述文本":1001、"提示文本":2538、"标题文本":4204；调试名占位 pageTitleText:182、countdownText:3082、resolvedMapText:3520、PlayersSelectionText:3891、statusText:5306、StageTitleText:5786。
- RaceMap.unity："Return To Room":954、"Start New Game":1654（ResultDecisionUI 按钮）；"EndMatch":4457（GO 名 Text (TMP)，未查到对应按钮）；"New Text" PlayerChoiseText:2496、StatusText:4591、CountdownText:10067。
- prefab：`Assets/UI/UIprefap.prefab` "Player3":112、"Player2":1226、"Player1":1435（RankingUI 静态样板，无脚本驱动）；`UIMenus/PropertySelector/PropertyGroupView.prefab` "Right":711、"Left":1135、"Select":1390，另 4 处 "New Text"；`技能绘马.prefab:221` "技能名称"（运行时覆盖）；`UIMenus/Lobby/LobbyBrowserListItem.prefab` hostText:68、LobbyIdText:202、"Room Name":336/612；`RoomPlayerEntry.prefab` HostBadgeText:68/452、"文字":202。

## 2. 表 UI_Timing（淡入淡出 / 动画时长 / 刷新间隔）

场景切换淡入淡出（SceneFadeController）：动画状态名 `:10-13` "CrossFade_In"、"CrossFade_out"、"CrossFade_in"、"CrossFade_Out"（技术绑定，不进表）；`fallbackDurationSeconds` `:28` 1（`PlayFadeInBeforeSceneLoad` 参数默认 1，:30）→ UI_Timing.sceneFadeFallbackSec；组件不在任何场景，运行时 `EnsureInstance`(:89) 创建，永远走代码默认；加载后淡出前固定等 2 帧 (:133-134)。

选择阶段 → 比赛过场：
- `PropertySelectionMatchTransitionTimeline.fallbackDurationSeconds` — 代码 `:13` 1 / 场景 `PropertySelection.unity:5602` 2 → UI_Timing.matchTransitionSec；只在 PlayableDirector 无资产时生效。
- `alphaCurve` — 代码 `:14`（EaseInOut 0→1，1 s）/ 场景 `:5603-5625`（0→1，2 s）— AnimationCurve 不适合 Excel。
- 实际生效时长来自 timeline：`PropertySelection.unity:3623` PlayableDirector 绑定 `PropertySelectionEnd.playable`（`Assets/Animation/CG/PropertySelectionEnd.playable:134-135` m_StopTime 2；alpha 曲线 0→1.0437 在 2 s 内 :87-97）；`GetDurationSeconds()`(:39-54) 返回 duration=2 s；消费 `PropertiesSelectionManager.ResolveMatchTransitionDurationSeconds`（:668-678），服务器据此 WaitForSeconds 再加载比赛场景(:645-649)。这是真正影响 gameplay 时序的值。
- `PropertiesSelectionManager._sceneTransitionFadeDurationSeconds / _preferTimelineForMatchTransition` — 代码 1 / true，场景 2 / 1 — 三重重复。

技能检视面板 / 技能墙：
- `SkillInspectPanelUI.fadeDuration` — 代码 `:24` 0.18 / 场景 `PropertySelection.unity:4027` 0.35；`fadeCurve` 场景 4028+ → UI_Timing.inspectPanelFadeSec。
- `SkillInspectController`（代码 `:28-35`，场景 `:2412-2442`）：enterDuration 0.35、exitDuration 0.25、flightArcHeight 0.2、enableIdleMotion 1、idleAmplitude 0.02、idleFrequency 0.75、idleYawDegrees 2（均等于代码默认）；`moveCurve` 场景起点斜率 2 → SelectionPresentation 表。
- `SkillWallItemView`（代码 `:18-29`，`技能绘马.prefab:343-372`）：hoverForwardDistance 0.08、hoverMoveDuration 0.12、defaultFlightDuration prefab 0.5 / 代码 0.35、normalColor 白、hoverColor (0.789,1,0.599) / 代码 (1,.95,.75)、selectedColor 白 / 代码 (.7,1,.7)。
- `SkillWallSelectionController`（代码 `:31-42`，场景 `:2381-2391`）：wallColumnCount 4、wallHorizontalSpacing 0.5 / 代码 1.2、wallVerticalSpacing 0.2 / 0.8、moveDuration 0.6 / 0.35、moveArcHeight 0.45、animateSelections 1 → SelectionPresentation.skillWall*。
- `SkillSelectionSlotAnchor.slotIndex` — 场景 `:438` 2、`:1366` 0、`:3832` 1 — 与 SlotCount=3 绑定，结构数据不进表。

属性轮播 `PropertyCarouselView`（代码 `:28-44`，`PropertyGroupView.prefab:531-537`）：spacing 320、itemYOffset 0、animationDuration prefab 0.5 / 代码 0.25、animationSharpness 12、minScale 0.8、maxScale 1.2、scaleFalloff 0.65 → UI_Layout.carousel*。

选择阶段相机 `PropertySelectionStageSceneController.transitionDuration` — 代码 `:38`，场景 `:1614` — 1.1；transitionCurve 线性；activeCameraPriority 100 / inactive 0（:1645-1646，技术）→ SelectionPresentation.stageCameraTransitionSec。贝塞尔控制点系数 0.35（:433,444）硬编码；`CurrentStageIndex >= 2` 视为 Third (:312) 硬编码。

技能槽 HUD `SkillSlotUI`（代码 `UI/InGameUI/SkillSlotUI.cs:32-42`；prefab `Assets/UI/UI预制键位/SkillSlot_1.prefab:576-591`、`SkillSlot_2.prefab:801-816`、`SkillSlot_3.prefab:951-957`、`FXJS_SkillUIRoot-1.prefab:51`）：rotateSpeed 120、slotIndexOverride -1、bindRetryInterval 0.5、successHoldSeconds 2、fallbackCastLockSeconds 2（与技能 castLockSeconds 重复）、previewTimeoutSeconds 0.5 → UI_Timing.skillSlot*。槽位号从节点名 "SkillSlot_" 前缀解析（:446-454）硬编码约定。

执念 HUD `ObsessionUI`（代码 `UI/InGameUI/ObsessionUI.cs:7-24`，`UIprefap.prefab:2003-2012`）：smoothSpeed 8、colorSmoothSpeed 5、fadeSpeed 3、maxShakeStrength prefab 5 / 代码 3、shakeSpeed 15、flashColor (0.84,0.82,0.82,1) / 代码 white、normalColor 白 / 代码 gray、targetIconColor 黑 → HUD_Obsession 表。闪烁频率 `Lerp(2,12,t)`(:160) 硬编码；阈值 `:37-39` Max 回退 1000、低阈 Max/3、高阈 ×2 硬编码 → HUD_Obsession.lowThresholdRatio / highThresholdRatio；字段名 buddha0_333、buddha334_666、buddha667_1000 隐含 Max=1000。

圈数动画 `LapAddLineUITrigger`（`:9,12`）：rebindIntervalSeconds 代码 1，两个实例 0.5（UIprefap.prefab:476）与 1（:823）；triggerName "LapAdd"（技术）。动画 `Assets/Animation/CG/AnimationLap/LapAnimUI.anim` m_StopTime 1 循环；`LapAnimUIControl.controller:38-49,123-126` LapAdd 触发退出时间 1；RaceMap 中 lapAddFeelPlayer 被覆盖为空（约 5614 行），运行时自动查找。

HUD 刷新与调试（不进表）：`LeaderboardTMPUI.localRefreshIntervalSeconds` 代码 `:18`、场景 RaceMap.unity:824 0.1，最小 0.02 (:59)；`PropertiesSelectorUI` / `SkillWallSelectionController` 诊断间隔 2 s（:352 / :979）。

## 3. 表 MiniMap（按地图分行）

- 世界边界 worldMinX/MaxX/MinZ/MaxZ — 代码 `UI/InGameUI/MiniMapController.cs:34-37`，prefab `UIprefap.prefab:1056-1059` — -653 / 469 / -1038 / 247 — `LateUpdate` UV 映射（:124-140，u 对应 Z，v 对应 X 反向，写死）→ MapUI.mapId / worldMin*/worldMax* — 边界与 RaceMap 绑定但组件在通用 HUD prefab。
- 贴图 `Assets/UI/UI资源/地图UI-PNG/地图 1.png`（UIprefap 与 RaceMap 引用）；遮罩 `地图蒙版.png/.mat`；RaceMap 中 mapImage m_SizeDelta.x 覆盖为 1120（约 RaceMap.unity:5619-5633）→ MapUI.minimapSprite / mapImageWidth。
- 其他（不进表）：movementSensitivity 1 (:1066)、clampToBounds 1、positionSource 0；playerTag prefab "Buddah"(:1049)，RaceMap 覆盖为 ""（5636-5637）；rebindEveryNFrames 10、staticFramesBeforeRebind 120、testAmplitude 200、testFrequency 1.5、enableDebugLogs 0。
- 已废弃字段（只声明从未读取，可删）：invertX、invertY、swapXY（"Legacy Flags" :39-42）、localPlayerOnly、followPlayerRoot、nameHint、detectExternalOverride、drawGizmos。

## 4. 表 SkillIcon（图标映射）

- `SkillUiSpriteLibrary.entries`（实际生效的 HUD 映射）— `UIprefap.prefab:1623-1656`（定义 `UI/InGameUI/SkillUiSpriteLibrary.cs:7-17`）— 6 行 skillId → black / white 图，均在 `Assets/UI/UI资源/文字PNG/New/`：acceleration 神足通.png / 神足通-白.png；blackcurtain 一念光明 / 一念光明-白；giant 法像金身 / 法像金身-白；push_projectile_hands 如来神掌 / 如来神掌-白；reverseturn 逆姻缘 / 逆姻缘-白；slowtrap 禅定缓流 / 禅定缓流-白 — 消费 `SkillSlotUI.RefreshSkillPresentation`(:178-182) 按 `SkillLoadout.GetSkillId(slot)` 忽略大小写 → SkillIcon.skillId / hudIdleSprite / hudActiveSprite。备注：6 个 *_anti 无图标；`blackSpriteName / whiteSpriteName` 全空且从未使用；文件名"逆姻缘"与显示名"逆因缘"不一致。
- `iconKey`（配置里有，UI 从未使用）— ProjectConfigDatabase_Main.asset:22-143 共 12 行（icon_acceleration、icon_slow_trap、icon_blackcurtain_anti 等）— 流向 `SkillDataBase.cs:186-199` → `SelectablePropertyOption.PreviewIconKey` → `SkillWallItemView._previewIconKey`(:76)，无任何解析 — 命名不一致：icon_slow_trap ↔ slowtrap；icon_blackcurtain_anti ↔ anti_blackcurtain。建议 SkillIcon 以 skillId 为主键，iconKey 作别名或删除。
- 技能槽按键 token 序列与图：`SkillSlot_1.prefab` tokens [W, Up, W]；`SkillSlot_2.prefab` [Up, W, Up]；`SkillSlot_3.prefab` tokens []，在 `UIprefap.prefab:2305-2356` 覆盖为 4 个（[2]、[3]=Up，[0]、[1] 未覆盖推断默认 W，即 [W, W, Up, Up]）；贴图 `UI资源/技能按键PNG/W.png`、`W-白 1.png`、`↑.png`、`↑-白 1.png`。重复 `ComboSkillInput.cs:77-78` 默认绑定与 Buddah.prefab bindings。建议由 ComboBinding 表生成，HUD 只配 Token → sprite 映射。被 AISkillSceneTests 锁定长度。
- 就绪图标 `技能准备就绪图标.png / -白.png`，5 个技能槽 prefab 静态引用。
- 执念佛头图：`ObsessionUIRoot.prefab` 引用 `UI资源/执着值PNG/二搞/头/执着值-佛头0/50/100.png`，三档切换阈值见第 2 节 → HUD_Obsession.tierSprite[0..2]。
- 名次图：`UI资源/排行PNG/壹..陆.png` 只有 壹.png 被 UIprefap RankingUI 静态引用；RankingUI 下 Player1..3 文本无脚本驱动。若做名次 HUD，建议 RankIcon 表。

## 5. 表 MenuOptions

- 最大人数：代码 `CreateRoomUI.cs:131-138,155-160` 兜底选项 {"4","5","6"} 默认下标 2（即 6），解析后 Clamp(4,6)，失败回退 4；场景 `MainMenu.unity:11446-11453` 选项 4/5/6，m_Value 0（即 4）→ MenuOptions.maxPlayers（min、max、default、list）— 代码默认与场景默认不一致；与 6 人上限耦合。
- 房间可见性：代码 `CreateRoomUI.cs:140-151,162-167` 下标写死 0=Public、1=FriendsOnly、2=Private，标签 "Friends Only"；场景 `:9671-9678` 标签 "FriendsOnly"，默认 0 → MenuOptions.visibility（index、enum、labelKey）。
- 菜单行为：`MainMenuUI.allowEscapeBack` 1、`escapeFromHomeQuits` 0 — 代码 `MainMenuUI.cs:30-31`，场景 `:9905` 起。
- 离开房间后场景：`RoomUI.mainMenuSceneName` 代码 `:36` "" / 场景 `:1256` "MainMenu" — 技术配置。
- 轮播布局开关 key：`PropertyGroupView.cs:148-149` 写死 "map"、"skin"；`PropertiesSelectorUI.cs:235` 写死 "map" — 建议放选择阶段表 layoutMode 列。
- 单人赛 AI 数量与难度：`SoloMatchSettings` 允许 0..5（测试锁定）；UI 只有固定文案"玩家 + 5 AI"，无 AI 数量选择器。

## 6. 表 Presentation_Timeline（UI 或开场引用的 timeline / animator）

- `TransitionToRaceMap.playable`（RaceMap.unity:2660）：Cinemachine Shot1 0–7 s、Shot2 7–14 s、Gameplay 14–18 s（:25-148）；动画轨 m_StopTime 18 / 15 / 14.97 / 18（:404,:649,:864,:1080）；18 s 进入 `IntroSequenceManager.GetConfiguredIntroDurationSecondsServer`（RaceIntro/IntroSequenceManager.cs:470-476）取 Max(timeline, 最长样条时长)，决定 GO 时刻；回退 fallbackIntroDuration 3（:40，RaceMap.unity:14493）。建议 RaceIntro.introDurationSec 或表中只读显示 timeline 导出值。
- `EndResultSwitchTimeline.playable`（RaceMap.unity:6832）：CM_End 1–6 s；信号 SIG_Result_BlackFullyCovered t=1、SIG_Result_TimelineFinished t=3（:327,:382）；动画轨停 3 s (:183)；消费 ResultPresentationTimelineBridge，另有回退协程。
- `LocalPlayerFinishTimeline.playable`（RaceMap.unity:3228）：两条 m_SizeDelta.y 轨各 1 s（:177,:297）；消费 LocalFinishPresentationTimelineBridge。
- `PropertySelectionEnd.playable`：2 s，见第 2 节。
- `CrossFade.controller` 及 3 个动画（In 1 s、out 1 s、IdleTransparent 0 s）：无任何场景、prefab 或资产引用；SceneFadeController 靠扫描 Animator 查找，实际只通过 `PropertySelectionMatchTransitionTimeline.RegisterPersistentFadeCarrier` 注册的 CanvasGroup 生效。
- `LapAnimUI.anim / LapAnimUIControl.controller`：见第 2 节。`冲线白色线条.prefab`（60 帧序列）无引用。`BuddahAnim/*` 角色动画不在本节。

## 7. 字体（影响配表文案能否显示）

- `Assets/Fonts/SoloCJK/BuddahGoSoloCJK.asset`：Noto Sans CJK SC 子集，静态 2048 图集，只有 499 个码位（README.txt），从"项目 C# 和序列化 UI 中出现的字符"抽取；只被 Solo UI 的 font 字段引用（MainMenu.unity:9083、PropertySelection.unity:4093、RaceMap.unity:11025）。中文文案进 Excel 后新字符不会自动进子集，导出流程必须重新生成子集并重烘图集。
- TMP 默认字体 `Assets/Plugins/TextMesh Pro/Resources/TMP Settings.asset:24` LiberationSans SDF，`m_fallbackFontAssets: []`(:32) 无中文回退；英文 UI 改中文会显示方块。
- 缺失字体 GUID `a3e319c71cf3da54782eca754363d2a9`：引用于 PropertySelection.unity:1003,2540,4206（检视面板标题/描述/提示）、技能绘马.prefab:223（技能名标签），正是显示中文技能名与描述的地方；工程里找不到对应 .meta。

## 8. 重复与遗留来源

1. 排行榜行格式两份：`LeaderboardManager.cs:284-288`（服务器快照）与 `LeaderboardTMPUI.cs:258-259`（客户端回退）。
2. 代码默认与 prefab/场景不一致：SkillInspectPanelUI.fadeDuration 0.18/0.35；PropertyCarouselView.animationDuration 0.25/0.5；SkillWallSelectionController moveDuration 0.35/0.6、水平间距 1.2/0.5、垂直间距 0.8/0.2；SkillWallItemView defaultFlightDuration 0.35/0.5 与颜色；ObsessionUI maxShakeStrength 3/5 与颜色；PropertySelectionMatchTransitionTimeline.fallbackDurationSeconds 1/2；PropertiesSelectionManager._sceneTransitionFadeDurationSeconds 1/2；LapAddLineUITrigger.rebindIntervalSeconds 1/0.5（两实例不同）；CreateRoomUI 最大人数默认下标 2 / 场景 0。
3. "选择 → 比赛"过场 2 s 三个来源：timeline 2 s、fallbackDurationSeconds 2、_sceneTransitionFadeDurationSeconds 2。
4. 技能图标两套 key：SkillUiSpriteLibrary（skillId，在用）vs iconKey / PreviewIconKey（没用，命名不一致）。
5. 技能槽 token 序列与 ComboSkillInput 绑定重复；SkillSlot_3 依赖 UIprefap 覆盖值。
6. `SkillSlotUI.fallbackCastLockSeconds=2` 与技能配置 castLockSeconds 重复。
7. 执念阈值按 1/3、2/3 写死；回退 Max=1000 与 ObsessionFigure.Max 重复；佛头字段名隐含 333/666/1000。
8. "Esc to close" 与 `InputSystem_Actions.inputactions:163-167` MenuCancel=escape 重复；改键后文案不会变。
9. 可见性标签：代码 "Friends Only" vs 场景 "FriendsOnly"。
10. Legacy 目录两个脚本仍在使用，不是死代码：LeaderboardTMPUI 由 RaceMap.unity:819 挂载（Online 显示排行榜，Practice 清空，遥测只写日志）；ResultDecisionUI 由 RaceMap.unity:4294 挂载，VoteOnResults=false 时 CanvasGroup 隐藏。
11. 遗留或无用字段/资产：PropertiesSelectorUI "Legacy Optional" readyButton / readyButtonText 永远隐藏或清空(:163-168)；PropertyCarouselView.scrollRect 被强制禁用(:61-65)；MiniMap 8 个从未读取字段；SkillUiSpriteLibrary *SpriteName 字段；CrossFade.controller 与 冲线白色线条.prefab 无引用；SceneFadeController 不在任何场景；UIprefap RankingUI 静态样板；UI Toolkit PanelSettings 无引用。
12. 未引用 UI 贴图（清理候选）：排行PNG/ 伍、叁、肆、贰、陆；地图UI-PNG/地图.png；执着值PNG/ 根目录全部（佛头0/50/100、点、线）；执着值PNG/三稿/执着值_点-黑.png；执着值PNG/二搞/ 5 张非"头"图；技能按键PNG/ W-白.png、↑-白.png、渐变.png。
13. Match/UI 运行时代码生成 UI，中文文案与布局常量全硬编码，与 Scripts/UI 英文风格不统一。

## 9. 会烘焙或生成设计数据的编辑器工具

真正写出设计数据：
- `AIDifficultyAssetTool.Build`（`Assets/Editor/AIDifficultyAssetTool.cs:11-28`）：读 `Tools/ai/difficulty-{easy,normal,hard}.json`，`JsonUtility.FromJsonOverwrite` 写入 `Assets/Resources/AI/{Easy,Normal,Hard}.asset`；无菜单项，batch 模式自动退出。现成的"JSON → SO"烘焙范式，可复用作 Excel 导出管线。
- `TrackWallFixtureTool.Build`（`Assets/Editor/TrackWallFixtureTool.cs:16-32`）：打开 RaceMap 射线测墙距，生成 `Tools/ai/fixtures/track-walls.json`；测试 fixture，赛道改动后须重新生成。

只做校验：
- `ProjectConfigValidationMenu`（`Assets/Scripts/Editor/Config/ProjectConfigValidationMenu.cs:8`，菜单 Tools/Project Config/Validate All Databases）与 `SkillConfigCrossValidator`（:11-120）：对比 SkillAction 与 ProjectConfigDatabase 的 cooldown、castLock、obsessionGain、antiSkillId、vfxId、skillEffectParams，只报告；不检查 SkillUiSpriteLibrary、iconKey、文案或字体覆盖，可作为导表后校验挂点。
- `NetworkObjectValidator`（[InitializeOnLoad]）与 `NetworkManagerValidator`（EditorWindow）：只校验网络对象。

其他（与设计数据无关）：`MatchScenePlaceholderSetup`（`Assets/Editor/MatchScenePlaceholderSetup.cs:13-131`，生成 `Assets/Scenes/MatchScene.unity` 与占位玩家，出生点写死；该场景当前不存在，遗留工具）；`A1DrivingTools`、`AISkillAcceptanceTools`（测试回执）、`ThrustVectorAcceptanceBuild`、`AssetUsageReport`、`VfxImportRecovery`。没有任何工具处理 UI 文案、图标映射、字体子集（目前外部 fonttools 手工生成，见 `Assets/Fonts/SoloCJK/README.txt`）。

## 10. 会因数值搬进表而失败的测试

- `Assets/Tests/EditMode/LeaderboardHudTests.cs`：:40-42、:56-57、:65 锁定行格式 "1. Ada - Lap 4 - 100.0% - Finished #1"；:87-88 锁定诊断文案（"Race Status:"、"Your Progress:"）不得出现；反射访问私有字段 outputText、maxRows（:27,:49）与私有方法 RenderRankings（:81）。
- `SixRacerResultsTests.cs:62-67`：锁定 `SoloResultsView.FormatResults` 输出 "6.  Racer5     DNF" 与时间格式 "01:35.000"、"04:54.000"。
- `MatchRulesTests.cs:46` 与 `SoloSessionFlowTests.cs:127`：锁定 DefaultPlayerName == "玩家"。
- `SoloMatchSettingsTests.cs:23-35`：锁定 AI 数量 0..5。
- `AISkillSceneTests.cs:57-61`：从 Buddah.prefab 读组合键序列，锁定长度 3 / 3 / 4。
- `AISkillDecisionTests.cs:25,43`：读 SkillPersonalities.json，锁定 5 种人格。
- `AIDifficultyProfilesTests.cs`：读 Resources/AI/*.asset；:13 要求三档 UseWallCorridor=true，:23 H ≤ N 的 MaxThrustAngle，E≥N≥H 的 RolloutWallWeight。
- `SixRacerFoundationTests.cs:36-68`：锁定 RaceMap 6 个出生点、间距大于 8，开场布局覆盖 4..6 人。
- `LoadoutRulesTests.cs:35`：锁定错误文案含 "invalid skillId"。
- 无测试覆盖：MiniMap、ObsessionUI、SkillSlotUI、SceneFade、选择阶段全部 UI 参数、第 6 节 timeline 时长。搬进表不会挂测试但无回归保护。
- `IntroHandoffTimingTests`、`PlayerCameraIntroExitTests` 等只用字面量测纯函数，不依赖资产数值。

## 待确认问题

1. 本地化方案：Unity Localization 包、自建 UI_Text 表，还是先只统一语言？
2. 字体重烘流程：499 码位静态图集要求导表后重生成子集并重烘，是否纳入导表流程？英文 UI 的 LiberationSans 无 CJK 回退，换中文前先定字体策略。
3. 缺失字体 GUID a3e319c7… 原本是哪个字体资产？
4. 图标表主键 skillId 还是 iconKey？*_anti 在 HUD 是否需要独立图标？
5. 技能槽 HUD token 是否改由 ComboBinding 表驱动，去掉 prefab tokens 与 UIprefap 覆盖？
6. 小地图边界与贴图只服务 RaceMap；MapUI 列挂在哪张地图表上？
7. AnimationCurve（fade / move / hover / flight / alpha / transition）在 Excel 里怎么表达：预设曲线 id 还是留在 prefab？
8. 调试与技术参数（rebind 帧数、相机优先级、playerTag、triggerName、刷新间隔）是否明确排除？
9. 过场时长唯一来源：timeline 资产本身（TransitionToRaceMap 18 s、PropertySelectionEnd 2 s）还是表覆盖？TransitionToRaceMap 直接决定 GO 时刻。
10. 跨范围文案（SteamLobbyManager message、ResultDecisionManager.BuildDecisionSummary、SelectablePropertyDefinition.DisplayName）需与对应负责人对齐 key。
11. `ResultDecisionUI` "Your Choice: {enum}" 直接显示枚举名，配表时是否同时修正？
