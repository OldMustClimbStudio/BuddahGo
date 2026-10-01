# 单机模式进度

最新 VFX 局部恢复见 [VFX 恢复记录](../vfx-asset-recovery.md)，此前有界 handoff 验证见 [Solo 呈现时间契约](solo-presentation-timeline.md)；此前选择页与 Development 证据见 [选择页与新采样复核](validation-2026-10-01.md)。旧检查点按各自源码保留。

状态：`todo` / `doing` / `paused` / `blocked` / `done` / `done-unverified`。流程见 [HANDOFF.md](HANDOFF.md)。

## 当前状态与验收边界（2026-10-01 恢复后）

S1 为 `doing`，尚非 user-ready；S1.5 未开始。最新集成检查点为 `aa7fb05`，已在 feature Editor 验证 VFX 定向重导入及局部可见效果；没有新 Player 构建或整套回归结果。此前 `cb4a9ad` 统一 Solo 开场/驾驶呈现时间，65 项 Unity 回归通过，两组首局/Rematch 实测及受控历史重置回归通过。完整自然生命周期、V3 精度、V12 来源资格/预算及六技能完整验收仍待独立完成；原粒子紫色及空 VFX 导入已有局部恢复证据，见 [VFX 恢复记录](../vfx-asset-recovery.md)。AI 计划保持 `17eb1c9` 的用户开始条件。

**AI 启动前置（2026-10-01 用户最新决定）**：先完成 S1 单人 Practice，再由用户逐项试玩技能，最后等待用户明确发出开始 AI 的信号。三个条件缺一不可；S1 完成、自动测试通过、文档批准或经过一段时间都不等于开始信号。包括 S1.5 在内的任何 AI 实现、接管调试、采集/可视化工具实现与 AI 运行测试均不得提前开始；信号前只做 AI 文档规划。此前“完成后直接推进 S1.5”的指令已被覆盖。

用户最新确定的 AI 实施与测试顺序为：**A1 单个 AI 无技能完整一圈 → A2 单个 AI 无技能三圈比赛及平均圈速/圈间稳定性 → A3 五个独立 AI 带技能比赛，玩家入场后不操作 → A4 通知用户手动试玩完整流程**。前一步通过才推进；这同时约束实现和测试，覆盖旧方案中不同的推进顺序。技能可使用独立的简单概率决策，不要求复杂行为树；仍须遵守既有技能合法性与玩法。

用户技能试玩交接与后续 AI 方案见 [ai-testing.md](ai-testing.md)。当前没有记录证明 Practice 已完成、用户已完成逐项技能试玩或已发出 AI 开始信号，三项均不可自动勾选。

主任务负责生产代码与实际测试；独立 benchmark 任务负责工具和 `benchmark.md`；文档任务只核对与记录事实，提交由主任务集成进 #59。

