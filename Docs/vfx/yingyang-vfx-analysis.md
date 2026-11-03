# Realistic Ying Yang VFX 解析：它的水墨是怎么做出来的

用户 2026-10-28 晚导入了 Piloto Studio 的 Realistic Ying Yang VFX，要求"解析里面的水墨特效内容，实际观察他们到底是怎么做的，并且尝试进行复用"。本文是实际观察的结果：在编辑器里逐个播放 21 个主效果、截取时间切片、读出每个粒子系统的模块与材质，再反查共用 shader 的节点图。复用试验的结果在第 5 节。逐系统数据见 [yingyang/systems.md](yingyang/systems.md)（由编辑器读出生成）。

## 1. 结论先行

1. **一个 shader 管全部。** 166 个材质用同一个 Shader Graph `Piloto Studio/UberFXSG`，4 个用扭曲 shader `Piloto Warp`。水墨感不来自特殊的 shader 技巧，而来自**贴图的形 + 每个粒子随寿命的侵蚀曲线 + 噪声扭曲 UV**。
2. **墨 = 黑色顶点色 × Alpha 混合**（SrcAlpha / OneMinusSrcAlpha）。21 个主效果共 483 个启用的粒子系统（用到 103 个材质），其中 352 个是 Alpha 混合，167 个起色是纯黑 `#000000`；白色高光和辉光是同一 shader 的 Add 混合（SrcAlpha / One）。
3. **形状全部来自画好的或拍下来的素材，噪声只做"吃掉"和"扰动"。** 三类贴图分工清楚：
   - 手绘 / 渲染的序列帧给体积：烟团 8×8、液体飞溅 7×7、空爆 6×6。
   - 手绘道具给符号：太极、竹子、竹叶、墨溅、地面墨树。
   - 工具贴图给结构：一条上缘不规则的亮带 `Tx_Line_*`、各种渐变、柔和噪声。
4. **冲击波 / 墨环都是网格不是面片。** 极坐标 UV 的盘 `UV_Distorted_PolarCords`、长锥 `Cone_LongAOE`、宽锥 `ConeTapered_Wide`，配一张"亮带"贴图沿 V 方向放，随寿命放大并被侵蚀，边缘被噪声扭成毛笔边。
5. **层次靠数量和排序，不靠单层精细。** 一个效果 9–52 个系统：中心一团黑雾 → 墨环网格 → 烟团序列帧 → 白色辉光 → 亮点碎屑 → 竹叶 / 太极符号。排序用渲染器的 sortingOrder（烟在 −500，环在 35）。
6. **它是按暗场景调的。** 在我们的亮沙地上，半透明黑变成灰褐，白色 Add 几乎看不见（第 3 节的截图每张都有上下两行对比）。

## 2. 共用做法

### 2.1 UberFXSG 的输入（节点图反查）

| 输入 | 来源 | 作用 |
|---|---|---|
| `_MainTex` + 通道选择 | 贴图 R / G / B / A 任选一通道 | 颜色或亮度的形；可平移（`_MainTexturePanning`） |
| Main Alpha 通道选择 | 同一张图的另一通道 | 形的透明度 |
| `_AlphaOverride`（`_USEALPHAOVERRIDE`） | 第二张图，自带平移与缩放 | 用另一张图当透明度：烟团序列帧、液体飞溅序列帧、上下渐变都放这里，`_MainTex` 只给内部纹理 |
| `_DetailNoise` + 五个通道选择 | 一张噪声图，可平移 | 分别驱动：UV 扭曲（`_DistortionIntensity`）、侵蚀阈值的噪声、乘法噪声、加法噪声、顶点偏移 |
| 色带（`_USERAMP`） | `_WhiteColor` / `_MidColor` / `_LastColor`、`_MiddlePointPos` | 把形的亮度映射到三色；太极、墨烟用它把灰度拉成纯黑 / 纯白 |
| 软粒子（`_USESOFTALPHA`，`_SoftFadeFactor`） | SceneDepth | 贴地或穿插处淡出，需要深度纹理 |
| Fresnel（`_FRESNEL`） | 视角 | 只在护盾的球上用 |
| 顶点色 | 粒子 startColor × colorOverLifetime | RGB 乘颜色，A 乘透明度。**墨就是这里给的黑** |
| UV0.z = Custom1.x | 粒子自定义数据 | 颜色亮度倍率（HDR）。常见值 1–25，常配"从 1 到 0"的曲线让光随寿命熄灭 |
| UV0.w = Custom1.y | 粒子自定义数据 | 侵蚀阈值，和噪声比较后 smoothstep，越大吃得越多，1 = 全无。典型曲线 `1 → 0.46（第 1–2 % 寿命）→ 1`：一出生就出现，随后被噪声慢慢吃掉，像墨迹干涸 |
| UV1.xy = Custom1.zw | 粒子自定义数据 | 主贴图 UV 偏移（`_USEUVOFFSET`）；另一路做顶点偏移的倍率 |

