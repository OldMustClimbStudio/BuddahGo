# 单机模式设计

- 术语以根目录 [CONTEXT.md](../../CONTEXT.md) 为准。
- 关键决策见 [Docs/adr/](../adr/)。
- 分阶段目标见 [phases.md](phases.md)，执行入口见 [HANDOFF.md](HANDOFF.md)。
- 事实依据：`dev` @ `ce5c1c2`。行号是定位提示；重构合入后按符号名重新定位。

## 1. 要做成什么

一个 **Solo Match（单机对局）**：
- 一个 Human Player（真人玩家）对 0–5 个 AI Racer（AI 赛手），完全离线，Steam 不开也能玩。
- 流程和 Online Match（联机对局）一样完整：属性/技能选择 → 入场与倒计时 → 比赛 → 名次 → 结算演出 → 再来一局或返回。
- AI 数量为 0 时就是 **Practice（练习）**：结算只显示时间和圈速，不排名次。

## 2. 已定决策

| # | 决策 | 结论 |
|---|---|---|
| Q1 | 单机形态 | 对战 AI（派对向），练习模式一并支持 |
| Q2 | 离线 | 完全离线，Steam 可以不运行 |
| Q3 | 流程 | 全部保留；只去掉投票这类联机特有的环节 |
| Q4 | 排期 | 重构计划先行；本分支在重构合入 `dev` 后同步再实现 |
| Q5 / Q18 / Q19 | 暂停 | 需要，但暂停菜单放到后续阶段。本期预留可暂停的比赛时钟，并提供 Esc 确认退出 |
| Q6 | 技术路线 | 离线传输层 Yak 加本机 host，复用整套服务器权威代码（[ADR 0001](../adr/0001-solo-match-runs-on-offline-host.md)） |
| Q7 | AI 数量 | 玩家自选 0–5 个，默认 3 个 |
| Q8 / Q30 | 难度 | 简单、普通、困难三档，**由规划精度决定，不设圈速目标**；带轻度追赶 |
| Q9 / Q31 | AI 技能 | AI 有随机配装，按每个技能的时机规则施放，同样积累执念值、同样会反噬 |
| Q10 | 练习 | 0 个 AI 的 Solo Match |
| Q11 | 名字 | 玩家名优先取 Steam 名字，没有则用"玩家"；AI 名字来自可配置列表（由团队填写） |
| Q12 | 选择阶段 | 不设超时；跳过地图投票 |
| Q13 / Q17 | 结束规则 | 沿用"第一名冲线后 15 秒结算"；玩家冲线后可以**跳过观战**，效果等同于倒计时立即结束 |
| Q14 / Q21 | 入口 | 主菜单首页新增"单人游戏" → 设置面板 → 选择场景。进入大厅后首页本来就不显示，不需要额外禁用 |
| Q16 | 遮挡视野对 AI | 被遮挡期间，AI 的规划精度下降 |
| Q20 | 再来一局 / 返回 | 再来一局回到选择场景，保留 AI 数量、难度和名字，配装重新随机；返回回到主菜单首页 |
| Q22 | 存档 | 本期不做 |
| Q23 | 练习模式的技能 | 照常可以施放，作用于他人的效果为空 |
| Q24 | 身份 | 引入 Racer，所有比赛数据按 RacerId 区分（[ADR 0002](../adr/0002-racer-identity-is-separate-from-connection.md)） |
| Q25 | AI 运动 | 与玩家同一个预测运动组件；AI 是服务器持有、没有 owner 的 Buddah |
| Q26–Q29, Q33 | AI 驾驶 | 往前推演式的转向规划；只输出 -1/0/+1；五个旋钮；按错率独立于难度；配套调参场景（[ADR 0003](../adr/0003-ai-steers-by-predicting-with-the-real-movement-rules.md)） |
| Q28 | 可替换 | AI 的决策层做成接口，将来简单难度可以换成经验公式 |
| Q32 | Steam 容错 | 修改 FishyFacepunch 的初始化，使它容忍 Steam 缺失（[ADR 0004](../adr/0004-fishyfacepunch-tolerates-missing-steam.md)） |

## 3. 架构

### 3.1 离线 host

```text
主菜单「单人游戏」→ 单机设置（AI 数量、难度）→ SoloMatchSettings（整局有效）
  → NetworkManager 使用 Yak 启动 host（server 与 client 在同一进程）
  → 沿用现有流程：RoomStateManager → PropertiesSelection → RaceMap → 结算
  → 结算：再来一局（回到选择场景）/ 返回（停止 host，回主菜单）
```

