# 菜单接入退役记录

`feat/menu-property-selection`（设计见 [menu-integration.md](menu-integration.md)）退役的场景、代码、资产、配表和测试都记在这里。2026-11-06 一次清理（用户规则：PR 收尾时一次删干净，再跑全量测试）。

状态：**已删**（从工程移除）、**已改**（还在，改成新流程）、**保留**（看着像旧东西但仍有读取方）。

## 场景

| 内容 | 状态 | 说明 |
|---|---|---|
| `Assets/Scenes/PropertySelection.unity` | 已删 | 地图 / 技能 / 皮肤改在菜单里选，开局直接进地图场景 |
| `Assets/Scenes/MainMenu.unity` | 已删 | `MenuEnvironmentSetting` 是主菜单；NetworkManager 物体和 SoloUI 字体引导先移进新场景 |
| Build Settings | 已改 | 只剩 `MenuEnvironmentSetting`、`RaceMap` |
| 新菜单殿内的 `TEMP_LobbyJoinButton` / `TEMP_LobbyLeaveButton` / `TEMP_LobbyCount`（模拟加入 / 退出） | 已删 | 席位跟房间名单走，模拟按钮无效；`MenuLobby` 的对应字段和导入器里的生成代码一起删 |

## 代码：选择场景

| 内容 | 状态 | 说明 |
|---|---|---|
| `PropertiesSelectionManager`、`PlayerPropertySelection`、`SelectablePropertyDefinition`（含 `PropertySelectionMode`） | 已删 | `RoomStateManager` 收选择、补全配装、直接进地图 |
| `PropertiesSelectorUI`、`PropertyCarouselView`、`PropertyGroupView`、`PropertyOptionItemView`、`PropertySelectionMatchTransitionTimeline`、`PropertySelectionStageSceneController`、`SkillWallSelectionController`、`SkillInspectController`、`SkillInspectPanelUI`、`SkillSelectionSlotAnchor`、`SkillWallItemView`、`SkillInspectableItem`、`SkillWallItemInteractionRelay`、`ISkillSelectionFocusProvider` | 已删 | 只在 PropertySelection 场景和它的 prefab 里 |
| `Assets/UIMenus/PropertySelector/`（`PropertyGroupView`、`PropertyOptionItemView`、`技能绘马` prefab，`CardSelection` 美术） | 已删 | 只被 PropertySelection 场景引用 |
| `RoomStateManager` 的 `TransitionToPropertiesSelector`、`ValidatePropertiesSelectionManagerServer`、`_propertiesSelectorSceneName`、`_mainMenuSceneName`（序列化）、`OnPropertiesSelectorTransitionRequested` | 已改 | 改为 `TransitionToMatchServer`、`OnMatchTransitionRequested`；菜单场景名只从 `SceneNames` 读 |
| `ResultDecisionManager.nextScenePropertySelection / returnSceneMainMenu` | 已删 | RaceMap 场景里还留着这两个序列化值，Unity 下次保存该场景时丢掉，运行时无读取方 |
| `SessionLauncher.Pump` 等 `PropertiesSelectionManager` 就绪 | 已改 | 改为等房间离开 `InRoom` |
| `SceneNames.PropertySelection` | 已删 | |
| `IMatchRules.SelectionTimeoutEnabled / SkipMapVote` 及两份规则实现 | 已删 | 只有选择场景读 |
| `ResolvedPropertySelectionCache`、`LoadoutRules`、`SelectablePropertyOption` | 保留 | 移到 `Network/Session/MatchSelection/`；`MatchSpawnManager`、`MatchLoadoutResolver`、`SkillDatabase.BuildSelectableSkillOptions` 在用 |

## 代码：旧主菜单 uGUI

