# S1 Practice 独立 benchmark

本协议只测 **Solo / Practice / 0 AI**。联机 V2、Steam 双人、Solo/Online 交替和旧多人 Editor R8 均不属于前置或通过门槛；没有执行的联机项应写“不适用”，不能写“通过”。保留已有联机资产，不修改它们。0 AI 的结果不能外推成 1/3/5 AI 性能。

当前交付包括离线分析器、协议与 opt-in Player 采样/操作器源码模板；采样接点已在 Unity 2022.3.55f1c1 非 Development Player 编译成功，**已取得三个 Release 诊断窗口，但 GC 缺失和退出后配置摘要冲突尚未解决，完整耐久验收未完成**。Python 分析器不会启动进程；主集成任务统一构建、运行唯一 Player。运行入口见工具 README，数据状态以 progress.md 的实际验收记录为准。

工具入口：[benchmark.py](../../Tools/SinglePlayerBenchmark/benchmark.py)。字段与主接点详见 [sampler-interface.md](../../Tools/SinglePlayerBenchmark/sampler-interface.md)。既有单机功能要求见 [phases.md](phases.md) 和 [design.md](design.md)。

## 两条证据轨道

| 轨道 | 必须具备的证据 | 不能据此声称什么 |
|---|---|---|
| 性能窗口 | 独立 Player、3 次同配置物理驾驶，每次至少 30 秒驾驶预热、连续 60 秒逐帧采样 | 不能仅凭窗口证明完整生命周期或全部 S1 验收 |
| 功能与生命周期 | 既有 strict14 的自然完赛、计时、真实结果按钮、清理/重开；另附 selection/intro/driving 三阶段 Esc 回执 | `sequencePassed` 不能替代 frame time、分配率、内存预算或计时精度结论 |

可以复用已完成 strict14，不要求为了逐帧采样另跑 14 局。两条轨道必须有相同的**已提交游戏源码 HEAD**，并分别保留构建产物和采样/驾驶器 SHA256；不同 instrumentation 构建应如实标记。未完成、旧 HEAD、无法确认构建来源的数据不能冒充本次完整验收。工具拒绝 dirty 源码资格认定，避免跨构建只凭相同 HEAD 混入不同玩法代码；私有采样辅助源单独哈希留档，不伪装成普通发行包。

## 可复现前置与配置

仅需现有 Python 3.10+，无第三方依赖。采集前由主操作者确认：

1. 一个 Player 执行采集，避免同时跑 Editor、其他 Player 或性能负载。保留应用内改动并在 finally 恢复，不修改全局机器设置。
2. 记录实际 Release/Development 类型、Unity/游戏版本、脚本后端、完整 HEAD、instrumented 构建哈希、操作系统/CPU/GPU/图形 API/内存容量；Editor 数据不能当 Player 基线。
3. 请求与实际窗口尺寸、画质等级、targetFrameRate、vSync 均有记录。模板默认 1280×720、quality index 2、60 FPS、vSync 0；采集前核实该画质索引有效，必要时明确修改请求。工具要求请求和实际一致，并检查每个测量窗口。实际 FPS 由帧数/实测秒数计算，不能拿请求的 60 填充。
4. S1 配置为 0 AI、RaceMap 三圈、seed 1729。每次 repeat 进入选择/开始会话前应用 seed；固定 route 及其内容哈希、技能 ID、皮肤 ID、起跑与采样锚点。记录实际生效 seed，不能仅记录 CLI 请求。
5. 复用现有 A/D Dynamic InputSystem 反馈控制器，只从赛道/刚体读取观测，通过普通键盘输入驾驶。控制器源代码和参数一起哈希；实际 tick/key 输入流另计算 SHA256。它是“固定控制策略的反馈驾驶”，**不是固定按键回放**，也不是 S1.5 AI 实现。相同 seed/策略不保证 Unity 浮点物理逐位相同；不同输入流哈希如实保留，不强行要求相同。
6. 禁止 teleport、位置/速度赋值、修改 lap/progress、手工 Finish、模拟结束、手动 Physics/InputSystem.Update、改 timescale 来制造成绩。输入身份、物理驾驶说明和干预计数均为证据的一部分。

配置入口不会执行任何游戏动作：

```powershell
# 在集成后的仓库根运行；目录由操作者选为本地私有证据目录。
$capture = Join-Path $env:TEMP 'BuddahGo-Solo-Capture'
python Tools/SinglePlayerBenchmark/benchmark.py plan --out "$capture/run.json"
```

