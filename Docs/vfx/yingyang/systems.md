# Realistic Ying Yang VFX：逐系统数据

由 Unity 编辑器从 `Assets/Piloto Studio/Super Realistic FX Bundle 02/Realistic Ying Yang/*.prefab` 读出（2026-10-28），只列启用且有材质的粒子系统。解读见 [../yingyang-vfx-analysis.md](../yingyang-vfx-analysis.md)。

列说明：渲染 = 渲染模式（Mesh 后是网格名）；混合 = 材质的 Src/Dst（Alpha = SrcAlpha/OneMinusSrcAlpha，Add = SrcAlpha/One）；贴图 = MainTex / AlphaOverride / DetailNoise；标志 = soft 深度软化、ramp 三色映射、dist 噪声扭曲强度；C1 = 自定义数据 Custom1 的 x（亮度倍率）与 y（侵蚀阈值）曲线，`x2 [0:1 1:0]` 表示倍率 2、从 1 到 0 的曲线；起色 = startColor（黑色即墨）。

## Ying Yang Beam（40 个系统，启用 35）

| 系统 | 渲染 | 混合 | 贴图 | 标志 | 寿命 | 延迟 | 尺寸 | 发射 | 序列帧 | C1 | 起色 | 排序 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| DefaultHDParticleMaterial/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 1..1.5 | 3 | 0.05..0.03 | 率 0 爆发[0s x0] | 2x2 | x x25 [0:1 1:0] · y x2 [0:0.5 1:1] | #FFFFFFFF | 15 |
| DefaultHDParticleMaterial/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 1..2 | 3 | 0.05..0.02 | 率 100 爆发[0s x0] | 2x2 | x x15 [0:1 1:0] · y x2 [0:0.5 1:1] | #FFFFFFFF | 15 |
| DefaultHDParticleMaterial/FuzzAlpha_Soft | Stretch | Alpha | Default-Particle / – / Default-Particle | soft | 0.25 | 3 | 3D x1.2 y10 z1 | 率 8 爆发[0s x0] |  | x x1 [0:0.11 0.127:1 1:0.416] · y 0 | #000000FF | 6 |
| DefaultHDParticleMaterial/FuzzAlpha_Soft | Stretch | Alpha | Default-Particle / – / Default-Particle | soft | 0.25 | 3 | 5..5 | 率 8 爆发[0s x0] |  | x x1 [0:0.11 0.127:1 1:0.416] · y 0 | #000000FF | -25 |
| DefaultHDParticleMaterial/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 3 | 3 | 8.6 | 率 0 爆发[0s x1] |  | x x8 [0:1 1:0.487] · y 0 | #000000DB | -25 |
| DefaultHDParticleMaterial/Ice_beam_Bright_Bg_add_Soft | Stretch | Alpha | Tx_Noise_20 / Gradient_Quad02 / Tx_Noise_12 | soft dist 1.4 | 3 | 3 | 3D x3.5 y40 z1 | 率 0 爆发[0s x1] |  | x x24.757 [0:0.508 1:1] · y x2 [0.944:0.577 1:1] | #000000FF | 5 |
| DefaultHDParticleMaterial/Trail_Flame_blue_soft_alpha | Stretch | Alpha | – / Trail_TaperedSoft_02 / Tx_Noise_10 | soft dist 0.1 | 3 | 3 | 3D x5 y43.9 z1 | 率 0 爆发[0s x1] |  | x x25 [0:0.503 1:1] · y x2 [0.9:0.5 1:1] | #FFFFFFFF | 8 |
| DefaultHDParticleMaterial/Ice_beam_center_alpha_soft | Stretch | Alpha | Tx_SmokeTrail_01 / Gradient_Quad02 / Tx_Noise_10 | soft dist 0.1 | 3 | 3 | 3D x18 y33.01 z1 | 率 0 爆发[0s x1] |  | x x100 [0:0.503 1:1] · y x2 [0.9:0.5 1:1] | #FFFFFFFF | 6 |
| DefaultHDParticleMaterial/Trail_Flame_blue_soft_alpha | Stretch | Alpha | – / Trail_TaperedSoft_02 / Tx_Noise_10 | soft dist 0.1 | 3 | 3 | 3D x1.5 y50 z1 | 率 0 爆发[0s x1] |  | x x25 [0:0.503 1:1] · y x2 [0.892:0.764 1:1] | #000000FF | 6 |
| DefaultHDParticleMaterial/Ice_Beam_muzzle_Connecto_add_soft | Stretch | Add | Tx_Noise_20 / Trail_TaperedSoft_02 / Default-Particle | soft | 0.25 | 3 | 3D x0.5 y13.4 z1 | 率 6 爆发[0s x0] |  | x x91.449 [0:0.513 0.241:1 1:0.416] · y 0 | #FFFFFFFF | 7 |
| DefaultHDParticleMaterial/Beam_Fire_Bg_Add | Stretch | Add | Trail73 / gradient_004 / Tx_Noise_30 | dist 0.1 | 3 | 3 | 3D x4.11 y50 z1 | 率 1 爆发[0s x1] |  | x x80 [0:0.599 1:1] · y 1 | #FFFFFFFF | 25 |
| DefaultHDParticleMaterial/Beam_Glow_edge_alpha | Stretch | Alpha | Gradient_maker_Double_gradient_inverted / gradient_004 / Tx_Noise_07 | dist 0.1 | 3 | 3 | 3D x3 y36.42 z1 | 率 0 爆发[0s x1] |  | x x1 [0:0.497 1:1] · y 1 | #FFFFFFFF | -2 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/yy_Shockwave_1_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | soft dist 2 | 1 | 3 | 4 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.013:0.459 1:1] | #FFFFFFFF | -5 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/Ink_Shockwave_1_Distorted_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02 / FX_TX_HorizontalGradient_04 / TX_Noise_01 | soft dist 0.35 | 1.5 | 3 | 3.5 | 率 0 爆发[0s x1] |  | x x5 [0:0.75 0.023:1 1:0] · y x2 [0.197:0.467 1:1] | #000000AF | -5 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/Shockwave_blood_Distorted_MD_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Mask_02 / Tx_Noise_29 / Tx_Noise_17 | soft ramp dist 0.1 | 0.9 | 3 | 3.5 | 率 0 爆发[0s x1] |  | x x500 [0:1 0.038:0.27 1:0] · y x2 [0:0.589 0.493:0.635 1:1] | #000000AC | 0 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/Explosion_Shockwave_06_Distorted_necro_add_soft | Mesh UV_Distorted_PolarCords | Alpha | OffsetLine_Warped2 / Tx_Liquid_01_2 / PearlyRainbowNoise | soft dist 0.1 | 1 | 3 | 2..5 | 率 0 爆发[0s x5] |  | x x100 [0:1 1:0.004] · y x2 [0.493:0.375 1:1] | #000000FF | 0 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/yy_Shockwave_3_Distorted_still_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Gradient_Quad03 / Tx_Noise_09 | soft ramp dist 0.3 | 1.5 | 0 | x3.5 [0:1 1:0.411] | 率 1 爆发[0s x1 cyc12/0.25] |  | x x10 [0:0 1:1] · y 1.29 | #000000D0 | -254 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/yy_Shockwave_1_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | soft dist 2 | 1.5 | 0.2 | x3 [0:1 1:0.488] | 率 0 爆发[0s x1 cyc12/0.25] |  | x x1 [0:0 1:1] · y x2 [0:1 0.5:0.583 1:1] | #FFFFFFFF | -5 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 5.5 | 0 | 6.28 | 率 0 爆发[0s x1] |  |  | #FFFFFF36 | 6 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 6 | 0 | 3 | 率 0 爆发[0s x1] |  | x x0 [0:0 1:1] · y 0 | #FFFFFF36 | 7 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 6 | 0 | 0.35 | 率 0 爆发[0s x1] |  | x x196.075 [0:0.069 0.499:1] · y 0 | #FFFFFF36 | 8 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/Flecks_Clipped_Alpha 1 | Stretch | Alpha | Fleks_AlphaClipped / – / FireNoise | dist 0.07 | 2..1 | 0 | 0.02..0.05 | 率 50 | 3x3 | x x50 [0:0.036 1:1] · y 0 | #FFFFFFFF | 0 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/Flecks_Goopy_Alpha | Stretch | Alpha | Flecks_Goopy / – / Noise_SuperContrasted | dist 0.1 | 0.5..1 | 0 | 0.08..0.02 | 率 50 | 2x2 | x x100 [0:0.036 1:1] · y 0 | #FFFFFFFF | 0 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/Flecks_Clipped_Alpha 1 | Stretch | Alpha | Fleks_AlphaClipped / – / FireNoise | dist 0.07 | 0.5..1 | 0 | 0.02..0.03 | 率 50 | 3x3 | x x250 [0:0.036 1:1] · y 0 | #FFFFFFFF | 0 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/mat_GOW_Flare_Cross_Add | Billboard | Add | Flare_Glint / – / – | soft | 3 | 0 | 4.58 | 率 0 爆发[0s x1] |  | x x50 [0:0.08 1:1] · y 0 | #FFFFFFFF | 1 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/bamboo_leaf_alpha_soft | Billboard | Alpha | bamboo_leaf_1 / – / – |  | 1.25..1.75 | 3 | 0.2..0.1 | 率 35 爆发[0s x35] | 2x2 | x x2 [0:1 1:0] · y x2 [0:0.5 1:1] | #D7FF61FF..#10590CFF | 0 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/yy_2_symbol_alpha_soft | Billboard | Alpha | yingyang_2 / Gradient_maker_simple_round_radial / Tx_Noise_12 | soft ramp dist 0.02 | 1.5 | 3 | 6 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.048:0.682 1:1] | #FFFFFF9F | 0 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/yy_Shockwave_3_Distorted_still_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Gradient_Quad03 / Tx_Noise_09 | soft ramp dist 0.3 | 1.5 | 2.6 | x5 [0:0.566 1:1] | 率 1 爆发[0s x1 cyc12/0.25] |  | x x10 [0:0 1:1] · y 1.29 | #000000D0 | -254 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 0.5..1 | 3 | 4 | 率 10 爆发[0s x0] | 8x8 | x 8.03 · y x2 [0:1 0.064:0.608 1:1] | #000000AA | -24 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/mat_GOW_Flare_Cross_Add | Billboard | Add | Flare_Glint / – / – | soft | 0.5 | 3 | 3D x8 y0.2 z1 | 率 5 爆发[0s x0] |  | x x150 [0:0.274 1:0.189] · y 0 | #FFFFFFFF | 5 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/mat_GOW_Flare_Cross_Add | Billboard | Add | Flare_Glint / – / – | soft | 0.5 | 3 | 3D x1 y2.5 z1 | 率 5 爆发[0s x0] |  | x x120 [0:0.274 1:0.189] · y 0 | #FFFFFFFF | 5 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 1 | 3 | 3..5 | 率 5 | 8x8 | x x25 [0:1 1:0.015] · y 0 | #717171FF | 0 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/mat_GOW_Flare_Cross_Add | Billboard | Add | Flare_Glint / – / – | soft | 1 | 3 | 5 | 率 0 爆发[0s x1] |  | x x120 [0:1 1:0] · y 0 | #FFFFFFFF | 5 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 6 | 0 | 6.97 | 率 0 爆发[0s x1] |  |  | #FFFFFFFF | -25 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 0.3..1 | 3 | 0.05..0.03 | 率 0 爆发[0s x25 cyc10/0.25] | 2x2 | x x25 [0:1 1:0] · y x2 [0:0.5 1:1] | #FFFFFFFF | 0 |

## Ying Yang Big Explotion（40 个系统，启用 36）

| 系统 | 渲染 | 混合 | 贴图 | 标志 | 寿命 | 延迟 | 尺寸 | 发射 | 序列帧 | C1 | 起色 | 排序 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Screenspace/yy_Shockwave_1_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | dist 2 | 1.5 | 4.9 | 3D x0.15 y0.15 z1 | 率 0 爆发[0s x2] |  | x x5 [0:1 0.334:0.078] · y x2 [0:1 0.218:0.534 1:1] | #FFFFFFFF | 1 |
| Screenspace/yy_Shockwave_1_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | dist 2 | 3 | 4.9 | 3D x0.15 y0.15 z1 | 率 0 爆发[0s x2] |  | x 0 · y x2 [0:1 0.218:0.534 1:1] | #000000FF | 1 |
| Spawn_1/bamboo_yy_1_alpha_soft | VerticalBillboard | Alpha | bamboo_tree_3 / gradient_004 / Tx_Noise_12 | soft dist 0.04 | 5.15 | 0 | 7.65 | 率 0 爆发[0s x1] |  | x 0 · y x2 [0:1 1:0.47] | #000000FF | -2 |
| Spawn_1/bamboo_yy_2_alpha_soft | VerticalBillboard | Alpha | bamboo_tree_2 / gradient_004 / Tx_Noise_12 | soft dist 0.04 | 5.15 | 0 | 9 | 率 0 爆发[0s x1] |  | x 0 · y x2 [0:1 1:0.47] | #000000FF | -2 |
| Spawn_1/bamboo_yy_3_alpha_soft | VerticalBillboard | Alpha | bamboo_tree_4 / gradient_004 / Tx_Noise_12 | soft dist 0.04 | 5.15 | 0 | 4 | 率 0 爆发[0s x10] | 2x2 | x 0 · y x2 [0:1 1:0.47] | #000000FF | -2 |
| Spawn_1/yy_Shockwave_5_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_04_Up / Tx_Mask_01_Clamped / Tx_Noise_05 | ramp dist 1.13 | 5.15 | 0.15 | 2.38 | 率 1 爆发[0s x1] |  | x x48.055 [0:0.087 0.903:0.289 1:1] · y x2 [0:1 0.048:0.589 1:0.496] | #B9B9B9D0 | 0 |
| Spawn_1/yy_Shockwave_5_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_04_Up / Tx_Mask_01_Clamped / Tx_Noise_05 | ramp dist 1.13 | 5.15 | 0.15 | 2.38 | 率 1 爆发[0s x1] |  | x x10 [0.023:1 1:0.284] · y x2 [0:1 0.064:0.548] | #000000D0 | 0 |
| Spawn_1/yy_Shockwave_3_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Tx_Mask_01_Clamped / Tx_Noise_09 | ramp dist 0.3 | 4 | 0.1 | 6.71 | 率 0 爆发[0s x1] |  | x x40 [0:1 1:0.004] · y x2 [0.537:0.641 1:1] | #00000092 | 0 |
| Spawn_1/FuzzAlpha | Billboard | Alpha | Default-Particle / – / Default-Particle |  | 5 | 0.2 | 26.57 | 率 0 爆发[0s x1] |  | x 1 · y 0 | #939393FF | -200 |
| Spawn_1/yy_2_symbol_alpha | Billboard | Alpha | yingyang_2 / Gradient_maker_simple_round_radial / Tx_Noise_20 | ramp dist 0.02 | 5 | 0.2 | 15.7 | 率 0 爆发[0s x1] |  | x 1 · y 0 | #909090BE | -50 |
| Spawn_1/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 3..2 | 0 | 5 | 率 15 爆发[0s x0] | 8x8 | x x1 [0:1 1:0] · y x2 [0:1 0.049:0.477 0.749:0.605 1:1] | #111111FF | -5 |
| Spawn_1/yy_Shockwave_6_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_04_Up / Tx_Mask_01_Clamped / Tx_Noise_05 | ramp dist 1.11 | x1 [0:1 1:1] | 1.5 | 2.1 | 率 0 爆发[0s x1] |  | x x50 [0:0 1:1] · y x2 [0:1 0.17:0.592 0.757:0.577 1:1] | c0.046#000000FF c0.13#171717FF c0.687#363636FF c1#FFFFFFFF a0=0.812 a0.462=0.994 a1=1 | 0 |
| Spawn_1/yy_3_symbol_alpha_soft | Billboard | Alpha | yingyang_3 / Gradient_maker_simple_round_radial / Tx_Noise_20 | soft ramp dist 0.01 | 4..3 | 0 | 3D x1.5 y3 z1 | 率 15 爆发[0s x1] | 2x2 | x 0 · y x2 [0.164:0.43 1:1] | #000000DD | 50 |
| Spawn_1/bamboo_leaf_alpha_soft | Billboard | Alpha | bamboo_leaf_1 / – / – |  | 3.5 | 1 | 0.1..0.12 | 率 35 爆发[0s x0] | 2x2 | x x2 [0:1 1:0] · y x2 [0:0.5 1:1] | #D7FF61FF..#10590CFF | 0 |
| Burst/yy_Shockwave_3_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Tx_Mask_01_Clamped / Tx_Noise_09 | ramp dist 0.3 | 1.5 | 5 | 4.73..5.81 | 率 0 爆发[0s x2] |  | x 2 · y x2 [0:0.432 1:1] | #000000B7 | 0 |
| Burst/yy_Shockwave_5_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_04_Up / Tx_Mask_01_Clamped / Tx_Noise_05 | ramp dist 1.13 | 2 | 5 | 5.62 | 率 1 爆发[0s x1] |  | x x48.055 [0:1 1:0] · y x2 [0:0.974 0.073:0.537 1:1] | #FFFFFFD0 | 0 |
| Burst/FuzzAdd | Billboard | Add | Default-Particle / – / Default-Particle |  | 2 | 5 | 13 | 率 0 爆发[0s x1] |  | x x6.808 [0:1 1:0] · y 1 | #909090E0 | 1 |
| Burst/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 0.4 | 5 | 12 | 率 0 爆发[0s x1] |  | x x35 [0:1 1:0] · y 1 | #989898FF | 1 |
| Burst/yy_Shockwave_1_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | dist 2 | 4..3 | 5.1 | 15..18.5 | 率 0 爆发[0s x2] |  | x x15 [0:0.75 0.023:1 1:0] · y x2 [0.537:0.398 1:1] | #636363FF | -5 |
| Burst/yy_2_symbol_alpha | Billboard | Alpha | yingyang_2 / Gradient_maker_simple_round_radial / Tx_Noise_20 | ramp dist 0.02 | 1.5 | 5 | 22.35 | 率 0 爆发[0s x1] |  | x 1 · y x2 [0:0.435 1:1] | #FFFFFFBE | -50 |
| Burst/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 2.5..5 | 5 | 0.05..0.01 | 率 1 爆发[0s x150] | 2x2 | x x167.767 [0:1 1:0] · y x2 [0:0.5 1:1] | #FFFFFFFF | 0 |
| Burst/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 2 | 5 | 12 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 0 | #000000FF | -2 |
| Burst/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 1..3 | 5 | 5 | 率 1 爆发[0s x50] | 8x8 | x x1 [0:1 1:0] · y x2 [0:1 0.049:0.477 0.749:0.605 1:1] | #000000C3 | -5 |
| Burst/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 0.5 | 5 | 8 | 率 0 爆发[0s x1] |  | x x4.854 [0:1 1:0] · y 0 | #000000FF | -2 |
| Burst/FuzzAlpha | Billboard | Alpha | Default-Particle / – / Default-Particle |  | 0.5 | 5 | 8 | 率 0 爆发[0s x1] |  | x x4.854 [0:1 1:0] · y 0 | #000000FF | -2 |
| Burst/Ink_flakes_1_alpha_soft | Billboard | Alpha | Liquid_smudge_pb_1 / Fuzzball_Clamped / Liquid_smudge_pb_1 | soft ramp dist 0.001 | 1.5..2 | 5 | 1..0.7 | 率 0 爆发[0s x35] | 2x2 | x x21.87 [0:1 1:0] · y x2 [0:0.542 1:1] | #FFFFFFFF | 1 |
| Burst/Flare | Billboard | Add | flare1_flare2 / – / – | soft | 0.3 | 5 | 18 | 率 0 爆发[0s x1] |  | x x15 [0:1 1:0] · y 1 | #6F6F6F98 | 50 |
| Burst/Flare | Billboard | Add | flare1_flare2 / – / – | soft | 0.3 | 5 | 18 | 率 0 爆发[0s x1] |  | x x15 [0:1 1:0] · y 1 | #6F6F6F98 | 50 |
| Burst/Ring_Halo_faded_add_soft | Billboard | Add | Halo_Faded / – / – | soft dist 2 | 0.4 | 5 | 12 | 率 0 爆发[0s x1] |  | x x2 [0:1 1:0] · y 0 | #48484866 | 2 |
| Burst/yy_Shockwave_3_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Tx_Mask_01_Clamped / Tx_Noise_09 | ramp dist 0.3 | 1.1 | 5 | 5..5 | 率 0 爆发[0s x1] |  | x 0 · y x2 [0.391:0.479 1:1] | #000000A6 | 0 |
| Burst/yy_3_symbol_alpha_soft | Billboard | Alpha | yingyang_3 / Gradient_maker_simple_round_radial / Tx_Noise_20 | soft ramp dist 0.01 | 4..3 | 5 | 1.5 | 率 15 爆发[0s x25] | 2x2 | x 0 · y x2 [0.164:0.43 1:1] | #000000DD | 5 |
| Burst/bamboo_leaf_alpha_soft | Billboard | Alpha | bamboo_leaf_1 / – / – |  | 2.5 | 5 | 0.15..0.12 | 率 35 爆发[0s x75] | 2x2 | x x2 [0:1 1:0] · y x2 [0:0.5 1:1] | #D7FF61FF..#10590CFF | 0 |
| Burst/FuzzAlpha | Billboard | Alpha | Default-Particle / – / Default-Particle |  | 3 | 5 | 26.57 | 率 0 爆发[0s x1] |  | x 1 · y 0 | #939393FF | -200 |
| Burst/Simple_Liquied_fb_1_alpha_soft | Billboard | Alpha | Tx_Noise_20 / Water_Splash_9 / Tx_Noise_20 | soft ramp | 1..2 | 5 | 1.5..1 | 率 0 爆发[0s x25] | 7x7 | x 0.96 · y 0 | #FFFFFFFF | 1 |
| Burst/yy_Shockwave_3_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Tx_Mask_01_Clamped / Tx_Noise_09 | ramp dist 0.3 | 5 | 5.1 | 3.32 | 率 1 爆发[0s x2] |  | x x10 [0.023:1 1:0.284] · y x2 [0:1 0.064:0.53 1:1] | #000000A8 | -254 |
| Burst/yy_Shockwave_3_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Tx_Mask_01_Clamped / Tx_Noise_09 | ramp dist 0.3 | 5 | 5.1 | 3.32 | 率 1 爆发[0s x1] |  | x x10 [0.023:1 1:0.284] · y x2 [0:1 0.064:0.53 1:1] | #000000A8 | -254 |

