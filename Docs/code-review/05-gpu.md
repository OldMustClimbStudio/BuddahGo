# 05 · 运行时 GPU 开销实测

2026-11-03 在 `.worktree/vfx-refactor`（HEAD `aafad1b7`）的编辑器里实测。比赛用编辑器单人赛：`AITuningHarness`，1 名玩家加 2 个 AI，RaceMap，由 `Tools/vfxperf/racestart.py` 开局。

## 0. 结论

- **画面本身很便宜。** RaceMap 静态画面在 1920×1080、High Fidelity 档下，GPU 每帧约 **2.3 ms**。平面反射、SSAO、后处理、阴影、MSAA 全部关掉，也只降到 2.2 ms。这些功能各自对每帧 GPU 时间的影响都小于 0.15 ms，在测量噪声以内。
- **显卡占用主要由帧率决定。** 构建版默认开 VSync（`Graphics.xlsx` 的 vsync 档在 low/medium/high 三个预设里都是 `on`），帧率等于屏幕刷新率。本机屏幕 144 Hz，静态画面下 GPU 大约忙 2.3 ms × 144 ≈ 33%；240 Hz 的屏幕会到约 55%。关掉 VSync 时，上限是 `frame_rate_cap`（默认 120）。
- **唯一的大头是如来神掌。** 6 掌齐发时，GPU 中位数从约 2.2 ms 涨到 **5.0 ms**（反噬 6.4 ms），p95 是 **9.9 ms**（反噬 13.1 ms），SetPass 从约 70 涨到约 200。其余技能最多加 0.8 ms（神足通），大多在噪声以内。
- 编辑器里的瓶颈是 CPU：帧时间约 13 ms，GPU 只有 2–3 ms。下面的数字只能做相对比较，不是构建版的绝对值。

## 1. 环境与方法

| 项 | 值 |
|---|---|
| 显卡 / CPU | NVIDIA GeForce RTX 4070 Laptop GPU（8 GB）/ Intel Core Ultra 9 185H |
| 屏幕 | 1920×1080，143.9 Hz |
| 管线 | `URP-HighFidelity`：MSAA 8×、HDR、渲染比例 1；主光阴影 4096、4 级联、220 m；渲染器特性 RenderBuddah、SSAO、RenderSkillVfx、RenderMapEdge、BlackCurtain、RenderTrails；地面有两处 `PlanarReflection` |
| 相机 | Main Camera：后处理开、FXAA、阴影开 |
| GPU 时间 | 每帧调用 `FrameTimingManager.CaptureFrameTimings` + `GetLatestTimings`，读 `gpuFrameTime`。编辑器里不开 Frame Timing Stats 也能读到 |
| 绘制统计 | `ProfilerRecorder`：Render 类别的 `Draw Calls Count`、`SetPass Calls Count`、`Triangles Count` |

脚本在仓库外的共享笔记目录 `code-review-scripts/mcp/`：

- `knobs2.cs`：每 2 秒切换一次"基线 / 关掉某项"，每项来回 3 次，每段丢掉前 0.6 秒。
- `skillgpu.cs`：8 个技能依次各测 9 秒。前 2 秒是基线，之后每 0.2 秒放一次、共 6 次，直接调 `ExecuteServer` + `ExecuteObservers`；如来神掌另外反射调 `TryPushServer`。
- 原始输出：`knobs1.out`、`knobs2.out`、`skill1.out`。

只改比赛中场景里的对象：相机数据、`PlanarReflection.enabled`。渲染器特性用 `ScriptableRendererFeature.SetActive` 在内存里切换，测完恢复，没有保存任何资产。

## 2. 渲染功能开关（交替对照，单位 ms）

| 关掉的项 | 对应基线的 GPU 中位 | 关掉后的 GPU 中位 | 差 | Draw | SetPass | 三角形 |
|---|---:|---:|---:|---|---|---|
| 平面反射（两处） | 2.46 | 2.34 | −0.12 | 111 → 92 | 61 → 52 | 3.56 M → 2.72 M |
| SSAO | 2.35 | 2.34 | −0.01 | 111 → 98 | 60 → 51 | 3.59 M → 2.91 M |
| 后处理 | 2.32 | 2.37 | +0.05 | 111 → 110 | 60 → 59 | — |
| 主光阴影 | 2.35 | 2.26 | −0.09 | 113 → 93 | 62 → 45 | 3.71 M → 2.82 M |
| MSAA | 2.49 | 2.36 | −0.13 | 不变 | 不变 | — |
| 以上全关 | 2.35 | 2.22 | −0.13 | 99 → 54 | 61 → 25 | 2.70 M → 0.99 M |

第一轮是 5 秒一档按顺序测的（`knobs1.out`），结论相同。那一轮做到一半比赛结束、回到了主菜单，最后三组作废。

**怎么读**：在这张卡上，这些功能的开销都在噪声里。不过三角形和 SetPass 的变化说明，平面反射、阴影、SSAO 各自把场景多画了一遍：反射相机约 0.85 M 三角形，阴影投射约 0.9 M，SSAO 的深度法线预渲染约 0.7 M。在低端显卡、更高分辨率或高刷新率下，这些开销才会显出来。

