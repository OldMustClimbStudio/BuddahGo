# 审查修复验证（2026-09-30）

本轮基于 `f7f6c7ffdd61eff78c84afe722ad8084f103ec70`，仅执行 review-2026-09-30.md 的 F1/F2/F4/F5/F6/F7，不重新 review 整个堆叠。工作目录为主项目的 `.worktree/review-fixes-20260930`，分支 `fix/review-fixes-20260930`。用户主目录 `dev@ce5c1c2` 的 PackageManagerSettings.asset 修改和旧 P6 worktree 的两项 meta 删除保持原样。

## 时钟契约与测试范围

- `BuddahTickMath` 统一事件、期限、modifier 和 handoff 时钟映射，使用已有 N7/N8 的宽整数相减与 `[0, uint.MaxValue]` 饱和语义。纯 client 每次从原始 reconcile 快照建立工作副本；server/host 不平移。
- EventTick/StartTick 的 0 是有效事件时间。FromData 的 InheritEndTick/BlendEndTick 可等于零起点（零时长阶段），属于阶段边界，仍需平移。SuppressSteeringUntilTick/RoomBypassUntilTick 及 modifier deadline 的 0 保留未设置语义。
- 默认 reset 不生成假 handoff；IsActive=false 时仍平移尚有效的 steering/bypass 尾部。非时间数据及 wire struct 均保持不变。
- FishNet `Reconcile_Reader_Remote` 为 owner 设置 ClientStateTick，为 non-owner 设置 ServerStateTick。因此 owner 的 GetTick 不再平移；non-owner 的诊断推进 tick 与转换后的工作状态使用同一时钟。QueueLaunchHandoffTargetRpc 的 owner 锚定换算保持原样。
- 新增 HandoffClockTests 覆盖 1828/2917/负偏移、历史快照重放、Inherit→Blend→Normal 时长与 BlendAlpha、零起点/零时长、default reset、inactive 尾部、host/server/owner/observer 路由、八种 modifier 期限及非时间字段保持。
- 整数边界测试确认转换不会 uint 下溢/上溢回绕。现有 resolver 使用普通大小比较，FromData 的加法本身不是环形时钟协议；本轮没有将整个预测系统改为跨 uint 回绕语义。饱和边界附近可能截短窗口，不能宣称跨完整时钟周期阶段时长仍保持。
- 原 prediction-helper-cases 的 22 项已由 BuddahTickMathTests、ProjectileBurstPlannerTests 覆盖，本轮核对后不重复迁移。Handshake 测试改用语义 API，并保留重置、断线、序列号、空名单和 guard 测试。

## 本轮结果

- Unity 2022.3.55f1c1 batch EditMode 实际运行：**60/60 通过，失败 0、跳过 0**，NUnit 执行时长 0.086154 秒（不含首次导入）。其中 HandoffClockTests 17、BuddahTickMathTests 16、ProjectileBurstPlannerTests 12、RaceStartHandshakeTests 5、LoadoutRulesTests 6、PlayerIdentityTests 4。
- R1：本轮 Runtime/Editor/Tests 及项目 C# 编译通过，最终日志没有 `error CS`。首次尝试发现 Handshake 诊断调用方仍直接访问已私有化集合（CS0122），已同步修正 RoomDiagnostics，最终编译及上述测试通过。
- 证据：本地 `Logs/review-editmode.xml`、`Logs/review-editmode-3.log`；此前失败保留在 `Logs/review-editmode-2.log`。没有引用历史 P6 43/43 作为本次结果。
- 静态核对：29 个 RPC/Replicate/Reconcile 声明的签名与顺序不变（其中 27 个 RPC），3 个预测 wire struct 不变，16 个存续 C# meta GUID 不变；asmdef JSON、修改文档相对链接、`git diff --check` 通过。结果摘要在 `Logs/review-invariants.json`。
- 导入限制：首次导入有 Input System UI 资源的 DirectoryNotFoundException；实际文件存在，路径超过常见 Windows 长度限制，属于长路径相关环境问题。另有 UnityConnect 请求超时。因此只声明 C# 编译与 EditMode 通过，不声明整个 Editor 导入/Console 无错误，不将此作为视觉或构建验收。
- 资产保护：1039 个已展开 LFS 文件 SHA256 与指针一致；另两份 PDF 保持原始指针。异常 Git 状态源于本轮首次受限操作留下的隔离 index.lock，确认无活动 Git 后仅清理该锁，按已验证路径刷新，暂存差异为 0。Unity 自动删除的两份孤立 `.sln.meta`/`.csproj.meta` 已按基线恢复，未删除对应用户旧 worktree 文件；无场景、prefab、视觉资源或主目录设置变更。
- F2：16 个存续两行 meta 补全并经本次 Unity 导入，GUID 保持；第 17 个随纯静态 BuddahModifierTickClock 合并删除，不生成新的替代 GUID。