## Ying Yang Buff up（31 个系统，启用 26）

| 系统 | 渲染 | 混合 | 贴图 | 标志 | 寿命 | 延迟 | 尺寸 | 发射 | 序列帧 | C1 | 起色 | 排序 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| BuildUp_Quick/Flare_Ultrawide_Intense | Billboard | Add | Flare_Ultrawide / – / – |  | 0.25 | 0 | 3D x6 y0.4 z1 | 率 0 爆发[0s x1] |  | x x60 [0:1 1:0] · y 1 | #FFFFFFFF | 1 |
| BuildUp_Quick/Flare | Billboard | Add | flare1_flare2 / – / – | soft | 0.35 | 0 | 5 | 率 0 爆发[0s x1] |  | x x15 [0:1 1:0] · y 0 | #FFFFFFFF | 0 |
| BuildUp_Quick/Flare_Ultrawide_Intense | Billboard | Add | Flare_Ultrawide / – / – |  | 0.3 | 0 | 3D x1 y5 z1 | 率 0 爆发[0s x1] |  | x 0..2 · y 0 | #FFFFFFFF | 0 |
| BuildUp_Quick/Flare_Ultrawide_Intense/Flare_Ultrawide_Intense_1 | Billboard | Add | Flare_Ultrawide / – / – |  | 0.3 | 0 | 3D x1 y5 z1 | 率 0 爆发[0s x1] |  | x 0..2 · y 0 | #FFFFFFFF | 0 |
| BuildUp_Quick/Flare_AnamorphcBlue | Billboard | Add | AnamorphicFlare_Blue / – / – |  | 0.3 | 0 | 3D x3 y1 z10 | 率 0 爆发[0s x1] |  | x 0..2 · y 0 | #FFFFFFFF | 0 |
| Ground/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 1.5 | 0.2 | 7.06 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 0 | #FFFFFFFF | -2 |
| Ground/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 2.5..1.5 | 0.2 | 0.05..0.01 | 率 1 爆发[0s x35] | 2x2 | x x167.767 [0:1 1:0] · y x2 [0:0.5 0.106:1] | #FFFFFFFF | 0 |
| Ground/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 0.2 | 0.2 | 8 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 1 | #FFFFFFFF | 1 |
| Ground/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 1 | 0.2 | 8 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 1 | #FFFFFFFF | 1 |
| Ground/Ink_Shockwave_1_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02 / FX_TX_HorizontalGradient_04 / TX_Noise_01 | dist 0.35 | 2 | 0.1 | 5..4.5 | 率 0 爆发[0s x2] |  | x 2 · y x2 [0:0.432 1:1] | #000000B8 | 0 |
| Ground/Blood_Dark_Shockwave_05_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | gradient_002_Clamped / Tx_Liquid_01_2 / Noise_TilingSoftWebbing | dist 0.3 | 1 | 0.1 | 3.8 | 率 0 爆发[0s x1] |  | x x40 [0:1 1:0.004] · y x2 [0.537:0.398 1:1] | #000000FF | 0 |
| Ground/Ink_Splash_Radial_Wide_1_Alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Mask_02 / Gradient_Quad03 / Tx_Noise_07 | soft dist 0.25 | 1 | 0.1 | 4 | 率 0 爆发[0s x1] |  | x x10 [0:1 1:0.004] · y x2 [0.551:0.568 1:1] | #FFFFFFFF | 0 |
| Ground/Ring_Smudge_1_warp | Billboard | ? | – / – / – |  | 0.7 | 0.2 | 15 | 率 0 爆发[0s x4] |  | x 0 · y 0 | #FFFFFFFF | -1 |
| Center/Ink_Beam_Rotated_1_alpha_soft | Stretch | Alpha | FX_Flare_Ultrawide_Rotated_T / – / sd_noise_202 | dist 0.22 | 0.75 | 0.25 | 3D x1.5 y4 z1 | 率 0 爆发[0s x1] |  | x x45 [0:0.06 0.109:1 1:0] · y x2 [0.631:0.764 1:1] | #FFFFFFFF | 20 |
| Center/Trail_WispyGeneric_Alpha | Stretch | Alpha | Trail_WispyGeneric / gradient_004 / PackedNoises_Generic | soft | 0.7 | 0.25 | 3D x1.2 y4 z1 | 率 1 爆发[0s x1] |  | x x80 [0:1 1:0] · y 1.2 | #4F4F4FFF | 20 |
| Center/Ink_Vertical_trail_alpha_soft | Stretch | Alpha | Trail_WispyGeneric / gradient_004 / Tx_Noise_04 | soft dist 0.2 | 1.25 | 0.25 | 3D x3.5 y6 z1 | 率 1 爆发[0s x1] |  | x x10 [0:1 1:0] · y 1.15 | #000000FF | 0 |
| Center/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 0.75 | 0.25 | 3D x3.5 y8 z1 | 率 0 爆发[0s x1] |  | x 1 · y 1 | #000000F2 | 0 |
| Center/Ink_Vertical_trail_alpha_soft | Stretch | Alpha | Trail_WispyGeneric / gradient_004 / Tx_Noise_04 | soft dist 0.2 | 0.75 | 0.25 | 3D x2 y5 z1 | 率 1 爆发[0s x1] |  | x x25 [0:1 1:0] · y 1 | #000000FF | 20 |
| Center/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 1..2 | 0.25 | 3D x0.01 y0.1 z1 | 率 0 爆发[0s x35] | 2x2 | x x25 [0:1 1:0] · y x2 [0:0.5 0.157:1] | #FFFFFFFF | 3 |
| Center/bamboo_leaf_alpha_soft | Billboard | Alpha | bamboo_leaf_1 / – / – |  | 1.5..2 | 0.25 | 0.09..0.2 | 率 0 爆发[0s x55] | 2x2 | x x2 [0:1 1:0] · y x2 [0:0.5 1:1] | #D7FF61FF..#10590CFF | 0 |
| Center/yy_2_symbol_alpha_soft | Billboard | Alpha | yingyang_2 / Gradient_maker_simple_round_radial / Tx_Noise_12 | soft ramp dist 0.02 | 1.5 | 0.2 | 9.65 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.056:0.474 1:1] | #FFFFFF9F | 0 |
| Parent/Ink_Shockwave_2_Distorted_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | gradient_horizontal_Clamped / Tx_Mask_01_Clamped / FX_Noise_04_T | soft ramp dist 0.5 | 1.5 | 0.25 | 1.2 | 率 1 爆发[0s x1] |  | x x10 [0.023:1 1:0.284] · y x2 [0.002:0.682 1:1] | #FFFFFFFF | 5 |
| Parent/Explosion_Shockwave_04_Distorted_M | Mesh UV_Distorted_PolarCords | Alpha | gradient_002_Clamped / Tx_Liquid_01_2 / Noise_TilingSoftWebbing | dist 0.3 | 1.5 | 0.25 | 1.2 | 率 0 爆发[0s x1] |  | x x20 [0:1 1:0.004] · y x2 [0.537:0.398 1:1] | #000000FF | 3 |
| Parent/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 0.5 | 0.25 | 4 | 率 0 爆发[0s x1] |  | x x4.854 [0:1 1:0] · y 0 | #000000FF | -2 |
| Parent/Flare_GlowdotPickbook_Add | Stretch | Add | Glowdots_Speckles / – / – | soft | 2.5..1.5 | 0.25 | 0.02..0.03 | 率 1 爆发[0s x30] | 3x3 | x x132.162 [0:0.076 0.169:1 0.29:0.046 0.405:1 0.587:0.017 0.768:1 1:0] · y x2 [0:0.5 1:1] | #FFFFFFFF | 0 |
| Parent/Ink_flakes_1_alpha_soft | Billboard | Alpha | Liquid_smudge_pb_1 / Fuzzball_Clamped / Liquid_smudge_pb_1 | soft ramp dist 0.001 | 1.5..1 | 0.25 | 1..1.5 | 率 0 爆发[0s x5] | 2x2 | x 5 · y x2 [0:0.508 1:1] | #FFFFFFFF | 1 |

## Ying Yang Buff（17 个系统，启用 14）

| 系统 | 渲染 | 混合 | 贴图 | 标志 | 寿命 | 延迟 | 尺寸 | 发射 | 序列帧 | C1 | 起色 | 排序 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Loop/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 2 | 0 | 5 | 率 5 爆发[0s x0] | 8x8 | x x5 [0:1 1:0] · y x1.985 [0:0.667 1:1] | #0000005C | 10 |
| Loop/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 3 | 0 | 6 | 率 0 爆发[0s x1] |  | x x4.854 [0:1 1:0] · y 0 | #0000008D | -2 |
| Loop/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 2..1.5 | 1 | 0.02..0.01 | 率 25 爆发[0s x0] | 2x2 | x x25 [0:1 1:0.504] · y x2 [0:0.5 1:1] | #FFFFFFF1 | 0 |
| Loop/Ink_Shockwave_1_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02 / FX_TX_HorizontalGradient_04 / TX_Noise_01 | dist 0.35 | 5 | 0 | 1 | 率 1 爆发[0s x0] |  | x x20 [0:1 1:0.004] · y x2 [0.524:0.589 1:1] | #000000E4 | 3 |
| Loop/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 2 | 0 | 2.5 | 率 5 爆发[0s x0] | 8x8 | x x5 [0:1 1:0] · y x1.985 [0:0.667 1:1] | #000000FF | 10 |
| Loop/Nothing_alphaclip | Billboard | Alpha | – / – / – | fresnel dist 1 | 2..2 | 0 | 0.05 | 率 4 爆发[0s x0] | 7x7 | x 10 · y 0 | #FFFFFFFF | 1 |
| Loop/yy_1_symbol_alpha_soft | Billboard | Alpha | yingyang_1 / Gradient_maker_simple_round_radial / Tx_Noise_20 | soft ramp dist 0.05 | 12 | 0 | 1.2 | 率 0 爆发[0s x1] |  | x x5 [0:0.13 1:0] · y x2 [0:1 0.496:0.496] | #FFFFFF86 | 10 |
| Loop/yy_Shockwave_1_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | soft dist 2 | 5 | 0.25 | 0.8 | 率 1 |  | x x10 [0.023:1 1:0.284] · y x2 [0:0.589 1:0.891] | #3C3C3CFF | 5 |
| Burst/Ink_Vertical_trail_alpha_soft | Stretch | Alpha | Trail_WispyGeneric / gradient_004 / Tx_Noise_04 | soft dist 0.2 | 2 | 0.25 | 3D x4 y6 z1 | 率 1 爆发[0s x1] |  | x x10 [0:1 1:0] · y 1.15 | #000000FF | 0 |
| Burst/Flare_GlowdotPickbook_Add | Stretch | Add | Glowdots_Speckles / – / – | soft | 2.5..3.5 | 0.25 | 0.05..0.03 | 率 1 爆发[0s x30] | 3x3 | x x132.162 [0:0.076 0.169:1 0.29:0.046 0.405:1 0.587:0.017 0.768:1 1:0] · y x2 [0:0.5 1:1] | #FFFFFFFF | 0 |
| Burst/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 1.1 | 0 | 6 | 率 0 爆发[0s x1] |  | x 5 · y 1 | #000000FF | 5 |
| Burst/Spark2_Alpha | Stretch | Alpha | trace_07_rotated / – / – |  | 0.65..0.5 | 0 | 0.3..0.15 | 率 0 爆发[0s x25] |  | x 0 · y 0 | #080416FF | 0 |
| Burst/Ink_Shockwave_1_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02 / FX_TX_HorizontalGradient_04 / TX_Noise_01 | dist 0.35 | 1.5 | 0 | 2.5 | 率 0 爆发[0s x2] |  | x x20 [0:1 1:0.004] · y x2 [0:0.48 1:1] | #00000055 | 3 |
| Burst/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 0.9 | 0 | 6 | 率 0 爆发[0s x1] |  | x x4.854 [0:1 1:0] · y 0 | #000000FF | -2 |

## Ying Yang Burst Shockwave（14 个系统，启用 12）

| 系统 | 渲染 | 混合 | 贴图 | 标志 | 寿命 | 延迟 | 尺寸 | 发射 | 序列帧 | C1 | 起色 | 排序 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Burst/DS_Aoe_Crystal_Warp_01 | Billboard | ? | – / – / – |  | 0.2 | 0 | 2 | 率 0 爆发[0s x4] |  | x 0 · y 0 | #FFFFFF0D | 2 |
| Burst/Smoke_loop_flipbook_thingy_Clean_alpha_soft | Stretch | Alpha | Tx_Noise_18 / Smoke_loop_thingy_alpha2025-05-30 / Tx_Noise_12 | soft dist 0.03 | 1..0.9 | 0 | 3..2 | 率 0 爆发[0s x10] | 5x5 | x 8.03 · y x2 [0:1 0.064:0.608 1:1] | #FFFFFF2F | -24 |
| Burst/Smoke_Explotion_Mom_sat_add_Soft | Billboard | Add | aerial_explosion_3_render_allDemon_explotionv2 / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_18 | soft dist 0.005 | 1..0.7 | 0 | 3 | 率 0 爆发[0s x2] | 6x6 | x x10 [0:1 0.25:0] · y x2 [0.5:0.5 1:0.614] | #FFFFFFEF | 0 |
| Burst/LightFlash_Soft | Billboard | Add | LightFlash / – / – | soft | 0.17 | 0 | 3 | 率 0 爆发[0s x1] |  | x x5 [0:1 0.099:0.18 1:0] · y 1.2 | #FFFFFFFF | 30 |
| Burst/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 0.25 | 0 | 2 | 率 0 爆发[0s x1] |  | x x1 [0:1 0.099:0.18 1:0] · y 0 | #FFFFFFFF | 15 |
| Burst/yy_Shockwave_1_alpha_soft | Mesh ConeTapered_Wide | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | soft dist 2 | 1 | 0 | 3D x0.45 y2 z0.45 | 率 0 爆发[0s x3] |  | x x5 [0:1 1:0] · y 1.15 | #FFFFFFFF | 0 |
| Burst/yy_Shockwave_1_alpha_soft | Mesh ConeTapered_Wide | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | soft dist 2 | 1 | 0 | 3D x0.2 y0.5 z0.2 | 率 0 爆发[0s x1] |  | x x5 [0:1 1:0] · y 1.15 | #FFFFFFFF | 0 |
| Burst/Explosion_Shockwave_06_Distorted_necro_add_soft | Mesh UV_Distorted_PolarCords | Alpha | OffsetLine_Warped2 / Tx_Liquid_01_2 / PearlyRainbowNoise | soft dist 0.1 | 1 | 0 | 3 | 率 0 爆发[0s x1] |  | x x100 [0:1 1:0.004] · y x2 [0.493:0.375 1:1] | #000000FF | 0 |
| Burst/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 0.7..1.5 | 0 | 2..2.5 | 率 0 爆发[0s x8] | 8x8 | x 3 · y x2 [0:0.737 0.189:0.491 0.895:0.548 1:1] | #000000AC | -24 |
| Burst/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 0.25 | 0 | 8 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 1 | #FFFFFF4A | 10 |
| Burst/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 0.5..1 | 0.1 | 0.02..0.03 | 率 1 爆发[0s x15] | 2x2 | x x150 [0:1 1:0] · y x2 [0:0.5 0.174:1] | #FFC6FDFF | 0 |
| Burst/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 0.5..1 | 0.1 | 0.02..0.03 | 率 1 爆发[0s x50] | 2x2 | x x150 [0:1 1:0] · y x2 [0:0.5 0.174:1] | #FFC6FDFF | 0 |

## Ying Yang Flash AoE（28 个系统，启用 24）

