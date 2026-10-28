# Scene rendering: current state and shader reference survey

Date: 2026-10-24 · Branch: `refactor/vfx-optimization` (renamed `feat/racemap-rendering` on 2026-10-28) · Unity 2022.3.55f1c1 · URP 14 · Forward, High Fidelity quality

Scope: first step of the scene-rendering unification work. No rendering change was made; this note records what the
project rendered with on 2026-10-24 and which open-source URP shader repositories are worth reading when moving to hand-written
HLSL. Repo facts (stars, dates, licenses) are a snapshot taken on 2026-10-24.

**Status (2026-10-26).** Section 1 describes the project before the RaceMap rendering work. Of the suggested order in
section 4, steps 1 and 2 are done for RaceMap and step 3 in part: lighting and environment with a hand-written sunset
sky, `BuddahGo/Lit` for the marble and sand, and jiaozi158's SSR in production (until 2026-10-28, when planar reflections replaced it and it was removed). Volumetric light, SSGI, fog volumes and
planar reflection were only evaluated in the local showcase ([showcase/README.md](showcase/README.md)). Steps 4 and 5
are open. Details: [racemap/README.md](racemap/README.md).

## 1. What the project rendered with on 2026-10-24

Pipeline: `Assets/Settings/URP-HighFidelity.asset` is the active quality asset (HDR, MSAA 8x, 4096 main shadow map,
4 cascades, 150 m shadow distance, depth + opaque texture on, reflection probe blending and box projection on).
Renderer `URP-HighFidelity-Renderer.asset` is Forward and carries SSAO plus four project features (RenderBuddah,
BlackCurtain fullscreen pass, RenderMapEdge, RenderTrails). Post volumes: MainMenu uses `SampleSceneProfile.asset`
(Bloom, Vignette, White Balance, Tonemapping, Color Adjustments); RaceMap used the Oasis sample's
`Oasis_Outdoor_Volume_Profile` (priority 1) and its own `Volume_Dark Profile` (priority 0).

Scenes in the build: MainMenu, PropertySelection, RaceMap. The other scene folders (Oasis 452 MB, Terminal, Garden,
Cockpit) are the Unity URP 3D Sample environments imported wholesale. RaceMap used a few Oasis assets: the sunset
skybox material, the `Oasis_Outdoor_Volume_Profile` post profile (with its height fog), `Oasis_LightingSettings.lighting`
and the `OasisSun_Flare` lens flare on `MainLight` (the flare is still in use).

RaceMap materials, traced through the scene, its prefabs and the FBX material remaps:

| Shader | Count | Where |
|---|---|---|
| URP `Lit` | 8 | 3 Substance-generated map materials (marble x2, desert sand) + 3 debug materials (White, Gray18, Metal) + URP's default `Lit.mat` (the torii arch and the plane below the map) + the FBX pillar material `材质.004` |
| Piloto Studio `UberFXSG` (Shader Graph) | 5 | slow-trap VFX zone prefab |
| Project Shader Graph `SG_MapEdge` | 1 | the edge ribbons (layer TrackEdge, shown under the black curtain) |
| Oasis sample Shader Graph `HeightFog` | 1 | the `Lighting/Post_Effects/Oasis Fog` fullscreen pass |
| Built-in skybox shader | 1 | `OasisSunsetSkybox_Mat` from the URP sample |
| Built-in sprite/legacy | 1 | `Glitch_.mat` under Animation/CG |

Lighting: 3 directional lights: `MainLight`, the shadowless fill light (the scene sun, see [racemap/README.md](racemap/README.md))
and `日光`, which the FBX instance `Map/地图场景_3.9` imports and the scene overrides to intensity 0; baked lightmaps on
(large lightmap set under `Assets/Scenes/RaceMap/`), realtime GI off, built-in fog off but the Oasis height-fog pass on
(orange, density 0.0012), ambient mode trilight (not skybox), reflection intensity 0.5.

Shader assets owned by the project (not plugins): 1 hand-written shader (`SharedAssets/Shaders/HLSL/StencilOnly.shader`,
a stencil-write pass) and about 35 Shader Graphs, of which only `DefaultSkin.shadergraph` (character skin, Lit + alpha
clip) and the VFX graphs (BlackCurtain fullscreen, MapEdge, Trail, Acceleration, Deceleration, MagicCircle, Glow) are the
project's own. The rest are URP sample graphs (Oasis terrain/vegetation/layered lit/tent/decals/height fog, Terminal
holograms) and the two marble graphs under `Assets/Sources/地图/MapSubstances/`.

