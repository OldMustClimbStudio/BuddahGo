# Rendering showcase

Date: 2026-10-24 · Branch `refactor/vfx-optimization` (renamed `feat/racemap-rendering` on 2026-10-28) · Unity 2022.3.55f1c1 · URP 14.0.11 · DirectX 11, RTX 4070 Laptop

One image per rendering effect from a fixed camera, so the art direction can be compared visually before any of it
goes into `RaceMap`. Open [index.html](index.html) for the gallery with captions, repository links, licenses and
cost notes, or [contact_sheet.jpg](contact_sheet.jpg) for everything on one image. Companion survey:
[../shader-repo-survey.md](../shader-repo-survey.md).

**Status (2026-10-26).** Two results went to production in the RaceMap work
([../racemap/README.md](../racemap/README.md)): jiaozi158's screen-space reflections, now in
`Assets/Plugins/UnitySSReflectionURP/` with the `[BuddahGo]` patches, and `Showcase/Lit`, promoted to
`BuddahGo/Lit` in `Assets/Rendering/Shaders/` (with URP Lit's full input layout, keyword set and a GBuffer pass added).
RaceMap also got a sunset sky of its own instead of the Oasis skybox shown here. On 2026-10-28 planar reflections
(`PlanarReflection`, written for RaceMap, not from the showcase) replaced SSR on the lane and SSR left the renderers;
the plugin stays in `Assets/Plugins/UnitySSReflectionURP/`, which this showcase still uses.

## Repository and local files

Only this folder is committed: this note, the gallery and the images (JPEG copies of the renders). The showcase
project itself, `Assets/Showcase/`, stays local on the machine that built it and is not in the repository. It holds
third-party evaluation code, including a GPL-3.0 demo-only folder, plus runtime assemblies and a `Resources`
compute shader that Unity would compile into every game build. Re-rendering and the paths below therefore need that
local copy. When it was built, no existing material, scene or asset under `Assets/Settings/` was modified.

| Path | Purpose |
|---|---|
| `Assets/Showcase/Showcase.unity` | The set: marble floor (project `Map_subsMarble_graph_0_material`), five pillars + lintel, Oasis rocks and tent, red/white colour-bleed corner, two sphere rows (roughness 0..1, dielectric and gold), the Buddha head with the project's `DefaultSkin` graph material, a shallow pool with the Oasis water material, Oasis grass, a grey prop for the stylized counter-example, one low sun, the Oasis sunset skybox. Six cameras: main, low (reflections), water, sun (volumetrics), spheres (PBR), head (SSS). |
| `Assets/Showcase/Editor/ShowcaseSceneBuilder.cs` | Rebuilds the scene from scratch (`Showcase > Build Scene`). |
| `Assets/Showcase/Editor/ShowcaseSettings.cs` | Creates `Assets/Showcase/Settings/Showcase-URP.asset` + `Showcase-Renderer.asset` as copies of the project's High Fidelity pair (SSAO kept, gameplay features dropped), adds the third-party renderer features by type name, builds the realistic post profile and the (unused) lighting settings (`Showcase > Build Settings`). |
| `Assets/Showcase/Editor/ShowcaseRenderer.cs` | The shot list with captions and the renderer: swaps the Graphics/Quality pipeline asset to the showcase copy inside `BeginBatch`/`EndBatch`, renders each camera to a 1920x1080 RenderTexture, writes `NN_<effect>.png`, restores the project assets. `Showcase > Render All` runs it from the menu. |
| `Assets/Showcase/Editor/ShowcaseEffects.cs` | Per-effect toggles, driven through reflection so the project compiles with or without any third-party package. |
| `Assets/Showcase/Editor/ShowcaseSkinLutBaker.cs` | Bakes `Assets/Showcase/Textures/Showcase_SssLut.png` (pre-integrated scattering LUT). |
| `Assets/Showcase/Editor/render_showcase.py` | Drives the render from outside the editor through Unity MCP; reloads scripts after a shot that breaks URP's render loop and continues. |
| `Assets/Showcase/Editor/build_gallery.py` | From the PNG renders and `captions.json` (which also sets the order), writes a JPEG copy of each render (quality 90), `contact_sheet.jpg` and `index.html` linking the JPEGs. The PNGs are git-ignored. |
| `Assets/Showcase/Shaders/Showcase_Lit.shader` + `ShowcaseLitInput.hlsl`, `ShowcaseLitForwardPass.hlsl`, `ShowcaseLighting.hlsl` | Hand-written URP 14 PBR surface shader (effect 8). |
| `Assets/Showcase/Shaders/Showcase_SkinSSS.shader` | Hand-written pre-integrated subsurface scattering (effect 9). |
| `Assets/Showcase/Shaders/Showcase_Water.shader` | Hand-written shallow water (effect 10). |
| `Assets/Showcase/ThirdParty/<repo>/` | Imported third-party code, each with its LICENSE and README (see below). UnitySSReflectionURP now lives in `Assets/Plugins/UnitySSReflectionURP/`. |
| `Assets/Showcase/Materials/`, `Assets/Showcase/Settings/` | Generated materials, profiles, the probe cubemap and the showcase pipeline copies. |

## Re-rendering

1. Open the project in the editor with the MCP for Unity bridge connected (`Docs/unity-mcp.md`).
2. From the repository root: `python Assets/Showcase/Editor/render_showcase.py` (about 1 minute). `--only 04_ssr_on`
   renders a single shot. The editor menu `Showcase > Render All` does the same over several editor frames but
   cannot recover if a third-party pass throws.
3. `python -I Assets/Showcase/Editor/build_gallery.py` (needs Pillow) to refresh the JPEG copies, `contact_sheet.jpg`
   and `index.html`. Commit those and `captions.json`; the PNG renders stay local.

The pipeline switch needs its own editor frame: URP rebuilds its sky reflection and ambient probe through the
lightmapper after the active asset changes, and rendering in the same frame gave black reflections. `BeginBatch`,
`Render` and `EndBatch` are therefore separate calls. Scripts never save the project's pipeline assets.

## Effects, images and verdicts

| # | Image(s) | Source | License | Verdict |
|---|---|---|---|---|
| 1 | `01_baseline_urp_lit.jpg` | URP 14 Lit, project pipeline asset | Unity | Reference. Trilight ambient makes the shadow side blue-grey and flat; the marble reads as plastic. |
| 2 | `02_lighting_pass.jpg` | RenderSettings + Volume only | Unity | Biggest change per unit of work: sky-coloured ambient, ACES, haze. No shader code, but the sunset skybox tints everything salmon; a neutral HDRI would be the next step. |
| 3 | `03_volumetric_light.jpg` | [CristianQiu/Unity-URP-Volumetric-Light](https://github.com/CristianQiu/Unity-URP-Volumetric-Light) | MIT | Works unmodified on URP 14. Lit air and pillar shadows in the fog are real, but the effect needs a backlit camera to read; from the main camera it is just haze. |
| 4 | `04_ssr_off.jpg`, `04_ssr_on.jpg` | [jiaozi158/UnitySSReflectionURP](https://github.com/jiaozi158/UnitySSReflectionURP) | MIT | Clear win on the marble: pillars, spheres and head reflect, roughness-blurred. Needed a null-check patch (see below). Only reflects what is on screen. |
| 5 | `05_planar_reflection.jpg` | [ColinLeung-NiloCat/UnityURP-MobileScreenSpacePlanarReflection](https://github.com/ColinLeung-NiloCat/UnityURP-MobileScreenSpacePlanarReflection) | MIT | A perfect mirror of the floor plane at mobile cost, but no roughness blur and it replaces the floor material with its own shader. Needed a URP 14 texture-binding patch. |
| 6 | `06_ssgi_off.jpg`, `06_ssgi_on.jpg` | [jiaozi158/UnitySSGIURP](https://github.com/jiaozi158/UnitySSGIURP) | MIT | Runs, and brightens the corner, but the red bounce is faint at this sun angle; the heaviest effect of the set for the smallest visible gain here. |
| 7 | `07_fog_volume.jpg` | [sinnwrig/URP-Fog-Volumes](https://github.com/sinnwrig/URP-Fog-Volumes) at commit `9376530` (2024-12-09) | MIT | Local floor-hugging fog; subtle at this density. Current `main` is Unity 6 only, so the last URP 14 revision was used. |
| 8 | `08_pbr_stock_lit.jpg`, `08_pbr_showcase_lit.jpg` | `Showcase/Lit` (project code) | project | Direct lighting identical to URP Lit by construction; specular occlusion and multi-bounce AO only show in creases and contact areas, so the pair is a near-match. The value is the shader as a foundation, not this image. |
| 9 | `09_skin_default_graph.jpg`, `09_skin_sss.jpg` | `Showcase/SkinSSS` (project code) | project | Softer, warmer terminator on the granite head. The technique needs a curvature scale tuned per mesh; it is cheap (one LUT fetch per channel). |
| 10 | `10_water_shadergraph.jpg`, `10_water_hlsl.jpg` | `Showcase/Water` (project code) vs the project's `Water.shadergraph` | project | The HLSL version has depth absorption, two scrolling normal layers and sun glints in one pass; the Shader Graph version is a calmer mirror with a depth fade. Foam is in the shader but barely visible at this pool depth. |
| 11 | `11_stylized_counterexample.jpg` | [Soung2279/Unity-Shaders-Collection](https://github.com/Soung2279/Unity-Shaders-Collection) BreathHalo | **GPL-3.0, demo only** | A flat additive glow that ignores the scene's light: the opposite of the realistic direction. Delete `Assets/Showcase/ThirdParty/Unity-Shaders-Collection_GPL_DEMO_ONLY/` before any build. |
| 12 | not rendered | [AkiKurisu/IllusionRP](https://github.com/AkiKurisu/IllusionRP) `urp14` | Unity Companion | Skipped: a full render-pipeline add-on, well beyond the one-hour budget. |
| 13 | `13_all_realistic_on.jpg` | all of the above except 5 and 11 | | Everything on together from the main camera. |

## Integration notes and local patches

- **Third-party code lives under `Assets/Showcase/ThirdParty/<repo>/`** (except UnitySSReflectionURP, see below), copied from the repositories' runtime,
  editor and shader folders with their `.meta` files, LICENSE and README. Only `Soung_BreathHalo.shader` was taken
  from the GPL collection.
- **`UnitySSReflectionURP`** (moved to its production home `Assets/Plugins/UnitySSReflectionURP/` on 2026-10-26 for
  RaceMap phase 2, GUIDs kept, so the showcase uses the same copy): `ScreenSpaceReflectionURP.cs`,
  `ForwardGBufferPass.FrameCleanup` dereferenced `gBuffer2` when the renderer's own normals texture is reused; the
  null reference left URP's frame half-finished and every later render in the session failed. Null checks added,
  marked `[BuddahGo]`.
- **`UnityURP-MobileScreenSpacePlanarReflection`** (written for URP 10): the compute pass bound
  `_CameraDepthTexture` / `_CameraOpaqueTexture` by temporary-RT name, which does not exist in URP 14's RTHandle
  world. `BindCameraTextures` now fetches the renderer's `m_DepthTexture` / `m_OpaqueColor` handles by reflection,
  marked `[BuddahGo showcase, URP 14]`.
- **`URP-Fog-Volumes`**: main branch targets Unity 6 (RenderGraph only) and does not compile on URP 14; commit
  `9376530` is used. Its `Hidden/VolumetricFog` shader was renamed `Hidden/Sinnwrig/VolumetricFog` because
  CristianQiu's package looks up a shader of the same name with `Shader.Find`. The feature's `FogVolumeData` asset
  is assigned from `ThirdParty/URP-Fog-Volumes/Runtime/Data/` by `ShowcaseSettings.ConfigureFeature`.
- **`Unity-URP-Volumetric-Light`** and **`UnitySSGIURP`** run unmodified. SSGI's shader reference and
  `m_OverrideAmbientLighting`, and SSR's post material, are assigned by `ShowcaseSettings.ConfigureFeature`
  because their inspectors normally do it.
- **Reference-only repositories** (nothing copied): Cyanilux `URP_ShaderCodeTemplates` (CC0) for pass layout,
  Nuomi-Chobits `Unity-URP-PBR` (no license, read for the specular-occlusion idea only), wingstone
  `PreintegratedSubsurfaceScattering` (technique), Oceana-URP (Apache-2.0, needs 2023.2, read for water shading).

## What did not work and why

- **Baked lightmaps**: the Progressive GPU lightmapper refused every job ("Material render job skipped - out of
  system memory"; commit charge was 48.6 GB of 51.2 GB on the machine during the session). The showcase therefore
  uses realtime lighting plus a per-shot synchronous reflection-probe bake; the probe cubemap is also projected to
  L2 spherical harmonics for the ambient probe and used as the custom sky reflection, so nothing depends on the
  lightmapper. `Assets/Showcase/Settings/Showcase.lighting` and `ShowcaseRenderer.StartBake` remain for a machine
  with memory to spare; the colour-bleed pair (effect 6) then becomes "baked bounce vs screen-space bounce" instead
  of "no bounce vs screen-space bounce".
- **Rendering right after a pipeline switch** gives black reflections (see Re-rendering).
- **Effects 3, 6, 7, 8 and 9 are subtle from the main camera**; 3, 8 and 9 got their own camera angles, 6 and 7
  stay on the main camera so they can be compared with image 2.

## Hand-written shaders

All three follow the layout of `Packages/com.unity.render-pipelines.universal@14/Shaders/Lit.shader`: ShaderLab
properties with the same names as URP Lit, an SRP-Batcher `UnityPerMaterial` CBUFFER, URP's `ShaderLibrary`
includes, `ForwardLit` plus the stock `ShadowCaster`, `DepthOnly`, `DepthNormals` and `Meta` passes, keywords
matching URP's. They compile without warnings on DX11.

- `Showcase/Lit`: direct lighting through URP's `LightingPhysicallyBased`; indirect lighting replaced by
  `ShowcaseGlobalIllumination` (Lagarde specular occlusion, Jimenez multi-bounce diffuse AO, horizon fade). Two
  sliders, `_SpecularOcclusion` and `_MultiBounceAO`, blend back to stock behaviour. Promoted to production as
  `BuddahGo/Lit` (see the status note above); the showcase keeps its own copy.
- `Showcase/SkinSSS`: Penner pre-integrated scattering. The LUT (N.L x curvature) is baked by an editor script
  from a six-Gaussian profile; curvature comes from screen-space derivatives of the normal.
- `Showcase/Water`: depth-based Beer-Lambert absorption of the opaque texture, two whiteout-blended normal layers,
  Schlick Fresnel against the reflection probe, GGX sun specular with shadows, shoreline foam from the depth
  difference.
