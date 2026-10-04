# AI 技能人格实现复核（2026-10-03，针对 `5afe638`）

状态：代码与证据复核。同日修正见实施记录末尾"复核后修正"：R1 撤回（用户已暂定固定六人）、R3 的成因归因有误（见下文更正）、其余项已修正；修正后未重新验证，等待用户测试。对象是 feat/single-player-mode 上的 `5afe638`（feat: implement five Solo AI skill personalities）及其 [实施记录](ai-skill-implementation-2026-10-03.md)。本复核只读代码、资产、场景差异与记录，没有运行 Unity、测试或构建；原始运行数据在实施者的私有证据目录，无法独立核对，以下凡引用数字均来自实施记录。

结论先行：决策管线、承诺状态机、服务器按键注入、掌形瞄准、反转适应与黑幕感知受损都按 [技能管线规格](ai-skills.md) 落地，EditMode 覆盖面合理；但有一项产品范围变更、一项共享代码的玩家规则变更和一项平衡结果需要用户决定或返工，另有几处小修。

## 一、需要用户决定或返工

### R1 Practice（0 AI）从设置面板被移除（已撤回：用户本轮已暂定固定六人）

`SoloSetupPanel` 删除了 AI 数量切换与 `SoloMatch.AICount` 偏好，开始按钮固定 `new SoloMatchSettings(RacerId.MaxAI, _difficulty)`。实施记录写"用户确认单机固定六人"。这与 [CONTEXT.md](../../CONTEXT.md) 的 Solo Match（0–5 个 AI）与 Practice（0 个 AI、只显示时间）定义、design §5.7（首次默认 3 个 AI）以及 phases 中已交付并经用户试玩的 Practice 里程碑相矛盾，而 CONTEXT／GLOSSARY／phases 没有同步更新。

- 若"固定六人"是用户的决定：需同步 CONTEXT.md 的 Solo Match／Practice 条目、design §5.7、phases 的 Practice 条目，并明确 Practice 是否仍然存在（0 AI 练习是否还需要）。
- 若不是：恢复 0／5 切换（或 0–5），`SpawnSoloAI` 的 `count == 0 → return` 已能支持 0 AI。

### R2 共享施法代码里新增的玩家规则变更（含联机路径）

`SkillExecutor.ResetActiveSkillEffectsForOwner` 现在除原有本地表现清理外，还会：取消待执行施法、清空组合缓冲、重置手部控制（含清掉掌形 buff）、清空感知状态、重置运动修饰符、销毁缓流区。它由 `BuddahPredictedMotor.Events` 的复活事件对**真人 owner** 同样调用（原先只对 owner 调用旧版本），`ResetActiveSkillEffectsServer` 也改为在服务器上直接执行这套清理再向 owner 发 TargetRpc。

后果：真人复活时掌形 buff、缓流区与待执行施法现在会被清掉，之前不会；该路径在联机 owner 客户端也执行（含预测修饰符重置）。这是玩家规则变更，且 harness 要求保持预测路由不变、单机任务不跑联机验证。`RequestResetActiveSkillEffectsForRespawn` 已无调用方。

- 建议：把新增的清理项用 `RacerAuthority.IsServerAI` 限定到 AI；或明确接受"复活清除一切技能状态"为新玩家规则并写入 design §5.5，再安排联机复核。二选一，不要隐式保留。

### R3 自然赛结果：每场 3/5 AI 被 15 秒截止判 DNF（成因归因已更正）

三场独立自然赛（dev-01 普通、dev-02 困难、dev-03 普通）都是 2/5 自然完赛、3/5 DNF。困难档完赛者圈时 78 s（快于无技能的 81.5–83.8 s），落后者 93–102 s。

**更正：** 本节初稿把干扰型的施法总数当成持续反转的依据，是误读。记录中干扰型 dev-03 普通场为 13 次黑幕 + 1 次反转，dev-02 困难场为 10 次黑幕 + 2 次反转；高频的是黑幕，不是反转。候选成因因此是高频黑幕带来的全员感知受损（每次约 13 s，普通档前视 ×0.6、失误 +0.1、反应 +6 tick）与少量反转适应叠加，其余车每圈慢 10–20 s，差距超过 15 s 截止窗口就是 DNF。`reverseturn` 执念增益 0 仍是设计复核 F4 的待确认项，但不能据此说已找到 DNF 主因。这不是代码缺陷，但玩家每场都会看到三个 DNF。

- 建议顺序：同一 seed 重跑 dev-03，分别关闭黑幕感知受损与反转，归因哪一项造成差距；`reverseturn` 增益 0→200 已作为平衡实验改入配置，不代表归因结论；仍不理想再讨论黑幕频率（干扰型权重／耐心）、感知受损强度或截止规则。不要先改比赛规则。

## 二、建议的小修

