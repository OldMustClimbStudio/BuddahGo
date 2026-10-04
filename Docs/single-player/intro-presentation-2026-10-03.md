# 开场路径、Idle 错相与本机玩家标识 — 2026-10-03

本次沿用 `feat/single-player-mode` / PR #59 的已有工作区。用户已认可 6P 路径及 idle 效果，随后要求同步其他人数布局、添加头顶 3D 本机玩家标识，并记录联机适用范围。用户最终确认标识暂作占位，且仅开场显示、GO 后立即隐藏。本记录不是联机验收报告；未运行联机、Steam 双端或 Solo/Online 交替测试。

## 本次改动

- `Assets/Scenes/RaceMap.unity`：以用户调整的 Layout_6P 六个 LaunchPoint 为固定落点，制作六条独立曲线。前段自由错位、各自小幅热胎摆动，后段分时归入左右交错的最终队列。较窄弯道收敛摆幅，末段平顺接入落点及出发朝向。
- 1P–5P 使用 6P 的前 N 条曲线和对应落点。1P 复用 Layout_2P 的首槽，配置数组已缩为一个元素。修正 Layout_3P 的 Slot_1 原来误引用 Slot_0 的 LaunchPoint / ForwardReference 的问题。
- 六条路径均约 1122.903 m，使用 2048 个弧长采样；既有 60 m/s 开场速度、权威 GO、物理接管和 Timeline 资源未改。路径编辑存储在 **RaceMap 场景及 prefab instance overrides** 中；`Assets/Placeholder/Intro/Layout4P.prefab`、`Layout5P.prefab`、`Layout6P.prefab` 源资产尚未应用这些覆盖。其他场景若新建这些 prefab 实例，不会自动获得本次曲线。
- `RaceBodyIntroStateController.ApplySoloIdleVariation`：在单机 visual start 的重复事件保护之后，一次性按槽位/sequence 错开 `Buddah_Idle` 相位，赋予 0.94–1.06 的速度倍率；GO 不重置动画。只调整 `PlayerBuddah_Animator.controller` 的 idle 状态参数 `IdlePlaybackSpeed`，不改变全局 Animator.speed、左右推掌和反噬状态的速度。
- `LocalPlayerOverheadMarker` 及 `Assets/Placeholder/UI/LocalPlayerMarker.prefab`：青色、深色描边的立体向下箭头占位，只在开场显示于本端拥有的角色；`IsGoApplied` 为 true 后的 LateUpdate 隐藏，驾驶阶段不显示。跟随 `BuddahPredictionVisualRootBridge.GetVisualRoot()` 的已平滑位置，位于角色根部上方 8.5 m；面向最终相机并按视距调整大小。prefab 默认隐藏，无 collider、无新增 NetworkBehaviour、无 RPC。失去本地拥有者资格、禁用、开场结束或 GO 后隐藏；目标处于相机近裁剪面之后也隐藏。正常深度遮挡保留，不是透墙标识，也不是所有对手的名字标签。

## 是否同时作用于联机

以下判断针对**包含本次工作区修改的同一构建**，不代表已经合入 dev 或已在旧发行包生效。

| 内容 | 单机 | 联机当前代码行为 | 后续工作 |
|---|---|---|---|
| 1–6 人曲线、落点、3P 引用修复 | 已接入；六人开场实际播放 | 共用 RaceMap，因此也读取新布局 | 联机仍随机分配槽位，不保证本机玩家 Slot1；逐人数、各端镜头和 GO 延迟需要独立验证 |
| 自由入场、热胎式小幅路线摆动 | 路径内容的一部分 | 使用同一布局时也存在 | 确认随机槽位、视觉时间差及客户端接管下无交叉穿模或跳位 |
| Idle 相位/速度差异 | 明确启用 | **不启用**：`MainMenuHome` 分支保护，联机默认 idle 倍率仍为 1 | 需要单独把相位初始化接入联机 visual start；利用服务器下发的 slotIndex / sequence 保持各端一致，验证重复广播及迟到客户端不重置动作 |
| 本机玩家 3D 箭头 | 仅开场显示、GO 后隐藏；六人单机实测 | 共用角色 prefab；按 IsClientInitialized + IsOwner 判断，代码上支持每端只标记自己 | **未做双端实测**；验证 host/client 各一个标识、远端无误标、所有权变化、重开、断线、观战和结算清理 |

