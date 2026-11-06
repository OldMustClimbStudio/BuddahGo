# PR #64 代码审查（RaceMap 场景渲染里程碑）

- PR：[OldMustClimbStudio/BuddahGo#64](https://github.com/OldMustClimbStudio/BuddahGo/pull/64)，`refactor/vfx-optimization` → `dev`。2026-10-28 分支改名为 `feat/racemap-rendering`，#64 因此被 GitHub 关闭，由 [#65](https://github.com/OldMustClimbStudio/BuddahGo/pull/65) 接替，内容相同
- 审查的提交：`bf35537`（本地与 `origin` 一致），对比 merge-base `5f91e51`（`origin/dev`），共 220 个文件
- 日期：2026-10-28
- 方法：只做静态审查，没有启动 Unity 或 MCP，没有运行构建或测试。6 个维度各有一名审查者，每个维度再由一名独立复核者逐条尝试推翻。被推翻的条目已删除，重复条目已合并。需要编辑器或构建才能确认的条目标为“未经编辑器验证”。
- 置信度：**高**＝复核者在代码里确认过；**中**＝代码路径成立，但影响取决于运行时；**低**＝有可能，需要编辑器、构建或联机试玩才能确认。

## 处理状态（2026-10-28，合并前）

| 条目 | 状态 | 提交 |
|---|---|---|
| M1 应用设置重建渲染管线 | 已修复：比较只读字段，不再创建或销毁管线资产；数值相同的副本直接沿用（编辑器实测修复前会释放当前管线） | `3f3bc29` |
| M2 出生点 y 3.3 | 已修复：`Match.xlsx` 改为 0.5478，新增测试保证配表、场景出生点和出发点一致；README 已改 | `5e20255`、`20f857b` |
| N8 结算台锚点 | 已修复：随碰撞体下移 0.998，4.4 → 3.401926 | `5e20255` |
| M6 SSR 与 Deferred 保活 renderer | 用户决定移除：SSR feature、RaceMap 的 SSR 组件和保活 renderer 已删，插件目录保留并写了加回步骤；变体 7609 → 6402 | `60fddd0`、`287c593`、`e572ccd` |
| N10、N13–N15 过时文档与注释 | 已修正 | `b3bd205` |
| N11 PR 描述 | 已修正（PR 描述，无提交） | — |
| N12 分支名与标题类型 | 分支改名为 `feat/racemap-rendering`，与 `feat:` 一致；`revert:` 前缀在 PR 描述里说明，历史未改写 | — |
| M3–M5、M7–M11，N1–N7、N9 | 未处理，保持开放 | — |

## 结论

| 严重度 | 数量 |
|---|---|
| Blocker | 0 |
| Major | 0 |
| Minor | 11 |
| Nit | 15 |

没有必须阻塞合并的问题。下面三条代价低、影响真实，建议合并前处理：

1. **M1** 每次应用画质设置都会销毁并重建当前渲染管线。
2. **M2** RaceMap 出生点的场景改动在运行时不生效，因为设计表仍把出生点放在 y 3.3。这与 README 的说法矛盾。
3. **M6** SSR 和 Deferred 保活渲染器目前没有任何内容在使用，却让每个构建多出几百个变体。需要明确决定保留还是移除，并写进文档。

---

## Minor

### M1 `GraphicsPipelineMapper.DiffersFrom` 每次 Apply 都会拆掉当前渲染管线
- **位置**：`Assets/Scripts/Rendering/Settings/GraphicsPipelineMapper.cs:47-56`；调用方 `GraphicsSettingsApplier.cs:147`
- **场景**：启动时（默认预设）以及 F9 面板的每次点击都会调用 `DiffersFrom`。它先 `Instantiate` 当前 URP 资产作为探针，再 `DestroyImmediate`。Unity 2022.3 的 `RenderPipelineAsset.OnDisable` 不检查被销毁的是不是当前资产，会无条件调用 `RenderPipelineManager.CleanupRenderPipeline()`。复核者反汇编了 2022.3.55f1c1 的 CoreModule.dll 来确认这一点。结果是正在使用的 `UniversalRenderPipeline` 被 Dispose：SSR 的 RTHandle、TAA 历史和相机附加数据全部丢掉，下一帧整体重建。即使设置没有任何变化也会这样，表现为一次卡顿加时域效果重置。资源不会损坏。
- **置信度**：高（引擎 IL 已确认；卡顿幅度没有用 Profiler 测）
- **建议修复**：比较时不要创建或销毁 URP 资产。直接用 levels 算出目标值，再和原资产字段比较；或者保留一个常驻的 DontSave 探针，每次比较前把原资产字段拷进去。

### M2 RaceMap 场景里移动 SpawnPoints 无效，Match/TrackSpawn 表仍把选手放在 y 3.3
- **位置**：`Assets/Design/Tables/_Export/Match/TrackSpawn.csv:5-10`（源头 `Match.xlsx`）；`Assets/Scripts/Network/Match/MatchSpawnManager.cs:41`；`Docs/rendering/racemap/README.md:579-583`
- **场景**：`MatchSpawnManager.Awake` 用表里的世界坐标覆盖每个 SpawnP。racemap 的 6 行都是 y 3.3，这个 PR 没有改。场景里 SpawnPoints 下移 2.7522（5986813/e56b0a4）因此在运行时被覆盖，README 说的“出生点位于静止高度 0.5478”并不成立。选手在 y 3.3 生成，胶囊底部约 3.0，车道顶面 0.25。进入开场状态变成 kinematic 之前，选手是动态刚体，最多会下落约 2.75 m。开场样条会接管位置，所以正常比赛里只影响生成到开场接管之间的那段时间。`ForceExitIntroState` 不会恢复出生姿态。服务器决定位置并随 spawn 同步，所以不会不同步。EditMode 检查读的是序列化场景而不是表，因此没有发现这个问题。
- **置信度**：高（静态确认；下落是否可见需要试玩）
- **建议修复**：在 `Match.xlsx` 里把 racemap 6 行的 y 改为 0.5478 并重新导出（CSV、JSON、asset），同步更新设计表文档。也可以撤回场景偏移并修正 README。建议加一个 EditMode 测试，比较表里的出生点 y 和场景布局高度。

### M3 关闭反射后，平面反射的 RenderTexture 不会释放
- **位置**：`Assets/Scripts/Rendering/PlanarReflection.cs:90-94`（释放只发生在 `OnDisable` 第 71-76 行和尺寸变化时第 123-129 行）
- **场景**：在 4K、反射为 high 时，玩家通过 F9 把反射调成 off（或切到 low 预设）想省显存。`ShouldReflect` 返回 false 后代码只执行 `Bind(null)`。RaceMap 里有两个组件，各自的 HDR RT（带 mip 和 24 位深度，每个约 120 MB）和隐藏镜像相机一直保留到场景卸载。跨场景不会泄漏。
- **置信度**：高
- **建议修复**：`Scale <= 0` 时先释放并销毁 `_texture`（可选同时销毁 `_mirror`），再 `Bind(null)`。把 OnDisable 里的清理提成一个 `ReleaseResources()`。

### M4 `BuddahGoLitShaderGUI` 在任何属性改动后都会重写渲染队列
- **位置**：`Assets/Editor/Rendering/BuddahGoLitShaderGUI.cs:73`（OnGUI 第 33-42 行）
- **场景**：美术在 BuddahGo/Lit 材质的 Render Queue 输入 2010。`EndChangeCheck` 触发 `UpdateAlphaClipState`，队列被无条件写回 -1 或 2450，输入框立刻弹回原值。之后任何属性改动都会冲掉手动或脚本设置的队列。`_QueueOffset` 从不被读取，所以从 URP Lit 切换过来时队列偏移会丢失。目前三个材质都是 -1，已提交的资产不受影响。
- **置信度**：高
- **建议修复**：在 `base.OnGUI` 前后记录 `_AlphaClip`，只有它变化时才更新。队列按 URP `BaseShaderGUI` 的做法计算：`(clip ? AlphaTest : Geometry) + _QueueOffset`。

### M5 `RaceMapTemporalNoise.Apply` 中途解析出错会丢掉撤销列表
- **位置**：`Assets/Editor/Rendering/RaceMapTemporalNoise.cs:557-741`（调用方 `MeasureCost` 第 363 行、`FreezeCompare` 第 420 行、`Tick` 第 148 行）
- **场景**：执行 `MeasureCost("a=lit:urp,fog:off,ssao:off,quality:2,taaq:x", ...)`。前几个 token 已经替换材质、关闭雾、对**真实**渲染器资产执行 SSAO `SetActive(false)` 并切换画质等级，之后 `int.Parse("x")` 抛出异常。`Restore` 还没返回给调用方，所以这些改动都不会撤销。下次保存场景时，材质替换和雾关闭会写进 `RaceMap.unity`。SSAO 的 `m_Active=false` 和画质等级会在下次保存对应资产或 ProjectSettings 时落盘。这只是开发诊断工具，仓库里没有代码调用它。
- **置信度**：高
- **建议修复**：由调用方创建 `Restore` 并传入 `Apply`，在 finally 里撤销；或者先解析并校验全部 token，再开始修改。

### M6 Deferred 保活渲染器和 SSR feature 让每个构建多出变体，但服务的功能处处关闭
- **位置**：`Assets/Settings/URP-HighFidelity.asset:21`、`URP-Balanced.asset:21`（列出 `URP-KeepDeferredVariants-Renderer`）；HighFidelity 和 Balanced 渲染器上的 SSR feature `m_Active: 1`
- **场景**：任何构建中，URP 只要在渲染器列表里看到 Deferred 就会保留 GBuffer 变体。作者的探针场景里变体从 1945 增加到 2576（+631），拉长着色器编译时间并增大包体。唯一带 SSR 的体积组件 `RaceMap_Environment_Profile` 设为 Disabled，`GraphicsOption` 也没有 SSR 选项，所以发布内容里没有任何路径能打开 SSR。另外，SSR 的 `AddRenderPasses` 每帧还会为每个相机执行，并做一次枚举装箱。README 第 26 行、第 724-725 行把它列为已知开放项。
- **置信度**：中（变体数字引自作者报告，没有重新构建）
- **建议修复**：合并前决定 SSR 的去留。如果不保留，从两个资产的列表里移除 Deferred 保活渲染器（PlanarReflection 按名字查找渲染器，不受索引变化影响），并移除 SSR feature 和 profile 里的组件；插件目录可以留着。如果保留，把这个决定写进 README 的“RaceMap 之外的改动”表。

### M7 F9 面板的点击和按键也会传到下方的菜单和游戏
- **位置**：`Assets/Scripts/Rendering/Settings/GraphicsSettingsDebugPanel.cs:112`（只在第 70 行对 F9 调用 `e.Use()`）
- **场景**：在菜单场景打开面板，点击叠在 uGUI 按钮上的“>”。设置改了，下方按钮也被触发（`<Mouse>/leftButton` 绑定了 Menu/MenuSelect）。比赛中面板打开时，键盘驾驶仍然有效。只影响测试人员。
- **置信度**：中（未经编辑器验证：需要确认某个场景的按钮确实位于面板下方）
- **建议修复**：面板打开时禁用 Player 和 Menu action map（或 EventSystem），或者在面板后面垫一个全屏、拦截射线的 uGUI Image。

### M8 默认开启 VSync，可能重新引出 FrameRateLock 要防的帧率与 tick 拍频抖动
- **位置**：`Assets/Scripts/Rendering/Settings/GraphicsSettingsApplier.cs:100`；`GraphicsOptionLevel.csv:50`
- **场景**：144 Hz 显示器、没有存档设置的玩家联机时，VSync 默认开启，帧率跟随 144 Hz，每个 60 Hz tick 对应 2.4 帧。如果 FrameRateLock 的前提成立，远端和预测选手的视觉平滑会出现规律的微抖。模拟按 tick 进行，所以不会不同步。这是有记录的取舍，`graphics-settings.md:148` 已列为待验证项。
- **置信度**：低（需要 75/144/165 Hz 显示器上的双端联机试玩）
- **建议修复**：先完成文档里的联机检查。如果确实抖动，只在刷新率是 tick 整数倍时默认开 VSync，否则默认关闭并限帧 120。

### M9 Play 模式中保存项目，可能把 DontSave 管线克隆写进 QualitySettings.asset
- **位置**：`Assets/Scripts/Rendering/Settings/GraphicsSettingsApplier.cs:158`（克隆来自 `GraphicsPipelineMapper.cs:141-145`）
- **场景**：编辑器里存在自定义画质设置时，Play 期间当前画质等级的 `renderPipeline` 指向一个 DontSave 克隆，vSync 和 mip 也被改过。如果在退出 Play 前执行 File > Save Project（或某个工具、MCP 调用了 `SaveAssets`），磁盘上的 `QualitySettings.asset` 可能出现 `customRenderPipeline: {fileID: 0}` 和被改的值，很容易被一起提交。恢复逻辑只修内存里的状态。
- **置信度**：低（未经编辑器验证：没有确认脚本对 QualitySettings 的写入会不会把资产标脏）
- **建议修复**：在 `UNITY_EDITOR` 下挂 `AssetModificationProcessor.OnWillSaveAssets`，保存期间临时恢复原值。至少要在 `graphics-settings.md` 里写明 Play 中保存项目不安全。

### M10 TemporalNoise 扫描期间重新编译，会把探针对象和资产开关遗留在编辑器里
- **位置**：`Assets/Editor/Rendering/RaceMapTemporalNoise.cs:104`（`s_Sweep` 是普通静态字段，没有 `AssemblyReloadEvents` 钩子）
- **场景**：Play 模式扫描中，脚本改动触发 Recompile and Continue Playing。`s_Sweep` 和订阅丢失，`End()` 不再执行。游戏相机保持禁用，DontSave 探针相机、代理对象和优先级 1000 的 SSR 体积可能留到编辑器重启，资产上的 SSAO/SSR 开关停留在内存里，之后可能被保存。
- **置信度**：低（未经编辑器验证：DontSave 对象能否在退出 Play 后存活，以及用户的重编译偏好设置）
- **建议修复**：在 `AssemblyReloadEvents.beforeAssemblyReload` 里调用 `End("aborted: domain reload")`；资产级开关的原值存进 `SessionState`。

### M11 两个 PlanarReflection 只做视锥 AABB 判断，没有距离或屏幕占比判断
- **位置**：`Assets/Scripts/Rendering/PlanarReflection.cs:99-115`
- **场景**：High 预设（planar 100%）下，只要终点场地地板的包围盒落在远裁剪面为 5000 m 的视锥里，即使被遮挡或在屏幕上很小，也会多渲染一次整个场景。两个组件同时满足时，一帧多两次场景绘制（按作者的测量约 2 × 0.65 ms），两张全分辨率 RT 也一直驻留。
- **置信度**：低（未经编辑器验证：实际一圈里终点地板在视锥内的频率需要 Profiler 测）
- **建议修复**：先用 Profiler 在终点区进入视锥时测量。如果代价明显，在 `RenderMirror` 前加最大距离或最小屏幕占比判断，或者每个相机每帧只渲染最相关的一个平面。

---

## Nit

| # | 位置 | 问题 | 置信度 | 建议 |
|---|---|---|---|---|
| N1 | `PlanarReflection.cs:105`；`ScreenSpaceReflectionURP.cs:168` | 每帧每组件 `CalculateFrustumPlanes` 新建 `Plane[6]`，SSR 的 `FieldInfo.GetValue` 枚举装箱，约 250-300 B/帧，开了增量 GC | 高 | 用静态 `Plane[6]` 和非分配重载；SSR 先判断 isActive 再返回 |
| N2 | `GraphicsSettingsService.cs:38`；`GraphicsSettingsDebugPanel.cs:64` | F9 面板装进每个构建（文档说明这是有意的临时方案），面板关闭时 OnGUI 仍每帧跑 Layout/Repaint | 中 | 设置菜单上线时加 `Debug.isDebugBuild`/宏开关；可选：面板关闭时禁用组件 |
| N3 | `GraphicsSettingsApplier.cs:123-127` | 窗口模式加原生尺寸时，如果系统裁剪了客户区，每次 Apply 都会重新 `SetResolution`，窗口跳动 | 低（未经编辑器验证） | 缓存上次应用的模式和尺寸，只在变化时调用 |
| N4 | `GraphicsSettingsApplier.cs:84` | `FrameRateLock` 先把 vSyncCount 写成 0，Applier 把 0 记成原值，退出 Play 后编辑器的画质等级停在 VSync 0（问题本身早于这个 PR） | 中 | FrameRateLock 记录被覆盖前的值，或者去掉它对 vSync 的写入 |
| N5 | `RaceMapTemporalNoise.cs:504, 722` | 每次运行泄漏一个 DontSave 代理 Mesh 和 nrm 材质克隆（仅诊断工具） | 高 | 销毁 GameObject 时一并销毁 sharedMesh；恢复后销毁克隆 |
| N6 | `BuddahGoLighting.hlsl:58` | 材质同时开 `_PlanarReflection` 和关 `_EnvironmentReflections` 时仍显示镜像，目前没有材质是这种组合 | 低 | 加 `!defined(_ENVIRONMENTREFLECTIONS_OFF)` 判断，或在 tooltip 里说明 |
| N7 | `BuddahGoLighting.hlsl:37` | 法线扰动按世界 XZ 加到屏幕 UV，方向随镜头朝向变化。这是常见近似，0.04 强度下几乎看不出 | 低（未经编辑器验证） | 可选：先转到视空间；或在注释里说明这是近似 |
| N8 | `Assets/Design/Tables/_Export/Match/TrackResultAnchor.csv:5` | 碰撞体调整后终点结算锚点（y 4.4）没有下调，落差从 1.3 m 变成 2.3 m（纯表现） | 中 | 在 `Match.xlsx` 里下调 0.998 后重新导出，或在 README 里注明接受 |
| N9 | `MovementSlowTrapZoneEffect.cs:135`；`BuddahHandControl.cs:695` | 以角色原点定位的判定体（减速陷阱、推击盒）相对其他选手胶囊低了 0.998 m，减速陷阱覆盖约 11.09→10.81 m。README 说“行为不变”，但没有提到这一点。服务器判定，不会不同步 | 中 | 在 README 碰撞体调整一节补充覆盖范围变化；如需保持原覆盖，偏移 +0.998 |
| N10 | `Assets/Plugins/UnitySSReflectionURP/BuddahGo_NOTES.md:20`；`GraphicsSettingsService.cs:79`；`Docs/README.md:20` | 仍写着 RaceMap 使用 SSR、SSR 依赖 feature，实际上 profile 已 Disabled，只有 SSAO 依赖 feature | 高 | 更新 “Used by” 段落和注释 |
| N11 | PR #64 描述 | 文本行数写 44（实际 58）；“后续”仍列平面反射（已交付）；“未验证”仍列 SSR 观感；成本表列名仍是屏幕空间反射 | 高 | 编辑 PR 描述 |
| N12 | `CONTRIBUTING.md:6` | 分支类型 `refactor/` 与标题 `feat:` 不一致；21a7bd8 使用了列表外的 `revert:` | 高 | 在 PR 描述里说明即可，不要重写已推送的历史 |
| N13 | `README.md:16` | 仍写“六个 Excel 表”，加上 Graphics 应为七个，与 `Docs/design-tables/README.md:10` 矛盾 | 高 | 改为七个并加上 Graphics |
| N14 | `Docs/design-tables/excel-spec.md:47` | 写着未实现的选项和预设都用 `##` 屏蔽，实际只有 ultra 预设行被屏蔽 | 高 | 改为“未实现的预设（目前只有 ultra）用 `##` 屏蔽” |
| N15 | `harness.md:18` | “renderer index 1 保留给 Deferred 保活渲染器”没有限定管线，Performant 的 index 1 是平面反射渲染器 | 中 | 限定为 High Fidelity 和 Balanced，并注明平面反射渲染器按名字查找 |

---

## 复核中被推翻的条目

- **平面反射的渲染缩放被应用两次**（`PlanarReflection.cs:121`）：离屏镜像相机没有后处理、不需要 opaque texture、不开 MSAA、使用全视口，所用渲染器也没有 feature，URP 不会创建中间目标。RT 只按预期缩放一次。
- **`URP-PlanarReflection-Renderer` 是旧序列化格式，默认 Intermediate Texture Always**：URP 14 只在 `rendererFeatures.Count != 0` 时才强制中间纹理，而这个渲染器没有 feature，所以没有额外的 blit。可选：在编辑器里重新保存一次，避免以后出现 diff 噪声。

## 检查过且没有发现问题的部分

- **设置模型与持久化**：用字符串 id 存储（不会因枚举下标漂移出错）；未知的预设、选项或等级在加载时丢弃；坏 JSON 被捕获；PlayerPrefs key 带版本号；`##` 行不会进入运行时；Apply 顺序（先切画质等级，再记录原值，再写入）正确；克隆的生命周期和退出 Play 后的恢复（`Application.quitting` 加 `SessionState`）完整；Enter Play Mode Options 已关闭。
- **运行时资产修改**：运行时代码只写 `Instantiate` 出的 DontSave 克隆，没有写真实的 URP 资产、sharedMaterial 或 Volume profile。SSR 补丁改用运行时材质副本，`ScreenSpaceReflection.mat` 不会再被写盘。EditMode 测试断言原资产不会被标脏。
- **SSR 补丁**：Create/Dispose 和 FrameCleanup 的空引用补丁正确；跳过 Reflection/Preview 相机和 OpenGL；镜像相机走无 feature 的渲染器，不会触发 SSR，也不会递归。
- **着色器**：BuddahGo/Lit 的 pass 集合、LightMode 和关键字列表与 URP 14.0.11 Lit 逐行一致；所有 pass 的 UnityPerMaterial CBUFFER 相同，SRP Batcher 兼容；ShadowCaster、DepthOnly、DepthNormals、GBuffer、Meta 齐全；SunsetSky 的 tag 和 NaN 防护正确；材质属性和关键字与着色器对应；`SG_MapEdge.shadergraph` 结构完整，没有悬空引用。
- **联机安全**：碰撞体中心 +0.998074 在 xlsx、CSV、JSON、asset 和 prefab 之间一致，RollbackHurtbox 同步移动；`BuddahPredictedMotorConfig` 在服务器、owner、观察者和 AI 上都从表里写入；质心随胶囊移动，同步的位置仍是原点；重生高度相对车道与 dev 相同；检查点和起点判定余量足够；画质代码不碰 `fixedDeltaTime`、Physics、TimeManager 或 NetworkObject；RaceMap 场景没有改 NetworkObject、sceneId、层或触发器。
- **构建与性能**：三个画质等级的资产引用正确；场景和 prefab 里没有相机使用 index 1；三个管线资产都有平面反射渲染器；Performant 渲染器没有变化；镜像 RT 不带 MSAA；运行时代码不引用未加保护的 UnityEditor；Resources 下的 JSON 只增加了约 56 KB。
- **仓库规则与泄漏**：diff 里没有 `Assets/Showcase/` 或 GPL 内容，也没有引用 Showcase 的 GUID；三个无关的工作区文件不在 PR 里；新资产都带 `.meta`，没有孤立的 meta；81 个二进制文件都走 LFS；SSR 插件带 MIT LICENSE 和上游 URL/commit，补丁有 `[BuddahGo]` 标记；Graphics、Movement、Respawn、UI 和 Global 的 xlsx、CSV、JSON、asset 与 Generated.cs 互相一致，`export-map.json` 和 `scope.json` 符合约定；文档里的所有相对链接在 bf35537 都能解析；`Tools/rendering/*.py` 没有绝对路径或密钥。

## 未能验证的部分

- 没有编译或构建：BuddahGo/Lit 在各平台的编译结果、实际变体数、构建中 GBuffer pass 是否真的保留、dev 与本分支的包体差异。
- 没有运行测试：作者报告的 EditMode 328/328，以及 14 个 GraphicsSettingsTests。
- 联机：VSync 在非 60 整数倍刷新率下的平滑表现；新旧版本混连（没有找到版本门槛）。
- 运行时表现：出生下落和结算下落是否可见、平面反射在 Balanced 上的代价、结算典礼时的终点反射、F9 点击穿透的具体场景。
- 编辑器行为：QualitySettings 的运行时写入会不会标脏并落盘；DontSave 对象能否在退出 Play 后存活；Render Queue 输入框在 URP 下是否可见（复核者根据 IL 判断是可见的）。
- SSR 插件除 `ScreenSpaceReflectionURP.cs` 以外的文件是否与上游 555d946 逐字节一致（审查时没有联网）。
- 81 张证据 JPEG 的画面内容（只检查了文件类型和 LFS 状态）。
- 生成资产确实出自导入器而不是手改（内容互相一致，但无法静态证明来源）。