- 传输层：用 Multipass 同时挂 FishyFacepunch（联机）和 Yak（单机），开局前选定使用哪一个。
- FishyFacepunch 初始化时必须能容忍 Steam 缺失（ADR 0004）。
- 联机、单机的差异集中在一个"对局规则"对象里，比如：是否投票、选择阶段是否有超时、地图投票是否跳过、是否允许暂停、是否可以跳过观战。各系统查询这个对象，不在各处各写一份 `if (solo)`。
- 纯 host 会话里，FishNet 不执行 reconcile 和回放：
  - 服务器只负责发送 reconcile（`NetworkBehaviour.Prediction.cs:1346`）。
  - host 端的历史记录直接返回（`:1306`）。
  - 所以联机远端客户端那类预测误差和回滚抖动，在单机里不会出现。
  - 视觉平滑层（TickSmoother）没有核实，列为验证项 V7。

### 3.2 Racer

- 进度、完赛、名次、结算演出、观战目标、执念值里"离领先者的距离"、名字显示，全部改为以 RacerId 为键。
- Human Player 的 RacerId 由连接号得出；每个 AI Racer 在生成时分配一个唯一的 RacerId。
- 在 Online Match 里，Racer 和 Human Player 一一对应，行为不变。
- 注册表建立在重构计划 P2 的 `PlayerRegistry` 之上：它扩展为 Racer 注册表，或者作为 Racer 注册表的来源。

### 3.3 AI Racer 的组成

```text
Buddah.prefab（服务器生成，无 owner）
  ├─ 同一个 BuddahPredictedMotor：转向输入来自 AI 决策层；油门本来就固定为 1
  ├─ AI 决策层（可替换接口）
  │    ├─ 转向规划器：往前推演，只输出 -1/0/+1
  │    └─ 施法规则：每个技能一条时机规则
  └─ 服务器侧入口：进度与完赛上报、复活、施法、出发控制权交接
```

- FishNet 4.7.1 对无 owner 对象的处理：`IsController = IsOwner || (IsServerInitialized && !Owner.IsValid)`（`NetworkBehaviour.QOL.cs:174`）。服务器会用自己构造的输入执行 replicate（`NetworkBehaviour.Prediction.cs:547-617`）。
- 所以，motor 读取转向的地方（`BuddahPredictedMotor.BuildReplicateData`，约 `:330`）是 AI 唯一的接入点。
- 技能的运动效果（反转、减速或定身、加速、缩放）都写在模拟里的 modifier 状态中，会自然作用于 AI。推人冲量也已支持无 owner 的对象（`CombatAdapter.cs:85-88`）。
- AI 缺的只是经 TargetRpc 在 owner 客户端播放的**表现**，这部分要改成不依赖 owner。

### 3.4 转向规划

**运动事实（写规划器必须遵守）：**

| 参数 | 值 | 位置 |
|---|---|---|
| 质量 | 2 | `Buddah.prefab:572` |
| 线性阻力 | 0，横向速度不会自然衰减 | `Buddah.prefab:573` |
| 角阻力 | 0.05 | `Buddah.prefab:574` |
| 刚体约束 | 锁 X、Z 旋转，只剩偏航 | `Buddah.prefab:589` |
| 推进 | 沿水平朝向施加 `FinalForwardForce × throttle` | `BuddahLocomotionStep.Compute` |
| 转向 | 按住时施加 `steering × FinalTurnTorque` 的偏航力矩；松开后角速度按 `TurnDecayPerSecond` 衰减 | motor 约 `:520-531` |
| 速度上限 | `ClampPlanarSpeed(FinalMaxSpeed …)` | motor |
| 默认配置 | forwardForce 50、turnTorque 30、turnDecay 3、maxSpeed 80 | `BuddahPredictedMotorConfig` |

- 所有 `Final*` 数值都会被技能的 modifier 改变，规划器每次规划时读取当前值。
- 玩家的输入是数字量：A/D 只能给出 -1、0、+1（`PlayerBuddahInputSource.GetSteering`）。

**规划方法：**
- 每隔若干 tick，用上面的公式把"左 / 不按 / 右，各保持若干 tick，之后松键"这几种候选按键序列向前推演。
- 推演不含碰撞。
- 评分项：与赛道样条线的偏离、速度方向与切线的夹角、与墙的接近程度。
- 执行评分最好的序列。撞墙或被推之后，靠下一次重新规划来修正。

**五个旋钮：**
- 难度决定其中四个精度旋钮：反应延迟、推演时长、重新规划间隔、候选分辨率。
- 按错率（Fumble）独立于难度。
- 另有走线偏移，用来避免 AI 排成一列。
- 被遮挡视野时，推演时长缩短、反应延迟增加。

**追赶机制（Q8 的"轻度追赶"）：**
- 只调 AI 的大脑，不改运动：落后于领先者的 AI 按错率降低、精度略升；领先的 AI 按错率略升。
- 推力、速度上限等物理参数不为 AI 单独修改（ADR 0003）。

**路线来源：** 第一期用现有的赛道样条线（`TrackSplineRef`、`SplineProgressTracker`）。路线来源做成可替换的，将来可以换成手绘赛车线。

### 3.5 AI 施法规则

每个技能一条时机规则，难度影响的是规则的判断精度（看多远、多久扫描一次）。

