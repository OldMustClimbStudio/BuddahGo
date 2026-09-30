# P5 预测运动与推击整理

## P5-1 原样拆文件

BuddahPredictedMotor 保持原 namespace、类名、主文件 GUID、序列化声明和继承。核心 tick/reconcile 留在主文件；Events、Rpc、DebugState、Shadow 为同类 partial。12 个连续源码片段逐字移动，按原位置重组与原文完全一致；再核对原 class body 的全部非空行在新文件中出现次数完全一致。没有改 tick 方法体、reconcile 数据、字段初始化或 RPC 相对顺序。影子字段与方法保留原完整条件宏；调用处宏留在核心/事件方法原位置。

R1：主 Editor 编译无新错误。R3/R7/R9 待 P5 阶段集中运行；核心 687 行，Events 751、Rpc 171、DebugState 246、Shadow 504（含各文件重复 using/namespace 外壳）。

## P5-2 提前返回收尾

I6 预检推翻“三个分支完全相同”的假设：writer relinquishment 有 ClearPendingForces 或 SimulateZeroVelocityPredictionStep 分支，之后还更新 handoff 并记录 writer；保持原样。仅 movement-blocked 和 rooted 两处完全相同的 ClearPendingForces→SetPredictionVelocitiesSafely→Simulate→FinalizeImpulseDebug→UpdateReplicateDebug 合并，status 参数仍各自为 blocked/rooted。调用位置与 return 保持，R7/R9 随阶段验证。

## P5-3 秒转 tick

三个计算表达式逐字相同，抽出 BuddahTickMath.DurationToTicks；保留外围三种 guard/返回语义，TimeManager.TickDelta 原位读取，CeilToInt、0.0001f 下限、uint cast 与 deadline 加法不变。两个拖尾重置循环存在 Unity `!= null` 与 CLR `?.` 差异，motor 还更新 debug 旗标，按 I6 不合并。R7 随阶段验证。
