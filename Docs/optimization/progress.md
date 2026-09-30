# 执行进度

状态取值：`todo` / `doing` / `blocked` / `done` / `done-unverified`。每完成一步就更新本表。流程见 [HANDOFF.md](HANDOFF.md)。

## 步骤

| 步骤 | 内容 | 状态 | 提交 | R 结果 | 备注 |
|---|---|---|---|---|---|
| P0-1 | 记录基线 → baseline.md | done-unverified | 874a55b | 基线 C# 编译 0 error / 11 已有 warning；R4–R9 未执行 | 仅本机，无第二 Steam 测试端；完整原因见 baseline.md。LFS pull 完成，origin/dev 无新增提交、无冲突。 |
| P0-2 | `.gitattributes` 修正二进制 .asset | done | 6151cb1 | R1：基线 C# 编译通过，本步不改源码；4 个 LightingData 原始字节与 HEAD 一致，asset 状态干净；check-attr binary=set | 95 个 .asset 中 4 个 LightingData 为二进制，XRSettings 为 JSON 保留文本，无 LFS 指针。I6 不适用。 |
| P0-3 | 修复正式包编译（B1） | done-unverified | 5a77b8a | R1 通过；R3 构建 0 error、主菜单启动；R7 本机测试未完成，见 p0-validation.md | 32 种宏组合源码比较通过；本机连接/Ready/配装同步已确认，RaceMap 注册阻塞有效预测回归。 |
| P0-4 | 调试探针只进 Editor/Dev（B2） | done-unverified | 84ad1be | R1/R2/R3 通过；正式包无探针元数据和心跳；R7 受 N2 阻塞未重跑 | 6 处守卫，192 组 Editor/Dev 源码比较一致；两种包均到达主菜单且启动日志无 Exception。 |
| P0-5 | 取消跟踪 `.VSCodeCounter`、`agent-exchange/console/raw` | done | 9f12e5f | 两目录跟踪数为 0；19 个文件逐字节保留 | 新增整目录 ignore；digest/handoff 保留，未改写历史。I6 不适用。 |
| P1-1 | 删除零引用脚本 | done | 32160bf | R1/R2 通过；见 p1-validation.md | 13 个目标 GUID/C# 引用均为 0、无启动钩子；保留 CombatAdapter。修正 refscan 的 Windows 路径过滤。 |
| P1-2 | 删除 MiniMap 半成品三件套 | done-unverified | 9319ae1 | R1 通过；本机 R4 辅助回归通过，完整矩阵未执行 | Presenter 无引用；Locator/Mapper 仅被 Presenter 引用，三件套外部引用为 0；MiniMapController 保持原样。 |
| P1-3 | 删除旧大厅链路 | done-unverified | 4aff429 | R1 通过；本机 R4 辅助回归通过，完整矩阵未执行 | 两脚本 YAML 引用为 0；MainMenu legacy prefab 为 null；仅旧链路内部与可移除 spawn 分支依赖。 |
| P1-4 | 删除 RaceFinishManager 调试结束路径 | blocked | 9205b7b | 不删除：预检发现活跃 UnityEvent | RaceMap.unity:6858 直接绑定 TriggerDebugFinishRaceFromLocalUi；按方案必须保留该方法及其调用链。 |
| P1-5 | 删除无调用者 API | blocked | 8f02f68 | 已删 29 个无调用声明；R1 通过；本机双端技能回归通过，完整 R4/R5 未执行 | SetInputSource 有实际控制路径调用，保留该项；其余 API 逐项引用/绑定复核后删除。 |
| P1-6 | 删除 DirectHeadingControl 分支 | done-unverified | 072fd93 | R1 通过；本机双端回归通过，R4 完整矩阵未执行 | 两个 sealed 输入源的 UseDirectHeadingControl 均为字面量 false；保留输入接口，移除不可达模式及专属无用字段。 |
| P1-7 | 移除未使用的包，锁定 ParrelSync 版本 | blocked | 438b0b7 | 四包移除、ParrelSync 固定；R1/R2 通过；本机双端回归通过，完整 R4 未执行 | SoftMask 在 RaceMap → UIprefap.prefab 中仍使用，保留该包；GUID 依赖预检推翻原计划“未使用”假设。 |
| P1-8 | 移除 FishNet Demos，重新生成 DefaultPrefabObjects | done-unverified | 71e63d5 | R1/R2 通过；本机回归通过；R4/R5 不完整，R6 未执行 | 306 个 Demo 文件；默认表实际 12 Demo + 3 游戏，保留全部三个游戏 prefab GUID/fileID。 |
| P2-1 | 新增 Foundation 模块 | done | dc3bcda | R1 通过：已加载 GameLog 类型、无 C# error | 暂不接入调用点；Registry 明确 activeInHierarchy/禁用组件语义，并首次查询补齐 Awake 顺序差异。 |
| P2-2a | New_Buddah 日志迁移 | done | 1b62bcf | R1/R2/R3/R7 通过；48 例回归后 1232/615 心跳非零均为 0 | 原消息、context、开关不变；N5 独立修复后回归完成。 |
| P2-2b | Buddah 日志迁移 | done | bc0459b | R1/R2/R3/R7 通过；48 例回归后 1232/615 心跳非零均为 0 | 原消息、context、开关不变；N5 独立修复后回归完成。 |
| P2-2c | Network 日志迁移 | done | aaa5382 | R1/R2/R3/R7 通过；48 例回归后 1232/615 心跳非零均为 0 | 原消息、context、开关不变；N5 独立修复后回归完成。 |
| P2-2d | RaceIntro 日志迁移 | done | 2b7f6d0 | R1/R2/R3/R7 通过；48 例回归后 1232/615 心跳非零均为 0 | 原消息、context、开关不变；N5 独立修复后回归完成。 |
| P2-2e | UI 日志迁移 | done | dbc5ba4 | R1/R2/R3/R7 通过；48 例回归后 1232/615 心跳非零均为 0 | 原消息、context、开关不变；N5 独立修复后回归完成。 |
| P2-3 | 常量与身份收敛 | done-unverified | 8dfe69b | R1 通过；本机双端重开/回房间通过，完整 R4 未执行 | 常量值与身份规则保持；见 p2-validation.md。 |
| P2-4 | 接入 PlayerRegistry | blocked | 0a81fbd | 5 处等价查询；全程 Registry 与引擎集合相等 | 其余查询不同组件或包含 inactive，按 I5 保留；部分完成，R8 未验证。 |
| P2-5a | RoomUI 名单快照 | done-unverified | 63335d6 | R1/R7 通过；本机 48 例与重开/回房间完成，R8 未验证 | 详见 p2-validation.md；N6 视觉起点问题转独立修复。 |
| P2-5b | BlackCurtain 目标缓存 | done-unverified | 5df211b | R1/R7 通过；本机 48 例与重开/回房间完成，R8 未验证 | 详见 p2-validation.md；N6 视觉起点问题转独立修复。 |
| P2-5c | 组件查询缓存 | done-unverified | d09c941 | R1/R7 通过；本机 48 例与重开/回房间完成，R8 未验证 | 详见 p2-validation.md；N6 视觉起点问题转独立修复。 |
| P2-5d | UI 文本快照 | done-unverified | 6927cb8 | R1/R7 通过；本机 48 例与重开/回房间完成，R8 未验证 | 详见 p2-validation.md；N6 视觉起点问题转独立修复。 |
| P2-5e | motor 调试摘要惰性构建 | done-unverified | bff7f9e | R1/R7 通过；本机 48 例与重开/回房间完成，R8 未验证 | 详见 p2-validation.md；N6 视觉起点问题转独立修复。 |
| P2-5f | RSM 诊断摘要门控 | done-unverified | b9ccad5 | R1/R7 通过；本机 48 例与重开/回房间完成，R8 未验证 | 详见 p2-validation.md；N6 视觉起点问题转独立修复。 |
| P2-5g | HealthReport 帧更新门控 | done-unverified | cbe59ca | R1/R7 通过；本机 48 例与重开/回房间完成，R8 未验证 | 详见 p2-validation.md；N6 视觉起点问题转独立修复。 |
| P3-1 | SkillAction 基类公共方法 | done | bd6e2ef | 预检 14 个 duration + 10 个 Feel 模板相同；R1/R7、48 例表现及 N7 修复后 16 例时长通过 | 字段留在子类，Reflection 预施放入口保持。 |
| P3-2 | Anti 公共实现共享（保留继承） | done | dc86d79 | R1/资产快照/实际赛道画面通过；R5 加 N7 时长复测通过 | 用户以功能保留为准；原继承和资源原样，见 p3-validation.md。 |
| P3-3 | SkillExecutor 下沉 | done-unverified | ecb5406 | R1/R7/本机重开回房间通过；R5 加 N7 时长复测通过，R6 普通推击不完整 | 5 个查找显式区分 parent-first 与 children-first。 |
| P3-4 | 配置交叉校验（编辑器） | done | 694daed | R1 编译通过，菜单报告 0 error / 6 既有漂移 warning，12 技能资源逐值不变 | Push 18/15 vs 12/10 与四个未使用 VFX 字段，见 p3-validation.md；D5 不改数据。 |
| P4-1 | RoomStateManager 下沉 | done-unverified | 96fd9dc | R1、两轮本机 R4/R9 通过；336/166 心跳 div=0 | 7 集合 reset 矩阵与 room ready/旗标复位通过 |
| P4-2 | RSM 三件套参数化 | done-unverified | 6f99c5e | 双端开赛/重开/回房间与 100ms 通过 | 原 guard/空集合语义保持；48 辅助检查通过 |
| P4-3 | PropertiesSelectionManager 下沉 | done-unverified | d6876cb | R1；全员提交、60s 超时自动补全、真实 client 断线通过 | cache 重建时机保持；完整 Steam/三圈矩阵未执行 |
| P4-4 | PlayerProgressReporter 分区 | done-unverified | 0d56d02 | 两轮实际结算 UnityEvent、重开和回房间通过 | 去掉 region 后 token 相同；完整 R4 未执行 |
| P5-1 | motor 拆成 partial 文件 | done-unverified | a7934e7 | R1/R3、本机 R7 通过；完整 R9 未执行 | 986/493 心跳 div=0；N8/N9 另修 |
| P5-2 | RunInputs 收尾抽取 | done-unverified | 8f48220 | 本机 0/100ms R7 通过，完整 R9 未执行 | 仅两分支等价，writer 分支保留 |
| P5-3 | 工具函数去重 | done | 8525126 | 边界检查及双端 R7 通过 | 拖尾 null 语义不同不合并 |
| P5-4 | BuddahHandControl 下沉 | done-unverified | 1304d81 | R1/22 helper 通过；N8/N9 修复后本机 R6 专项通过 | 外部条件/通用碰撞等价性未验证 |
| P5-5 | 影子对比表驱动（可选） | done | 不采用 | 原影子逻辑/格式不变 | 可选项无必要行为收益，本轮只拆文件 |
| P6-1 | 第三方程序集前置 | done | 3a79e6f | R1 主 Editor 编译通过；实际程序集名核对 | Feel 已有程序集，无需重复拆分 |
| P6-2 | Runtime / Editor 程序集 | done-unverified | 5a26a36 | R1/R2/R3/R10 与本机 R4/R9 通过；336/166 心跳 div=0 | 外部 Steam/Dev 双端及三圈未验证；六绑定实际触发 |
| P6-3 | 可选 UI 程序集 | done | 不采用 | 保留既有 UI 依赖 | 本轮边界为 Runtime / Editor / Tests |
| P6-4 | EditMode 测试 | done | d9a8f96 | 主/clone 各 43/43 通过，零失败/跳过 | Editor-only，internal 使用 friend assembly |
| P7 | 资源瘦身（需 D7） | blocked | 用户暂缓 | 未执行 | 等待美术确认，不改写历史 |

