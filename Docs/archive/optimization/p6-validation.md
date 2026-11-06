# P6 程序集与 EditMode 测试

用户已授权 D6，P7 继续等待美术确认。本阶段新增 FishyFacepunch、BuddahGo.Runtime、BuddahGo.Editor 和 Editor-only BuddahGo.Tests；不拆可选 UI 程序集，保留既有 Session 到 SceneFadeController 的调用。

## 预检

- FishyFacepunch 的 9 个脚本当前在 Assembly-CSharp-firstpass。它们直接依赖 FishNet.Runtime 与平台匹配的 Facepunch Steamworks 预编译 DLL。新 asmdef 使用 FishNet.Runtime 显式引用，DLL 沿用 PluginImporter 的自动引用/平台选择，不硬编码 Windows DLL 名称。
- Feel 已通过 asmref 归入 MoreMountains.Tools，运行时 MMF_Player 的实际程序集也为 MoreMountains.Tools。无需重复建立 MMFeedbacks asmdef 或迁移第三方目录。
- Runtime 覆盖 Assets/Scripts；直接依赖逐一核对现有 asmdef/实际程序集名称，包括 FishNet、FishyFacepunch、InputSystem、Cinemachine、Splines、TMP、VFX、MoreMountains.Tools、Mathematics、Timeline、URP/Core 和 UnityEngine.UI。InputSystem_Actions 生成代码位于 Scripts 下。
- Editor 仅覆盖 Assets/Scripts/Editor。Assets/Editor 保持默认 Editor 程序集；自动引用项目运行时 asmdef。
- 全 Assets 序列化文本扫描确认 6 处游戏 UnityEvent 程序集限定名：MainMenuUI 三处，RaceFinishManager 一处，ResultPresentationTimelineBridge 两处。仅迁移这六处；StarterAssets 的默认程序集事件/预制体 override 保留。
- 五个 EditMode 文件覆盖 LoadoutRules（输入校验/部分规范化/顺序/重复策略）、RaceStartHandshake（重置矩阵/退出/序列门控）、ProjectileBurstPlanner 与 timing（居中/间距/大小/冷却）、TickMath（Ceil 浮点边界/时钟偏移/饱和）、PlayerIdentity（无 Steam 会话的回退与空连接）。测试通过 friend assembly 访问 internal helper，不将其改为 public。

以下为实际编译、构建与运行结果。

P6-1：FishyFacepunch 独立 asmdef 导入后主 Editor 编译通过，实际程序集名为 FishyFacepunch；MMF_Player 仍为 MoreMountains.Tools。第三方源码及 DLL 导入设置未改。

P6-2：主 Editor 完整编译通过，实际类型程序集确认 Runtime / Editor / FishyFacepunch。首次刷新被“场景外部修改”重载提示阻挡，加载磁盘上迁移后的 MainMenu 后恢复。场景 diff 仅六处程序集限定名；既有 C#、脚本 GUID、网络协议和字段布局未改。克隆端随后连同测试程序集刷新，实际程序集名称一致。

P6-4：两端实际发现并运行 43 项 EditMode 测试，全通过、0 failed / 0 skipped（主 1.459s，clone 0.146s）。测试程序集仅 Editor，autoReferenced=false；生产 helper 保持 internal。首次导入发现 TestAssemblies 与显式 TestRunner 引用重复，去掉后又发现纯测试对平台 Steam DLL 的间接签名依赖；PlayerIdentity 的 nullable Lobby 调用改用反射，从而无需固定 Windows DLL 名称。初次误启动的零测试结果未计为通过。并发导入期间出现一次源文件时间戳过期，刷新后编译及实际测试均完成。


## 构建与包内容

| 构建 | 结果 | 时间 / 大小 | 启动 |
|---|---|---|---|
| ArchitectureP6Release | 0 error / 15 warning | 37.325s / 1134.46 MB | 实际到主菜单，Create/Browse 面板可打开 |
| ArchitectureP6Development | 0 error / 12 warning | 23.646s / 1162.37 MB | 实际到主菜单 |

Release 的额外资源提示来自原有 SG_BlackCurtain pow Shader warning 和 Feel MMDefaultPostProcessingProfile/Volume 缺失脚本；早期日志已有同类提示，资源未改。C# 警告仍来自既有 PredictionSmoother obsolete、unreachable 和 unused field。两种 Player.log 均无 Exception/MissingReference；正式包无开发探针输出。Development 启动弹出 Windows 防火墙权限提示，未授予额外权限；只终止本次启动且路径已核实的测试进程，未操作安全设置。

直接检查两个 Player 的 BuddahGo.Runtime.dll：Release 的 GameLog.Verbose 调用数为 0，Perf/VisualShake probe 类型为 0；Development 保留 209 个调用和两个探针。两包均无 BuddahGo.Tests.dll，也无项目 Editor 程序集。

## 最终本机双端回归

2026-09-30 08:18:54 UTC 完成。主/clone 均使用新程序集，同源 Tugboat 本机连接。

- R1：两端编译通过；43/43 EditMode 测试各通过一次，0 failed / 0 skipped。
- R10：两端实际调用主菜单三处持久 Button.onClick，CreateRoom/JoinById/BrowseRooms 面板及 activeSelf 正确，目标类型程序集均为 BuddahGo.Runtime。正式包也实际打开 Create/Browse 面板。
- 进入房间、ready、全员选择、RaceMap、开场和控制权交接通过；每端施放六种技能，总计 server CAST 12、每端 observer 12，确认 FishNet 织入后的 RPC 到达。
- 第一轮先普通 95s，再 100ms 模拟延迟 100s；336/166 条 D-LOC 心跳全部 loc/tel/mod/hof div=0，无 FATAL、SceneId 或采集到的游戏 Error/Exception。
- 两轮均通过实际 EndMatch UnityEvent 结束；两处 Timeline reaction 在两端、两轮各实际触发，随后重开和 client 投票回房间成功。
- 第二轮故意不提交 client 配装；真实 60s 计时器到期自动补齐 acceleration/slowtrap/blackcurtain，host 已提交的三项保留，两端解析 cache 相同。
- 回房间后 host ready=true、client ready=false，go/movement/waiting/countdown 都为 false。host cache=0、client cache=2，保持 P4 记录的既有清理语义。
- 场景全文归一化换行后逐字比较，仅六处预定 UnityEvent 程序集名变化；没有其他场景值或脚本 GUID 变化。新程序集/测试之外的生产 C# 方法体无 P6 改动。

日志为 Logs/p6-regression-host.jsonl 与 clone 的 Logs/p6-regression-client.jsonl；菜单启动日志保存在 Logs/p6-release-menu.log / p6-development-menu.log。Console 中有开始前即存在的 SceneFadeController Animator 诊断和编译期 Timings 信息，本轮运行采集未出现它们；未将历史 Console 等同为零错误。

R4/R9 的完整 Dev Build + Steam 外部端、真实跑完三圈，以及通用碰撞响应等价性未执行；R8 缺少同条件可比较基线，不能声称性能收益已量化。P7 按用户要求暂缓，未删除美术资源或改写历史。
