# Solo 开场与驾驶呈现时间契约

2026-10-01，生产代码 `cb4a9ad`。本模块修复本地开场到驾驶的 handoff；S1 整体验收仍为 doing。

## 行为与边界

Solo 本地 host 的 actor 和相机 follow target 从开场起读取同一份带时间戳姿态历史。历史记录每次 Unity 物理步完成后的真实刚体姿态，使用 `Time.fixedTimeAsDouble`；渲染采样 `Time.timeAsDouble - fixedDeltaTime`，当前固定延迟约 16.67 ms。GO 不切换时间源、增加延迟或叠加视觉 blend。历史不足时停在最近真实样本，不外推发车速度；不合并不同时间的相同姿态，因此真实停留仍能被检测。

开场开始时把整段物理样条的 GO 对齐到下一个固定步边界，偏移小于一个物理步；样条、速度、时长和 UI 倒计时不变。端点属于最后一个开场物理步，GO 通过既有权威路径直接幂等送达本地，在紧接的驾驶物理步之前消费既有 handoff 事件。原消费函数的 Sleep/Wake 会清零速度；Solo 在该步积分前恢复同一份已消费 snapshot 的速度，并执行原有 root/运动许可检查。后续输入采集、技能、motor tick、碰撞与权威 Match Clock 继续走现有路径。

`TransformTickSmoother.SetPresentationSuspended` 只转交图形节点写入权；Solo 期间队列清空、所有图形回调停止写入，预测和 PhysicsMode 保留。离开此路径恢复平滑器。普通联机分支保留现有路径，本模块未运行联机测试。

传送、root 进入/退出、外部控制进入/退出、开场取消/替换、禁用/重建会清理历史。相机在视觉桥完成 LateUpdate 后采样同一 follow pose。没有通过调整样本时间、发车速度、检测阈值或 blend 掩盖停留。

## 实际验证

- Unity 2022.3.55f1c1：65/65 相关原生回归通过，含 12 项新时间线边界测试、既有 handoff/clock/握手/本地 transport 与会话流程测试。
- 非 Development Windows Player，`BuildOptions.None`，修正构建 0 errors/18 warnings；另一个控制回归构建 0 errors/20 warnings。包含私有观察器，不等同普通发行包或性能验收。
- 正常首局和实际 Rematch：各 2690 帧，共 5380 帧，0 捕获错误；GO 前后 -150 至 +300 ms 的最低有符号水平速度分别为 57.96785、57.96760 m/s，无停留或倒退。约 58 m/s 的首步来自真实物理响应；snapshot 仍为 60 m/s。相机采样到最终 render-camera-end 的 actor/follow pose 差均为 0；D/A 设备事件得到 +2/-2 motor 输入。
- 固定呈现延迟实测为 16.66667 ms；Solo FishNet 图形队列在边界始终为 0。输入与 motor 不等待这条历史。GO+0.7 至 +1.0 s 的水平姿态差除以刚体速度，中位数从基线 27.00/20.05 ms 降至 5.27/12.56 ms；这是相对当前物理姿态的空间滞后估计，不是端到端输入延迟。两轮 D/A 请求均在下一个正常 motor 帧生效。
- 独立控制回归又完成首局/Rematch 共 5380 帧、0 捕获错误，边界最低速度 57.96704/57.96651 m/s。第二轮通过既有 modifier、external-control setter、权威 DropRespawn 事件检查 root 进入/退出、控制进入/退出与传送，五次历史重置后首帧只含新 pose。该传送是受控回归，不代表自然跌落复活或全部技能验收。
- 实际 GPU 截图已在私有任务对话展示。边界拼图只做原始 readback 的垂直翻转、缩放和数据标注，原件保留。

初版统一历史候选仍失败：最低速度 5.73/3.34 m/s。逐物理步证据确认 Sleep/Wake 后首步速度为零，修正后重新构建、回归和双轮采样；没有沿用该失败候选结论。一次 MCP 回归任务被构建打断，状态滞留，不计入通过；65 项结果来自随后独立原生 TestRunner 重跑。

私有证据位于 feature worktree 的 `Logs/handoff-timeline/`：`manifest.json`、`handoff-related-tests.json`、`handoff-test-cases.jsonl`、`timeline-velocity-actual/`、`timeline-controls-actual/`、`CHECKPOINT.txt`。证据目录不提交。`timeline-candidate-actual/` 保留被拒初版；更早失败 blend 与原时钟诊断不构成本次通过证据。

## 仍需独立完成

受控完赛仅用于到达真实 Rematch 按钮，两组均没有自然完整比赛。strict14、V3 精度、完整 V12/预算、自然复活与全部技能试玩未在此模块完成。本模块当时遗留的原粒子 shader 紫色及空 VFX 导入，后由 `aa7fb05` 定向重导入修复并局部可见验证，见 [VFX 恢复记录](../vfx-asset-recovery.md)；后续 `9e428c1` 又验证六技能本地触发/到期并修复 Animator 扫描警告，见 [技能验收](skill-acceptance.md)；对手效果和部分画面仍未验证，后续结果不计入本模块通过范围。两项孤立 meta 删除及 Unity 重写的设置保留，不能声称 clean-source。本模块历史检查时尚未交付试玩；后续游戏源码 `21cc3cf` 普通 Release 已交付，相关回归 151/151，见 [快速试玩清单](practice-playtest.md)。用户初步试玩反馈技能可用，并于 23:51 UTC 明确授权在 PR #59 从 A1 开始；AI 尚未实现，未验证项不因此通过。