Reading: the gameplay map is already a plain PBR scene (URP Lit + baked GI + SSAO). The style conflict comes from the
layers on top of it, not from the surface shader: a desert-sunset skybox from a sample scene, Substance marble with no
scene-specific detail, stylized additive VFX (Piloto UberFX, Feel toon/matcap shaders in `Plugins/Feel`), and a flat
trilight ambient that does not match the skybox. Unifying the look therefore starts with lighting and environment
(sky, ambient source, fog, exposure/tonemap) and a small set of project-owned surface shaders, and only then with
effect shaders.

## 2. The repo the user found: Soung2279/Unity-Shaders-Collection

82 stars, GPL-3.0, active (last push 2026-10-17), URP 13+ (14 recommended), Unity 2022+. It is a particle/VFX shader
collection for stylized effects: V2 has one uber effect shader (`Soung_URP_AllEffect`) with a custom ShaderGUI,
diamond, text cut-off, breath halo, flipbook, metaball, outline noise, Spine and skybox shaders, screen post effects
(distortion, chromatic aberration, vignette, flash) built on a GrabPass render feature, and heat-distortion /
depth-height-fog render features. V1 (archived) holds Built-in shaders, Genshin/PrimoToon character shaders and a few
Shader Graphs. Some shaders were generated by Amplify Shader Editor.

Fit for BuddahGo: useful as a reference for VFX/UI effect shaders later (skill VFX, hit flashes, screen distortion),
not for the scene-surface and lighting work. Two cautions: GPL-3.0 is incompatible with shipping a closed Steam build
if code is copied, so read for technique and rewrite; and its look is stylized/toon, the opposite of the realistic
direction.

## 3. Realistic-leaning URP repositories (hand-written HLSL first)

### 3a. Foundations for writing URP 14 lit shaders by hand