## 待验证

- N10 双端 0/100ms 开赛阶段时长、开赛后第一秒位置校正量及 R7 各轴 divergence；没有把 helper 测试当作该双端回归。
- N6 高速行驶人工视觉验收；当前世界起点设计保持。
- 完整 R6，尤其 P1-8 的范围和通用碰撞等价性；既有 N8/N9 专项不能替代。
- 外部 Steam、跨机 Dev 双端、实际驾驶三圈。
- 可比较的 dev R8 GC.Alloc 基线及同负载 0/100ms 双端性能对照。

## 已有 PR 的归属方案（尚未发布）

保持 `#48 → #47 → #49 → #50 → #51 → #52 → #53 → #54 → #55 → #56 → #57 → #58`，底层 dev；本地汇总验证不等于更新远端。发布前向主会话报告验证结果，再按以下文件/片段分配并顺序处理依赖，不强推、不新开 PR。

| PR | 应归入的修复 |
|---|---|
| #48 | networking.md 的 SceneCondition/global/非 BufferLast RPC 规则。 |
| #47 | p0-validation 的日志删除影响与 `git show 9f12e5f^:<path>` 恢复说明，以及 SERGATE 守卫限制；PR 描述仍待发布。 |
| #49 | 删除遗留 ProBuilder Settings.json；无调用的 PlayVfxAllObserversRpc 只记录，保留协议声明。 |
| #50 | baseline/p2-validation 的范围与默认配置收益更正；F3 不执行。 |
| #52 | BuddahHandControl 两个无调用 burst API 删除、空 skillId warning，以及 N6 视觉取舍文档。 |
| #53 | SerializedObject using、SkillExecutor 空行、P3 OnEnable 文档、5 个 importer meta。 |
| #54/#57 | modifier helper 合并在 #57 落地（依赖 #56 已有 BuddahTickMath），删除旧 helper/meta并更新 modifier-clock-cases；不在较早 #54 提前引入对后续提交的依赖。 |
| #55 | Handshake 私有集合/语义 API、RSM 与诊断调用方、RoomRoster 命名、4 个 importer meta、session-helper-cases。 |
| #56 | BuddahHandControl 的 NaN 比较注释片段、7 个 importer meta；22 条 helper 已在 #58 测试覆盖。 |
| #57 | N10 reconcile 工作副本转换、统一时钟 helper及 owner/observer 时钟边界。 |
| #58 | 新 N10 EditMode、Handshake 测试适配、identity 反射精确签名/断言、测试宏约束、Runtime 无用引用删除、六个 UnityEvent 文档更新、汇总 progress。 |

不得直接将整个本地汇总提交 cherry-pick 到每个 PR；应按表中的文件/片段归属更新，之后重新验证最终堆叠。

## 本地交付

- `bcdbedc`：N10 时钟修复、旧 modifier helper 合并及新增 17 个阶段/边界测试。
- `81d5fc1`：其余审查整理、16 个 GUID 不变的 importer meta、Handshake/identity 测试适配及程序集配置。
- `docs: record review fixes and validation`：本文件及 F4/F5/F7、progress 更新。

这三个提交仅保存到隔离分支，不代表已按上述归属表发布到各 PR。F3 历史保持，D1/视觉设计/P7 不变。
