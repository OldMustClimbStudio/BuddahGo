# 新菜单接入与选择场景退役

分支 `feat/menu-property-selection`（2026-11-06 起）。目标：`MenuEnvironmentSetting` 成为正式主菜单；`PropertySelection` 场景退役，地图、技能、皮肤都在菜单里选，开局直接进赛道。退役清单见 [retired.md](retired.md)。

## 用户决定（2026-11-06）

| 问题 | 决定 |
|---|---|
| 主菜单场景 | `MenuEnvironmentSetting` 取代 `MainMenu`，旧场景和旧 uGUI 退役 |
| 联机 | 本分支一起接：道场创建 / 加入房间、房间名单、准备、开局 |
| 皮肤 | 菜单里选的皮肤要在比赛里生效（以前选择场景的皮肤阶段没有读取方） |
| 地图 | 保留地图选择这一步，现在只有 RaceMap 一张也要接进流程 |
| 结算后“继续游戏” | 未回答。暂定：直接重开当前地图，沿用各自配装；PR 里标出 |

## 流程

```text
旧：MainMenu ──开局──▶ PropertySelection（地图投票 → 技能三槽 → 皮肤）──▶ RaceMap
新：MenuEnvironmentSetting（技能页 / 皮肤页 / 道场沙盘选地图）──开局──▶ 地图场景
```

- **本地配装**：技能三槽、皮肤、地图存在 `PlayerPrefs`（`LocalMatchSelection`），菜单打开时恢复。技能页拖绘马、皮肤页换皮、沙盘选图都写这里。
- **上报房间**：`RoomPlayerState` 加 `Skill0..2`、`SkinId`；客户端连上房间和本地配装变化时发 `RoomStateManager.SubmitSelection`，服务器用 `LoadoutRules` + `SkillDatabase` 校验。地图是房间级 `SyncVar`，只有房主能改。
- **开局**：`StartGameServerRpc` / Solo 自动开局 → `TransitionToMatchServer`：按房间名单写 `ResolvedPropertySelectionCache`（不完整的技能槽按旧规则自动补全），广播淡出，`LoadGlobalScenes(地图场景)`。`MatchSpawnManager` 读 cache 的路径不变。
- **皮肤生效**：`RoomPlayerState` 同步到所有客户端，赛车上的 `RacerSkinApplier` 按 owner 的 playerId 查皮肤 id，从目录取材质换到模型上。AI 用默认皮肤。
- **目录**：`SelectionCatalog`（`Resources/Match/SelectionCatalog.asset`）：皮肤 id → 名字、短句、材质、名字书法；地图 id → 场景名、名字。原来写死在 `MenuTitleHomeBuilder.SkinList` 的五个皮肤搬进来，菜单和比赛读同一份。材质引用放不进 xlsx，所以用 ScriptableObject；文字以后再接 `UI/Text`。
- **网络对象**：旧 `MainMenu` 里的 NetworkManager 物体（FishNet、GameNetworkManager、SteamLobbyManager、Multipass[Facepunch, Yak]……）做成 prefab，放进新菜单场景。`SceneNames.MainMenu` 指向 `MenuEnvironmentSetting`，序列化的 `MainMenu` 场景名一起改。

## 菜单里的接线

| 位置 | 现在 | 接入后 |
|---|---|---|
| 道场 · 单人 / 练习 | 已调 `StartSoloHost` | 不变；开局前确保配装已写入本地 |
| 道场 · 创建房间 | 只播镜头 | `SteamLobbyManager.CreateLobby`，成功后进殿 |
| 道场 · 加入 | 写死的三间示例房 | `RefreshLobbyList` 的真实列表 + 按房号（lobby id）加入 |
| 殿内席位 | `MenuLobby` 本地模拟、`guestNames` | `RoomStateManager.Players`：名字、房主、准备 |
| 准备 / 开局 | 无准备；`StartOnlineRequested` 无人订阅 | 客人切换准备；房主 `RequestStartGame`（`CanHostStartGame`） |
| 离开房间 | 只播镜头 | `SteamLobbyManager.LeaveLobby` |
| 沙盘 | 只看 | 地图列表（现只有 RaceMap），房主点选，客人看到房主的选择 |
| 赛后回房间 | 旧场景看到 `IsInLobby` 直接显示房间 | 新场景载入时若在房间里，直接进殿 |
| Steam 不可用 / 启动失败 | 旧 `SoloUIBootstrap` 文字 | 道场入口提示 `SteamUnavailableMessage` / `LastError` |

## 不在本分支

- 殿内“形象 / 技能”两项跳到皮肤 / 技能页再回殿（现在提示“尚未接入房间流程”）：先保留提示，配装在进殿前选好。
- AI 皮肤多样化、皮肤解锁。
- 多套预设（原型 S8–S15）。