| 系统 | 渲染 | 混合 | 贴图 | 标志 | 寿命 | 延迟 | 尺寸 | 发射 | 序列帧 | C1 | 起色 | 排序 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Center/LightFlash | Billboard | Alpha | LightFlash / – / – | soft | 0.4 | 0 | 4 | 率 0 爆发[0s x3] |  | x x15.363 [0:1 1:0] · y 1 | #FFFFFFFF | 10 |
| Center/DiagonalFlare_ConalSpliff | Billboard | Add | Diagonal_RedFlare / – / – | soft dist 2 | 0.1 | 0 | 7 | 率 0 爆发[0s x2] |  | x x5 [0:1 1:0] · y 1 | #FFFFFFFF | 12 |
| Spawn/yy_Shockwave_1_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | soft dist 2 | 1.5 | 0.1 | 3 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.013:0.459 1:1] | #FFFFFFFF | -5 |
| Spawn/yy_Shockwave_1_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | soft dist 2 | 1.5 | 0.1 | 3 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.013:0.459 1:1] | #FFFFFFFF | -5 |
| Spawn/yy_Shockwave_3_Distorted_still_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Gradient_Quad03 / Tx_Noise_09 | soft ramp dist 0.3 | 1.5 | 0.1 | 2.5 | 率 1 爆发[0s x1] |  | x x10 [0.023:1 1:0.284] · y 1.29 | #000000FF | -254 |
| Spawn/Explosion_Shockwave_04_Distorted_M | Mesh UV_Distorted_PolarCords | Alpha | gradient_002_Clamped / Tx_Liquid_01_2 / Noise_TilingSoftWebbing | dist 0.3 | 1.5 | 0.1 | 2.5 | 率 0 爆发[0s x1] |  | x x20 [0:1 1:0.004] · y x2 [0.537:0.398 1:1] | #000000FF | 3 |
| Spawn/Blood_Dark_Shockwave_05_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | gradient_002_Clamped / Tx_Liquid_01_2 / Noise_TilingSoftWebbing | dist 0.3 | 1 | 0.1 | 8 | 率 0 爆发[0s x1] |  | x x40 [0:1 1:0.004] · y x2 [0.537:0.398 1:1] | #000000FF | 0 |
| Spawn/Ink_Shockwave_1_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02 / FX_TX_HorizontalGradient_04 / TX_Noise_01 | dist 0.35 | 3 | 0.1 | 7..8 | 率 0 爆发[0s x2] |  | x 2 · y x2 [0:0.432 1:1] | #000000B7 | 0 |
| Spawn/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 2 | 0.1 | 13 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 1 | #FFFFFFFF | 1 |
| Spawn/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 0.4 | 0.1 | 12 | 率 0 爆发[0s x1] |  | x x15.363 [0:0 0.045:0.273 0.275:0.274 0.283:0.603 0.528:0.6 0.536:0.727 0.684:0.73 0.715:0.834 1:1] · y 1 | #FFFFFF19 | 1 |
| Spawn/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 2.5..1.5 | 0.1 | 0.05..0.01 | 率 1 爆发[0s x55] | 2x2 | x x167.767 [0:1 1:0] · y x2 [0:0.5 0.089:1] | #FFFFFFFF | 0 |
| Spawn/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 2 | 0.1 | 12 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 0 | #FFFFFFFF | -2 |
| Spawn/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 1..3 | 0.1 | 5 | 率 1 爆发[0s x50] | 8x8 | x x1 [0:1 1:0] · y x2 [0:1 0.049:0.477 0.749:0.605 1:1] | #FFFFFF3A | -5 |
| Spawn/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 0.5 | 0.1 | 4 | 率 0 爆发[0s x1] |  | x x4.854 [0:1 1:0] · y 0 | #000000FF | -2 |
| Spawn/Ink_flakes_1_alpha_soft | Billboard | Alpha | Liquid_smudge_pb_1 / Fuzzball_Clamped / Liquid_smudge_pb_1 | soft ramp dist 0.001 | 1.5..2 | 0.1 | 0.5..0.7 | 率 0 爆发[0s x35] | 2x2 | x 5 · y x2 [0:0.508 1:1] | #FFFFFFFF | 1 |
| Spawn/Explosion_Shockwave_03_Distorted_M | Mesh UV_Distorted_PolarCords | Alpha | Tx_Noise_29 / T_Gradient_Line_V20 / Noise_TilingSoftWebbing | dist 0.2 | 1 | 0.1 | 15 | 率 0 爆发[0s x1] |  | x x5 [0:0.75 0.023:1 1:0] · y 0 | #000000B2 | 0 |
| Spawn/yy_2_symbol_alpha_soft | Billboard | Alpha | yingyang_2 / Gradient_maker_simple_round_radial / Tx_Noise_12 | soft ramp dist 0.02 | 1.8 | 0.2 | 6 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.048:0.682 1:1] | #FFFFFF9F | 0 |
| Ground/FuzzAdd | Billboard | Add | Default-Particle / – / Default-Particle |  | 1 | 0.2 | 15 | 率 0 爆发[0s x3] |  | x x1 [0:1 1:0] · y 1.39 | #FFFFFF3C | 5 |
| Ground/Flare | Billboard | Add | flare1_flare2 / – / – | soft | 0.3 | 0.2 | 18 | 率 0 爆发[0s x1] |  | x x15 [0:1 1:0] · y 1 | #FFFFFFFF | 50 |
| Ground/Flare | Billboard | Add | flare1_flare2 / – / – | soft | 0.3 | 0.2 | 18 | 率 0 爆发[0s x1] |  | x x15 [0:1 1:0] · y 1 | #FFFFFFFF | 50 |
| Ground/Ring_Halo_faded_add_soft | Billboard | Add | Halo_Faded / – / – | soft dist 2 | 0.4 | 0.2 | 12 | 率 0 爆发[0s x1] |  | x x2 [0:1 1:0] · y 0 | #FFFFFF0E | 2 |
| Ground/yy_Shockwave_3_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Tx_Mask_01_Clamped / Tx_Noise_09 | ramp dist 0.3 | 5 | 0.21 | 3 | 率 1 爆发[0s x2] |  | x x10 [0.023:1 1:0.284] · y x2 [0:1 0.064:0.53 1:1] | #000000A8 | -254 |
| Ground/yy_Shockwave_3_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Tx_Mask_01_Clamped / Tx_Noise_09 | ramp dist 0.3 | 5 | 0.21 | 3 | 率 1 爆发[0s x1] |  | x x10 [0.023:1 1:0.284] · y x2 [0:1 0.064:0.53 1:1] | #000000A8 | -254 |
| Ground/bamboo_leaf_alpha_soft | Billboard | Alpha | bamboo_leaf_1 / – / – |  | 2.5 | 0 | 0.1..0.2 | 率 0 爆发[0s x35] | 2x2 | x x2 [0:1 1:0] · y x2 [0:0.5 1:1] | #D7FF61FF..#10590CFF | 0 |

## Ying Yang Floor Shockwave（18 个系统，启用 16）

| 系统 | 渲染 | 混合 | 贴图 | 标志 | 寿命 | 延迟 | 尺寸 | 发射 | 序列帧 | C1 | 起色 | 排序 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Ground_2/Ink_Shockwave_1_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02 / FX_TX_HorizontalGradient_04 / TX_Noise_01 | dist 0.35 | 0.7 | 0 | 3.5 | 率 0 爆发[0s x1] |  | x x5 [0:0.75 0.023:1 1:0] · y x2 [0.197:0.467 1:1] | #000000AF | -5 |
| Ground_2/Liquid_smudge_pb_3_nac_alpha | Billboard | Alpha | Liquid_smudge_pb_3 / – / Tx_Noise_20 | dist 0.005 | 2 | 0.1 | 7 | 率 0 爆发[0s x2] | 2x2 | x x30 [0:1 1:0.04] · y x2 [0:0.977 0.128:0.553 1:1] | #00000080 | 2 |
| Ground_2/Liquid_smudge_pb_3_alpha | Billboard | Alpha | Liquid_smudge_pb_3 / – / Tx_Noise_20 | dist 0.005 | 2 | 0.1 | 1..0.5 | 率 0 爆发[0s x1 cyc15/0.02] | 2x2 | x x30 [0:1 1:0.04] · y x2 [0:0.977 0.128:0.553 1:1] | #00000080 | 2 |
| Ground_2/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 0.15 | 0 | 3 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 0 | #000000FF | -2 |
| Ground_2/Shockwave_blood_Distorted_MD_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Mask_02 / Tx_Noise_29 / Tx_Noise_17 | ramp dist 0.1 | 0.7 | 0 | 3 | 率 0 爆发[0s x1] |  | x x500 [0:1 0.038:0.27 1:0] · y x2 [0:0.589 0.493:0.635 1:1] | #000000AC | 0 |
| Ground_2/Explosion_Shockwave_06_Distorted_necro_add_soft | Mesh UV_Distorted_PolarCords | Alpha | OffsetLine_Warped2 / Tx_Liquid_01_2 / PearlyRainbowNoise | soft dist 0.1 | 1 | 0 | 5 | 率 0 爆发[0s x1] |  | x x100 [0:1 1:0.004] · y x2 [0.493:0.375 1:1] | #000000FF | 0 |
| Ground_2/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 1..1.5 | 0 | 2..1.5 | 率 0 爆发[0s x8] | 8x8 | x 3 · y x2 [0:0.737 0.189:0.491 0.895:0.548 1:1] | #100C16AC | -24 |
| Ground_2/yy_Shockwave_3_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Tx_Mask_01_Clamped / Tx_Noise_09 | ramp dist 0.3 | 1 | 0 | 3.5 | 率 1 爆发[0s x1] |  | x x10 [0.023:1 1:0.284] · y 1.29 | #000000D0 | -254 |
| Ground_2/yy_Shockwave_1_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | dist 2 | 0.4 | 0.1 | 4 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.013:0.459 1:0.638] | #FFFFFFFF | -5 |
| Ground_2/Explosion_Shockwave_04_Distorted_M | Mesh UV_Distorted_PolarCords | Alpha | gradient_002_Clamped / Tx_Liquid_01_2 / Noise_TilingSoftWebbing | dist 0.3 | 0.7 | 0 | 3 | 率 0 爆发[0s x1] |  | x x40 [0:1 1:0.004] · y x2 [0.537:0.398 1:0.739] | #FFFFFF09 | 0 |
| Ground_2/FuzzAdd | Billboard | Add | Default-Particle / – / Default-Particle |  | 1 | 0 | 6 | 率 0 爆发[0s x1] |  | x x5 [0:1 1:0] · y 1 | #FFFFFF48 | 1 |
| Ground_2/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 0.25 | 0 | 8 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 1 | #FFFFFF4A | 10 |
| Ground_2/Flare soft | Billboard | Add | flare1_flare2 / – / – | soft | 0.3 | 0 | 10 | 率 0 爆发[0s x1] |  | x x5 [0:1 1:0] · y 1 | #FFFFFF48 | 50 |
| Ground_2/Flare | Billboard | Add | flare1_flare2 / – / – | soft | 0.3 | 0 | 8 | 率 0 爆发[0s x1] |  | x x15 [0:1 1:0] · y 1 | #FFFFFF21 | 50 |
| Ground_2/Ring_Halo_faded_add_soft | Billboard | Add | Halo_Faded / – / – | soft dist 2 | 0.4 | 0 | 5 | 率 0 爆发[0s x1] |  | x 0 · y 0 | #FFFFFF2B | 2 |
| Ground_2/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 2..1 | 0 | 0.02..0.05 | 率 1 爆发[0s x50] | 2x2 | x x150 [0:1 1:0] · y x2 [0:0.5 1:1] | #FFC6FDFF | 0 |

## Ying Yang Frontal Pulses（20 个系统，启用 17）

| 系统 | 渲染 | 混合 | 贴图 | 标志 | 寿命 | 延迟 | 尺寸 | 发射 | 序列帧 | C1 | 起色 | 排序 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Pulses/yy_Liquid_Splat_Frontal_3_alpha | Billboard | Alpha | – / – / Tx_Noise_20 | dist 0.005 | 1.5 | 0 | 3D x0 y0 z0 | 率 1 爆发[0s x1] |  | x x5 [0:1 1:0] · y x2 [0:1 0.086:0.791 0.852:0.824 1:1] | #FFFFFFFF | -2 |
| Pulses/FuzzAlpha | Billboard | Alpha | Default-Particle / – / Default-Particle |  | 0.5 | 0 | 2 | 率 1 爆发[0s x1] |  | x x4.854 [0:1 1:0] · y 0 | #000000FF | -2 |
| Pulses/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 2.5..1.5 | 0 | 0.05..0.074 | 率 0 爆发[0s x50 cyc6/1] | 2x2 | x x167.767 [0:1 1:0] · y x2 [0:0.5 1:1] | #FFFFFFFF | 0 |
| Pulses/FuzzAdd | Billboard | Add | Default-Particle / – / Default-Particle |  | 2 | 0 | 13 | 率 1 爆发[0s x1] |  | x x1 [0:1 1:0] · y 1 | #FFFFFF31 | -500 |
| Pulses/FuzzAdd | Billboard | Add | Default-Particle / – / Default-Particle |  | 2 | 0 | 13 | 率 1 爆发[0s x1] |  | x x1 [0:1 1:0] · y 1 | #FFFFFF31 | -500 |
| Pulses/yy_Shockwave_1_Distorted_short_alpha | Billboard | Alpha | Tx_Line_08_Up / Gradient_Quad02_nomipmap / TX_Noise_01 | dist 0.31 | 1 | 0 | 3D x7.39 y14.02 z3.5 | 率 1 爆发[0s x1] |  | x x80.5 [0:1 1:0.004] · y x2 [0:0.432 1:1] | #FFFFFFFF | 0 |
| Pulses/ink_short_Dark_Shockwave_05_Distorted_alpha | Billboard | Alpha | Tx_Mask_02_Flipped / Gradient_Quad02 / Noise_TilingSoftWebbing | ramp dist 0.1 | 0.5 | 0 | 3D x7.5 y23.23 z0 | 率 1 爆发[0s x1] |  | x x5 [0:1 1:0.004] · y x2 [0.419:0.624 1:1] | #FFFFFFFF | 0 |
| Pulses/Explosion_Shockwave_04_Distorted_M | Billboard | Alpha | gradient_002_Clamped / Tx_Liquid_01_2 / Noise_TilingSoftWebbing | dist 0.3 | 1.5 | 0 | 3D x2.1 y5 z0 | 率 1 爆发[0s x2] |  | x x20 [0:1 1:0.004] · y x2 [0.537:0.398 1:1] | #000000FF | 3 |
| Pulses/Simple_Liquied_fb_1_alpha_soft | Billboard | Alpha | Tx_Noise_20 / Water_Splash_9 / Tx_Noise_20 | soft ramp | 1.5..0.7 | 0 | 2..2 | 率 0 爆发[0s x30 cyc6/1] | 7x7 | x x1 [0:1 1:0] · y x2 [0.304:0.733 1:1] | #FFFFFFFF | 0 |
| Pulses/bamboo_leaf_alpha_soft | Billboard | Alpha | bamboo_leaf_1 / – / – |  | 1.5..0.7 | 0 | 0.25..0.5 | 率 0 爆发[0s x30 cyc6/1] | 2x2 | x x25 [0:1 0.159:0.148 1:0] · y x2 [0.304:0.733 1:1] | #14590CFF | 0 |
| Loop/bamboo_pulse_ink_1_alpha | Billboard | Alpha | bamboo_tree_1 / gradient_004 / tree_pulse_1_mask |  | 6 | 0 | 9 | 率 0 爆发[0s x1] |  | x 0 · y x2 [0:1 0.081:0.5 0.903:0.5 1:1] | #000000FF | -2 |
| Loop/Tree_pulse_floor_ink_1_alpha | Billboard | Alpha | Pulse_floor_1_color / Pulse_floor_1_color / Tx_Noise_17 | ramp dist 0.02 | 6 | 0 | 10.19 | 率 0 爆发[0s x1] |  | x 26.88 · y x2 [0:1 0.11:0.5 0.903:0.5 1:1] | #FFFFFFFF | -2 |
| Loop/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 3 | 0 | 4 | 率 15 | 8x8 | x x0.976 [0:1 0.079:1 1:1] · y x2 [0:0.977 0.128:0.624 0.797:0.753 1:1] | #0000004A | 0 |
| Loop/Ink_Shockwave_1_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02 / FX_TX_HorizontalGradient_04 / TX_Noise_01 | dist 0.35 | 3 | 0.1 | 0.2..0.3 | 率 15 爆发[0s x2] |  | x 2 · y x2 [0:0.432 1:1] | #FFFFFFFF | 0 |
| Loop/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 2..1.5 | 0.2 | 0.05..0.01 | 率 25 爆发[0s x5] | 2x2 | x x167.767 [0:1 1:0] · y x2 [0:0.5 1:1] | #FFFFFFFF | 0 |
| Loop/bamboo_leaf_alpha_soft | Billboard | Alpha | bamboo_leaf_1 / – / – |  | 1.25..1.75 | 0 | 0.2..0.3 | 率 0 爆发[0s x35] | 2x2 | x x36 [0:1 1:0] · y x2 [0:0.5 1:1] | #14590CFF | 0 |
| Loop/yy_3_symbol_alpha_soft | Billboard | Alpha | yingyang_3 / Gradient_maker_simple_round_radial / Tx_Noise_20 | soft ramp dist 0.01 | 4..3 | 0 | 1.8 | 率 5 爆发[0s x0] | 2x2 | x 0 · y x2 [0.164:0.43 1:1] | #000000FF | 5 |

## Ying Yang Frontal（28 个系统，启用 25）