结论：一个粒子的"墨感"=（画好的形）×（随寿命变化的侵蚀）×（随时间流动的噪声扭曲）。粒子系统只负责发射时机、尺寸曲线和颜色曲线。

### 2.2 形从哪来

总表见 [yingyang/_textures.jpg](yingyang/_textures.jpg)，每张图的 RGB 与 Alpha 分开看。

| 类 | 例子 | 怎么用 |
|---|---|---|
| 画好的序列帧 | `Smoke_cloud_Sharp_1_render_all`（2048²，8×8 墨烟团）、`Water_Splash_9`（2048²，7×7，形在 A 通道）、`aerial_explosion_3`（4096²，6×6 空爆）、`Smoke_loop_thingy`（4096²，5×5 循环烟）、`Simple_Smoke_disipassion`（4096²，5×5 消散） | 放在 `_MainTex` 或 `_AlphaOverride`，粒子的 Texture Sheet Animation 按整张表在寿命内播完；烟团系统 27 处复用同一个材质 |
| 画好的道具 | `yingyang_1/2`（太极）、`yingyang_3`（四朵祥云）、`bamboo_tree_1–4`、`bamboo_leaf_1`（2×2）、`Pulse_floor_1_color`（地面墨树）、`Ink splat smudged`、`Liquid_smudge_pb_1/3`（2×2 墨团） | 单张面片，配色带变成纯黑 / 纯白，再配侵蚀曲线消散 |
| 工具贴图 | `Tx_Line_02`（下黑上亮、上缘不齐的带）、`Tx_Line_05_Up`（带破碎上缘的亮带）、`Tx_Liquid_01_2`（向下流的墨柱）、`Gradient_maker_*`、`FX_TX_HorizontalGradient_04`、`Noise_TilingSoftWebbing`、`TX_Noise_01`、`Tx_Noise_20` | 在极坐标盘 / 锥上沿径向铺"亮带"就是冲击波；`Tx_Liquid_01_2` 当透明度就是向外流的墨；噪声只负责扭和吃 |

### 2.3 网格

`UV_Distorted_PolarCords`（极坐标盘，几乎所有环都用它）、`Cone_LongAOE`（向前的长锥：Frontal 的扇形墨冲）、`ConeTapered_Wide`（宽锥：Burst Shockwave、护盾闪电环）、`Vertical_srike_cilinder_new`、`Jar_shape_tube`（竖向柱 / 罐：Pulses、Rune Fall 的上冲）、`RadialUV_Dense_UVFlipped`、`Sphere` / `SphereIn`（护盾）、`High_Poly_flat_ring`（Rune 的墨笔环）。

### 2.4 叠放与时序（多数效果的共同骨架）

1. t = 0：中心一团黑雾。`FuzzAlpha` / `FuzzAlpha_Soft` 是默认圆点贴图、纯黑、尺寸 3–15，寿命 0.4–2 s，sortingOrder 偏负（在后面），给出"墨的体积"。
2. t = 0–0.25：两到五个墨环网格依次出现（各自 startDelay 0 / 0.1 / 0.2 / 0.25），尺寸曲线从 0.5–0.8 长到 1，侵蚀曲线从 1 迅速降到约 0.5 再慢慢回到 1。
3. t = 0–3：烟团序列帧向外散、慢慢被吃掉。
4. 同时：白色辉光（`FuzzAdd`，尺寸 12–15 的大柔光）、拉伸的亮点碎屑（`Flecks_Shiny_Additive`，Stretch，0.02–0.07）、竹叶（2×2，绿到深绿的随机色，环绕 + 噪声，带旋转）。
5. 循环组（Loop）里放长寿命的道具：太极符号 4–9.5 s、地面墨树 6 s、祥云 3–4 s。

