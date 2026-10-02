# Tracker 停滞与限幅状态修复 — 2026-10-02

本模块仅修改 `SplineProgressTracker`、Buddah prefab 的有限投影距离及直接回归。继续 `feat/single-player-mode` / draft PR59，基线 `ba30112`；不合并、不重开分支，不改变驾驶算法、V5 参数、技能、排名或结算消费者。

## 两个独立问题及证据

使用 task-18 的真实三圈 `trajectory.jsonl`（3783 点），在当前 RaceMap 中调用实际 tracker，按约 60 Hz 对相邻约 10 Hz 保存位置/速度插值。临时 Editor-only 分支采样记录拒绝原因；这是保存位置回放，**不是原运行逐渲染帧日志，也不是新自然比赛**。

1. **有限距离不足导致停滞。** prefab 的 20 m 覆盖旧代码默认 40 m。旧逻辑回放在 360.233333–362.533333 秒的 24 个保存点中，首点正常、后 23 点均实际进入 `global-distance` 拒绝分支；进度停在约 0.7910364，末点与当前 spline 全局投影相差 119.44 m。原记录为 0.791036，独立折线分析约 119.296 m；两种几何采样的差别不混写。
2. **限幅距离/参数不一致。** 旧逻辑改变 `chosenD` 后保留旧 `_lastT01`。20 m 全回放最大 `DistanceAtT(_lastT01)` 与已提交距离差 126 m；仅改成 30/40 m 时仍差 17.5 m。因此阈值修复不能代替状态修复。

三圈所有保存点最大 spline 投影距离 29.11184 m。174 个大于 20 m 的保存位置向下射线全部命中 RaceMap 路面 collider，车体相对路面高度约 1.29588–1.29597 m；这支持它们属于正常道路走廊。30 m 与 40 m 对本数据均无距离拒绝，采用更小的 **30 m**。此证据不保证所有未来路线/碰撞姿态都处于 30 m 内。

## 最终行为

- prefab 与代码默认值统一为 30 m；保留三维世界距离验证，不无限放宽、不改 HUD。
- 保留原全局/局部连续性选择与速度限幅；对最终选中的候选分支也检查距离，避免另一条道路的近全局点掩盖离车体很远的局部候选。
- 限幅后以 `track.TAtDistance(chosenD)` 计算参数，提交的距离、下一帧搜索中心和 forwardDot 切线保持一致。
- 拒绝帧保持进度、清除旧 delta/wrap 标记，并在已有有效历史时按当前速度更新方向。首次超限样本不再初始化无效进度。
- `LapProgress`、合法触发过线、顺序 checkpoint、`RaceCompletionTracker` 的逆行及完成规则没有改动。无新增运行时日志、HUD 或永久诊断采样器。

## 回放对照

| 版本 / 距离 | 拒绝保存点 | 最大状态差 | 最大全局投影落后/偏差 |
|---|---:|---:|---:|
| 旧逻辑 / 20 m | 174 | 126.000 m | 119.437 m |
| 旧逻辑 / 30 m | 0 | 17.500 m | 3.592 m |
| 修复逻辑 / 20 m | 175 | 0.0113 m | 119.437 m |
| 修复逻辑 / 30 m | 0 | 0.0098 m | 3.269 m |

40 m 对照与各自 30 m 结果相同。修复逻辑 / 20 m 多一次拒绝来自新增 selected-distance 检查。最终 30 m 回放中，目标区间进度推进到 0.8118115，最大投影偏差 2.869 m，没有累积约 120 m 的追赶；逆映射残差在查表/二分精度内。

## 验证与边界

Unity 2022.3.55f1c1：首轮 97 项中 96 通过、1 个新测试的单步预期失败（误写 <3 m，而既有高速下限为 3.5 m）。只修正测试预期后 tracker 14/14 通过；随后将所有合成道路夹具也改为读取实际 prefab 的搜索窗口 0.2（代码默认 0.1），最终再次 14/14 通过。其余 83 项 timing / accepted-lap / race-end / handoff / AI 回归在首轮通过，未无故重跑。最终夹具最大落后 2.868652 m、最大单步 3.5 m、平台点数 0。不能将此描述成一次最终 97/97 的运行。

永久回归加载实际 prefab/RaceMap 和 [70 点保存位置夹具](../../Tools/tracker/README.md)。合成用例覆盖正反限幅与正反跨圈、拒绝后的标记/方向、首次无效投影、平行/交叉道路不跳到远程进度分支、高差及距离超限分支拒绝、原顺序 checkpoint 方向门槛。邻近道路用例验证保留历史分支；它不声称仅凭 spline 就能识别所有几何歧义或作弊传送。

独立 Windows x64 **非 Development** 构建成功：**0 错误 / 24 警告**。警告为既有源代码弃用/未使用项、nographics 光照、shader 和 Feel 资源项；无 tracker 新编译警告。输出 `task/build/BuddahGoTracker.exe`，未覆盖用户包；本轮未启动此 Player，构建成功不等于自然比赛验收。

未重新运行自然三圈计圈/完赛或自然六人比赛；保存位置回放不写圈数、完成状态或结果。六人旧 rank 仍为 -1，**不能声称六人排名/checkpoint 权限迁移或结算通过**。五 AI 106–128 ms 的历史采样帧耗时、AI 接触/模型异常、速度目标、技能和结果迁移均保留未完成状态。V1–V5 速度调参预算没有重启，未做联机验收。

## 证据与工作区

- 私有目录：`C:/Users/dwh88/Documents/Codex/2026-10-02/task`。
- `baseline-{20,30,40}.csv`、`fixed-{20,30,40}.csv`、`replay-summary.json`：完整三圈回放与分支结果。
- `baseline.log`、`fixed-replay.log`、`instrumented-fixed-tracker.cs`、`private-helpers/TrackerReplayProbe.cs`：实际执行和临时采样源码；两次 FishNet 夹具初始化失败日志另存，不作产品通过证据。
- `tests-initial.xml/log` 与 `tests.xml/log`：首轮和最终受影响测试结果；`build/receipt.json` / `build.log`：独立构建。
- `original-wip.json`、`final-audit.json`、`HANDOFF.txt`：WIP 哈希与最终交接。原 demo pause、root dev 和所有原 WIP 均保留。未运行或操纵用户 Player，未改变测试盒子启用状态、未删除场景物体。

下一模块继续 owner-keyed 消费者向 RacerId 迁移、技能/排名/checkpoint 权限及结算；当前修复只解决已复现的 tracker 数据缺陷。
