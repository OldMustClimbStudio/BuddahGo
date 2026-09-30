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
| P5-4 | BuddahHandControl 下沉 | blocked | 1304d81 | R1/22 helper 检查通过；R6 发现 N8/N9 | 坐标/服务器命中通过，owner 冲量延迟；另修后回归 |
| P5-5 | 影子对比表驱动（可选） | done | 不采用 | 原影子逻辑/格式不变 | 可选项无必要行为收益，本轮只拆文件 |
| P6-1–4 | 程序集拆分与测试（需 D6） | todo | | | |
| P7 | 资源瘦身（需 D7） | todo | | | |

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