| 范围 | 可确证状态 | 仍缺的验收证据 |
|---|---|---|
| S1 实现 | Yak 离线启动、自动选择、Practice 计时/结算、退出与失败恢复已有代码和测试用例 | 架构差异见下表；已实现不等于整个 S1 通过 |
| V1 / V3 | 历史可见 Editor 三圈自然完赛 734.000 s；合成场景测试覆盖结果与一次 Rematch；三阶段 Esc 退出已有历史记录 | 完整结果页停留/实际 Return 按钮和后续流程仍未闭合；Player 启动冒烟不等于完整比赛 |
| V7 / 本地 handoff | `cb4a9ad` 两组非 Development 首局/真实 Rematch 均各 5380 帧、0 错误；边界最低有符号速度约 57.97 m/s，无停留或倒退；最终渲染/相机姿态一致，五次控制/传送历史重置通过 | 只覆盖有界 handoff 与受控控制事件；未替代自然完整比赛、自然复活或六技能试玩 |
| V8 | `27685f9` 真实 selection/intro/driving 三阶段 Esc、清理及重开回执 VALID；client/server 停止，Buddah/Reporter 为 0，Clock/Timing 清空，捕获错误为 0 | 结果页连续 3 次 Rematch、Return 后重开及完整生命周期仍未闭合；不能把 Esc 重开等同结果按钮验收 |
| V10 / R1 / R3 / R10 | 最新相关原生回归 65/65；统一时间线非 Development 观察包 0 errors/18 warnings，控制回归包 0 errors/20 warnings | 不是重跑全项目测试；私有观察包不等同普通发行包验收。被构建打断的 MCP 任务及初版首步速度失败均保留，不计通过 |
| V11 | 历史 strict14 在首场主样本完成前中断；本轮最终 strict14 尚未开始，**未完成、未通过** | 最终源码的完整生命周期、对象计数和 Player 内存趋势；短帧窗口或历史 heap 局部观测不能证明稳定 |
| V12 | `9cca08e` 的独立 Development 三窗口各 3600 帧、共 10800 帧，原生 GC 零缺失、0 运行错误，窗口 `VALID`；measured actual 与 Home 配置分开，不外推为新 handoff 源码性能 | **完整 V12/基线未通过**：预算全 null，clean-source 与最终源码 strict14/Esc 等证据未齐；Development frame/GC 成组使用，不当 Release 结果，不拼接旧被拒采样 |
| VFX / 用户技能试玩准备度 | `aa7fb05`：19 项 Piloto 材质恢复原 shader，四个 VFX Graph 重新生成 11 个受支持 shader；Flecks、发射粒子、掌形和释放/反噬爆发有局部可见证据。UI 与 handoff 保留各自有界结果 | 正常掌形完整生命周期、神足通粒子/拖尾/消失、六技能真实组合输入及全面效果未验收；Animator-controller 错误待查。完整 Practice 未完成，**尚非 user-ready** |
| 来源资格 / 文件恢复 | 三文件曾按用户授权备份恢复成功；随后 Unity 自动删除两项孤立 meta 并重写设置，证据及 dirty 状态保留 | 不循环 restore、不清无关 dirty；clean-source 未通过；主根 dev 未改动 |
| V2 / Steam 双客户端 / Solo–Online 交替 | **N/A，不执行，不标通过** | 无单机验收前置 |

文档 worker 仅只读核对证据；65 项回归与 Player 采样来自此前 handoff 模块，不是 VFX 修复后的新结果。VFX 当前直接原因是本地导入产物失效（`Hidden/GraphErrorShader2` / 缺少生成 shader），首次导入失败的历史原因未确证。仅定向重导入原资产，未替换原材质、修改 graph 或安装新包；其他 checkout 的 Library 不会仅因拉取提交而自动修复。技能调用与独立 Play fixture 不等于键盘组合输入或完整技能生命周期通过；详见 [VFX 恢复记录](../vfx-asset-recovery.md)。UI/Development 历史证据继续保留。

选择页 `26dd6df` 的替代 URP Lit/摘要修复已实际复核；随后发现 720p 固定像素 Canvas 导致标题/Confirm 越界，`affbd6f` 只调整三个 CanvasScaler 字段。保存后重进 720p/1080p 检查通过；皮肤页使用同样字段的运行时预览，比赛标题隐藏与退出清理已核对。原缺失美术未还原，不将动作回调测试写成鼠标命中或技能效果通过。

以下局部记录是历史证据；其中“尚未采集/下一 session”等描述只反映当时状态，最新结论以上表与新验证报告为准。

### 历史：S1 handoff 局部复核 — 2026-10-01（游戏修复 ec2bac2）