`plan` 不覆盖已有文件，输出含 null 的待填模板。主任务依据真实构建/采样回填，不把模板当成功回执。所有字段定义见采样接口；[test_benchmark.py](../../Tools/SinglePlayerBenchmark/test_benchmark.py) 的 `make_fixture` 是完整、明确标为 synthetic 的可执行示例。

## 性能窗口与计算

版本化策略 `s1-30s-60s-3-v1` 固定三个 repeat。每次使用新的一局、同一起跑策略；运动解锁后自然驾驶预热至少 30 秒，之后记录连续 60 秒，至首次达到窗口长度的完整帧即结束。三个窗口不能重叠，不能跨场景、拼接不同局、包含结果停留或强制 GC。窗口内结束比赛、丢帧、溢出、时间倒退或字段错位均无效；不能截取一段“好帧”替代完整窗口。

CSV 的 frame index、unscaled frame time、elapsed time、GC 和 main-thread counter 必须对齐同一已完成帧。预分配缓冲，窗口结束后统一写盘。计数器开销及私有驾驶器也属于该 instrumentation 配置；比较时保持一致。现有每秒 `drive` 或 VJitter 日志无法推导 p95/p99，也不能转换成逐帧数据。

每个 repeat 报告：

- 实测秒数、帧数、实际平均 FPS；frame time 的算术均值、p95、p99（毫秒）。分位数定义为 nearest rank：升序第 `ceil(p*n)` 个样本。
- GC allocated bytes/frame = 总分配字节 / 全部测量帧数；bytes/second = 总分配字节 / 实测窗口秒数。只要一个 GC 样本缺失，该窗口两种分配率均为 null，不用有效帧子集制造完整速率。
- 可选 main-thread 均值/p95/p99，与 frame time 分开。main-thread 时间不是帧间隔。不可用时为 null。

预算使用三个 repeat 中各指标的最差值。没有把跨 repeat 的 p95/p99 取平均并叫作总体分位数；每次分布完整保留。默认预算全部 null，**代表尚未确定，不能自动通过**。第一份完整真实采集可作为基线候选材料，由主任务在采集前声明/固定可接受预算；不得看完结果不断放宽直到“通过”。不同预算的报告要明确区分。

## strict14 生命周期与计时

复用现有 `SoloContinuousAcceptanceRuntime` 原 JSONL，按 `kind` 解析，仅使用该种类真实采样的字段，不把 C# Evidence 类其他零初始化字段视为测量值。

```text
M1 R M2 R M3 R E3 H -> Home -> M4
M4 R M5 R M6 R E6 H -> Home -> M7
M7 R M8 R M9 R E9 H -> Home -> M10
M10 R E10 H -> final Home
```

M 为 10 个主样本；E 是 4 场额外完整物理比赛；R 是结果页 Rematch；H 是结果页 ReturnHome。主样本每三局（及最后一局）通过额外比赛返回首页；**不是每第三场物理比赛立即返回**。这保留了每个主样本重开和三次连续 Rematch。`draft10-selection-exit` 不是等价方案，不被本版本接受。

验证器要求每局 selection-ready、GO、movement-unlocked、drive、自然完赛、结果交互、结果停留、race-completed、正确按钮及相应 Home 的有序证据；10/4/10/4 的摘要计数只做交叉检查。第一结果至少停留 35 秒，其余至少 2 秒。每局 1500 秒、返首页 120 秒、整体 7 小时上限沿用现有 harness。失败/Error/Exception/Assert/incomplete 回执使轨道失败。

每局三圈时长为有限正数，总时长与圈时长之和误差不超过 1e-6 秒；圈观察序列必须完整，原始观察残差与重新计算结果一致。**现有计时观察是逐帧轮询，报告间隔默认 100ms，不是精确 OnTriggerEnter 时间戳。** 工具将 `timing_consistency=VALID` 与 `timing_precision=NOT_MEASURED` 分开。benchmark 的通过只覆盖本协议的计时一致性，不是完整 V3 精度认证。需要精度认证时，由主任务另接真实触发时间戳并按设计标准验证，不能把 explanatoryObservationBudget 当设计精度阈值。

结果页对象须为 1 个 BuddahMovement、1 个 PlayerProgressReporter；干净 Home 均为 0。NetworkObject 数量包含 inactive，仅比较相同阶段，不能拿 Home 和比赛场景直接相减。主样本前两局作生命周期内存预热，使用 M3–M10 的 results-after-hold 检查点；另比较 4 次完整返回 Home。

