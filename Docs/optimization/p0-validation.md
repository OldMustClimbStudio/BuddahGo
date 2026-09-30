# P0 验证记录

日期：2026-09-29（America/New_York）。目标分支：`refactor/architecture-optimization`。

## P0-3：正式包编译修复

- 预检：逐个检查 motor 中 20 个 Editor/Dev 条件字段。唯一未守卫的有效代码引用是 `_reconcileCallbackCount++`；另一命中只是 `_realScratch` 注释。
- 修改：递增语句使用与声明相同的 `(UNITY_EDITOR || DEVELOPMENT_BUILD) && BUDDAH_PREDICTION_SHADOW`。
- I6：不适用，没有合并重复实现。32 种宏组合静态比较：12 种保留逐字相同的有效源码，20 种仅移除无效的计数器递增。
- R1：Editor C# 编译 0 error；无新增 C# warning（首次全量 11 条已有 warning，增量 Assembly-CSharp 9 条）。
- R3 构建：Windows64 / 非 Development 成功，0 error、44 build warning，687.4 秒，1137.62 MB。输出 `Builds/ArchitectureP0Release/BuddahGo.exe`，MCP job `build-1f84e09cbf`。
- R3 启动：已实际启动并观察到 Create Room / Browse Rooms / Join Room 主菜单。当前未启动 Steam，日志首先报告 `SteamApi_Init failed with NoSteamClient`，随后 PredictionManager 有连带 NullReferenceException；不能视为 Steam 联机成功。正式包中仍出现 D-PERF，这是 P0-4 将处理的已知 B2。
- 本机证据：`Logs/architecture-editor.log`、`Logs/p0-3-release-player.log`（生成日志，不提交）。

## 首轮本机多端测试（补充验证）

拓扑：原 worktree 的 Unity Editor 为 host，ParrelSync `BuddahGo_clone_0` 的独立 Unity Editor 为 client；两端均为 Unity 2022.3.55f1c1。使用已安装的 ParrelSync，通过其 `CreateCloneFromCurrent()` 创建。MainMenu 在内存中临时将 TransportManager 指向 Tugboat，监听 `127.0.0.1:17845`，停用 FishyFacepunch；不保存场景。两端限制为 60 fps、后台运行。

参考：[FishNet Tugboat](https://fish-networking.gitbook.io/docs/fishnet-building-blocks/transports/tugboat)、[ParrelSync](https://github.com/VeriorPies/ParrelSync)、[FishNet 延迟模拟](https://fish-networking.gitbook.io/docs/tutorials/simple/simulating-bad-network-connections)。当前测试不覆盖 Steam 大厅、P2P relay 或真实公网条件，也不是计划 R4 要求的 Dev Build host 拓扑。

已观察到的结果：

1. host ClientId=0，纯 client ClientId=1，host 的 `ServerManager.Clients.Count=2`。
2. client 调用原有 `RequestToggleReady()`；host 上玩家 1 的 Ready 从 false 变 true；client 收到相同名单。没有直接写 Ready 状态。
3. 无 Steam 大厅时，本地 host 的名单条目初始 `IsHost=false`。为隔离下游测试，仅在本轮测试夹具中于本地连接就绪后重新调用原有 `AddOrUpdatePlayer(localConnection)`，得到 host=true、ready=true。该辅助刷新不计作正常大厅流程通过，也未修改产品代码。
4. host 经原有 `RequestStartGame()` 进入 PropertySelection。client 通过原有 RPC 选择 acceleration / giant / reverseturn，host 选择 slowtrap / blackcurtain / push_projectile_hands；服务器收到全部 6 个槽位。
5. 两端均加载 RaceMap，并观察到相同网络 ObjectId=2/3、OwnerId=0/1，每端仅自身玩家的 IsOwner=true。
6. client 报告 RaceMap 场景对象找不到 SceneId，涉及 LeaderBoardManager、ResultRuntime、ResultDecisionManager、IntroSequenceManager 与 DebugboxCanPush 等。观察时控制权未解锁；本轮在记录问题后停止，没有强行绕过开赛门控。
7. 日志汇总：host 153 条 D-LOC 心跳、client 34 条；四项 div 非零窗口数均为 0，未发现 D-LOC FATAL。但 client 最后 hof-compared=0、rec-cb=3；host 最后 hof-compared=1、rec-cb=0。未完成有效开赛后的双端 90 秒测试，因此 **R7 不通过验收 / 未完成**，不能用静止阶段的零 divergence 代替。

R4 仅部分路径验证，R5 仅配装提交验证、没有完整技能施放矩阵；R6、R8、R9 未完成。P0-3 保留 `done-unverified`。

## 首轮发现，后续集中处理

| 编号 | 现象 | 处理边界 |
|---|---|---|
| N1 | 无 Steam 的本机 Tugboat host 连接建立后，房间名单未识别 host，阻挡正常开始按钮路径 | blocked：已记录，产品修复应按 D8 单独处理；本轮仅明确记录测试夹具的名单刷新。 |
| N2 | 同源 ParrelSync client 加载 RaceMap 时多个 SceneId 未注册；开赛控制权没有完成解锁 | blocked：保留日志与截图，定位/修复后再集中回归，不重复重跑当前失败场景。 |
| N3 | Steam 未运行时初始化异常引发 PredictionManager 连带异常 | 环境限制及启动健壮性问题，未在架构提交中修复。 |
| N4 | 首次导入有空动画、LightingData 不兼容警告，选择 UI 出现中文字体缺字 | 基线资源告警，不作为本次源码改动产生的新 warning。 |

按用户要求：本轮发现先记录；相关修复完成后再做一次针对性回归，避免同一问题反复测试。
