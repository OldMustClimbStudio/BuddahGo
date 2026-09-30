# 现有 PR 审查跟进（2026-09-30）

按 #48 → #47 → #49–58 保持普通 merge 祖先关系；不改写历史。最终验证记录在 #58 汇总。

## PR #48

静态检查 MatchSpawnManager.TrySpawnPlayer 的 Spawn→ApplyResolvedSkillLoadout 链：SkillLoadout.OnStartServer/SetSlotsServer 只修改 SyncList；SkillExecutor.OnStartServer 只解析引用；PlayerProgressReporter 的注册 coroutine 更新 Leaderboard SyncList。所查初始化链没有必须在观察者加入前送达的非 BufferLast ObserversRpc。后续技能/终局 RPC 属于运行期操作，不改为缓冲。SceneCondition/global 规则已补充；回主菜单/再来一局已有证据归 #49 的 P1 验证，未将其扩大为跨机 Steam 证明。

## PR #47

补充日志删除影响、恢复命令与 SERGATE 宏说明。CONTRIBUTING 的分支命名统一为 <type>/<topic>，Tools/Audit/refscan.py 为引用审计脚本；PR 描述同步补充。F3 经用户确认保留现有历史，后续修复用追加/merge 传播，不做 fixup/强推。

## PR #49

删除遗留 ProBuilder Settings.json。PlayVfxAllObserversRpc 没有调用方，本轮保留声明以避免改变 RPC 索引。MainMenu._legacyLobbyManagerPrefab 与 Buddah.directHeadingDegreesPerSecond/directHeadingAngularDamping/directHeadingSnapAngle 的旧 YAML 项仍在；运行时代码已不读取，保留避免额外场景/prefab 改动。R6 的 P1-8 通用碰撞等价性仍未完整验证。

## PR #50

补充 BlackCurtain 缓存快照限制：fade 中新激活/新生成根对象在下次 rebuild 被发现。ResultAreaInteractionGate 保留 actor 根组件缓存；它仍避免每次从 child 向上搜索，进一步组件缓存需失效契约，属低优先级建议。NetLog.Info 的 Conditional 会移除 Release 的 Info 诊断（含连接/场景 Info），Warning/Error 保留；源注释与 N5 表格原已正确。P2-3 实现/I6 静态对照完成，阶段 R1 已有记录；完整 R4/跨机验收未完成。默认调试开关未改，不能声称默认性能收益；R8 无可比基线。

## PR #51

SlowTrap 旧矩阵的 root/force 恢复结论限定 server/host；纯 client 时钟证据归后续 #54。历史 48 例原始日志未纳入仓库，不能从摘要重建为原始证据。本轮最终 EditMode 属于 helper/阶段验证，不能替代旧矩阵重跑。

## PR #52

删除无调用的两项 burst wrapper；空 replicatedSkillId 提前警告。保留 server world-origin 视觉设计及速度×延迟取舍，未做人工高速视觉验收；历史图像/日志缺失不补造。
