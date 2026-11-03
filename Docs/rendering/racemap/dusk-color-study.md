# RaceMap 黄昏色彩研究：为什么整张图发黄，怎么改

日期 2026-11-03 · 分支 `feat/racemap-rendering-update`（dev c34d0d11 之上）· 研究阶段，**没有改任何场景或资产**。
背景与总体说明在 [README.md](README.md)（"Zen landscape and dusk"、"Known limits"、"Re-tuning checklist"）。
用户的问题（原话）："目前的黄昏表现将整个 scene 变成一个全黄色状态……让能见度变高，并且不要整个画面几乎失去色彩的纯暖色调。保证色彩分层，特效清晰可见，Buddah 材质清晰可以辨认"。

证据图（都在 [dusk_study/](dusk_study/)）：

| 文件 | 内容 |
|---|---|
| [candidates_01_04.jpg](dusk_study/candidates_01_04.jpg) | 现状 vs 候选 A2 / B2 / C2，起跑格与开场镜头 |
| [candidates_02_03_05.jpg](dusk_study/candidates_02_03_05.jpg) | 同上，直道 / 急弯 / 结算场 |
| [candidates_01.gif](dusk_study/candidates_01.gif)、[candidates_04.gif](dusk_study/candidates_04.gif) | 现状 → A2 → B2 → C2 循环 |
| [candidates_vfx_slow.jpg](dusk_study/candidates_vfx_slow.jpg)、[candidates_vfx_cast.jpg](dusk_study/candidates_vfx_cast.jpg) | 禅定缓流墨池、通用前摇金光在各方案下的样子 |
| [ablation_light.jpg](dusk_study/ablation_light.jpg)、[ablation_fog_post.jpg](dusk_study/ablation_fog_post.jpg)、[ablation_views.jpg](dusk_study/ablation_views.jpg) | 逐层关掉暖色源 |
| [grading_mode.jpg](dusk_study/grading_mode.jpg) | LDR 与 HDR 调色模式 |
| [candidates_round1.jpg](dusk_study/candidates_round1.jpg) | 第一轮候选（已弃用，见第 9 节） |

## 0. 结论先行

- **发黄不是某一层的问题，是四层都在加暖，而且没有任何一层给冷色。** 以起跑格视角计算，全图平均色度（CIE Lab 色度 C\*）33，其中太阳贡献约 15，后期（colorFilter + WhiteBalance 22 + 暖 Bloom）约 9，天空与环境光约 4，雾约 2；四层全部中性化后只剩 5（见第 4 节）。画面 98% 的带色像素落在 0–30° 这一个色相区间，色相标准差只有 7°，这就是"几乎失去色彩"的数值形态。
- **银色 Buddha 看起来是金的，来源是实时反射探针反射的橙色天空，不是材质。** 把探针换成中性灰立方图，Buddha 色度从 42.6 降到 15.3，与赛道的色差 ΔE 从 10.6 升到 22.5；其余画面不变。所以只要天空的天顶和地平线不再整片橙色，Buddha 自动变回银色。
- **技能特效全部走后期调色。** 四个 Renderer Feature（RenderBuddah、RenderSkillVfx、RenderMapEdge、RenderTrails）都在 `AfterRenderingTransparents`（500），后期在它们之后；WhiteBalance、colorFilter、饱和度和暖色 Bloom 都会作用到特效上。现状下通用前摇的金光与橙色赛道色相几乎重合（b\* 33 对 30），只靠 a\* 分开；禅定缓流的墨池只比赛道暗，不比赛道冷（a\*/b\* 与赛道一致），所以它在比赛里读成"一大片棕"（slots.md 里记录的现象）。
- **推荐方向 A2"金时暖光·冷影"**：暖色只留在太阳里（3400 K、滤镜接近白），天空天顶回到蓝、地平线窄一点，雾按天空重校并变淡，后期滤镜和白平衡归零，用 Shadows/Midtones/Highlights 把阴影推冷、高光推暖。全图色度 33 → 16，色相标准差 7° → 32°（有了冷暖两个方向），Buddha 色度 42.6 → 22.7（读成银），墨池 Lab 从 (32, 17.5, 26.6) 变成 (32, 10.8, 5.2)（读成墨黑），仍然是低角度暖阳。全部是运行时数值（太阳、雾、`RaceMap_Sky_Dusk.mat`、`RaceMap_Dusk_Profile.asset`），不动菜单场景的共用资产，不需要 bake。B2"黄昏分色"更晚、更紫，C2"最小改动"保留现在的橙调只去掉过量部分，三者数值都在第 6 节。

## 1. 现状：四层暖色叠在一起

在编辑器里读出的实际值（`RaceMapDuskColorStudy.Snapshot()`，2026-11-03，42be7613 引入的黄昏）：

