# S1 选择页与新采样复核（2026-10-01）

S1 仍为 **doing，尚非 user-ready**。本次运行的已提交游戏源码为 `9cca08e`；仅完成选择页、相关 handoff 回归及独立 Development 性能窗口。strict14、V3 精度留给下一短 session；AI 和联机测试均未启动。

## 选择页

已在可见 Unity 验证 `26dd6df` 的替代 URP Lit 材质与 Practice 摘要隐藏：绘马墙/桌子不再洋红，六技能卡文字无原摘要遮挡。原美术材质缺失，未宣称还原。六个技能均通过正常 UI 控制器入口逐项选择、移除，并完成三个槽位替换及实际 Confirm/皮肤 Select 按钮回调；这不是鼠标命中测试或六项技能效果验收。

发现 1280×720 下固定像素 Canvas 使标题/Confirm 越界；`affbd6f` 仅将三个 CanvasScaler 字段改为基于 1920×1080 的自适应缩放。保存后重新进入选择页，在 720p/1080p 实际截图及按钮边界检查均通过，重进草稿为空。皮肤页在同样三字段的运行时预览下完成确认；比赛中两项选择标题确实隐藏。普通 Dynamic InputSystem Escape 打开退出框、阻断输入，真实 ConfirmQuit 后 client/server 停止、Clock/Timing 清空。

## Handoff 残留

用户提供的 `b0b1f7e` 集成为 `525600e`：消费 GO 当 tick 使用新权威 room bypass 保留继承速度；本地权威 owner 且 launch/smoother 有效时跳过重复 post-intro 对齐。控制权、定身与转向限制保留。

首次 Unity 回归 48/53，新增五项在 owner 前置断言失败；`9cca08e` 为测试夹具赋有效 ClientId，复跑 **53 passed / 0 failed / 0 skipped**（13 新增+原40项）。首轮失败证据保留。历史分析器51/51、隔离采样8/8本次未重跑。

新非 Development 私有观察包 0 errors/20 warnings；首次/实际 Rematch 两轮 2690+2696 帧、0运行错误，target60/vSync0，最终 Home 清理成功。第一轮受控终点仅用于到达 Rematch，**0自然完赛，不算 strict14/V3/V11**。

两轮 GO 当帧速度与快照均约60m/s，重复 post-intro-visual-lock 消失；GO [-0.5,+1]秒未见水平倒退。实际 tick间隔1/60秒、附近每采样帧最多一次 OnPostTick，记录了平滑队列长度/头tick。

**整体流畅性仍未通过**：两轮 GO 后约14.1/15.8ms各有一帧视觉水平停留，刚体水平前进0.966128m、竖向约0.166650m；不能当成仅竖向修正。当前为最终 LateUpdate 观察，未捕获所有物理/队列写入前后状态，不据此宣告残留原因或完整手感通过。

## Development 性能窗口

同游戏源码另建独立 Development 包，0 errors/15 warnings；请求仅 Development，不启用 Deep Profiling、Profiler 自动连接或 Script Debugging。真实 build label/后端、完整报告选项、源码/辅助源/全部产物哈希均私有留档。三个采样辅助源与已提交模板哈希一致。

运行完成 `True`，运行错误 0；逐帧窗口验证 `VALID`，GC 检查 10800 行、缺失 0 行。原生计数器结果：`available for every measured frame`。

| 窗口 | 帧数 | 秒数 | 实际FPS | mean/p95/p99 ms | GC bytes/frame | GC bytes/s |
|---|---:|---:|---:|---|---:|---:|
| 1 | 3600 | 60.008331 | 59.9917 | 16.6690/16.9315/17.0942 | 6757.15 | 405372.61 |
| 2 | 3600 | 60.003903 | 59.9961 | 16.6678/16.9558/17.1171 | 6777.02 | 406594.62 |
| 3 | 3600 | 60.006807 | 59.9932 | 16.6686/16.9664/17.1106 | 6803.16 | 408143.54 |

每局30秒驾驶预热、首次达到60秒的连续测量窗口；三个独立物理比赛，0 AI，无 teleport/模拟完赛。窗口 requested/actual/start/end 均1280×720、quality2、vSync0、target60，窗口内设置变化与丢帧计数均0。全局 measured actual固定于首窗口，退出后的 Home target500另存 post_cleanup_actual，不能覆盖窗口配置。

以上 **Development 的 frame与GC数据必须一起使用**，不当作普通 Release 性能，不与旧 Release 数据拼接。旧 Release 10800缺失GC行保留且未获资格。

## 仍未闭合

- 预算全部 null，未虚构门槛；独立窗口有效不等于完整 benchmark/V12或基线通过。
- 三处受保护差异完全保留：PackageManagerSettings 与两项已删除 FishNet meta；主根 dev用户设置未动。clean-source资格仍未通过，未重试此前被拒绝的恢复。
- 最终源码的strict14、结果连续流程、V3精度和完整生命周期待后续；旧源码Esc证据不冒充最终HEAD。
- handoff单帧停留和完整单机移植体验仍需后续闭合。完成Practice、用户逐技能试玩及明确AI开始信号前，不开始任何AI工作。

原始截图、轨迹、失败/成功回调、GC CSV及哈希清单仅私有保存；本文只记录结果摘要。