- 实际 Unity EditMode 相关回归 **40 passed / 0 failed / 0 skipped**：新增 IntroHandoffContinuity 12、HandoffClock 20、RaceStartHandshake 5、SoloSessionFlow 2、SoloTransport 1。MCP 的首个异步测试状态滞留不计作结果；以独立 TestRunner 回调落盘结果为准。
- 新 Windows x64 非 Development 私有观察构建基于 `6e9f851`（含 `ec2bac2`），**0 errors / 15 compiler warnings**。首次与真实 Rematch 按钮后的第二次开场分别记录 2689/2690 帧，从选择/开场前即固定 targetFPS 60、vSync 0；不是解锁后才限帧。捕获错误为 0，最终 Home 的 client/server 均停止且 Clock/Timing 清空，Player 正常退出。
- 两次 GO 前后 [-0.5, +1] 秒内未观察到视觉位置沿当前车头方向倒退；GO 帧视觉与终点快照误差分别为 0 / 0.000000477 m。该邻域最大 frame delta 为 16.980 / 16.757 ms，最大视觉单帧位移 1.1951 / 1.1956 m，最大镜头位移 1.9442 / 1.9080 m；镜头旋转未变，FOV 最大单帧变化 0.7635 / 0.7539 度。这些是观察量，不是自定性能或手感阈值。
- 每轮仍有两次 GO 后 body 移动而视觉位置单帧停留：首次分别为 frame 2442（body 仅竖向约 0.167 m）、2445（body 位移约 0.985 m）；重开为 5505/5508。因此只确认本次未见倒退，不宣告整体流畅性通过。两次采样均未捕获 pending 帧：GO 在同帧已被 motor 消费，新增 pending 姿态保持分支仍只有测试证据。每个新 racer 的 handoff eventId 为 1，不将跨对象相同 ID 推断为重复执行。
- movement unlock 在 GO 后约 17.8 / 15.9 ms，转向抑制在约 145.6 / 149.1 ms 后解除；后续普通 Input System A/D 均实际产生正负转向。为到达 Rematch，第一轮使用一次明确标记的受控终点登记，**不是自然完赛、V3 精度或 V11 长测**。未运行 AI 或任何联机测试。
- `06a440f` 集成独立 benchmark 修复：测量 actual 与退出后设置分开、检查新鲜 GC 样本、缺失保持 null，新增单独 Development 构建协议。集成后 51 Python 与 8 隔离 C# 测试通过；实际新 Development 采样未执行，下一短 session 重建后完成三个 30 秒预热/60 秒测量窗口，不与旧 Release 帧数据拼接。预算仍待定。
- 选择页只读定位：`PropertySelection.unity` 的 `地图/绘马墙` 与 `地图/桌子` MeshRenderer 材质引用为 null；标题对象为 `Canvas/SharedUIRoot/Header/PageTitleText` 与 `StageTitleText`。交给独立 UI 任务处理，本次未改场景。原始帧、截图、构建与来源哈希均只在私有证据中保存。
- 受保护设置及两项删除 meta 未恢复，dirty 来源资格仍未闭合；主根用户 WIP 不变。S1 仍 doing，strict14/V3/V12 与用户试玩准备度未通过。

### 历史：S1 真实验收 — 2026-10-01（游戏源码 27685f9）

- 已核验 instrumented non-Development Player 的全部构建文件哈希与交接清单一致，三个私有辅助源码与已提交模板一致；普通发行构建、此前三项场景测试仍按各自历史记录引用。本轮 Python 分析器 42/42 通过。
- **三阶段 Esc 新证据通过**：selection、intro-before-GO、driving-after-GO 均真实打开确认框、屏蔽输入、通过 ConfirmQuit 回 Home 并重开。每次 client/server 停止，Buddah/Reporter 为 0，Clock/Timing 清空，捕获错误为 0；独立回执分析返回 VALID。这不替代结果页 Rematch/Return 或 strict14。
- **V12 未通过**：三个真实物理驾驶窗口各预热至少 30 秒、测量约 60 秒，各 3600 连续帧，窗口配置均为 1280×720、quality 2、vSync 0、targetFPS 60，报告丢帧为 0，捕获错误为 0。原始 frame mean/p95/p99（ms）：16.6692/16.8968/17.0080、16.6679/16.8968/17.0021、16.6687/16.9109/17.0262；仅为诊断分布，不是已合格基线。
- **旧 Release 采样缺陷（后续独立 Development 补测见最新验证报告，不回填旧数据）**：三个窗口共 10800 帧的 GC 分配列全部缺失，Release 的 `GC Allocated In Frame` 不可用/空；Main Thread 样本可用。`Finish` 在回 Home 后记录全局 actual，此时 host 恢复 targetFPS 500，导致分析器报 `Window settings changed`。须区分测量配置与退出后配置，并确定独立 GC 采样方案；禁止把 heap 差值或缺失值填成分配量。预算仍未确定，不编造通过。
- 构建来源仍含保留的设置/孤立 meta 差异；没有恢复受保护 WIP，也没有把 dirty 来源改写成 clean。该状态不能取得工具要求的干净源码资格。
- **handoff 流畅度仍未验证**：本次窗口开始前的 intro/GO/unlock 按 host targetFPS 500 运行；60 FPS 在 movement-unlocked 之后才施加，不能据此宣称正常帧率下开场到驾驶平顺。此后 `ec2bac2` 已集成修复：等待 motor 消费 launch handoff 时保持 GO 姿态，并修正路径距离的重复转换；当时没有修复后运行结果；其后独立构建与局部运行结果见上节。唯一 Unity 验收任务负责新构建及真实姿态/镜头/输入连续性验证；最终 strict14 与新 V3 触发精度长测仍未完成。
- 当前 Editor 实际技能选择提供六个 unlocked ID：acceleration、slowtrap、blackcurtain、giant、push_projectile_hands、reverseturn。此项证明选择入口可用，不代表每项技能效果已验收。反转转向正常效果排除施法者，0 AI Practice 无法验证其对手效果；不新增 AI 靶子。选择场景截图同时出现洋红材质和文字叠放，列为待修复复核的独立资源/UI 项，未推定其来源。当前不能标为 user-ready；六个选项可用不等于完整界面或六项技能表现通过。
- 原始日志、配置、CSV、回执、来源清单和实际界面截图均私有保存；截图已在工作对话展示，不上传公共 PR。S1 保持 doing；AI 仍等待 Practice 完成、用户逐项试玩及明确开始信号。