### 2.5 在亮沙地上为什么变灰

半透明黑叠在亮沙上会成为介于两者之间的灰褐；`FuzzAdd` 这类白色加色在亮背景上几乎没有反差。包里的黑雾中心层（尺寸大、透明度低）在亮背景上尤其像一团脏雾。这和我们第一、二次画面评审的结论一致（[push-projectile-hands.md 第 12、13 节](push-projectile-hands.md)）。要在赛道上用它，墨的核心要更实、边缘更窄，亮部换成金色并降低占比。

## 3. 逐个特效

每张图上排是预览台的亮沙地，下排是暗底，三列是 t = 0.3 / 1.0 / 2.5 s（编辑器里 Simulate，取景框住 10–90 % 的粒子）。系统数是 prefab 里全部 / 实际启用的粒子系统。

### 3.1 Ying Yang Orb（18 / 14）

![](yingyang/Ying_Yang_Orb.jpg)

太极球：起手一圈墨环向外翻（0.2 s 延迟），1 s 时太极符号出现并慢转。结构是两组：Spawn 一次性爆发 + Loop 循环层。
- 太极：`yy_2_symbol_alpha_soft`，`yingyang_2` + 色带 白 / `#3A3A3A` / 黑，`_DetailNoise` 扭曲 0.02，寿命 9.5 s，自转 0.7 rad/s。
- 外圈墨环：`yy_Shockwave_1`，极坐标盘 + `Tx_Line_02_up_nomipmap`，按 V 平铺 5 次，用 `Gradient_maker_double_Fade` 当透明度，扭曲强度 2。墨环的毛边就是这次大扭曲。
- 球内一层慢流的柔光：`Mana_Gather_in_aura`，Sphere 网格，Add。
- 外围烟：`Smoke_loop` 序列帧，5×5，排序 −500，从球面向内收（radial −0.25 至 −0.5）。
- 地面黑影：`FuzzAlpha` 在球下 −1.1。
- 绕球的竹叶：Donut 形状，绕轴转 + 噪声。

可复用：太极贴图、墨环材质、"球 + 外圈烟 + 绕飞小物"的结构，可以当掌心光球的蓝本。

### 3.2 Ying Yang Frontal（28 / 25）

![](yingyang/Ying_Yang_Frontal.jpg)

向前推出的扇形墨冲，最接近"一掌推出"。分两组：holder_1（向前）和 Center（原地）。
- 向前三层：`Ink_Shockwave_1_Distorted`、`Blood_Dark_Shockwave_05_Distorted`、`Explosion_Shockwave_04_Distorted` 都画在 `Cone_LongAOE` 长锥上，3D 尺寸 x25 y15，延迟 0.1–0.25，寿命 1–3 s。透明度用 `Tx_Liquid_01_2`（向下流的墨柱）并把 V 翻转成向外流，扭曲 0.3–0.35，噪声 `Noise_TilingSoftWebbing` 向下平移。
- 拉伸的墨屑：`Flecks_Clipped_Alpha` 用 3×3 序列帧，Stretch。
- 墨溅：`yy_Liquid_Splat_Frontal`，贴图是 `Ink splat smudged`，寿命 5 s。
- 烟团：8×8 序列帧。
- 液体飞溅：`Simple_Liquied_fb`，7×7 序列帧放在透明度槽。
- 中心：一团白色大柔光 `FuzzAdd_Soft`（尺寸 15）、墨环、竹叶，以及两张太极符号（`yy_3` 祥云、`yy_2`）。

画面上 0.3 s 时是一团白光加向前喷的黑色墨丝，1 s 时变成贴地向前的黑色笔触与烟，2.5 s 只剩地上一道道墨痕。可复用：长锥 + 墨柱透明度 + 噪声扭曲，就是"劲风 / 推掌冲击"。

### 3.3 Ying Yang Frontal Pulses（20 / 17）

![](yingyang/Ying_Yang_Frontal_Pulses.jpg)