| 层 | 资产 | 实际值 | 备注 |
|---|---|---|---|
| 太阳 | 场景 `Lighting/MainLight` | 旋转 (19, 66, 0)，颜色滤镜 (1, 0.80, 0.52) **且** 色温开、2900 K，强度 3.6 | 两个暖色相乘：最终线性光色 (3.60, 0.95, 0.11)，蓝通道只有红的 3%。1b 阶段的记录是 3600 K + (1, 0.92, 0.82) + 4.0 |
| 距离雾 | 场景 RenderSettings | Exp2，密度 0.0011，颜色 sRGB (0.96, 0.68, 0.40)，线性 (0.91, 0.42, 0.13) | 黄昏之后没有按 checklist 第 2 步对天空重校 |
| 天空 | `Assets/Scenes/RaceMap/RaceMap_Sky_Dusk.mat`（`BuddahGo/Skybox/Sunset Sky`） | zenith (0.16, 0.18, 0.38)、horizon (0.98, 0.55, 0.28)、horizon-sun (1.7, 0.58, 0.16)、ground (0.55, 0.30, 0.16)、glow (1.1, 0.38, 0.08) power 14、halo 0.6、disk (3, 1.25, 0.35) 2.4°、**horizon exponent 2.4**、exposure 1.1 | exponent 2.4 让暖色地平线带一直铺到天顶，开场镜头（俯仰 15°、FOV 60）里完全看不到蓝；1b 的 sunset 用的是 7 |
| 环境光 | 场景 RenderSettings | **Trilight 渐变**：sky (0.36, 0.44, 0.60)、equator (0.66, 0.50, 0.36)、ground (0.20, 0.15, 0.13)；ambientIntensity 1.2 对渐变模式无效 | 任务简报里写的"skybox ×1.2"不对。实际环境球谐 L0 = (0.285, 0.173, 0.127)，比渐变颜色更橙，可能仍是黄昏前 sunset bake 留下的环境探针（未核实） |
| 反射 | `Reflection Probe Lower`（Realtime, OnAwake, 256, HDR）；赛道与结算场地板用平面反射 | 探针包围盒 1760 × 137 × 1900，整张图 | 银色 Buddha（`SilverSkin.shadergraph`，metal/smooth 贴图）主要靠它；它反射的是橙色天空 |
| 后期 | `Assets/Scenes/RaceMap/RaceMap_Dusk_Profile.asset`（场景 `Volume`，priority 1） | ACES；ColorAdjustments exposure +0.05、contrast 14、colorFilter (1, 0.94, 0.84)、saturation 8；WhiteBalance 22 / −8；Bloom 0.7、阈值 0.95、scatter 0.72、tint (1, 0.78, 0.55) | 管线 `URP-HighFidelity` 调色模式 LDR。`Volume_Dark Profile`（priority 0，黑幕用）还是旧 sunset 值：exposure +0.2、contrast 10、sat 5、WB 4 / −3、Bloom 0.5 / 0.6，和黄昏 profile 不一致 |

另两盏方向光（强度 0.31 白光无阴影、强度 0）对颜色影响可忽略；`RaceMap_Environment_Profile.asset` 与 `RaceMap_Sky.mat` 是黄昏前版本，菜单仍共用，本研究不碰。

## 2. 量化方法

工具：`Assets/Editor/Rendering/RaceMapDuskColorStudy.cs`（本次新增的研究探针，调用方式与 token 列表在文件头）。它复用 `RaceMapViewpointCapture.Viewpoints` 的五个固定视角，但有三点不同：

1. **渲染进 HDR 缓冲再转 sRGB 读回**，和游戏一致（旧工具是 8-bit 目标，README "How to compare" 的第一个 caveat）。LDR 抓图的差别见第 4 节末。
2. **代理模型放在射线命中的地面 + 胶囊体离地高度上**，不再用旧的 track y 2.62（第二个 caveat）。
3. 所有改动都是**运行时覆盖**，抓完即恢复：太阳、雾、环境光直接改 RenderSettings；天空换成临时材质副本；后期用 `Volume.profile` 的深拷贝（见第 10 节的坑）；反射探针可换成临时的中性灰渐变立方图；每次抓完重新渲染环境探针与实时反射探针。

抓图之后用 Python（Pillow + numpy，脚本在会话 scratchpad，未入库）统计：

- 全图：sRGB 均值、CIE Lab 均值与色度 C\* = √(a\*² + b\*²)、HSV 平均饱和度、色相直方图（12 × 30°，按 S·V 加权）、"暖色占比"（色相 15–60° 且 S > 0.2 的像素比例）、圆形色相标准差（S > 0.1 的像素）、用 McCamy 公式从平均色估的相关色温 CCT。
- 分区：用一张**材质 ID 遮罩**（每个 Renderer 临时换成 Unlit 纯色，16 级/通道编码材质序号，天空为 0，代理模型为 4095）切出赛道 `RaceMapZen_Lane`/`RaceMap_Lane`、路缘 `RaceMap_Curb`、沙地 `RaceMapZen_Ground`、Buddha、天空。
- 对比度：赛道 vs 路缘取相邻 24 px 带内的像素，WCAG 对比度（相对亮度 (L1+0.05)/(L2+0.05)）与 ΔE76；Buddha vs 赛道取 Buddha 外扩 80 px 范围内的赛道；天空 vs 沙地（开场镜头）。
- 特效：同一视角抓"有特效"和"无特效"两张，ΔE > 6 的像素算特效像素，统计平均 ΔE、ΔE > 20 的像素数、特效像素处的亮度比（WCAG）和 Lab 均值。特效在编辑模式里用 `ParticleSystem.Simulate` 逐帧步进到指定时间（禅定缓流 3 s、通用前摇 1 s、神足通 0.2 s），层脚本不运行。

注意：游戏态核对过——play mode 里用同一工具抓的五张图与编辑模式抓图数值一致（第 4 节表里 `dusk_play2` 行），所以下面的数字可以当作游戏里的数字。

## 3. 现状数值

全图（`dusk_now`，= 游戏态 `dusk_play2`）：

