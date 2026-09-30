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
| P2-2a | New_Buddah 日志迁移 | doing | 1b62bcf | 待 P2 集中验证 | 27 处调用；原消息、context、开关不变。 |
| P2-2b | Buddah 日志迁移 | doing | bc0459b | 待 P2 集中验证 | 77 处调用；原消息、context、开关不变。 |
| P2-2c | Network 日志迁移 | doing | aaa5382 | 待 P2 集中验证 | 50 处调用；原消息、context、开关不变。 |
| P2-2d | RaceIntro 日志迁移 | doing | 2b7f6d0 | 待 P2 集中验证 | 52 处调用；原消息、context、开关不变。 |
| P2-2e | UI 日志迁移 | doing | dbc5ba4 | 待 P2 集中验证 | 8 处调用；原消息、context、开关不变。 |
| P2-3 | 常量与身份收敛 | doing | 8dfe69b | | |
| P2-4 | 接入 PlayerRegistry | blocked | 0a81fbd | 5 处等价 movement 查询已接入，待阶段验证 | 其余实际查找不同组件或包含 inactive，保留以满足 I5；见 p2-validation.md。 |
| P2-5a | RoomUI 名单快照 | doing | 63335d6 | | |
| P2-5b | BlackCurtain 目标缓存 | doing | 5df211b | | |
| P2-5c | 组件查询缓存 | doing | d09c941 | | |
| P2-5d | UI 文本快照 | doing | 6927cb8 | | |
| P2-5e | motor 调试摘要惰性构建 | doing | bff7f9e | | |
| P2-5f | RSM 诊断摘要门控 | doing | b9ccad5 | | |
| P2-5g | HealthReport 帧更新门控 | doing | | | |
| P3-1 | SkillAction 基类公共方法 | todo | | | |
| P3-2 | Anti 类改为继承 | todo | | | |
| P3-3 | SkillExecutor 下沉 | todo | | | |
| P3-4 | 配置交叉校验（编辑器） | todo | | | |
| P4-1 | RoomStateManager 下沉 | todo | | | |
| P4-2 | RSM 三件套参数化 | todo | | | |
| P4-3 | PropertiesSelectionManager 下沉 | todo | | | |
| P4-4 | PlayerProgressReporter 分区 | todo | | | |
| P5-1 | motor 拆成 partial 文件 | todo | | | |
| P5-2 | RunInputs 收尾抽取 | todo | | | |
| P5-3 | 工具函数去重 | todo | | | |
| P5-4 | BuddahHandControl 下沉 | todo | | | |
| P5-5 | 影子对比表驱动（可选） | todo | | | |
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

## 阶段 PR

- P0：[PR #47](https://github.com/OldMustClimbStudio/BuddahGo/pull/47)，已实现并记录未验证范围，草稿。
- 本机联机启动修复：[PR #48](https://github.com/OldMustClimbStudio/BuddahGo/pull/48)，提交 `be7c0fd`，从 dev 单独分支；正常 host 身份、场景注册和开赛交接已恢复。本机两端各运行约 195 秒（普通 95 秒 + 100ms LatencySim 100 秒），D-LOC 非零窗口/FATAL/SceneId 错误均为 0。
- 2026-09-29 用户更新：后续使用 stacked PR，最后统一处理，不逐阶段合并 dev。当前依赖为 #48（base dev）→ #47（base fix/local-multiplayer-flow）→ P1（base refactor/architecture-optimization）。P0 已通过 d6439a6 纳入联机修复基线。
- 按用户要求，阶段内先记录问题并完成修改，再集中执行相关构建/本机回归；每步仍保留独立实现提交及预检证据。
- P1：[PR #49](https://github.com/OldMustClimbStudio/BuddahGo/pull/49)，base refactor/architecture-optimization；完整验证记录见 p1-validation.md。
