# 如来神掌：表面烟雾的技术路线对比（2026-10-29）

用户要求："线条性"——烟是连续的细流线叠成的薄层，像墨入水、细烟柱（参考图见 [palm-mesh-tripo.md 第 12 节](palm-mesh-tripo.md)），不是一片片面片；并让看一眼 Unity 的[六面光照烟雾](https://developer.unity.cn/projects/63e49a20edbc2a3f7df070a9)。本文把能走的路线列出来供选，含参考链接；都基于当前工程（Unity 2022.3.55、URP 14、VFX Graph 14 已装）。

| 路线 | 做法 | 线条性 | 代价 / 风险 | 现状 |
|---|---|---|---|---|
| A. CPU 流线（现有） | Shuriken：粒子不画本体，只画连续 Trails；curl 噪声低频弱强度让线平滑弯曲；从网格表面发射 | 中：每根线有，但 600 根上限，叠不成参考图那种"层"，线宽固定 | 最便宜，已做好，材质 `Mat_FX_Palm_Wisp` | 在 prefab 里（`SurfaceWisps`） |
| B. 预模拟序列帧 | 用真实流体模拟烘的序列帧当烟（只取 alpha 当黑墨，不打光）；Shuriken Texture Sheet 或 VFX Graph Flipbook 播放；几张大面片从手面升起 | 高（取决于素材：选"细烟柱 / 墨入水"类序列帧，线条就是模拟出来的） | 要素材：Unity 随文章给的[六面贴图库](https://drive.google.com/drive/folders/1_oh0UkAOW6hISqouCXjYwKQ0lkoj0CIF)（Lightmap A 的 alpha 就是密度序列帧，可直接当无光照烟用）、[GoVFX 免费烟雾包](https://govfx.itch.io/realistic-smoke-fx-free-pack-v2)（8×8，PNG）、[luzemrik 付费包](https://luzemrik.itch.io/smoke-puff-flipbooks-vol1)（2048²，EXR）；面片在侧视会露扁；序列帧是"播放"不是"生成"，每次一样，要靠随机起帧、翻转、多片错开掩盖 | 未做；需要下载素材（我会先问你再下） |
| B'. 六面光照烟（文章） | 同 B 的序列帧，但用六张方向光照贴图实时打光，烟有体积明暗 | 同 B | **URP 在 2022.3 不支持**：文章时只有 HDRP；[URP 支持 2025-04 才发布](https://legacy-activation.unity3d.com/roadmap/2216-vfx-graph-smoke-lighting-for-urp)，[Unity 6 的 Six Way Shader Graph](https://docs.unity3d.com/Manual/urp/prebuilt-shader-graphs-urp-sixway.html)；社区有 [URP 2022 的受光烟 shader](https://realtimevfx.com/t/unity-lit-smoke-shaders-for-universal-rp-vfxgraph-shuriken/19535) 可参考。而且本项目的烟是黑墨，打光意义不大 | 不建议现在做 |
| C. GPU 2D 流体（墨入水） | [keijiro/StableFluids](https://awesome.ecosyste.ms/projects/github.com%2Fkeijiro%2FStableFluids)（MIT，compute shader，URP）：在一张面向相机的面片上跑 Stable Fluids，每帧把手的剪影（或 SDF 切片）当墨源注入，涡量约束出细丝，墨真的在"水"里扩散、拉丝 | 最高：这就是参考图里墨入水的物理 | 一套 compute（约 300 行）+ 每个实例一份 256² 模拟（6 人同屏 6 份，每份约 0.1–0.2 ms）；2D 面片侧视露馅（可两片正交，或把密度场当体积 shader 的噪声源）；移动时拖尾天然正确（把速度场加上手的运动） | 未做；用户之前点名过 keijiro 的仓库，方向吻合 |
| D. GPU 条带（VFX Graph Strips） | VFX Graph [Particle Strips](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/manual/toc.html) + Turbulence / Vector Force Field / Noise 算子：几千根 GPU 细丝从网格表面（Mesh 采样）升起 | 高：数量够多就叠成层，线宽可随寿命变 | 要用 VFX Graph（README 第 7 节之前决定"VFX Graph 仍不用"，需要用户改决定）；Strips 在 14.0 仍是实验特性；每个实例一份 GPU 模拟 | 未做；工程里旧的 `如来神掌.vfx` 就是 VFX Graph，环境现成 |
| E. 体积内流线 | 现有 `FX/InkVolume` 的噪声改成沿流向拉长的各向异性噪声（`_NoiseStretch` 已有）+ 3D 流场平流 | 中：只影响手内部的雾纹，不解决"从表面冒出去"的烟 | 便宜，和任何路线叠加 | 部分已做 |

## 建议

- 要"墨入水"的线条性且愿意加一套 compute：**C**（最像参考，拖尾最自然）。
- 要快、要稳、要省：**B**（真实模拟素材，一天内能看到效果），配 A 的少量细丝做外飘。
- 想要大量细丝且接受回到 VFX Graph：**D**。
- B' 现阶段做不了（URP 2022.3），除非升 Unity 6。

组合建议：**C 做表面烟（主）+ 现有体积（形体）+ A 少量细丝**；如果 C 的 2D 面片在侧视露馅不可接受，再退到 B。

## 参考链接

- 六面光照原文（中）：https://developer.unity.cn/projects/63e49a20edbc2a3f7df070a9 ；英文博客 https://blog.unity.com/es/technology/realistic-smoke-with-6-way-lighting-in-vfx-graph ；论坛 https://discussions.unity.com/t/vfx-graph-introducing-six-way-lighting-for-smoke-dust-and-clouds-vfx/906564 ；Skull & Bones 的技术来源 https://realtimevfx.com/t/smoke-lighting-and-texture-re-usability-in-skull-bones/5339
- Stable Fluids：keijiro 仓库（见上）；入门文 https://80.lv/articles/intro-to-real-time-fluid-simulation-with-compute-shaders-in-unity/ ；优化文 https://80.lv/articles/coding-optimizing-a-real-time-fluid-simulation-in-unity
- 涡丝烟雾（学术）：https://hal-univ-tlse3.archives-ouvertes.fr/INRIA/inria-00402131v1 ；Houdini filament particles https://www.sidefx.com/docs/houdini/dopparticles/filaments.html
- Unity 高级 VFX 电子书（Strips + Noise 算子）：https://unity.com/blog/games/create-spellbinding-visual-effects-with-advanced-vfx-guide
