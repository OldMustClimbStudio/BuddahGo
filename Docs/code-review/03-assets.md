# 03 资产盘点（仓库里该有什么、不该有什么）

审查对象：`refactor/vfx-refactor` @ `ddb00807`（PR #68）。只读盘点，没有删除或改动任何资产。

## 0. 方法与口径

- **体积**：`git ls-tree -r -l HEAD` 取每个 blob 的大小；blob 是 LFS 指针的，解析指针里的 `size`（不看磁盘，这个 worktree 的 LFS 没 pull）。同一个 LFS 对象被多个路径引用时，"去重"列按 oid 只算一次（`git lfs ls-files -l -s`）。
- **引用**：从全部被跟踪的 `.meta` 建 guid→路径表（4558 个），扫描所有文本序列化资产和 `.meta`（`.unity .prefab .asset .mat .controller .anim .playable .shadergraph .shadersubgraph .vfx .lighting .asmdef …`，含 shadergraph 里转义的 `\"guid\":\"…\"` 和 asmdef 的 `GUID:`）得到 1822 条引用边。运行时根 = Build Settings 的 3 个场景（MainMenu / PropertySelection / RaceMap）+ `ProjectSettings/*` + `Assets/DefaultPrefabObjects.asset` + 所有 `Resources/` 目录（非 Editor）。下文"运行时引用 0"指从这些根出发不可达。
- **已知盲区**：`LightingData.asset` 是二进制，解析不到它对光照贴图的引用，所以光照贴图的去留按"场景的 `m_LightingDataAsset` 指向谁"单独判断；按名字加载用 grep 补查（`Resources.Load` 只有 `AIDifficultyProfiles.cs:18`、`AISkillCatalog.cs:97`、`ProjectConfigRuntime.cs:84/88`，都在 `Assets/Resources/`；`AssetDatabase.LoadAssetAtPath` 只在编辑器代码；`Packages/manifest.json` 里没有 Addressables）。
- 中间结果在共享笔记目录 `code-review-scripts/`（仓库外）（`inv.json`、`edges.json`、`reach_rt.json`），可复跑。

**总量**：8894 个被跟踪文件，2311 MB。其中 LFS 1276 个文件 2201 MB（按 oid 去重 1913 MB），普通 git blob 109.5 MB。

### 与已有审计的关系

