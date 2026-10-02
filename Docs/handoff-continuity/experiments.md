# 排查实验（单变量，单端）

每次只改一个变量，用同一开场和同一指标测量。探针单独提交（`chore: [EXP-H.n] …`），正式修复前 revert；prefab 改动用 `git restore` 回退。结果填入 [progress.md](progress.md)。根因编号见 [findings.md](findings.md) R7。

## 标准脚本

1. 单机或 host 进入一局，等待开场 spline 动画与 GO。
2. GO 前后各 0.5 s 为观察窗口；录像一段。
3. 重复开赛 5 次。

## 指标采集

| 指标 | 来源 | grep 前缀 |
|---|---|---|
| 逐帧 VisualRoot / 物理根位置、`rb.velocity`、相机 FOV 与偏移 | fix-spec §H.0 探针 | `[HandoffFrame]` |
| GO tick 与 consume tick | 既有 `[IntroGo] CompleteGoTransition`、`[HandoffDebug] consuming queued handoff`（需 `enableVerboseLogs`） | |
| spline 驱动异常 | `[IntroSplineDiag]` 的 backApplied / snapApplied | |

---

## EXP-H 本地交接连续性（R7，单端可测）

- **范围**：只处理本地切换；联机的 RTT 死区与投影差由 `fix/online-prediction-handoff` 负责。单机模式走同一 host 路径，可直接用单机复现与验证。
- **硬约束**：开场 spline 动画的播放效果（路径、速度、相机稳定模式、时长）必须保持与现在一致；任何干预或修复若改变开场动画观感，即判为不通过。
- **假设**：卡顿来自 spline 驱动到 motor 驱动的本地切换（findings.md R7），host 与单机同样出现，与网络无关。
- **预检**：单端（host 或单机）开场仍能看到切换瞬间卡一下；`[HandoffDebug] consuming queued handoff` 的 `currentTick` 比 `[IntroGo] CompleteGoTransition` 所在 tick 大 1 以上。
- **采集**：GO 前后各 0.5 s 逐帧记录 VisualRoot 位置、物理根位置、`rb.velocity`、相机 FOV 与跟随偏移（临时探针，见 fix-spec §H.0）。判定口径见 acceptance.md「交接连续性」。
- **干预**（每次只开一个，分别测，全部可在 Inspector 或一行代码内完成，不进正式提交）：
  1. **R7.1 相机**：`Buddah.prefab` → PlayerCamera 的 `zoomBySpeed` 置 0 且 `directionalOffsetPerSpeed` 置 (0,0,0)。卡顿消失或明显减弱 → 相机速度源是主因。
  2. **R7.2 死区**：在 `RaceBodyIntroStateController.DriveSplinePose` 末尾临时加 `targetRigidbody.velocity = snapshot.Velocity`，并在 `CompleteGoTransition` 里打印 `TimeManager.LocalTick`，与 consume 的 tick 比较。差值 ≥1 即确认死区存在；同时观察相机是否因速度连续而不再顿挫（与 1 交叉验证）。
  3. **R7.3 视觉参考系**：`Buddah.prefab` → BuddahPredictionVisualRootBridge 的 `lockVisualRootDuringIntroAndPresentation` 置 0。切换处「停、吸、再跟」是否消失；开场期间是否出现新的视觉滞后（预期 1 tick）。
  4. **R7.4 角速度**：查看所用 spline 末端是否为直线（SplineIntroPath 末段切线变化）；若有曲率，临时把 `ApplyLaunchInheritedVelocity` 中的角速度参数改为 `rb.angularVelocity`。
  5. **R7.5 高度**：日志打印消费时 `snapshot.Position.y` 与消费后第 10 tick 的 `rb.position.y`，差值 > 0.05 u 即存在落差。
- **判定**：
  - 1 或 2 单独就让卡顿消失 → 按 fix-spec §H.1 实施相机与速度源修复（最小方案），其余子项按需。
  - 1、2 之后仍有位置停顿 → 3 也成立，按 §H.2 决定最小方案（渲染采样偏移 + 删 post-intro 锁 + GO 当帧消费）还是架构方案（spline 进 motor 状态，DH1）。
  - 全部干预后仍有卡顿 → 记入 progress.md「新发现」，停下来找人。
- **预期**：host 与单机在本项后交接连续。
