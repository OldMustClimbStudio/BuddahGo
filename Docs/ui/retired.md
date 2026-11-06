# 菜单接入退役记录

`feat/menu-property-selection`（设计见 [menu-integration.md](menu-integration.md)）退役的代码、场景、资产和测试记在这里，PR review 时一次删干净，再跑全量测试（用户规则）。

状态：**待删**（新路径已接上，等统一清理）、**等替换**（新路径还没接完）、**保留**（看着像旧东西但仍有读取方）、**已删**（写明提交）。

## 场景

| 内容 | 状态 | 先清掉的引用 |
|---|---|---|
| `Assets/Scenes/PropertySelection.unity` | 等替换 | Build Settings、`SceneNames.PropertySelection`、`RoomStateManager._propertiesSelectorSceneName`（prefab 序列化）、`ResultDecisionManager.nextScenePropertySelection`（RaceMap 序列化）、`ThrustVectorAcceptanceBuild`、`SoloUIBootstrap` |
| `Assets/Scenes/MainMenu.unity` | 等替换 | Build Settings、`SoloTransportTests`、`SoloSessionFlowTests`、`SettingsPanelBuilder`、诊断 harness 的场景名检查、`A1DrivingTools` |

## 代码：选择场景

| 内容 | 状态 | 说明 |
|---|---|---|
| `PropertiesSelectionManager`、`PlayerPropertySelection`、`SelectablePropertyDefinition`、`SelectablePropertyOption` | 等替换 | `SkillDataBase.BuildSelectableSkillOptions` 和诊断 harness 还在用 |
| `PropertiesSelectorUI`、`PropertyCarouselView`、`PropertyGroupView`、`PropertyOptionItemView`、`PropertySelectionStageSceneController`、`PropertySelectionMatchTransitionTimeline`、`SkillWallSelectionController`、`SkillInspectController` | 等替换 | 只在 PropertySelection 场景里 |
| `RoomStateManager` 的 `TransitionToPropertiesSelector`、`ValidatePropertiesSelectionManagerServer`、`OnPropertiesSelectorTransitionRequested` | 等替换 | 改成直接进地图 |
| `SessionLauncher.Pump` 里等 `PropertiesSelectionManager` 就绪的判断 | 等替换 | 改成等房间开局 |
| `ResolvedPropertySelectionCache`、`LoadoutRules` | 保留 | 新路径继续用 |
| 配表 `Match/SelectionStage`、`Match/SelectionOption`、`ModeRule.selectionStageSeconds / selectionTimeoutEnabled / skipMapVote` | 待定 | 看新路径要不要读；不读就退役，改 xlsx 后重新导入 |

## 代码：旧主菜单 uGUI

| 内容 | 状态 | 说明 |
|---|---|---|
| `MainMenuUI`、`CreateRoomUI`、`LobbyBrowserUI`、`LobbyBrowserListItemUI`、`RoomJoinUI`、`RoomUI`、`RoomPlayerEntryUI`、`SettingsPanelUI` 与 `Assets/UIMenus/Lobby/` 下的列表项 prefab | 等替换 | 只在旧 MainMenu 场景；`RoomStateManager.prefab` 本身保留 |
| `SoloUIBootstrap.BuildMenu`、`SoloSetupPanel` | 等替换 | 道场页取代；字体回退那部分保留 |

## 测试

| 内容 | 状态 | 说明 |
|---|---|---|
| `SoloSessionFlowTests` 里走 PropertySelection 阶段和 `MainMenuUI` 面板的步骤 | 等替换 | 改成从新菜单场景开 Solo，直接进 RaceMap |
| 诊断 harness（`AISkillLifecycleHarness`、`HarnessSupport`、`MechanicsPracticeHarness`）里提交技能 / 皮肤阶段的步骤 | 等替换 | 改成写本地配装后开局 |