## 决策

| ID | 事项 | 结论 | 决定人 / 日期 |
|---|---|---|---|
| D1 | 调试开关默认关闭 | 待定 | |
| D2 | 移除 Legacy 运动路径 | 待定 | |
| D3 | 删除预测协议里的死字段 | 待定 | |
| D4 | RaceGateState 枚举 | 待定 | |
| D5 | JSON 兜底配置，SO 与配置表的漂移 | 待定 | |
| D6 | 第三方 asmdef 与程序集拆分 | 授权 P6 | 用户 / 2026-09-30 |
| D7 | 资源瘦身，是否改写历史 | P7 暂缓，等待美术确认；不改写历史 | 用户 / 2026-09-30 |
| D8 | 修复缺陷 B3–B14 | 既有 B3–B14 仍待定；本轮新发现 N1/N2 按用户“记录问题、修复后集中回归”要求单独修复，见 PR #48 | 用户 / 2026-09-29 |

## 新发现

执行中发现的新缺陷或与方案不符之处，记在这里，不要在重构提交里修。

| 日期 | 位置 | 描述 | 关联步骤 |
|---|---|---|---|
| 2026-09-29 | 本机 Tugboat host 名单 | N1：无 Steam 的本机 host 首次登记 IsHost=false；已在独立修复 be7c0fd / PR #48 处理，一次回归无需名单补丁，待合并。 | P0-3 / R4 |
| 2026-09-29 | ParrelSync client RaceMap | N2：SceneCondition 缺失导致场景注册/交接受阻；已在 be7c0fd / PR #48 处理，本机两端 195 秒回归通过，待合并。 | P0-3 / R7 |
| 2026-09-29 | 正式包启动 / 基线资源 | N3/N4：无 Steam 的初始化连带异常，以及字体、空动画、LightingData 告警；详见 p0-validation.md。 | P0 |
| 2026-09-29 | RaceMap EndMatch UnityEvent | P1-4 删除前提不成立：直接绑定 RaceFinishManager.TriggerDebugFinishRaceFromLocalUi（Assembly-CSharp）；保持现有按钮行为。 | P1-4 |
| 2026-09-30 | Handoff shadow / 重置传送 | N5：真实侧 ResetModifiers 会清空 handoff 和 pending，shadow 未处理；P2 技能夹具第一次传送前 95 秒无 div，传送后 hof-div=1，测试停止。该文件与 origin/dev 的 blob 相同；32 组合单测复现 4 失败。独立修复后再继续技能矩阵。 | P2 / R7 |
| 2026-09-30 | ReconcileState `_handoffState = data.HandoffState` | N10（代码审查发现，既有缺陷）：reconcile 带来的 handoff tick 字段是 server 时钟，纯 client 用自己的 LocalTick 推进，整个窗口停在 Inherit、跳过 Blend；与 N7/N8 同类。用户后续要求归入现有 PR；本轮实现与验证见 review-fixes-validation.md，不新开 PR。 | P5 / R7 / R9 |