| 视角 | L\* | a\* | b\* | 色度 C\* | 饱和度 | 暖色占比 | 色相标准差 | CCT |
|---|---|---|---|---|---|---|---|---|
| 01 起跑格 | 36 | 21 | 24 | 33 | 0.64 | 62% | 7° | 2428 K |
| 02 直道 | 49 | 18 | 23 | 31 | 0.50 | 51% | 18° | 3024 K |
| 03 急弯 | 55 | 22 | 30 | 38 | 0.56 | 73% | 7° | 2766 K |
| 04 开场 | 52 | 20 | 33 | 39 | 0.61 | 81% | 8° | 2845 K |
| 05 结算场 | 47 | 21 | 24 | 33 | 0.54 | 53% | 7° | 2728 K |

色相直方图：起跑格 98.2% 的加权色相在 0–30°，急弯 86%，开场 66.5%（另 33% 在 30–60°）。换句话说整张图只有一个色相。

分区与对比度（起跑格 / 开场）：

| 区域 | sRGB 均值 | Lab | 色度 | 色相 |
|---|---|---|---|---|
| 赛道（01） | (192, 122, 87) | 57.7 / 23.7 / 30.2 | 38.9 | 18° |
| 路缘（01） | (70, 27, 13) | 16.0 / 19.1 / 18.4 | 26.6 | 15° |
| 沙地（01） | (96, 47, 26) | 25.4 / 20.5 / 22.3 | 30.4 | 15° |
| Buddha（01） | (162, 97, 54) | 47.5 / 22.8 / 35.4 | **42.6** | 23° |
| 天空（04） | (233, 173, 100) | 74.9 / 14.0 / 45.3 | 47.6 | 33° |

| 对比 | 01 | 02 | 03 | 04 | 05 |
|---|---|---|---|---|---|
| 赛道 vs 路缘 WCAG | 5.03 | 2.35 | 4.11 | 6.13 | – |
| Buddha vs 赛道 WCAG / ΔE | 1.11 / 10.6 | 1.50 / 23.9 | 1.02 / 4.3 | 1.00 / 17.3 | 4.08 / 41.0 |
| 天空 vs 沙地 ΔE | – | – | – | 40.0 | – |

赛道和路缘的明度对比其实够用（除了直道 2.35）；问题在 Buddha：在三个比赛视角里它和赛道的亮度比是 1.0–1.1，色相又一样，只剩 ΔE 4–11，急弯视角几乎融进赛道。

特效（起跑格，现状）：

| 特效 | 特效像素 | ΔE 均值 | 亮度比 | 特效 Lab | 背景 Lab |
|---|---|---|---|---|---|
| 禅定缓流墨池 3 s | 430 k | 19.5 | 1.79（更暗） | 32 / 17.5 / 26.6 | 47.5 / 23.0 / 24.1 |
| 神足通释放 0.2 s（加法闪光） | 27 k | 16.0 | 1.38 | 58 / 16 / 31 | 49 / 24 / 27 |
| 通用前摇 1 s（金色光柱 + 火星） | 96 k | 15.3 | 1.11 | 57 / 13.5 / 33 | 55.5 / 23.7 / 29.9 |

墨池和背景的 a\*/b\* 相同，只靠暗；金光和背景的 b\* 相同，只靠 a\* 低 10。如来神掌出掌（`VFX_Palm_Release`）在编辑模式里步进后粒子都在（最大层包围盒 300 m）但画不出来，原因没查到（第 8 节），所以加法发光用通用前摇和神足通代替。

## 4. 逐层关掉暖色源

每次只中性化一层（太阳 = 关色温、滤镜白；雾 = 关掉；天空 = 所有颜色换成同亮度灰 + 环境渐变灰；探针 = 中性灰渐变立方图；后期 = 滤镜白、白平衡 0/0、Bloom tint 白），其余不动。起跑格 / 开场两个视角：

| 变体 | 色度 01 | b\* 01 | CCT 01 | 色相标准差 01 | 色度 04 | CCT 04 | Buddha 色度 01 | Buddha–赛道 ΔE 01 |
|---|---|---|---|---|---|---|---|---|
| 现状 | 33 | 24 | 2428 K | 7° | 39 | 2845 K | 42.6 | 10.6 |
| 太阳中性 | **18** | 16 | 4064 K | 10° | **29** | 3623 K | 37.5 | 22.2 |
| 雾关掉 | 31 | 23 | 2450 K | 10° | 38 | 2860 K | 42.1 | 10.6 |
| 雾同亮度灰 | 30 | 22 | 2501 K | 8° | 37 | 2907 K | 41.5 | 10.4 |
| 天空 + 环境光中性 | 29 | 22 | 2798 K | 7° | 31 | 3490 K | 42.5 | 15.1 |
| 反射探针中性灰 | 31 | 23 | 2491 K | 7° | 38 | 2863 K | **15.3** | **22.5** |
| 后期中性 | **24** | **12** | 3108 K | 19° | 25 | 3681 K | 27.8 | 13.2 |
| 后期整个关掉 | 22 | 10 | 3439 K | 19° | 25 | 3658 K | 21.0 | 10.6 |
| 四层全部中性 | 5 | −4 | 6921 K | 36° | 5 | 6441 K | 10.0 | 16.8 |

五个视角的完整表在 [dusk_study/](dusk_study/) 的图里可以对照看（`ablation_views.jpg` 是直道 / 急弯 / 结算场）。每层对"发黄"的贡献（起跑格色度，单层去掉的下降量）：

