# 入弯反向整圈缺陷：修复与实际代价

2026-10-02，基线 `84b5bf5`，同一 `feat/single-player-mode` / PR #59。
用户观看 V5 后授权针对明确的入弯转圈缺陷定位和修复；这不是恢复 V1–V5 参数搜索，也未开展 A3。

## 结论

真实车辆转圈已复现，问题在规划搜索允许额外朝向绕圈，而不是仅有镜头旋转。
新增连续朝向分支约束后，本次连续三圈未观察到额外绕圈，但**没有提速**。
平均圈速 126.044 秒，慢于观察版 V5 单圈 118.100 秒，也慢于历史 A2 三圈平均 116.533 秒。
不能把该缺陷直接认定为圈速差距的最大原因，不能把本修复称为 A2 性能达标或干净圈。

## 根因证据与排查

- 用户独立演示 `task-17/visible-player-demo` 与历史 A2 的首段真实 Rigidbody 数据一致：4.8–14.3 秒累计旋转约 −778°，车辆位置仍向前移动。历史这段碰撞前模型朝向误差约 0.003°以内，不能用镜头或模型转向符号错误解释这些整圈。
- 本任务重新构建只增加观察信息的 V5，在 `evidence/baseline` 完成自然一圈。9.93 秒的规划选择 neutral，其 3 秒预测旋转为 −702.4°，候选成本 neutral/left/right 约 135/252/261。规划器主动选中了包含绕圈的未来状态；重规划只执行第一段输入，不声称整条预测被原样执行。
- 原成本累计横向位置、横向速度、沿路线速度误差和前进收益；朝向只经 sin/cos 影响施力。不同整圈数可以得到相同末端朝向，搜索没有保存相对于路线的朝向分支，因此能用转圈改变推力/速度以获得更低的有限时域成本。
- 真实 yaw 与 visualRoot yaw 的最大采样差仅 2.563°（修复前）/3.523°（修复后）；相机 yaw 另行记录。车辆根节点已经在转，视觉随动并非这次整圈的唯一来源。未修改相机或视觉桥。
- 对照 CSV 包含 position、heading、yawRate、input、目标位置/切线、投影段、三种起始键的成本和选中计划的旋转量。候选日志约 20Hz，真实轨迹约 10Hz，按最近已发生计划对齐，最大时间差 0.0334 秒。相关路段目标与投影连续，不需要通过进度回退或角度数值 wrap 来解释；未改路径投影、checkpoint 或圈数规则。
- 独立全路线几何投影复核：每个真实 position 重新投到全部采样线段，完全不使用 planner 的段提示。前后规划目标投影与独立投影最大相差 2.650 / 2.681 米（含最多 0.0334 秒计划年龄）；独立计算仍为修复后三圈零分支越界。产品 `SplineProgressTracker.progress01` 则曾停滞，最大落后独立投影 35.540 / 119.296 米。该 tracker 有局部/全局投影选择、每步限幅和距离拒绝逻辑；具体停滞触发尚未定位。`AIRacerDriver` 直接投影自身 position，不读取 tracker 的 progress01，因此不能据此归因 AI 转圈。CSV 同时保留两种进度，图的同路段横轴改用独立几何投影；未改产品计时/圈数/排名进度。
- 新记录的模型误差并非全部通过：baseline 无侧接触标记窗口 88/97 通过，fixed 为 293/297 通过；失败窗口保留在机器结果中。旧 A2 的单个失败窗口也未被本任务解释掉。模型/碰撞误差仍是独立问题。

## 修复

`ForwardSimPlanner` 的候选保存连续的 `HeadingError` 和上一个路线切线角。
起点采用最短朝向分支；后续累加真实预测 yaw 增量，只对路线切线差使用 `DeltaAngle`。
候选越出该分支的 ±180° 时淘汰，因此不会把额外一整圈折叠成一个便宜的短角度。
路线本身正常转弯、赛道接缝和 ±180° 数值 wrap 仍然允许。

当整个预测时域没有可行候选时返回 neutral，让原 motor 的角速度衰减生效，并防止读取过期 beam 内容。
fixed 共有 40/7555 次这类回退，未导致本次额外整圈，但这是后续速度/路线质量调查的重要线索。
这是规划约束，不是对真实 Rigidbody 做朝向钳制；转向、惯性、全油门、碰撞和速度上限不变。
V5 profile 在前后两次运行中逐字节相同，没有新增调参候选。

## 自然驾驶结果