## 阶段 PR

- P0：[PR #47](https://github.com/OldMustClimbStudio/BuddahGo/pull/47)，已实现并记录未验证范围，草稿。
- 本机联机启动修复：[PR #48](https://github.com/OldMustClimbStudio/BuddahGo/pull/48)，提交 `be7c0fd`，从 dev 单独分支；正常 host 身份、场景注册和开赛交接已恢复。本机两端各运行约 195 秒（普通 95 秒 + 100ms LatencySim 100 秒），D-LOC 非零窗口/FATAL/SceneId 错误均为 0。
- 2026-09-29 用户更新：后续使用 stacked PR，最后统一处理，不逐阶段合并 dev。当前依赖为 #48（base dev）→ #47（base fix/local-multiplayer-flow）→ P1（base refactor/architecture-optimization）。P0 已通过 d6439a6 纳入联机修复基线。
- 按用户要求，阶段内先记录问题并完成修改，再集中执行相关构建/本机回归；每步仍保留独立实现提交及预检证据。
- P1：[PR #49](https://github.com/OldMustClimbStudio/BuddahGo/pull/49)，base refactor/architecture-optimization；完整验证记录见 p1-validation.md。
- P2：[PR #50](https://github.com/OldMustClimbStudio/BuddahGo/pull/50)，base refactor/architecture-p1；构建通过，发现 N5，独立修复后恢复双端专项。

- N5：[PR #51](https://github.com/OldMustClimbStudio/BuddahGo/pull/51)，base refactor/architecture-p2；修复后两端 32/32 组合通过，48 技能用例、重开与回房间完成，1232/615 条心跳各项 div=0。
- N6（2026-09-30）：反噬连发服务器/视觉在不同端重算坐标；示例纵向偏差 8.335571。按用户“所有技能表现正确，无坐标错误/打不中/表现差别”要求另开修复 PR，不混入 P3 重构。
- B11 决定：用户确认 SlowTrap 反噬“就是没特效的”；保留空 id 与现有定身后加速，只在独立修复中跳过无效 VFX 调用。
- P3-2 预检：更改 Anti 继承会迁移序列化字段声明类，与 I1 冲突。用户要求以保留全部功能为准，执行方案改为共享普通方法，保留原继承、字段声明、GUID 和资源值；不做字段迁移。
- 用户补充验收：技能/反噬的全屏变亮、必要摄像机移动/FOV/震动必须存在，后续回归采样实际 Volume 和相机状态并检查恢复。

- 技能表现修复：[PR #52](https://github.com/OldMustClimbStudio/BuddahGo/pull/52)，base fix/teleport-handoff-shadow，979fb87；编译通过，运行验证与 P3 集中进行。

- P3：[PR #53](https://github.com/OldMustClimbStudio/BuddahGo/pull/53)，base fix/skill-presentation-consistency；48 例、重开/回房间完成，1236/617 心跳 div=0。N6/B11 已验证；既有 N7 时基问题阻止 R5 完整通过，见 p3-validation.md。
- N7（2026-09-30）：SlowTrap anti 在 server 定身 1s 后加速，client 在 21s 用例内仍 rooted。P2/P3 都复现；modifier 服务器 deadline 与 client 非同步 LocalTick 混用，独立修复，保留协议布局。

- N7 独立修复完成：modifier deadline 映射客户端 LocalTick，15 纯逻辑检查及 16 双端/延迟用例通过；570/283 心跳 div=0，重开和回房间完成。见 n7-modifier-clock-validation.md。

- N7：[PR #54](https://github.com/OldMustClimbStudio/BuddahGo/pull/54)，base refactor/architecture-p3，85486fa；16 用例时长修复回归通过。P4 继续堆叠在此修复之上。

- P4：[PR #55](https://github.com/OldMustClimbStudio/BuddahGo/pull/55)，base fix/modifier-deadline-clock；本机两轮、超时与断线专项通过，见 p4-validation.md。

- N8/N9（2026-09-30）：P5 物理矩阵发现 remote owner 的 server 冲量/传送事件与非同步 LocalTick 比较，导致冲量积压约 50 秒、传送未消费；旧冲量也污染后续近战夹具。保持 P5 重构差异，独立 fix 后重测。见 p5-validation.md。

- P5：[PR #56](https://github.com/OldMustClimbStudio/BuddahGo/pull/56)，base refactor/architecture-p4；实现和初测完成，R6 等待独立 N8/N9 修复回归。

- N8/N9：[PR #57](https://github.com/OldMustClimbStudio/BuddahGo/pull/57)，base refactor/architecture-p5，55aa5ab；20 项矩阵及四项受控近战完成，654/326 + 90/45 心跳 div=0。全部投射物、有效距离推击及 owner 复活消费通过；此前超射程未命中如实保留。

- P6：[PR #58](https://github.com/OldMustClimbStudio/BuddahGo/pull/58)，base fix/owner-event-clocks；两种构建及启动、两端各 43 EditMode、主菜单/Timeline UnityEvent、0/100ms 两轮回归全部完成。详见 p6-validation.md。

## 代码审查

- 2026-09-30：#47–#58 全部 approve-with-nits，无 blocker；跟进项 F1–F7 见 [review-2026-09-30.md](review-2026-09-30.md)。F1 为既有缺陷 N10（handoff 时钟），用户后续要求在现有 PR 中修复，不新开 PR。

## 本轮收尾

P0–P6 的可执行改动已实现并以 stacked draft PR 提交，未合并 dev。P1-4、P1-5、P1-7、P2-4 中预检不成立的部分继续标 blocked：活跃 UnityEvent/API/SoftMask、不同语义查询均保留，以避免破坏现有功能；不是遗漏删除。其余 done-unverified 行保留各项环境与基线限制，后续 N1/N2、N5、N6/B11、N7、N8/N9 修复与复测见对应阶段记录。

P7 按用户决定等待美术确认。原工作区既有 PackageManagerSettings.asset 修改保留；本轮 worktree 的 Unity 自动生成无关改动已恢复。当前交付顶端为 refactor/architecture-p6，完整堆叠证据在以上 PR 与各阶段验证文档。

## 审查修复执行（2026-09-30，本地）

本轮隔离分支 `fix/review-fixes-20260930`，基线 `f7f6c7f`。主目录仍为 dev，已有 PackageManagerSettings.asset 修改保留；旧 P6、phase6/phase7 worktree 未修改。未更新远端、未新开 PR、未合并、未部署、未强推。完整范围、逐 PR 文件/片段归属与证据见 [review-fixes-validation.md](review-fixes-validation.md)。

| 项 | 状态 | 本轮提交 | 实施与验证边界 |
|---|---|---|---|
| F1 N10 | done-unverified | bcdbedc | 用户明确本轮一起修复；17 个 N10 用例通过，全套 EditMode 60/60，R1 编译通过。0/100ms 双端及位置校正量未测；不扩展其他网络修复。 |
| F2 meta | done | 81d5fc1（旧 helper 删除在 bcdbedc） | 16 个存续两行 meta 补齐 MonoImporter，本次 Unity 已导入且 GUID 全部不变；第 17 个随合并的 BuddahModifierTickClock 删除。 |
| F3 历史压缩 | blocked / 待决定 | 不执行 | 用户明确保留历史；不 fixup/rebase bff7f9e 与 6ae3c72，保留中间提交的编译限制记录。 |
| F4 文档 | done-unverified | 本记录 docs 提交 | baseline 对齐历史证据；P2 默认开关无门控收益、SlowTrap host 限定、P3 OnEnable、6 个 UnityEvent、P0 日志删除恢复说明已纠正。R6/R8 缺口保留，#47 描述未发布。 |
| F5 N6 取舍 | done-unverified | 本记录 docs 提交 | 服务器世界起点与客户端模型的车速×延迟偏移已记录；设计不变，高速人工验收待团队确认。 |
| F6 整理 | done-unverified | 81d5fc1；时钟合并 bcdbedc | burst 无调用 API、warning、using、Handshake/调用方/测试、RoomRoster、NaN 注释、测试宏/identity/asmdef、ProBuilder 配置已处理。R1、EditMode 60/60 和静态不变式核对通过；完整双端回归未跑。 |
| F7 SceneCondition | done | 本记录 docs 提交 | networking.md 记录 global/场景归属及非 BufferLast RPC 的观察关系前提；文档链接核对通过。 |

代码后续清理：`SkillVfxReplicator.PlayVfxAllObserversRpc` 无调用方，当前实际使用 `PlayVfxAllObserversRpcCustom`；按本次范围仅记账，保留旧 RPC 声明及其协议顺序，留待专门 RPC 清理。

验证记录必须区分本次与历史 P6 43/43；未执行的跨机 Steam、三圈、N6 高速人工验收、N10/R7 的 0/100ms 双端、R6 通用碰撞及 R8 dev 对照均不标通过。D1、D7/P7 及视觉设计未决事项保持不变。

用户最新优先级为 review 中的功能性问题，网络问题后续集中处理。报告中除 N10 外没有另一项已确认的游戏功能缺陷；F2 为导入元数据完整性，F6 空 skillId warning 为诊断补充，其余主要为代码卫生、文档或待验收事项。已完成的无害整理保留，不据此扩大功能范围。用户随后明确回答 N10“这次一起修复”；本轮已纳入，实现与其余整理可分离，其他网络整改仍留后续。

本地提交台账：`bcdbedc`（N10 及 17 项测试）、`81d5fc1`（其余整理/meta/测试适配）、本记录所在的 `docs: record review fixes and validation`。上述均在隔离分支，尚未发布到现有 PR；F4/F5/F7 的文档提交可通过该唯一标题在本分支定位。最终可运行结果与未验证项见 review-fixes-validation.md。


### 2026-09-30 现有 PR 审查跟进发布

按 #48 → #47 → #49–58 的原 head 创建独立本地分支，逐层普通 merge 传播，按归属追加修复；不改 dev/main、不新 PR、不强推、不历史重写。补齐 #58 非空 transport 测试与 #50/#53 注释，最终栈 R1 编译通过、EditMode **61/61**（本轮实际执行），结构/GUID/祖先关系检查通过。各 PR 的 disposition 见 [review-followups.md](review-followups.md)，测试和找回历史原始日志的 SHA256 见 [review-fixes-validation.md](review-fixes-validation.md)。N10 live 双端、N6 人工高速、完整 R6/跨机 Steam/R8 未执行，不以 helper 结果替代。F3 历史编译问题按用户要求留最后单独诊断。


## 2026-09-30 second review follow-up

Functional corrections and actual local two-process evidence are tracked in
[review-runtime-round2.md](review-runtime-round2.md). The impulse replay timing fix,
diagnostic log sampling and session-helper documentation are staged locally.
R8 scene measurements and #52 visual acceptance remain open; this entry does not
claim all new comments resolved. No history rewrite or dev/main merge is performed.