- `Docs/optimization/audit-report.md` 第 6 节"仓库与资源卫生"和 `refactor-plan.md` 的 **P7 资源瘦身**（等 D7 决策）已经列过 Oasis / Garden / Cockpit / RaceMapEndField / Sources / NiceVibrations。本文复核了这些结论并给出当前数字；下面"已登记"的条目不再当新发现。
- `Docs/design-tables/inventory-assets.md:146` 列过一批"疑似遗留 / 未引用"的小资产；`Docs/vfx/retired.md` 第 2、5、6.3 节登记过特效相关的退休资产。这些只引用，不重复。
- audit-report 第 6 节有几处**已过时**（低，确定；核实：`git log`、`git ls-files`）：`LightingData.asset` 已由 `6151cb18` 标成 `binary`；`agent-exchange/` 现在只有 68 个文件 9.7 MB（88.8 MB 的 raw 日志已由 `9f12e5fb` 移出跟踪）；`.VSCodeCounter/` 已忽略且不存在；`FishNet/Demos` 已删（`71e63d5`）。
- **和已有审计相反的一点**（中，确定）：audit-report 写 `Sources/Skin_Subs` "0 引用"、inventory-assets.md:159 写"未接线"，但 `Assets/Material/Skin/DefaultSkin.shadergraph` 引用了 `kashmir_white_granite_graph_0` 的 basecolor / metallic / normal 三张 64 MB TGA（`grep 1713465dc8fcf7d4985043717e576495` 命中该 shadergraph），而 `Assets/Character/Prefab/Buddah.prefab` 引用这个 shadergraph，`RaceMap.unity` 又引用 Buddah.prefab。按 audit-report 删 Skin_Subs 会丢角色皮肤的默认贴图。**[PR#63]**（Buddah.prefab 在 PR #63 里有改动，合并后要重查这条链）

## 1. 分类总表

结论：**该进** = 运行时或工具链需要；**不该进** = 应移出仓库（或至少移出 `Assets/`）；**存疑** = 需要用户或 PR #63 合并后决定。大小为 LFS 实际大小（非去重）。

| 类别 | 文件数 | 大小 | 结论 | 理由 |
|---|---:|---:|---|---|
| `Assets/Scripts`（含 Editor） | 658 | 2.1 MB | 该进 | 游戏代码 |
| `Assets/Tests` | 87 | 0.3 MB | 该进 | EditMode 测试 |
| `Assets/Editor` | 49 | 0.2 MB | 该进 | 编辑器工具（`ArchitectureLocalMcp.cs` 已在 info/exclude，未跟踪） |
| `Assets/Resources`、`Assets/Data`、`Assets/Design/Tables`（xlsx + `_Export` CSV） | 185 | 0.5 MB | 该进 | 配表源与导出产物；按 `Docs/design-tables/README.md` 规定一起提交 |
| `Assets/Design/Tables/_Reports/validation.md` | 1 | <0.1 MB | 存疑 **[PR#63]** | 导入器每次重写，新 worktree 导入后会变脏（memory 已记）；是证据还是生成物由用户定 |
| `Assets/Scenes/{MainMenu,PropertySelection,RaceMap}.unity` | 3 | 1.0 MB | 该进 | Build 场景 |
| `Assets/Scenes/RaceMap/`（228 张 `Lightmap-*_comp_light.exr` + 228 张方向图 png + 3 张反射探针 + 材质 / profile） | 937 | 130.2 MB | 该进 | `RaceMap.unity:99` 的 `m_LightingDataAsset` 指向 `RaceMap/LightingData.asset`（guid `3df36e97…`），烘焙结果在用；每次重烘会整批重写约 128 MB LFS |
| `Assets/Scenes/Oasis/` | 607 | 450.0 MB | 不该进（迁出 13 个在用文件后）· 已登记 P7 | URP 示例；运行时只用到 13 个文件 25.9 MB，见 2.3 |
| `Assets/Scenes/Terminal/` | 163 | 15.0 MB | 不该进（迁出 3 张光晕贴图后）· 已登记 | URP 示例；只用到 3 张 flare 贴图 3.8 MB |
| `Assets/Scenes/Garden/`、`Cockpit/` | 74 | 6.7 MB | 不该进（Cockpit 的 1 个子图除外）· 已登记 | URP 示例；脚本仍编进 Assembly-CSharp |
| `Assets/Scenes/RaceMapEndField/` | 6 | 1.2 MB | 不该进 · 已登记 | 无 .unity、不在 Build；LightingData guid `58b20fe8…` 0 引用 |
| `Assets/SharedAssets/` | 163 | 4.9 MB | 不该进 · 已登记 | URP 示例（FirstPersonController、Benchmark、示例 TMP 字体）；其中 `TextMeshPro/Resources` 2.2 MB 因为在 Resources 下会**无条件打进包**，见 2.4 |
| `Assets/Sources/地图/`（赛道 fbx + MapSubstances） | 105 | 866.3 MB | 部分该进 | 3 个 fbx 和 Desert_Sand / Map_subsMarble / Map_subsMarble 1 的部分 TGA 被 RaceMap 材质引用（370 MB）；其余约 496 MB 运行时 0 引用，见 2.1 |
| `Assets/Sources/Skin_Subs/` | 57 | 432.2 MB | 部分该进 **[PR#63]** | 3 张 64 MB TGA 被 DefaultSkin.shadergraph 用；其余 24 个文件 240 MB 0 引用 |
| `Assets/Sources/Buddah/` | 18 | 81.0 MB | 不该进 · 已登记 | 4 个 Google Drive 下载包里的 fbx（文件夹名带 `-20260215T0711xxZ-1-001`）+ `佛手Projectile.fbx`，全部 0 引用 |
| `Assets/Character/` | 16 | 40.1 MB | 部分该进 | `佛头动画tpost.fbx` 被 PropertySelection 用；`佛头待机动画.fbx` 20 MB 0 引用，见 2.2 |
| `Assets/VFX/`（技能插槽、Shared/Piloto 副本） | 336 | 69.5 MB | 该进 | 插槽 prefab 和从 Piloto 复制的贴图（`Docs/vfx/asset-sources.md`）；退休项见 retired.md |
| `Assets/UI/` | 259 | 15.4 MB | 大部分该进 **[PR#63]** | 92 个文件运行时在用；4 个 `.ai` 源文件 4.7 MB 和若干草稿 png 0 引用，见 2.5 |
| `Assets/Fonts/SoloCJK` | 9 | 8.4 MB | 该进 | 中文子集字体与 TMP 字体资产（运行时在用）；字体源 `Assets/Sources/Font/` 80 MB 已忽略，正确 |
| `Assets/Animation`、`Material`、`Placeholder`、`Rendering`、`Settings`、`Skills_Assets`、`UIMenus`、`UI Toolkit` | ~190 | 1.5 MB | 该进 | 少数 0 引用小文件见 2.6 |
| 第三方 `Assets/FishNet/` | 1478 | 5.4 MB | 该进 | 网络库源码（含 GameKit ObjectPooling 自带的 `Demo/` 小场景，范围外） |
| 第三方 `Assets/Plugins/Feel/` | 2327 | 70.1 MB | 主体该进，部分不该进 · 已登记 P7 | MMFeedbacks / Shakers / Cinemachine 在用；NiceVibrations（27.7 MB）与 MMTools/Accessories（37.5 MB）0 引用，见 2.4 |
| 第三方 `Assets/Plugins/Facepunch.Steamworks`、`FishyFacepunch` | 50 | 4.1 MB | 该进 | Steam 传输 |
| 第三方 `Assets/Plugins/TextMesh Pro` | 72 | 3.4 MB | 该进 | TMP Essentials（`TMP Settings.asset` 在这里） |
| 第三方 `Assets/Plugins/Piloto Studio`（已跟踪的旧包） | 135 | 6.8 MB | 存疑 · 已登记 retired.md 6.3 | 21 个文件在用，41 个 0 引用；"是否连旧包目录一起清由用户定" |
| 第三方 `Assets/Plugins/UnitySSReflectionURP` | 19 | 0.1 MB | 该进（暂留） | SSR 已于 2026-10-28 拆除，`Docs/rendering/racemap/README.md:27` 写明插件留作回滚；MIT 许可 |
| 第三方 `Assets/Plugins/GabrielAguiarProductions` | 7 | 0.1 MB | 存疑 | `Flare00_AB_2.mat` / `Flare00.PNG` 0 引用；另有 LFS 大小写问题见 4.2 |
| 第三方 `Assets/Adobe/Substance3DForUnity` | 337 | 36.6 MB | 该进 | 生成 MapSubstances / Skin_Subs 的编辑器插件；iOS `.a`、macOS `.dylib`（单个 18 MB）、Linux `.so` 对 Windows 目标无用，但属于插件原件，不建议拆 |
| 已忽略：`Assets/Piloto Studio/`、`Assets/FishNet/Demos/`、Feel Demos、`TutorialInfo`、`Sources/Font/` | — | — | 正确忽略 | `git ls-files` 里无这些路径 |
| 本地沙盒 `Assets/Showcase/` | 0 | — | 正确未跟踪，但规则位置不对 | 只在 `.git/info/exclude:18`，见 4.1 |
| 本地测试场景 `Assets/Scenes/VFXtests.unity` | 0 | — | 存疑 | 未跟踪也未忽略，编辑器菜单依赖它，见 3.1 |
| `Docs/`（含 158 张 LFS 图） | 281 | 46.4 MB | 该进 | 调查文档与证据图（用户规则：不删）；`palm-mesh-tripo/` 59 张 PNG 15.3 MB，之后的截图可学 rendering 的"PNG 留本地、提交 JPEG"做法（低） |
| `Docs/optimization/evidence/review-round2/r8-editor-csv.zip` | 1 | 2.0 MB | 该进（证据），但应进 LFS | 被 `.gitignore:30` 的 `*.zip` 命中、强制加入的普通 blob |
| `agent-exchange/` | 68 | 9.7 MB | 该进（历史证据） | harness 定义为证据；`console/*.log` 40 个 9.2 MB 是普通 blob，其中 `2026-04-18-phase3a-v5-host.log` 单个 8.9 MB |
| `Tools/` | 100 | 1.6 MB | 该进 | 校验 / 基准 / 配表脚本；AI 遥测 csv/jsonl 已走 LFS |
| `Packages/manifest.json`、`packages-lock.json` | 2 | <0.1 MB | 该进，齐全 | 两个 git 包（softmask、unity-mcp tag）由 lock 锁定 hash |
| `ProjectSettings/` | 27 | 0.1 MB | 该进，齐全 | 与磁盘一一对应，无缺失、无多余 |
| `UserSettings/`、`Library/`、`Temp/`、`Logs/`、`Builds/`、`*.csproj`、`*.sln` | 0 | — | 正确忽略 | `git ls-files` 无命中；`Tools/SinglePlayerBenchmark/sampler-tests.csproj` 是手写项目，由该目录自己的 `.gitignore` 的 `!sampler-tests.csproj` 放行，正确 |
| 根目录 agent 配置（`.mcp.json`、`.codex/config.toml`、`.claude/skills`、`.agents/skills`、`AGENTS.md` / `Agent.md` / `CLAUDE.md`） | 8 | <0.1 MB | 该进 | 只有回环地址，无凭据（`git grep` 查过 key/token/secret） |

## 2. 已经进仓库但不该在里面的文件

体积为 LFS 大小。"运行时引用"按第 0 节的可达性分析；"代码按名加载"均已 grep 排除。

### 2.1 `Assets/Sources/` 里运行时 0 引用的部分（中；引用关系确定，删法"可能"）

| 路径 | 大小 | 理由 |
|---|---:|---|
| `Sources/Buddah/右手推动画-20260215T071138Z-1-001/**`（2 个 fbx） | 39.7 MB | 下载包原样解压；guid 0 引用。`Assets/Animation/BuddahAnim` 下有独立的 `推Left/Right_Start/End.anim`（是否由这些 fbx 提取未核实） |
| `Sources/Buddah/左手推动画-20260215T071139Z-1-001/**`（2 个 fbx） | 39.7 MB | 同上；`左手推_进入CD动画/` 下的文件名却是 `佛头左手推_判断前动画.fbx`（与另一个同名不同 oid），命名错误 |
| `Sources/Buddah/佛手Projectile.fbx` | 1.7 MB | guid `8ab0269d…` 0 引用 |
| `Sources/地图/MapSubstances/Map_subsMarble_graph_0 1/` 6 张 TGA | 384 MB（去重约 320 MB） | `Map_subsMarble.sbsar` 的**第二个图实例**（文件夹名带 " 1"），4096² 输出，没有任何材质用；在用的是 `Map_subsMarble_graph_0/`（4 MB 版） |
| `Sources/地图/MapSubstances/macael_marble_knit_tiles_*.png`（8 张） | 43.1 MB | 没有 sbsar / 材质引用；basecolor 与 diffuse 是同一个 oid |
| 各 Substance 图的 `*_diffuse.tga`、`*_specular.tga`、部分 `*_height.tga` | Map_subsMarble 1 的 diffuse 64 MB；kashmir 的 diffuse/height/specular 192 MB；knit_tiles、marble 两图全部 48 MB | URP Lit / BuddahGo/Lit 不读 diffuse/specular；只有 `graph_0.asset` 引用它们 |
| `Sources/地图/反转法线空气墙.fbx`、`.prefab` | <1 MB | 0 引用（prefab 已在 inventory-assets.md:146 登记） |
| `Sources/地图/地图资源-资产库白模.fbx` | 小 | 只被 2 个非运行时资产引用 |
| `Sources/VFX资源/VFXTutCylinder.fbx` | <0.1 MB | 0 引用 |

合计（按 oid 去重）约 **531 MB**。核实：可达性分析 + 对每个文件的 `.meta` guid 在 `Assets/`、`ProjectSettings/` 下所有文本资产里 grep。

怎么修：Substance 输出是插件按 `graph_0.asset` 生成的，直接删 TGA 可能在重导入时被重新生成。应在 Substance 图的 Inspector 里删掉 " 1" 图实例、关掉不用的输出（diffuse / specular），再提交删除；`Sources/Buddah/` 和 `macael_*` 不归插件管，可以直接移出仓库（放美术网盘）。另外 `Assets/Sources` 这个名字暗示"源文件"，实际混着运行时贴图和赛道 fbx；PR #63 还往里加了 1226 个 `MenuEnvironment` 文件 **[PR#63]**，建议合并后把运行时资产迁到 `Assets/Art/`，`Sources/` 只留确实要进仓库的源。

### 2.2 `Assets/Character/佛头待机动画.fbx`（低，确定）

20.0 MB，guid `c679372d…` 在所有场景 / prefab / controller / timeline / meta 里 0 引用，代码无路径加载。新发现（inventory-assets.md 只把 `MenuPlayer.prefab` 列为可疑）。建议移出或确认后删。

### 2.3 URP 示例残留（中，确定；已登记 refactor-plan P7，D7 未决）

运行时真正用到的文件（删目录前必须先迁走）：

| 文件 | 谁在用 |
|---|---|
| `Oasis/Art/Environment/LensFlares/OasisSun_Flare.asset` | `RaceMap.unity` 的 MainLight 镜头光晕 |
| `Terminal/Art/Environment/Textures/{PartialRIng,Ring,Shimmer}_T_Flare.png`（3.8 MB） | 上面的 OasisSun_Flare |
| `Oasis/Scripts/Runtime/OasisFog.cs`、`Oasis/Shaders/HeightFog.shadergraph` | `RaceMap.unity`（README 说 `Post_Effects/Oasis Fog` 已禁用，但组件和引用还在） |
| `Cockpit/Shaders/Subgraphs/MainLight.shadersubgraph` | HeightFog.shadergraph |
| `Oasis/Art/Environment/Meshes/BigRock_01_Mesh.fbx`、`Materials/BigRock_01_Mat.mat` + 4 张 `BigRock_01_T_*.png`（11.9 MB） | `Assets/VFX/VisualEffectGraph/沙砾.prefab` → `Buddah.prefab` |
| `Oasis/Art/Props/Materials/Firepit_01_Mat.mat` + 4 张 `Firepit_01_T_*.png`（13.3 MB） | 同上 `沙砾.prefab` |

注意最后两行：角色身上的沙砾粒子借用了 Oasis 的 4K 岩石 / 火坑贴图（约 25 MB 进包）。retired.md 第 4 节把 `沙砾.prefab` 列为"还在用"，所以这是迁移项不是删除项；迁移时可考虑换成小贴图（表现力不做减法，换图需用户确认）。

不在用：Oasis 其余 271 个文件约 424 MB（含 `OasisScene.unity` 9.95 MB 文本场景、`OasisScene/Lightmap-1_comp_light.exr` 10.8 MB）、Terminal 其余 67 个约 11 MB、Garden 全部 5 个 6.6 MB、Cockpit 其余、`RaceMapEndField/` 全部、`SharedAssets/` 全部（Shaders、Textures、FirstPersonController、Benchmark、Prefabs）。按 oid 去重约 427 MB。核实：可达性分析；另把这些目录里 84 个 C# 类型名在 `Assets/Scripts|Editor|Tests|VFX|Rendering` 里 grep，除泛名 `Event` 外无命中。

### 2.4 在包里白占空间的 Resources 与第三方附件

- `Assets/SharedAssets/TextMeshPro/Resources/FontsAndMaterials/LiberationSansSDF.asset`（2.15 MB）+ `-Fallback.asset`（低，确定）：URP 示例带来的第二份 LiberationSans；guid `0981b8e3…` 0 引用，TMP 默认字体走 `Plugins/TextMesh Pro/Resources/TMP Settings.asset` 里的另一份（guid `8f586378…`）。因为在 `Resources/` 下，Unity 会**无条件打进 Player**。随 SharedAssets 一起删即可。
- Feel `NiceVibrations/OlderVersions/` 三个 `.unitypackage`（11.6 MB）、`NiceVibrations/Plugins/iOS/LofeltHaptics.framework/LofeltHaptics`（13.2 MB，**普通 blob**，见 4.2）、`Android/libs/LofeltHaptics.aar`（1.3 MB，普通 blob）、`macOS/*.dylib`（1.2 MB）（低，确定）：Windows/Steam 项目不用；`.framework.meta` 已排除 Editor/Win/Linux/OSX。NiceVibrations 的 C# 不能删——`MMFeedbacksForThirdParty/NiceVibrations/Feedbacks/MMF_NV*.cs` 引用 `Lofelt` 命名空间（grep 过），只能删原生库和旧版包，或整体按 refactor-plan P7 先确认程序集依赖。
- Feel `MMTools/Accessories/MMVFX`（28.3 MB）、`MMPrototypeTextures`（7.5 MB）（低，确定）：0 引用（Accessories 下唯一"可达"的是 `MMDebugOnScreen/Resources` 的 prefab，那只是因为在 Resources 下）。已登记 P7。

### 2.5 `Assets/UI/UI资源` 里的源文件与草稿（低；`.ai` 确定，png 可能）**[PR#63]**

- 4 个 Illustrator 源文件，普通 blob 共 4.7 MB：`执着值PNG/执着值二稿_组合.ai`、`技能按键PNG/W-分层.ai`、`技能按键PNG/↑-分层.ai`、`技能按键PNG/技能准备就绪图标.ai`。Unity 不导入 `.ai`（DefaultAsset），放在 `Assets/` 只增体积；应移到仓库外或至少进 LFS。
- 0 引用的 png（约 3.4 MB）：`执着值PNG/二搞/*`、`三稿/*`、`执着值-佛头{0,50,100}.png`、`执着值-点.png`、`执着值-线.png`、`技能按键PNG/渐变.png`、`排行PNG/{贰,叁,肆,伍,陆}.png`、`地图UI-PNG/地图.png`。代码里 grep 这些文件名无命中。但 PR #63 改了 `UIprefap.prefab` 和三个 `SkillSlot_*.prefab`，合并后要重跑引用分析再定。

### 2.6 零散小件（低）

- 已登记、仍未删：inventory-assets.md:146 的 `Animation/CG/AnimationLap/冲线白色线条.prefab`、`Settings/TryUltra URP*.asset`、`Settings/MapLight.lighting`、`Material/Sky.mat`；retired.md 第 5 节的 `Material/ShaderGraphs/SG_Transparent.shadergraph`；retired.md 6.3 的 `VFX/禅定缓流/阵法-完整黑底.png`（1.47 MB）与 Piloto 旧包 41 个 0 引用文件。
- 新发现：`Assets/Animation/UiAnimation/CrossFade.controller` 与 `CrossFade_IdleTransparent.anim`、`CrossFade_In.anim`、`CrossFade_out.anim` 运行时 0 引用（可达性分析）；可能被尚未合并的 UI 改动使用，确认后再删（可能）。
- `Assets/VFX/Shared/Piloto/Textures/yingyang_3.png`（0.39 MB）0 引用，可能是阴阳特效调研的候选图（`Docs/vfx/yingyang/`），由特效会话决定。

### 2.7 孤立的文件夹 meta（低，确定）

- `Assets/Animation/CGTimeline.meta`（guid `ac7a71f2…`，`folderAsset: yes`，来自 `215e2809`）
- `Assets/UIMenus/PropertySelector/CardSelection.meta`（来自 `bc545a45`）

对应文件夹在仓库和磁盘上都不存在（空文件夹不进 git）。Unity 打开时会报"meta 存在但资产找不到"并删掉它们，工作区随之变脏。删这两个 meta 即可（无 guid 引用，grep 过）。

## 3. 应该进仓库却漏掉的

### 3.1 `Assets/Scenes/VFXtests.unity`（中，确定未跟踪；该不该进由用户定）

`Docs/vfx/README.md:92` 说插槽"在 `Assets/Scenes/VFXtests.unity`（本地测试场景）里手调，用菜单 `Tools/BuddahGo/VFX/Proto/Release Placeholders/` 写回"；`Assets/Editor/VFX/Proto/PalmFrontalProto.cs:111` 的菜单 `Write Frontal Pose From VFXtests` 直接依赖它（`:119` 找不到对象就报错）；`palm-mesh-tripo.md:704` 还提到同场景的 `Assets/Scenes/VFXtests_Environment_Profile.asset`。这两个文件在任何分支都没提交过（`git log --all` 无记录），也不在 `.gitignore` / `info/exclude` 里，主仓库目录里也不存在（只在用户正开着的 vfx-refactor worktree 里，按约定未读）。
- 选项 A：提交场景和 profile，让编辑器工具对别的 worktree / 机器可用。
- 选项 B：确认只本地用，加进忽略（4.3 给了规则），并在 README 里写明"菜单需要本地场景"。
- 现在的状态（既不跟踪也不忽略）最容易被某次 `git add -A` 误提交。

### 3.2 `.meta` 完整性（确定）

`Assets/` 下所有被跟踪文件和目录都有 `.meta`，只有 `LofeltHaptics.framework/` 内部 4 个文件没有——`.framework` 在 Unity 里是单个资产，`LofeltHaptics.framework.meta` 已跟踪，所以正常。反方向只有 2.7 的两个孤立文件夹 meta。

### 3.3 被 `.gitignore` 误伤 / 留下噪音的（低，确定）

主仓库 `git status` 显示两个未跟踪文件：`Assets/FishNet/CodeGenerating/cecil-0.11.4/Mono.Cecil.sln.meta`、`Assets/FishNet/Runtime/Transporting/Transports/Tugboat/LiteNetLib/LiteNetLib.csproj.meta`。FishNet 自带的 `.sln` / `.csproj` 被根 `.gitignore` 的 `*.sln` / `*.csproj` 忽略了（这没问题，Unity 编译不需要它们），但 Unity 给它们生成的 `.meta` 没被忽略，一直挂在未跟踪列表里。用 4.3 的规则忽略这两个 meta。

### 3.4 `Packages/`、`ProjectSettings/`（确定）

`manifest.json` 与 `packages-lock.json` 都在；`ProjectSettings/` 27 个文件与磁盘完全一致，含 `ProjectVersion.txt`（2022.3.55f1c1）、`EditorBuildSettings.asset`、URP / VFX / ShaderGraph / Timeline 设置。没有缺项。

## 4. `.gitignore` 与 `.gitattributes`

### 4.1 `.gitignore` 现状问题

1. **`Assets/Showcase/` 只在 `.git/info/exclude`**（中，确定）：memory 记它含第三方评估代码和 GPL demo、"永不提交"。`info/exclude` 不随仓库走，别的克隆、CI 或重新 clone 后没有保护，一次 `git add -A` 就会把 35 MB 的 GPL 代码带进来。`.claude/worktrees/` 同理只在 info/exclude。应移进 `.gitignore`。`ParrelSync`、`ArchitectureLocalMcp.cs`、`AGENTS.override.md` 是个人本地项，留在 info/exclude 是对的。
2. **缺 Unity 官方模板项**（低，确定）：`[Rr]ecordings/`、`ExportedObj/`、`.consulo/`、`.gradle/`、`*.suo`、`*.tmp`、`*.pdb`、`*.mdb`、`*.opendb`、`*.VC.db`、`*.pidb.meta`、`*.pdb.meta`、`*.mdb.meta`、`sysinfo.txt`、`*.apk`、`*.aab`、`*.app`、`crashlytics-build.properties`、`/Assets/Plugins/Editor/JetBrains*`。
3. **Python 缓存只在两个子目录忽略**（低，确定）：`Tools/SinglePlayerBenchmark/.gitignore`、`Tools/vfxperf/.gitignore` 有 `__pycache__/`，但 `Tools/design-tables`、`Tools/Validation`、`Tools/ai`、`Tools/Performance`、`Tools/Audit` 也都有 `.py`。
4. **冗余**（低）：`agent-exchange/console/raw/*.log`、`*.txt`、`_*.py` 三行已被下面的 `/agent-exchange/console/raw/` 整体覆盖。
5. **规则对应的目录已不存在**（信息）：`Assets/Piloto Studio/`、`Assets/FishNet/Demos/`、`Assets/Adobe/.../Runtime/Plugins/Temp/`、`/.VSCodeCounter/` 在主仓库都不存在了。作为防重导入的保护可以留着，但 Piloto 那段注释应补一句"目录已删，防止重新导入"。
6. **未锚定的目录规则**（低，确定）：`[Ll]ibrary/`、`[Tt]emp/`、`[Ll]ogs/`、`[Bb]uild/` 等不带前导 `/`，会匹配任意深度的同名目录——`!Assets/Adobe/Substance3DForUnity/Connector/Library/` 这条例外就是被它误伤后补的。主仓库 `git status --ignored` 显示目前没有别的误伤，但改成 `/[Ll]ibrary/` 这类锚定写法后可以删掉 Adobe 例外，也不会再误伤以后的插件。
7. 强制加入的例外没写明：`Docs/optimization/evidence/review-round2/r8-editor-csv.zip` 被 `*.zip` 命中（`git ls-files -ci --exclude-standard` 唯一一条）。加一行 `!` 例外让意图可读。

### 4.2 `.gitattributes` 现状问题

1. **`*.pdf` 规则被删，留下两个"孤儿 LFS 指针"**（中，确定）：`c3355e6c`（2026-03-26 "Refine Unity ignore and LFS rules"）去掉了 `*.pdf filter=lfs`，但 `Assets/Adobe/Substance3DForUnity/HOWTO.pdf`（243 KB）和 `Assets/Plugins/TextMesh Pro/Documentation/TextMesh Pro User Guide 2016.pdf` 在仓库里仍是 LFS 指针。没有 filter 属性，`git lfs pull` 不会还原它们，任何新克隆 / 新 worktree 检出的都是 130 字节的指针文本（本 worktree 里 `head -c 200 HOWTO.pdf` 就是 `version https://git-lfs…`；主仓库是好的，因为它在规则删除前就检出了）。核实：比对 `git check-attr filter` 与 blob 内容，`git log -S'*.pdf' -- .gitattributes`。
2. **大小写**（低，确定）：`Assets/Plugins/GabrielAguiarProductions/FreeQuickEffectsVol1/Textures/Flare00.PNG` 是 LFS 指针，但 `*.png` 在 `core.ignorecase=false` 的克隆（Linux / macOS / CI）里不匹配 `.PNG`（`git -c core.ignorecase=false check-attr filter` → `unspecified`）。本机 `core.ignorecase=true` 所以看不出来。文件本身 0 引用。
3. **该走 LFS 却是普通 blob 的二进制**（低，确定；共约 25 MB，已进历史的不会因改规则变小）：

| 文件 | 大小 | 缺的规则 |
|---|---:|---|
| `Plugins/Feel/NiceVibrations/Plugins/iOS/LofeltHaptics.framework/LofeltHaptics` | 13.2 MB | 无扩展名的 framework 二进制 |
| `UI/UI资源/**/*.ai`（4 个） | 4.7 MB | `*.ai` |
| `Docs/optimization/evidence/review-round2/r8-editor-csv.zip` | 2.0 MB | `*.zip` |
| `Scenes/{RaceMap,Oasis/OasisScene,RaceMapEndField}/LightingData.asset` | 1.4 MB | 现在是 `binary`，每次重烘整份重写，应走 LFS |
| `Plugins/Feel/NiceVibrations/Plugins/Android/libs/LofeltHaptics.aar` | 1.3 MB | `*.aar` |
| `Adobe/.../libsubstance_linker.so.9`、`libsubstance_ogl3_blend.so.9` | 1.1 MB | `*.so` 不匹配 `.so.9` |
| `Plugins/Facepunch.Steamworks/redistributable_bin/**/steam_api*.lib` | 0.7 MB | `*.lib` |
| `Fonts/SoloCJK/BuddahGoSoloCJK.otf`、TMP `LiberationSans.ttf`、Feel `orange kid.ttf` | 0.55 MB | `*.otf`、`*.ttf` |

   核实：`git ls-files --eol` 里 `i/-text` 的 20 个文件全部不是 LFS。
4. **YAML 类型没声明 `text eol=lf`**（低）：`.lighting`、`.giparams`（已跟踪）以及 `.mask`、`.signal`、`.terrainlayer`、`.spriteatlas`、`.preset`、`.mixer`、`.physicMaterial`、`.renderTexture`、`.cubemap`、`.flare`、`.fontsettings`、`.shadervariants`、`.compute`、`.asmref`。目前索引里全部是 LF（`git ls-files --eol` 无 `i/crlf`），只是缺保护。
5. **脚本行尾**（低，可能）：本机 `core.autocrlf=true`，36 个 `.py` / `.sh` / `.ps1` 在工作区是 CRLF。`Tools/ai/thrust-vector/scripts/lap.sh`、`race.sh` 首行是 `#!/bin/bash\r\n`（`od -c` 看过），直接用 bash 跑可能报 `$'\r'`。加 `*.sh text eol=lf`。
6. **大文本资产**（信息）：`Assets/Fonts/SoloCJK/BuddahGoSoloCJK.asset` 8.25 MB、两份 LiberationSans SDF 各 2.15 MB、`VFX_Common_Cast_Anti.prefab` 5.26 MB、`OasisScene.unity` 9.95 MB 是 YAML，按文本存是 Unity 惯例；字体图集每次重建会产生 8 MB 级 diff，可以考虑给 `Assets/Fonts/**/*.asset` 加 `-diff`，不必进 LFS。

### 4.3 建议的新规则（可直接粘贴）

`.gitignore` 追加：

```gitignore
# --- Unity 官方模板里缺的项 ---
[Rr]ecordings/
ExportedObj/
.consulo/
.gradle/
*.suo
*.tmp
*.pdb
*.pdb.meta
*.mdb
*.mdb.meta
*.opendb
*.VC.db
*.pidb.meta
*.apk
*.aab
*.app
sysinfo.txt
crashlytics-build.properties
/Assets/Plugins/Editor/JetBrains*

# Unity 给 FishNet 自带的 .sln / .csproj 生成的 meta（本体已被 *.sln / *.csproj 忽略）
/Assets/**/*.csproj.meta
/Assets/**/*.sln.meta

# 本地渲染样板间：含第三方评估代码和 GPL demo，永不提交（以前只在 .git/info/exclude）
/Assets/Showcase/
/Assets/Showcase.meta

# Claude Code 的 worktree（以前只在 .git/info/exclude）
/.claude/worktrees/

# Python 缓存（Tools/ 多个目录有 .py）
__pycache__/
*.py[cod]

# 已跟踪的证据压缩包（被上面的 *.zip 命中，强制加入）
!/Docs/optimization/evidence/review-round2/r8-editor-csv.zip
```

`.gitignore` 同时建议改写（可选）：

```gitignore
# 锚定到仓库根，避免误伤插件内部同名目录；改后可删掉 Adobe Connector/Library 的两条例外
/[Ll]ibrary/
/[Tt]emp/
/[Ll]ogs/
/[Uu]ser[Ss]ettings/
/[Mm]emoryCaptures/
/[Bb]uild/
/[Bb]uilds/
# [Oo]bj/ 保持不锚定（Tools/ 下的 dotnet 项目也会产生 obj/）

# 删掉这三行（已被 /agent-exchange/console/raw/ 覆盖）：
# agent-exchange/console/raw/*.log
# agent-exchange/console/raw/*.txt
# agent-exchange/console/raw/_*.py
```

若用户选 3.1 的选项 B（VFXtests 只本地用）：

```gitignore
# 本地 VFX 测试场景（Docs/vfx/README.md 第 92 行的"本地测试场景"；编辑器菜单依赖它）
/Assets/Scenes/VFXtests.unity
/Assets/Scenes/VFXtests.unity.meta
/Assets/Scenes/VFXtests_Environment_Profile.asset
/Assets/Scenes/VFXtests_Environment_Profile.asset.meta
```

`.gitattributes`：把 `LightingData.asset binary` 一行替换，并在"Binary assets use Git LFS"一节追加：

```gitattributes
# 烘焙光照数据：二进制且每次重烘整份重写，走 LFS（替换原来的 "LightingData.asset binary"）
LightingData.asset filter=lfs diff=lfs merge=lfs -text

# c3355e6c 误删的规则；两个 PDF 至今是 LFS 指针，新克隆检出为指针文本
*.pdf filter=lfs diff=lfs merge=lfs -text
# core.ignorecase=false 的克隆里 *.png 不匹配大写扩展名（Flare00.PNG）
*.PNG filter=lfs diff=lfs merge=lfs -text
*.JPG filter=lfs diff=lfs merge=lfs -text
*.TGA filter=lfs diff=lfs merge=lfs -text
*.FBX filter=lfs diff=lfs merge=lfs -text

# 目前以普通 blob 进仓库的二进制类型
*.ai filter=lfs diff=lfs merge=lfs -text
*.otf filter=lfs diff=lfs merge=lfs -text
*.ttf filter=lfs diff=lfs merge=lfs -text
*.aar filter=lfs diff=lfs merge=lfs -text
*.jar filter=lfs diff=lfs merge=lfs -text
*.lib filter=lfs diff=lfs merge=lfs -text
*.so.* filter=lfs diff=lfs merge=lfs -text
*.zip filter=lfs diff=lfs merge=lfs -text
*.7z filter=lfs diff=lfs merge=lfs -text
# iOS framework 里的无扩展名二进制（只点名这一个，避免把 Info.plist / 头文件也塞进 LFS）
Assets/Plugins/Feel/NiceVibrations/Plugins/iOS/LofeltHaptics.framework/LofeltHaptics filter=lfs diff=lfs merge=lfs -text

# 仓库里暂时没有、但 Unity 项目常见的二进制
*.tif filter=lfs diff=lfs merge=lfs -text
*.tiff filter=lfs diff=lfs merge=lfs -text
*.bmp filter=lfs diff=lfs merge=lfs -text
*.gif filter=lfs diff=lfs merge=lfs -text
*.psb filter=lfs diff=lfs merge=lfs -text
*.aif filter=lfs diff=lfs merge=lfs -text
*.aiff filter=lfs diff=lfs merge=lfs -text
*.flac filter=lfs diff=lfs merge=lfs -text
*.obj filter=lfs diff=lfs merge=lfs -text
*.max filter=lfs diff=lfs merge=lfs -text
*.ma filter=lfs diff=lfs merge=lfs -text
*.mb filter=lfs diff=lfs merge=lfs -text
*.spp filter=lfs diff=lfs merge=lfs -text
*.sbs filter=lfs diff=lfs merge=lfs -text
*.bytes filter=lfs diff=lfs merge=lfs -text
*.pcache filter=lfs diff=lfs merge=lfs -text
```

在"Unity YAML / text assets"一节追加：

```gitattributes
*.lighting text eol=lf
*.giparams text eol=lf
*.mask text eol=lf
*.signal text eol=lf
*.terrainlayer text eol=lf
*.spriteatlas text eol=lf
*.preset text eol=lf
*.mixer text eol=lf
*.physicMaterial text eol=lf
*.renderTexture text eol=lf
*.cubemap text eol=lf
*.flare text eol=lf
*.fontsettings text eol=lf
*.shadervariants text eol=lf
*.compute text eol=lf
*.asmref text eol=lf
*.md text eol=lf
*.sh text eol=lf
*.py text eol=lf
```

已核实的匹配：用临时 `core.attributesFile` / `core.excludesFile` 加 `git check-attr` / `git check-ignore --no-index` 试过，`*.pdf`、`*.PNG`（ignorecase=false）、`*.so.*`、framework 二进制的单独路径、`*.ai`、`LightingData.asset` 都命中 `filter: lfs`；`/Assets/**/*.csproj.meta`、`/Assets/**/*.sln.meta`、`__pycache__/` 命中 3.3 的两个 meta 和 `Tools/design-tables/__pycache__/`。

落地注意：改完属性后，已存在的文件不会自动变成 LFS。要在一个单独的 commit 里对受影响路径执行 `git add --renormalize <路径>`（两个 PDF、Flare00.PNG、LightingData ×3、`.ai` ×4、`.aar`、`.lib` ×2、`.so.9` ×2、字体 ×3、zip、framework 二进制），提交前用 `git lfs ls-files` 确认。历史里的旧 blob 不会消失；要真正缩小仓库得 `git lfs migrate`，那是改写历史，需用户另行决定。`.PNG/.JPG` 等大写规则在本机（ignorecase=true）看不出效果，用 `git -c core.ignorecase=false check-attr filter -- <路径>` 验证。

## 5. 最大的 30 个被跟踪文件

"RT" = 从 Build 场景 / Resources / ProjectSettings 可达；"--" = 运行时 0 引用。前 18 个全是 64 MB（4096² RGBA 未压缩 TGA）。

| # | 大小 | 存储 | 引用 | 路径 | 是否必要 |
|---:|---:|---|---|---|---|
| 1 | 64.0 MB | LFS | RT | `Sources/Skin_Subs/kashmir_white_granite_graph_0/kashmir_white_granite_basecolor.tga` | 必要（DefaultSkin.shadergraph）**[PR#63]** |
| 2 | 64.0 MB | LFS | -- | `…/kashmir_white_granite_diffuse.tga` | 不必要（与 #1 同 oid，不占额外 LFS） |
| 3 | 64.0 MB | LFS | -- | `…/kashmir_white_granite_height.tga` | 不必要 |
| 4 | 64.0 MB | LFS | RT | `…/kashmir_white_granite_metallic.tga` | 必要 **[PR#63]** |
| 5 | 64.0 MB | LFS | RT | `…/kashmir_white_granite_normal.tga` | 必要 **[PR#63]** |
| 6 | 64.0 MB | LFS | -- | `…/kashmir_white_granite_specular.tga` | 不必要 |
| 7 | 64.0 MB | LFS | RT | `Sources/地图/MapSubstances/Map_subsMarble 1_graph_0/Map_subsMarble 1_basecolor.tga` | 必要（RaceMap_Curb.mat） |
| 8 | 64.0 MB | LFS | -- | `…/Map_subsMarble 1_diffuse.tga` | 不必要（与 #7 同 oid） |
| 9 | 64.0 MB | LFS | RT | `…/Map_subsMarble 1_height.tga` | 必要（RaceMap_Curb.mat） |
| 10 | 64.0 MB | LFS | RT | `…/Map_subsMarble 1_metallic.tga` | 必要 |
| 11 | 64.0 MB | LFS | RT | `…/Map_subsMarble 1_normal.tga` | 必要 |
| 12 | 64.0 MB | LFS | RT | `…/Map_subsMarble 1_specular.tga` | 必要（材质引用了，但 Lit 是否真读 specular 存疑） |
| 13 | 64.0 MB | LFS | -- | `Sources/地图/MapSubstances/Map_subsMarble_graph_0 1/Map_subsMarble_basecolor.tga` | 不必要（重复图实例） |
| 14 | 64.0 MB | LFS | -- | `…/Map_subsMarble_graph_0 1/Map_subsMarble_diffuse.tga` | 不必要 |
| 15 | 64.0 MB | LFS | -- | `…/Map_subsMarble_graph_0 1/Map_subsMarble_height.tga` | 不必要 |
| 16 | 64.0 MB | LFS | -- | `…/Map_subsMarble_graph_0 1/Map_subsMarble_metallic.tga` | 不必要 |
| 17 | 64.0 MB | LFS | -- | `…/Map_subsMarble_graph_0 1/Map_subsMarble_normal.tga` | 不必要 |
| 18 | 64.0 MB | LFS | -- | `…/Map_subsMarble_graph_0 1/Map_subsMarble_specular.tga` | 不必要 |
| 19 | 20.0 MB | LFS | -- | `Character/佛头待机动画.fbx` | 不必要（2.2） |
| 20 | 19.9 MB | LFS | RT | `Character/佛头动画tpost.fbx` | 必要（PropertySelection） |
| 21 | 19.8 MB | LFS | -- | `Sources/Buddah/左手推动画-…/左手推_进入CD动画/佛头左手推_判断前动画.fbx` | 不必要 |
| 22 | 19.8 MB | LFS | -- | `Sources/Buddah/右手推动画-…/佛头右手推_进入CD动画/佛头右手推_进入CD动画.fbx` | 不必要 |
| 23 | 19.8 MB | LFS | -- | `Sources/Buddah/左手推动画-…/左手推_判定前动画/佛头左手推_判断前动画.fbx` | 不必要 |
| 24 | 19.8 MB | LFS | -- | `Sources/Buddah/右手推动画-…/右手推_判定前动画/佛头右手推_判断前动画.fbx` | 不必要 |
| 25 | 18.1 MB | LFS | -- | `Adobe/Substance3DForUnity/Runtime/Plugins/libsubstance_mtl_blend.dylib` | 插件原件（macOS 用），范围外，保留 |
| 26 | 17.8 MB | LFS | -- | `Sources/地图/MapSubstances/macael_marble_knit_tiles_basecolor.png` | 不必要 |
| 27 | 17.8 MB | LFS | -- | `Sources/地图/MapSubstances/macael_marble_knit_tiles_diffuse.png` | 不必要（与 #26 同 oid） |
| 28 | 14.0 MB | LFS | -- | `Scenes/Oasis/Art/Environment/Textures/SandSmooth_01_T_Packed.png` | 不必要（URP 示例） |
| 29 | 13.8 MB | LFS | -- | `Scenes/Oasis/Art/Environment/Textures/SandHarsh_01_T_Packed.png` | 不必要（URP 示例） |
| 30 | 13.2 MB | **普通 git** | -- | `Plugins/Feel/NiceVibrations/Plugins/iOS/LofeltHaptics.framework/LofeltHaptics` | 不必要（iOS，且不在 LFS） |

（路径省略了开头的 `Assets/`。第 31 名起是 Oasis 的贴图、`OasisScene.unity` 9.95 MB 和 `agent-exchange/console/2026-04-18-phase3a-v5-host.log` 8.9 MB。）

前 30 里 13 个必要，16 个不必要，1 个是范围外的插件原件。必要的 9 张 64 MB TGA 都是 Substance 的 4K 输出，导入设置会压缩，进包体积不等于 64 MB；但仓库和 LFS 存储里是全尺寸。降到 2K 或换成压缩源格式属于美术决定（refactor-plan P7 已列"用到的转为 PNG 或降分辨率"）。

## 6. PR #63 交叉

PR #63（`feat/mechanics-update`）在本文涉及的范围里改了：`Assets/Character/Prefab/Buddah.prefab`（Skin_Subs 引用链）、`Assets/UI/UIprefap.prefab` 与 `UI预制键位/SkillSlot_{1,2,3}.prefab`（2.5 的 UI 引用）、`Assets/Design/Tables/_Reports/validation.md`，并新增 `Assets/Sources/MenuEnvironment/`（1226 个文件，含 126 个 fbx、189 张 png、4 张 exr）和 `Assets/Scenes/MenuEnvironmentSetting.unity`。它没有改 `.gitignore`、`.gitattributes`、`Packages/`、`ProjectSettings/`，所以第 4 节的规则改动可以先做；标了 **[PR#63]** 的删除类建议要等它合并后按新的引用关系重跑一遍。
