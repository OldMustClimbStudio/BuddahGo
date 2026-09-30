# N5：重置传送后的 handoff shadow 偏差

P2 的技能专项在首个施放前调用现有 TryApplyServerAuthoritativeTeleport（ResetModifiers=true）。真实 ConsumePendingTeleportEvent 清除 handoff 状态及 pending 槽，随后 RefreshLaunchState 归一化空状态；BuddahHandoffStep 仍从传送前快照推进，导致 hof-div=1。该文件在 origin/dev 与 P2 的 blob 均为 2d7167491daa7b8cc38ac1f944a0d7a58c587d87，属于既有诊断模型遗漏。

修复只调整 shadow：读取先前 TeleportStep 实际消费的 reset 标志，清空旧 handoff 并取消同 tick pending handoff，再走原 Advance。真实运动/传送路径、RPC、协议结构和比较器不改。

验证：Tools/Validation/teleport-handoff-cases.cs.txt 是可交给 Unity MCP execute_code 的独立代码体，覆盖 5 个布尔维度的 32 种组合（传送存在、已到期、清理标志、handoff 存在、已到期），比较完整 handoff state、是否消费、消费 cursor。使用反射调用以兼容 CodeDom 对 readonly in 参数的识别。

- 修复前：4/32 失败，cases 7/15/23/31；其中 case 31 还错误消费了已被传送取消的 handoff。
- 修复后：32/32 通过，Unity C# 编译通过。完整双端技能矩阵尚待恢复执行。
- 失败原始记录已保存在本机 Logs/p2-skill-matrix-initial-divergence（不提交原始日志）；传送前约 95 秒没有 divergence，首次传送后 host 记录 138 条心跳并自动停止。

## 修复后完整本机回归（2026-09-30 05:33 UTC）

在 74c1ea8 上以原 Editor + ParrelSync、Tugboat 跑完 48 个技能用例（6 技能 × 正常/反噬 × host/client × 0/100ms）。host/client 分别施放 24 次；48 次定位传送、再来一局、第二局运行和 client 投票回房间均完成。主机约 1284 秒、客户端约 1261 秒；D-LOC 心跳分别 1232/615 条，loc/tel/mod/hof 的非零计数全部为 0，无 FATAL、夹具异常或 SceneId 错误。两端 32/32 组合检查与本次运行共同验证 N5 已修复。

技能矩阵另发现反噬连发视觉起点本地重算造成坐标不一致（N6），与 shadow 修复无关，转独立修复。R4 通过的是按钮结算/重开/回房间路径，未代替真实三圈完赛；未使用第二 Steam 账号。
