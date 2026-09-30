# 现有 PR 审查跟进（2026-09-30）

按 #48 → #47 → #49–58 保持普通 merge 祖先关系；不改写历史。最终验证记录在 #58 汇总。

## PR #48

静态检查 MatchSpawnManager.TrySpawnPlayer 的 Spawn→ApplyResolvedSkillLoadout 链：SkillLoadout.OnStartServer/SetSlotsServer 只修改 SyncList；SkillExecutor.OnStartServer 只解析引用；PlayerProgressReporter 的注册 coroutine 更新 Leaderboard SyncList。所查初始化链没有必须在观察者加入前送达的非 BufferLast ObserversRpc。后续技能/终局 RPC 属于运行期操作，不改为缓冲。SceneCondition/global 规则已补充；回主菜单/再来一局已有证据归 #49 的 P1 验证，未将其扩大为跨机 Steam 证明。

## PR #47

补充日志删除影响、恢复命令与 SERGATE 宏说明。CONTRIBUTING 的分支命名统一为 <type>/<topic>，Tools/Audit/refscan.py 为引用审计脚本；PR 描述同步补充。F3 经用户确认保留现有历史，后续修复用追加/merge 传播，不做 fixup/强推。
