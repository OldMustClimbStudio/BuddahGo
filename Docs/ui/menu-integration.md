# 新菜单接入与选择场景退役

分支 `feat/menu-property-selection`（2026-11-06）。`MenuEnvironmentSetting` 是正式主菜单；`PropertySelection` 场景退役，地图、技能、皮肤都在菜单里选，开局直接进赛道。删了什么见 [retired.md](retired.md)。

## 用户决定（2026-11-06）

| 问题 | 决定 |
|---|---|
| 主菜单场景 | `MenuEnvironmentSetting` 取代 `MainMenu`，旧场景和旧 uGUI 退役 |
| 联机 | 本分支一起接：道场创建 / 加入房间、房间名单、准备、开局 |
| 皮肤 | 菜单里选的皮肤在比赛里生效（以前选择场景的皮肤阶段没有读取方） |
| 地图 | 保留地图选择这一步，现在只有 RaceMap 一张也要接进流程 |
| 结算后“继续游戏” | 未回答。现做法：直接重开当前地图，沿用各自配装（中途经过菜单场景，黑屏不露出） |

## 流程

```text
旧：MainMenu ──开局──▶ PropertySelection（地图投票 → 技能三槽 → 皮肤）──▶ RaceMap
新：MenuEnvironmentSetting（技能页 / 皮肤页 / 道场沙盘选地图）──开局──▶ 地图场景
```

- **本机选择**：`LocalMatchSelection`（`Match/Selection/`）把技能三槽、皮肤、地图存进 `PlayerPrefs`，菜单打开时恢复。技能页拖绘马（`MenuSkillLoadout`）、皮肤页换皮（`MenuSkinPage`）、沙盘上 ← →（房主）都写这里。
- **目录**：`SelectionCatalog`（`Resources/Match/SelectionCatalog.asset`）：皮肤 id → 名字、短句、材质、名字书法；地图 id → 场景名、名字。菜单和比赛读同一份。材质放不进 xlsx，所以用 ScriptableObject。白玉用 `DefaultSkin`（菜单佛像自己的材质），赛车 prefab 默认是银身，不能留空。
- **上报房间**：`RoomPlayerState` 加 `Skill0..2`、`SkinId`。客户端连上和本机选择变化时发 `SubmitSelectionServerRpc`，服务器用 `MatchLoadoutResolver`（`LoadoutRules` + `SkillDatabase`：已解锁、重复策略）校验，非法整组拒收、皮肤 id 规范化。地图是房间级 `SyncVar`（`MapId`），只有房主能改，开局前锁定。
- **开局**：房主 `RequestStartGame`（客人都已准备）或 Solo 自动开局 → `TransitionToMatchServer`：按名单补全每人配装写进 `ResolvedPropertySelectionCache`（房主的选择直接读本机，避免 Solo 自动开局时 RPC 还没到），广播淡出并保持黑屏，`LoadGlobalScenes(地图场景, ReplaceAll)`。`MatchSpawnManager` 读 cache 的路径不变。
- **皮肤生效**：赛车 prefab 上的 `RacerSkinApplier` 在每台机器上等到身份和名单都到了，按 owner 的皮肤 id 把目录材质换到身体网格。AI 和木桩保留 prefab 材质。
- **再来一局**：FishNet 不会重载已加载的场景（`ReplaceScenes` 跳过要加载的同名场景），所以先全局加载菜单场景，服务器在菜单加载完时立刻再开局；全程黑屏。
- **回房间**：结算选“回房间”时全局加载菜单场景、保留会话；菜单发现自己还在 Steam 大厅，跳过开场直接进殿。
- **淡出**：唯一的 CrossFade 载体在选择场景的 timeline 里，已随场景退役；`SceneFadeController` 没有载体时自建全屏黑色遮罩。
- **网络对象**：旧 `MainMenu` 的 NetworkManager 物体（FishNet、GameNetworkManager、SteamLobbyManager、Multipass[FishyFacepunch, Yak]……）和 SoloUI 字体引导已移进新菜单场景；`SceneNames.MainMenu` 指向它，菜单场景名不再序列化。

## 道场（`MenuDojoPage` + `MenuRoomSession`）

| 位置 | 行为 |
|---|---|
| 创建房间 | `SteamLobbyManager.CreateLobby`：人数上限和可见性取 `UI/MenuOption` 的默认行（现为 4 人、公开），房名 `roomName`，四位房号写进 lobby 数据 `l_code`；成功后进殿。没有 Steam 时直接进殿，只能单人修行 |
| 加入房间 | 打开时刷新 Steam 公开房间列表填进行；键入四位房号按 Enter 用 `JoinLobbyByCode` 找房（好友 / 私密房间搜不到） |
| 殿内席位 | 人数跟 `RoomStateManager.Players`；房主坐房主位，其他人按座位顺序；名字后标 房主 / 已准备 / 未准备，自己显示“你” |
| 准备 / 开局 | 客人 Enter 切换准备；房主在所有客人准备后 Enter 开局，否则提示等准备；连上房间但名单还没到时显示“正在连接”，什么都不能开 |
| 单人 / 练习 | 离线或房主独自在线时显示难度、单人修行、练习。在线时先关房间（离开 lobby、停会话），`SessionLauncher.QueueSoloAfterStop` 在传输层空闲后开单人局，不重载菜单。默认难度取 `ModeRule` Solo 行的 `defaultDifficulty`，之后记住上次选择 |
| 离开房间 | 房主有客人时先确认（解散 / 留下）；离开 lobby 后会话停止，菜单原地回到殿门（不重载场景）。房主离开时客人提示“房主已离开，房间解散” |
| 连接失败 | 进了 lobby 但会话没起来超过 1.5 秒：离开 lobby 并提示原因 |
| 沙盘 | 标签写当前地图名；看沙盘时房主 ← → 换图（只有一张时提示），客人提示由房主选 |
| 启动失败 | 单人局在离开菜单后失败、回到菜单时，道场页取回失败请求并把错误写在标签上 |

## 验证

- 每个提交单独编译（`git archive` 出的源码对着工程的 csproj 编译）；Unity 2022.3.55f1c1 batch 模式导入通过、配表导入 0 错误。
- EditMode 全量见 PR 描述。`SoloSessionFlowTests` 改为从新菜单开 Solo / 练习，并检查赛车用菜单的配装和皮肤、再来一局保留配装。
- 联机（Steam 双端）只做了代码走查：创建 → 加入 → 名单 / 准备 → 开局 → 各自配装和皮肤 → 再来一局 / 回房间 / 离开。真机双端待用户测。

## 不在本分支

- 殿内“形象 / 技能”两项跳到皮肤 / 技能页再回殿：现在提示“进房前在主页里选好”。
- AI 皮肤多样化、皮肤解锁、多套预设（原型 S8–S15）。
- `MenuOption` 的人数 / 可见性只读默认行，菜单里还没有改它们的入口。