| 系统 | 渲染 | 混合 | 贴图 | 标志 | 寿命 | 延迟 | 尺寸 | 发射 | 序列帧 | C1 | 起色 | 排序 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| holder_1/Explosion_Shockwave_04_Distorted_M | Mesh Cone_LongAOE | Alpha | gradient_002_Clamped / Tx_Liquid_01_2 / Noise_TilingSoftWebbing | dist 0.3 | 1.5 | 0.25 | 3D x12 y5 z3.52 | 率 0 爆发[0s x1] |  | x x20 [0:1 1:0.004] · y x2 [0.537:0.398 1:1] | #000000FF | 3 |
| holder_1/Blood_Dark_Shockwave_05_Distorted_alpha | Mesh Cone_LongAOE | Alpha | gradient_002_Clamped / Tx_Liquid_01_2 / Noise_TilingSoftWebbing | dist 0.3 | 1 | 0.1 | 3D x25 y9 z0 | 率 0 爆发[0s x1] |  | x x25 [0:1 1:0.004] · y x2 [0.537:0.398 1:1] | #FFFFFFFF | 0 |
| holder_1/Ink_Shockwave_1_Distorted_alpha | Mesh Cone_LongAOE | Alpha | Tx_Line_02 / FX_TX_HorizontalGradient_04 / TX_Noise_01 | dist 0.35 | 3 | 0.1 | 3D x25 y15 z3.5 | 率 0 爆发[0s x2] |  | x 2 · y x2 [0:0.432 1:1] | #000000B8 | 0 |
| holder_1/Ink_Shockwave_1_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02 / FX_TX_HorizontalGradient_04 / TX_Noise_01 | dist 0.35 | 3 | 0.1 | 2..2.5 | 率 0 爆发[0s x2] |  | x 2 · y x2 [0:0.432 1:1] | #0000007F | 0 |
| holder_1/Flecks_Clipped_Alpha 1 | Stretch | Alpha | Fleks_AlphaClipped / – / FireNoise | dist 0.07 | 2..1 | 0.25 | 0.02..0.03 | 率 500 | 3x3 | x x50 [0:1 1:0] · y x2 [0:0.574 0.057:1] | #FFFFFFFF | 0 |
| holder_1/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 2 | 0 | 13 | 率 0 爆发[0s x1] |  | x x5 [0:1 1:0] · y 1 | #9C9C9C21 | 1 |
| holder_1/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 0.4 | 0 | 12 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 1 | #7D7D7DFF | 1 |
| holder_1/yy_Shockwave_1_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | dist 2 | 1.5 | 0 | 2..3 | 率 0 爆发[0s x1] |  | x x10 [0:0.75 0.023:1 1:0] · y x2 [0.206:0.485 1:1] | #898989FF | -5 |
| holder_1/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 2 | 0 | 5 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 0 | #FFFFFFFF | -2 |
| holder_1/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 0.5 | 0 | 3 | 率 0 爆发[0s x1] |  | x x4.854 [0:1 1:0] · y 0 | #000000FF | -2 |
| holder_1/FuzzAlpha | Billboard | Alpha | Default-Particle / – / Default-Particle |  | 0.5 | 0 | 2 | 率 0 爆发[0s x1] |  | x x4.854 [0:1 1:0] · y 0 | #000000FF | -2 |
| holder_1/yy_Liquid_Splat_Frontal_3_alpha | Billboard | Alpha | – / – / Tx_Noise_20 | dist 0.005 | 5 | 0 | 3D x3 y4 z1 | 率 0 爆发[0s x1] |  | x x0 [0:1 1:0] · y x2 [0:1 0.108:0.478 0.868:0.442 1:1] | #000000FF | -2 |
| holder_1/Ink_flakes_1_alpha_soft | Billboard | Alpha | Liquid_smudge_pb_1 / Fuzzball_Clamped / Liquid_smudge_pb_1 | soft ramp dist 0.001 | 1.5..3.5 | 0 | 0.5..0.7 | 率 0 爆发[0s x35] | 2x2 | x 5 · y x2 [0:0.508 1:1] | #FFFFFFFF | 1 |
| holder_1/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 2..1 | 0 | 4 | 率 15 爆发[0s x30] | 8x8 | x x0.976 [0:1 0.079:1 1:1] · y x2 [0:0.977 0.128:0.624 0.797:0.753 1:1] | #0D0A13FF | 0 |
| holder_1/Simple_Liquied_fb_1_alpha_soft | Billboard | Alpha | Tx_Noise_20 / Water_Splash_9 / Tx_Noise_20 | soft ramp | 1.1 | 0 | 3 | 率 50 | 7x7 | x x1 [0:1 1:0] · y x2 [0.304:0.733 1:1] | #FAFAFAFF | 0 |
| Center/Ink_Shockwave_1_Distorted_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02 / FX_TX_HorizontalGradient_04 / TX_Noise_01 | soft dist 0.35 | 0.7 | 0 | 2.5 | 率 0 爆发[0s x2] |  | x x20 [0:1 1:0.004] · y x2 [0.524:0.589 1:1] | #000000E4 | 3 |
| Center/Simple_Liquied_fb_1_alpha_soft | Billboard | Alpha | Tx_Noise_20 / Water_Splash_9 / Tx_Noise_20 | soft ramp | 1..0.75 | 0 | 1.5..2 | 率 0 爆发[0s x20] | 7x7 | x 10 · y 0 | #39354DFF | 1 |
| Center/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 0.75 | 0 | 15 | 率 0 爆发[0s x1] |  | x x5 [0:1 1:0] · y 1 | #FFFFFFFF | 10 |
| Center/Spark2_Alpha | Stretch | Alpha | trace_07_rotated / – / – |  | 0.65..0.5 | 0 | 0.3..0.7 | 率 0 爆发[0s x80] |  | x 0 · y 0 | #080416FF | 0 |
| Center/yy_Shockwave_3_Distorted_still_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Gradient_Quad03 / Tx_Noise_09 | soft ramp dist 0.3 | 1 | 0.1 | 0.845 | 率 1 爆发[0s x1] |  | x x10 [0.023:1 1:0.284] · y 1.29 | #000000FF | -254 |
| Center/Explosion_Shockwave_04_Distorted_M | Mesh UV_Distorted_PolarCords | Alpha | gradient_002_Clamped / Tx_Liquid_01_2 / Noise_TilingSoftWebbing | dist 0.3 | 1.5 | 0.1 | 1 | 率 0 爆发[0s x1] |  | x x20 [0:1 1:0.004] · y x2 [0.537:0.398 1:1] | #000000FF | 3 |
| Center/Blood_Dark_Shockwave_05_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | gradient_002_Clamped / Tx_Liquid_01_2 / Noise_TilingSoftWebbing | dist 0.3 | 1 | 0.1 | 2 | 率 0 爆发[0s x1] |  | x x40 [0:1 1:0.004] · y x2 [0.537:0.398 1:1] | #000000FF | 0 |
| Center/bamboo_leaf_alpha_soft | Billboard | Alpha | bamboo_leaf_1 / – / – |  | 1.25..1.75 | 0 | 0.1..0.15 | 率 0 爆发[0s x70] | 2x2 | x x36 [0:1 1:0] · y x2 [0:0.5 1:1] | #D7FF61FF..#10590CFF | 0 |
| Center/yy_3_symbol_alpha_soft | Billboard | Alpha | yingyang_3 / Gradient_maker_simple_round_radial / Tx_Noise_20 | soft ramp dist 0.01 | 4..3 | 0 | 1.5 | 率 0 爆发[0s x25] | 2x2 | x 0 · y x2 [0.164:0.43 1:1] | #000000DD | 5 |
| Center/yy_2_symbol_alpha_soft | Billboard | Alpha | yingyang_2 / Gradient_maker_simple_round_radial / Tx_Noise_12 | soft ramp dist 0.02 | 1 | 0 | 2.49 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.078:0.434 1:1] | #FFFFFF9D | 0 |

## Ying Yang Nova Shockwave（16 个系统，启用 14）

| 系统 | 渲染 | 混合 | 贴图 | 标志 | 寿命 | 延迟 | 尺寸 | 发射 | 序列帧 | C1 | 起色 | 排序 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Center/LightFlash | Billboard | Alpha | LightFlash / – / – | soft | 0.4 | 0 | 4 | 率 0 爆发[0s x3] |  | x x15.363 [0:1 1:0] · y 1 | #FFFFFFFF | 10 |
| Center/DiagonalFlare_ConalSpliff | Billboard | Add | Diagonal_RedFlare / – / – | soft dist 2 | 0.1 | 0 | 7 | 率 0 爆发[0s x2] |  | x x5 [0:1 1:0] · y 1 | #FFFFFFFF | 12 |
| Center/yy_Shockwave_1_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | soft dist 2 | 1.5 | 0.2 | 4 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.013:0.459 1:1] | #FFFFFFFF | -5 |
| Center/yy_Shockwave_1_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | soft dist 2 | 1.5 | 0.2 | 2 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.013:0.459 1:1] | #FFFFFFFF | -5 |
| Center/Ink_Shockwave_1_Distorted_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02 / FX_TX_HorizontalGradient_04 / TX_Noise_01 | soft dist 0.35 | 1.5 | 0.1 | 3.5 | 率 0 爆发[0s x1] |  | x x5 [0:0.75 0.023:1 1:0] · y x2 [0.197:0.467 1:1] | #000000AF | -5 |
| Center/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 0.15 | 0.1 | 3 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 0 | #000000FF | -2 |
| Center/Shockwave_blood_Distorted_MD_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Mask_02 / Tx_Noise_29 / Tx_Noise_17 | soft ramp dist 0.1 | 1.2 | 0.1 | 3 | 率 0 爆发[0s x1] |  | x x500 [0:1 0.038:0.27 1:0] · y x2 [0:0.589 0.493:0.635 1:1] | #000000AC | 0 |
| Center/Explosion_Shockwave_06_Distorted_necro_add_soft | Mesh UV_Distorted_PolarCords | Alpha | OffsetLine_Warped2 / Tx_Liquid_01_2 / PearlyRainbowNoise | soft dist 0.1 | 1 | 0.1 | 5 | 率 0 爆发[0s x1] |  | x x100 [0:1 1:0.004] · y x2 [0.493:0.375 1:1] | #000000FF | 0 |
| Center/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 1..1.5 | 0.1 | 4..3.5 | 率 0 爆发[0s x8] | 8x8 | x 3 · y x2 [0:0.737 0.189:0.491 0.895:0.548 1:1] | #100C1662 | -24 |
| Center/yy_Shockwave_3_Distorted_still_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Gradient_Quad03 / Tx_Noise_09 | soft ramp dist 0.3 | 1.5 | 0.1 | 3.5 | 率 1 爆发[0s x1] |  | x x10 [0.023:1 1:0.284] · y 1.29 | #000000D0 | -254 |
| Center/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 0.35 | 0.1 | 8 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 1 | #FFFFFF80 | 10 |
| Center/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 1.5 | 0.1 | 8 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 1 | #FFFFFF80 | 10 |
| Center/Flare soft | Billboard | Add | flare1_flare2 / – / – | soft | 0.3 | 0.1 | 10 | 率 0 爆发[0s x1] |  | x x5 [0:1 1:0] · y 1 | #FFFFFF48 | 50 |
| Center/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 1.5..2.5 | 0.1 | 0.02..0.05 | 率 1 爆发[0s x50] | 2x2 | x x150 [0:1 1:0] · y x2 [0:0.5 0.174:1] | #FFC6FDFF | 0 |

## Ying Yang Orb（18 个系统，启用 14）

| 系统 | 渲染 | 混合 | 贴图 | 标志 | 寿命 | 延迟 | 尺寸 | 发射 | 序列帧 | C1 | 起色 | 排序 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Orb_1/Spawn/yy_Shockwave_1_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | soft dist 2 | 3 | 0.2 | 2 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.013:0.459 1:1] | #000000FF | -5 |
| Orb_1/Spawn/Mana_Gather_in_aura_quad_add_soft_desat | Mesh Sphere | Add | RainbowMirrorNoise / Gradient_maker_Y_Fade_tappered / Tx_Noise_36 | soft dist 0.2 | 1.5 | 0 | 1.25 | 率 0 爆发[0s x1] |  | x 2 · y 1 | #FFFFFF0B | 6 |
| Orb_1/Spawn/Ring_Halo_faded_alpha_soft | Billboard | Alpha | Halo_Faded / – / – | dist 2 | 1.5 | 0 | 3D x1.9 y1.95 z1 | 率 0 爆发[0s x1] |  | x 1 · y 1 | #FFFFFFFF | 35 |
| Orb_1/Spawn/Ring_StrongOutline_innerglow_strong_1_alpha_soft | Billboard | Alpha | Ring_StrongOutline_GlowInner / – / – | soft dist 2 | 1.5 | 0 | 1.625 | 率 0 爆发[0s x1] |  | x 0 · y 1.2 | #FFFFFFFF | 35 |
| Orb_1/Loop/yy_Shockwave_1_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | soft dist 2 | 3 | 0.2 | 2 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.013:0.459 1:1] | #000000FF | -5 |
| Orb_1/Loop/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 3 | 0 | 3 | 率 0 爆发[0s x1] |  | x 5 · y 1 | #000000FF | -50 |
| Orb_1/Loop/yy_2_symbol_alpha_soft | Billboard | Alpha | yingyang_2 / Gradient_maker_simple_round_radial / Tx_Noise_12 | soft ramp dist 0.02 | 9.5 | 0 | 1.65 | 率 0 爆发[0s x1] |  | x 5 · y x2 [0:1 0.118:0.556 0.879:0.509 1:1] | #FFFFFFFF | 0 |
| Orb_1/Loop/Mana_Gather_in_aura_quad_add_soft_desat | Mesh Sphere | Add | RainbowMirrorNoise / Gradient_maker_Y_Fade_tappered / Tx_Noise_36 | soft dist 0.2 | 1.51 | 1.49 | 1.25 | 率 0 爆发[0s x1] |  | x 2 · y 1 | #FFFFFF0B | 6 |
| Orb_1/Loop/Ring_Halo_faded_alpha_soft | Billboard | Alpha | Halo_Faded / – / – | dist 2 | 1.51 | 1.49 | 3D x1.9 y1.95 z1 | 率 0 爆发[0s x1] |  | x 1 · y 1 | #FFFFFFFF | 35 |
| Orb_1/Loop/Ring_StrongOutline_innerglow_strong_1_alpha_soft | Billboard | Alpha | Ring_StrongOutline_GlowInner / – / – | soft dist 2 | 1.51 | 1.49 | 1.625 | 率 0 爆发[0s x1] |  | x 0 · y 1.2 | #FFFFFFFF | 35 |
| Orb_1/Loop/Smoke_loop_flipbook_thingy_Clean_alpha_soft | Billboard | Alpha | Tx_Noise_18 / Smoke_loop_thingy_alpha2025-05-30 / Tx_Noise_12 | soft dist 0.03 | 3..4 | 1.5 | 5..4 | 率 2 爆发[0s x0] | 5x5 | x 8.03 · y x2 [0:1 0.066:0.582 1:1] | #000000FF | -500 |
| Orb_1/Loop/Shockwave_blood_Distorted_MD_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Mask_02 / Tx_Noise_29 / Tx_Noise_17 | soft ramp dist 0.1 | 5 | 0 | 1.25 | 率 0.5 爆发[0s x1] |  | x x25 [0:1 1:0] · y x2 [0:0.633 1:0.927] | #525252FF | -50 |
| Orb_1/Loop/FuzzAlpha | Billboard | Alpha | Default-Particle / – / Default-Particle |  | 3 | 0 | 3 | 率 0 爆发[0s x1] |  | x 5 · y 1 | #000000FF | 5 |
| Orb_1/Loop/bamboo_leaf_alpha_soft | Billboard | Alpha | bamboo_leaf_1 / – / – |  | 2.5 | 0 | 0.1..0.12 | 率 25 爆发[0s x0] | 2x2 | x x2 [0:1 1:0] · y x2 [0:0.5 1:1] | #D7FF61FF..#10590CFF | 0 |

## Ying Yang pulse（9 个系统，启用 7）

| 系统 | 渲染 | 混合 | 贴图 | 标志 | 寿命 | 延迟 | 尺寸 | 发射 | 序列帧 | C1 | 起色 | 排序 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Ground_2/Liquid_smudge_pb_3_nac_alpha | Billboard | Alpha | Liquid_smudge_pb_3 / – / Tx_Noise_20 | dist 0.005 | 4 | 0 | 9.6 | 率 2 爆发[0s x0] | 2x2 | x x30 [0:1 1:0.04] · y x2 [0:0.977 0.504:0.584] | #00000060 | 2 |
| Ground_2/Liquid_smudge_pb_3_nac_alpha | Billboard | Alpha | Liquid_smudge_pb_3 / – / Tx_Noise_20 | dist 0.005 | 4 | 0 | 1..0.5 | 率 12 | 2x2 | x x30 [0:1 1:0.04] · y x2 [0:0.977 0.128:0.553 1:1] | #0000006E | 2 |
| Ground_2/Explosion_Shockwave_06_Distorted_necro_add_soft | Mesh UV_Distorted_PolarCords | Alpha | OffsetLine_Warped2 / Tx_Liquid_01_2 / PearlyRainbowNoise | soft dist 0.1 | 2 | 0 | 3 | 率 2 爆发[0s x0] |  | x x100 [0:1 1:0.004] · y x2 [0.493:0.375 1:1] | #000000FF | 0 |
| Ground_2/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 3..2.5 | 0 | 2..1.5 | 率 8 爆发[0s x0] | 8x8 | x 3 · y x2 [0:0.737 0.189:0.491 0.895:0.548 1:1] | #00000074 | -24 |
| Ground_2/yy_Shockwave_3_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Tx_Mask_01_Clamped / Tx_Noise_09 | ramp dist 0.3 | 2 | 0 | 3.5 | 率 2 爆发[0s x0] |  | x x10 [0.023:1 1:0.284] · y 1.29 | #000000D0 | -254 |
| Ground_2/yy_Shockwave_1_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | dist 2 | 2 | 0 | 4 | 率 2 爆发[0s x0] |  | x x1 [0:1 1:0] · y x2 [0:1 0.013:0.459 1:0.638] | #FFFFFFFF | -5 |
| Ground_2/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 2..1 | 0 | 0.02..0.05 | 率 25 爆发[0s x0] | 2x2 | x x150 [0:1 1:0] · y x2 [0:0.5 1:1] | #FFC6FDFF | 0 |

## Ying Yang Pulses（43 个系统，启用 31）

