# P5 预测运动与推击整理

## P5-1 原样拆文件

BuddahPredictedMotor 保持原 namespace、类名、主文件 GUID、序列化声明和继承。核心 tick/reconcile 留在主文件；Events、Rpc、DebugState、Shadow 为同类 partial。12 个连续源码片段逐字移动，按原位置重组与原文完全一致；再核对原 class body 的全部非空行在新文件中出现次数完全一致。没有改 tick 方法体、reconcile 数据、字段初始化或 RPC 相对顺序。影子字段与方法保留原完整条件宏；调用处宏留在核心/事件方法原位置。

R1：主 Editor 编译无新错误。R3/R7/R9 待 P5 阶段集中运行；核心 687 行，Events 751、Rpc 171、DebugState 246、Shadow 504（含各文件重复 using/namespace 外壳）。
