# 执行进度

状态取值：`todo` / `doing` / `blocked` / `done` / `done-unverified`。每完成一步就更新本表。流程见 [HANDOFF.md](HANDOFF.md)。

## 步骤

| 步骤 | 内容 | 状态 | 提交 | R 结果 | 备注 |
|---|---|---|---|---|---|
| P0-1 | 记录基线 → baseline.md | done-unverified | 874a55b | 基线 C# 编译 0 error / 11 已有 warning；R4–R9 未执行 | 仅本机，无第二 Steam 测试端；完整原因见 baseline.md。LFS pull 完成，origin/dev 无新增提交、无冲突。 |
| P0-2 | `.gitattributes` 修正二进制 .asset | done | 6151cb1 | R1：基线 C# 编译通过，本步不改源码；4 个 LightingData 原始字节与 HEAD 一致，asset 状态干净；check-attr binary=set | 95 个 .asset 中 4 个 LightingData 为二进制，XRSettings 为 JSON 保留文本，无 LFS 指针。I6 不适用。 |
| P0-3 | 修复正式包编译（B1） | done-unverified | 5a77b8a | R1 通过；R3 构建 0 error、主菜单启动；R7 本机测试未完成，见 p0-validation.md | 32 种宏组合源码比较通过；本机连接/Ready/配装同步已确认，RaceMap 注册阻塞有效预测回归。 |
| P0-4 | 调试探针只进 Editor/Dev（B2） | done-unverified | 84ad1be | R1/R2/R3 通过；正式包无探针元数据和心跳；R7 受 N2 阻塞未重跑 | 6 处守卫，192 组 Editor/Dev 源码比较一致；两种包均到达主菜单且启动日志无 Exception。 |
| P0-5 | 取消跟踪 `.VSCodeCounter`、`agent-exchange/console/raw` | done | 本步骤提交（阶段记录补记 hash） | 两目录跟踪数为 0；19 个文件逐字节保留 | 新增整目录 ignore；digest/handoff 保留，未改写历史。I6 不适用。 |
| P1-1 | 删除零引用脚本 | todo | | | |
| P1-2 | 删除 MiniMap 半成品三件套 | todo | | | |
| P1-3 | 删除旧大厅链路 | todo | | | |
| P1-4 | 删除 RaceFinishManager 调试结束路径 | todo | | | |
| P1-5 | 删除无调用者 API | todo | | | |
| P1-6 | 删除 DirectHeadingControl 分支 | todo | | | |
| P1-7 | 移除未使用的包，锁定 ParrelSync 版本 | todo | | | |
| P1-8 | 移除 FishNet Demos，重新生成 DefaultPrefabObjects | todo | | | |
| P2-1 | 新增 Foundation 模块 | todo | | | |
| P2-2a–e | 日志迁移（按目录） | todo | | | |
| P2-3 | 常量与身份收敛 | todo | | | |
| P2-4 | 接入 PlayerRegistry | todo | | | |
| P2-5a–g | 热路径去浪费 | todo | | | |
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
| D6 | 第三方 asmdef 与程序集拆分 | 待定 | |
| D7 | 资源瘦身，是否改写历史 | 待定 | |
| D8 | 修复缺陷 B3–B14 | 待定 | |

## 新发现

执行中发现的新缺陷或与方案不符之处，记在这里，不要在重构提交里修。

| 日期 | 位置 | 描述 | 关联步骤 |
|---|---|---|---|
| 2026-09-29 | 本机 Tugboat host 名单 | N1：IsHost=false；测试中显式重做名单刷新后可推进。产品修复按 D8 处理，blocked。 | P0-3 / R4 |
| 2026-09-29 | ParrelSync client RaceMap | N2：多个 SceneId 未注册，开赛控制权未解锁；记录后停止该轮，待修复再回归，blocked。 | P0-3 / R7 |
| 2026-09-29 | 正式包启动 / 基线资源 | N3/N4：无 Steam 的初始化连带异常，以及字体、空动画、LightingData 告警；详见 p0-validation.md。 | P0 |