| 系统 | 渲染 | 混合 | 贴图 | 标志 | 寿命 | 延迟 | 尺寸 | 发射 | 序列帧 | C1 | 起色 | 排序 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| DefaultHDParticleMaterial_1/mat_Empty/mat_Empty/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 0.25 | 1.6 | 8 | 率 0 爆发[0s x1 cyc5/1] |  | x x2 [0:1 0.099:0.18 1:0] · y 0 | #FFFFFFFF | 15 |
| DefaultHDParticleMaterial_1/mat_Empty/mat_Empty/LightFlash_Soft | Billboard | Add | LightFlash / – / – | soft | 0.7 | 1.6 | 10 | 率 0 爆发[0s x1 cyc5/1] |  | x x1 [0:1 0.099:0.18 1:0] · y 1.2 | #FFFFFFFF | -25 |
| DefaultHDParticleMaterial_1/mat_Empty/mat_Empty/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 0.75 | 1.6 | 14.6 | 率 0 爆发[0s x1 cyc5/1] |  | x x1 [0:1 1:0] · y 0 | #000000FF | -2 |
| DefaultHDParticleMaterial_1/mat_Empty/mat_Empty/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 1.5..1 | 1.6 | 3..5 | 率 0 爆发[0s x20 cyc5/1] | 8x8 | x 0 · y x2 [0:1 0.064:0.608 1:1] | #00000096 | -24 |
| DefaultHDParticleMaterial_1/mat_Empty/mat_Empty/Flecks_Shiny_Alpha | Stretch | Alpha | Fleks_Shiny / Fleks_Shiny / Fleks_Shiny |  | 0.9..1 | 1.6 | 0.02..0.04 | 率 5 爆发[0s x82 cyc5/1] | 2x2 | x x25 [0:1 1:0] · y x2 [0:0.5 0.089:1] | #FFFFFFFF | 0 |
| FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 4 | 0 | 5.64 | 率 1 爆发[0s x0] |  | x 0 · y 0 | #DB5D1EFF | -25 |
| FuzzAdd_Soft/ant/Burst_1/Center/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 5.5 | 0.15 | 15 | 率 0 爆发[0s x1] |  | x x0 [0:0 1:1] · y 0 | #FFFFFF65 | -10 |
| FuzzAdd_Soft/ant/Burst_1/Center/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 5.5 | 0.15 | 15 | 率 0 爆发[0s x1] |  | x 1 · y 0 | #000000FF | -25 |
| FuzzAdd_Soft/ant/Burst_1/Center/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 1.5..2 | 0.15 | 8..10 | 率 10 爆发[0s x0] | 8x8 | x 3 · y x2 [0:0.737 0.189:0.491 0.895:0.548 1:1] | #636363FF | -24 |
| FuzzAdd_Soft/ant/Center/Orb_1/Spawn/yy_Shockwave_1_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | soft dist 2 | 3 | 0.2 | 2 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.013:0.459 1:1] | #000000FF | -5 |
| FuzzAdd_Soft/ant/Center/Orb_1/Spawn/Mana_Gather_in_aura_quad_add_soft_desat | Mesh Sphere | Add | RainbowMirrorNoise / Gradient_maker_Y_Fade_tappered / Tx_Noise_36 | soft dist 0.2 | 1.5 | 0 | 1.25 | 率 0 爆发[0s x1] |  | x 2 · y 1 | #FFFFFF0B | 6 |
| FuzzAdd_Soft/ant/Center/Orb_1/Spawn/Ring_Halo_faded_alpha_soft | Billboard | Alpha | Halo_Faded / – / – | dist 2 | 1.5 | 0 | 3D x1.9 y1.95 z1 | 率 0 爆发[0s x1] |  | x 1 · y 1 | #FFFFFFFF | 35 |
| FuzzAdd_Soft/ant/Center/Orb_1/Spawn/Ring_StrongOutline_innerglow_strong_1_alpha_soft | Billboard | Alpha | Ring_StrongOutline_GlowInner / – / – | soft dist 2 | 1.5 | 0 | 1.625 | 率 0 爆发[0s x1] |  | x 0 · y 1.2 | #FFFFFFFF | 35 |
| FuzzAdd_Soft/ant/Center/Orb_1/Loop/yy_Shockwave_1_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | soft dist 2 | 2 | 0.2 | 3 | 率 1 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.013:0.459 1:1] | #000000FF | -50 |
| FuzzAdd_Soft/ant/Center/Orb_1/Loop/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 7 | 0 | 3 | 率 0 爆发[0s x1] |  | x 5 · y 1 | #000000FF | -50 |
| FuzzAdd_Soft/ant/Center/Orb_1/Loop/yy_2_symbol_alpha_soft | Billboard | Alpha | yingyang_2 / Gradient_maker_simple_round_radial / Tx_Noise_12 | soft ramp dist 0.02 | 7 | 0 | 1.65 | 率 0 爆发[0s x1] |  | x 5 · y x2 [0:1 0.118:0.556 0.911:0.509 1:1] | #FFFFFF8F | 250 |
| FuzzAdd_Soft/ant/Center/Orb_1/Loop/yy_2_symbol_alpha_soft | Billboard | Alpha | yingyang_2 / Gradient_maker_simple_round_radial / Tx_Noise_12 | soft ramp dist 0.02 | 1.5 | 1.5 | 3.5 | 率 0 爆发[0s x1 cyc5/1] |  | x 5 · y x2 [0:1 0.051:0.487 1:1] | #FFFFFF2F | 250 |
| FuzzAdd_Soft/ant/Center/Orb_1/Loop/Mana_Gather_in_aura_quad_add_soft_desat | Mesh Sphere | Add | RainbowMirrorNoise / Gradient_maker_Y_Fade_tappered / Tx_Noise_36 | soft dist 0.2 | 5.5 | 1.49 | 1.25 | 率 0 爆发[0s x1] |  | x 2 · y 1 | #FFFFFF0B | 6 |
| FuzzAdd_Soft/ant/Center/Orb_1/Loop/Ring_Halo_faded_alpha_soft | Billboard | Alpha | Halo_Faded / – / – | dist 2 | 5.1 | 1.49 | 3D x1.9 y1.95 z1 | 率 0 爆发[0s x1] |  | x 1 · y 1 | #FFFFFFFF | 35 |
| FuzzAdd_Soft/ant/Center/Orb_1/Loop/Ring_StrongOutline_innerglow_strong_1_alpha_soft | Billboard | Alpha | Ring_StrongOutline_GlowInner / – / – | soft dist 2 | 5.25 | 1.49 | 1.625 | 率 0 爆发[0s x1] |  | x 0 · y 1.2 | #FFFFFFFF | 35 |
| FuzzAdd_Soft/ant/Center/Orb_1/Loop/Shockwave_blood_Distorted_MD_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Mask_02 / Tx_Noise_29 / Tx_Noise_17 | soft ramp dist 0.1 | 3 | 0 | 1.25 | 率 1 爆发[0s x1] |  | x x25 [0:1 1:0] · y x2 [0:0.633 1:0.927] | #525252FF | -50 |
| FuzzAdd_Soft/ant/Center/Orb_1/Loop/bamboo_leaf_alpha_soft | Billboard | Alpha | bamboo_leaf_1 / – / – |  | 2.5 | 0 | 0.1..0.15 | 率 10 爆发[0s x0] | 2x2 | x x25 [0:0.078 0.47:1 1:0] · y x2 [0:0.5 1:1] | #14590CFF | 0 |
| FuzzAdd_Soft/ant/Center/Smoke_loop_flipbook_thingy_Clean_alpha_soft | Billboard | Alpha | Tx_Noise_18 / Smoke_loop_thingy_alpha2025-05-30 / Tx_Noise_12 | soft dist 0.03 | 3..2 | 0.7 | 10..8 | 率 8 爆发[0s x0] | 5x5 | x 8.03 · y x2 [0:1 0.066:0.582 1:1] | #00000070 | -24 |
| FuzzAdd_Soft/ant/Center/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 0.9..1 | 0.6 | 0.02..0.03 | 率 0 爆发[0s x75 cyc5/1] | 2x2 | x x25 [0:1 1:0] · y x2 [0:0.5 1:1] | #FFFFFFFF | 0 |
| FuzzAdd_Soft/ant/Center/bamboo_leaf_alpha_soft | Billboard | Alpha | bamboo_leaf_1 / – / – |  | 2.5 | 1.7 | 0.07..0.09 | 率 0 爆发[0s x50 cyc5/1] | 2x2 | x x25 [0:0.078 0.47:1 1:0] · y x2 [0:0.5 1:1] | #14590CFF | 0 |
| FuzzAdd_Soft/ant/Center/yy_Shockwave_1_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | dist 2 | 1 | 1.7 | 12 | 率 0 爆发[0s x1 cyc5/1] |  | x x5 [0:1 1:0] · y x2 [0:1 0.013:0.459 1:0.656] | #FFFFFFFF | -5 |
| FuzzAdd_Soft/ant/Center/yy_Shockwave_3_Distorted_still_alpha | Mesh Vertical_srike_cilinder_new | Alpha | Tx_Line_05_Up / Tx_Mask_01_Clamped / Tx_Noise_09 | ramp dist 0.3 | 1.1..1 | 1.5 | 3D x2 y1 z2 | 率 0 爆发[0s x2 cyc5/1] |  | x x9 [0.023:1 1:-0.019] · y x2 [0.201:0.522 1:1] | #000000A3 | -5 |
| FuzzAdd_Soft/ant/Center/yy_Shockwave_1_alpha | Mesh Vertical_srike_cilinder_new | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | dist 2 | 0.9 | 1.5 | 3D x2 y1 z2 | 率 1 爆发[0s x1] |  | x x21.284 [0.023:1 1:-0.019] · y x2 [0.555:0.606 1:0.677] | #FFFFFFFF | -5 |
| FuzzAdd_Soft/ant/Center/FuzzAlpha | Billboard | Alpha | Default-Particle / – / Default-Particle |  | 7.5 | 0 | 45.17 | 率 0 爆发[0s x1] |  | x 1 · y 0 | #606060FF | -1000 |
| FuzzAdd_Soft/ant/Center/yy_3_symbol_alpha_soft | Billboard | Alpha | yingyang_3 / Gradient_maker_simple_round_radial / Tx_Noise_20 | soft ramp dist 0.01 | 4..3 | 0 | 3D x1.5 y3 z1 | 率 15 爆发[0s x1] | 2x2 | x 0 · y x2 [0.164:0.43 1:1] | #000000DD | 5 |
| FuzzAdd_Soft/ant/Center/Secondary_1/yy_Shockwave_1_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | dist 2 | 1.5 | 1.5 | 3D x0.15 y0.15 z1 | 率 0 爆发[0s x2 cyc5/1] |  | x 0 · y x2 [0:1 0.218:0.534 1:1] | #000000FF | 1 |

## Ying Yang Rain（27 个系统，启用 24）

| 系统 | 渲染 | 混合 | 贴图 | 标志 | 寿命 | 延迟 | 尺寸 | 发射 | 序列帧 | C1 | 起色 | 排序 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Spawn_1/Blood_Dark_Shockwave_05_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | gradient_002_Clamped / Tx_Liquid_01_2 / Noise_TilingSoftWebbing | dist 0.3 | 1 | 0.25 | 7 | 率 0 爆发[0s x1] |  | x x40 [0:1 1:0.004] · y x2 [0.537:0.398 1:1] | #000000FF | 0 |
| Spawn_1/Ink_Shockwave_4_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02 / FX_TX_HorizontalGradient_04 / TX_Noise_01 | dist 0.25 | 5 | 0.1 | 8..8 | 率 0 爆发[0s x2] |  | x 2 · y x2 [0.624:0.432 1:1] | #000000B7 | 0 |
| Spawn_1/Ink_Shockwave_4_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02 / FX_TX_HorizontalGradient_04 / TX_Noise_01 | dist 0.25 | 5 | 0.1 | 7.8..8 | 率 0 爆发[0s x1] |  | x 2 · y x2 [0.64:0.618 1:1] | #FFFFFFB7 | 0 |
| Spawn_1/FuzzAdd | Billboard | Add | Default-Particle / – / Default-Particle |  | 2 | 0.2 | 13 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 1 | #FFFFFF3A | 1 |
| Spawn_1/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 0.4 | 0.2 | 12 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 1 | #FFFFFF48 | 1 |
| Spawn_1/FuzzAlpha | Billboard | Alpha | Default-Particle / – / Default-Particle |  | 4.5 | 0 | 31.25 | 率 0 爆发[0s x1] |  | x 1 · y 0 | #606060FF | 0 |
| Spawn_1/yy_3_symbol_alpha_soft | Billboard | Alpha | yingyang_3 / Gradient_maker_simple_round_radial / Tx_Noise_20 | soft ramp dist 0.01 | 4..3 | 0 | 1.5 | 率 15 爆发[0s x1] | 2x2 | x 0 · y x2 [0.164:0.43 1:1] | #000000DD | 5 |
| Spawn_1/yy_Shockwave_1_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | soft dist 2 | 1.5 | 0.2 | 2.6 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.013:0.459 1:1] | #FFFFFFFF | -5 |
| Spawn_1/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 2.5..1.5 | 0.2 | 0.05..0.01 | 率 1 爆发[0s x55] | 2x2 | x x167.767 [0:1 1:0] · y x2 [0:0.5 0.07:1] | #FFFFFFFF | 0 |
| Spawn_1/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 2 | 0.2 | 12 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 0 | #FFFFFFFF | -2 |
| Spawn_1/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 1..3 | 0 | 5 | 率 1 爆发[0s x50] | 8x8 | x x1 [0:1 1:0] · y x2 [0:1 0.049:0.477 0.749:0.605 1:1] | #FFFFFF1D | -5 |
| Spawn_1/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 0.5 | 0.25 | 4 | 率 0 爆发[0s x1] |  | x x4.854 [0:1 1:0] · y 0 | #000000FF | -2 |
| Spawn_1/Ink_flakes_1_alpha_soft | Billboard | Alpha | Liquid_smudge_pb_1 / Fuzzball_Clamped / Liquid_smudge_pb_1 | soft ramp dist 0.001 | 1.5..2 | 0.25 | 0.5..0.7 | 率 0 爆发[0s x35] | 2x2 | x 5 · y x2 [0:0.508 1:1] | #FFFFFFFF | 1 |
| Spawn_1/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 1 | 0 | 15 | 率 0 爆发[0s x1] |  | x x15.363 [0:0 0.045:0.273 0.275:0.274 0.283:0.603 0.528:0.6 0.536:0.727 0.684:0.73 0.715:0.834 1:1] · y 1 | #575757FF | 5 |
| Spawn_1/Flare | Billboard | Add | flare1_flare2 / – / – | soft | 0.3 | 0 | 18 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 1 | #FFFFFFFF | 50 |
| Spawn_1/Flare | Billboard | Add | flare1_flare2 / – / – | soft | 0.3 | 0 | 18 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 1 | #FFFFFFFF | 50 |
| Spawn_1/yy_Shockwave_3_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Tx_Mask_01_Clamped / Tx_Noise_09 | ramp dist 0.3 | 5 | 0.25 | 2.8 | 率 1 爆发[0s x2] |  | x x10 [0.023:1 1:0.284] · y x2 [0:1 0.153:0.513 1:1] | #00000057 | -254 |
| Spawn_1/bamboo_leaf_alpha_soft | Billboard | Alpha | bamboo_leaf_1 / – / – |  | 2.5 | 0 | 0.07..0.12 | 率 35 爆发[0s x0] | 2x2 | x x2 [0:1 1:0] · y x2 [0:0.5 1:1] | #D7FF61FF..#10590CFF | 0 |
| Rain_1/Nothing_alphaclip | Stretch | Alpha | – / – / – | fresnel dist 1 | 1.6 | 0.25 | 3D x3 y1 z1 | 率 15 爆发[0s x1] |  | x x25 [0:1 1:0] · y 0 | #000000FF..#FFFFFFFF | 0 |
| Rain_1/Nothing_alphaclip/Ink_flakes_1_alpha_soft | Billboard | Alpha | Liquid_smudge_pb_1 / Fuzzball_Clamped / Liquid_smudge_pb_1 | soft ramp dist 0.001 | 0.5..1 | 0 | 1..2 | 率 0 爆发[0s x5] | 2x2 | x 5 · y x2 [0:0.508 1:1] | #000000FF | 1 |
| Rain_1/Nothing_alphaclip/yy_2_symbol_alpha | Billboard | Alpha | yingyang_2 / Gradient_maker_simple_round_radial / Tx_Noise_20 | ramp dist 0.02 | 1.5 | 0 | 0.5 | 率 0 爆发[0s x1] |  | x x4.854 [0:1 1:0] · y x2 [0:1 0.07:0.548 1:1] | #8484842F | -2 |
| Rain_1/Nothing_alphaclip/Spark2_Alpha | Stretch | Alpha | trace_07_rotated / – / – |  | 0.65..0.5 | 0 | 0.3..0.7 | 率 0 爆发[0s x8] |  | x 0 · y 0 | #080416FF | 0 |
| Rain_1/Nothing_alphaclip/Simple_Liquied_fb_1_alpha_soft | Billboard | Alpha | Tx_Noise_20 / Water_Splash_9 / Tx_Noise_20 | soft ramp | 1..0.75 | 0 | 1..0.5 | 率 0 爆发[0s x2] | 7x7 | x 10 · y 0 | #FFFFFFFF | 1 |
| Rain_1/Nothing_alphaclip/yy_Shockwave_3_Distorted_still_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Gradient_Quad03 / Tx_Noise_09 | soft ramp dist 0.3 | 0.8..1.5 | 0 | 1.5..2.2 | 率 0 爆发[0s x2] |  | x x20 [0:1 1:0.004] · y x2 [0.524:0.589 1:1] | #000000B3 | 3 |

## Ying Yang Rune Fall（52 个系统，启用 48）