| 层 | 色度下降 | 占现状色度 | 说明 |
|---|---|---|---|
| 太阳（2900 K × 橙滤镜） | 15 | 45% | 最大来源，也是唯一会同时把亮部和阴影都染橙的来源（阴影靠的是同样橙的雾与环境光，没有冷色补） |
| 后期（滤镜 + WB 22 + 暖 Bloom） | 9 | 27% | 全画面均匀加暖，特效一起被染；b\* 从 24 降到 12 说明它是"黄"的主要来源，太阳更多是"红橙" |
| 天空 + 环境光 | 4（开场 8） | 12% | 在看得见天的镜头里是第二大来源；它还决定了探针，也就是 Buddha 的颜色 |
| 雾 | 2 | 6% | 比赛视角几乎不贡献，开场镜头的地平线是它和天空的接缝 |
| 反射探针 | ≈0（全图） | – | 对全图没影响，对 Buddha 决定性：色度 42.6 → 15.3 |

四层全去掉后剩 5 是材质本身的颜色（沙、瓦、木），说明资产本身并不暖。

LDR 抓图（旧 `RaceMapViewpointCapture` 的方式）和 HDR 抓图比：全图色度 32–38 对 33–39 差 1 以内，但 Buddha 色度 37 对 42.6、开场 41 对 50.8，高光被截掉后金色显得没那么重——旧工具会低估 Buddha 的问题。

## 5. 参考研究

### 5.1 真实黄昏的色彩结构

- 黄金时刻与日落时直射阳光的色温大约在 2000–3000 K（阳光穿过更厚的大气，蓝光被散射掉）；同一时刻天空散射光仍是蓝的，日落后到蓝调时刻为 9000–12000 K。所以真实黄昏是**暖光 + 冷填充**：被太阳照到的面是橙的，阴影和背光面被天空照，是蓝到紫的；朝向太阳的地平线最暖，背向太阳的地平线偏粉紫（地影与维纳斯带），天顶始终是蓝的。
- 相机的白平衡会把整体往暖拉，但也只是让中性灰偏暖；画面里仍然同时存在暖和冷两个方向。现状场景里没有任何方向是冷的（色相标准差 7°），这是它读成"滤镜"而不是"时间"的原因。
- 摄影后期的常见做法是分区调色（split toning / 色轮）：高光加暖、阴影加冷（青或蓝），而不是整体加一层暖滤镜。

