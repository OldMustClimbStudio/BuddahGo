# P5 预测运动与推击整理

## P5-1 原样拆文件

BuddahPredictedMotor 保持原 namespace、类名、主文件 GUID、序列化声明和继承。核心 tick/reconcile 留在主文件；Events、Rpc、DebugState、Shadow 为同类 partial。12 个连续源码片段逐字移动，按原位置重组与原文完全一致；再核对原 class body 的全部非空行在新文件中出现次数完全一致。没有改 tick 方法体、reconcile 数据、字段初始化或 RPC 相对顺序。影子字段与方法保留原完整条件宏；调用处宏留在核心/事件方法原位置。

R1：主 Editor 编译无新错误。R3/R7/R9 待 P5 阶段集中运行；核心 687 行，Events 751、Rpc 171、DebugState 246、Shadow 504（含各文件重复 using/namespace 外壳）。

## P5-2 提前返回收尾

I6 预检推翻“三个分支完全相同”的假设：writer relinquishment 有 ClearPendingForces 或 SimulateZeroVelocityPredictionStep 分支，之后还更新 handoff 并记录 writer；保持原样。仅 movement-blocked 和 rooted 两处完全相同的 ClearPendingForces→SetPredictionVelocitiesSafely→Simulate→FinalizeImpulseDebug→UpdateReplicateDebug 合并，status 参数仍各自为 blocked/rooted。调用位置与 return 保持，R7/R9 随阶段验证。

## P5-3 秒转 tick

三个计算表达式逐字相同，抽出 BuddahTickMath.DurationToTicks；保留外围三种 guard/返回语义，TimeManager.TickDelta 原位读取，CeilToInt、0.0001f 下限、uint cast 与 deadline 加法不变。两个拖尾重置循环存在 Unity `!= null` 与 CLR `?.` 差异，motor 还更新 debug 旗标，按 I6 不合并。R7 随阶段验证。

## P5-4 推击参数与纯计算

内部 18 参数连发方法改为非序列化的 ProjectileBurstParameters 值快照；三个 public 入口及所有 RPC/同步/序列化字段保持原样，各赋值顺序复刻原实参求值顺序。普通与蓄力连发的排布数学逐字相同，共用 ProjectileBurstPlanner.GetSpawnPosition；两者 spawn 动作不同（蓄力 clamp impulse/size、普通路径原样），不合并整段循环或改变碰撞参数。Collider size 的纯计算原式移到 planner。

PushAttackTiming 接收已按原短路条件算出的 active 状态，保留 action delay 的 `!(time >= until)` 与 cooldown 的 `time < until` 差别、正 charged cooldown 优先与非负普通值回退。两个 private cooldown wrapper 全 Assets C#/YAML/字符串搜索仅有各自定义及本类调用，移除后由纯 helper 承担；BuddahHandControl 类型本身有 YAML/C# 引用，完整保留。

R1 主 Editor 编译通过；tick rounding、minimum delta、普通/蓄力冷却、delay、奇偶/单发居中和 collider 下限共 22 检查通过。最初测试误将 float 0.001/0.0001 的 Ceil 当作 10；实际原表达式比值为 10.000001、结果 11，确认原行为后用精确单 tick 边界修正夹具，未改生产公式。R6 待实际双端矩阵。

P5-5 可选影子表驱动本轮不采用；影子计算/消息保持现有实现，仅随 P5-1 搬入 Shadow partial，避免无必要地扩大验证面。