| 系统 | 渲染 | 混合 | 贴图 | 标志 | 寿命 | 延迟 | 尺寸 | 发射 | 序列帧 | C1 | 起色 | 排序 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| DefaultHDParticleMaterial/yy_Shockwave_3_Distorted_still_alpha_soft | Mesh Jar_shape_tube | Alpha | Tx_Line_05_Up / Gradient_Quad03 / Tx_Noise_09 | soft ramp dist 0.3 | 1.5 | 0 | 3D x3.5 y2.5 z3.5 | 率 1 爆发[0s x1] |  | x x35 [0:0.584 0.023:1 1:0] · y 1.4 | #FFFFFFFF | -5 |
| DefaultHDParticleMaterial/yy_Shockwave_3_Distorted_still_alpha_soft | Mesh Jar_shape_tube | Alpha | Tx_Line_05_Up / Gradient_Quad03 / Tx_Noise_09 | soft ramp dist 0.3 | 1.5 | 0 | 3D x3.5 y2.5 z3.5 | 率 1 爆发[0s x1] |  | x x35 [0:0.584 0.023:1 1:0] · y 1.4 | #000000FF | -5 |
| DefaultHDParticleMaterial/yy_Shockwave_3_Distorted_still_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Gradient_Quad03 / Tx_Noise_09 | soft ramp dist 0.3 | 0.8 | 0 | 1.8 | 率 1 爆发[0s x1] |  | x x10 [0.023:1 1:0.284] · y 1.29 | #000000FF | -254 |
| DefaultHDParticleMaterial/bamboo_leaf_alpha_soft | Billboard | Alpha | bamboo_leaf_1 / – / – |  | 1.5 | 0 | 0.3..0.5 | 率 1 爆发[0s x50] | 2x2 | x x39.53 [0:0.379 0.023:1 0.245:0.221 1:0] · y x2 [0:1 0.049:0.55 1:1] | #14590CFF | -5 |
| DefaultHDParticleMaterial/FuzzAlpha | Billboard | Alpha | Default-Particle / – / Default-Particle |  | 1.5 | 0 | 19.11 | 率 0 爆发[0s x1] |  |  | #000000FF | -7 |
| DefaultHDParticleMaterial/Add_Glow 1 | Billboard | Add | Default-Particle / – / – |  | 1.5 | 0 | 35.61 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 0 | #FFFFFF2A | -2 |
| DefaultHDParticleMaterial/FullRainbowFlare 1 | Stretch | Add | RainbowFlare / – / – |  | 2..2 | 0 | 0.1..0.1 | 率 0 爆发[0s x10] |  | x x20 [0.62:1 1:0] · y 0 | #FFFFFFFF | 0 |
| DefaultHDParticleMaterial/Simple_StarDust_Noise_Add | Billboard | Add | Simple_Star_Dust_Mask / Simple_Star_Dust_Mask / RainbowMirrorNoise |  | 2.5 | 0.7 | 2.5 | 率 1 爆发[0s x50] | 2x2 | x x150 [0.022:0.084 0.023:1 1:0] · y x2 [0:1 0.049:0.55 1:1] | #FFFFFFFF | -5 |
| DefaultHDParticleMaterial/Flecks_Shiny_Additive | Billboard | Add | Fleks_Shiny / – / Fleks_Shiny |  | 1..1.2 | 0.7 | 0.05 | 率 0 爆发[0s x0] | 2x2 | x x500 [0:0.032 0.079:0.848 1:0] · y x2 [0.7:0.5 1:1] | #FFFFFFFF | 0 |
| DefaultHDParticleMaterial/Bullet_rain_1_nomipmap_add_Soft | Stretch | Add | RainbowMirrorNoise_HighRes / Water_drop_nomipmap / RainbowMirrorNoise_HighRes | soft | 1..1.2 | 0.7 | 3D x0.1 y1 z1 | 率 0 爆发[0s x8] |  | x x500 [0:0.032 0.072:1 1:0] · y x2 [0.7:0.5 1:1] | #FFFFFFFF | 0 |
| DefaultHDParticleMaterial/Bullet_rain_1_nomipmap_add_Soft | Stretch | Add | RainbowMirrorNoise_HighRes / Water_drop_nomipmap / RainbowMirrorNoise_HighRes | soft | 0.3..0.5 | 1.85 | 3D x0.1 y1 z1 | 率 0 爆发[0s x10] |  | x x500 [0:0.032 0.072:1 1:0] · y x2 [0.7:0.5 1:1] | #FFFFFFFF | 0 |
| DefaultHDParticleMaterial/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 2.5..2 | 0.6 | 8..5 | 率 0 爆发[0s x15] | 8x8 | x 3 · y x2 [0:0.737 0.189:0.491 0.895:0.548 1:1] | #0000007D | -24 |
| DefaultHDParticleMaterial/FuzzAlpha | Billboard | Alpha | Default-Particle / – / Default-Particle |  | 2 | 0.8 | 19.11 | 率 0 爆发[0s x1] |  |  | #FFFFFFFF | -7 |
| DefaultHDParticleMaterial/FuzzAdd | Billboard | Add | Default-Particle / – / Default-Particle |  | 2 | 0.8 | 35.61 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 0 | #FFFFFF2A | -2 |
| DefaultHDParticleMaterial/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 1.5 | 0.8 | 3D x4 y80 z1 | 率 0 爆发[0s x1] |  | x x5 [0:1 1:0] · y 0 | #FFFFFF60 | -2 |
| DefaultHDParticleMaterial/FuzzAlpha_Soft/FuzzAlpha_Soft_1 | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 1.5 | 0.8 | 3D x4 y80 z1 | 率 0 爆发[0s x1] |  | x x5 [0:1 1:0] · y 0 | #FFFFFF60 | -2 |
| DefaultHDParticleMaterial/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 1.5 | 0.8 | 3D x1 y80 z1 | 率 0 爆发[0s x1] |  | x x5 [0:1 1:0] · y 0 | #FFFFFF60 | -2 |
| DefaultHDParticleMaterial/FuzzAlpha_Soft/FuzzAlpha_Soft_1 | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 1.5 | 0.8 | 3D x1 y80 z1 | 率 0 爆发[0s x1] |  | x x5 [0:1 1:0] · y 0 | #FFFFFF60 | -2 |
| DefaultHDParticleMaterial/FuzzAdd | Billboard | Add | Default-Particle / – / Default-Particle |  | 1.5 | 0.8 | 18.1 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 0 | #FFFFFF60 | -2 |
| DefaultHDParticleMaterial/yy_2_symbol_alpha_soft | Billboard | Alpha | yingyang_2 / Gradient_maker_simple_round_radial / Tx_Noise_12 | soft ramp dist 0.02 | 2.2 | 0.75 | 4 | 率 0 爆发[0s x1] | 1x1 | x x0 [0:0.401 0.547:0.967 1:1] · y x2 [0.527:0.483 0.66:1] | #FFFFFFFF | 25 |
| DefaultHDParticleMaterial/yy_Shockwave_1_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | dist 2 | 0.5 | 1.1 | 2.06 | 率 10 爆发[0s x0] |  | x x8.152 [0:0.401 0.547:0.967 1:1] · y x2 [0.118:0.483 1:1] | #8080803C | 25 |
| DefaultHDParticleMaterial/yy_Shockwave_1_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | dist 2 | 1 | 0.85 | x8.307 [0:0.566 1:1] | 率 10 爆发[0s x0] |  | x x1 [0:0.401 0.547:0.967 1:1] · y x2 [0:0.552 1:1] | #FFFFFF72 | 25 |
| DefaultHDParticleMaterial/Holy_Long_Beam_Vertical_Slowfire_add_soft | Stretch | Add | Trail73 / gradient_004 / RainbowMirrorNoise | dist 0.1 | 1.2 | 0.8 | 3D x2.9 y12 z1 | 率 0 爆发[0s x1] |  | x x10 [0:0.599 1:1] · y 1.25 | #FFFFFF74 | 25 |
| DefaultHDParticleMaterial/FuzzAlpha | Billboard | Alpha | Default-Particle / – / Default-Particle |  | 2 | 1.95 | 35.61 | 率 0 爆发[0s x1] |  | x x5 [0:1 1:0] · y 0 | #80808045 | -2 |
| DefaultHDParticleMaterial/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 0.25 | 1.95 | 40 | 率 0 爆发[0s x1] |  | x x25 [0:1 0.099:0.18 1:0] · y 0 | #000000FF | 15 |
| DefaultHDParticleMaterial/Ring_Halo_faded_add_soft | Billboard | Add | Halo_Faded / – / – | soft dist 2 | 0.4 | 1.95 | 12 | 率 0 爆发[0s x1] |  | x x2 [0:1 1:0] · y 0 | #FFFFFF0E | 2 |
| DefaultHDParticleMaterial/yy_Shockwave_3_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Tx_Mask_01_Clamped / Tx_Noise_09 | ramp dist 0.3 | 5 | 1.95 | 2.21 | 率 1 爆发[0s x2] |  | x x10 [0.023:1 1:0.284] · y x2 [0:1 0.064:0.53 1:1] | #000000A8 | -254 |
| DefaultHDParticleMaterial/yy_Shockwave_3_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Tx_Mask_01_Clamped / Tx_Noise_09 | ramp dist 0.3 | 5 | 1.95 | 2.21 | 率 1 爆发[0s x1] |  | x x10 [0.023:1 1:0.284] · y x2 [0:1 0.064:0.53 1:1] | #000000A8 | -254 |
| DefaultHDParticleMaterial/FuzzAdd | Billboard | Add | Default-Particle / – / Default-Particle |  | 1 | 1.95 | 15 | 率 0 爆发[0s x3] |  | x x1 [0:1 1:0] · y 1.39 | #FFFFFF3C | 5 |
| DefaultHDParticleMaterial/Flare | Billboard | Add | flare1_flare2 / – / – | soft | 0.3 | 1.95 | 18 | 率 0 爆发[0s x1] |  | x x15 [0:1 1:0] · y 1 | #FFFFFF3C | 50 |
| DefaultHDParticleMaterial/Flare | Billboard | Add | flare1_flare2 / – / – | soft | 0.3 | 1.95 | 18 | 率 0 爆发[0s x1] |  | x x15 [0:1 1:0] · y 1 | #FFFFFF3C | 50 |
| DefaultHDParticleMaterial/FuzzAdd | Billboard | Add | Default-Particle / – / Default-Particle |  | 0.25 | 1.95 | 45 | 率 0 爆发[0s x1] |  | x x25 [0:1 0.099:0.18 1:0] · y 0 | #5B5B5BFF | 15 |
| DefaultHDParticleMaterial/yy_Shockwave_1_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | soft dist 2 | 1 | 1.95 | 2.1 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.013:0.459 1:1] | #FFFFFFFF | -5 |
| DefaultHDParticleMaterial/yy_Shockwave_1_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | soft dist 2 | 1 | 1.95 | 2.1 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.013:0.459 1:1] | #FFFFFFFF | -5 |
| DefaultHDParticleMaterial/yy_Shockwave_3_Distorted_still_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Gradient_Quad03 / Tx_Noise_09 | soft ramp dist 0.3 | 1.5 | 1.95 | 1.8 | 率 1 爆发[0s x1] |  | x x10 [0.023:1 1:0.284] · y 1.29 | #000000FF | -254 |
| DefaultHDParticleMaterial/Explosion_Shockwave_04_Distorted_M | Mesh UV_Distorted_PolarCords | Alpha | gradient_002_Clamped / Tx_Liquid_01_2 / Noise_TilingSoftWebbing | dist 0.3 | 1.5 | 1.95 | 1.8 | 率 0 爆发[0s x1] |  | x x20 [0:1 1:0.004] · y x2 [0.537:0.398 1:1] | #000000FF | 3 |
| DefaultHDParticleMaterial/Blood_Dark_Shockwave_05_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | gradient_002_Clamped / Tx_Liquid_01_2 / Noise_TilingSoftWebbing | dist 0.3 | 1 | 1.95 | 6 | 率 0 爆发[0s x1] |  | x x40 [0:1 1:0.004] · y x2 [0.537:0.398 1:1] | #000000FF | 0 |
| DefaultHDParticleMaterial/Ink_Shockwave_1_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02 / FX_TX_HorizontalGradient_04 / TX_Noise_01 | dist 0.35 | 3 | 1.95 | 5..7 | 率 0 爆发[0s x2] |  | x 2 · y x2 [0:0.432 1:1] | #000000B7 | 0 |
| DefaultHDParticleMaterial/yy_2_symbol_alpha_soft | Billboard | Alpha | yingyang_2 / Gradient_maker_simple_round_radial / Tx_Noise_12 | soft ramp dist 0.02 | 1.8 | 1.95 | 4.5 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.054:0.561 1:1] | #FFFFFF9F | 0 |
| DefaultHDParticleMaterial/Ink_flakes_1_alpha_soft | Billboard | Alpha | Liquid_smudge_pb_1 / Fuzzball_Clamped / Liquid_smudge_pb_1 | soft ramp dist 0.001 | 1.5..2 | 1.95 | 0.5..0.7 | 率 0 爆发[0s x25] | 2x2 | x 5 · y x2 [0:0.508 1:1] | #FFFFFFA5 | 1 |
| DefaultHDParticleMaterial/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 2 | 1.95 | 13 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 1 | #FFFFFFFF | 1 |
| DefaultHDParticleMaterial/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 0.4 | 1.95 | 12 | 率 0 爆发[0s x1] |  | x x15.363 [0:0 0.045:0.273 0.275:0.274 0.283:0.603 0.528:0.6 0.536:0.727 0.684:0.73 0.715:0.834 1:1] · y 1 | #FFFFFF19 | 1 |
| DefaultHDParticleMaterial/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 2.5..1.5 | 1.95 | 0.05..0.01 | 率 1 爆发[0s x55] | 2x2 | x x167.767 [0:1 1:0] · y x2 [0:0.5 0.089:1] | #FFFFFFFF | 0 |
| DefaultHDParticleMaterial/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 1..3 | 1.95 | 5 | 率 1 爆发[0s x50] | 8x8 | x x1 [0:1 1:0] · y x2 [0:1 0.049:0.477 0.749:0.605 1:1] | #FFFFFF1F | -5 |
| DefaultHDParticleMaterial/FuzzAlpha | Billboard | Alpha | Default-Particle / – / Default-Particle |  | 3 | 1.95 | 45.2 | 率 0 爆发[0s x1] |  | x 7.14 · y 0 | #000000FF | 4 |
| DefaultHDParticleMaterial/Flecks_Shiny_Additive | Billboard | Add | Fleks_Shiny / – / Fleks_Shiny |  | 1..2 | 1.95 | 0.05 | 率 100 爆发[0s x0] | 2x2 | x x500 [0:1 1:0] · y x2 [0.7:0.5 1:1] | #FFFFFFFF | 0 |
| DefaultHDParticleMaterial/bamboo_leaf_alpha_soft | Billboard | Alpha | bamboo_leaf_1 / – / – |  | 1..1.5 | 1.95 | 0.5..0.7 | 率 0 爆发[0s x55] | 2x2 | x x25 [0:1 0.205:0.419 1:0] · y x2 [0:0.5 1:1] | #14590CFF | 15 |
| DefaultHDParticleMaterial/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 1..1.5 | 1.95 | 8..5 | 率 0 爆发[0s x30] | 8x8 | x 3 · y x2 [0.077:1 0.189:0.491 0.895:0.548 1:1] | #0000004C | -24 |

## Ying Yang Rune（38 个系统，启用 32）

| 系统 | 渲染 | 混合 | 贴图 | 标志 | 寿命 | 延迟 | 尺寸 | 发射 | 序列帧 | C1 | 起色 | 排序 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1A/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 2..1 | 0 | 0.02..0.03 | 率 0 爆发[0s x0] | 2x2 | x x25 [0:1 1:0] · y x2 [0:0.5 1:1] | #FFFFFFFF | 0 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1A/FuzzAlpha | Billboard | Alpha | Default-Particle / – / Default-Particle |  | 1.5 | 0 | 5 | 率 0 爆发[0s x1] |  | x 1 · y 0 | #000000E9 | -11 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1A/Slash_Ink_Fill_alpha | Mesh High_Poly_flat_ring | Alpha | InkStroke_Test_1 / gradient_004 / Tx_Noise_15 | ramp dist 0.01 | 1 | 0 | 1.5 | 率 0 爆发[0s x1] |  | x x25 [0:0.187 1:1] · y 1 | #6A6A6AFF | 25 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1A/FuzzAdd | Billboard | Add | Default-Particle / – / Default-Particle |  | 1 | 0 | 8 | 率 0 爆发[0s x1] |  | x x1 [0:0 1:1] · y 1 | #FFFFFF16 | 5 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1A/Ink_Shockwave_1_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02 / FX_TX_HorizontalGradient_04 / TX_Noise_01 | dist 0.35 | 1 | 0.1 | 2.5 | 率 0 爆发[0s x2] |  | x 2 · y x2 [0.473:0.502 1:1] | #000000B7 | 0 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1A/Ink_Shockwave_1_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02 / FX_TX_HorizontalGradient_04 / TX_Noise_01 | dist 0.35 | 0.8 | 0 | 2 | 率 0 爆发[0s x1] |  | x 2 · y x2 [0.473:0.502 1:1] | #000000B3 | 0 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1A/Simple_Circle_faded_alpha | Billboard | Alpha | Simple_Circle_Tx_Sphere / – / Default-Particle |  | 1 | 0.2 | 4.13 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 0 | #00000050 | -2 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1A/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 2.5..1.5 | 0 | 0.05..0.07 | 率 25 爆发[0s x0] | 2x2 | x x167.767 [0:1 1:0] · y x2 [0:0.5 1:1] | #FFFFFFFF | 0 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1A/yy_2_symbol_alpha_soft | Billboard | Alpha | yingyang_2 / Gradient_maker_simple_round_radial / Tx_Noise_12 | soft ramp dist 0.02 | 1 | 0 | 7.83 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.016:0.605 1:0.457] | #FFFFFFFF | 0 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/Ground_2/Shockwave_blood_Distorted_MD_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Mask_02 / Tx_Noise_29 / Tx_Noise_17 | ramp dist 0.1 | 0.7 | 1 | 4 | 率 0 爆发[0s x1] |  | x x500 [0:1 0.038:0.27 1:0] · y x2 [0:0.589 0.493:0.635 1:1] | #000000FF | 0 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/Ground_2/Simple_StarDust_notil_Noise_Desaturated_Add | Billboard | Add | Simple_Star_Dust_Mask / Simple_Star_Dust_Mask / RainbowMirrorNoise |  | 0.7..1.4 | 1 | 3..5 | 率 0 爆发[0s x50] | 2x2 | x x794.694 [0:1 1:0] · y x2 [0:0.5 1:0.52] | #FFFFFFFF | 15 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/Ground_2/Explosion_Shockwave_04_Distorted_M | Mesh UV_Distorted_PolarCords | Alpha | gradient_002_Clamped / Tx_Liquid_01_2 / Noise_TilingSoftWebbing | dist 0.3 | 1 | 1 | 5 | 率 0 爆发[0s x1] |  | x x40 [0:1 1:0.004] · y x2 [0.537:0.398 1:0.739] | #000000FF | 0 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/Ground_2/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 1 | 1 | 8 | 率 0 爆发[0s x1] |  | x x5 [0:1 1:0] · y 1 | #D1CBDBFF | 1 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/Ground_2/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 0.5 | 1 | 8 | 率 0 爆发[0s x1] |  | x x5 [0:1 1:0] · y 1 | #FFFFFFFF | 1 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/Ground_2/Ink_Shockwave_1_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02 / FX_TX_HorizontalGradient_04 / TX_Noise_01 | dist 0.35 | 1.5 | 1 | 5 | 率 0 爆发[0s x2] |  | x x5 [0:0.75 0.023:1 1:0] · y x2 [0.197:0.467 1:1] | #0D0A13AF | -5 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/Ground_2/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 0.15 | 1 | 7.06 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 0 | #000000FF | -2 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/Ground_2/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 1 | 1 | 15 | 率 0 爆发[0s x1] |  | x 5 · y 1 | #000000FF | 5 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/Ground_2/Explosion_Shockwave_06_Distorted_necro_add_soft | Mesh UV_Distorted_PolarCords | Alpha | OffsetLine_Warped2 / Tx_Liquid_01_2 / PearlyRainbowNoise | soft dist 0.1 | 1.5 | 1 | 6..8 | 率 0 爆发[0s x2] |  | x x100 [0:1 1:0.004] · y x2 [0.493:0.375 1:1] | #000000FF | 0 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/Ground_2/Liquid_Splat_radial_ink_1_alpha | Billboard | Alpha | Blood_Decal_Omni4_em / – / Tx_Noise_20 | ramp dist 0.005 | 5 | 1 | 13.68 | 率 0 爆发[0s x2] |  | x x5 [0:1 0.109:0.17 1:0] · y x2 [0:1 0.05:0.507 0.797:0.507 1:1] | #464646D7 | 1 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/Ground_2/Blood_Dark_Shockwave_05_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | gradient_002_Clamped / Tx_Liquid_01_2 / Noise_TilingSoftWebbing | dist 0.3 | 1 | 1 | 5 | 率 0 爆发[0s x1] |  | x x40 [0:1 1:0.004] · y x2 [0.537:0.398 1:1] | #000000FF | 0 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/Ground_2/Blood_Splash_Radial_Wide_Alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Mask_02 / Gradient_Quad03 / Tx_Noise_13 | soft dist 0.1 | 1 | 1 | 5 | 率 0 爆发[0s x1] |  | x x10 [0:1 1:0.004] · y x2 [0.551:0.624 1:1] | #000000BE | 0 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/Ground_2/FuzzAdd_Soft | Billboard | Add | Default-Particle / – / Default-Particle | soft | 4 | 1 | 15 | 率 0 爆发[0s x1] |  | x x3 [0:0.609 0.477:1] · y 1 | #DDDDDD16 | 5 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/Ground_2/Flare | Billboard | Add | flare1_flare2 / – / – | soft | 0.3 | 1 | 18 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 1 | #FFFFFFFF | 50 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/Ground_2/Flare | Billboard | Add | flare1_flare2 / – / – | soft | 0.3 | 1 | 18 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 1 | #FFFFFFFF | 50 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/Ground_2/Ring_Halo_faded_add_soft | Billboard | Add | Halo_Faded / – / – | soft dist 2 | 0.4 | 1 | 12 | 率 0 爆发[0s x1] |  | x x0 [0:1 1:0] · y 0 | #FFFFFF2D | 2 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/Ground_2/yy_Shockwave_1_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | soft dist 2 | 1 | 1 | 3 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.013:0.459 1:1] | #FFFFFFFF | -5 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/Ground_2/yy_Shockwave_1_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | soft dist 2 | 1 | 1 | 3 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.013:0.459 1:1] | #FFFFFFFF | -5 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/ink_Shockwave_ring_tall_1_alpha_soft | Mesh ConeTapered_Wide | Alpha | TX_Noise_01 / gradient_horizontal_Simple / Tx_Noise_19 | soft ramp dist 0.1 | 0.5 | 1 | 3D x0.8 y0.5 z0.8 | 率 0 爆发[0s x5] |  | x 100 · y x2 [0.324:0.76 1:1] | #000000FF | 0 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/Curse_Vertical_flare_Blood_alpha_soft | Mesh tube | Alpha | RainbowMirrorNoise_HighRes / Gradient_maker_Noise_Y_Cut_Down / TX_Noise_21 | dist 0.05 | 0.5 | 1 | 3D x2 y5 z2 | 率 0 爆发[0s x1] |  | x x40 [0:1 1:0] · y x2 [0:0.425 1:0.663] | #FFFFFFFF | -5 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 3..1.5 | 1 | 4..3 | 率 0 爆发[0s x15] | 8x8 | x 3 · y x2 [0:0.737 0.189:0.491 0.895:0.548 1:1] | #100C16AC | -24 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/bamboo_leaf_alpha_soft | Billboard | Alpha | bamboo_leaf_1 / – / – |  | 1.25..1.75 | 1 | 0.2..0.3 | 率 0 爆发[0s x35] | 2x2 | x x2 [0:1 1:0] · y x2 [0:0.5 1:1] | #D7FF61FF..#10590CFF | 0 |
| DefaultHDParticleMaterial/DefaultHDParticleMaterial/1M/yy_2_symbol_alpha_soft | Billboard | Alpha | yingyang_2 / Gradient_maker_simple_round_radial / Tx_Noise_12 | soft ramp dist 0.02 | 2 | 0.9 | 13.91 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:-0.395 0.048:0.682 1:1] | #FFFFFFFF | 0 |