### 当前 Practice 质量边界

Practice 的目标是让本地单机完整承接既有联机流程与表现，特别是加载/场景 handoff、开场镜头/动画/倒计时到驾驶的姿态、镜头和输入控制权交接，并真实验证流畅。此要求是本地适配与验收，不是运行联机测试；也不授权全局移除 prediction。用户此前提出的本地 handoff 问题已由 `cb4a9ad` 在已测首局/Rematch 与控制事件范围内修复验证，见 [Solo 呈现时间契约](solo-presentation-timeline.md)；此结论不替代完整 Practice、自然生命周期或技能/VFX 验收。65 项 Unity 回归、两组各 5380 帧的首局/真实 Rematch 及五次历史重置已有独立证据，不再将旧单帧停留列为当前阻塞。原粒子 VFX 已在 feature Editor 重导入恢复并完成局部可见验证；自然复活/全部技能、strict14/V3 与完整 V12 仍待各自验证；运行由当前唯一 Unity 任务负责。

### 设计与实现待核对

| 差异 | 证据与影响 | 主实现任务的处理点 |
|---|---|---|
| 菜单依赖边界已修复 | `ad55ff0` 将具体 UI 调用改为 `SoloUIBootstrap` 注册的 `Action<SoloMatchSettings>` 回调 | 此项不再列为待修复；不据此宣告全部 S1 验收通过 |
| 会话创建职责存在设计内部冲突 | design.md §3.2 明定 `GameNetworkManager` 持有 `SessionLauncher`，代码也如此；§3.1 却要求现有代码仅依赖契约 | 明确组合入口是否有显式例外，或调整构造位置；在决定前不宣告严格依赖检查通过 |
| Solo UI 目录偏离设计 | 设计指定 `Assets/Scripts/UI/Solo/`，实际为 `Assets/Scripts/Match/UI/`，命名空间仍为 `SteamMultiplayer.UI` | 确认保留目录还是迁移及其序列化影响；不为对齐文档擅自移动代码 |

已修正文档中的过时前置：Steam 容错、单人自动开赛和 S1 Match Clock 已实现；S6 不再要求重跑 V2。S1.5 的 V11 引用与全阶段稳定性要求对齐，单车性能证据不冒充 5 AI 指标。0 AI 锁定是 S1 已允许的限制，不是 AI 完整功能已交付。

## 阶段