| 内容 | 状态 | 说明 |
|---|---|---|
| `MainMenuUI`、`CreateRoomUI`、`LobbyBrowserUI`、`LobbyBrowserListItemUI`、`RoomJoinUI`、`RoomUI`、`RoomPlayerEntryUI`、`SettingsPanelUI`、`SoloSetupPanel` | 已删 | 道场页（`MenuDojoPage` + `MenuRoomSession`）和设置页（`MenuSettingsPage`）取代 |
| `Assets/UIMenus/Lobby/LobbyBrowserListItem.prefab`、`RoomPlayerEntry.prefab` | 已删 | `RoomStateManager.prefab` 保留 |
| `Assets/Editor/UI/SettingsPanelBuilder.cs` | 已删 | 只给旧场景搭设置面板 |
| `SoloUIBootstrap.BuildMenu`（单人按钮、联机可用性文字） | 已删 | 留字体回退和 RaceMap 的退出 / 结算界面 |
| `MenuDojoPage.JoinRequested / StartOnlineRequested` | 已删 | 无人订阅的占位钩子，换成真实房间流程 |
| `MenuTitleHomeBuilder.SkinList` | 已删 | 皮肤搬进 `SelectionCatalog` |

## 配表

| 内容 | 状态 | 说明 |
|---|---|---|
| `Match/SelectionStage`、`Match/SelectionOption` 两张表、记录类、`scope.json` 与 `export-map.json` 登记 | 已删 | 读取方是 `PropertiesSelectionManager` |
| `Match/ModeRule` 的 `selectionTimeoutEnabled`、`skipMapVote`、`selectionStageSeconds` 列 | 已删 | 同上 |
| `Global/__enums__` 的 `PropertySelectionMode`、`SelectionLayout` | 已删 | 只给上面两张表用 |
| `UI/Text` 的 `selection.stage.*`、`selection.option.*` 15 行 | 已删 | 同上 |
| `UI/Text` 里来源文件已删的 141 行自动提取文案：`ui.propertiesselectorui.*` 25 行，以及 `PropertiesSelectionManager`、`SoloSetupPanel`、`CreateRoomUI`、`RoomUI`、`LobbyBrowserUI` 等脚本和 `MainMenu.unity`、`PropertySelection.unity`、两个 prefab 的 116 行 | 已删（2026-11-07，PR #75 合入前） | 来源随本页的场景、代码和 prefab 一起删了；逐个 key 搜过代码、场景、prefab、资产和表，没有读取方。3aecb17a（25 行）、c83bb977（116 行） |
| `UI/UiTiming` 里已删组件的 35 行（`SkillInspectPanelUI`、`SkillInspectController`、`SkillWallItemView`、`SkillWallSelectionController`、`PropertyCarouselView`、`PropertySelectionStageSceneController`、`PropertySelectionMatchTransitionTimeline`、`PropertiesSelectionManager`） | 已删（2026-11-07） | 组件已删；UiTiming 是只读展示表，生成物不变。42106eb1 |
| `Assets/Animation/CG/PropertySelectionEnd.playable`，以及它在 `Global/Timeline`、`UI/UiTiming` 的两行 | 待定 | 选择场景的结束 timeline，场景删了以后全工程 0 引用（2026-11-07 按 GUID 查）。`Global/Timeline` 由 timeline 资产导出，删资产时两行一起删；没有替用户删 |
| `UI/MenuOption`、`ModeRule.defaultDifficulty` | 保留 | 原读取方 `CreateRoomUI` / `SoloSetupPanel` 已删；改由 `MenuDojoPage` 读（新建房间的人数上限与可见性、单人默认难度） |

## 测试与诊断

| 内容 | 状态 | 说明 |
|---|---|---|
| `SoloSessionFlowTests` | 已改 | 从新菜单场景开 Solo / 练习，直接进 RaceMap；新增“赛车用菜单的配装和皮肤”“再来一局保留配装”检查 |
| `SoloTransportTests` | 已改 | 打开新菜单场景找 NetworkManager |
| `DesignTableTests` 里选择表的两条断言 | 已删 | |
| `SoloHarnessFlow` / `MechanicsPracticeHarness.PracticeFlow`、`AISkillLifecycleHarness` 的选择阶段步骤 | 已改 | 合成一个 `SoloHarnessFlow`：写菜单选择 → 开局 → 还原玩家自己的选择 |

## 文档

入口文档（README、架构、会话辅助、训练验证、Unity MCP、画质设置、配表规范）已改成新流程。`Docs/design-tables/inventory-match-flow.md`、`Docs/single-player/design.md` 是当时的调查记录，只在开头加了“已被取代”说明；`Docs/code-review/`、`Docs/archive/`、ADR 不改。