## Ying Yang Screen Space FX（4 个系统，启用 1）

| 系统 | 渲染 | 混合 | 贴图 | 标志 | 寿命 | 延迟 | 尺寸 | 发射 | 序列帧 | C1 | 起色 | 排序 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| yy_SS_test_1/Scaler/ScreenSpaceEffect_Base | Mesh RadialUV_Door | Alpha | tex_Noise_Caustic_001 / Gradient11_CLAMPED / Tx_Noise_26 | ramp dist 0.025 | 4 | 0 | 0.07 | 率 0 爆发[0s x1] |  | x 0 · y x2.2 [0:1 0.228:0.433 0.805:0.432 1:1] | #FFFFFFFF | 150 |

## Ying Yang Shield（24 个系统，启用 21）

| 系统 | 渲染 | 混合 | 贴图 | 标志 | 寿命 | 延迟 | 尺寸 | 发射 | 序列帧 | C1 | 起色 | 排序 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| DefaultHDParticleMaterial/Necromancy_Shockwave_ring_Lightning_add_soft | Mesh ConeTapered_Wide | Add | TX_Noise_01 / gradient_horizontal_Simple / Tx_Noise_30 | soft dist 0.1 | 2 | 0 | 3D x0.6 y0.2 z0.6 | 率 3 爆发[0s x2] |  | x 6.64 · y 1.5 | #FFFFFFFF | 0 |
| DefaultHDParticleMaterial/Simple_Circle_faded_alpha_soft | Billboard | Alpha | Simple_Circle_Tx_Sphere / – / Default-Particle | soft | 2 | 0 | 7 | 率 0 爆发[0s x1] |  |  | #FFFFFFFF | -25 |
| DefaultHDParticleMaterial/Ring_Halo_faded_add | Billboard | Add | Halo_Faded / – / – | dist 2 | 2 | 0 | 6.98 | 率 0 爆发[0s x1] |  | x x2 [0:1 1:0.252] · y 0 | #FFFFFF9F | 1 |
| DefaultHDParticleMaterial/Ring_StrongOutline_innerglow | Billboard | Add | Ring_StrongOutline_GlowInner / – / – | dist 2 | 2 | 0 | 7.5 | 率 0 爆发[0s x1] |  | x x1.016 [0:0.048 0.493:1 1:0.096] · y 0 | #FFFFFF9F | 2 |
| DefaultHDParticleMaterial/FuzzAlpha | Billboard | Alpha | Default-Particle / – / Default-Particle |  | 9 | 0 | 14.1 | 率 0 爆发[0s x1] |  | x 1 · y 0 | #000000FF | -11 |
| DefaultHDParticleMaterial/Flare | Billboard | Add | flare1_flare2 / – / – | soft | 2 | 0 | 30 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 0 | #FFFFFF9F | 0 |
| DefaultHDParticleMaterial/yy_2_symbol_alpha_soft | Billboard | Alpha | yingyang_2 / Gradient_maker_simple_round_radial / Tx_Noise_12 | soft ramp dist 0.02 | 2 | 0 | 11.52 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0.091:0.496 1:1] | #FFFFFF9F | 0 |
| DefaultHDParticleMaterial/Flare | Billboard | Add | flare1_flare2 / – / – | soft | 2 | 0 | 45 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y 0 | #FFFFFF9F | 0 |
| DefaultHDParticleMaterial/yy_2_symbol_alpha_soft | Billboard | Alpha | yingyang_2 / Gradient_maker_simple_round_radial / Tx_Noise_20 | ramp dist 0.02 | 10 | 0.5 | 9.96 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.218:0.434 0.848:0.471 1:1] | #9898989F | 0 |
| DefaultHDParticleMaterial/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 3.5..2 | 0 | 13..10 | 率 0 爆发[0s x15] | 8x8 | x 3 · y x2 [0:0.737 0.189:0.491 0.895:0.548 1:1] | #5959599F | -24 |
| DefaultHDParticleMaterial/yy_Shockwave_3_Distorted_still_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Gradient_Quad03 / Tx_Noise_09 | soft ramp dist 0.3 | 3 | 0 | 8 | 率 2 爆发[0s x1] |  | x x1.854 [0:1 1:0] · y x2 [0:1 0.036:0.726 1:0.726] | #FFFFFFFF | -25 |
| DefaultHDParticleMaterial/Aura_yy1_Shockwave_add_soft | Mesh RadialUV_Dense_UVFlipped | Add | Tx_Line_02_down / Gradient8.1 / Tx_Noise_30 | dist 0.1 | 10 | 0 | 47 | 率 0 爆发[0s x1] |  | x x3.276 [0:1 1:1] · y x2 [0:1 0.106:0.458] | #FFFFFFFF | -10 |
| DefaultHDParticleMaterial/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 1.3..0.9 | 0 | 0.02..0.01 | 率 0 爆发[0s x0] | 2x2 | x x25 [0:1 1:0] · y x2 [0:0.5 1:1] | #FFFFFFFF | 0 |
| DefaultHDParticleMaterial/yy_1_Wall_Fill_TG_new_add_soft | Mesh SphereIn | Add | Tx_Noise_36 / Gradient_maker_Y_Fade_tappered / RainbowMirrorNoise_HighRes | soft dist 0.1 | 10 | 0 | 3.82 | 率 0 爆发[0s x1] |  | x x20.569 [0:1 1:1] · y x2 [0:1 0.08:0.358 0.907:0.368 1:1] | #FFFFFF9F | 7 |
| DefaultHDParticleMaterial/yy_1_Wall_Frenel_new_TG_add_soft | Mesh Sphere | Alpha | RainbowMirrorNoise_HighRes / Gradient_Quad03 / Tx_Noise_36 | soft fresnel dist 0.05 | 10 | 0 | 3.85 | 率 0 爆发[0s x1] |  | x x0 [0:1 1:1] · y x2 [0:1 0.085:0.603 0.912:0.613 1:1] | #FFFFFF9F | 6 |
| DefaultHDParticleMaterial/yy_1_shield_lines_add_soft | Mesh SS_Water_Sphere | Alpha | RainbowMirrorNoise_HighRes / Gradient_Quad03 / Tx_Noise_20 | soft dist 0.02 | 10 | 0 | 4 | 率 0 爆发[0s x1] |  | x x0 [0:1 1:1] · y x2 [0:1 0.085:0.711 0.912:0.721 1:1] | #FFFFFF9F | 4 |
| DefaultHDParticleMaterial/Smoke_Disipation_Explode_FB_Alpha_Soft | Billboard | Alpha | Simple_Smoke_disipassion_Explotion_alpha2025-05-312 / Simple_Smoke_disipassion_Explotion_alpha2025-05-312 / Tx_Noise_12 | soft dist 0.03 | 1.5..2 | 0 | 8..10 | 率 10 爆发[0s x0] | 5x5 | x 3 · y x2 [0:0.737 0.189:0.491 0.895:0.548 1:1] | #1216149F | -24 |
| DefaultHDParticleMaterial/Simple_Circle_faded_alpha_soft | Billboard | Alpha | Simple_Circle_Tx_Sphere / – / Default-Particle | soft | 8.2 | 1.5 | 6 | 率 0 爆发[0s x1] |  |  | #000000FF | -25 |
| DefaultHDParticleMaterial/FX_Shield_Ring_White_01_M | Billboard | Add | Ring_StrongOutline_GlowInner / Gradient_Quad02_nomipmap / TX_Noise_01 | dist 0.07 | 2 | 0 | 7.5 | 率 0 爆发[0s x2] |  | x x6.3 [0:1 1:-0.025] · y x2 [0:1 0.074:0.508 0.846:0.699 1:1] | #FFFFFF2B | 25 |
| DefaultHDParticleMaterial/Ring_Halo_faded_rainbow_add_soft | Billboard | Add | Halo_Faded / Halo_Faded / RainbowMirrorNoise_HighRes | soft | 8.3 | 1.5 | 6.98 | 率 0 爆发[0s x1] |  | x x5 [0:1 1:0.252] · y 0 | #FFFFFFF8 | 1 |
| DefaultHDParticleMaterial/bamboo_leaf_alpha_soft | Billboard | Alpha | bamboo_leaf_1 / – / – |  | 2.5 | 0 | 0.2..0.3 | 率 10 爆发[0s x0] | 2x2 | x x2 [0:1 1:0] · y x2 [0:0.5 1:1] | #D7FF61FF..#10590CFF | 0 |

## Ying Yang Smoke（19 个系统，启用 17）

| 系统 | 渲染 | 混合 | 贴图 | 标志 | 寿命 | 延迟 | 尺寸 | 发射 | 序列帧 | C1 | 起色 | 排序 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| loop/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 4..5 | 0 | 3 | 率 15 爆发[0s x1] | 8x8 | x 0 · y x2 [0.164:0.43 1:1] | #B9B9B95E..#DBDBDB4C | 5 |
| loop/Smoke_screen_cloud_sharp_fb_1_alpha_soft/yy_3_symbol_alpha_soft | Billboard | Alpha | yingyang_3 / Gradient_maker_simple_round_radial / Tx_Noise_20 | soft ramp dist 0.01 | 4..3 | 0 | 3 | 率 0 爆发[0s x1] |  | x 0 · y x2 [0.164:0.43 1:1] | #000000DD | 5 |
| loop/Flecks_Shiny_Alpha | Stretch | Alpha | Fleks_Shiny / Fleks_Shiny / Fleks_Shiny |  | 3..2 | 0 | 0.03..0.02 | 率 5 爆发[0s x75] | 2x2 | x x1 [0:1 1:0] · y x2 [0:0.5 1:1] | #FFFFFFFF | 0 |
| loop/Flecks_Shiny_Alpha | Stretch | Alpha | Fleks_Shiny / Fleks_Shiny / Fleks_Shiny |  | 3..2 | 0 | 0.03..0.02 | 率 55 爆发[0s x5] | 2x2 | x x1 [0:1 1:0] · y x2 [0:0.5 1:1] | #FFFFFFFF | 0 |
| loop/verticalwind_Vertical_aura_Add_Soft | Stretch | Add | Tx_Noise_12 / Gradient_maker_simple_round_radial / Tx_Noise_31 |  | 1..2 | 0 | 3D x0.5 y0.5 z0 | 率 5 爆发[0s x39] |  | x x1 [0:1 1:0] · y x2 [0:0.523 1:1] | #FFFFFFFF | 0 |
| loop/Desat_wind_Shockwave3_wide_add_Soft | Mesh UV_Distorted_PolarCords | Add | Tx_Mask_02 / Tx_Mask_02 / Tx_Noise_31 | soft dist 0.12 | 1..1.5 | 0 | 3 | 率 1 爆发[0s x2] |  | x x0 [0:0.768 0.023:1 1:0] · y x2 [0:1 0.056:0.529 1:0.573] | #FFFFFF94 | -5 |
| loop/Wind_Frosty_add_soft | Mesh ConeTapered_Wide | Add | TX_Noise_01 / gradient_horizontal_Simple / Tx_Noise_12 | soft dist 0.31 | 2 | 0 | 3D x1.2 y0.5 z1.2 | 率 1 |  | x 6.64 · y 1.5 | #FFFFFFFF | 0 |
| loop/FuzzAlpha | Billboard | Alpha | Default-Particle / – / Default-Particle |  | 4.5 | 0 | 31.25 | 率 0 爆发[0s x1] |  | x 1 · y 0 | #606060FF | 0 |
| loop/bamboo_leaf_alpha_soft | Billboard | Alpha | bamboo_leaf_1 / – / – |  | 1.25..1.75 | 0 | 0.1..0.2 | 率 0 爆发[0s x35] | 2x2 | x x10 [0:1 1:0] · y x2 [0:0.5 1:1] | #D7FF61FF..#10590CFF | 0 |
| loop/bamboo_leaf_alpha_soft | Billboard | Alpha | bamboo_leaf_1 / – / – |  | 1.25..1.75 | 0.5 | 0.1..0.15 | 率 25 爆发[0s x0] | 2x2 | x x10 [0:1 1:0] · y x2 [0:0.5 1:1] | #D7FF61FF..#10590CFF | 0 |
| loop/yy_2_symbol_alpha_soft | Billboard | Alpha | yingyang_2 / Gradient_maker_simple_round_radial / Tx_Noise_12 | soft ramp dist 0.02 | 6 | 0 | 13.91 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.078:0.434 1:1] | #FFFFFF41 | 0 |
| loop/Liquid_smudge_pb_3_alpha | Billboard | Alpha | Liquid_smudge_pb_3 / – / Tx_Noise_20 | dist 0.005 | 3 | 0.1 | 1..0.5 | 率 25 | 2x2 | x x30 [0:1 1:0.04] · y x2 [0:0.977 0.128:0.553 1:1] | #0000002A | 2 |
| loop/Shockwave_blood_Distorted_MD_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Mask_02 / Tx_Noise_29 / Tx_Noise_17 | ramp dist 0.1 | 1.5 | 0 | 8 | 率 0 爆发[0s x1] |  | x x500 [0:1 0.038:0.27 1:0] · y x2 [0:0.589 0.493:0.635 1:1] | #0000005A | 0 |
| loop/Explosion_Shockwave_06_Distorted_necro_add_soft | Mesh UV_Distorted_PolarCords | Alpha | OffsetLine_Warped2 / Tx_Liquid_01_2 / PearlyRainbowNoise | soft dist 0.1 | 1 | 0 | 10 | 率 0 爆发[0s x1] |  | x x100 [0:1 1:0.004] · y x2 [0.493:0.375 1:1] | #000000FF | 0 |
| loop/yy_Shockwave_3_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Tx_Mask_01_Clamped / Tx_Noise_09 | ramp dist 0.3 | 1.5 | 0 | 6.32 | 率 1 爆发[0s x1] |  | x x10 [0.023:1 1:0.284] · y 1.29 | #000000D0 | -254 |
| loop/yy_Shockwave_1_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | dist 2 | 1 | 0.1 | 8 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.013:0.459 1:0.638] | #B0B0B0FF | -5 |
| loop/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 2..1 | 0 | 0.02..0.05 | 率 1 爆发[0s x85] | 2x2 | x x150 [0:1 1:0] · y x2 [0:0.5 1:1] | #FFFFFFFF | 0 |

## Ying Yang Vertical Explotion（26 个系统，启用 23）

