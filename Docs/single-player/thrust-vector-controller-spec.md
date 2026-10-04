> **2026-10-04:** the V5 beam planner (`ForwardSimPlanner`), the `Legacy*` reference copies, the `ThrustVectorRevision*Fixture` frozen revisions and the beam-only profile fields (`UseThrustVector`, `BeamWidth`, `HorizonSeconds`, `ControlSeconds`, `ReplanTicks`, `CorneringFactor`, `LateralGain`, `LateralWeight`, `LateralVelocityWeight`, `SpeedWeight`, `ProgressWeight`, `DiverseSearch`) were retired; `ThrustVectorPlanner` is the only controller and `ThrustVectorSpecTests` its acceptance. References to them below are historical. The 2026-10-04 round-4 cleanup also removed their evidence and the superseded thrust-vector iterations from the branch: apart from `Tools/ai/difficulty-*.json`, `Tools/ai/fixtures/`, `Tools/ai/thrust-vector/scripts/` and the tier runs `rollout-2026-10-02/player/race4-*`, `race5-easy`, `race6-easy`, every `Tools/ai/...` path below resolves at commit `20401e5` (`git show 20401e5:<path>`).

# ThrustVectorPlanner 最终规格（destination）

2026-10-02；基于 `68e4d70`（`feat/single-player-mode` / PR #59）。
本文是终点规格，取代 review-planning-cost-68e4d70.md 第六节的逐次修正。实现者按本文实现、按第六节固定流程校准、按第七节门槛验收。规格之外的规则改动需要先写进本文再实现。

## 一、目标

| 项 | 可接受 | 目标 |
|---|---:|---:|
| 真实三圈平均圈速（Normal，无 modifier） | ≤ 118 s | ≤ 116 s |
| 整圈旋转（相对路线展开朝向） | 全程 ±180° 内 | 同 |
| 侧向接触 / 圈 | ≤ 25 | ≤ 15 |
| 离线无墙整圈 | 完成，max \|e\| ≤ 8 m | max \|e\| ≤ 5 m |
| 换侧次数 / 圈 | ≤ 15 | ≤ 8 |
| 五 AI 性能（GO 后 2–11 s 窗口） | 中位帧时 ≤ 20 ms，net ticks ≥ 540/9 s | 同 |
| 模型误差无接触窗口通过率 | ≥ 293/297 | 同 |

对照：V5 beam 真实单圈 118.1 s，A2 三圈 116.5 s，spin-fix 三圈 126.0 s。

## 二、车辆常量（实测，不可调）

| 符号 | 值 | 来源 |
|---|---:|---|
| a | 25 m/s² | 推力 50 / 质量 2 |
| v_max | 80 m/s | 速度上限，模型 ClampMagnitude |
| α | 4.82 rad/s² | 车头角加速度，实测（multiplier 2 生效） |
| β | 3 rad/s² | 空挡角速度衰减 |
| 地面摩擦 | 1.96 m/s² | 忽略 |

推论：横向加速度 = a·sin θ，纵向 = a·cos θ，θ 为车头与速度方向夹角。θ < 90° 必加速，> 90° 必减速，= 90° 匀速。弯内自然平衡 v = sqrt(a/κ)。

## 三、架构（固定）

```
路线层（每赛道一次）      采样、切线、切线角、曲率、pace 曲线
引导层（每 tick）         投影 → 参考系 → 横向需求 a_lat → 速度模式 → θ → e_target
姿态层（每 tick）         e_car 连续、φ、开关曲线、键 {-1,0,+1}
难度层（后做）            τ、margin、deadband、反应延迟、噪声、扭矩 modifier
避让层（可选，后做）      3–5 个横向偏移的短闭环 rollout
```

### 路线层

- 采样间距 2 m（沿用 `SplineRacingLine.Capture`）。每段存切线、切线角（预计算 atan2）、曲率 κ（12 m 跨度）。
- 曲率算法已由圆弧 fixture（半径 150 m）验证：两条 span 长弦的夹角等于其中点间的切线转角，中点相距一个 span，所以角度除以平均弦长正确，复现 1/150。（2026-10-02 曾误判为高估一倍并改为除以两弦之和，圆弧 fixture 立即暴露为减半，已撤回。）
- pace 曲线：pace[i] = min(v_max, sqrt(a · f_pace / κ[i]))，向后回传 pace[i] = min(pace[i], sqrt(pace[i+1]² + 2 · a_brake_plan · step))。
- **设计值**：f_pace = 0.90，a_brake_plan = 10 m/s²。依据：减速分支下限 120° 给 12.5 m/s²，车头摆到 120° 约需 0.5–1 s，按 10 规划留出摆动余量；f_pace 0.9 让弯内加速解与减速解靠近 90°，减少切换。校准范围见第六节。