Frontal 的循环版。脉冲层用面片版的墨冲（`ink_short_Dark_Shockwave_05`，3D 尺寸 x7.5 y23）。循环层是两张大面片：
- `bamboo_pulse_ink_1`：竹林剪影 + 遮罩显形。
- `Tree_pulse_floor_ink_1`：贴地的墨树。

两张寿命都是 6 s，下层还有烟和太极祥云。可复用：墨树、竹林作为地面装饰。

### 3.4 Ying Yang Buff（17 / 14）

![](yingyang/Ying_Yang_Buff.jpg)

施法 buff：先一个黑色墨球（多层 `FuzzAlpha_Soft` 黑雾 + 墨环），再从球顶升起一棵烟"树"（烟团序列帧向上，`Ink_Vertical_trail` 竖向拉伸），配 `yy_1` 太极。寿命最长 12 s（循环），爆发组 1 s 内完成。可复用：作为施法确认，"墨球炸开 + 烟往上升"的节奏。

### 3.5 Ying Yang Buff up（31 / 26）

![](yingyang/Ying_Yang_Buff_up.jpg)

强化起手：地面一圈墨溅（`Ink_Splash_Radial_Wide`，极坐标盘）、中间竖起的墨柱（`Ink_Beam_Rotated` 竖向 Stretch）、`Trail_WispyGeneric` 竖向烟线、两道超宽横向光晕（`Flare_Ultrawide`），还有一个扭曲环（`Ring_Smudge_1_warp`，Piloto Warp，需要不透明纹理）。全部在 0.25 s 内起、2.5 s 内结束。可复用：竖向墨柱 + 地面墨溅，适合"施法确认爆发"，但扭曲层在 Balanced / Performant 下失效。

### 3.6 Ying Yang Burst Shockwave（14 / 12）

![](yingyang/Ying_Yang_Burst_Shockwave.jpg)

小型爆发，最短：寿命 ≤ 1.5 s，延迟 ≤ 0.1。两层宽锥冲击（`ConeTapered_Wide`）、一层 `aerial_explosion_3` 空爆序列帧（Add）、一层 `Smoke_loop` 墨烟（Stretch）、闪光、亮点，还有一个扭曲水晶（Warp）。可复用：消散或命中的"小爆"。

### 3.7 Ying Yang Smoke（19 / 17）

![](yingyang/Ying_Yang_Smoke.jpg)

循环的地面墨烟。中心一圈墨环（极坐标盘）上叠：
- `Liquid_smudge_pb_3` 墨团；
- 风感的宽环（`Desat_wind_Shockwave3_wide`，Add，噪声快速向下平移 −0.5）；
- 竖直的风（`verticalwind`，Stretch）；
- 祥云和太极；
- 竹叶。

可复用：烟团序列帧和墨团贴图做消散；地面层和"脚下无墨"冲突。

### 3.8 Ying Yang Vertical Explotion（26 / 23）

![](yingyang/Ying_Yang_Vertical_Explotion.jpg)

竖直的墨爆：
- 一个向内收的径向盘（三层 `*_Radial_disk_in_*`）：主贴图向内快速平移（`_MainTexturePanning` −1 到 −2），做"被吸进去"。
- 收完后向上炸出烟团序列帧（`Smoke_cloud_Sharp`）。
- 竖向拉伸的液体飞溅（`Simple_Liquied_fb`，Stretch）。
- 墨团、墨环、竹叶、祥云。

可复用："先收后放"的节奏和径向向内的盘，适合蓄力。

### 3.9 Ying Yang Beam（40 / 35）

![](yingyang/Ying_Yang_Beam.jpg)

光束：
- 光束主体是三层 Stretch 的冰 / 火光束材质（`Ice_beam_*`、`Beam_Fire_Bg_Add`）。
- 发射口：墨环、烟团、两层太极。
- 外圈：黑色竖向长条 `FuzzAlpha_Soft`（3D 尺寸 y10）做墨线。
- `mat_GOW_Flare_Cross_Add` 十字星光。
- 亮点碎屑延迟 3 s 才出现，配合光束结束。

可复用：发射口的组合。光束本体是蓝白色，不是墨。

### 3.10 Ying Yang Big Explotion（40 / 36）

