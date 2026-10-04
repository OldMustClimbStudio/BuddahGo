# 五 AI 驾驶：性能归因与行为保持的成本优化

2026-10-02；基线 `a9c9a17471a7073c5bab84f9bbb32013b7356213`；同 `feat/single-player-mode` / PR59，未合并。

## 范围与方法

本模块只处理五 AI 开始驾驶后的 CPU 成本。没有调整 V5 profile、3 tick 重规划周期、48 beam、3 秒 horizon、控制块、物理步长、tick 上限、速度规则、转圈分支约束或 neutral fallback；没有扩展技能、排名或结算。

私有非 Development Windows x64 Player，1280×720，60 fps cap、vSync=0，真实 MainMenu → 五 AI 设置 → 配装 → 六车 intro → GO。每轮 GO 后约 12 秒退出，分析统一使用 GO 后 2–11 秒窗口，排除入场截图与首次缓存。仅私有 Player 使用虚拟键盘；没有用户进程控制。未进行 Rematch、人工终场、自然完赛或在线测试；未改动/删除测试盒子。

固定长度预分配缓冲累计每 AI 完整 Plan、BuildReplicateData（包含 AI）、RunInputs、tracker、observer、VJitter，以及 net tick / physics 回调。深层诊断另测每次 Project 和模型 Step；只在结束时导出逐帧 CSV，非持续日志/HUD。阶段计时存在包含关系，不能相加；帧墙钟取相邻 LateUpdate 的实测间隔，`dt` 和 Main Thread recorder 有阶段偏移，不相减推断空白耗时。

初步三组全部启用深层计时，分别是完整旧观察器+日志、轻量观察器+日志、轻量且关闭 VJitter。这个探针增加模型循环成本，数值不能和历史 task21 帧时直接比较。正式 A/B 在同一二进制中选择冻结原实现或优化实现，均关闭深层计时、重观察器和 VJitter。场景运行受实时物理/tick 积压影响，不是相同输入轨迹回放；行为等价用另行保存输入的测试判断。

## 归因

| 运行 | 深层计时 | 窗口帧数 | 中位帧时 ms | P95 ms | net ticks | physics 回调 |
|---|---|---:|---:|---:|---:|---:|
| baseline-full | 是 | 55 | 161.88 | 232.88 | 165 | 555 |
| baseline-light | 是 | 55 | 168.56 | 195.49 | 165 | 540 |
| baseline-quiet | 是 | 42 | 206.06 | 305.06 | 126 | 534 |
| compare-legacy | 否 | 60 | 147.88 | 185.43 | 180 | 542 |
| compare-optimized | 否 | 90 | 97.52 | 162.38 | 249 | 546 |
| compare-final | 否 | 101 | 93.18 | 128.39 | 291 | 539 |

完整观察器基线：五 AI Plan 合计 83.74% 墙钟，投影 36.74%、模型 30.80%、tracker 3.43%、observer 0.50%、VJitter 0.10%。每 AI 55 次规划、平均 22.36–30.29 ms/次；不能把重叠 scope 再相加。

同构建首次 A/B（compare-legacy / compare-optimized）中位数 147.88→97.52 ms，tick 180→249。随后两项重复工作去除的最终新构建补测 compare-final 为 93.18 ms / P95 128.39 ms，101 帧 / 291 ticks / 8.965 秒。相对原版窗口，中位帧时下降 37.0%，归一化 tick/s 提升 62.5%。这是先后有界窗口的实测，最终补测没有再运行旧版，也不是统计显著性或整场预算保证。

最终每 AI 97 次规划，平均 9.56–17.82 ms/次；合计仍占约 80.2% 墙钟。五 AI 与人类的 GO/动态状态及起止位置独立保存；不同实时调度下轨迹不相同。首版优化某 AI 在结束快照速度仅约 1.46 m/s，不能据短测声称驾驶流畅、无碰撞或不卡住。六组窗口 GC collection 增量均为 0。

观察器/日志并非主要瓶颈：轻量与关闭 VJitter 没有恢复帧率，窗口间 AI 状态和调度波动不可解释成日志使性能变好/变差的因果关系。五个规划都被单独计时，完整规划成本才是占比依据，未将每 3 tick 的整个时间区间归给规划。

Main Thread recorder 可读取；GC Allocated In Frame 不可用（-1）；GPU Frame Time 虽存在但全零，视作不可得。GC.CollectionCount 可读取，报告实测次数；不能据此声称零分配或 GPU 无开销。physics 列是 TimeManager 的 post-physics 回调次数，不混同网络 tick。

## 改动

- `SplineRacingLine` 缓存每段水平 delta、投影分母和标准化 tangent，只为最终胜出候选生成 point/distance。保留原 Vector3 算式、除法、遍历范围和并列候选先后顺序。构造/公开 Points 都做快照，防止外部数组写入使缓存过期。Capture 用弱 track key 共享匹配几何；每次仍重采样并精确核对，变更位置/采样后换新几何。pace 数组和规划 beam/nearSegment 独立。
- `BuddahMotionModel.PreparedMotion` 在每次 Plan 内缓存不变的质量分母、阻尼、摩擦步量、速度 cap 和三种数字键 torque；每个控制块仅解析一次键值/是否衰减。下一 Plan 重新捕获参数，不跨 modifier/接触变化复用。保留积分运算顺序和实际 Rigidbody 控制。
- `VisualJitterDiagnostic` 增加默认关闭的 `diagnosticsEnabled` 序列化开关，需要时显式开启。私有对照强制日志状态；没有常驻 HUD。

最初尝试的标量投影表达式在测试中造成微小浮点/成本差异，已撤回；失败 35/38 的记录保留，最终采用保持原算式的版本。

## 验证及边界

最终 Unity EditMode **39/39**：6 个成本等价/缓存测试、19 个既有 AI 驾驶/模型/转圈测试、14 个 tracker 测试。包括 6,875 对投影、30 次保存状态/连续规划（正反转向符号）的选择与成本精确相等、2,880 个模型 tick 的位置/速度/yaw/yawRate 相等；共享几何但独立 pace，位置/采样变化失效。无 epsilon 放宽来掩盖算式差异。实际 Player 各轮捕获 runtime errors=0。

最终普通非 Development 构建结果见 `Tools/ai/performance/release-receipt.json`。诊断用旧观察器和冻结原算法均归档到任务目录，已移出 Assets；未向产品添加性能日志/HUD。

运行时仍有剩余性能成本。用户未设本模块帧预算，不能声称整场性能验收通过。短窗口不证明全赛道/全局并列/所有平台的浮点等价，也不证明自然六人全流程已完成。AI rank=-1、技能交互和终场仍是后续短模块；历史速度/接触/模型异常仍保持原状态。

## 证据

仓库 `Tools/ai/performance/` 保存窗口 CSV、分析脚本、汇总和测试结果。完整私有构建/源清单、探针、初步失败记录与 Player 日志位于 `C:/Users/dwh88/Documents/Codex/2026-10-02/task-2`。

初步三组继承的 summary.json 将 syntheticFinishes 写成 1，是旧观察器固定字段；实际本模块终止路径在 GO+12 秒直接退出，未进入 controlled-finish。正式 A/B 已修正该元数据为 0，原始证据不覆盖改写。

全部 29 项 feature 原 WIP 与 root 1 项原 WIP 已逐字节核对；root dev 未变。仅本模块文件提交；无新 branch/worktree/PR，无 merge。运行/写入释放情况见任务目录 HANDOFF.txt。