### 引导层

1. **投影**：`Project(position, nearSegment, 8)`。若 |e| > 15 m，改用 ±40 段并优先保持段连续（避免平行/交叉路段跳段）。
2. **参考系**：v ≥ 5 m/s 且 |e_v| ≤ 90° 时用速度方向角 ψ_v；否则**恢复模式**：参考系取切线，e_target = 0，速度模式 = ACCEL。e_v = wrap(ψ_v − tangentYaw)。
3. **横向需求**（两遍计算，不迭代）：
   - 第一遍：用未预测的 e 按第 3–6 步算临时 a_lat、θ、e_target；在连续分支上 Δθ = 临时 e_target − e_car，T = sqrt(2 · |Δθ| / α) + |ω| / α，clamp [0.3, 1.0] s。
   - 第二遍：e_pred = e + ė · T + ½ · a_lat_now · T²，a_lat_now = a · sin(θ_now) · side_now；用 e_pred 重算最终 a_lat、θ、e_target。T 不再更新。
   - a_corr = −k_p · e_pred − k_d · ė，k_d = 1.8 · sqrt(k_p)（阻尼比 0.9）。k_p 设计值 0.3，对应 k_d 0.99。
     依据（2026-10-02 修正）：横向是双积分，k_p 是刚度，固有频率 sqrt(k_p)；e_pred 的 T 只补偿车头滞后，自身阻尼比仅 0.06–0.2，必须有显式 k_d。车头约 1 s 滞后把横向带宽限制在约 0.5 rad/s，收敛时间 8 s 量级，k_p 超过 0.5 进入滞后不稳定区。
   - a_lat = v² · κ(s) · sign(弯向) + a_corr，clamp ±a。κ 用**当前位置**。
   - side = sign(a_lat)，死区：|a_lat| < 3 m/s² 时沿用上一 side。
4. **速度模式**（迟滞）：v_ref = min(pace[s .. s+L])，L = v · τ，τ 设计值 1.0 s；margin 设计值 2 m/s。
   - v < v_ref − margin → ACCEL
   - v > v_ref + margin → BRAKE
   - 带内且 |a_lat| ≥ 18 → HOLD；带内且 |a_lat| < 18 → 保持上一模式
   - v ≥ 79 且 v_ref ≥ 78 → ACCEL（上限由钳制处理）
5. **θ**（相对速度方向的夹角大小）：
   - ACCEL：asin(min(|a_lat|, a) / a)
   - HOLD：90°
   - BRAKE：max(120°, π − asin(min(|a_lat|, a) / a))，上限 178°
6. **目标**：e_target = e_v + side · θ，**不 wrap**，clamp ±178°。

### 选择层（2026-10-02 修订：rollout 选择取代直接采用引导层 θ）

设计值 lap 的逐 tick 轨迹（`Tools/ai/thrust-vector/spec-fixtures-2026-10-02/design-lap.csv`）证明闭式引导的隐含假设不成立：车头从 71° 摆回 5° 需要约 1 s，这 1 s 内 25 m/s² 的推力扫过所有中间角度，实际横向加速度达 15 m/s² 而指令只有 1–2 m/s²；每次 ACCEL/BRAKE 切换都是 90–150° 的摆动。推力恒定、姿态滞后 1 s 的系统不能把 θ 当作瞬时可达。beam 能开是因为它把摆动仿真进去了。

因此引导层的 θ 只作为**锚点候选**，最终 θ 由短闭环 rollout 选出（规格第七节停止规则中的既定退路，提前启用）：

