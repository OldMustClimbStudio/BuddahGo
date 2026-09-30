# P1 删除预检与验证

阶段基线：P0 `d6439a6`，包含独立联机修复 `be7c0fd`。按用户要求使用 stacked PR，改动后集中执行阶段构建/本机回归，各步骤保留独立提交。

## P1-1

Windows 下 refscan 原本用 POSIX 前缀过滤 os.walk 路径，会漏掉所有脚本；现将比较路径按 os.sep 归一化后再过滤。修复后实际运行脚本的结果如下：

| 类型 | YAML 引用 | 其他 C# 文件引用 | 启动钩子 |
|---|---:|---:|---|
| DebugNetworkTester | 0 | 0 | False |
| DebugRaceFinishButton | 0 | 0 | False |
| DebugLobbyTester | 0 | 0 | False |
| LobbyFlowDebugOverlay | 0 | 0 | False |
| PredictionApiSpike | 0 | 0 | False |
| BuddahPredictionGateAdapter | 0 | 0 | False |
| BuddahPredictionInputAdapter | 0 | 0 | False |
| BuddahPredictionIntroAdapter | 0 | 0 | False |
| BuddahPredictionRespawnAdapter | 0 | 0 | False |
| BuddahPredictionSkillAdapter | 0 | 0 | False |
| BuddahPredictionEventIds | 0 | 0 | False |
| LeaderboardUI | 0 | 0 | False |
| LobbyRoomListItemUI | 0 | 0 | False |

正向对照：BuddahPredictionCombatAdapter 的 C# 引用为 1，保留。额外搜索 Assets 的 scene/prefab/asset/playable/json 未发现待删除类型名绑定。删除文件同时删除 .meta；Testing/Sandbox 的空目录 meta GUID 无其他资源引用，因此一起移除，避免新增孤立 meta 警告。

I6：不适用（没有合并重复实现）。R1/R2：待 P1 阶段集中验证，静态检查不视为编译或运行通过。

## P1-2

预检：MiniMapPresenter 的 YAML/C# 引用为 0/0；MiniMapPlayerLocator、MiniMapWorldMapper 为 0/1，唯一引用方均为同一删除集合内的 MiniMapPresenter。没有启动钩子，Assets 中无三者的序列化类型名绑定。现用 MiniMapController 与该组无依赖且文件未修改。删除三个文件及其 meta。

I6：不适用。R1/R4（小地图）：待阶段集中验证。
