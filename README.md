# BuddahGo

`BuddahGo` 是一个基于 Unity 2022.3 的多人竞速原型项目，当前重点在三块内容：

- 基于 `FishNet` 的联机框架
- 基于组合输入的技能系统
- 基于样条线进度的排行榜、圈数和反噬系统

本文档描述的是当前仓库里已经实现并正在使用的版本，适合作为同步到 Git 之前的项目说明。

## 当前项目状态

目前项目已经具备以下主功能：

- 玩家基础移动、转向、推手交互
- FishNet 联机对象生命周期、RPC 和同步数据结构
- 组合输入触发技能槽位
- 服务端权威的技能施放、冷却和反噬判定
- 多种技能表现：
  - 加速 / 反噬减速
  - 减速陷阱 / 反噬定身后加速
  - 全体反转转向 / 反噬自我反转
  - Black Curtain 屏幕后处理效果
- 基于样条线的赛道进度计算
- 基于圈数和赛道距离的排行榜
- 本地 UI 显示排行榜、进度、Obsession 和反噬概率
- 技能 Feel、VFX 复制和黑幕视效控制

最近一轮代码清理还做了两件事：

- 去掉了 `ComboSkillInput` 里重复触发网络 cast 的旧路径，输入现在只负责识别 combo，真正施法统一交给 `SkillExecutor`
- 删除了当前未使用、也未被资源引用的旧“反转陷阱区”实现和未接线的全局技能设置壳

## 技术栈

- Unity `2022.3.55f1c1`
- Input System
- Cinemachine
- URP
- Visual Effect Graph
- Splines
- FishNet

说明：

- 项目代码里大量使用 `FishNet.Object`、`FishNet.Connection`、`FishNet.Object.Synchronizing`
- 当前 `Packages/manifest.json` 里没有直接写出 FishNet 依赖，说明它很可能是通过工程内已有插件、嵌入包或其他本地方式引入的

## 项目核心实现

### 1. 玩家移动

核心脚本：

- `Assets/Scripts/Buddah/BuddahMovementController.cs`
- `Assets/Scripts/Buddah/BuddahHandControl.cs`
- `Assets/Scripts/Buddah/PushHitbox.cs`

当前移动逻辑是客户端 owner 驱动物理：

- 只有 `IsOwner` 的客户端会读取输入并驱动自身 `Rigidbody`
- 非 owner 客户端上的刚体设为非模拟或不参与本地输入
- 推手命中后由服务端判定，再通过 `TargetRpc` 把冲量发回目标 owner 客户端执行

这意味着当前项目采用的是一种“局部客户端驱动 + 服务端控制关键事件”的混合方式，而不是所有物理完全在服务端跑。

### 2. 技能系统

核心脚本：

- `Assets/Scripts/Buddah/ComboSkill/ComboSkillInput.cs`
- `Assets/Scripts/Buddah/ComboSkill/SkillExecutor.cs`
- `Assets/Scripts/Buddah/ComboSkill/SkillAction.cs`
- `Assets/Scripts/Buddah/ComboSkill/SkillLoadout.cs`
- `Assets/Scripts/Buddah/ComboSkill/SkillDataBase.cs`

当前技能链路如下：

1. 本地 owner 输入 `W / UpArrow`
2. `ComboSkillInput` 根据输入窗口和绑定组合判断是否命中技能槽位
3. `SkillExecutor` 监听槽位触发事件
4. owner 客户端通过 `CastSlotServerRpc` 请求施法
5. 服务端检查：
   - 槽位是否合法
   - 技能 ID 是否存在
   - cast lock
   - 冷却时间
   - Obsession 反噬概率
6. 服务端执行 `SkillAction.ExecuteServer`
7. 服务端通过 `ObserversRpc` 广播本次最终执行的是 normal 还是 anti 版本
8. 各客户端执行 `SkillAction.ExecuteObservers`

设计原则：

- 游戏状态由服务端决定
- 视觉表现由 observer/client 侧播放
- 对 owner 本体的短时移动影响通常通过 `TargetRpc` 只发给对应 owner

### 3. 当前已实现技能

#### Acceleration

- 正常版本：给自己加速
- Anti 版本：给自己减速
- 真实数值效果通过 `TargetRpc` 下发到 owner，再由 `MovementAccelerationEffect` 改写移动参数
- 视觉表现主要走 `SkillFeelRouter`

#### Slow Trap

- 正常版本：给施法者挂一个服务器端减速触发区
- 碰到目标后，通过 `ApplyAccelerationToOwner(负数)` 给目标施加减速
- Anti 版本：自己先被短暂 root，再获得一段加速
- 这组技能会使用 `SkillVfxReplicator` 把世界 VFX 同步到所有客户端