- 候选：引导层锚点 θ、上一 tick 选中的 θ、固定集 {0, ±30, ±60, ±90, ±120, ±150, ±170}°，共 15 个，全部 clamp 到 ±MaxThrustAngleDegrees（设计值 170°）。
- 每个候选用姿态层 + `BuddahMotionModel` 闭环推演 RolloutSeconds（设计值 1.5 s，90 tick），每 RolloutSampleTicks（6）投影一次（±6 段窗口），目标 e_target = e_v(t) + θ 随速度方向更新。
- 成本：Σ 样本 dt·(LateralWeight·e² + LateralVelocityWeight·ė² + SpeedWeight·(v_fwd − min(TargetSpeed, pace))²) − ProgressWeight·advance，权重沿用 V5（1 / 0.7 / 1 / 5），pace 为带回传的曲线值。另加 SwitchPenaltyPerDegree·|θ − θ_prev|（设计值 0.03/°）作为切换迟滞。
- 取最小成本候选，e_target = clamp(e_v + θ, ±170°)，交姿态层。
- 恢复模式（v < 5 m/s 或 |e_v| > 90°）不 rollout，e_target = 0。
- 成本：15 × 90 = 1,350 模型步 + 225 次 13 段投影，约为 beam 单次 Plan 的 4%，每 tick 执行。

ACCEL/HOLD/BRAKE 模式、k_p/k_d、e_pred 仍用于生成锚点与观测字段，不再直接决定输出。

### 姿态层

- e_car：车头相对切线的误差，连续累积（每 tick 加真实 yaw 增量、减切线转角），不 wrap。
- φ = e_target − e_car，不 wrap。
- remaining = φ − ω · |ω| / (2β)。|remaining| < deadband → 0，否则 sign(remaining)。deadband 设计值 1.5°，迟滞 0.5°。
- 数值平移：|e_car| 与 e_target 任一超出 ±360° 时同步平移 360°，φ 不变。
- **外部恢复**：|e_car| > 200°，或 0.5 s 内有碰撞标记且 |e_car| > 180° → e_car 重锚为 wrap 后最短值。控制器自身永远不命令超过 178°。

### 不变量（写成断言）

- 控制器输出只有 {-1, 0, +1}，油门恒 1，经 `BuddahPredictedMotor` 施加。
- |e_target| ≤ 178°，|θ| ≤ 178°。
- 非恢复模式下 |e_car| ≤ 200°。
- 车头连续性：任意 tick 的 Δφ 与 Δe_target 之差（即 −Δe_car）绝对值 < 10°，豁免重锚与恢复模式进出。rollout 选择使 e_target 可在相邻 tick 间任意变化，所以不变量只约束 e_car 的连续性，这是接缝缺陷的直接判据。（原规则：φ 变化 < 10°，豁免四种情形：side 刚换（允许一次 ≤ 360°）、刚重锚、速度模式切换（ACCEL/HOLD/BRAKE 之间）、恢复模式进出。模式切换豁免附加条件：side 不变且 |e_target| ≤ 178°，即车头在同一侧经过 90° 摆动，不经过车尾。已被上述规则取代。）

## 四、与 motor、物理、权威的关系（不变）

- 不绕过 `BuddahPredictedMotor`，不写 Transform/Rigidbody/速度，不沿样条插值，不给 AI 单独 tick。
- Normal 难度零 modifier。扭矩 modifier、橡皮筋只在难度层，经 `BuddahPredictedModifierResolver`。
- `ForwardSimPlanner`、`Legacy*.cs`、`AICostEquivalenceTests` 保留为对照。

## 五、测试（全部为 EditMode，必须保留）

| fixture | 断言 |
|---|---|
| 制动（76.56 → 41.11） | BRAKE 分支 θ ≥ 120° |
| 换侧 | 换侧经过切线方向（e_car 经过 0），不经过车尾 |
| 外部旋转 300° | 重锚后顺转 60° 回正，而非反向 300° |
| e_target 跨 ±180°（7.267 s） | φ 变化 < 5°，输入不反向 |
| **圆弧**：半径 150 m 圆形赛车线，车在线上、v = sqrt(a · f_pace · 150) ≈ 58 m/s 沿切线，5 s | 首 tick θ 朝向圆心；全程 \|e\| < 3 m。左转/右转圆各一次。单独判定 κ 符号约定，直线 fixture 测不到 |
| 直线偏移 10 m、50 m/s、**8 s** | e 单调减小，过线 ≤ 2 m，**8 s 末 \|ė\| < 0.5 m/s**，FinalSteeringSign ±1。4 s 窗口超出物理（带宽约 0.5 rad/s），已放宽 |
| 弯道入口（当前 52% 饱和的代表 tick） | 用设计值参数时 a_lat 不饱和，或饱和由 v²κ 主导（≥ 70%） |
| 离线无墙整圈 | 完成；max \|e\| ≤ 8 m；换侧 ≤ 15；展开朝向 ±180° 内 |
| 不变量 | 第三节四条断言在整圈 CSV 上全程成立 |