| 阶段 | 目标 | 状态 | PR | 验证结果 | 备注 |
|---|---|---|---|---|---|
| S0 | 同步重构结果 | done | — | 文档事实已按合并后的 dev 复核 | 2026-09-30 合并 dev（含 #47–#58） |
| S1 | 离线单人 Practice 跑通 | doing | [#59 (draft)](https://github.com/OldMustClimbStudio/BuddahGo/pull/59) | 最新 Unity 65/65、本地 handoff 在已测范围内通过；选择页复核与旧源码 Development 窗口 VALID；V3/V8/V11/V12 未全部闭合，V2 不适用 | 同步 aa7fb05，VFX 局部恢复；65 项回归与 handoff 结果属 cb4a9ad；尚非 user-ready；AI 未开始 |
| S1.5 | 规划器可行性验证 | todo | | 未开始；等待用户 AI 开始信号 | Practice 完成、用户逐项技能试玩、明确开始信号三项齐备才启动 |
| S2 | Racer 身份 | todo | | | 仅单机验收 |
| S3a | AI 完整跑完一局 | todo | | | |
| S3b | AI 施法入口与表现 | todo | | | |
| S4 | AI 驾驶与调参场景 | todo | | | |
| S5 | AI 施法规则 | todo | | | |
| S6 | 体验收尾（占位美术） | todo | | | |
| S7 | 暂停（后续） | todo | | | |

## AI 实施与测试里程碑（尚未开始）

| 步骤 | 完成内容 | 状态 | 推进条件 |
|---|---|---|---|
| A1 | 单个 AI 无技能完整自然跑一圈，记录圈速与轨迹 | 未开始 | 三项启动前置满足后才能实现；本步通过再 A2 |
| A2 | 单个 AI 无技能三圈比赛，检查平均圈速与圈间稳定性 | 未开始 | A1 通过；本步通过再 A3，数值稳定性门槛待定 |
| A3 | 五个独立 AI 带技能比赛，玩家入场后不操作；记录各自完赛率/时间/轨迹 | 未开始 | A2 通过；正常完成困难则调整复测，成功率门槛待定 |
| A4 | 通知用户手动试玩完整流程并记录反馈 | 未开始 | A3 通过；不得把通知或自动化通过等同用户已试玩 |

## AI 调参记录（S1.5 / S4 / S5，尚未开始）

只在取得用户 AI 开始信号后填写实际结果；完整字段与逐圈轨迹方案见 [ai-testing.md](ai-testing.md)。每圈圈速及失败/中断记录保存在对应运行证据中，不只填平均值。

| 运行标识/阶段 | 配置与 seed | 计划/完成圈数、完成率 | 逐圈圈速与轨迹证据 | 碰撞/复位/卡住 | 技能条件及效果/恢复 | 规划耗时 | 结论与未测项 |
|---|---|---|---|---|---|---|---|

## 决策

| 日期 | 事项 | 结论 | 决定人 |
|---|---|---|---|
| 2026-09-30 | 设计会话 Q1–Q34 | 见 design.md §2 | 团队 |
| 2026-09-30 | 补充决策 Q35–Q40：提前结算、Lap Time、Stuck Recovery、RacerId 规则、AI 配装、S1.5 与 S3 拆分 | 按推荐方案执行，见 design.md §2 | 团队 |
| 2026-09-30 | 历史原则：联机只做冒烟检查 | 已被 2026-10-01 用户的“单机不跑联机测试”决定覆盖 | 团队 |
| 2026-10-01 | 单机不需要任何联机测试；单机应有自己的 benchmark | V2/Steam 双端/联机交替移出单机 gate；新增独立 V12 协议 | 用户 |
| 2026-10-01 | 历史关机暂停检查点 | 当时 S1 paused；保存部分 Player 证据，不把未完成长测记为通过；当前已恢复 | 用户 |
| 2026-10-01 | 恢复单机工作，并由独立任务维护文档与 benchmark | S1 doing；只更新可核实证据，沿用 #59；不提前开始 S1.5 | 用户 |
| 2026-10-01 | AI 必须等待 Practice 完成、用户逐项技能试玩及明确开始信号 | 覆盖此前直接推进 S1.5 的指令；信号前 AI 只落文档 | 用户 |
| 2026-10-01 | Practice 完整适配既有流程，重点保证 handoff 流畅 | 开场镜头/动画/倒计时→驾驶及加载/结算/重开/返回须本地真实验证；不运行联机测试、不授权全局移除 prediction | 用户 |
| 2026-10-01 | AI 实施与测试按 A1–A4 逐步通过 | 单 AI 无技能一圈 → 单 AI 无技能三圈稳定性 → 五独立 AI 带技能比赛（玩家不操作，可用简单概率施法）→ 通知用户手动试玩；覆盖旧的不同推进顺序 | 用户 |
| 2026-09-30 | 默认决策：AI 只需配装，皮肤用占位颜色；观战规则沿用并改为按 RacerId；头顶名字和小地图对手由 Match Rules 控制，单机开启 | 可以调整，见 design.md §2 | 设计默认 |
| 2026-10-01 | AI 名字列表 | 漂移禅师、轮回圈王、面壁达摩、金刚不刹、回头是岸（见 design.md §5.8） | 团队 |

## 新发现

| 日期 | 位置 | 描述 | 关联阶段 |
|---|---|---|---|

## 历史证据：S1 implementation checkpoint — 2026-10-01

- Implemented: Match contracts/rules, Yak-only local host, bounded startup and deferred stop, Practice setup (0 AI), automatic room start, skill/skin selection without map voting or timers, tick-based timing/Lap Times, immediate Practice results, Rematch/Return, and an Escape confirmation dialog which blocks local steering.
- Serialized scene changes: Multipass (Steam + Yak) in MainMenu; preplaced MatchClockSync/RaceTimingSync on the RaceMap network object; Solo UI roots in all three scenes. Chinese UI uses a bundled OFL-licensed subset and a static atlas baked from its 499 supported codepoints. The final quit/results screenshots were inspected after fixing stale glyph mapping. Diagnostic telemetry is log-only and the serialized debug-finish button is disabled. Online ordinary standings remain visible; Practice hides ranking through the existing EndOnHumanFinish rule. The left-side legacy names are static placeholders, not a replacement standings view.
- Verified in Unity 2022.3.55f1c1 with the Steam API unavailable (no Steam.exe process observed during checks; no user Steam process was stopped): Yak starts both managers without starting Steam transport, then stops both. The real scene integration test passes setup → selection → Escape/confirm → Home → reopen → selection → intro/GO → Escape/cancel → controlled finish → results → Rematch → selection → stop. Input is synthesized through the real Input System Dynamic update; the test temporarily redirects unfocused batch input and restores its settings.
- The independent startup-recovery test also passed: a controlled failure after loading selection defers shutdown, automatically restores the visible Solo setup panel with the failed difficulty and error, an enabled Start button, and no surviving player/host. Normal Return/Escape still return Home. This injects the rollback boundary; it does not simulate a real authentication timeout.
- Regression assertions passed in that integration run: a new timing round clears synchronized old Lap Times; an unowned racer cannot consume the first finish order; final leaderboard registration precedes immediate Practice ending. Controlled timing/finish data is synthetic and does **not** certify physical racing, lap detection, or V11 endurance.
- Final V10 complete-suite result: **152 passed, 0 failed, 0 skipped** (visible-Editor EditMode suite after startup-recovery and Online HUD review fixes, including EnterPlayMode scene tests and three standings regressions; the preceding baseline batch run passed 149/149). R1 editor compilation and serialized network binding checks passed; R3 Windows x64 non-Development build **succeeded, 0 errors, 18 BuildReport warnings**. The latest BuildReport includes 14 compiler warnings in existing code (obsolete PredictionSmoother API, unused fields, and unreachable code) plus the same four shader/third-party resource warnings; the earlier cached build reported only those four asset warnings. The new timing component preserves FishNet's editor Reset callback.
- The final standalone player also started and remained running after reporting Steam initialization unavailable; this is a startup smoke only, not completed-game evidence.
- Current scope: V2 and all online testing are **not applicable** to this Solo task by the user decision on 2026-10-01. Remaining Solo gates include full V3 results/button follow-through, V8 consecutive Rematches/reopen, V11 complete endurance/object/memory evidence and V12 independent Solo benchmark. Historical test results remain evidence, not an online prerequisite.
- Earlier batch input failures were isolated to the unfocused Editor input buffer and editor-iteration-based waits; the corrected focused integration run passed. Raw logs/screenshots and temporary verification helpers are private and excluded from commits/PRs.

## 历史证据：S1 acceptance continuation — 2026-10-01

- Review fixes: `73f29b3` restores Online standings without diagnostic fields and carries a one-time failed-Solo request across deferred shutdown/menu initialization. The scene test verifies the setup is automatically visible with its original difficulty and error.
- Independently exercised Escape via virtual keyboard events on the normal Dynamic input update in selection, intro before GO, and racing after GO. All three confirmation exits reached visible Home with both network managers stopped, zero PlayerProgressReporter objects, timing services cleared, and the input block released. No Error/Exception/Assert was captured during this operator run.
- Superseded requirement: the previously recorded Steam two-endpoint prerequisite is removed from this Solo task. No Steam accounts or online session are required to resume or complete Solo stages; V2 is N/A, not passed.
- Physical offline race: normal virtual A/D input drove the unmodified body through the real start trigger, sequential checkpoints and three complete laps. Natural finish total **734.000 s**, Lap Times **242.567 / 243.067 / 248.367 s**. The result phase became interactive approximately 3.75 s after finish, with no 15-second Practice end delay. All 734 one-second racing samples contained 10 NetworkObjects; no Error/Exception/Assert was captured. The operator then verified complete shutdown/Home. This was a visible-Editor run with Steam unavailable, not a standalone endurance result.
- Physical timing observation: the second lap transition interval observed by EditorApplication.update was 243.217 s versus the synchronized 243.067 s (0.150 s difference); the third interval was 248.283 s versus 248.367 s (0.083 s difference). Timing is recorded through the existing 100 ms progress-report RPC, and Editor observation adds frame scheduling delay; these measurements must not be described as single-tick finish-line precision.
- The first physical operator stopped at ResultInteractive before the results view activated its buttons. Consequently its intended 35-second results hold and actual Return button check did not execute and remain open; no product failure was inferred from this operator sequencing issue. Editor GC samples include Editor/Console overhead and do not close V11's Player memory-growth gate.
- Warning provenance was checked against dev `7389ec8`: all six source files producing the 14 compiler warnings have identical Git blob IDs in dev and the reviewed feature head. The unreachable branch is the existing constant-disabled ranking log. FishNet APIs and those warning-producing fields were not changed by S1. The remaining four asset warnings match the earlier report; no new product-code warning was identified. The separate instrumented acceptance build is not the ordinary R3 deliverable.

## 历史暂停检查点 — 2026-10-01 user request

以下记录描述关机前保存状态；当前已恢复工作，以本文顶部状态为准。

- At this saved checkpoint, S1 was **paused**, not done. Published implementation/review checkpoints are `73f29b3` and `88f763a`; the complete suite previously passed 152/152, including real Unity scene tests. The ordinary Windows x64 non-Development release succeeded with 0 errors and 18 warnings, whose existing-source provenance is recorded above.
- Completed physical evidence: one visible-Editor three-lap Practice race, total 734.000 s, natural checkpoints/finish and immediate Practice result transition; three-phase Escape/confirm/Home cleanup; automatic failed-start setup restoration with original difficulty/error.
- The independent instrumented Player strict14 sequence was stopped at the user's pause request during its first main race (lap 3, approximately 73.57% complete at normal closure). It had not completed a main sample, so neither strict14 nor the ten-match V11 gate passed. Partial live measurements showed one Buddah, one reporter, ten NetworkObjects and about 14 MB managed heap, with no captured game Error/Exception before stopping. These are partial observations, not a memory-stability conclusion.
- Private helpers, source hashes, process manifest, logs, partial JSONL and normal/instrumented builds are saved locally. They are excluded from this PR. The temporary Unity helper sources were removed from Assets.
- The saved strict14 evidence lacks all-frame performance percentiles, allocation/frame and allocation/second, a fixed-seed run protocol and captured quality/FPS configuration. It is functional/endurance evidence only. The independent benchmark session stopped cleanly at baseline `88f763a` without creating source, protocol, or a new commit. At that checkpoint, `Tools/SinglePlayerBenchmark/` and `benchmark.md` were **not implemented or executed**; do not borrow online Editor R8 numbers or repeat a long run solely to fill metadata.
- Remaining acceptance order after resumption: read this checkpoint and implement the bounded independent benchmark package/protocol; confirm all local/remote checkpoints; reopen the feature project; finish actual results hold/Return/Rematch and required multi-match integrity; close the bounded S1 0-AI benchmark protocol/measurement; update applicable Solo gates. Do not begin S1.5 while S1's applicable Solo gates remain incomplete. The later user decision adds two further requirements: the user completes the skill playtest and explicitly signals AI implementation may begin; S1 completion alone is insufficient. No online tests or Steam coordination are part of this resume plan.