因此不能把本次交付描述成“全部效果已在联机验收”。联机 idle 仍需实现适配；共享布局和本机标识仍需独立联机验证。后续联机任务再处理这些事项，本单机任务没有执行联机测试。

## 实际验证

- 路线按 120 Hz 同步采样：最终 6P 最小车心间距约 **9.165 m**；每约 1 m 对中心及半径 4.2 m 的八个周边点向下探测，采样点均命中赛道路面。角色物理 Capsule 半径约 3.887 m。这是有限几何采样，不是所有技能形态下的碰撞证明。
- 1–6 人配置引用、独立 LaunchPoint、路径注册与曲线复制检查通过；对应槽位与 6P 的最大采样偏差小于 **1 mm**。1–5 人没有逐一运行完整对局。
- 改 idle 后 Unity 编译完成，开场连续性、时序和镜头退出相关 EditMode 测试 **33/33 通过**。首次播放仍加载旧 Animator 资源，保留为失败检查；重新保存 idle 子资源后，第二次六人实播确认六个不同相位、0.94/0.96/0.98/1.00/1.02/1.06 倍率、Animator.speed 均为 1。初次错相后采样未见相位回退，六车均完成 GO，0 捕获运行错误。
- 初版标识加入后的单机六人实播（后续按用户要求改为 GO 隐藏）：**387 个采样、0 捕获运行错误、六车全部完成 GO**。AI 标识均未显示；标识显示时与平滑角色锚点的最大位置误差约 `9.54e-7 m`。初始 0.5 秒之后的 378 个采样中，本机标识显示 372 次、隐藏 6 次；不据此声称全程都在屏幕中可见。开场截图已检查到本机角色上方的青色箭头。
- 最终 GO 隐藏复核：单机六人实际开场共 **383 个采样、0 捕获运行错误**，六车均接收到 GO；首个 GO 采样六个标识均隐藏，其后 35 个 GO/驾驶采样中可见标识数始终为 0。该结果覆盖初次开场到 GO 后约两秒，不代表完整对局或联机验收。
- 最终非 Development Windows x64 构建已发起；Unity MCP 在构建期间状态查询超时并断连，尚未取得完成回执，因此构建结果**未确认，不记通过**。编译、33 项相关测试、Editor 单机实播结果与本次构建结论分开。
- prefab 静态检查：默认隐藏、2 个 MeshRenderer、0 个标识 Collider；角色的 NetworkBehaviour 数量未增加。最终显示门为本地 owner + intro active + 未 GO；本次没有自然完整比赛或双端生命周期验收。

## 保留的问题与范围

- 原开场/GO 镜头在门架附近会受场景几何遮挡；本次未改镜头动画，也未把箭头改成透墙渲染。镜头遮挡、HUD 与开场画面的重叠仍需单独调整。
- 现有排名/结算 WIP、私有验证工具、MainMenu 的现有序列化差异和其他无关改动保留，未纳入本次修改。
- 本地检查证据位于主工作区的 `.worktree/_archive/intro-layouts-20261003/`：`layout-copy-checks.json`、`solo-playback/`、`idle-playback/`（旧 Animator 检查）、`idle-playback-v2/`、`marker-playback/`（初版标识）、`marker-go-final/`（最终 GO 隐藏）。这些截图、采样和临时制作脚本留在本地，不上传 PR。

## 联机后续清单

1. 明确是否把新的布局同步进源 prefab，避免其他场景实例仍使用旧线路；保留每个布局的唯一 splineId 和完整 registry 引用。
2. 将 idle 错相扩展到联机 visual start，保持重复事件幂等，不调整技能动画和实际运动。
3. 验证不同人数、随机槽位下每端标识只指向本机角色；检查 intro、GO、复活、完赛、观战、Rematch、返回及断线。
4. 在独立联机任务中验证客户端时间差、延迟 GO 接管和相机遮挡；不得直接沿用本次 Solo 结果标记通过。