## 六、校准流程（一次网格，一次选取，不迭代）

离线无墙整圈，网格：

| 参数 | 取值 |
|---|---|
| k_p（k_d = 1.8·sqrt(k_p)） | 0.15, 0.30, 0.45 |
| τ | 0.8, 1.0, 1.2 |
| margin | 1.5, 2.0, 3.0 |
| f_pace | 0.85, 0.90, 0.95 |
| a_brake_plan | 10, 12.5 |

共 162 组。选取规则，按序：完成整圈 → 展开朝向 ±180° 内 → 换侧 ≤ 15 → max |e| 最小 → 圈速最小。取前 3 组进真实 Player 单圈，再取最优 1 组跑三圈。结果表写入交付文档，包含全部 162 组的完成/换侧/max |e|/圈速。

deadband、T 的 clamp、side 死区、HOLD 阈值 18、恢复阈值 200° 不进网格，是设计值。

## 七、验收顺序与停止规则

1. 用设计值跑圆弧与直线 fixture（只测符号与基本收敛）。圆弧不过即为 κ 符号问题，先修，不跑网格。其余 fixture 的设计值结果记录，不作门槛。
2. 第六节网格；至少一组满足"完成 + ±180° + 换侧 ≤ 15"。
2a. 用网格选出的参数复验第五节全部 fixture，阈值不变。设计值失败记录保留在交付文档。
3. 真实单圈 ≤ 125 s、接触 ≤ 40（中间门槛），否则回到 2 查 CSV，不改规则。
4. 真实三圈达到第一节可接受值。
5. 五 AI 性能窗口。
6. 难度层：Easy/Hard 用 τ、margin、deadband、反应延迟、噪声、扭矩倍率；每车不同 LateralOffset 与噪声种子。

**停止规则**：第 2 步 162 组无一满足，不再改规则，切换到混合横向：速度/刹车层不变，横向改为 3–5 个 side·θ 偏移（±0°、±10°、±20°）的 1.5 s 闭环 rollout（`BuddahMotionModel`，90 步/候选），按 max |e| 与进度打分，每 0.5 s 重选。成本约 beam 的 3%。此为规格内的既定退路，不是新方案。

## 八、交付

- 代码：`ThrustVectorPlanner`、路线层扩展、`PlanObservation` 字段（ψ_v、e、ė、e_pred、a_lat 三分量、mode、side、θ、e_target、e_car、φ、key）。
- 测试：第五节全部。
- 文档：`Docs/single-player/thrust-vector-controller-<日期>.md`，含网格结果表、真实单圈/三圈对照、性能窗口、未解决项。
- 提交信息注明基于 68e4d70。

## 十、实施记录（2026-10-02，接手实现）

基于 68e4d70 工作区，实现者的 `ThrustVectorPlanner`、fixture 与测试保留，在其上修改。全部 EditMode 运行通过 Unity batchmode；轨迹与结果在 `Tools/ai/thrust-vector/rollout-2026-10-02/`（每轮运行前一版移入 `superseded-*`）。

| 轮 | 改动 | 离线整圈 | 圆弧 fixture |
|---|---|---|---|
| run1 | 常数 θ rollout（15 候选、1.5 s）；V5 权重 | DNF 32.8 s，max\|e\| 169 m | 失败：起始车头沿切线不可行 |
| run2 | 误判曲率高估一倍并改算式 | 同前 | 暴露曲率被减半 → 撤回 |
| run3–4 | 曲率撤回；圆弧 fixture 改稳态起步；摩擦圆 pace | DNF 32.4 s | 通过（0.8 / 2.8 m） |
| run6 | 整圈起步 60 → 20 m/s；候选成本写入轨迹 | DNF 41.7 s，max\|e\| 124 m | 通过 |
| run7 | 非对称速度权重 4/0.25、进度 0.5 | DNF 27.9 s（刹到爬行） | 11.3 m 失败 |
| run8 | 对称 1/1、进度 1、切换惩罚 0.3/° | DNF 30.6 s | 6.0 m |
| run9 | **两段式候选**：θ 保持 0.6 s，之后按锚点律推演 | **完成 119.233 s**，RMS 34.3 m，max\|e\| 106 m，换侧 30，朝向 −198～168°，不变量 0 违例 | 3.45 m |
| run10 | 二层显式 (θ1, θ2∈{0, 90, 170}·弯向) | DNF 35.8 s，出现 −525° 旋转 → 撤回 | 5.5 m |
| run11 | 回到 run9 结构 | 同 run9 | 同 run9 |