| 技能 | 普通版施放时机 |
|---|---|
| 加速 | 前方是直道 |
| 减速陷阱 | 身后近处有 Racer |
| 反转转向 | 前方有 Racer 正在过弯 |
| 遮挡视野 | 附近有对手 |
| 推手 / 弹丸 | 侧前方有 Racer |
| 巨大化 | 周围 Racer 多 |

- 反噬的概率照常由执念值决定。
- 技能池以配置仓库为准。

### 3.6 比赛时钟（为暂停做的预留）

以下比赛内的计时统一改用一个可暂停的比赛时钟（本期不做暂停逻辑）：

| 类别 | 计时器 | 当前时间源 |
|---|---|---|
| 冲线后倒计时 | `RaceFinishManager` 的 15 秒 | `unscaledTime`，`:78/:125` |
| 开赛倒计时 | `RoomStateManager` | `WaitForSeconds`，`:985` |
| 技能 | 冷却与施法延迟 | `SkillExecutor :199/:247/:263` |
| 执念值 | 衰减 | `ObsessionFigure :83` |
| 复活 | 计时 | `BuddahRespawn` |
| 推人判定框 | 存活时间 | `PushHitbox` |
| 进度与完赛 | 宽限期 | `RaceCompletionTracker :72` |
| 结算 | 各倒计时 | `MatchResultPresentationCoordinator`、`ResultDecisionManager` |
| 选择阶段 | 倒计时 | `PropertiesSelectionManager` |

- FishNet 的 tick 按 `unscaledDeltaTime` 累加（`TimeManager.cs:700`）；motor 里 modifier 的持续时间按 tick 计。
- 所以暂停时 tick 也要停，方案留到 S7 定。

## 4. 现有代码中必须处理的点

以下各处目前都按 owner 或连接区分。AI 的 `OwnerId` 恒为 -1，`Owner` 是 EmptyConnection 而不是 null。

**生成**
- `MatchSpawnManager` 只为 `ServerManager.Clients` 生成角色（`:97/:123`）。
- 出生点编号取自 `_spawnedPlayers.Count`（`:114-125`）。

**开场**
- `IntroSequenceManager` 按车体数量选开场布局（`:122/:311-323`），只配了 1–3 人（`RaceMap.unity:13423-13438`）。
- 它按 OwnerId 排序（`:491-493`），所以 AI 会排在玩家前面。
- `RaceBodyIntroStateController` 在非本地 owner 的分支里设置 `isKinematic = true`（`:301/:352`），**会把 AI 锁死在原地**。
- 服务器端有 `TryApplyServerAuthoritativeLaunchHandoff`（motor `:902`）可以利用。

**进度与完赛**
- `PlayerProgressReporter` 有 `IsOwner` 门槛（`:28`），并通过 ServerRpc 上报（`:74`）。
- `RaceCompletionTracker` 也有 `IsOwner` 门槛（`:61/:248`）。
- `LeaderboardManager` 用 `_progressByClientId`（`:21`）区分，所有 AI 会并成一条记录。
- `RaceFinishManager` 用 `_finishedClientIds`（`:120-127`）区分。

**结算与观战**
- `MatchResultPresentationCoordinator` 按 clientId 区分（`:47/:119`）；`TryGetReporter` 按 OwnerId 查找（`:248-254`）。
- `RaceSpectatorTargetResolver` 按 OwnerId 匹配（`:45-51`）。
- `ResultDecisionManager` 的投票参与者取自 `RoomStateManager.Players`。在 Solo Match 中投票由"再来一局 / 返回"按钮代替。

**技能**
- `SkillExecutor.CastSlotServerRpc` 要求 ownership（`:173`），冷却逻辑写在 RPC 函数体里（`:199-251`）；需要一个服务器侧施法入口。
- `Apply*ToOwner` 发出的 TargetRpc 对 AI 会被丢弃，并打出警告。
- `SkillLoadout` 按 `Owner.ClientId` 解析配装，AI 会退回默认配装（`:27/:81`）。
- `ObsessionFigure` 计算"离领先者的距离"时按 ClientId 查找（`:114`）。

**复活**
- `BuddahRespawn` 由 `IsLocalOwner` 触发（`:225-228`）。
- motor 的 `ResetActiveSkillEffectsForOwner` 需要 `IsOwner`（`:1886`）。

**Steam 与菜单**
- `FishyFacepunch.cs:98-101` 调用 `SteamClient.Init` 时没有容错。
- 当前只有 FishyFacepunch 一个传输层（`MainMenu.unity:6249-6352`）；Yak 位于 `Assets/FishNet/Runtime/Plugins/Yak/`。
- 玩家名依赖 `SteamClient.Name`（`RoomStateManager :679`）。
- `MainMenuUI` 按 `IsInLobby` 切换首页和房间面板（`:215-224`）。

## 5. 不做（后续清单）

- 成绩存档
- 简单难度改用经验公式
- 手绘赛车线
- 联机房间里用 AI 补位
- 玩家名字编辑
- 暂停菜单本体（S7）