| Repo | Stars | License | Last push | What it gives |
|---|---|---|---|---|
| [Cyanilux/URP_ShaderCodeTemplates](https://github.com/Cyanilux/URP_ShaderCodeTemplates) | 322 | CC0 | 2023-09 | Unlit / SimpleLit / PBR Lit templates with the full pass set, plus `PBRInput.hlsl` and `PBRSurface.hlsl`. Written for URP 10; pass names and DepthNormals need the URP 14 forms. Best starting skeleton. |
| [phi-lira/UniversalShaderExamples](https://github.com/phi-lira/UniversalShaderExamples) | 838 | none stated | 2020-12 | By a former URP lead. Example 51 (Lit Physically Based) and 52 (Clear Coat) show how to call URP's BRDF and GI from your own shader. Old URP, read for structure. |
| [AnCG7/URPShaderCodeSample](https://github.com/AnCG7/URPShaderCodeSample) | 219 | none stated | 2025-06 | Same Unity version as this project (2022.3.55f1c1, URP 14.0.11). Pure code samples: Lambert through PBR, parallax with self-shadow, lightmaps, probes, reflection probes, shadows, multi-light, plus NPR and decals. Closest version match, Chinese comments. |
| [Nuomi-Chobits/Unity-URP-PBR](https://github.com/Nuomi-Chobits/Unity-URP-PBR) | 46 | none | 2023-08 | Custom PBR vs URP Lit comparison, SH + IBL ambient, specular occlusion from AO with multi-bounce, skin SSS via curvature LUT and bent normals. Good for a project `Lit` with better ambient than stock. |
| [FaithTong/URP-Build-In-Shader](https://github.com/FaithTong/URP-Build-In-Shader) | 38 | none | 2021-07 | Companion to a Chinese book dissecting URP's built-in Lit. Annotated reading of `Lighting.hlsl`, `BRDF.hlsl`, `GlobalIllumination.hlsl`. |
| [ColinLeung-NiloCat/UnityURPToonLitShaderExample](https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample) | 7827 | MIT | 2026-07 | Toon, but the cleanest example of a maintained multi-pass URP lit shader with keywords, shadow/depth/meta passes and a ShaderGUI. Read for file organisation. |

Unity's own `Packages/com.unity.render-pipelines.universal@14/ShaderLibrary` and `Shaders/Lit.shader` + `LitForwardPass.hlsl`
remain the ground truth; every repo above is a wrapper around them.

### 3b. Environment surfaces (terrain, water, foliage)

| Repo | Stars | License | Last push | What it gives |
|---|---|---|---|---|
| [walterpalladino/urp-shaders](https://github.com/walterpalladino/urp-shaders) | 28 | GPL-3.0 | 2025-06 | Triplanar terrain, terrain blending, snow cover, planar reflections, vegetation wind, textured water, sky dome with day/night. Mixed stylized/realistic. GPL: read only. |
| [ZloyKorovanovich/Oceana-URP](https://github.com/ZloyKorovanovich/Oceana-URP) | 81 | Apache-2.0 | 2025-04 | Realistic ocean: SSS, cascades, foam, underwater fog, god rays, compute-based mesh and buoyancy. Needs Unity 2023.2+ and Deferred for the underwater post; the surface shading is still readable as reference for a `Water.shadergraph` replacement. |
| [keijiro/StandardTriplanar](https://github.com/keijiro/StandardTriplanar) | - | Unlicense | old | Built-in, but the shortest correct triplanar normal blending; port the maths. |
| [Delt06/unity-graphics](https://github.com/Delt06/unity-graphics) | 74 | MIT | 2022-07 | URP 10 grab bag: billboard/geometry grass, blob shadows feature, fog skybox, parallax ice. Mostly stylized. |

No open repo combines realistic terrain + foliage + SSS in HLSL for URP 14; that part will be project code.

### 3c. Lighting, GI and atmosphere as renderer features (the biggest lever for "realistic")

| Repo | Stars | License | Last push | What it gives |
|---|---|---|---|---|
| [CristianQiu/Unity-URP-Volumetric-Light](https://github.com/CristianQiu/Unity-URP-Volumetric-Light) | 928 | MIT | 2026-10 | Volumetric lighting for main and additional lights with shadows and cookies, 2022.3+ and Unity 6, Forward/Forward+/Deferred. Directly usable. |
| [sinnwrig/URP-Fog-Volumes](https://github.com/sinnwrig/URP-Fog-Volumes) | 245 | MIT | 2026-01 | Raymarched local fog volumes, half/quarter res with depth-aware upsample, artistic rather than physical. |
| [jiaozi158/UnitySSGIURP](https://github.com/jiaozi158/UnitySSGIURP) | 458 | MIT | 2025-06 | Screen-space GI as a renderer feature, Unity 2022.3.35+ / URP 14, volume-driven. Early release, 20 open issues. |
| [jiaozi158/UnitySSReflectionURP](https://github.com/jiaozi158/UnitySSReflectionURP) | 217 | MIT | 2023-09 | Screen-space reflections for URP 2022. Pairs with the marble floor in RaceMap. |
| [ColinLeung-NiloCat/UnityURP-MobileScreenSpacePlanarReflection](https://github.com/ColinLeung-NiloCat/UnityURP-MobileScreenSpacePlanarReflection) | 1199 | MIT | 2022-11 | Cheap planar reflection feature; a good fit for a single large marble plane. |
| [AkiKurisu/IllusionRP](https://github.com/AkiKurisu/IllusionRP) | 331 | Unity Companion | 2026-10 | High-end URP add-on: screen-space SSS, dual-lobe skin, hair, OIT, GTAO, PCSS, contact shadows, SSR, SSGI/PRTGI/RTGI, ACES. `urp14` branch supports 2022.3.62. Forward/Forward+ only, DX12 for RT. The most complete realistic reference, but heavy. |
| [af8a2a/VividRP](https://github.com/af8a2a/VividRP) | 39 | unstated | 2026-10 | A separate SRP on Unity 7 alpha with forked CoreRP. Not usable here; read only for OpenPBR material ideas. |

### 3d. Character skin (for a `DefaultSkin.shadergraph` replacement)

- [wingstone/PreintegratedSubsurfaceScattering](https://github.com/wingstone/PreintegratedSubsurfaceScattering): pre-integrated SSS with curvature and LUT bakers, Unity 2019 Built-in; the technique port is small.
- Nuomi-Chobits Skin example (above) is already URP.
- IllusionRP's screen-space SSS is the high-end option if character close-ups matter.
- DarkBlade909/DarkBladeSkinShader depends on z3y Graphlit (node tool); skip.

### Aggregators

- [Gforcex/OpenGraphic](https://github.com/Gforcex/OpenGraphic) (2290 stars) and [crazyshader/GameDev](https://github.com/crazyshader/gamedev) (2004 stars) index Unity rendering repos, including the SSR/SSGI and GPU-driven terrain projects above.

## 4. Suggested order for the realistic direction

1. Lighting and environment first, with stock Lit: project skybox (HDRI or procedural) matched to the sun, ambient
   from skybox instead of trilight, physically plausible exposure + ACES, height/distance fog, re-bake lightmaps.
   This alone removes most of the style mismatch and costs no shader code.
2. One project-owned `BuddahGo/Lit` written in HLSL on top of URP's `LitForwardPass`, with the additions the stock
   shader lacks: specular occlusion from AO, optional detail/triplanar, vertex wind flag. Templates: Cyanilux + AnCG7.
3. Renderer features from the MIT list: volumetric light, SSR or planar reflection for the marble, optionally SSGI.
4. Replace the sample-scene graphs that are actually used (water, skin) with HLSL versions; retire unused Oasis /
   Terminal / Garden / Cockpit content from the project to cut about 470 MB.
5. VFX and UI effect shaders last; the Soung2279 collection and Feel's shaders are the references there.

Open questions for the user: target hardware floor (decides SSGI/volumetrics), whether the marble map stays as the
primary arena (decides reflections priority), and whether Oasis/Terminal sample content can be removed.