关键发现：
1. **候选形态比成本权重重要。** 常数 θ 保持整个时域时，"刹车"候选被末段低速与进度损失压过，入弯不刹；两段式让"现在刹、之后按律"成为可比对象，整圈才首次完成。
2. **pace 曲线必须用摩擦圆。** 推力矢量车刹车与转弯共享 25·f 的圆，弯中可用纵向 = sqrt(circle² − (v²κ)²)。常数 10 m/s² 回传在弯中承诺了给不出的减速。
3. **曲率算式正确**，圆弧 fixture（R = 150）精确复现 1/150；一次误改被 fixture 当场抓住。
4. **离线无墙整圈的门槛要重新校准。** beam 在同一离线模型中 86.6 s DNF、RMS 44.9 m；真机 20 次/圈的侧接触说明它本就靠墙修正。新控制器离线完成且 RMS 34 m，已优于 beam 的离线表现，但距 8 m 门槛很远。真实判据只能在真机上比（同一构建、同一 harness）。
5. 第二阶段若用"当前弯向"的固定角度（run10），S 弯处成本估计反向，比锚点律差；锚点律虽单独驾驶不佳，作为 rollout 末段策略足够。

当前设计值：k_p 0.3 / k_d 0.986 / τ 1.0 / margin 2 / f_pace 0.9 / a_brake_plan 10 / rollout 2.0 s / hold 0.6 s / 采样 6 tick / 权重 e² 1、ė² 0.7、超速 1、低速 1、进度 1、末段 1 / 切换 0.3 per° / θ 上限 170°。文件：`Tools/ai/thrust-vector-design.json`。

成本：21 候选 × 120 tick ≈ 2,520 模型步 + 420 次 13 段投影，每 tick 一次。beam 每次 Plan 24,651 步 + 68,475 段，每 3 tick 一次。按步数折算约为 beam 的 30%/tick，10%/s 口径下仍是显著节省但不是数量级；若真机帧时不达标，先改 `EffectiveReplanTicks` 为 2–3，再 IL2CPP。

真机验收：`AITuningHarness` 新增 `--ai-count N`（服务器 AI 共享加载的 profile）与 GO+2..11 s 帧时窗口（summary.json 的 perfMedianMs / perfP95Ms / perfTicks，perf.csv）。构建脚本 `Assets/Editor/ThrustVectorAcceptanceBuild.cs`（Development；当时输出在仓库外 `Documents/Codex/2026-10-02/claude-thrust/`，2026-10-03 起默认输出到项目内被 git 忽略的 `Logs/thrust-vector-build/`，可用环境变量 `THRUST_BUILD_OUTPUT` 覆盖）。`Tools/ai/thrust-vector/scripts/lap.sh`、`race.sh` 会把每次运行的 summary/configuration/events/perf（race 另含 race-results）复制到 `Tools/ai/thrust-vector/rollout-2026-10-02/player/<name>/`，所以逐次副本会进入仓库（CSV/JSONL 经 Git LFS）。真机结果见下一节（运行后补）。

## 十一、墙走廊：墙是这辆车的刹车（2026-10-02）

用户指出设计意图：游戏不设刹车，因为墙没有摩擦。贴墙滑过弯道只损失撞墙瞬间的法向速度分量，切向速度保留，向心力全由墙提供，推力可以继续全部向前；擦边角越小损失越小。玩家在合适的弯用刹车走线、在合适的弯擦墙，按弯选择。此前的 pace 曲线（自己提供全部向心力）和中线目标都把墙当障碍，方向反了；beam 与跟线版的圈速都是"刹车派"成绩。

