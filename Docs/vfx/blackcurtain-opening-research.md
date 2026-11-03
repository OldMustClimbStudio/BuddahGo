# 一念光明（`blackcurtain`）开场特效调研

调研日期 2026-10-31，同日两轮。只调研、出方案，不改资产和代码；用户选定方案后再实现。

**问题。** 用户认可黑幕开启后保持全黑、看不到赛道。不满意的是开启那一刻：现在只有一个屏幕扭曲。

**第二轮的前提（用户 2026-10-31）。** 原话："只不过我想要保留那个扭曲黑洞一样那个特效 只不过目前的表现可能在表现力角度不太足够 比较廉价"。所以开场的核心仍是现在这个扭曲黑洞，不换成别的；要做的是让它有分量。第一轮那五个"换掉黑洞"的方向保留在[附录](#附录第一轮的五个方向)，其中的墨晕、墨烟聚拢等改作黑洞的收尾或叠加层。

**用户选定（2026-11-02）。** 原话："用 H1 墨眼"，并要求先出一个能看的实例。原型的做法、默认值和打开方式见[第 7 节](#7-h1-墨眼原型2026-11-02)。

**范围。** 只看"从正常画面到全黑"这一段，也就是配表的展开时长（`expandDurationSeconds`，现在 1 s）。保持（10 s）、淡出（2 s）、黑幕里的路线拖尾（`curtain_hit`）、赛道边缘发光都不动。

相关文档：
- [README.md](README.md)：规格、配色、插槽与时长规则。
- [slots.md](slots.md)：`curtain_release` / `curtain_hit` 插槽。
- [yixuan-palette.md](yixuan-palette.md)：正向配色。
- [anti-palette.md](anti-palette.md)：反噬配色。
- [yingyang-vfx-analysis.md](yingyang-vfx-analysis.md)：Piloto 包拆解。

可视化对比：[一念光明开场对照](https://claude.ai/artifact/B8YyEQqQ4BH6EECykXBbnN)（私有 artifact）。页面上半部分是第二轮的现状和 H1–H3，下半部分是第一轮的 A–E。都是 2D 动态示意，不是游戏截图；可以切换正向 / 反噬配色、慢放、拖动进度。

## 1. 现状（读代码和资产得到，2026-10-31）

### 1.1 谁在画什么

| 部分 | 位置 | 做什么 |
|---|---|---|
| 技能 | `Assets/Scripts/Buddah/ComboSkill/Skill_BlackCurtain/Skill_BlackCurtain.cs`、`Skill_BlackCurtain_Anti.cs` | 服务器只写感知状态（`SkillPerceptionState.ApplyCurtain`）。表现全在 `ExecuteObservers` 里，每个客户端各自播：先在施法者身上播 `curtain_release`（所有人都看得到），再调 `BlackCurtainPresentation.TryPlay` |
| 分配视角 | `BlackCurtainPresentation.cs` | `localShouldSeeEdge = localIsCaster ^ isAnti`：正向时施法者看得见赛道边缘，其他人是受害者；反噬反过来。圆心 `_Center` = 施法者位置（+1 m）在本地相机里的视口坐标，只在开始时算一次；施法者在相机后面时取画面中心 |
| 计时与开关 | `BlackCurtainViewController.cs`（挂在主相机上） | 安排赛道边缘的淡入淡出、`Volume` / `Volume_Dark` 的切换。受害者从 0 s 起隐藏其他玩家，到淡出开始时恢复。所有玩家身上播 `curtain_hit` 路线拖尾 |
| 全屏材质驱动 | `BlackCurtainScreenEffect.cs` | 每帧写 `_Expansion`、`_Opacity`、`_ElapsedTime`、`_EffectActive`、`_Center`、`StrengthBlackHole` |
| 全屏 shader | `Assets/VFX/一念光明/SG_BlackCurtain.shadergraph`（GUID `542df486…`；渲染器和两个技能 SO 引用的都是它） | URP Fullscreen Shader Graph，四个节点组：`空间坍缩`（扭曲黑洞）、`MainCurtainMask` / `Radius`（黑幕遮罩）、`FlowOffset`（黑幕内的流动噪声）。拆解见第 2 节 |
| 渲染器 | `Assets/Settings/URP-HighFidelity-Renderer.asset` 的 `BlackCurtain`（Full Screen Pass，注入点 BeforeRenderingTransparents，Requirements = Color） | 先画黑幕；之后 `RenderMapEdge`、`RenderTrails`、`RenderBuddah`、`RenderSkillVfx` 在 AfterRenderingTransparents 画，所以赛道边缘、拖尾、角色和技能特效都盖在黑幕上面 |
| 释放插槽 | `Assets/VFX/一念光明/Release/VFX_Curtain_Release.prefab` | 只有用户放进去的 `BlackCurtainTrail`，窗口占 13 s 的前 77% |

`Assets/VFX/ShaderGraph/一念光明/` 下另有一份 `SG_BlackCurtain`（GUID `b27a5757…`）和材质，没有被引用。它是旧副本，应该登记进 [retired.md](retired.md)；本次没有改。

### 1.2 开场时间线（配表：展开 1 s、保持 10 s、淡出 2 s）

| 时刻 | 发生什么 |
|---|---|
| −1 s | 通用前摇 `cast`（所有技能共用） |
| 0 | 每个客户端：`curtain_release` 开始；全屏材质激活；受害者看不到其他玩家，只看到拖尾 |
| 0 – 0.5 s | `_Expansion` = 0（`ExpansionDelaySeconds` 写死 0.5 s）。画面上只有扭曲黑洞，强度线性上升 |
| 0.5 – 1.5 s | 黑幕从施法者处外扩（`_Expansion` 0 → 2）；扭曲继续加强到 15 |
| 1.0 s | 控制器认为展开结束：施法者的 `Volume` 切到 `Volume_Dark`；`_Center` 从施法者**跳回画面中心**（`elapsed < _expandDuration`），这时外扩还没完成 |
| 1.5 s | 扭曲强度从 15 **一帧掉到 0**，黑幕铺满 |
| 1 – 11 s | 保持，全黑 |
| 11 – 13 s | 不透明度 1 → 0 |

另外，`BlackCurtainPresentation.csv` 里有 `expansionDelaySeconds`、`maxBlackHoleStrength`、`maxExpansionProgress` 三列，但 `BlackCurtainScreenEffect` 用的是常量，这三列没有接上。

### 1.3 对方案有约束的事实

- **黑幕是每个客户端本地画的。** 服务器只同步"谁被黑幕影响"，开场怎么画不涉及网络，不加 RPC，也不改服务器权威。Piloto 包的 `Ying Yang Screen Space FX` 之所以不能直接用，是因为它会挂到每个客户端的主相机上（[yingyang-vfx-analysis.md 3.20](yingyang-vfx-analysis.md#320-ying-yang-screen-space-fx4--1)）；黑幕本来就是"每个人自己的屏幕"，这一条在这里不是问题。
- **世界空间特效画在黑幕上面。** `curtain_release` 里的粒子在 `Buddah` 层，由 `RenderSkillVfx` 在黑幕之后画，而且和场景深度做遮挡：黑幕下看不见的地形、建筑仍会挡住粒子。所以施法者身上的吸积盘、被吸的墨片在全黑时仍然可见。这是机会（黑里只剩被吸进去的墨和光），也是风险（展开结束时要收掉，免得在黑幕上留一团）。
- **只有 High Fidelity 渲染器有 `BlackCurtain` pass**（[racemap README](../rendering/racemap/README.md)）。Balanced / Performant 下根本没有黑幕。这是已有的缺口，不是开场的问题。
- **反噬插槽还没有**：`Skill_BlackCurtain_Anti.releaseVfxId` 为空，全屏材质也是同一个。
- **时长只有一个来源**（[README 第 4 节](README.md#4-运行时结构设计待实现)）：开场长度必须等于配表的 `expandDurationSeconds`，不能另写常量。
- **项目没有音频**（README 第 9 节）。下面的声画同步点先作为预留的时间点写进方案。

## 2. 现在的扭曲黑洞：在做什么，为什么显得廉价

### 2.1 拆解（`空间坍缩` 节点组 + `BlackCurtainScreenEffect`）

`空间坍缩` 节点组只做一件事：改写全屏取色的坐标。

```
off = uv − C                         // uv：0–1 的屏幕坐标；C：_Center
d   = saturate(length(off))
F   = 1 + S · (1 − d)^10             // S：StrengthBlackHole
uv' = C + off · F                    // 用 uv' 取一次 URP Sample Buffer（Blit Source）
```

| 项 | 现状 |
|---|---|
| 扭曲方式 | 径向"捏"：每个像素到离中心 F 倍远的地方取色，所以远处的画面被压进中心周围一小块，像缩小镜。没有旋转，也没有把画面弯成环 |
| 强度曲线 | S 用 1.5 s 从 0 线性升到 15（`MaxBlackHoleStrength`），1.5 s 那一帧直接变 0。没有预备、没有峰值、没有回落 |
| 范围 | 衰减 (1 − d)^10。S = 15 时 F − 1 > 0.5 的区域是 d < 0.29；S = 5（0.5 s）时是 d < 0.2。d 按屏幕 UV 算、没乘宽高比，16:9 下是横向 1.78 倍的椭圆 |
| 中心 | 没有黑核。正中心的像素取的就是 C 自己，周围取的是被压缩的远处画面 |
| 越界 | S = 15 时，离中心 0.1 的像素会到 0.62 远的地方取色。这已经超出屏幕，于是拿屏幕边缘的像素拉伸，成为拖长的色带 |
| 颜色 / 亮度 | 无。只挪坐标，不压暗、不提亮、不着色 |
| 高光 / 层次 | 无。没有边缘亮环、色散或拖影；只取一次色 |
| 和黑幕的关系 | 黑幕从 0.5 s 起从同一圆心外扩，0.75 s 时半径已到 0.5，盖住了扭曲区（0.29）。这时 S 才 7.5，**后半程 7.5 → 15 的强扭曲全被黑幕盖住，玩家看不到** |
| 世界空间 / 镜头 / 声音 | 都没有。`curtain_release` 里只有拖尾，Feel 事件 `blackcurtain_observers` 在表里但 router 没注册，项目没有音频 |

黑幕遮罩：`Distance(uv, C)`（同样没乘宽高比）加上 (`Simple Noise` 尺度 100 − 0.5) × `_NoiseStrength` 0.12，再 `smoothstep(R − 0.08, R)` 取反。内部颜色由 `FlowOffset` 的梯度噪声经 pow(0.01) 和 step(0.02) 二值化，几乎全是 `FlowTint` 黑，只在噪声接近 0 的地方漏出 `_CurtainColor` 的灰点。所以边缘是细颗粒的溶解边，没有墨的形。

### 2.2 为什么显得廉价（落到具体的视觉原因）

1. **像图片编辑器里的"挤压"滤镜。** 只挪像素、不改光：被吸的画面亮度、颜色都不变，看不出"光被吞掉"。人们认得出的黑洞，是中心一块黑影加上紧贴着它的一圈亮环：EHT 拍到的 M87 是这样，《星际穿越》的 Gargantua 也是这样（第 3 节）。这两样现在都没有。
2. **没有轮廓。** 衰减是一个软气泡，边界说不清在哪；没有事件视界那样的硬边，眼睛没有可以抓住的形。椭圆的形状还暴露了"这是屏幕 UV"。
3. **不转。** 吸积、漩涡、被卷进去，都要靠旋转才读得出来。纯径向的缩放读起来像放大镜，不像黑洞。
4. **节奏是平的，最强的部分还看不见。** 线性加强，没有预备（动画十二原则的 anticipation），没有坍缩的那一下，结尾一帧归零。玩家只看到前 0.65 s 的弱扭曲。
5. **采样瑕疵。** 越界时边缘像素被拉长，是一眼就能看出的"便宜"信号。只取一次色，强扭曲处会出锯齿和拉丝，也没有拖影把它抹成运动。
6. **只发生在屏幕上。** 赛道上没有任何东西被吸进去，镜头不动，也没有声音，所以它像贴在镜头上的滤镜，不像场景里发生的事。
7. **和水墨没有关系。** 没有墨、白光或金，跟 Piloto 包的画风、仪玄配色都接不上。

## 3. 怎么把"空间扭曲 / 黑洞"做得有分量

### 3.1 参考

| 来源 | 借什么 | 链接 |
|---|---|---|
| James、von Tunzelmann、Franklin、Thorne，*Gravitational lensing by spinning black holes in astrophysics, and in the movie Interstellar*，Classical and Quantum Gravity 32（2015）065001 | 《星际穿越》Gargantua 的渲染器 DNGR：光线在弯曲时空里追踪。黑洞的辨识度来自"光被弯"：背景和吸积盘绕过黑洞，挤成贴着阴影的细环，而且带多普勒和引力频移造成的明暗、颜色变化 | [arXiv:1502.03808](https://arxiv.org/abs/1502.03808) |
| Event Horizon Telescope，2019-04-10 公布的 M87 黑洞图像 | 中间暗区 + 一圈不对称的亮环，一侧亮一侧暗。不对称让环有方向感，转起来才看得出转 | [EurekAlert 新闻稿](https://www.eurekalert.org/news-releases/579027) |
| 《Dota 2》谜团「黑洞」 | 把周围敌人往中心拖并控制数秒。游戏里"黑洞"技能最常见的读法：中心 + 拉扯 | [StrategyWiki · Enigma](https://strategywiki.org/wiki/Dota_2/Enigma) |
| 《Apex 英雄》地平线「黑洞」 | 部署后先延迟约 2 s，再持续把人往中心吸。有明确的"起势 → 吸"节奏 | [Apex Legends Wiki · Horizon](https://apexlegends.wiki.gg/wiki/Horizon) |
| 《守望先锋》查莉「重力喷涌」 | 官方的一句话描述就是"发射一个把敌人吸过去的重力井"。同类技能的定义就是"中心 + 吸" | [官方英雄页](https://overwatch.blizzard.com/en-us/heroes/zarya/) |

### 3.2 技法（URP 14 全屏 shader 里能做的）

| 技法 | 做法 | 解决第 2.2 节哪条 | 来源 |
|---|---|---|---|
| 引力透镜式偏折 | 用"偏折量 ∝ Rs² / r"代替现在的线性缩放：越靠近视界偏得越多；进了视界取不到光，就是黑；视界外一圈会把远处画面挤成细环（爱因斯坦环），再往里是翻过来的镜像。比"捏"多出硬边、黑核和环 | 1、2 | DNGR 论文是物理依据；实时版本是全屏 shader 里一次坐标变换（Custom Function） |
| 光子环 | 视界外紧贴一条细亮带；亮度乘 (0.5 + 0.5 cos(θ − φ(t)))，一侧亮一侧暗、随时间转 | 1、3 | EHT 图像 |
| 旋转 | Shader Graph 的 Twirl 节点（Center、Strength、Offset）；强度随距离衰减，越近转得越多 | 3 | [Twirl Node（Shader Graph 14）](https://docs.unity3d.com/Packages/com.unity.shadergraph@14.0/manual/Twirl-Node.html) |
| 径向拖影 | 沿像素到中心的连线取 5–8 次色累加并衰减，把被吸的画面抹成往里流的条纹；GPU Gems 3 第 13 章的体积光就是这个结构 | 3、5 | [GPU Gems 3 第 13 章](https://developer.nvidia.com/gpugems/gpugems3/part-ii-light-and-shadows/chapter-13-volumetric-light-scattering-post-process) |
| 预备的外鼓 | 吸之前整屏先往外鼓一下（取色坐标往中心缩一点）。URP 的 Lens Distortion 也能做，但它是 Volume 后期，而黑幕期间控制器会切 Volume，所以写进同一个全屏 shader 更稳 | 4 | [URP 14 Lens Distortion](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/Post-Processing-Lens-Distortion.html) |
| 越界取黑 | 取色坐标出了屏幕，就返回墨色（再加一点噪声边），不拉伸边缘像素 | 5 | — |
| 节奏 | 预备 → 动作 → 收尾：预备给动作分量。这里是外鼓 → 吸 → 坍缩那一下 → 吞 | 4 | [动画十二原则 · Anticipation](https://en.wikipedia.org/wiki/Twelve_basic_principles_of_animation)（Thomas & Johnston，*The Illusion of Life*，1981） |
| 镜头推拉与震动 | 一次动作叠几种小反馈（推镜、震动、闪），每加一层手感好一档。Vlambeer 的 "The Art of Screenshake"（INDIGO 2013）是这个思路的出处 | 6 | [Game Developer 报道](https://gamedeveloper.com/design/vlambeer-co-founder-shares-advice-on-building-better-action-games) |
| 世界空间被吸物 | 赛道上的墨片、墨烟、竹叶旋入施法者；它们在黑幕之上画，全黑后仍可见 | 6、7 | 本项目渲染顺序（第 1.3 节） |

### 3.3 色散在本项目要改成"双影"

RGB 色散会出现青色、品红的彩边，违反 [README 第 2 节](README.md#2-视觉方向已定2026-10-28-晚第三次改写)的"只有黑白，正向另加金"。所以改成两次取色的"双影"：
- 正向：偏外的一层只取亮度、乘金加色；偏内的一层乘墨。
- 反噬：一层乘红雾（Alpha 混合，不发光），一层乘纯黑。

画面上仍然有"光被拆开"的层次，颜色只用规定的几种。

### 3.4 黑洞怎么做成水墨（贴合 Piloto 阴阳包与配色）

| 黑洞的部位 | 水墨 / 阴阳包的对应 | 正向（仪玄，外黑内亮） | 反噬（红） |
|---|---|---|---|
| 事件视界（黑核） | 一滴浓墨。边缘用包里的流动噪声（`CausticNoise` 或 `Smoke_loop_thingy` 的一帧）扰动，不是正圆；边缘加深 | 墨 `#111A19` | 纯黑 `#000000` |
| 墨壳（视界外的旋臂） | 被吸进来的墨，按旋向拉成旋臂，包在光子环外面 | 墨，旋臂边缘带白描边（湿墨高光） | 纯黑墨 + 血红烟 `#950000` |
| 光子环 | **外黑内亮**：从外往里依次是墨壳、金环、黑核。光是从墨里透出来的，不贴在墨的外缘 | 金 `#FFBC6B` 加色，平时 ×2–4，坍缩那 1–2 帧到白 | 不发光：血红烟的 Alpha 环 + 红雾 `#FF1010`（α ≤ 0.55）；白只做环上几个小点 |
| 吸积盘（世界空间） | 包里的太极符 `yingyang_1` 和墨环 `Ink_Shockwave_1_Distorted`，压扁成斜看的盘，绕施法者转 | 白底墨边太极，内缘一圈金 | 黑底白边的反转太极（README 反噬规则），外裹红雾 |
| 被吸的东西（世界空间） | 墨片 `Ink_flakes_1_alpha_soft`、墨烟、包里的竹叶，旋入施法者 | 墨片里夹少量金火星 | 墨片 + 血红烟，白色小点 |
| 双影 | 第 3.3 节 | 金 / 墨 | 红雾 / 黑 |
| 坍缩那一下的闪 | 包里的 `LightFlash_Soft`，只在施法者处，一次 | 白核 + 金晕 | 不用白闪：一团红雾炸开，几粒白点 |

## 4. 保留扭曲黑洞并升级：三个方案

### 4.1 三个方案共用的底座

不管选哪个，下面这些都要做，用来消除第 2.2 节列的廉价感：
1. 半径按宽高比校正，形状是圆，不是椭圆。
2. 越界取色返回墨色，不拉伸边缘像素。
3. 强度按展开进度 p（0 → 1）走一条四段曲线：外鼓、吸、坍缩、吞。曲线跟配表时长等比缩放；峰值放在黑幕盖住之前，结尾平滑归零。
4. 加旋转（Twirl）和 5–8 次的径向拖影。
5. 圆心每帧重新投影到施法者，黑洞粘在他身上，不再中途跳到画面中心。施法者不在画面里时，取他所在方向的屏幕边缘。
6. 修掉写死的 0.5 s 延迟，展开按配表时长走。

要动的代码：`SG_BlackCurtain` 加一到两个 Custom Function 和若干参数；`BlackCurtainScreenEffect.cs` 按 p 写参数。用户正在改这两个文件，所以等用户改完、选定方案后再动。

### 4.2 H1 墨眼：引力透镜 + 视界吞没

**观感。** 施法者身上睁开一只墨做的"眼"：中心是浓墨的黑核，紧贴着它的是一圈从墨里透出的金色光子环（一侧亮、一侧暗，在转），外面是被吸进来的墨旋臂。周围的赛道被弯成一圈细环挤在黑核外缘。施法者脚下一只压扁的太极吸积盘越转越快，墨片从四周旋进去。坍缩那一下，黑核猛地张大，光环闪到白；然后视界一路张开吞满屏幕，光环被推出画面。最后的全黑就是"在黑洞里面"，和保持段的 10 s 全黑是连着的。

**分镜**（p = 展开进度，配表 1 s 时 p 就是秒）：

| p | 屏幕（全屏 shader） | 世界（`curtain_release`） | 镜头 / 声音预留 |
|---|---|---|---|
| 0 – 0.15 预备 | 整屏往外鼓一下，中心略微压暗；施法者处出现一个针尖大的黑点 | 墨片开始从地面浮起 | 低频嗡声进 |
| 0.15 – 0.55 吸 | 黑核从针尖长到半径约 0.07（以屏幕高为 1）；透镜偏折、旋转、拖影一起加强，赛道被拉成往里流的弧线；光子环成形并开始转；双影出现 | 太极吸积盘出现并加速，墨片、墨烟旋入 | FOV 缓慢收窄约 4°（推镜），嗡声升调 |
| 0.55 – 0.62 坍缩 | 黑核 2–3 帧内张到约 0.15（略过冲再回），光子环亮到峰值 1–2 帧 | 被吸物猛地加速，施法者处一次闪 | 声画同步点：低频重击；震动约 0.12 s，幅度小 |
| 0.62 – 1.0 吞 | 视界张开吞满屏幕，边缘换成流动的墨边（第一轮 A 的墨晕），光子环贴着边被推出画面后熄灭；透镜强度随视界变大而回落 | 吸积盘和被吸物用 `endWithWindow` 在 p = 1 时收完 | 呼啸尾音 |

**实现要点。**
- 全屏 shader：
  - 新 Custom Function `BlackHoleLens(uv, C, Rs, k, twirl, blur, aspect)`：返回取色坐标和"是否在视界内"。在其中做透镜偏折（偏折量 ≈ k · Rs² / r，r 小于偏折量时翻到对侧，这就是爱因斯坦环的镜像）、旋转、5 次拖影、越界取黑。
  - 光子环、墨旋臂、双影都在同一个函数里算，叠到颜色上。
  - 原来的 `空间坍缩` 组被这个函数替换；原来的遮罩组保留，用在 p > 0.62 的视界边缘。
- 新参数：`_HoleRadius`、`_LensStrength`、`_Twirl`、`_RadialBlur`、`_RingIntensity`、`_RingPhase`、`_Bulge`、`_Dispersion`。由 `BlackCurtainScreenEffect` 按 p 用曲线写入：曲线放在技能 SO 上，横轴是 p，所以配表改时长时自动等比缩放。
- 插槽：`curtain_release` 加一层 `Imp`（`Fixed`，窗口 = 展开时长），内容是：
  - 太极吸积盘：`yingyang_1` 面片，加 `Ink_Shockwave_1_Distorted` 反向收缩。
  - 墨片、墨烟：轨道速度 + 向心速度。
  - 金火星。
  - 闪：`LightFlash_Soft`。

  约 6–9 个粒子系统，在默认上限 10 以内。
- Feel：把 `blackcurtain_observers` 在 `SkillFeelRouter` 里注册，做本地推镜和一次小震动。竞速游戏里镜头动作要小、短，只在本地。

**成本。**
- 开发：中高。透镜函数要调参，曲线 8 条。
- GPU：开场那 1 s 里全屏约 8–10 次取色。1080p 下估计 0.1–0.3 ms（未测，实现后按 [README 8.1](README.md#81-性能测量流程2026-10-30可对任一技能复用) 测）。
- 粒子：+6–9 个系统。
- 联机：无影响。

**保留全黑：** 是，视界吞满屏幕。

**风险。**
- 透镜的镜像环调得不好会让人看不懂。参数 k 要在 VFXtests 里实拍调。
- 光子环扫过整个屏幕时要压亮度，峰值只有一次（光敏，参见第一轮引用的《斯普拉遁 3》）。

### 4.3 H2 卷墨入渊：画面被卷进去

**观感。** 最贴近现在的扭曲。整个画面像搅开的墨水一样旋转着缩进施法者，缩成一个越来越小的画面圆盘。圆盘外被"掏空"的地方是墨：黑底上有旋转的湿墨条纹。圆盘的边缘内侧有一圈金光（外黑内亮）。最后圆盘拧成一个点，闪一下，全黑。读起来是"一念之间，天地被他收进去"。

**分镜：**

| p | 屏幕 | 世界 | 镜头 / 声音预留 |
|---|---|---|---|
| 0 – 0.15 预备 | 反向拧一下（约 −20°），同时外鼓 | 墨片浮起 | 嗡声进 |
| 0.15 – 0.75 卷入 | 画面按现在的"捏"继续缩小，但改成越界取墨，所以四周被墨吃掉；旋转逐渐加到中心 2 圈以上；拖影把画面抹成螺旋条纹；圆盘边缘带墨的毛边和内侧金光 | 墨片、竹叶旋入 | 推镜 |
| 0.75 – 0.9 收口 | 剩下的画面圆盘半径约 0.15，边缘抖动，金光加亮 | — | 升调 |
| 0.9 – 1.0 熄 | 圆盘拧成一个点，施法者处一次闪，全黑 | 闪 | 同步点：重击 + 震动 |

**实现要点。**
- 保留现有 `空间坍缩` 的映射（uv' = C + off · F），把 F 改成按 p 的曲线。再加：
  - 宽高比校正；
  - Twirl 节点；
  - 拖影（一个 Custom Function）；
  - 越界取墨：在 uv' 出屏幕的距离上做噪声边；
  - 内侧金边：uv' 到屏幕边的距离上做一条窄带。
- 全黑由映射本身产生：画面都被吸出屏幕范围，不需要另一个外扩圆。原来的 `MainCurtainMask` 只在最后 0.05 用来兜底，保证 p = 1 时全黑。
- 螺旋墨纹：用 Polar Coordinates 节点把 `InkStroke_Test_1` 卷成螺旋，在被掏空的区域按旋向滚动。
- 插槽和 Feel 同 H1，被吸物少一些，约 4–6 个系统。

**成本。**
- 开发：中。和现在的节点结构最接近，大部分是现成节点，只要一个 Custom Function。
- GPU：同 H1，略少。
- 联机：无影响。

**保留全黑：** 是。

**风险。**
- 黑是从四周往里吃的。对施法者来说是"天地被吸进自己"，很合适；对受害者，施法者不在画面里时漩涡中心偏到屏幕边缘，看着不如 H1 完整。
- 没有"黑核 + 亮环"这个黑洞最强的识别符号，读起来更像漩涡，不太像黑洞。

### 4.4 H3 坍缩 · 墨爆：先吸后爆

**观感。** 黑洞是预备，墨爆是兑现。施法者处的黑洞先吸（带黑核、光子环、旋转），越吸越小，缩成一个点"憋住"一拍。然后爆开：一圈墨从点上炸出，带着墨里透出的金色内光（第一轮 A 的墨晕前沿）向外吞满屏幕。

**分镜：**

| p | 屏幕 | 世界 | 镜头 / 声音预留 |
|---|---|---|---|
| 0 – 0.15 预备 | 外鼓 | 墨片浮起 | 嗡声进 |
| 0.15 – 0.45 吸 | 黑核长到约 0.06，透镜、旋转、拖影、光子环 | 吸积盘、墨片旋入 | 推镜 |
| 0.45 – 0.56 憋 | 黑核反向缩成一个点，吸力在这里最强，画面被扯到最紧 | 被吸物全部收进施法者 | 声音抽空一拍 |
| 0.56 爆 | 一次闪（正向白核金晕，反噬红雾炸开） | 墨溅 `Ink_Splash_Radial_Wide` 炸开 | 同步点：重击 + 震动 |
| 0.56 – 1.0 吞 | A 的墨晕前沿从点上外扩吞满屏幕；残余的透镜和旋转在前沿后面回落 | 墨溅收掉 | 呼啸尾音 |

**实现要点。**
- 前半段用 H1 的透镜函数，后半段用第一轮 A 的墨晕遮罩，两套按 p 交接。
- 插槽比 H1 多一层爆开的墨溅，约 8–10 个系统，接近默认上限。

**成本。**
- 开发：中高。两套效果加交接。
- GPU：同 H1。
- 联机：无影响。

**保留全黑：** 是。

**风险。**
- 1 s 里塞了吸和爆两个动作，每段都短（吸 0.4 s、爆 0.44 s）。想让它舒展，可能要把配表的展开时长拉到 1.3–1.5 s，但这会推迟"全黑"这个玩法时刻，要用户定。

### 4.5 叠加层：第一轮方案里能用上的部分

| 叠加层 | 出处 | H1 | H2 | H3 |
|---|---|---|---|---|
| 墨晕前沿（流动墨边 + 内侧金光） | 第一轮 A | 视界张开时的边 | 只在兜底时用 | 爆开后的主体 |
| 墨烟、墨片聚拢（世界空间） | 第一轮 E | 吸的阶段 | 卷入阶段 | 吸的阶段 |
| 太极吸积盘（世界空间） | 本轮新增 | 主体之一 | 可选 | 吸的阶段 |
| 墨团贴屏 | 第一轮 B | 可选：只给受害者，高档位 | 不用 | 可选：爆的那一刻 |
| 推镜 + 震动（Feel，本地） | 本轮新增 | 有 | 有 | 有 |
| 声画同步点 | 本轮新增 | 坍缩时 | 收口熄灭时 | 爆时 |

### 4.6 对比

| | H1 墨眼 | H2 卷墨入渊 | H3 坍缩 · 墨爆 |
|---|---|---|---|
| 黑洞辨识度 | 最高（黑核 + 光子环 + 透镜环） | 中（漩涡） | 高（前半段同 H1） |
| 离现在的扭曲多近 | 换了映射，保留"往施法者吸" | 最近，同一映射加料 | 前半同 H1 |
| 分量从哪来 | 坍缩那一下 + 视界吞屏 | 整个画面被卷走 | 憋一拍再爆 |
| 黑从哪来 | 视界从施法者处张开 | 四周被掏空 | 墨从施法者处炸开 |
| 外黑内亮 | 墨壳 → 金环 → 黑核 | 被掏空的墨 → 画面边缘内侧的金 | 吸时同 H1，爆时同 A |
| 开发量 | 中高 | 中 | 中高 |
| 粒子系统 | +6–9 | +4–6 | +8–10 |
| 1 s 里是否够用 | 够 | 够 | 偏紧，可能要拉长配表 |

## 5. 推荐（第二轮）

**推荐 H1 墨眼**，叠加第一轮 E 的墨片聚拢、A 的墨边、本轮的太极吸积盘和 Feel 推镜震动。

理由：
1. **它最像黑洞。** 现在缺的正是黑洞最强的两个符号，黑核和贴着它的亮环；大众对黑洞的视觉印象（EHT 照片、《星际穿越》）就是这两样。H1 直接补上。
2. **保留用户要的扭曲，并给它分量。** 往施法者吸的扭曲还在，但从"挪像素"变成"弯光"：有硬边、有环、会转、有拖影，峰值放在玩家看得见的时候。
3. **黑幕和黑洞是一回事。** 全黑就是视界的里面，展开段和 10 s 保持段在叙事上连着，不是"扭曲完再盖一层黑"。
4. **配色规则一一对应。** 外黑内亮正好是"墨壳 → 金环 → 黑核"；反噬把金环换成血红烟和红雾，结构不变。RGB 色散换成金墨 / 红黑双影，不引入别的颜色。
5. **成本可控。** 一个 Custom Function、一组曲线、一层插槽（10 个系统以内）、一个 Feel 事件；不改联机，GPU 只在开场那 1 s 多一点。

如果更看重"和现在最像、改动最少"，选 H2。如果想要最大的爆发感，并且愿意把展开时长拉长，选 H3。

## 6. 需要用户定的事

1. 选 H1、H2 还是 H3，以及第 4.5 节的叠加层要哪些。
2. 展开时长是否保持 1 s。H3 建议 1.3–1.5 s；改的话"全黑"这个玩法时刻也会推迟。
3. 镜头推拉和震动要不要（竞速里会影响手感，幅度会压得很小）。
4. 坍缩那一下的峰值能到多亮（正向允许 1–2 帧接近白；有光敏顾虑可以封顶在金）。
5. 施法者和受害者看同一个开场，还是给受害者额外加墨团贴屏。
6. 反噬（红）开场现在一起做，还是正向定稿后再做（`Skill_BlackCurtain_Anti.releaseVfxId` 现在为空）。**2026-11-02 定：一起做，屏幕和世界层都按深浅翻译成红（第 8.2 节）。**
7. Balanced / Performant 渲染器没有黑幕 pass，要不要另开任务补上。

## 7. H1 墨眼原型（2026-11-02）

> 2026-11-03 起 H1 已经转正、旧黑幕已退休，现在的文件、接法和看法见第 8 节。本节保留原型阶段的记录：文件名、菜单和比赛开关都已不存在。

用户选定 H1，并要求先出一个能直接看的实例。原型是独立的一套，没有改原有的一念光明：`BlackCurtainScreenEffect.cs`、`SG_BlackCurtain`、`curtain_release` 插槽 prefab、渲染器资产、VFXtests 都没动。没有提交。

### 7.1 文件（都在 `Assets/VFX/一念光明/Proto_H1/`）

| 文件 | 做什么 |
|---|---|
| `CurtainEyeH1.shader` | 全屏的黑洞（手写 HLSL，`BuddahGo/Proto/CurtainEyeH1`）：透镜偏折、旋转、8 次带抖动的径向拖影、外鼓预备、墨做的视界（羽化的流动边 + 往外洇的墨晕）、墨壳旋臂、光子环（一侧亮、在转）、金墨双影、坍缩闪光、保持段和淡出。越界取色时渐变到墨色。另有一个红色反噬开关，只对屏幕部分生效 |
| `CurtainEyeH1RenderPass.cs` | 从脚本注入全屏 pass（`RenderPipelineManager.beginCameraRendering` + `EnqueuePass`），只在原型播放时、只对目标相机生效，不改渲染器资产。时机是 BeforeRenderingTransparents + 1，排在原来的 `BlackCurtain` 之后，所以赛道边缘、拖尾、赛手、技能特效仍然画在它上面 |
| `CurtainEyeH1Player.cs` | 按开场进度 p 写 shader 参数（四段曲线：外鼓、吸、坍缩、吞），然后是全黑保持和淡出。生成世界层并让它跟随施法者，速度按 1 / 展开时长缩放。可选的镜头推近和震动默认关闭 |
| `CurtainEyeH1Preview.cs` | 预览场景里的控制：开始就播、循环、重播、慢放、开关镜头动作和红色、调峰值金，另有截帧 |
| `CurtainEyeH1MatchHook.cs` | 比赛里的临时开关（只在编辑器里有效，默认关，见 7.3） |
| `Editor/CurtainEyeH1Builder.cs` | 菜单 `Tools/BuddahGo/VFX/Proto/一念光明 H1 墨眼/`：打开预览并播放、重建原型资源、比赛里临时启用 |
| `VFX_Curtain_EyeH1_World.prefab` | 世界层，按 1 s 开场调，8 个粒子系统，全部在 `Buddah` 层：太极吸积盘（`yy_1_symbol` 平铺在地上加速转）、盘内金环（`Ring_Halo_faded_add_soft`）、往里收的墨环（`Ink_Shockwave_1_Distorted_alpha_soft`）、旋入的墨烟、墨片、竹叶、金火星，以及 p = 0.55 的坍缩闪光（`LightFlash_Soft`，金色）。墨环、墨烟、太极、火星从 `VFX_Reverse_Hit` 里克隆，沿用调好的渲染设置；材质都引用 `Assets/VFX/Shared/Piloto/` 或 `Assets/Plugins/Piloto Studio/`，不引用包目录 |
| `CurtainEyeH1_Preview.unity` 与 `Materials/` | 自建的预览场景：沙地、紫色赛道、白色赛道边、石柱、两座鸟居、三个赛手（`佛头动画tpost.fbx`，层级和 `Buddah.prefab` 一样：根 > VisualRoot ×4 > model），用 `RaceMap_Environment_Profile` 做后期。材质是这个场景自己的 5 个 URP Lit |

### 7.2 怎么看

1. 菜单 `Tools/BuddahGo/VFX/Proto/一念光明 H1 墨眼/Open Preview And Play (打开预览并播放)`。会先问是否保存当前场景，然后打开预览场景并进入 Play。也可以手动打开 `CurtainEyeH1_Preview.unity` 再按 Play。
2. Play 后自动循环：正常画面 → 1 s 开场 → 1.5 s 全黑（预览缩短了，比赛是 10 s）→ 0.8 s 淡出 → 1.2 s 后重播。
3. 操作：左上角面板，或键盘：
   - 空格：重播。
   - 1–5：速度 ×1 / ×0.5 / ×0.25 / ×0.1 / ×0.05。
   - L：循环。
   - K：镜头推近 + 震动。
   - R：红色反噬（只有屏幕部分）。
   - V：施法者 / 受害者视角。
   - 滑条：峰值金。
4. 调参：选中 `CurtainEyeH1 Preview` 物体，在 Inspector 里改 `CurtainEyeH1Player` 的时长、峰值金、中心偏移和 Curves（每个参数一条曲线，Play 时可以直接改；退出 Play 后改动不保留，想留下来就在 Play 外改）；粒子在 `VFX_Curtain_EyeH1_World.prefab` 里改。改了 builder 代码后，用"重建原型资源"重新生成；重建前要先关掉预览场景。

### 7.3 比赛里临时开关

- 菜单 `Tools/BuddahGo/VFX/Proto/一念光明 H1 墨眼/In Match (比赛里临时启用，默认关)` 打勾，下次 Play 时生效。存在 EditorPrefs 里，只影响本机编辑器，打包的游戏里没有这段代码。
- 打开后，这个客户端每次出现黑幕，都改由原型来画：
  - 在黑幕期间，把原来材质的 `_EffectActive` 和黑洞强度压成 0，原来的扭曲和外扩不再出现。
  - 原型按 `BlackCurtainScreenEffect` 当前的展开、保持、淡出时长，画开场、全黑和淡出。
  - 施法者取最新的 `curtain_release` 插槽的施法者，位置用 VisualRoot，缩放用根节点。
  - 谁被隐藏、赛道边缘、拖尾、Volume 切换仍由 `BlackCurtainViewController` 管。
- 2026-11-02 在单人赛（1 玩家 + 2 AI，High Fidelity）验证过一次：本地玩家放一念光明，0.35 / 0.6 / 1.5 s 截图正常，没有新的错误。原来的扭曲没有叠在上面。验证完已经把开关关回去。联机双端和反噬版没有测。

### 7.4 先按保守默认做的决定（用户可改）

| 待定项（第 6 节） | 原型里的默认 | 怎么改 |
|---|---|---|
| 展开时长 | 1 s，比赛里读配表 | `CurtainEyeH1Player.expandSeconds` 或配表 |
| 镜头推近和震动 | 关 | 预览里按 K；`cameraKick` |
| 坍缩峰值 | 封顶在金（`flashCap` 1.6，HDR） | 预览滑条，或 `flashCap`，调大会往白走 |
| 受害者额外表现 | 没有，所有人看同一个开场 | — |
| 红色反噬 | 只有屏幕部分能切到红色预览，红色世界层没做 | 定稿后再做 |
| 低画质渲染器 | 不在范围内，Balanced / Performant 仍然没有黑幕 | 另开任务 |
| 黑洞中心 | 施法者身体中部（偏移 (0, 3.5, 0)），原来的黑幕是 (0, 1, 0) | `centerWorldOffset` |

### 7.5 用户第一次看后的修改（2026-11-02）

用户的反馈："边缘太过于僵硬了"，以及"眼睛的效果是有的 但是边缘不足够丝滑"。改了这几处：
- 视界边缘：原来的过渡只有 0.01 宽，边上的噪声是角度上的高频起伏。现在改成羽化宽度随黑洞变大（0.012 + 0.10 × 半径），噪声低频、随时间缓慢漂移，起伏幅度减小；外面再加一圈往外洇的墨晕。
- 透镜：原来到某个半径就把画面硬翻过去，留下一道接缝环。现在改成 r · exp(−a / r²)，远处的偏折和原来一样，近处平滑地折回去。
- 越界取色：原来一出屏幕就变成墨色，现在在 0.08 的距离内渐变。
- 拖影：8 次取样按像素抖动，阶梯状的重影变成运动模糊。
- 墨旋臂：过渡带加宽。
- 世界层的墨环：改用 soft 材质，透明度降到 0.55（原来的描边很硬）。

### 7.6 用户第二次看后的修改（2026-11-02）

用户的反馈有四条：
1. 开启技能后，地图边缘那个发光没了，那是给发动者的。
2. 原本背景里有一些像碎屑在后面飞的特效，是特意做的，要重新加上。
3. 实际逻辑是：只有施加者看得到边缘；受害者只能看到自己的轨迹。
4. 动画没有曲线渐入，所以显得僵硬。

做法：在单人赛里分别以施法者和受害者的身份放一念光明，原版和 H1 在同样的时刻（0.35–12.3 s）各截一组图对比。

- **边缘发光和施法者的路线拖尾（第 1 条）。** 原因是渲染顺序：H1 的 pass 原来排在 BeforeRenderingTransparents + 1，晚于透明 pass。发光的边缘丝带和 `BlackCurtainTrail` 是在透明 pass 里画的，所以被原型的黑盖住了；赛手是 `RenderBuddah` 在 AfterRenderingTransparents 重画的，所以还看得见。现在改为 BeforeRenderingTransparents，并在渲染器的 feature 之前入队，排在原来的 `BlackCurtain` 和透明 pass 前面，和原版的位置一样。复测：施法者看得到白色发光边缘和红色路线拖尾，和原版一致。
- **飞的碎屑（第 2 条）。** 来自 `SG_BlackCurtain` 的 `FlowOffset` 组：一层快速流动的梯度噪声（屏幕坐标 × 9.4，加上时间 × (10, −10)，噪声尺度 10），噪声到 0 的地方显示 `_CurtainColor` 的灰点。原型静音旧材质时把它一起关掉了。现在把同样的算法（Shader Graph Gradient Noise 的原式）搬进 `CurtainEyeH1.shader`，凡是墨覆盖的地方都显示，所以视界吞屏、全黑保持、淡出期间都有。参数在材质的 Flying specks 一组。复测和原版一致。
- **视角逻辑（第 3 条）。** 原型不改谁看到什么，这部分全由 `BlackCurtainViewController` 管：`localIsCaster ^ isAnti` 决定谁看边缘，受害者隐藏其他赛手。受害者视角复测（AI 放、本地玩家受害）：原版和 H1 一样，没有边缘、其他赛手隐藏、有拖尾。**和用户描述不一致的一点：** 两个版本里受害者都能看到不止自己的一条拖尾，其他人的金色拖尾也在（人被隐藏了）。原因是 `SetTrailsVisible` 在每个玩家身上都播 `curtain_hit`，[slots.md](slots.md) 也写的是"每个玩家身上的路线拖尾"。这是现有的玩法代码，原型没有改。**用户决定（2026-11-02）：保留所有拖尾**，受害者照旧看到每个玩家的 `curtain_hit` 拖尾，玩法代码不改。这是有意的设计：赛手本身被隐藏，拖尾就是用来让人看到所有玩家在哪里的。
- **曲线渐入（第 4 条）。** 原来的参数用分段公式，起步有突变：黑核一出现就有可见大小；外鼓用正弦，起步速度最大；各段斜坡有折角。现在每个参数都是 `CurtainEyeH1Player.curves` 里的一条 AnimationCurve，横轴是开场进度 p。默认关键帧切线为 0，所以每个值都从静止缓入、缓出。坍缩保留一下有意的快速张开（过冲后回落），吞屏段在 p = 1 处切线为 9，持续加速直到盖满。Play 时可以在 Inspector 里直接改曲线。世界层的发射率也改成在前 35% 的时间里从 10% 渐增到满。
- **编辑器预览补全。** 预览场景加了两条发光边缘：用游戏的 `SG_MapEdge` 材质，放在 `TrackEdge` 层，`_EdgeVisible` 按 `BlackCurtainViewController` 的时序驱动（+0.5 s 起淡入到展开结束，保持，跟着淡出）。另外加了视角开关（面板或 V 键）：施法者视角看得到发光边缘；受害者视角没有边缘，保持段里隐藏其他赛手。预览里施法者不动，所以看不到路线拖尾；拖尾要在比赛里用临时开关看。

### 7.7 还没做 / 已知

- 正式接入时要做：
  - 并回 `SG_BlackCurtain`（或把这个 shader 正式化）和 `BlackCurtainScreenEffect`；
  - 曲线放到技能 SO 上；
  - 世界层并进 `curtain_release` 插槽，登记粒子系统上限；
  - 新 shader 登记进 `PreloadShader.shadervariants`；
  - 去掉 `Shader.Find`。
- 原型的噪声是 shader 里算的，没有用包里的噪声贴图；正式化时可以换成 `CausticNoise` 或 `Smoke_loop_thingy`。
- 性能没测。预计开场那 1 s 里全屏多 9 次取色，粒子系统 8 个。
- 还没有用户的评审结论。

## 8. 转正：H1 墨眼成为一念光明的黑幕（2026-11-03）

用户 2026-11-02 看过比赛里的 H1 后说"目前来说差不多 可以先退休老的特效了"，并定了反噬配色：做红色世界层，"将目前黄色色系的配色 合理按照深浅度 分层'翻译'成为红色色系 并且保持与其他 anti 类似效果"。旧黑幕退下来的东西逐项记在 [retired.md](retired.md) 6.5。

### 8.1 文件与接法

| 文件 | 做什么 |
|---|---|
| `Assets/Rendering/Shaders/FX/FX_CurtainEye.shader`（`FX/CurtainEye`） | 原型 shader 正式化：开场、全黑保持、淡出、飞屑都在这一个 pass 里；加了反噬红色的分层映射（8.2）和配表 `maxOpacity`（`_MaxOpacity`） |
| `Assets/Scripts/Buddah/ComboSkill/Skill_BlackCurtain/BlackCurtainScreenEffect.cs` | 同名组件改写（`BlackCurtainViewController` 照旧把它加在本机相机上）：按配表的展开 / 保持 / 淡出播放，中心每帧跟着施法者的 VisualRoot，生成并跟随世界层（反噬用红色的那份），可选镜头推近（默认关） |
| `BlackCurtainEyeProfile.cs` + `Assets/VFX/一念光明/Eye/CurtainEye_Profile.asset` | 正向和反噬共用的外观：shader、两份世界层、峰值金 1.6、中心偏移 (0, 3.5, 0)、镜头推近开关、每个参数一条曲线。两个技能 SO 的 `eyeProfile` 都指向它 |
| `BlackCurtainRenderFeature.cs` | `URP-HighFidelity-Renderer` 里原来 `BlackCurtain`（Full Screen Pass）的位置换成它，名字不变。BeforeRenderingTransparents，只在本机相机有黑幕时入队，所以发光边缘、拖尾、赛手和技能特效仍画在黑幕上面 |
| `Assets/VFX/一念光明/Eye/VFX_Curtain_Eye.prefab`、`VFX_Curtain_Eye_Anti.prefab` | 世界层（原型的 `VFX_Curtain_EyeH1_World`，GUID 不变）和它的反噬 prefab 变体（只覆盖颜色）。两份都登记进 `VfxPackVendor.EffectPrefabs`；用到的材质、贴图都已在仓库里 |

谁看边缘、谁被隐藏、拖尾、Volume 切换仍然全由 `BlackCurtainViewController` 管，没有改。

### 8.2 反噬：金按深浅翻译成红

按 [anti-palette.md](anti-palette.md) 第 5、6 节和如来神掌反噬变体的先例：红全部是 Alpha 混合的"染"，不发光；白只留小点。

| 层 | 正向 | 反噬 |
|---|---|---|
| 屏幕：光子环核心、坍缩闪光（最亮的金） | 金，加色，封顶 1.6 | 红雾 `#FF1010`，α ≤ 0.55 |
| 屏幕：环外的光晕（金的辉光） | 金 | 血红烟 `#950000`，α ≤ 0.8 |
| 屏幕：双影（中间调的金） | 金 | 暗血红 `#6F0000`，α ≤ 0.6 |
| 屏幕：墨、全黑保持 | 玄青 `#111A19` | 纯黑 `#000000` |
| 屏幕：湿墨高光、飞屑 | 白、灰 `#595959` | 不变（高光减到 0.6） |
| 世界层：太极符 | 白 α 0.95 | 半透明白 α 0.53（同 `VFX_Reverse_Hit` 的阴阳符） |
| 世界层：盘内亮金环（加色） | `#FFFFAB` | 中灰 `#3C3C3C`（同神掌"亮金环 → 中灰"） |
| 世界层：金火星（加色小点） | `#FFFFD6` | 白点 `#FFFFFF`（白峰值只做点） |
| 世界层：坍缩闪光（大面积加色） | `#FFE281` | 置黑 = 关（同神掌"大面积加色光晕置黑"；屏幕的红雾承担这一下） |
| 世界层：墨片 | `#111A19` | `#000000` |
| 世界层：竹叶 | `#1F241F` | 暗血红 `#6F0000` |
| 世界层：墨环、旋入墨烟 | 黑 | 不变 |

### 8.3 怎么看

- **预览（本地，不入库）**：打开 `Assets/VFX/一念光明/Proto_H1/CurtainEyeH1_Preview.unity`，选 `CurtainEyeH1 Preview`，按 Play。预览直接播正式的组件和 profile，所以看到的就是比赛里的样子。键位：空格重播，1–5 速度，L 循环，K 镜头推近，R 反噬红色，V 切换"你是施法者 / 你是其他玩家"（谁看边缘按游戏规则 `施法者 XOR 反噬` 算）。
- **调参**：改 `一念光明/Eye/CurtainEye_Profile.asset` 的曲线、峰值金、中心偏移；粒子改两份世界层 prefab。profile 是资产，Play 中改的曲线会保留；预览里的 K 和峰值金滑条在退出 Play 时还原。
- **比赛**：不再需要开关，正常开一局，放一念光明就是新黑幕。

### 8.4 验证（2026-11-03，编辑器，单人赛 1 玩家 + 2 AI，High Fidelity）

- 编译 0 错误。旧资产的 GUID 在 `Assets/`、`ProjectSettings/` 里 0 引用后才删。
- 预览：正向和反噬各截 p = 0.1–0.95 与全黑一组。正向和原型一致；反噬是墨里的红雾环和血红烟，保持段纯黑带灰色飞屑。
- 比赛里三种情况各截 0.3–13.5 s：
  - 本地玩家放正向：看得到发光边缘和自己的路线拖尾，保持段有飞屑，13 s 后恢复；
  - AI 放正向、本地玩家受害：没有边缘，其他赛手隐藏，拖尾在；
  - 本地玩家反噬：红色开场，施法者看不到边缘（反噬规则）。
  - 三次都没有新的 Console 错误。
- 没测：联机双端、Development build、性能（开场 1 s 里全屏多 9 次取色，世界层 8 个粒子系统）。

### 8.5 还开着

- `Skill.xlsx` 的 BlackCurtainPresentation 表还有五列在记旧常量（retired.md 6.5，待删，走配表流程）。
- `FX/CurtainEye` 没进 `PreloadShader.shadervariants`（旧的 `SG_BlackCurtain` 也没进）。第一次放的那一下可能有编译卡顿，比赛截图里第一次施法的开场比后面快，要不要预热由用户定。
- Balanced / Performant 渲染器仍然没有黑幕 pass（第 6 节第 7 条，没改）。
- 2026-11-03 用户定：施法者看得到边缘和所有赛手，但看不到任何人的拖尾；受害者只看得到拖尾。施法者身上原来的红色拖尾（`curtain_release` 插槽里的 `BlackCurtainTrail`）已删，比赛复测符合，见 retired.md 6.5。

## 附录：第一轮的五个方向

2026-10-31 第一轮调研的结论是"换掉扭曲黑洞"，用户否定了这个前提（见文首）。下面的参考和方案保留，作为第 4.5 节叠加层的出处。

### 附录 1：外部参考（遮挡视野类）

| 作品 | 做法 | 借什么 | 来源 |
|---|---|---|---|
| 《英雄联盟》魔腾 R「鬼影重重」 | 缩小敌方视野 6 s；V10.18 视觉更新后，被影响的敌人屏幕上加一层阴影状屏幕空间效果；施法时全员屏幕变色（友方蓝、敌方红） | 施法者和被致盲的人看到的颜色不同 | [LoL Wiki · Nocturne](https://wiki.leagueoflegends.com/en-us/Nocturne/LoL) |
| 《斯普拉遁 3》Splattercolor Screen | 敌人穿过后屏幕变暗、变黑白 6 s；有玩家反映光敏不适 | 屏幕级压暗最有效，全屏高对比闪烁要慎用 | [Inkipedia](https://splatoonwiki.org/wiki/Splattercolor_Screen)、[GoNintendo](https://gonintendo.com/contents/29715-splatoon-3-players-experiencing-photosensitivity-and-sensory-issues-due-to-the) |
| 《斯普拉遁》系列受伤 | 墨从屏幕边框往里侵入 | 墨从四周吃，不挡中心 | [Inkipedia · Damage](https://splatoonwiki.org/wiki/Damage) |
| 《马力欧卡丁车》系列 乌贼 | 竞速游戏里把墨喷满前方玩家的挡风玻璃，几秒后褪去 | 竞速 + 墨挡视线，墨团"打到镜头上" | [Mario Kart Racing Wiki · Blooper](https://mariokart.fandom.com/wiki/Blooper) |
| 《大神》 | 画笔技能时游戏暂停，叫出画布作画 | 把画面"变成纸"的仪式感 | [Wikipedia · Ōkami](https://en.wikipedia.org/wiki/%C5%8Ckami) |
| 《绝区零》仪玄 | 墨占画面 60–80% 只在整屏压暗的时刻，金光从墨里透出 | 本项目正向配色的出处 | [yixuan-palette.md](yixuan-palette.md) |
| 《黑神话：悟空》第三回片尾 | 黑白水墨动画 | 当代水墨动态语言 | [Global Times 2024-09](https://globaltimes.cn/page/202409/1320307.shtml) |
| 《山水情》（1988，特伟，上海美术电影制片厂） | 墨在湿纸上晕开、浓淡层叠 | 墨晕开的节奏与留白 | [Wikipedia](https://en.wikipedia.org/wiki/Feeling_from_Mountain_and_Water)、[Columbia Journal of Asia 2022](https://journals.library.columbia.edu/index.php/cja/article/view/9275) |
| 电影 iris 转场 | 圆缩到画面某一点 | 收到施法者身上 | [Wikipedia · Wipe (transition)](https://en.wikipedia.org/wiki/Wipe_(transition)) |
| URP 14 Full Screen Pass / Fullscreen Master Stack | 三个注入点；Requirements = Color 时屏幕色拷到 `_BlitTexture` | 实现基础 | [Full Screen Pass](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/renderer-features/renderer-feature-full-screen-pass.html)、[Fullscreen Master Stack](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/urp-shaders/fullscreen-master-stack-reference.html) |
| Flowmap 驱动的溶解 | flowmap 推动溶解噪声，边缘在流；遮罩梯度上取两个阈值，一条发光、一条焦黑 | 墨边会流动 | [deepspacebanana · Flowmapped Burn Shader](https://deepspacebanana.github.io/blog/shader/art/unreal%20engine/Flowmapped-Burn-Shader) |
| 水彩 / 水墨的边缘加深 | 颜料在湿区边缘堆积，墨块的边比内部深 | "墨"和"黑色剪影"的区别 | [Curtis et al., SIGGRAPH 1997](https://www.cs.princeton.edu/courses/archive/fall00/cs597b/papers/curtis97.pdf) |

### 附录 2：五个方向（摘要）

| | 方向 | 观感 | 开发量 | 第二轮的用处 |
|---|---|---|---|---|
| A | 墨晕吞没 | 墨从施法者处像墨滴入水一样洇开，前沿是流动墨边加内侧金光 | 中 | H1 视界边缘、H3 爆后主体 |
| B | 泼墨上屏 | 3–5 团湿墨从施法者方向贴到镜头上，晕开连成一片 | 中偏高 | 受害者专属点缀（可选） |
| C | 墨帘合拢 | 上下两幅飞白毛边的墨帘合拢，中缝金光被挤灭 | 低 | 不用 |
| D | 墨圈收拢 | 墨从四周渗入，收成光圈，收到施法者身上熄灭 | 低到中 | 不用（H2 的"四周被掏空"是同一类读法） |
| E | 万墨归一 | 赛道上的墨烟、墨片卷向施法者，最后冲向镜头 | 高 | 三个方案的世界空间被吸物 |

第一轮各方向的完整分镜、实现要点和风险没有留在本文里：本文没有提交过，第二轮是直接改写的。对照页下半部分仍保留这五个方向的动态示意。第一轮推荐的是 A；用户在第二轮改为保留黑洞，所以 A 现在只作叠加层。