![](yingyang/Ying_Yang_Big_Explotion.jpg)

大型地面爆炸：Spawn 组的五层墨环（`yy_Shockwave_3/5/6_Distorted_still`，扭曲强度 0.3–1.13，`Tx_Line_04/05_Up` 的破碎上缘）层层外扩，Burst 组是三张竖立的竹林面片（VerticalBillboard）、烟团、液体飞溅、墨屑，另有一个挂在相机上的屏幕层（`Screenspace` 组，脚本 `ScreenSpacePlaneAutoScale_FOV` + `MainCameraAutoParent`）。可复用：多层墨环的错时外扩。屏幕层不能直接用（会挂到每个客户端的主相机）。

### 3.11 Ying Yang Flash AoE（28 / 24）

![](yingyang/Ying_Yang_Flash_AoE.jpg)

白色光罩 + 墨环：中心一个大白光（`LightFlash`、`DiagonalFlare`），地面几层墨环与烟，空中一个太极。0.3 s 时是白色半球，1 s 时成为墨环加墨烟。可复用：出掌闪帧的"白光一闪，转成墨"。

### 3.12 Ying Yang Floor Shockwave（18 / 16）与 Ying Yang pulse（9 / 7）

![](yingyang/Ying_Yang_Floor_Shockwave.jpg)

![](yingyang/Ying_Yang_pulse.jpg)

贴地的墨池冲击波：
- 墨团 `Liquid_smudge_pb_3`：一张带 `_nac`（无 alpha clip），一张带 clip。
- 多层极坐标墨环：`Ink_Shockwave_1`、`Shockwave_blood_Distorted_MD`、`Explosion_Shockwave_04/06`。
- 加上柔光和亮点。
- pulse 是它的精简版，只有 7 个系统。

可复用：墨环材质组合；整体是地面层，和"脚下无墨"冲突。

### 3.13 Ying Yang Nova Shockwave（16 / 14）

![](yingyang/Ying_Yang_Nova_Shockwave.jpg)

空中的球形冲击：几层墨环朝向各异，叠出一个球壳，配闪光与烟。全部在 2.5 s 内结束。可复用：命中或消散时的"墨球炸开"。

### 3.14 Ying Yang Pulses（43 / 31）

![](yingyang/Ying_Yang_Pulses.jpg)

太极球 + 地面墨山：
- 包含 Orb 的全部层。
- 加两个竖向冲击柱（`Vertical_srike_cilinder_new`）。
- 地面四张祥云（`yy_3_symbol`）。
- 一个屏幕层。

画面上 2.5 s 时地面出现一片水墨山，背景被大面积压暗。可复用：祥云贴图；整体太大太重。

### 3.15 Ying Yang Rain（27 / 24）

![](yingyang/Ying_Yang_Rain.jpg)

墨雨：地面墨池（两层 `Ink_Shockwave_4_Distorted`）+ 从上方落下的竖向拉伸雨线（`Nothing_alphaclip`、`Spark2_Alpha`，Stretch）。Rain 组有子发射器：落地溅墨（`Ink_flakes`、`Simple_Liquied_fb`）。可复用：子发射器做"墨滴落地溅开"。

### 3.16 Ying Yang Rune（38 / 32）

![](yingyang/Ying_Yang_Rune.jpg)

地面太极符阵：
- 一张平放的太极（`yy_2`）。
- 外圈一道毛笔环 `Slash_Ink_Fill_alpha`：`High_Poly_flat_ring` 网格 + `InkStroke_Test_1` 墨笔扫，U 方向反向平铺 1.5 次，透明度用 `gradient_004`。这是包里唯一直接用"毛笔扫"贴图画的笔触。
- 符阵消失时炸出一片墨溅（`Liquid_Splat_radial_ink`，`Blood_Decal_Omni4_em` 贴图 + 色带）。

可复用：**毛笔环的做法**（环形网格 + 笔扫贴图 + 平移），可直接改成空中的一笔墨圆或劲风弧线。

### 3.17 Ying Yang Rune Fall（52 / 48）

![](yingyang/Ying_Yang_Rune_Fall.jpg)