实现：
- `TrackWallProbe.Measure`：沿赛车线每个采样点从 1.5 m 高向左右射线（80 m，忽略 trigger），得每段左右墙距。`SplineRacingLine.Capture` 在服务器 play mode 下采集一次并缓存在共享几何中；离线测试读 `Tools/ai/fixtures/track-walls.json`（Editor 工具 `TrackWallFixtureTool.Build` 对 RaceMap 生成；全线 2,869 个采样两侧均有墙，中位 20.5 / 22.7 m，最小 0.4 / 0.8 m）。
- rollout 走廊（`UseWallCorridor`）：采样时若 |e| 超过该侧墙距 − `WallMargin`（1.5 m），抹掉向外的法向速度、位置贴回墙面、切向速度保留。成本：走廊内弱居中 `RolloutCenterWeight`（0.02）·e²，撞墙损失 `RolloutWallWeight`（0.2）·v_n²，**超速项权重 0**（中线 pace 是自转弯极限，有墙时不适用；速度由撞墙损失与进度约束）。低速项、进度项不变。
- 离线世界模型加同一条无摩擦墙规则；观察器在 |e| ≥ 15 m 时不再把投影跳段判为失败（发卡弯贴墙时中线投影合法跳段）。
- 结果：离线墙圈 98.9 s（无墙模型跟线 119.7 s），RMS 16.8 m，最大 27.8 m，约 24% 时间贴墙，朝向 ±177° 内。真机单圈 **97.6 s**（跟线版 109.95，同构建 beam 114.6），95 次接触为刻意擦墙，0 运行错误，平均速度 62.9 m/s，展开朝向 −171°～183.5°。
- 每个弯刹车还是擦墙由 rollout 按成本决定，不设全局开关。profile：`Tools/ai/thrust-vector-wall.json`。

边界：墙的物理摩擦/弹性按零假设（PhysicMaterial 默认），真机与离线圈速 97.6 / 98.9 的一致性支持该假设；车车碰撞不在模型内；`WallMargin`、`RolloutWallWeight` 未做网格校准。

### 第二阶段策略与撞墙权重的离线对比（2026-10-02）

用户观察：有墙可用的弯仍有"回头硬减速"。原因是候选只保持 0.6 s，之后按锚点律（跟中线、按中线 pace 刹车）推演，所有候选的未来都被算成会刹车，"现在不刹"的收益被抹平。新增 `RolloutTail`（0 锚点律 / 1 保持候选角 / 2 全推力向前），离线墙圈：

| tail | 撞墙权重 | 圈速 s | RMS m | 换侧 | 朝向范围 |
|---|---:|---:|---:|---:|---|
| 0 锚点律 | 0.20 | 98.90 | 16.8 | 40 | −177..168 |
| 0 | 0.05 | 95.95 | 16.0 | 43 | −177..169 |
| **1 保持候选** | 0.20 | 95.00 | 15.2 | 14 | −155..152 |
| **1 保持候选** | **0.05** | **92.67** | 15.3 | 12 | −154..152 |
| 2 全推力 | 0.20 | 99.45 | 17.2 | 23 | −178..166 |
| 2 | 0.05 | 98.35 | 18.1 | 15 | −185..167 |

围绕 tail=1 的二次扫描：权重 0.05 / 0.02 / 0 在 92.7–93.1 s 之间持平；时域 2.6 s 反而慢到 94.2–94.5 s。锁定 tail=1、权重 0.05、时域 2.0 s（`Tools/ai/thrust-vector-wall.json`）。

## 十二、难度层方案

全部走 profile 参数，Normal 零 modifier；每一项对应人类水平差异的一个方面，"敢不敢用墙"直接编码用户描述的两种过弯方式的取舍。

| 维度 | 参数 | Easy | Normal | Hard |
|---|---|---|---|---|
| 敢不敢用墙 | `RolloutWallWeight` | 1.0（怕墙，多刹车） | 0.2 | 0.05 |
| 反应 | `ReactionTicks`（既有机制） | 9 | 3 | 0 |
| 胆量 | `TargetSpeed` | 65 | 74 | 80 |
| 预判 | `RolloutSeconds` | 1.2 | 2.0 | 2.0 |
| 决策频率 | `ThrustReplanTicks` | 4 | 2 | 2 |
| 手抖 | 目标 θ 噪声（待实现，每车种子） | ±12° | ±5° | 0 |
| 走线个性 | `LateralOffset`、噪声种子（每车不同） | 各异 | 各异 | 各异 |