| 编号 | 位置 | 问题 | 修法 |
|---|---|---|---|
| S1 | `AISkillCaster.CanContinueCast` | 确认时 `IsRooted` 会取消 AI 的施法，真人被定身时施法不会被取消；Q2 要求同规则 | 去掉 `IsRooted` 条件，只保留比赛状态与可见全局效果复核 |
| S2 | `SkillExecutor.CastSlotServer` / `QueueCast` | 确认时取消发生在 `PlayQueuedCastFeedbackObserversRpc` 之后，观众已看到前摇甚至反噬动画，然后什么都没发生 | AI 分支在 `CastSlotServer` 入口先做一次可见全局效果检查并直接拒绝（不扣冷却、不播反馈）；确认时的取消保留为兜底 |
| S3 | `AISkillCaster.Aim` | buff 期间任何分支都 `return true`，整段 15 s 不决策；规格写的是"末段 5 s 不搓招"。每次掌形多付 5 s 空窗 | `remaining > 5` 且（发射次数用尽或无可瞄准目标）时放行 `Decide` |
| S4 | `AISkillCaster.Decide` 的 `required` | 只算按键与确认时间，不含失误重试（≥0.35 s + 重搓）与反应延迟；简单档会更多出现 `opportunity-expired` | 加 `KeyMistakeProbability × (窗口 + 序列长度 × KeyMax)` 或固定 0.5 s 余量 |
| S5 | `AI.Skill` 剖析标记 | 包裹整个 `Tick()`，含 `InjectServerPush → TryPushServer` 的协程启动、判定框／投射物生成，以及 Development 构建下 `SkillExecutor` 未加 `NetDebug.EnableVerboseLog` 门控的 `GameLog.Verbose` 字符串构造与 `Debug.Log`；>0.2 ms 的 13 帧很可能是这些而非决策逻辑 | 拆成 `AI.Skill.Decide` 与 `AI.Skill.Input`；给 `SkillExecutor` 的 Verbose 加与 `ComboSkillInput` 相同的门控；再测一次 |
| S6 | design §5.4 | `5afe638` 删掉了用户在 `f0f123b` 刚恢复的那句"难度只影响规则判断的精度。反噬概率照常由执念值决定。技能池以配置仓库为准。" | 恢复该句（可紧跟新段落），不要再覆盖用户的措辞 |
| S7 | design §5.8 | 五个旧名字（漂移禅师等）与分配规则被整段删除；用户只说旧名不是最终名，没有说删除 | 以"历史候选名"保留列表，分配规则按新文字 |
| S8 | 工作区 | `5afe638` 之外还有未提交改动：删除 `Assets/FishNet/.../Mono.Cecil.sln.meta`、`LiteNetLib.csproj.meta`，修改 `ProjectSettings/PackageManagerSettings.asset`，新增 `Assets/Adobe/.../Temp.meta`。是 Unity 会话残留 | `git checkout -- Assets/FishNet ProjectSettings/PackageManagerSettings.asset`，删除 Temp.meta；不要带进下一个提交 |

## 三、核对无误、值得记录的点

- **MapPathLine 的重新启用是正确修复。** 该对象在 dev 与 `559d57c` 为启用，在 `e95c63c`（开场表现提交）变为停用并一直延续到 `f0f123b`；期间产品场景里 `TrackSplineRef.Instance` 不存在，AI 只能直行。`5afe638` 恢复为与 dev 一致。请在试玩时顺带确认赛道上没有多出一条可见路径线（该对象上另有一个未能在源码中定位的组件）。
- 四个测试盒的 `WasActiveDuringEdit` 改为 false 并补 `EmptyNetworkBehaviour`，与停用的 DebugBox 父节点一致；测试准备改为只关闭 Collider／Renderer 并恢复，不再动 SetActive。
- 服务器按键注入走同一个 `ComboSkillInput.PushToken` 与 `BuddahHandControl.TryPushServer`，真人 RPC 仍 `RequireOwnership`；AI 用 tick 时间，真人用 `Time.time`，互不混用。
- `AISkillWorld` 一次快照五车共用、曲率前缀和 O(1)、固定容量历史、无场景查询；决策按 tick 相位与帧级门控交错。`MatchSpawnManager` 在 RaceMap 场景内，Rematch 重载场景时世界随之重建，`??=` 可接受。
- 反转适应用认知符号替换规划输入，物理与效果时长不变；受损 profile 为独立实例，不改资产，不改每车基础 profile（有测试）。
- 黑幕通过 `SkillPerceptionState.ApplyCurtain` 给全部参赛者写服务器状态，真人放的黑幕也会让 AI 受损，Q4 对称成立。
- 权重、局势偏置与严重度数组的索引顺序与 `AISkillKind`／`AISkillSituation` 一致，`SkillPersonalities.json` 五个预设与 [人格配置](ai-personalities.md) 表一致。
- `5afe638` 已推送到 origin。

## 四、验证边界

实施记录中的 160/24/29 项测试、三场自然赛与性能窗口均未由本复核重跑；S5 的峰值归因是代码阅读推断，需实测确认。用户试玩未发生。