只在稳定检查点强制 GC，驾驶测量窗口不强制 GC。使用 `managedAfterFullGc` 分析保留内存：报告数列、相对首个稳定样本的最大正增长、最终差值和最小二乘斜率（每主样本）。结果阶段和 Home 分别计算，两者取较坏值检查预算。不能仅从最后值较低推断没有峰值/持续增长；样本和斜率同时保留。Unity total allocated、Mono used/heap 分开列为辅助值；不支持或为零的 profiler 值标记为缺失，不宣称内存为零；也不把它们混称 GC 分配率。这里只判定指定样本/预算，不是通用泄漏证明。

三阶段 Esc 回执必须记录真实 Escape、对话框可见、输入阻断、正常 ConfirmQuit、停止会话、清空 Clock/Timing、对象清理及重新进入 Practice，时序必须递增。这些退出探针独立于 14 场完整比赛和性能窗口，不能用中途退出补足完赛数。

## 文件、运行及退出码

私有 bundle 固定包含 `run.json`、`frames.csv`、`endurance.jsonl`、`escapes.jsonl`。manifest 中 SHA256 绑定三份数据，防止错配/被截断后还使用旧清单。它不提供防伪认证；原始日志、构建清单与驾驶器来源需由主任务保留审阅。报告不回显原始 note、stack 或文件路径。

```powershell
# 填写真实采集后；这些命令只读分析，不执行游戏。
python Tools/SinglePlayerBenchmark/benchmark.py run --bundle "$capture" --out "$capture/report.json"

# 可选：比较已独立建立的同配置真实 Solo 基线；两边都必须完整合格。
python Tools/SinglePlayerBenchmark/benchmark.py run --bundle "$capture" --baseline "$baseline" --out "$capture/comparison.json"

# 仅检查已有 strict14；即使流程完整也不能宣称 benchmark 通过。
python Tools/SinglePlayerBenchmark/benchmark.py inspect-strict14 --input "$strict14" --out "$env:TEMP/solo-endurance-review.json"
```

| 状态/退出码 | 含义 |
|---|---|
| `PASSED` / 0 | 真实 bundle 满足本协议完整性与预声明预算；仍不是所有 S1 验收项的证书 |
| `NOT_PASSED` / 1 | 证据缺失、阶段不完整、指标无效、未设预算、超预算或不兼容比较 |
| `SYNTHETIC_ONLY` / 1 | 合成数据，可验证工具计算；永不宣称实际游戏通过 |
| CLI 错误 / 2 | 参数/读写失败或试图用报告覆盖原始证据 |
| `PLAN_ONLY` / 0 | 只生成计划，无采集、无验收结论 |

可选 baseline 比较要求 profile、设备、请求/实际设置、workload（含 route/controller hash 和 seed）、采样策略、Unity/后端/构建类型一致，允许源码 HEAD/游戏版本不同。输出指标绝对差值，不凭旧多人 R8 或不同机器数据判定回归。未设百分比回归预算，不能把这个差值报告当额外自动回归门槛。

## 工具自身验证与后续扩展

```powershell
python -m unittest discover -s Tools/SinglePlayerBenchmark -p "test_*.py" -v
python Tools/SinglePlayerBenchmark/test_benchmark.py --fixture "$env:TEMP/solo-synthetic-example"
python Tools/SinglePlayerBenchmark/benchmark.py run --bundle "$env:TEMP/solo-synthetic-example" --out "$env:TEMP/solo-synthetic-report.json"
```

合成目录必须不存在，避免覆盖证据。最后命令预期 `SYNTHETIC_ONLY`/退出 1，不应改成“实际基准通过”。测试包含完整与损坏输入、数值单位、分位数、GC 缺失、时序/圈数/阶段、对象残留、内存趋势、哈希错配、配置不兼容和输出保护。

后续 AI 阶段应新增版本化 profile，声明 1/3/5 AI、难度、规划/技能负载与独立基线，再扩 schema/采样/测试；本版本直接拒绝非 0 AI。本任务不实现 AI，也不改变单机生产玩法或已有联机验证资产。

后续 AI 实施/验收顺序与用户启动门槛见 [ai-testing.md](ai-testing.md)；23:51 UTC 已明确授权在 PR #59 从 A1 开始，所需最小采样/验证按该阶段实施；A1 已有自然圈/模型采样；run04 最近规划耗时采样均值 6.50ms、最大 16.38ms 仅用于诊断，不是正式 profiler 或五 AI 性能结果。现有协议仍限 0 AI，完整 AI benchmark 尚未扩展合格，现有未验证项不因此变成通过。