来源：[glass.photo 的 golden hour 词条](https://glass.photo/photography-terms/golden-hour)、[PhotographyTalk：Golden Hour vs Blue Hour](https://www.photographytalk.com/golden-hour-vs-blue-hour-when-to-shoot-what-in-landscape-photography/)、[Adobe：What is golden hour](https://www.adobe.com/ie/creativecloud/photography/discover/golden-hour.html)、[Darkroom：Use Color Grading for Mood](https://darkroom.co/help/guide/color-grading-mood)。这些是摄影向的通俗资料，数值级别够用，不是色彩科学文献。

### 5.2 游戏里的做法

- 时间段靠**关系**而不是靠滤镜读出来：暖的主光、冷的补光（填充光约在主光 90° 外、取互补色）、高光与阴影的冷暖对立；"如果所有东西都同样清楚，就减少补光或加大主光/补光对比"（[Game Developer：Lighting design fundamentals, colored light](https://www.gamedeveloper.com/design/lighting-design-fundamentals-how-and-where-to-use-colored-light)、[using contrast](https://www.gamedeveloper.com/art/lighting-design-fundamentals-using-contrast-in-your-game)）。
- 竞速游戏的日落关：Forza Horizon 系列的天空来自实拍 HDR 延时（FH3 的 GDC 2017 分享、FH5 的 400 小时天空素材），橙色地面反弹到车身是美术方向的一部分，但天顶和阴影保持冷色，车漆的识别靠的正是冷天空的反射（[Game Developer 报道](https://gamedeveloper.com/business/importing-real-australian-skies-into-i-forza-horizon-3-i-)、[Ben Penrose 访谈](https://geekculture.co/?p=31804)）。没有找到 Trackmania / Mario Kart 的公开技术资料，这里不引用。
- 可落地的通用手法（按优先级）：
  1. 把暖色放进光里（太阳色温/滤镜），把冷色放进天空、环境光和阴影里，后期只做微调。
  2. 按亮度分区调色（URP 自带 Shadows/Midtones/Highlights 或 Lift/Gamma/Gain，SplitToning），而不是 colorFilter + WhiteBalance 这种整体平移。
  3. 雾的颜色按天空校准（README checklist 第 2 步）并降低密度；远处用雾分层，近处不染色。
  4. 反射来源：实时探针反射的是天空，所以天空定了 Buddha 的颜色；如果要 Buddha 永远读成银，可以给它一张独立的中性 cubemap（`SilverSkin.shadergraph` 里采样）或提高 Fresnel 边缘的中性反射，但会和场景脱节（第 9 节的 C 方案就是这个效果）。
  5. HDR 调色模式：ACES 在 LDR 模式下先 tonemap 再查 LUT，饱和的橙在 ACES 里被压得更黄；HDR 模式反过来让现状**更**饱和（起跑格色度 33 → 37、开场 39 → 43，`grading_mode.jpg`），对 A2 影响小（16 → 16）。它是所有场景共用的管线资产改动，本轮不建议动。

### 5.3 特效层是否受后期影响

`URP-HighFidelity-Renderer.asset` 的四个 Feature 全部在 `RenderPassEvent.AfterRenderingTransparents`（500）：RenderBuddah（Opaque 队列，Buddah 层）、RenderSkillVfx（Transparent 队列，Buddah 层）、RenderMapEdge（层 8）、RenderTrails（层 7）；黑幕 Feature 在 `BeforeRenderingTransparents`。后期（调色 LUT、Bloom）在这之后，所以**所有技能特效、拖尾、边缘线都被 WhiteBalance / colorFilter / 饱和度处理，并参与 Bloom**。三个技能插槽 prefab（如来神掌、禅定缓流、神足通）的全部物体都在 Buddah 层（6），由 RenderSkillVfx 绘制。`Assets/Rendering/Shaders/FX/*` 的手写 shader 都开了 `multi_compile_fog`，加法发光用 `MixFogColor(…, 0)` 正确地只衰减不染色；Piloto 的 `UberFXSG` 图里有一个 Fog 节点，没有核对它是否接入输出。

结论：要让特效"清晰可见"，不能只改特效，先把后期的整体暖移去掉；之后特效自己的颜色（仪玄色板的墨 / 金 / 白）才能和背景分出色相。

## 6. 候选方案

三个方案都只改四个运行时数值源：`MainLight`、RenderSettings 雾、`RaceMap_Sky_Dusk.mat`、`RaceMap_Dusk_Profile.asset`。共同代价与约束：

- **不需要 bake**：太阳、雾、后期是运行时值；天空改动按 checklist 应重 bake 环境探针，但用户已定"地图没做完不 bake"，光照探针现在就是旧 bake，三个方案都和现状在同一前提下比较。实时反射探针会在场景加载时按新天空重新渲染。
- **菜单场景不受影响**：改的是 RaceMap 自己的 `_Dusk` 副本，`RaceMap_Sky.mat` 和 `RaceMap_Environment_Profile.asset` 不动。
- **Feel shaker**：profile 里保留 WhiteBalance 和 Bloom 组件，只改值；Bloom 强度 0.7、阈值 0.95 不动，技能闪光依赖它。
- `Volume_Dark Profile` 应同步成新 profile 的值再减 0.1 EV（checklist 第 4 步），三个方案都要做这一步。
- 开场镜头的地平线接缝（README Known limits）：雾色换了之后要按 checklist 第 2 步重校一次，候选图里的雾色是手估的。

### A2 金时暖光·冷影（推荐）

暖色只留在太阳里，天空恢复冷天顶，后期从"整体加暖"改成"阴影冷、高光暖"。

| 资产 | 现状 → A2 |
|---|---|
| `MainLight` | 色温 2900 → **3400 K**；滤镜 (1, 0.80, 0.52) → **(1, 0.93, 0.85)**；强度 3.6 → **3.4** |
| 雾 | 密度 0.0011 → **0.0008**；颜色 (0.96, 0.68, 0.40) → **(0.95, 0.76, 0.60)**（之后按 checklist 对天空重校） |
| `RaceMap_Sky_Dusk.mat` | horizon exponent 2.4 → **5**；zenith (0.16, 0.18, 0.38) → **(0.14, 0.20, 0.48)**；horizon (0.98, 0.55, 0.28) → **(1.0, 0.60, 0.38)**；horizon-sun (1.7, 0.58, 0.16) → **(1.8, 0.70, 0.28)**；ground (0.55, 0.30, 0.16) → **(0.45, 0.32, 0.24)**；glow (1.1, 0.38, 0.08) → **(1.2, 0.55, 0.20)**；其余不变 |
| `RaceMap_Dusk_Profile.asset` | colorFilter → **白**；WhiteBalance 22 / −8 → **0 / 0**；saturation 8 → **5**；contrast 14 → **12**；Bloom tint (1, 0.78, 0.55) → **(1, 0.88, 0.72)**；**新增 ShadowsMidtonesHighlights**：shadows (0.90, 0.94, 1.12)、midtones (1, 1, 1)、highlights (1.10, 1.02, 0.92) |
| 反射探针 | 不动（实时，反射新天空） |

### B2 黄昏分色

比 A2 更晚、更"暮"：太阳更暖更弱，天空偏紫，阴影用 SplitToning 推蓝。

| 资产 | 现状 → B2 |
|---|---|
| `MainLight` | 色温 **3200 K**；滤镜 **(1, 0.90, 0.78)**；强度 **3.0** |
| 雾 | 密度 **0.0009**；颜色 **(0.82, 0.70, 0.68)** |
| `RaceMap_Sky_Dusk.mat` | exponent **5**；zenith **(0.14, 0.18, 0.45)**；horizon **(0.92, 0.56, 0.48)**；horizon-sun **(1.7, 0.64, 0.24)**；ground **(0.40, 0.30, 0.30)** |
| `RaceMap_Dusk_Profile.asset` | colorFilter 白；WhiteBalance **3 / 0**；saturation 5；contrast 12；Bloom tint (1, 0.88, 0.70)；**新增 SplitToning**：shadows (0.55, 0.65, 0.85)、highlights (1, 0.88, 0.70)、balance −15 |

### C2 最小改动

保留 2900 K 的橙阳和现在的天空结构，只去掉"过量"的那部分：滤镜叠加、白平衡 22、Bloom 着色。

| 资产 | 现状 → C2 |
|---|---|
| `MainLight` | 色温不变 2900 K；滤镜 → **(1, 0.92, 0.82)**；强度不变 |
| 雾 | 密度 **0.0009**；颜色 **(0.93, 0.72, 0.52)** |
| `RaceMap_Sky_Dusk.mat` | exponent **4**；zenith **(0.16, 0.20, 0.44)**；其余不变 |
| `RaceMap_Dusk_Profile.asset` | colorFilter 白；WhiteBalance **10 / −4**；saturation **4**；Bloom tint **(1, 0.90, 0.78)**；contrast 不变 |

### 候选数值

全图与分区指标（HDR 抓图；完整五视角原始表在会话记录里，这里列起跑格 01 与开场 04，另给其余视角的 Buddha 列）：

| 方案 | 视角 | 色度 C\* | b\* | 色相标准差 | CCT | 赛道/路缘 WCAG | Buddha 色度 | Buddha–赛道 ΔE | 天空–沙地 ΔE |
|---|---|---|---|---|---|---|---|---|---|
| 现状 | 01 | 33 | 24 | 7° | 2428 K | 5.03 | 42.6 | 10.6 | – |
| 现状 | 04 | 39 | 33 | 8° | 2845 K | 6.13 | 50.8 | 17.3 | 40.0 |
| A2 | 01 | **16** | 3 | **32°** | 4343 K | 5.98 | **22.7** | 15.0 | – |
| A2 | 04 | **17** | 5 | 30° | 4670 K | 7.11 | 27.7 | 23.5 | 33.3 |
| B2 | 01 | 16 | −4 | 42° | 5218 K | 5.08 | 18.0 | 11.3 | – |
| B2 | 04 | 16 | −1 | 38° | 5376 K | 6.25 | 21.9 | 20.9 | 29.9 |
| C2 | 01 | 21 | 13 | 19° | 3391 K | 5.35 | 28.9 | 13.6 | – |
| C2 | 04 | 22 | 16 | 15° | 3843 K | 6.66 | 34.0 | 21.4 | 33.3 |

| Buddha 色度 / Buddha–赛道 ΔE | 02 直道 | 03 急弯 | 05 结算场 |
|---|---|---|---|
| 现状 | 37.6 / 23.9 | 35.9 / 4.3 | 41.5 / 41.0 |
| A2 | 20.8 / 26.8 | 19.9 / 6.2 | 33.4 / 42.2 |
| B2 | 16.5 / 25.5 | 16.5 / 4.5 | 27.4 / 42.7 |
| C2 | 24.9 / 24.1 | 22.8 / 4.5 | 33.1 / 39.4 |

读法：A2 和 B2 把全图色度压到现状的一半，并且第一次出现了 30° 以上的色相分布（有冷有暖）；C2 只减了三分之一，色相仍然集中。Buddha 的色度 A2 降到 23（银带一点天空暖反射），B2 降到 18，C2 还有 29（反射的天空仍偏橙）。**急弯视角里 Buddha 和赛道的 ΔE 三个方案都只有 4–6**：那里 Buddha 被太阳正面照亮、亮度和赛道一样，这是调色解决不了的，要靠材质/光（边缘 Fresnel、接触阴影、或赛道基色）——列入未验证项。赛道/路缘对比在三个方案里都持平或略升，直道仍是 2.4 左右。

特效可见性（起跑格 01，特效像素相对无特效帧；Δa\*/Δb\* = 特效 − 背景）：

| 特效 | 现状 ΔE / 亮度比 / Δa\*, Δb\* | A2 | B2 | C2 |
|---|---|---|---|---|
| 禅定缓流墨池 3 s | 19.5 / 1.79 / −5.5, +2.5 | **23.1 / 2.14** / −2.7, +2.8 | 19.8 / 1.86 / −3.9, +1.5 | 21.6 / 1.96 / −3.9, +5.7 |
| 通用前摇金光 1 s | 15.3 / 1.11 / −10.2, +3.3 | 14.2 / 1.06 / −6.4, **+6.0** | 14.7 / 1.16 / −8.0, **+6.7** | 14.7 / 1.10 / −8.4, +4.9 |
| 神足通闪光 0.2 s | 16.0 / 1.38 / −8.0, +3.5 | 16.7 / 1.43 / −6.2, +5.1 | 15.6 / 1.39 / −6.2, +6.1 | 15.3 / 1.42 / −7.0, +2.2 |

读法：墨池在 A2 里和赛道的亮度比从 1.79 升到 2.14，自身 Lab 从 (32, 17.5, 26.6) 变成 (32, 10.8, 5.2)——读成墨黑而不是棕；金光和闪光的 ΔE 数值基本不变（14–17），但分离方向从"比背景不那么红"（Δa\*）变成"比背景黄"（Δb\*），也就是它们在 A2/B2 下读成金色。特效本身的颜色是仪玄色板定的，调色只能还它们一个中性的背景，不会把 ΔE 翻倍；要更显眼还得在特效端加亮度差（README 第 2 节的"亮度顺序"）。

## 7. 推荐

**A2。** 它和 B2 都把全图色度降到现状的一半、让色相标准差从 7° 升到 30° 以上（有冷有暖）、让 Buddha 回到银色、让墨池读成黑而不是棕；A2 比 B2 保住了更多太阳的暖意（b\* 3 对 −4，CCT 4300 K 对 5200 K），赛道与沙地的明度差也更大；太阳仍是低角度的暖光，开场镜头的地平线仍是橙的，所以还是日落。它也是改法最"物理"的：之后再调时间（更早/更晚），只要动太阳和天空两个地方。

B2 适合用户觉得 A2"太像下午"的情况——它把整个场景往暮色推，代价是赛道和沙地都偏紫，比赛视角里赛道/沙地的明度差更小。C2 适合用户想保留现在这种"橙红日落"情绪的情况，但它只解决了一半：Buddha 还是金的（反射的天空还是橙的），特效和背景仍是同一色相。

## 8. 未验证项

- 候选图是编辑器 HDR 抓图，没有在比赛里看过；运动中的颜色、黑幕（`Volume_Dark`）切换、技能闪光的 Bloom 都没有验证。
- 雾色是手估的，没有做 checklist 第 2 步的三轮校准；开场镜头的地平线接缝要在实现时处理。
- 如来神掌出掌 `VFX_Palm_Release` 在编辑模式步进后不显示（粒子数和包围盒都正常）；神足通只在 0.2 s 有少量像素。原因没查，可能和 `UberFXSG` 的软粒子/深度或粒子渲染器在编辑模式的更新有关。特效结论来自禅定缓流和通用前摇两个插槽。
- 环境球谐 L0 (0.285, 0.173, 0.127) 来自哪里（Trilight 渐变还是旧 bake 的环境探针）没核实；如果是旧 bake，正式 bake 之后阴影会更冷一些。
- HDR 调色模式只抓了图没有做评审；SMH / SplitToning 的具体数值是第一版估值，实现时还要在比赛镜头里微调。
- Balanced / Performant 管线没有 RenderSkillVfx 等 Feature（README Known limits），这里的"特效走后期"结论只对 High Fidelity 核过。

## 9. 第一轮候选为什么弃用

[candidates_round1.jpg](dusk_study/candidates_round1.jpg)：A（3800 K + 滤镜近白 + 饱和度 0）读成阴天下午，太阳的存在感没了；B 和 B2 接近；C 用了"反射探针换中性灰"这一招，Buddha 立刻变成青灰色、和场景脱节，而天空仍是整片橙色，说明**探针是 Buddha 的开关，但不是场景的开关**。如果以后要"Buddha 永远是银的"，应该在 `SilverSkin.shadergraph` 里给它一张独立 cubemap，而不是动场景探针。

## 10. 这次踩的坑（给后来者）

- `Object.Instantiate(volumeProfile)` 是**浅拷贝**：`components` 列表仍指向资产里的子对象，覆盖参数会直接改到 `RaceMap_Dusk_Profile` 的内存副本（磁盘不变，但进出 play mode 和 domain reload 都不会复原，直到重启编辑器）。第一轮消融因此全部带着"后期已中性化"的状态，被 play mode 对照发现后重做。正确做法是 `volume.profile = null; var clone = volume.profile;`（URP 自己深拷贝），用完 `volume.profile = null` 并销毁克隆的组件。探针脚本已经改成这样。
- 用 Bash heredoc 往 `.cs` 里写 `"\n"` 会变成真实换行（"Newline in constant"），改用 Write/Edit 工具。
- 编辑器向 MCP 服务注册需要真正在前台：`AppActivate` 返回 True 但无效，`SetForegroundWindow` 之前先发一个 ALT 键才生效。
- 编辑模式抓图与 play mode 抓图一致的前提是先 `DynamicGI.UpdateEnvironment()` + 实时探针 `RenderProbe()`（`RaceMapDuskColorStudy.RefreshNow()`）；这次两者本来就一致，但下一次改了天空再抓之前仍要刷新。

## 11. 实现（用户选 C2，2026-11-03）

用户看过第 6 节的三张候选后选了 C2（"C2 最佳"）。已写入资产并分四个小提交：

| 提交 | 内容 |
|---|---|
| e3530406 | `RaceMap.unity`：MainLight 滤镜 (1, 0.80, 0.52) → (1, 0.92, 0.82)，2900 K 不变；雾密度 0.0011 → 0.0009，颜色 (0.96, 0.68, 0.40) → **(1.263, 0.967, 0.612)** |
| e58def96 | `RaceMap.unity`：编辑器保存时顺带清掉的过期序列化字段（`debugLabel`、`resultSceneName`），与本改动无关，单独提交 |
| ad27da69 | `RaceMap_Sky_Dusk.mat`：horizon exponent 2.4 → 4，zenith (0.16, 0.18, 0.38) → (0.16, 0.20, 0.44)，ground = 雾色的线性值 (1.671, 0.927, 0.333) |
| a49af48f | `RaceMap_Dusk_Profile.asset`：colorFilter → 白，WhiteBalance 22 / −8 → 10 / −4，saturation 8 → 4，Bloom tint (1, 0.78, 0.55) → (1, 0.90, 0.78)；`Volume_Dark Profile.asset` 同步为 exposure −0.05、contrast 14、saturation 4、WB 10 / −4 |

和第 6 节 C2 候选的区别只有雾色：候选图用的是手估的 (0.93, 0.72, 0.52)，实现时按 README checklist 第 2 步对天空校准（开场镜头 284–292 行天空 vs 298–302 行全雾远沙，32 列平均，每轮乘 (sky/band)^1.2，同时把天空的 ground 颜色设成雾色线性值），10 轮后差 4.8 级（1b 阶段做到 1.6 级；这次收敛慢是因为 ground 颜色跟着雾走、天空下沿也在变）。校准后地平线比候选图更淡、更接近天空，是正确的接缝处理，但观感上"远处发白"比候选图明显——如果用户想要候选图那种更橙的远景，把雾色拉回 (1.05, 0.80, 0.56) 左右并接受地平线有一条暗带即可。

实现后的数值（`c2_after`，编辑器 HDR 抓图，与候选 C2 几乎一致）：

| 视角 | 色度 C\* | b\* | 色相标准差 | CCT | 赛道/路缘 WCAG | Buddha 色度 | Buddha–赛道 ΔE |
|---|---|---|---|---|---|---|---|
| 01 起跑格 | 33 → **21** | 24 → 14 | 7° → 16° | 2428 → 3403 K | 5.03 → 5.08 | 42.6 → **26.5** | 10.6 → 10.5 |
| 02 直道 | 31 → 20 | 23 → 10 | 18° → 43° | 3024 → 4181 K | 2.35 → 2.35 | 37.6 → 22.1 | 23.9 → 23.5 |
| 03 急弯 | 38 → 22 | 30 → 15 | 7° → 17° | 2766 → 3874 K | 4.11 → 4.22 | 35.9 → 20.4 | 4.3 → 3.5 |
| 04 开场 | 39 → 23 | 33 → 16 | 8° → 15° | 2845 → 3848 K | 6.13 → 6.12 | 50.8 → 31.2 | 17.3 → 18.8 |
| 05 结算场 | 33 → 19 | 24 → 12 | 7° → 18° | 2728 → 3993 K | – | 41.5 → 33.0 | 41.0 → 38.8 |

特效（起跑格）：禅定缓流墨池 ΔE 19.5 → 23.0、亮度比 1.79 → 2.03，自身 Lab (32, 17.5, 26.6) → (33, 12.5, 18.8)；通用前摇金光 ΔE 15.3 → 14.4，Δb\* +3.3 → +4.8。

证据：[dusk_c2_compare/all.jpg](dusk_c2_compare/all.jpg)（五视角 before/after）、[dusk_study/c2_before_after_01.gif](dusk_study/c2_before_after_01.gif)、[dusk_study/c2_before_after_04.gif](dusk_study/c2_before_after_04.gif)、[dusk_study/c2_vfx.jpg](dusk_study/c2_vfx.jpg)。

没做的：没有 bake（用户决定）；没在比赛里看；急弯视角 Buddha 对赛道的 ΔE 仍是 3.5（第 8 节）；A2/B2 的数值保留在第 6 节，随时可以换。

## 12. 菜单共用与反射探针（2026-11-03）

- **菜单（cd6f8f47）**：`MenuEnvironmentSetting.unity` 改用 `RaceMap_Sky_Dusk.mat`、`RaceMap_Dusk_Profile.asset`、RaceMap 的 Trilight 环境光和校准后的雾；太阳保留菜单自己的方向和强度 1.4，颜色改成和 RaceMap 一样的 2900 K + (1, 0.92, 0.82)。`MenuEnvironmentImporter` 写同样的值。整帧 L\* 36 → 28（黄昏 profile 曝光 +0.05，旧的是 +0.3），没有补偿。对比：[menu_dusk_compare/all.jpg](menu_dusk_compare/all.jpg)。
- **RaceMap 反射探针（96f5fbbb）**：`Reflection Probe Lower` 从实时改成 `Lightmapping.BakeReflectionProbe` 烘出的 `ReflectionProbe-Lower.exr`（Custom 模式）；新增 `Reflection Probe EndField`（600 × 120 × 600，中心 (1493, 40, −684)）覆盖原来探针盒子没包到的结算场；景观和赛道的 368 个渲染器标成 Reflection Probe Static。起跑格 Buddha 色度 26.5 → 28.4（探针里多了橙色沙地），直道 / 急弯 Buddha–赛道 ΔE 23.5 → 28.2 / 3.5 → 10.5，结算场 38.8 → 29.5。对比：[dusk_refl_compare/all.jpg](dusk_refl_compare/all.jpg)。
- **菜单反射探针（be22c51a）**：`Reflection Probe Shrine`，包围整个神社环境 + 10 m，`MenuEnvironmentSetting/ReflectionProbe-Shrine.exr`，导入器重建时会复用。对比：[menu_refl_compare/all.jpg](menu_refl_compare/all.jpg)（主镜头里变化很小，受益的是银色 Buddha 和光滑道具）。
- **灯光烘焙没有做成**：用户 2026-11-03 要求烘焙，但 Progressive Lightmapper 的 GPU 和 CPU 后端都报 `out of memory`——系统提交内存 51 / 54 GB、可用 0 MB（编辑器进程 7.7–10.7 GB，重启编辑器后也一样），连只烘两块旧平面都失败；另外禅意景观与赛道的 FBX 没有 lightmap UV（`uv2` 为空），要让它们参与 GI 得先在导入设置里开 Generate Lightmap UVs 并给低 Scale in Lightmap。取消烘焙会把场景的 LightingData 引用清掉（光照探针消失、整帧回暖 +10 色度），已用 `Lightmapping.lightingDataAsset` 挂回旧的 bake。需要的条件：关掉其他大内存应用或重启后，在 Lighting 窗口 Generate Lighting；之后把两个 RaceMap 探针改回 Baked（或继续用 Custom）。
