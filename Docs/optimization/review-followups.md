# 现有 PR 审查跟进（2026-09-30）

按 #48 → #47 → #49–58 保持普通 merge 祖先关系；不改写历史。首轮验证见 [审查修复记录](review-fixes-validation.md)，后续公开证据见 [双端运行跟进](review-runtime-round2.md)。以下按已发布代码更新契约，不把历史测试结果扩大到后续提交。

## PR #48

静态检查 MatchSpawnManager.TrySpawnPlayer 的 Spawn→ApplyResolvedSkillLoadout 链：SkillLoadout.OnStartServer/SetSlotsServer 只修改 SyncList；SkillExecutor.OnStartServer 只解析引用；PlayerProgressReporter 的注册 coroutine 更新 Leaderboard SyncList。所查初始化链没有必须在观察者加入前送达的非 BufferLast ObserversRpc。后续技能/终局 RPC 属于运行期操作，不改为缓冲。SceneCondition/global 规则已补充；回主菜单/再来一局已有证据归 #49 的 P1 验证，未将其扩大为跨机 Steam 证明。

## PR #47

补充日志删除影响、恢复命令与 SERGATE 宏说明。CONTRIBUTING 的分支命名统一为 <type>/<topic>，Tools/Audit/refscan.py 为引用审计脚本；PR 描述同步补充。F3 经用户确认保留现有历史，后续修复用追加/merge 传播，不做 fixup/强推。

## PR #49

删除遗留 ProBuilder Settings.json。PlayVfxAllObserversRpc 没有调用方，本轮保留声明以避免改变 RPC 索引。MainMenu._legacyLobbyManagerPrefab 与 Buddah.directHeadingDegreesPerSecond/directHeadingAngularDamping/directHeadingSnapAngle 的旧 YAML 项仍在；运行时代码已不读取，保留避免额外场景/prefab 改动。R6 的 P1-8 通用碰撞等价性仍未完整验证。

## PR #50

BlackCurtain 在下次 rebuild 才发现 fade 中新激活/生成的根对象；ResultAreaInteractionGate 保留 actor 根组件缓存，进一步缓存仍需失效契约。NetLog.Info 的 Conditional 保留 Release 的既有裁剪，Warning/Error 不变。后续按用户决定移除预测/技能输入屏幕 debug，保留日志和普通 HUD，见 [诊断契约](../diagnostic-log-sampling-r8.md)。[公开 R8](review-r8-editor.md) 已区分原 P2 与 log-only 增量；仅限记录的 Editor 比较，不代表 Player/Release 或完整 R4/跨机验收。

## PR #51

SlowTrap 旧矩阵的 root/force 恢复结论限定 server/host；纯 client 时钟证据归后续 #54。历史 48 例原始日志未纳入仓库，不能从摘要重建为原始证据。本轮最终 EditMode 属于 helper/阶段验证，不能替代旧矩阵重跑。

## PR #52

删除无调用的两项 burst wrapper；空 replicatedSkillId 提前警告。server world-origin 视觉设计保持。后续[公开视觉记录](review-runtime-round2.md#high-speed-visual-capture-and-shader-environment) 已说明缓存修复与采集边界；用户视觉接受和视频公开许可仍待定，不能把数值一致当作视觉接受。

## PR #53

五个 importer meta 保持 GUID 并补全；SerializedObject 使用 using；纠正 LoadAssetAtPath 可触发 OnEnable。ApplyScaleServer 两个 duration 参数保留 SkillExecutor 转发签名，注释说明仅 presentation 消费，server physics 不读取，未改调用/行为。

## PR #54

补全旧 modifier clock 的 importer meta，随后在 #57 与 BuddahTickMath 统一时删除旧 helper/meta。后续 `97a186f` 将 reconcile 的 modifier/handoff 映射改为快照 `ServerStateTick` / `ClientStateTick` 配对；合成边界测试覆盖接收时钟回退、合法 deadline 延长及历史 replay。现行契约见 [预测设计](../prediction-design.md#reconcile-快照与本地期限)，不再沿用“映射算法未改”的首轮结论，也不将测试输入当作实测数据。

## PR #55

Handshake 集合私有化，提供按 Stage 操作的语义 API，更新 RSM/RoomDiagnostics 与验证脚本；RoomRoster 命名整理，四个 meta 保持 GUID。Diagnostic* 保留 internal：仅同程序集协作者使用，未扩大公共 API；进一步只读快照封装属可选设计，不引入分配或新架构。

## PR #56

补充 NaN 有序比较意图，七个 importer meta 保持 GUID。22 个 helper cases 已由 #58 的 Assets/Tests/EditMode/BuddahTickMathTests.cs 和 ProjectileBurstPlannerTests.cs 覆盖，不重复迁移。已公开的 [R6 矩阵、夹具 miss 与补证](review-runtime-round2.md#r6--r7-and-charged-timing) 各自保留原范围；不据此将通用碰撞等价性或未公开补证标为全面通过。

## PR #57

N10 纯 client reconcile 工作副本统一平移六个 handoff tick，区分零事件/阶段边界与 deadline 零哨兵；owner GetTick 不重复平移，server/host 不平移。`97a186f` 使用快照配对而非接收时钟。`c853768` 令纯 client 冲量按历史 ServerReplayTick 判断 replay 消费资格，前向与 server/host 分支保留原时钟。两种时钟用途及测试入口统一见 [预测设计](../prediction-design.md)。迟到客户端完整 12/20 tick 呈现仍需协议决策，不随时钟修复标完成。

## PR #58

N10、Handshake、identity 及测试程序集整理的首轮结果保留在 [审查修复记录](review-fixes-validation.md)。后续增加冲量消费资格和诊断采样/wiring 测试，已公开运行结果见 [第二轮记录](review-runtime-round2.md#reproduction-and-evidence)。`e66e59d` / `4dfb45d` 的快照配对回归使用合成输入，覆盖 owner/observer、历史 Blend 和合法期限延长；旧 61/75 项通过数不改写成这些新测试的运行结果。


## 历史证据定位补充

首轮只读追查找到了旧 P6 + clone 的 P2/P3/N7/N8 原始 JSONL 与 P2 图像，来源摘要见 [验证记录](review-fixes-validation.md#找回的历史原始证据)。该核验只证明来源/范围，不把旧执行当成后续修复的 runtime 通过。#51 的 SlowTrap 旧结论仍限定 server/host。第二轮已公开材料以 [运行记录](review-runtime-round2.md) 和 [R8](review-r8-editor.md) 为入口；未公开原日志、数值与视频不随文档整理补入。