## 3. 技能特效（每个技能连放 6 次，单位 ms）

`pre` 是施放前 2 秒，`on` 是施放后 0–3.5 秒，`tail` 是施放后 3.5–7 秒。

| 技能 | pre GPU 中位 | on GPU 中位 | on p95 | tail 中位 | on 时 SetPass | 结论 |
|---|---:|---:|---:|---:|---:|---|
| push_projectile_hands_anti | 2.23 | **6.43** | **13.06** | 4.05 | 230 | 最大 |
| push_projectile_hands | 3.31* | **5.01** | **9.88** | 3.80 | 198 | 第二 |
| acceleration | 2.09 | 2.87 | 4.80 | 2.97 | 87 | +0.8 |
| giant | 2.10 | 2.38 | 4.15 | 2.46 | 63 | +0.3 |
| reverseturn | 2.37 | 2.34 | 4.06 | 3.18 | 60 | 噪声内 |
| blackcurtain | 2.74 | 2.66 | 4.49 | 2.51 | 80 | 噪声内 |
| slowtrap | 2.48 | 2.48 | 4.30 | 2.27 | 75 | 噪声内 |
| slowtrap_anti | 2.47 | 2.43 | 4.06 | 2.37 | 53 | 噪声内 |

\* 掌法的 pre 段里还带着上一个技能的尾巴。

如来神掌的开销来自墨手手体 `FX_InkVolume` 的光线步进和 `InkFluidVolume` 流体，和 `Docs/vfx/push-projectile-hands.md` §15.1 的结论一致。

## 4. 按开销从大到小，以及不降低表现力的做法

| # | 开销来源 | 量级 | 不降低表现力的做法 | 会改变观感、要你决定的做法 |
|---|---|---|---|---|
| 1 | **帧率**（每秒 GPU 时间 = 帧率 × 每帧时间） | 静态画面：144 Hz 约占 33%，240 Hz 约占 55% | ① 开 VSync 时也遵守 `frame_rate_cap`。现在只有关 VSync 才生效（`Rendering/Settings/GraphicsSettingsApplier.cs:97-103`）。默认值 120 = 2 × tick，和 `FrameRateLock` 的初衷一致，只是在 144/240 Hz 屏上少画几帧。② 窗口失焦或最小化时降到 30 fps。③ 编辑器 Play 时 `targetFrameRate` 实测是 500；改成和构建版一样用 120，开发时显卡就不会满载 | 默认帧率上限改成 60 |
| 2 | **如来神掌墨手**（`FX_InkVolume` + `InkFluidVolume`） | 6 掌时每帧 +3–4 ms，p95 +8–11 ms | 手体 shader 按用户 2026-10-31 的决定不改。不改观感的做法：① 墨手在相机视锥外时跳过流体 Dispatch。`InkFluidVolume.PickSimulating` 现在按离相机的远近分预算，视锥外的也照样参与。② 没有墨手时释放流体状态池的显存（02 B §3.1，本次已修，见 README） | 墨体积降到半分辨率再合成；`simulationBudget` 从 8 降到 4 |
| 3 | **平面反射** | 每处多画一遍场景：约 +0.85 M 三角形、+9 SetPass；这张卡上 GPU 只多约 0.1 ms | 反射相机改用更窄的 culling mask，只画会出现在反射里的层；反射 RT 的分辨率已经由画质档的 `planarScale` 控制 | `high` 档的 `planarScale` 从 1 降到 0.5 |
| 4 | **主光阴影**（4096、4 级联、220 m） | +0.9 M 三角形、+17 SetPass | 远处级联的投射物做 LOD（RaceMap 没有 LODGroup）；阴影距离以外的装饰物关掉 `castShadows` | 4 级联降到 3，或阴影图降到 2048 |
| 5 | **SSAO** | +0.7 M 三角形（深度法线预渲染）、+9 SetPass | — | `high` 档改成只用深度，省掉多画的那一遍；或者改成半分辨率 |
| 6 | **MSAA 8× + FXAA** | 这张卡上 −0.13 ms | — | 用户 2026-10-28 定的是 MSAA8 + FXAA（高速时闪烁减半），不建议动 |
| 7 | **神足通释放** | +0.8 ms | 粒子数在已登记的插槽上限以内，不用改 | — |

这次没有改任何会影响观感的设置。第 1 行的 ①②③ 也会改变帧节奏，要你点头再做，所以只列出来。

## 5. 没做的部分

- 构建版没有实测。编辑器的 CPU 瓶颈会让帧时间失真，但 GPU 时间是可信的。
- 没有用 Frame Debugger 逐 pass 截帧：编辑器里每项的 GPU 差值都在 0.15 ms 以内，再细分意义不大。
- 6 人联机、24 掌的最坏情况已经在 push-projectile-hands.md §15.1 测过，这次没有重测。
