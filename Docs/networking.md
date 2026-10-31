# FishNet 开发要点

以本库 `Assets/FishNet/` 的版本和调用方式为准。下面保留会影响正确性的要点，不规定固定检查流程。

## 权限与复制

- 所有权变更和权威状态写入由服务器执行；客户端通过 RPC 请求，服务器校验。
- `ServerRpc` 默认要求所有权。改变 `RequireOwnership` 时保留对应的调用者校验。
- Host 初始化同时涉及服务器和客户端身份；在 `OnStartServer` 中按目的区分 `Owner.IsLocalClient` 与 `IsOwner`。
- SyncTypes 的变化不是立即送达；依赖 RPC 与状态同步的先后关系时，检查实际发送时机。
- SyncCollection 内部对象的修改需要正确标记 dirty；自定义网络数据需要完整、可往返的序列化。
- 向 observers 发消息前确认观察者已建立；不要将 `OnStartServer` 等同于所有客户端已经 ready。
- 网络组件预先放到 prefab，避免运行时动态添加 `NetworkBehaviour`。只在运行时需要的状态挂普通 MonoBehaviour（如 `SkillStateBoard`、`YuanqiRegistry`、`RacerSkillPresentationState`、`TrainingDummy`），RPC 留在 prefab 上已有的 `NetworkBehaviour` 里。

## 协议版本

- `PredictionProtocol.Version`（`Network/PredictionProtocol.cs`，现为 2）由大厅 host 写进 Steam lobby 元数据；加入方的 `SteamLobbyManager` 读到不同版本（或没有版本）就离开大厅、报加入失败。
- 以下变化都要把版本加一，并在 `PredictionProtocol` 的 History 注释写一行：reconcile / replicate 载荷的序列化字段，TargetRpc 或 ObserversRpc 的参数，RPC 的增删或重排（FishNet 的 RPC id 按声明顺序分配），SyncVar 的增删或重排，以及经网络传输的枚举的新值（如冲量来源 `BodyImpact=5`，随 `ImpulseCmd.SourceType` 以 byte 传输）。玩法深化一轮（未发布）的全部改动合在 v2 里。

## 技能与缘起的同步

- 运动效果走预测修饰命令：服务器写一份，再用 TargetRpc 让 owner 写同一条，见[预测设计](prediction-design.md#技能-modifier-命令)。服务器 AI 和训练木桩没有有效 owner，`SkillExecutor.ApplyXToOwner` 只在服务器写，TargetRpc 跳过。
- 缘起的技能状态板和注册表只在服务器，不同步。客户端只收表现：`SkillExecutor` 的 `YuanqiObserversRpc`、`SkillConsumedObserversRpc`、`YuanqiImpactObserversRpc`，以及技能被消耗时停特效的 `SkillVfxReplicator.StopVfxAllObserversRpc`。
- 反噬惩罚的结束 tick 是 `ObsessionFigure` 的 SyncVar：SyncVar 变化时各端触发 `BackfireStateChanged(true)`，本端时钟越过结束 tick 时触发 `false`，不等服务器清零；惩罚中途加入的端会收到一次开始。冻结和突进的剩余时间从本端的 modifier 期限读，不另发。
- owner HUD 的冷却与同招加价窗口按服务器 tick 定锚：`CastObserversRpc` 和 `BackfireSkillUiTargetRpc` 带服务器执行时的 `serverTick`，owner 用本端估算的服务器 tick（`TimeManager.Tick`）减去它得到事件年龄（`LocalSkillUiEvent.SecondsSinceServerTick`），不按 RPC 到达时刻计时。远端只剩上行延迟的偏差，host 没有偏差。

## 多人场景

- 通过房间系统调用 FishNet 场景管理。区分 global 与 connection 场景的加载/卸载 API。
- 新场景按名称定位；已有实例或同名堆叠场景按 handle/reference 定位。
- 客户端加入场景的观察关系后才能看到网络对象；普通 Unity 场景加载不等于完成网络登记。
- 跨场景保留的网络对象需要纳入对应迁移数据。场景切换后重新确认对象和连接引用。

## 定位常见问题

| 症状 | 优先查看 |
|---|---|
| RPC 没到达 | 调用侧、所有权、连接存活和实际接收日志 |
| 列表改动不同步 | 修改是否发生在服务器，是否通知集合变化 |
| 切场景后对象不可见 | 客户端场景登记、observer 与对象迁移 |
| Host 正常、远端异常 | 序列化、客户端输入/回放、RPC 返回链路 |
| 加入大厅报 `Prediction protocol mismatch` | 两端构建的 `PredictionProtocol.Version` 不同，换同一版本的构建 |
| 角色或相机抖动 | [预测设计](prediction-design.md)中的模拟状态与视觉平滑边界 |
| 预测对象随速度回跳或重影，host 与 client 都有 | NetworkManager → TimeManager 的 Physics Mode 必须是 `TimeManager`；`Unity` 模式下 reconcile 回放不做物理积分，每次校正都是真实回跳（[联机修复 R1](online-repair/findings.md)）。Editor/Development 下 `BuddahPredictedMotor` 启动时会报 invariant 错误 |

## SceneCondition 与常驻对象

MainMenu 的 ObserverManager 默认使用 SceneCondition。房间级常驻 NetworkObject 必须设为 global，或放进由 FishNet 加载、登记观察关系的场景，否则 client 看不到它。不要只靠普通 Unity 的场景加载或 DontDestroyOnLoad 推断可见性。

client 完成 RaceMap 加载并建立 observer 关系之前，服务器发出的非 BufferLast observers RPC 对该 client 不会补发。新增 spawn/初始化路径上的 RPC 时，应等待观察关系或使用适合的同步状态/缓存机制，并核对 host 与纯 client 的实际接收行为。