#### Reverse Turn

- 正常版本：服务端遍历其他玩家，让他们的 owner 客户端进入反转输入状态
- Anti 版本：只反转自己
- 反转输入由 `MovementInvertTurnInputEffect` 实现

#### Black Curtain

- 不走普通世界特效，而是走本地相机视角下的全屏材质效果
- observer 侧根据“本地是否为施法者”和“是否为 anti”决定是否显示边缘轮廓
- `BlackCurtainViewController` 负责：
  - 调用全屏材质动画
  - 控制赛道边缘可见性
  - 控制其他玩家可见性
  - 控制黑幕期间的 trail 表现
  - 可选切换 URP Camera Renderer

## FishNet 联机实现

这一部分是当前项目最重要的基础设施。

### 1. 当前怎么用 FishNet

项目中的大部分多人系统都继承自 `NetworkBehaviour`，例如：

- `BuddahMovement`
- `BuddahHandControl`
- `ComboSkillInput`
- `SkillExecutor`
- `SkillLoadout`
- `LeaderboardManager`
- `PlayerProgressReporter`
- `LapProgress`
- `ObsessionFigure`
- `PlayerCamera`
- `SkillVfxReplicator`

判断本地身份时，主要使用：

- `IsOwner`
- `IsServerInitialized`
- `IsClientInitialized`
- `Owner`
- `OwnerId`
- `LocalConnection`

### 2. 三种 RPC 的用法

项目里已经形成比较清晰的职责划分：

#### `ServerRpc`

用途：从客户端向服务端请求一件事。

当前典型场景：

- `SkillExecutor.CastSlotServerRpc`
- `SkillLoadout.SetSlotServerRpc`
- `PlayerProgressReporter.ReportSplineProgressServerRpc`
- `BuddahHandControl` 的动作请求和推手请求

适用原则：

- 客户端只“请求”
- 服务端才做最终判定

#### `ObserversRpc`

用途：服务端把结果广播给所有客户端。

当前典型场景：

- `SkillExecutor.CastObserversRpc`
- `SkillVfxReplicator.PlayVfxAllObserversRpc`
- `BuddahHandControl` 的左右手动画广播

适用原则：

- 用于所有人都应该看到的表现层事件
- 例如技能释放、共享 VFX、动画和 Feel

#### `TargetRpc`

用途：服务端把结果只发给某一个客户端。

当前典型场景：

- `SkillExecutor.ApplyAccelerationTargetRpc`
- `SkillExecutor.ApplyRootThenAccelerationTargetRpc`
- `SkillExecutor.ApplyInvertTurnInputTargetRpc`
- `BuddahMovement.ApplyPushImpulseTargetRpc`

适用原则：

- 只影响某个玩家自己的输入、相机、移动或局部物理
- 尤其适合“owner 本地执行”的效果

### 3. SyncVar 和 SyncList

项目当前也使用了 FishNet 的同步数据结构：

#### `SyncVar`

典型用法：

- `ObsessionFigure` 的 `_current`
- `LobbyManager` 中的一些房间状态

特点：

- 服务端修改
- 客户端自动收到
- 适合单值状态同步

#### `SyncList`

典型用法：

- `SkillLoadout.SlotSkillIds`
- `LeaderboardManager.Rankings`
- `LobbyManager.Players`

特点：

- 适合列表型状态
- UI 可以直接订阅 `OnChange`
- 当前排行榜 UI 就是通过 `Rankings.OnChange` 自动刷新

### 4. 当前网络设计特点

项目不是完全服务端模拟，而是按系统拆分：

- 输入和移动主体：owner 客户端执行
- 关键技能是否合法：服务端执行
- 排行榜和比赛排序：服务端执行
- VFX / Feel / 屏幕效果：客户端执行
- 只影响某个玩家本地体验的效果：服务端通过 `TargetRpc` 下发到对应 owner

这种设计的优点是：

- 交互响应快
- 技能和排行榜仍保持服务端权威
- 表现层可以灵活扩展

当前需要理解的一点是：

- 只要是“比赛结果、技能是否成功、圈数、排名”这种会影响游戏规则的内容，都尽量放在服务端
- 只要是“本地镜头、视觉、临时输入翻转”这种局部体验内容，都可以通过 `TargetRpc` 发给 owner 执行

## 排行榜、圈数与赛道进度

核心脚本：

- `Assets/Scripts/Buddah/SplineProgressTracker.cs`
- `Assets/Scripts/GameData/LapProgress.cs`
- `Assets/Scripts/Network/PlayerProgressReporter.cs`
- `Assets/Scripts/Network/LeaderboardManager.cs`
- `Assets/Scripts/UI/LeaderboardTMPUI.cs`