| 运行 | 自然圈速（秒） | 平均 | 行驶侧接触 | 额外绕圈证据 |
|---|---|---|---|---|
| 本任务观察版 V5，1 圈 | 118.100 | 118.100 | 20 | 连续相对朝向范围 −861.9° 至 +486.4° |
| 修复后同一场 3 圈 | 126.783 / 123.533 / 127.817 | 126.044 | 66 | 连续相对朝向范围 −179.9° 至 +178.6°，越出分支采样 0 |
| 历史 A2（原记录） | 116.650 / 115.667 / 117.283 | 116.533 | 70 | 包含整圈缺陷，详见历史数据 |

baseline 有 1182 个采样、2353 个计划；fixed 有 3783 个采样、7555 个计划。
两次 sample ID 连续、clock 严格递增，采样 gap 为 6 或 7 ticks，不宣称每个间隔都相等。
两次运行均无运行错误、检测到的位置跳变、stall 或现有 respawn 事件。
fixed 原始 summary 的 67 次接触包含赛后 `Plane (2)` 的一次零冲量接触；行驶统计为 66。
fixed 自然进入 ResultInteractive，finish input 为 `drive=False; steering=0`。
两次均记录五个临时测试盒恢复 active=True；未删除或修改场景盒子资产。

判定整圈采用沿实际经过路线展开的 body yaw 与切线之差，而不是直接看 Euler 0/360 跳变。
fixed 仍有一个计划的绝对 yaw 变化为 374.2°，但路线也在转、相对朝向留在分支内；绝对 yaw >360 本身不能充当额外绕圈判据。
同一路段 15–30% 的位置/朝向对照见图和 CSV。去掉绕圈后仍有明显左右摆头和壁面接触。
前后运行次数不同且未控制所有调度差异，所以不做统计显著性或成功率断言；实测结果已经足以否定“本次修复已提速”的说法。

## 检查、构建与复现

- 最终 AI EditMode：19/19 通过，包括实际转圈入弯采样的正/反转向符号回放、朝向 wrap/绕圈分离、不可行候选回退和原 15 项测试。
- 同次首轮其他受影响计时回归：70/70 通过（AcceptedLapTiming 12、HandoffClock 28、MatchClock 8、RaceTiming 22）。首轮 AI 有 2 个失败，原因是新增合成直线测试擅自要求 8 秒后速度 <60，实际约80.319；改成真实记录回放后仅重跑 AI。保留首轮失败记录，不声称一次最终 89 项全通过，也不声称已解决速度目标。
- Development baseline/fixed 构建分别 0 errors / 3 warnings、0 errors / 14 warnings；最终普通构建 0 errors / 9 warnings。警告保留于日志。普通构建仅完成构建验证，不含开发 harness，不充当性能验收。
- 构建入口每次同步核对 `Application.dataPath`；所有输出位于 `task-18/builds`，没有覆盖用户 demo 或 Practice 包。
- 独立 Development CLI：`--ai-a1-output <empty-dir> --ai-a1-profile <normal.json>`；三圈使用 `--ai-a2-output` / `--ai-a2-profile`。harness 走正常选人、发车、checkpoint、计时和收尾，无强制完成或位姿写入。
- `python Tools/ai/plot_spin.py <task-18-root>` 读取 `evidence/baseline`、`evidence/fixed`，生成 PNG/SVG、两份 aligned CSV 与 `comparison.json`；依赖 matplotlib。

私有完整证据根目录：`C:/Users/dwh88/Documents/Codex/2026-10-01/task-18`。
关键文件：`evidence/spin-comparison.png`、`evidence/baseline-aligned.csv`、`evidence/fixed-aligned.csv`、两组原始 trajectory/plans/events/configuration/racing-line/summary、`evidence/tests-initial.xml`、`evidence/tests-ai-final.xml`、各构建 `receipt.json`。
仓库内保存 [机器结果](https://github.com/OldMustClimbStudio/BuddahGo/blob/20401e5/Tools/ai/spin-results.json)、真实入弯 [回归 fixture](../../Tools/ai/fixtures/spin-entry.json) 和绘图脚本。
fixture 的点来自完整实际路线，长度由首个已记录投影恢复；回放使用 prefab 力/阻力配置与日志四舍五入的惯量，不是新的精确模型一致性验收。

## 保留边界

本修复是有已知圈速退步的缺陷修复，留在 draft PR 中供评审，不合并、不宣称提速、A3/A4 或 Practice 全验收。
90 秒目标、弯道速度控制、壁面接触、无接触标记的模型失败窗口仍未解决。
`fc7ab594`（PR60）仍不是 feature HEAD 的祖先；motor/visual/intro/camera 重叠尚未同步，本结论不能套用到未来合并版本。
没有联机测试。没有新 branch/worktree。原 28 项 WIP 和 task-17 Editor demo pause 开关保留；仅提交本次拥有的变更。
启动前原交接的 Unity/用户 Player PID 已不在运行；只启动和结束了本任务构建/测试进程，没有关闭用户实例或注入输入。