最大的一个：
- 地面星形冲击（Jar_shape_tube 网格 + 墨环）。
- 竖直光柱（`Holy_Long_Beam_Vertical`）、落下的光雨（`Bullet_rain`）。
- 2.5 s 时一大圈墨烟环（多层烟团序列帧，排序很靠后）。

可复用：大墨烟环的烟团排布，但规模远超单掌预算。

### 3.18 Ying Yang Vertical Rune（49 / 46）

![](yingyang/Ying_Yang_Vertical_Rune.jpg)

竖向符文：
- Build Up Floor 组（18 个系统）在地面蓄力。
- Momentum 组（28 个系统）是竖立的墨光柱：`yy_Beam_1_Rotated`，`FX_Flare_Ultrawide_Rotated` + `Square_Light_Blurr` 透明度，扭曲 0.22。
- 加竖向墨线、烟、太极。

可复用：竖立墨光柱的材质，可做掌的"墨色拖影"。

### 3.19 Ying Yang Shield（24 / 21）

![](yingyang/Ying_Yang_Shield.jpg)

太极护盾，是唯一用 Fresnel 的效果：
- 球壳：`yy_1_Wall_Frenel`，球体 + Fresnel，Alpha。
- 内壁：`yy_1_Wall_Fill`，SphereIn，Add。
- 流线：`yy_1_shield_lines`，彩虹噪声向下平移。
- 球内太极，加闪电环（`Necromancy_Shockwave_ring_Lightning`）。
- 结束时的消散序列帧 `Smoke_Disipation_Explode_FB`。

可复用：消散序列帧的用法；护盾本体不相关。

### 3.20 Ying Yang Screen Space FX（4 / 1）

屏幕空间的边框（`ScreenSpace_YingYang`，`RadialUV_Door` 网格，焦散噪声），由脚本挂到主相机前。联机下会作用于每个客户端，不复用。图略。

## 4. 能借到如来神掌哪一层

| 我们的层（[push-projectile-hands.md 第 4 节](push-projectile-hands.md#4-分层拆解表正常版本)） | 包里最接近的做法 | 要改什么 |
|---|---|---|
| 主体（发光的掌） | 没有手形。Orb 的"太极面片 + 外圈烟 + 内部慢流光"是最近的结构 | 主体仍是我们的佛手网格；可借它的球内慢流光材质（`Mana_Gather_in_aura`）思路 |
| 蓄力 | Vertical Explotion 的径向向内盘；Buff 的墨球 | 缩小，墨环换成金 |
| 劲风 / 出掌冲击 | Frontal 的长锥墨冲（`Cone_LongAOE` + `Ink_Shockwave_1` / `Blood_Dark_Shockwave_05` + `Tx_Liquid_01_2`）；Rune 的毛笔环 | 长锥朝后；寿命压到 ≤ 0.4 s；墨的核心加实 |
| 飞行拖尾 | 包里没有现成拖尾；用 8×8 烟团序列帧在 World 空间按距离发射，配侵蚀曲线 | 改 World 模拟、按距离发射 |
| 消散 | Burst Shockwave、Nova Shockwave、`Smoke_Disipation_Explode_FB` | 缩小 |
| 施法确认爆发 | Buff / Buff up 的墨球与竖向墨柱 | 去掉扭曲层 |
| 闪帧 | Flash AoE 的白光转墨 | 白光改成暖白金 |

## 5. 复用试验

（复用小样做完后在此记录：用了哪些 prefab / 材质，改了什么，截图路径，结论。）

## 6. 数据来源与工具

- 截图：编辑器内 `ParticleSystem.Simulate`，预览台场景（`PalmVfxPreview.BuildStage`），每个效果 3 个时刻 × 亮 / 暗两种背景，存在 [yingyang/](yingyang/)。
- 逐系统数据：编辑器脚本读 prefab 的全部模块，生成 [yingyang/systems.md](yingyang/systems.md)。
- shader：`Assets/Plugins/Piloto Studio/Shaders_Reforged/UberFXSG.shadergraph` 的节点与连线。Custom1 各分量的用途是从连线反查出来的，不是文档写的。
- 动态观察：在 Editor 菜单 `Tools/BuddahGo/VFX/Rebuild VFX Sandbox` 重建调试场后，这 42 个 prefab 都在列表里，可以慢放、暂停、换机位。