当前流程如下：

1. `SplineProgressTracker` 在本地持续计算玩家在样条线上的位置、距离和 forward dot
2. `LapProgress` 在 owner 侧根据起点触发器、接近终点阈值和朝向判断来管理圈数
3. `PlayerProgressReporter` 每隔一小段时间通过 `ServerRpc` 上报当前距离、朝向和圈数
4. `LeaderboardManager` 在服务端缓存所有玩家的进度并定时重建 `Rankings`
5. `LeaderboardTMPUI` 订阅 `SyncList` 变化后实时刷新 UI

当前排行榜排序规则：

1. 先比 `Lap`
2. 再比 `Checkpoint`（兼容字段，当前主要还是 spline 进度）
3. 再比 `DistanceOnTrack`
4. 最后按名字兜底排序

## Obsession 与反噬系统

核心脚本：

- `Assets/Scripts/GameData/ObsessionFigure.cs`

当前实现：

- Obsession 是 `SyncVar<float>`
- 技能释放后由服务端增加 obsession
- 系统会持续衰减 obsession
- 如果玩家落后于领跑者，会根据“与第一名的差距”获得额外恢复
- 反噬概率通过 sigmoid 函数从 obsession 推导出来

这使得技能系统现在不是固定风险，而是和比赛进度与状态绑定的动态风险系统。

## VFX 与表现系统

核心脚本：

- `Assets/Scripts/Network/SkillVfxReplicator.cs`
- `Assets/Scripts/Buddah/SkillVFXDatabase.cs`
- `Assets/Scripts/Buddah/ComboSkill/TimedVfxInstance.cs`
- `Assets/Scripts/Buddah/ComboSkill/SkillFeelRouter.cs`
- `Assets/Scripts/Buddah/ComboSkill/Skill_FunctionScripts/BlackCurtainScreenEffect.cs`
- `Assets/Scripts/Buddah/ComboSkill/Skill_FunctionScripts/BlackCurtainViewController.cs`
- `Assets/Scripts/Buddah/PlayerBlackCurtainTrail.cs`
- `Assets/Scripts/Buddah/TrackEdgeVisibility.cs`

当前表现系统分为三类：

### 1. Feel Router

- 本地触发配置好的反馈播放器
- 适合音效、镜头感、小型共享表现

### 2. Skill VFX Replicator

- 服务端发 `ObserversRpc`
- 每个客户端本地实例化对应 prefab
- 通过 `TimedVfxInstance` 自动结束和销毁
- 适合普通世界空间技能特效

### 3. Black Curtain 屏幕表现

- 不走普通 VFX prefab
- 直接作用于相机和全屏材质
- 是当前项目里最特殊的一套技能表现系统

## 当前目录关注点

如果要继续开发，建议优先看这些目录：

- `Assets/Scripts/Buddah/ComboSkill`
- `Assets/Scripts/Network`
- `Assets/Scripts/GameData`
- `Assets/Scripts/UI`
- `Assets/Scripts/VFX`

其中：

- `ComboSkill` 负责技能输入、执行、技能资产和局部效果
- `Network` 负责排行榜、房间、VFX 联机复制
- `GameData` 负责圈数、进度、Obsession 等比赛数据
- `UI` 负责本地展示

## 当前已完成的结构整理

为了让现在的版本更适合继续维护，已经完成：

- 清理了重复的技能触发路径
- 清理了当前没有接入主逻辑的旧实现
- 保留了当前正在生效的技能链路和 FishNet 联机结构

现在的主线已经比较清晰：

- 输入识别在 `ComboSkillInput`
- 施法判定在 `SkillExecutor`
- 规则效果在 `SkillAction.ExecuteServer`
- 表现广播在 `ObserversRpc`
- owner 本地效果在 `TargetRpc`

## 提交前建议

在同步到 Git 之前，建议至少确认以下内容：

- Unity 能正常重新编译脚本
- 技能释放链路正常
- Black Curtain 在本地和远端视角都表现正常
- 排行榜能随着样条线进度更新
- 当前删除的旧脚本没有遗漏场景引用

## 后续可继续优化的方向

- 统一技能的 VFX 配置接口，减少“有字段但当前未使用”的保留项
- 给技能系统补一份时序图文档
- 把排行榜、圈数、Obsession 的 UI 抽成独立面板
- 进一步梳理当前客户端权威移动和服务端规则之间的边界

---

如果后续继续同步功能，建议直接在这个 README 基础上增量更新，而不是重新写一份。这样最适合跟 Git 历史一起维护。