| 系统 | 渲染 | 混合 | 贴图 | 标志 | 寿命 | 延迟 | 尺寸 | 发射 | 序列帧 | C1 | 起色 | 排序 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| ant/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 2..1 | 0 | 5 | 率 15 爆发[0s x0] | 8x8 | x 0 · y x2 [0:1 0.064:0.608 1:1] | #FFFFFF24 | -24 |
| ant/yy_Radial_disk_in_add | Mesh UV_Distorted_PolarCords | Alpha | Tx_Noise_35 / Gradient8.1 / Tx_Noise_05 | ramp dist 2 | 0.3 | 0 | 2.5 | 率 8 爆发[0s x0] |  | x x1 [0:0.158 0.462:1 1:0.211] · y x1 [0:1 0.056:0.529 1:1] | #FFFFFFFF | -5 |
| ant/ink_Radial_disk_in_accent_add | Mesh UV_Distorted_PolarCords | Alpha | Tx_Noise_35 / Gradient8.1 / RainbowMirrorNoise_HighRes | dist 0.15 | 0.5 | 0 | 2.5 | 率 5 爆发[0s x0] |  | x 50 · y 1.3 | #303030FF | -5 |
| ant/ink_Radial_disk_in_border_add | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02 / Gradient_Quad03 / TX_Noise_21 | dist 0.1 | 2 | 0 | 2.78 | 率 0 爆发[0s x1] |  | x 58.92 · y 0 | #2E2E2EFF | -5 |
| Burst/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 2..1 | 2 | 0.02..0.05 | 率 1 爆发[0s x85] | 2x2 | x x150 [0:1 1:0] · y x2 [0:0.5 1:1] | #FFFFFFFF | 0 |
| Burst/Shockwave_blood_Distorted_MD_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Mask_02 / Tx_Noise_29 / Tx_Noise_17 | ramp dist 0.1 | 1.5 | 2 | 8 | 率 0 爆发[0s x1] |  | x x500 [0:1 0.038:0.27 1:0] · y x2 [0:0.589 0.493:0.635 1:1] | #0000005A | 0 |
| Burst/yy_Shockwave_3_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Tx_Mask_01_Clamped / Tx_Noise_09 | ramp dist 0.3 | 1.5 | 2 | 6.32 | 率 1 爆发[0s x1] |  | x x10 [0.023:1 1:0.284] · y 1.29 | #000000D0 | -254 |
| Burst/Liquid_smudge_pb_3_alpha | Billboard | Alpha | Liquid_smudge_pb_3 / – / Tx_Noise_20 | dist 0.005 | 3 | 2 | 1..0.5 | 率 150 | 2x2 | x x30 [0:1 1:0.04] · y x2 [0:0.977 0.128:0.553 1:1] | #00000055 | 2 |
| Burst/bamboo_leaf_alpha_soft | Billboard | Alpha | bamboo_leaf_1 / – / – |  | 1.25..1.75 | 2 | 0.15..0.2 | 率 0 爆发[0s x70] | 2x2 | x x25 [0:1 1:0] · y x2 [0:0.5 1:1] | #D7FF61FF..#10590CFF | 0 |
| Burst/FuzzAlpha | Billboard | Alpha | Default-Particle / – / Default-Particle |  | 3 | 2 | 31.25 | 率 0 爆发[0s x1] |  | x 1 · y 0 | #606060FF | 0 |
| Burst/Flecks_Shiny_Alpha | Stretch | Alpha | Fleks_Shiny / Fleks_Shiny / Fleks_Shiny |  | 3..2 | 2 | 0.03..0.02 | 率 0 爆发[0s x25] | 2x2 | x x1 [0:1 1:0] · y x2 [0:0.5 1:1] | #FFFFFFFF | 0 |
| Burst/Flecks_Shiny_Alpha | Stretch | Alpha | Fleks_Shiny / Fleks_Shiny / Fleks_Shiny |  | 3..2 | 2 | 0.03..0.02 | 率 0 爆发[0s x25] | 2x2 | x x1 [0:1 1:0] · y x2 [0:0.5 1:1] | #FFFFFFFF | 0 |
| Burst/Simple_Liquied_fb_1_alpha_soft | Stretch | Alpha | Tx_Noise_20 / Water_Splash_9 / Tx_Noise_20 | soft ramp | 1.5..1 | 2 | 3D x2 y2 z10 | 率 0 爆发[0s x50] | 7x7 | x 15..20 · y x2 [0:0.575 1:1] | #4B4B4BFF | 1 |
| Burst/yy_Shockwave_1_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | dist 2 | 1 | 2 | 8.94 | 率 0 爆发[0s x1] |  | x x5 [0:1 1:0] · y x2 [0:1 0.013:0.459 1:0.656] | #787878FF | -5 |
| Burst/FuzzAdd | Billboard | Add | Default-Particle / – / Default-Particle |  | 0.5 | 2 | 17.87 | 率 0 爆发[0s x2] |  | x x1 [0:1 1:0.04] · y x2 [0:0.977 0.128:0.553 0.797:0.753 1:1] | #E5E5E547 | -50 |
| Burst/FuzzAlpha | Billboard | Alpha | Default-Particle / – / Default-Particle |  | 4 | 2 | 15.16 | 率 0 爆发[0s x1] |  | x 1 · y 0 | #00000086 | -35 |
| Burst/yy_Shockwave_3_Distorted_still_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Gradient_Quad03 / Tx_Noise_09 | soft ramp dist 0.3 | 1.5 | 2 | 2.5 | 率 1 爆发[0s x1] |  | x x10 [0.023:1 1:0.284] · y 1.29 | #000000FF | -254 |
| Burst/Explosion_Shockwave_04_Distorted_M | Mesh UV_Distorted_PolarCords | Alpha | gradient_002_Clamped / Tx_Liquid_01_2 / Noise_TilingSoftWebbing | dist 0.3 | 1.5 | 2 | 2.5 | 率 0 爆发[0s x1] |  | x x20 [0:1 1:0.004] · y x2 [0.537:0.398 1:1] | #000000FF | 3 |
| Burst/Blood_Dark_Shockwave_05_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | gradient_002_Clamped / Tx_Liquid_01_2 / Noise_TilingSoftWebbing | dist 0.3 | 1 | 2 | 8 | 率 0 爆发[0s x1] |  | x x40 [0:1 1:0.004] · y x2 [0.537:0.398 1:1] | #000000FF | 0 |
| Burst/yy_3_symbol_alpha_soft | Billboard | Alpha | yingyang_3 / Gradient_maker_simple_round_radial / Tx_Noise_20 | soft ramp dist 0.01 | 4..3 | 0 | 1 | 率 0 爆发[0s x1] | 2x2 | x 0 · y x2 [0.164:0.43 1:1] | #000000DD | 5 |
| Burst/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 4..5 | 2 | 5 | 率 0 爆发[0s x35] | 8x8 | x 0 · y x2 [0.164:0.43 1:1] | #B9B9B95E..#DBDBDB4C | 5 |
| Burst/yy_2_symbol_alpha_soft | Billboard | Alpha | yingyang_2 / Gradient_maker_simple_round_radial / Tx_Noise_12 | soft ramp dist 0.02 | 2.5 | 1.9 | 11.2 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.078:0.434 1:1] | #FFFFFF41 | 0 |
| Burst/Ink_Shockwave_1_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02 / FX_TX_HorizontalGradient_04 / TX_Noise_01 | dist 0.35 | 3 | 2 | 7..8 | 率 0 爆发[0s x2] |  | x 2 · y x2 [0:0.432 1:1] | #000000B7 | 0 |

## Ying Yang Vertical Rune（49 个系统，启用 46）

| 系统 | 渲染 | 混合 | 贴图 | 标志 | 寿命 | 延迟 | 尺寸 | 发射 | 序列帧 | C1 | 起色 | 排序 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Momentum/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 3..4 | 3 | 5 | 率 0 爆发[0s x15] | 8x8 | x 8.03 · y x2 [0:1 0.064:0.608 1:1] | #0000008B | -24 |
| Momentum/FuzzAlpha | Billboard | Alpha | Default-Particle / – / Default-Particle |  | 1 | 3 | 41 | 率 0 爆发[0s x1] |  | x x2.334 [0:1 1:0] · y 0 | #000000FF | -2 |
| Momentum/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 1 | 3 | 14.6 | 率 0 爆发[0s x1] |  | x x4.854 [0:1 1:0] · y 0 | #FFFFFF55 | -2 |
| Momentum/FuzzAlpha | Billboard | Alpha | Default-Particle / – / Default-Particle |  | 3 | 3 | 33.15 | 率 0 爆发[0s x1] |  | x x25 [0:1 0.099:0.18 1:0] · y 0 | #B2B2B2FF | -10000 |
| Momentum/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 0.75 | 3 | 25 | 率 0 爆发[0s x1] |  | x x25 [0:1 0.099:0.18 1:0] · y 0 | #000000FF | 15 |
| Momentum/Explosion_Shockwave_03_Distorted_M | Mesh UV_Distorted_PolarCords | Alpha | Tx_Noise_29 / T_Gradient_Line_V20 / Noise_TilingSoftWebbing | dist 0.2 | 1 | 3 | 15 | 率 0 爆发[0s x1] |  | x x5 [0:0.75 0.023:1 1:0] · y 0 | #000000B2 | 0 |
| Momentum/yy_Shockwave_1_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | dist 2 | 3 | 3 | 7..8 | 率 0 爆发[0s x2] |  | x 2 · y x2 [0:0.432 1:1] | #000000B7 | 0 |
| Momentum/Blood_Dark_Shockwave_05_Distorted_alpha | Mesh UV_Distorted_PolarCords | Alpha | gradient_002_Clamped / Tx_Liquid_01_2 / Noise_TilingSoftWebbing | dist 0.3 | 1 | 3 | 8 | 率 0 爆发[0s x1] |  | x x40 [0:1 1:0.004] · y x2 [0.537:0.398 1:1] | #000000FF | 0 |
| Momentum/yy_Shockwave_3_Distorted_still_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Gradient_Quad03 / Tx_Noise_09 | soft ramp dist 0.3 | 1.5 | 3 | 2 | 率 1 爆发[0s x1] |  | x x10 [0.023:1 1:0.284] · y 1.29 | #000000FF | -254 |
| Momentum/Simple_StarDust_Noise_Desaturated_Add | Billboard | Add | Simple_Star_Dust_Mask / Simple_Star_Dust_Mask / RainbowMirrorNoise |  | 3..2 | 3 | 5..4.88 | 率 0 爆发[0s x15] | 2x2 | x x120 [0:1 1:0] · y x2 [0:0.5 1:1] | #FFFFFFFF | 15 |
| Momentum/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 3..5 | 3 | 3D x0.03 y0.2 z1 | 率 0 爆发[0s x35] | 2x2 | x x1 [0:1 1:0] · y x2 [0:0.584 1:1] | #FFFFFFFF | 15 |
| Momentum/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 1.5..2.5 | 3 | 4 | 率 0 爆发[0s x25] | 8x8 | x x0 [0:1 1:0] · y x2 [0:0.977 0.084:0.435 0.745:0.315 1:0.089] | #FFFFFFFF | 0 |
| Momentum/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 3..4 | 3 | 0.05..0.01 | 率 1 爆发[0s x75] | 2x2 | x x167.767 [0:1 1:0] · y x2 [0:0.5 0.073:1] | #FFFFFFFF | 0 |
| Momentum/yy_Beam_1_Rotated_1_alpha_soft | VerticalBillboard | Alpha | FX_Flare_Ultrawide_Rotated_T / Square_Light_Blurr / sd_noise_202 | soft dist 0.22 | 2 | 3 | 3D x4 y21.21 z1 | 率 1 爆发[0s x1] |  | x x5 [0:1 1:0] · y 1 | #303030FF | 25 |
| Momentum/yy_Beam_1_Rotated_1_alpha_soft | VerticalBillboard | Alpha | FX_Flare_Ultrawide_Rotated_T / Square_Light_Blurr / sd_noise_202 | soft dist 0.22 | 1 | 3 | 3D x2 y21.21 z1 | 率 1 爆发[0s x1] |  | x x25 [0:1 1:0] · y 1.48 | #262626FF | 25 |
| Momentum/yy_Beam_1_Rotated_1_alpha_soft | VerticalBillboard | Alpha | FX_Flare_Ultrawide_Rotated_T / Square_Light_Blurr / sd_noise_202 | soft dist 0.22 | 0.5 | 3 | 3D x1 y21.21 z1 | 率 1 爆发[0s x1] |  | x x75 [0:1 1:0] · y 1.28 | #FFFFFFFF | 25 |
| Momentum/bamboo_leaf_alpha_soft | Billboard | Alpha | bamboo_leaf_1 / – / – |  | 1.5..2 | 3 | 0.09..0.2 | 率 0 爆发[0s x55] | 2x2 | x x2 [0:1 1:0] · y x2 [0:0.5 1:1] | #D7FF61FF..#10590CFF | 0 |
| Momentum/yy_2_symbol_alpha | Billboard | Alpha | yingyang_2 / Gradient_maker_simple_round_radial / Tx_Noise_20 | ramp dist 0.02 | 4..2.5 | 3.1 | 15.02 | 率 0 爆发[0s x1] |  | x x25 [0:1 0.099:0.205 1:0] · y x2 [0:0.509 1:1] | #FFFFFF65 | -500 |
| Momentum/yy_Shockwave_3_Distorted_still_alpha_soft | Mesh Vertical_srike_cilinder | Alpha | Tx_Line_05_Up / Gradient_Quad03 / Tx_Noise_09 | soft ramp dist 0.3 | 1 | 2.8 | 3D x1.8 y2 z1.8 | 率 0 爆发[0s x1] |  | x x25 [0:0.75 0.023:1 1:-0.019] · y x2 [0.537:0.398 1:-0.019] | c0#FFFFFFFF c1#FFFFFFFF a0=1 a1=1 | -5 |
| Momentum/yy_Shockwave_1_alpha_soft | Mesh Vertical_srike_cilinder | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | soft dist 2 | 1.2 | 2.8 | 3D x2 y2 z2 | 率 0 爆发[0s x2] |  | x x25 [0:0.75 0.023:1 1:-0.019] · y x2 [0.537:0.398 1:-0.019] | #070707FF | -5 |
| Momentum/yy_3_symbol_alpha_soft | Billboard | Alpha | yingyang_3 / Gradient_maker_simple_round_radial / Tx_Noise_20 | soft ramp dist 0.01 | 2..3 | 3 | 3D x1.5 y3 z1 | 率 0 爆发[0s x10] | 2x2 | x 0 · y x2 [0.164:0.43 1:1] | #000000DD | 50 |
| Momentum/yy_Shockwave_1_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | soft dist 2 | 1.5 | 3 | 2 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.013:0.459 1:1] | #FFFFFFFF | -5 |
| Momentum/yy_Shockwave_1_alpha_soft | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_02_up_nomipmap / Gradient_maker_double_Fade_tappered / Tx_Noise_05 | soft dist 2 | 1.5 | 3 | 2 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.013:0.459 1:1] | #FFFFFFFF | -5 |
| Momentum/yy_Shockwave_3_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Tx_Mask_01_Clamped / Tx_Noise_09 | ramp dist 0.3 | 7 | 3 | 1.62 | 率 1 爆发[0s x1] |  | x x10 [0.023:1 1:0.284] · y x2 [0:1 0.064:0.53 1:1] | #000000A8 | -254 |
| Momentum/yy_Shockwave_3_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Tx_Mask_01_Clamped / Tx_Noise_09 | ramp dist 0.3 | 7 | 3 | 1.62 | 率 1 爆发[0s x2] |  | x x10 [0.023:1 1:0.284] · y x2 [0:1 0.064:0.53 1:1] | #000000A8 | -254 |
| Momentum/Flare | Billboard | Add | flare1_flare2 / – / – | soft | 1 | 3.2 | 22.46 | 率 0 爆发[0s x1] |  | x x5 [0:1 1:0] · y 1 | #9A9A9AFF | 50 |
| Momentum/Flare | Billboard | Add | flare1_flare2 / – / – | soft | 1 | 3.2 | 22.46 | 率 0 爆发[0s x1] |  | x x5 [0:1 1:0] · y 1 | #9A9A9AFF | 50 |
| Momentum/FuzzAdd | Billboard | Add | Default-Particle / – / Default-Particle |  | 1 | 3 | 15 | 率 0 爆发[0s x3] |  | x x1 [0:1 1:0] · y 1.39 | #FFFFFF3C | 5 |
| Build Up Floor/FuzzAlpha_Soft | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 3 | 0 | 3D x7.18 y18.1 z1 | 率 0 爆发[0s x1] |  | x 1 · y 1 | #000000F2 | 0 |
| Build Up Floor/FuzzAlpha_Soft/FuzzAlpha_Soft_1 | Billboard | Alpha | Default-Particle / – / Default-Particle | soft | 3 | 0 | 3D x7.18 y18.1 z1 | 率 0 爆发[0s x1] |  | x 1 · y 1 | #000000F2 | 0 |
| Build Up Floor/Trail_WispyGeneric_Alpha | Stretch | Alpha | Trail_WispyGeneric / gradient_004 / PackedNoises_Generic | soft | 3 | 0 | 3D x1.8 y6.5 z1 | 率 1 爆发[0s x1] |  | x x80 [0:0 1:1] · y 1.2 | #3C3C3CFF | 20 |
| Build Up Floor/yy_Beam_1_Rotated_1_alpha_soft | Billboard | Alpha | FX_Flare_Ultrawide_Rotated_T / Square_Light_Blurr / sd_noise_202 | soft dist 0.22 | 3 | 0 | 3D x3 y1 z1 | 率 0 爆发[0s x1] |  | x x150 [0:0 1:1] · y 1.35 | #FFFFFFFF | 2 |
| Build Up Floor/yy_Beam_1_Rotated_1_alpha_soft/yy_Beam_1_Rotated_1_alpha_soft_1 | Billboard | Alpha | FX_Flare_Ultrawide_Rotated_T / Square_Light_Blurr / sd_noise_202 | soft dist 0.22 | 3 | 0 | 3D x3 y1 z1 | 率 0 爆发[0s x1] |  | x x150 [0:0 1:1] · y 1.35 | #FFFFFFFF | 2 |
| Build Up Floor/Ink_Vertical_trail_alpha_soft | Billboard | Alpha | Trail_WispyGeneric / gradient_004 / Tx_Noise_04 | soft dist 0.2 | 3 | 0 | 3D x2 y1.5 z1 | 率 0 爆发[0s x1] |  | x x0 [0:1 1:0] · y 1 | #000000FF | 2 |
| Build Up Floor/Ink_Vertical_trail_alpha_soft/Ink_Vertical_trail_alpha_soft_1 | Billboard | Alpha | Trail_WispyGeneric / gradient_004 / Tx_Noise_04 | soft dist 0.2 | 3 | 0 | 3D x2 y1.5 z1 | 率 0 爆发[0s x1] |  | x x0 [0:1 1:0] · y 1 | #000000FF | 2 |
| Build Up Floor/bamboo_leaf_alpha_soft | Billboard | Alpha | bamboo_leaf_1 / – / – |  | 3.5 | 1 | 0.1..0.12 | 率 10 爆发[0s x0] | 2x2 | x x2 [0:1 1:0] · y x2 [0:0.5 1:1] | #D7FF61FF..#10590CFF | 0 |
| Build Up Floor/yy_3_symbol_alpha_soft | Billboard | Alpha | yingyang_3 / Gradient_maker_simple_round_radial / Tx_Noise_20 | soft ramp dist 0.01 | 2..3 | 0 | 3D x1.5 y3 z1 | 率 15 爆发[0s x1] | 2x2 | x 0 · y x2 [0.164:0.43 1:1] | #000000DD | 50 |
| Build Up Floor/Smoke_screen_cloud_sharp_fb_1_alpha_soft | Billboard | Alpha | Smoke_cloud_Sharp_1_render_all / aerial_explosion_3_alphaDemon_explotionv2 / Tx_Noise_20 | soft | 1.5..2 | 0 | 4.5 | 率 15 爆发[0s x0] | 8x8 | x x1 [0:1 1:0] · y x2 [0:1 0.049:0.477 0.749:0.605 1:1] | #111111FF | -5 |
| Build Up Floor/FuzzAlpha | Billboard | Alpha | Default-Particle / – / Default-Particle |  | 3.5 | 0.2 | 20.21 | 率 0 爆发[0s x1] |  | x 1 · y 0 | #000000FF | -500 |
| Build Up Floor/yy_Shockwave_5_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_04_Up / Tx_Mask_01_Clamped / Tx_Noise_05 | ramp dist 1.13 | 3.1 | 0 | 1.22 | 率 1 爆发[0s x1] |  | x x10 [0.023:1 1:0.284] · y x2 [0:1 0.064:0.548] | #FFFFFFFF | 0 |
| Build Up Floor/yy_Shockwave_3_Distorted_still_alpha | Mesh UV_Distorted_PolarCords | Alpha | Tx_Line_05_Up / Tx_Mask_01_Clamped / Tx_Noise_09 | ramp dist 0.3 | 3.1 | 0 | 1.34 | 率 1 爆发[0s x1] |  | x x48.055 [0:0.087 0.903:0.289 1:1] · y x2 [0:1 0.048:0.589 1:0.496] | #070707AA | 0 |
| Build Up Floor/bamboo_yy_3_alpha_soft | VerticalBillboard | Alpha | bamboo_tree_4 / gradient_004 / Tx_Noise_12 | soft dist 0.04 | 3.1 | 0 | 3D x2 y4 z1 | 率 0 爆发[0s x10] | 2x2 | x 0 · y x2 [0:1 1:0.47] | #00000086 | -2 |
| Build Up Floor/yy_2_symbol_alpha | Billboard | Alpha | yingyang_2 / Gradient_maker_simple_round_radial / Tx_Noise_20 | ramp dist 0.02 | 3.2 | 0 | 8 | 率 0 爆发[0s x1] |  | x x1 [0:1 1:0] · y x2 [0:1 0.056:0.648 1:0.513] | #FFFFFF5E | 0 |
| Build Up Floor/Flecks_Shiny_Additive | Stretch | Add | Fleks_Shiny / – / Fleks_Shiny |  | 1..2 | 0 | 0.05..0.01 | 率 50 爆发[0s x0] | 2x2 | x x167.767 [0:1 1:0] · y x2 [0:0.5 0.146:1] | #FFFFFFFF | 0 |
| Build Up Floor/FuzzAlpha | Billboard | Alpha | Default-Particle / – / Default-Particle |  | 5 | 0 | 21.37 | 率 0 爆发[0s x1] |  | x 1 · y 0 | #000000FF | -11 |
| Build Up Floor/FuzzAdd | Billboard | Add | Default-Particle / – / Default-Particle |  | 3 | 0 | 8.41 | 率 0 爆发[0s x1] |  | x x1 [0:0 1:1] · y 0 | #FFFFFF6E | -11 |
