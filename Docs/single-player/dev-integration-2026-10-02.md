# dev / PR60 集成与 Solo 开场复核

2026-10-02。现有 `feat/single-player-mode` / draft PR #59，将 `origin/dev` 的 `fc7ab594b2c57af7f192c6c3d702bd6c17c33eb4` 普通合并到 `eae9f6635479a458a96c888beb0e066e9aca7ee7`，不重写历史、不合并 PR59 到 dev。本模块仅集成和受影响 Solo 验证；五 AI + 玩家、六车起点/运镜和完整结束动画属于下一模块。

## 合并语义

- Solo 保留已完成物理步姿态历史、一次 fixedDelta 呈现延迟、固定步 GO 对齐和即时消费；不叠加 PR60 的一 tick 网络图形延迟，不在 Solo GO 采样中引入网络时钟外推。Solo 的第二条样条图形采样明确禁用。
- 原网络路径保留 PR60 的有限 GO 外推、延后一 tick 的开场渲染及移除旧 post-intro lock；motor 的网络 room bypass 使用 PR60 逻辑，Solo 保留消费当 tick 的门控。没有修改玩家输入、技能派发、权威 Match Clock 或物理参数。
- `PlayerCamera`、`BuddahMovement`、`IntroTimeUtility` 与 dev 内容一致（忽略换行）。相机保留样条驱动速度读取和开场退出缓动。保留 feature 的归一化距离修正，避免样条弧长二次转换。
- visual bridge 同时保留 Solo 历史控制和上游关闭默认的诊断入口；Docs 索引保留两方入口，未整文件选边覆盖。

## 新证据与边界

- Unity 2022.3.55f1c1 针对性 EditMode **79/79 通过，0 failed / 0 skipped**，覆盖 handoff/timing、Solo history、相机退出、握手、Yak 本地 transport、AI 回归。初轮 77/79 的两项失败来自新测试的空样条 fixture，改为实际终点锚点后重跑全组通过；不隐去失败回执。
- 独立 Windows x64 **非 Development** 观察包构建成功，**0 errors / 27 warnings**。警告包含既有过时 FishNet API、未用字段、私有观察器的 3 项过时 API、nographics 光照与 shader/资源警告；不是零警告构建。一次私有构建回执脚本 int/uint 编译错误已修正，失败日志保留。
- 新源码首局和实际 Rematch 共 **5380 帧**，0 捕获运行错误；正常选择、GO、D/A 输入、受控结算、真实 Rematch 按钮和最终 Home 会话清理完成。两轮均单个 Practice 车身，没有运行多 AI 或联机/Steam 验收。初次隐藏窗口运行虽完成两轮且无错误，但无最终渲染回调、截图全黑；保留在 `evidence/`，不计视觉通过，随后同包显示独立窗口重跑采集。
- 物理 GO 端点前 150 ms 至后 300 ms 渲染窗口的最低有符号水平速度分别 **57.96721 / 57.96712 m/s**，无停留或倒退。呈现延迟始终 **16.66667 ms**；FishNet 图形队列为 0 且 suspended；没有第二个样条采样。LateUpdate 到最终 render 的 actor/follow 位移差均为 0。
- D/A 的正常 motor 转向值分别 +2/-2；GO 到 +1.2 s 相机最大逐帧 FOV 变化分别 0.14882 / 0.14905 度。原始 GPU 截图和逐帧相机位姿已保存并复核，未通过换时间源或修改发车速度消除停顿。
- 完赛是一次**受控注册**，仅用于进入实际 Rematch；不是自然计圈、终场动画完整验收、长期稳定性或性能 benchmark。合并前 A2 / 转圈修复的三圈和模型窗口证据仅保留历史来源，不能算合并后通过。

私有证据：`task-20/tests-final2.xml`、`tests.xml`（初轮）、`logs/tests-final.log`（私有 helper 编译失败）、`build/receipt.json`、`build/warnings.txt`、`source-build-manifest.json`、`path-audit.json`、`evidence-visible/summary.json`、`evidence-visible/analysis.json`、两轮 `handoff-*-frames.jsonl` / `handoff-*-phases.jsonl` / 原始截图。观察器仅加入私有 build define，不提交仓库；本任务测试包不覆盖用户试玩包。

## 仍未闭合及下一模块

转圈修复后的历史三圈 126.783 / 123.533 / 127.817 秒，平均 126.044 秒，慢于诊断基线 118.100 秒；66 次行驶侧接触、4 个无接触标记模型窗口失败。V1–V5 调参预算结束，本次未改 AI 配置、规划器或继续调参。

`SplineProgressTracker` 存在底层进度停滞，不能当作纯 HUD：task-18 第三圈 elapsed 360.233–362.533 s 连续 24 个采样 `progress01=0.791036`，车仍移动，末点独立投影 0.811832，落后 119.296 m。AI 自己的投影正常且不读取这个 tracker；产品值会进入 `PlayerProgressReporter` 排名、`LapProgress` 自动 checkpoint 和 `RaceCompletionTracker` 逆行判断。此前自然三圈计圈/完赛正常，无已证实漏圈，但排名正确性不能关单。后续须回放投影选择、限幅和拒绝分支，不能删除校验或改 HUD 掩盖。

下一模块可继续六参赛者结构、五 AI + 玩家开场/镜头和结算动画；本模块未实现 A3。S1 完整自然生命周期、最终源码 ±100ms、strict14/V11、完整 V12、对手技能效果等仍保留未验状态。网络运行测试不在本任务范围，保留测试资产。

## 工作区保护

开工先保存主仓库 1 项 WIP、feature 原 28 项加 demo `AITuningHarness` 暂停改动共 29 项的状态、binary diff、原始文件及 SHA256。上游变更与 WIP 无文件交集，无需移动/暂存 WIP。结束再次逐字节检查；根仓库保持 dev，原场景对象和五个测试盒均未修改、未禁用、未删除。仅运行 task-20 私有进程；没有关闭用户 Player 或注入用户会话。
