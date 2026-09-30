# P6 程序集与 EditMode 测试

用户已授权 D6，P7 继续等待美术确认。本阶段新增 FishyFacepunch、BuddahGo.Runtime、BuddahGo.Editor 和 Editor-only BuddahGo.Tests；不拆可选 UI 程序集，保留既有 Session 到 SceneFadeController 的调用。

## 预检

- FishyFacepunch 的 9 个脚本当前在 Assembly-CSharp-firstpass。它们直接依赖 FishNet.Runtime 与平台匹配的 Facepunch Steamworks 预编译 DLL。新 asmdef 使用 FishNet.Runtime 显式引用，DLL 沿用 PluginImporter 的自动引用/平台选择，不硬编码 Windows DLL 名称。
- Feel 已通过 asmref 归入 MoreMountains.Tools，运行时 MMF_Player 的实际程序集也为 MoreMountains.Tools。无需重复建立 MMFeedbacks asmdef 或迁移第三方目录。
- Runtime 覆盖 Assets/Scripts；直接依赖逐一核对现有 asmdef/实际程序集名称，包括 FishNet、FishyFacepunch、InputSystem、Cinemachine、Splines、TMP、VFX、MoreMountains.Tools、Mathematics、Timeline、URP/Core 和 UnityEngine.UI。InputSystem_Actions 生成代码位于 Scripts 下。
- Editor 仅覆盖 Assets/Scripts/Editor。Assets/Editor 保持默认 Editor 程序集；自动引用项目运行时 asmdef。
- 全 Assets 序列化文本扫描确认 6 处游戏 UnityEvent 程序集限定名：MainMenuUI 三处，RaceFinishManager 一处，ResultPresentationTimelineBridge 两处。仅迁移这六处；StarterAssets 的默认程序集事件/预制体 override 保留。
- 五个 EditMode 文件覆盖 LoadoutRules（输入校验/部分规范化/顺序/重复策略）、RaceStartHandshake（重置矩阵/退出/序列门控）、ProjectileBurstPlanner 与 timing（居中/间距/大小/冷却）、TickMath（Ceil 浮点边界/时钟偏移/饱和）、PlayerIdentity（无 Steam 会话的回退与空连接）。测试通过 friend assembly 访问 internal helper，不将其改为 public。

实现、编译、构建、UnityEvent 与运行结果待填写。

P6-1：FishyFacepunch 独立 asmdef 导入后主 Editor 编译通过，实际程序集名为 FishyFacepunch；MMF_Player 仍为 MoreMountains.Tools。第三方源码及 DLL 导入设置未改。

P6-2：主 Editor 完整编译通过，实际类型程序集确认 Runtime / Editor / FishyFacepunch。首次刷新被“场景外部修改”重载提示阻挡，加载磁盘上迁移后的 MainMenu 后恢复。场景 diff 仅六处程序集限定名；既有 C#、脚本 GUID、网络协议和字段布局未改。克隆端将连同测试程序集集中刷新。

P6-4：两端实际发现并运行 43 项 EditMode 测试，全通过、0 failed / 0 skipped（主 1.459s，clone 0.146s）。测试程序集仅 Editor，autoReferenced=false；生产 helper 保持 internal。首次导入发现 TestAssemblies 与显式 TestRunner 引用重复，去掉后又发现纯测试对平台 Steam DLL 的间接签名依赖；PlayerIdentity 的 nullable Lobby 调用改用反射，从而无需固定 Windows DLL 名称。初次误启动的零测试结果未计为通过。并发导入期间出现一次源文件时间戳过期，刷新后编译及实际测试均完成。