三份 profile 资产由 `SoloDifficulty` 选择；服务器 AI 各加种子避免同线。橡皮筋与扭矩 modifier 留给 Hard 以上或追逐感调节，不进基础难度。未实现项：θ 噪声、种子、`SoloDifficulty` → profile 映射。

### 禁刹：夹角上限 90°（2026-10-02）

用户要求"圈速优先，尽量避免减速"。`MaxThrustAngleDegrees` 90° 使候选集中不再存在刹车解（θ > 90° 纵向为负），低速权重 3 把速度推向 80。离线墙圈 83.0 s（170° 版 92.7）；同场比赛三圈 82.88 / 83.00 / 81.97，均 82.62 s，第一名。120° 版 89.7 s 居中。结论与用户的驾驭经验一致：墙没有摩擦时，减速几乎总是净损失。profile：`Tools/ai/thrust-vector-nobrake.json`。

同场比赛流程（`AITuningHarness --ai-profiles`，`race-results.json`）成为版本比较的标准方法：一次运行、同一车流、按完成圈数与总时排名。限制：产品规则在第一名完赛后结束比赛，其余车只记录已完成圈；车流互撞使单圈成绩有噪声，排名差距小于 2 s 时不作结论。

## 十三、难度实现与随机性（2026-10-02）

**产品路径**：`MatchSpawnManager.SpawnSoloAI` 在生成每个服务器 AI 时调用 `AIDifficultyProfiles.Resolve(settings.Difficulty, index)`，`AIRacerDriver.AdoptProfile` 持有并随车销毁该实例。三份资产 `Assets/Resources/AI/{Easy,Normal,Hard}.asset` 由 `AIDifficultyAssetTool.Build` 从 `Tools/ai/difficulty-*.json` 生成，保证比赛过的 profile 与产品加载的一致。`AIDifficultyProfilesTests` 校验三档可加载、均为推力矢量 + 墙走廊、档位单调（速度、反应、怕墙程度、Hard 不刹车）。

**随机性**（用户要求：AI 不能开得一模一样，宁可圈速波动）。两层，参数全在 profile：

- 静态个性（`Resolve` 时按每车每场的种子抽一次）：`SpeedNoise`（目标速度 ± m/s）、`LateralOffsetNoise`（走线偏置 ± m）；种子写入 `NoiseSeed` 便于复现。
- 动态随机（行驶中，`ThrustVectorPlanner` / `AIRacerDriver`）：`AngleNoiseDegrees` 高斯摆动，每 `WobbleTicks`（30）重抽一次，叠加在选中的推力夹角上，裁到 ±3σ；`MistakeProbability` 每次选择以该概率改取次优候选并保持一个选择周期（像人的误判，不会产生不可能的指令）；`ReactionJitterTicks` 在反应延迟上加 0..N tick 的随机量。

| | Easy | Normal | Hard |
|---|---|---|---|
| 算法 | 墙走廊 170°，怕墙（撞墙权重 1.0） | 墙走廊 170°，撞墙权重 0.2 | 禁刹 90°，撞墙权重 0 |
| TargetSpeed | 65 | 74 | 80 |
| ReactionTicks（+抖动） | 9（+0..6） | 3（+0..3） | 0 |
| ThrustReplanTicks / RolloutSeconds | 4 / 1.6 | 2 / 2.0 | 2 / 2.0 |
| SpeedNoise / LateralOffsetNoise | ±8 / ±4 | ±4 / ±2.5 | ±2 / ±1.5 |
| AngleNoiseDegrees σ | 12° | 5° | 2° |
| MistakeProbability | 0.30 | 0.10 | 0.03 |

Easy 列为首版；定稿（race6）为 TargetSpeed 67、撞墙权重 0.7、ReactionTicks 8（+0..5）、ThrustReplanTicks / RolloutSeconds 3 / 1.8、SpeedNoise / LateralOffsetNoise ±6 / ±3.5、σ 10°、MistakeProbability 0.25。产品数值以 `Tools/ai/difficulty-*.json` 为准。

验证方式：每档一场六车赛，5 个该档服务器 AI（产品路径 `--ai-product-profiles --ai-difficulty <tier>`）加 harness 驾驭的玩家 Buddah（Normal profile），看排名是否随难度变化、谁胜。结果见 thrust-vector-controller-2026-10-02.md。
