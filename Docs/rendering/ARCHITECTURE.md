# Rendering architecture

Current state of BuddahGo's rendering as of 2026-11-05 (branch `feat/racemap-rendering-update`, PR
[OldMustClimbStudio/BuddahGo#73](https://github.com/OldMustClimbStudio/BuddahGo/pull/73)). Unity 2022.3.55f1c1, URP
14.0.11, Forward, linear colour space.
This file says **what exists, why, where it lives and what depends on it**. How each piece was found and tuned
(phases, measurements, reviews) stays in [racemap/history.md](racemap/history.md) and
[racemap/dusk-color-study.md](racemap/dusk-color-study.md). Agents start from [HARNESS.md](HARNESS.md).

Each module below has the same fields:
**Need** (the requirement and the decision behind it), **What**, **Where** (assets, code), **Truth** (the single place a
value is decided; everything else copies or reads it), **Adjust** (the supported knobs), **Depends on**, **Remove it and**
(what breaks).

## Requirements and user decisions

The rendering exists to serve these. A change that contradicts one needs the user, not just a better image.

| # | Requirement or decision | Date | Serves |
|---|---|---|---|
| R1 | RaceMap reads as **dusk** with colour layers: warm only in the sun, cool sky and shadows, effects and Buddha clearly readable, not a full-screen orange cast. The user chose candidate **C2** of the dusk study | 2026-11-03 | sun, sky, fog, post, bake |
| R2 | The **menu environment follows the race's dusk look** (shared sky, post profile, lighting settings, fog, ambient; own sun) | 2026-11-03 | menu |
| R3 | **Reflection probes are baked and the map is baked** ("make reflection probes and bake"). Supersedes "no bake until the map is finished" (2026-10-27) | 2026-11-03 | bake |
| R4 | **Planar reflections replace SSR** on the flat lane and end-field floor; SSR and its Deferred keep-alive renderer are removed (review M6) | 2026-10-28 | planar, pipelines |
| R5 | **High Fidelity defaults to MSAA 8x + FXAA**; SMAA is gone (it crawls on the sand ripples); TAA only without MSAA | 2026-10-28 | AA, settings |
| R6 | **VSync on by default** (the play test showed tearing); the frame cap applies only with VSync off | 2026-10-28 | settings |
| R7 | **Buddha's floating is fixed in the collider only**, never by moving the model or the clips (effects are tuned against the animated model) | 2026-10-27 | not rendering code, but a capture or lighting change must not "fix" it visually |
| R8 | **Feel shakers keep working**: Feel's URP shakers on RaceMap's volumes (`MMWhiteBalanceShaker_URP`, `MMBloomShaker_URP`, ColorAdjustments) take their component from the profile in Awake without a null check, so WhiteBalance, Bloom and ColorAdjustments stay in the profiles (`RenderingModulesTests.PostVolumesHoldWhatTheirFeelShakersDrive`) | phase 1 | post |
| R9 | The **black curtain** keeps tonemapping and grade: `Volume_Dark` follows the gameplay profile, 0.1 EV darker | phase 1 | post, curtain |
| R10 | **Rendering is modular and the settings panel really changes quality**; a setting that needs a restart is tagged "需要重启" like most games (checked in play mode on 2026-11-05: every option applies live, so none is tagged today). **Gameplay passes are on every quality level**: the 一念光明 curtain, racers, edges, trails and skill VFX must look the same in meaning on low, medium and high | 2026-11-05 | renderers, settings, VFX |
| R11 | **A collaborator's bake matches ours**: one scripted bake, recorded inputs, committed outputs | 2026-11-05 | bake |
| R12 | **Every bake output is in Git LFS**, `LightingData.asset` included (it was a plain-Git binary of about 0.9 MB that changes on every bake); `.gitattributes` routes every `LightingData.asset` there | 2026-11-05 | bake |

Where to record a new decision: add a row here (date, what, which modules), then the module's **Need** line.

## The frame, in order

What one gameplay frame does, on every quality level unless marked (since c7124d10, 2026-11-05):

1. Shadow map for `MainLight` (the only realtime shadow caster light; off on Performant).
2. `PlanarReflection` mirror cameras, one per base game camera that sees a receiver from above, with
   `URP-PlanarReflection-Renderer` (no features, no shadows, no post) into an HDR mip-mapped texture. The curtain is
   per camera, so a mirror never draws it.
3. Opaque pass: `BuddahGo/Lit` (lane and end-field floor sample the planar texture), `ZenGround`, URP Lit, the player's
   `SilverSkin` (reflects `Reflection Probe Lower` / `EndField`). SSAO (High Fidelity and Balanced only).
4. Skybox (`BuddahGo/Skybox/Sunset Sky`), exp2 fog applied per shader.
5. Transparent pass for scenery. Every default renderer's transparent mask excludes the gameplay layers `Buddah` (6),
   `BlackCurtainTrail` (7) and `TrackEdge` (8) (mask 4294966847).
6. At AfterRenderingTransparents (500), the gameplay passes in this order: `BlackCurtain` (covers the whole map,
   transparent scenery included, only while 一念光明 plays), `RenderBuddah` (layer 6 opaque racers), `RenderMapEdge`
   (layer 8, transparent queue), `RenderTrails` (layer 7, transparent queue), `RenderSkillVfx` (layer 6 transparent,
   the skill VFX slots). Who sees what is `BlackCurtainViewController`'s job: the caster sees edges and racers, a
   victim sees the red trails and itself, the anti skill swaps them.
7. Post (one global stack): Stop NaN (RaceMap `Main Camera`, zeroes NaN / Inf pixels so Bloom cannot spread one across
   the screen; [../vfx/slots.md](../vfx/slots.md) 1.2), Bloom, WhiteBalance, ColorAdjustments, ACES tonemapping (LDR
   grading mode), then FXAA / TAA from `CameraAntialiasingPolicy`. Everything above, effects included, is graded.

## Modules

### 1. URP pipelines and renderers

- **Need**: three quality levels the settings presets map onto; the gameplay passes on every level (R10); the planar
  mirror needs a feature-less renderer (R4).
- **What / Where** (`Assets/Settings/`, `ProjectSettings/QualitySettings.asset`):

  | Quality level | Pipeline asset | Renderer index 0 | Cosmetic features | Gameplay features | HDR | MSAA | Shadows |
  |---|---|---|---|---|---|---|---|
  | Performant | `URP-Performant` | `URP-Performant-Renderer` | none | the five below | off | 1 | off |
  | Balanced | `URP-Balanced` | `URP-Balanced-Renderer` | SSAO | the five below | on | 1 | 150 m, 2 cascades |
  | High Fidelity (Standalone default) | `URP-HighFidelity` | `URP-HighFidelity-Renderer` (the reference) | SSAO | the five below | on | 8 | 220 m, 4 cascades at 10/30/70 m |

  Gameplay features, after the cosmetic ones and in this order: `BlackCurtain`, `RenderBuddah`, `RenderMapEdge`,
  `RenderTrails`, `RenderSkillVfx`, all at AfterRenderingTransparents (frame steps 5-6). Only SSAO differs per level.
  Index 1 of **every** pipeline's renderer list is `URP-PlanarReflection-Renderer`. All grade in LDR mode.
  `TryUltra URP*.asset` and `MapLight.lighting` in the same folder are referenced by no scene ([retired.md](retired.md)).
- **Truth**: the contract is code, `Assets/Scripts/Rendering/RenderingModules.cs` (`BuddahGo.Rendering.RenderingModules`:
  the gameplay feature names and order, the gameplay layers excluded from the transparent pass), checked by
  `RenderingModulesTests` and by a startup warning when a level breaks it. The feature settings themselves live on
  `URP-HighFidelity-Renderer`; `Assets/Editor/Rendering/RendererGameplayPasses.cs`
  (`Tools/BuddahGo/Rendering/Sync Gameplay Passes`) copies them to the other two renderers. The settings layer never
  writes the assets; it uses runtime clones (module 11).
- **Adjust**: edit a gameplay feature on `URP-HighFidelity-Renderer`, then run Sync Gameplay Passes. Cosmetic knobs go
  through `Graphics.xlsx` (module 11) or the asset, re-checking `EachPresetDefaultReproducesItsQualityLevel`.
- **Depends on**: renderer features' scripts (`BlackCurtainRenderFeature`, URP `RenderObjects`, SSAO); the layers
  `Buddah` (6), `BlackCurtainTrail` (7), `TrackEdge` (8).
- **Remove it and**: removing a gameplay feature from one level makes that level's players miss gameplay information
  (before 2026-11-05 a 一念光明 victim on low or medium saw no curtain); putting layers 6-8 back into the transparent
  pass draws them twice and lets them show under the curtain. Removing or reordering index 1 breaks planar reflections
  (selected by name, but tests and docs assume index 1). Cost of the gameplay passes on Balanced and Performant: within
  the bench's 0.5 ms noise; the curtain 0.3-0.7 ms while it plays ([graphics-settings.md](graphics-settings.md#modules)).

### 2. Lighting and bake

- **Need**: R1, R3, R11. Probe-lit objects (lane, players, props) read baked light probes; glossy surfaces read baked
  reflection cubemaps.
- **What**:
  - Sun `Lighting/MainLight`: Mixed, soft shadows, rotation (19, 66, 0), filter (1, 0.92, 0.82), 2900 K on, intensity 3.6,
    shadow bias 0.6 / normal 0.4, Oasis lens flare 0.25. `RenderSettings.sun` points at it. The fill light `阳光` and a
    zero-intensity light stay in the scene, negligible.
  - Ambient: **Trilight** sky (0.50, 0.62, 0.90), equator (0.50, 0.50, 0.52), ground (0.18, 0.16, 0.16) (bake "D": cooler
    than the first dusk gradient so the 2900 K bounce does not turn the probes orange).
  - `RaceMap_Lighting.lighting`: Progressive **GPU**, Baked Indirect, 32 / 256 / 128 samples (direct / indirect /
    environment), 2 bounces, probe sample multiplier 8, 8 texels/unit, 2048 atlas, directional, OIDN denoisers, albedo boost 1,
    `RaceMap_LightmapParameters.giparams`. **Shared with the menu scene** (same asset, not a copy).
  - Light probes: 1480, laid out by `RaceMapLightProbeLayout` along `MapPathLine`, over the sand and the end field.
  - Lightmaps: one atlas `Lightmap-0_comp_light.exr` / `_dir.png`, for the only static GI contributors (the sand plane
    `沙地` with `RaceMap_Sand`, the plane below the map). The zen landscape and track have **no lightmap UVs**, so they do
    not contribute GI; they are Reflection Probe Static (368 renderers) so probes see them.
  - Reflections: sky reflection `ReflectionProbe-0.exr` (from the bake, via LightingData); `Reflection Probe Lower`
    (whole map) and `Reflection Probe EndField` (600 x 120 x 600 at (1493, 40, -684)) are **Custom** mode holding
    `ReflectionProbe-Lower.exr` / `-EndField.exr`, 256 px HDR, rendered by `Lightmapping.BakeReflectionProbe`.
  - `LightingData.asset` (binary, about 0.9 MB, Git LFS since 2026-11-05) holds probes, lightmap and sky-reflection
    references.
- **Where**: `Assets/Scenes/RaceMap.unity` (sun, RenderSettings, probes), `Assets/Scenes/RaceMap/` (bake outputs and
  settings), `Assets/Editor/Rendering/RenderingBake.cs`, `RaceMapLightProbeLayout.cs`.
- **Truth**: **`RenderingBake`** for the shared environment (ambient, fog, sky, reflection settings) and the recorded
  sun/settings values it checks; the `.lighting` asset file for the bake settings; the committed outputs for the result.
- **Adjust**: change the value in `RenderingBake` (and the scene object for the sun), then run the scripted bake
  ([Bake](#bake-reproducibility)). Never re-tune with the Lighting window's colour pickers (LDR, clamps the fog).
- **Depends on**: sky (module 3) and the sun for ambient and reflections; `MapPathLine` and the track walls for probes.
- **Remove it and**: without LightingData the probes vanish and the frame warms up (a cancelled bake does this: reassign
  `Lightmapping.lightingDataAsset`); without the custom probes Buddha's silver reads the raw orange sky (study section 1).

### 3. Sky and fog

- **Need**: R1; one horizon where sky and fogged ground meet.
- **What**: `BuddahGo/Skybox/Sunset Sky` (`Assets/Rendering/Shaders/SunsetSky.shader`, hand-written) on
  `RaceMap_Sky_Dusk.mat`: zenith (0.16, 0.20, 0.44), horizon exponent 4, warm horizon toward the sun's azimuth, glow,
  halo and disk read from `_WorldSpaceLightPos0`, ground colour = `RenderSettings.fogColor.linear`. Fog: Exponential
  Squared, density 0.0009, colour (1.263, 0.967, 0.612), calibrated against the sky (4.8 levels off).
- **Truth**: sky colours in the material; fog in `RenderingBake` (written to both scenes).
- **Adjust**: sky colours are **linear** values (`[HDR]` properties pass through unconverted); fog colour is the
  inspector (gamma) value and is linearised, so the same numbers look more saturated as fog. After a sky change: bake,
  recalibrate the fog (racemap README re-tuning checklist step 2), set `RenderingBake.FogColor`, set the sky's ground
  colour to `fogColor.linear`, bake again.
- **Depends on**: the sun direction (glow and disk).
- **Remove it and**: the bake's ambient and reflections change with it (everything probe-lit); the menu shares it (R2).
  `RaceMap_Sky.mat` (pre-dusk) is used by no scene ([retired.md](retired.md)).

### 4. Post volumes and the black curtain

- **Need**: R1 grade, R8 shakers, R9 curtain.
- **What**: `Volume` (priority 1) with `RaceMap_Dusk_Profile.asset`: ACES, exposure +0.05, contrast 14, colour filter
  white, saturation 4, WhiteBalance 10 / -4, Bloom 0.7 / threshold 0.95 / scatter 0.72 / tint (1, 0.90, 0.78).
  `Volume_Dark` (priority 0) with `Volume_Dark Profile.asset`: the same grade 0.1 EV darker. `BlackCurtainViewController`
  (Main Camera) disables `Volume` for the player who sees the track edges during the curtain; `Volume_Dark` stays.
  `BlackCurtainRenderFeature` (`Assets/Scripts/Buddah/ComboSkill/Skill_BlackCurtain/`) darkens the frame at
  AfterRenderingTransparents on every quality level, before the other gameplay passes, so transparent scenery (the
  landscape's contact-shadow decals) no longer shows through; `TrackEdgeVisibility` drives `_EdgeVisible` on the
  `SG_MapEdge` ribbons (Preserve Specular Lighting off, so hidden = invisible).
- **Truth**: the two profile assets. The menu's Global Volume references `RaceMap_Dusk_Profile` (R2).
- **Adjust**: edit `RaceMap_Dusk_Profile`, then mirror the grade into `Volume_Dark Profile` minus 0.1 EV. Keep WhiteBalance,
  Bloom and ColorAdjustments present (values may change; Feel shakes relative to them). Runtime values, no bake.
- **Remove it and**: removing a component a Feel shaker drives throws a NullReferenceException every frame after a skill
  reflection (`RenderingModulesTests.PostVolumesHoldWhatTheirFeelShakersDrive` catches it in every build scene);
  letting `Volume_Dark` drift makes the curtain drop or change the grade. Turning off Stop NaN on the race camera
  brings back the full-screen white glow with a black hole on GPUs that keep Inf / NaN in the HDR target (Intel Arc
  reproduces it; `RenderingModulesTests.RaceCameraStopsNaNBeforePost`). Bloom's `clamp` is the profile default 65472;
  lowering it is an open user decision (it would dim the sun glare).
  `RaceMap_Environment_Profile.asset` (pre-dusk) is used by no scene ([retired.md](retired.md)).

### 5. `BuddahGo/Lit` and its materials

- **Need**: URP Lit with occlusion terms and shadowed reflections for readability on polished floors; the planar
  reflection sample (R4).
- **What**: `Assets/Rendering/Shaders/BuddahGoLit.shader` + `BuddahGoLitInput.hlsl`, `BuddahGoLighting.hlsl`,
  `BuddahGoLitForwardPass.hlsl`. URP Lit's inputs and keywords; four weights `_SpecularOcclusion`, `_MultiBounceAO`,
  `_HorizonOcclusion`, `_ShadowedReflections` (0 = stock URP Lit); `_PLANAR_REFLECTION` keyword. Inspector
  `Assets/Editor/Rendering/BuddahGoLitShaderGUI.cs` (carries URP Lit keywords over on a shader switch).

  | Material | Used by (RaceMap) | Planar |
  |---|---|---|
  | `Assets/Sources/RaceMapZen/Materials/RaceMapZen_Lane.mat` | the zen track `Map/RaceMapZen_Track` (prefab instance) | yes |
  | `Assets/Scenes/RaceMap/Materials/RaceMap_Lane.mat` | `EndField/Floor` | yes |
  | `Assets/Scenes/RaceMap/Materials/RaceMap_Sand.mat` | the sand plane `沙地` (static, lightmapped) | no |
  | `Assets/Scenes/RaceMap/Materials/RaceMap_Curb.mat` | only through a prefab-instance override in RaceMap (old curb strips); check before reuse | no |

- **Truth**: the shader files; per-surface values in the materials.
- **Adjust**: material values; scripts that set toggle floats call `BuddahGoLitShaderGUI.CarryOverKeywords`.
- **Remove it and**: the lane loses its planar reflection and shadowed reflections; a GBuffer pass is kept for a future
  SSR return.

### 6. `ZenGround`

- **Need**: the karesansui sand around the track, cheap and data-driven from the Blender layout.
- **What**: `Assets/Sources/RaceMapZen/Shaders/ZenGround.shader` (`BuddahGo/ZenGround`) on `RaceMapZen_Ground.mat`.
  `_Field` texture: R rake distance, G distance to track edge, B/A black/yellow sand weights; rake grooves computed in the
  shader; own DepthOnly and DepthNormals passes (SSAO sees the same raked normal).
- **Where / Truth**: the shader and `Assets/Sources/RaceMapZen/Data/`; `Assets/Editor/RaceMapZen/RaceMapZenImporter.cs`
  rebuilds `RaceMapZen_Landscape` (one root, replaced on re-import, no colliders).
- **Remove it and**: the ground is the largest screen area in gameplay; the anti-aliasing tuning (R5) was measured on it.

### 7. Planar reflection

- **Need**: R4: exact, stable reflections on the flat lane top and end-field floor.
- **What**: `Assets/Scripts/Rendering/PlanarReflection.cs` on each receiver renderer: per base game camera, a mirrored
  camera with an oblique near plane at the receiver's top (read from its bounds), renderer **by name**
  `URP-PlanarReflection-Renderer`, into a mip-mapped HDR texture bound to `BuddahGo/Lit` (`_PLANAR_REFLECTION`). Only
  up-facing pixels at the plane height take it; side walls keep the probe.
- **Adjust**: graphics option `reflections` sets `SettingsScale` (off = probe only, 50%, 100%).
- **Depends on**: module 1 (renderer list), module 5 (keyword). **Remove it and**: the floors fall back to the probe.
  About 0.65 ms on High Fidelity.

### 8. Anti-aliasing

- **Need**: R5.
- **What**: `Assets/Scripts/Rendering/CameraAntialiasingPolicy.cs` on RaceMap's gameplay camera picks the post AA per
  quality level (High Fidelity FXAA on MSAA 8x, Balanced TAA, Performant FXAA); `Apply(mode)` is the settings hook. The
  camera's stored mode is None. MSAA comes from the pipeline asset.
- **Truth**: `Graphics.xlsx` `anti_aliasing` levels and presets; the policy for the no-settings default.
- **Remove it and**: SMAA would come back from the camera if someone sets it there; it crawls on the sand.

### 9. `OccluderFade`

- **Need**: the player stays visible behind tall scenery (torii, gate, watari span, 塔庭 torii).
- **What**: `Assets/Scripts/Rendering/OccluderFade.cs` on the landscape root (added by `RaceMapZenImporter`): tests the
  camera-to-follow-target segment against occluder bounds, swaps to transparent material copies and fades to 0.25, then
  restores the opaque materials. Client-side only.
- **Adjust**: `occluders`, `fadedAlpha`, `fadeTime`, `padding`, `targetHeight` on the component (the importer sets them).
- **Remove it and**: the player disappears behind arches in gameplay.

### 10. VFX rendering path

- **Need**: skill effects readable over the dusk grade; tiers for the settings (R10).
- **What**: skill VFX slot prefabs on layer 6 (Buddah) driven by `Assets/Scripts/VFX/SkillVfxPhaseController.cs` and
  `SkillVfxLayer`; `InkFluidVolume` for the ink smoke. Hand-written FX shaders in `Assets/Rendering/Shaders/FX/`
  (`FX_Additive_Glow`, `FX_Alpha_Smoke`, `FX_InkVolume`, `FX_InkFluid3D` + `InkFluid3D.compute`, `FX_CurtainEye`,
  `FXCommon.hlsl`, `FXInk.hlsl`); they all `multi_compile_fog`, additive ones fog toward black. Drawn by `RenderSkillVfx`
  last among the gameplay passes on every quality level (frame step 6), graded by post like everything else.
- **Tiers** (`vfx_quality`, values in the `vfx*` columns of `Graphics.xlsx` `GraphicsOptionLevel`, read into
  `BuddahGo.Rendering.VfxQuality.Settings` by `GraphicsSettingsApplier.VfxTierFrom`; `VfxTierSettings.Fallback` without
  tables), Low / Medium / High: slot layers kept up to priority 1 / 2 / 3; detail particle emission 40 / 70 / 100%
  (systems with more than 4 particles per cycle; gameplay shapes and zone areas stay at 100%); real-time lights off / on /
  on; cosmetic trails 0.5 / 0.75 / 1; ink-smoke budget 2 / 4 / 8; ink grid cell scale 1.5 / 1.25 / 1. Read when a slot
  starts (ink budget every frame); no restart. Effects that carry gameplay information must not depend on the tier.
- **Truth / spec**: [../vfx/README.md](../vfx/README.md) (layers, budgets, naming) and `Graphics.xlsx` for the tiers
  ([graphics-settings.md](graphics-settings.md#vfx-tiers)).
- **Remove it and**: without `RenderSkillVfx` (or with layer 6 back in the transparent pass) effects lose their draw
  order against the curtain and trails, or draw twice.

### 11. Graphics settings layer

- **Need**: R5, R6, R10.
- **What / Where**: `Assets/Scripts/Rendering/Settings/` (catalog, model, applier, pipeline mapper, service, F9 debug
  panel), spec `Assets/Design/Tables/Graphics/Graphics.xlsx`, main-menu panel `SettingsPanelUI` built by
  `Assets/Editor/UI/SettingsPanelBuilder.cs`.
- **Truth**: `Graphics.xlsx` (never the generated asset). Full description: [graphics-settings.md](graphics-settings.md),
  owned by the parallel modularisation session; this file does not repeat it.
- **Interface** (shared by the menu panel and the F9 panel): `GraphicsSettingsService.IsActive(option)` greys an option
  out when it is moot (`GraphicsOptionDependencies`: upscaler at 100%, sharpening without TAA, frame cap with VSync on),
  when the preset allows one level (SSAO on low), or where `GraphicsSettingsApplier.AppliesDisplay` is false (window and
  resolution in the editor or with a command-line window); `RequiresRestart` / `PendingRestart` drive the "需要重启" tag
  and the restart notice. Every option's `restartRequired` is false today (all apply live, play-mode check 2026-11-05;
  window and resolution not checked in a player).
- **Rule**: URP assets are never written; values go to runtime clones and are restored when play mode ends.
- **Remove it and**: the game runs on the quality level's real assets (an empty catalog makes the layer do nothing).

### 12. Menu scene's shared assets

- **Need**: R2.
- **What**: `Assets/Scenes/MenuEnvironmentSetting.unity` (prototype, outside the build list; layout owned by Blender,
  [ADR 0005](../adr/0005-menu-environment-layout-is-owned-by-blender.md)) references RaceMap's `RaceMap_Sky_Dusk.mat`,
  `RaceMap_Dusk_Profile.asset` and `RaceMap_Lighting.lighting`, and gets the shared ambient, fog and reflection values from
  `RenderingBake.ApplyEnvironment()` (called by `Assets/Editor/MenuEnvironmentImporter.cs`). Own sun. Own
  `Reflection Probe Shrine` (Custom, `Assets/Scenes/MenuEnvironmentSetting/ReflectionProbe-Shrine.exr`). **No lightmap
  bake** (no LightingData; ambient is computed at load), although the importer marks its objects Contribute GI.
- **Remove it and**: an edit to RaceMap's sky, profile or lighting asset changes the menu too; that is intended.

### 13. Diagnostic tools

| Tool | Use |
|---|---|
| `Assets/Editor/Rendering/RaceMapDuskColorStudy.cs` | **Default capture.** HDR captures of the five viewpoints with temporary overrides (tokens in its header); `Capture(label, "", "")` captures the scene as it is; `Snapshot()` prints the colour layers |
| `Assets/Editor/Rendering/RaceMapViewpointCapture.cs` | The original five viewpoints (also used by the other tools); its own capture is 8-bit and assumes the old track height |
| `Assets/Editor/Rendering/RaceMapTemporalNoise.cs`, `Tools/rendering/temporal_noise.py`, `build_aa_evidence.py` | Shimmer and motion sweeps, gameplay clips, HDR cost runs |
| `Assets/Editor/Rendering/RaceMapRenderCost.cs` | Frame time per viewpoint with the pipeline's MSAA |
| `Assets/Editor/Rendering/RaceMapLightProbeLayout.cs` | Regenerates the light probes (`BuddahGo/Rendering/Generate RaceMap Light Probes`) |
| `Assets/Editor/Rendering/RenderingBake.cs` | Verify / bake (next section) |
| `Tools/rendering/build_racemap_compare.py` | Labelled before/after side-by-sides of two capture folders |
| `Tools/rendering/capture_diff.py` | Per-view difference statistics of two capture folders and a labelled before / after / difference sheet |

Capture folders under `Docs/rendering/racemap/<label>/` are local evidence (PNG, git-ignored or untracked): never commit
them; commit the JPEG comparison sheets.

## Bake reproducibility

**One entry point**: `BuddahGo/Rendering/Bake/Bake RaceMap + Menu` (`RenderingBake.BakeAll()`), or
`RenderingBake.Verify()` to check without changing anything. What it does:

1. Refuses to start if an open scene has unsaved changes or play mode is on (no dialogs).
2. Opens RaceMap, **writes** the recorded shared environment (`ApplyEnvironment`: lighting-settings asset, sky, Trilight
   ambient, skybox reflection settings, fog including red > 1), then **checks** the lighting-settings fields, the sun
   (name, rotation, filter, temperature, intensity, Mixed) and the custom probes (Custom mode, recorded file). Any
   mismatch stops the bake: record the new value in `RenderingBake` first (`BakeAll(…, force: true)` overrides).
3. Optionally regenerates the light probes (`… (Regenerate Light Probes)`).
4. `Lightmapping.Bake()` (lightmaps, light probes, sky reflection), then renders each custom probe into its recorded
   file with `Lightmapping.BakeReflectionProbe`, saves RaceMap.
5. Opens the menu, writes the same environment, renders `Reflection Probe Shrine`, saves it. No lightmap bake there.
6. Writes [bake-manifest.json](bake-manifest.json): inputs hash (recorded values, the sun, the `.lighting`, `.giparams`
   and sky material file hashes, every light-probe position), the lighting settings as used, git HEAD, Unity version,
   GPU / CPU / memory, every warning or lightmapper message logged during the bake, and every output file with its size
   and SHA-256. Then reopens the scenes that were open.

Batch mode: `Unity -batchmode -projectPath . -executeMethod BuddahGo.Rendering.Editor.RenderingBake.BakeAllBatch`
(exit 0 when baked; `-regenerateLightProbes` optional). Not tested yet.

**Committed** (outputs go in the same commit as the input change and the manifest): `LightingData.asset`,
`Lightmap-0_comp_light.exr` and `_comp_dir.png`, `ReflectionProbe-0.exr`, `-Lower.exr`, `-EndField.exr`,
`ReflectionProbe-Shrine.exr` (all in Git LFS; `LightingData.asset` since 2026-11-05, R12), both scenes,
`bake-manifest.json`. A fresh clone or worktree needs `git lfs pull` before Unity opens it, or the scenes load LFS
pointer text as lighting data. A collaborator who only plays or edits other
things never bakes; they get the committed result. Rebake only when an input changes, and only one person per change.

**What is and is not identical across bakes** — see [Measured tolerance](#measured-tolerance) for numbers:

- Identical by construction: every input the tool records (the inputs hash says so; two machines with the same hash
  baked the same scene state, geometry aside: scene geometry is covered by the git HEAD, not by the hash).
- Not bit-identical: the GPU lightmapper's path tracing and the denoiser give different bytes per run, per GPU and per
  driver; the CPU lightmapper differs from the GPU one more than two GPU runs differ from each other. Output SHA-256s
  therefore differ; compare images, not hashes.
- Machine traps: the lightmapper reports "out of memory" when the system commit charge is near its limit, whatever the
  scene holds (close memory-heavy programs); if the GPU lightmapper falls back to CPU, the manifest's messages show it.
  A cancelled bake detaches LightingData (probes vanish): reassign `Lightmapping.lightingDataAsset`, do not save.
- Meta-file trap (seen 2026-11-05, first verification bake): the bake could not write `ReflectionProbe-0.exr.meta`
  ("Cannot open file … for write", the file was briefly held by another process) and the sky reflection re-imported as a
  2D texture with default settings. The tool now reports "BAKED WITH PROBLEMS" when any reflection EXR does not import
  as a Cubemap; fix it with `git checkout -- <file>.exr.meta`, reimport the EXR, and check `git status` shows no `.meta`
  change before committing. The second bake did not hit it.
- After a scripted scene reopen the editor shows the sky reflection wrongly until a bake or a play-mode round trip:
  capture only after either.

**How to compare a bake with the committed one**: capture before (after a play-mode round trip) and after the bake
with `RaceMapDuskColorStudy.Capture(label, "", "")`, then
`python -P Tools/rendering/capture_diff.py <before> <after> --sheet <out>`. Accept the bake when every view is within the
tolerance below; otherwise look at the sheet's difference column before committing.

### Measured tolerance

Verification bake on 2026-11-05: RTX 4070 Laptop, Progressive GPU, Unity 2022.3.55f1c1, git 42c5d6bd, inputs hash
`3e84c5b3…`, bake 28-38 s in total. Captures are HDR, five viewpoints, 1920x1080, compared in 8-bit sRGB levels
(`capture_diff.py`). Sheet: [racemap/bake_repro_compare/all.jpg](racemap/bake_repro_compare/all.jpg) (committed | tool
bake | difference x8).

| Comparison | Output files (SHA-256) | Captures: mean / pixels > 8 levels / max, per view |
|---|---|---|
| Capture repeated, nothing changed (noise floor) | — | 0.43-0.49 / 0% / 2 |
| Tool bake 1 vs tool bake 2, same machine | `LightingData.asset`, `Lightmap-0_comp_light.exr`, `ReflectionProbe-Lower.exr`, `ReflectionProbe-Shrine.exr` differ every run; `Lightmap-0_comp_dir.png`, `ReflectionProbe-0.exr` (sky), `ReflectionProbe-EndField.exr` identical | **0 / 0% / 0** on every view: the byte differences do not reach the screen |
| Committed bake (2026-11-03, by hand) vs tool bake | all differ except the directional map and the sky reflection | 0.06-0.17 / 0.00-0.47% / 26-38, **only on the silver Buddha** (and a few rock pixels in the corner view): its custom probes were re-rendered after c7124d10 took the gameplay layers out of the transparent pass; lightmap- and probe-lit surfaces stay within 2 |

**Tolerance to accept a rebake with unchanged inputs**: per view mean ≤ 0.5 levels, at most 1% of pixels over 8 levels,
and those only on reflective surfaces (Buddha, the lane's reflections). Anything wider, or any change of the
probe-lit ground or ambient, means an input changed: check the manifest's inputs hash and `applied` lists first.
**Across machines** this was not measured (one machine available). Expect the lightmap and probe bytes to differ more
(another GPU or driver, or the CPU fallback), still within the tolerance above for this scene, because the lightmapped area
is small (the sand plane) and the bake's visible products are the light probes and cubemaps. A collaborator's first
bake should be compared against the committed one with the loop above before it is committed.

## Where things are recorded

| What | Where |
|---|---|
| Current architecture (this file) | `Docs/rendering/ARCHITECTURE.md` |
| Agent entry, invariants, standard loop | [HARNESS.md](HARNESS.md) |
| Open items, traps, tools, re-tuning checklist | [racemap/README.md](racemap/README.md) |
| Phase history, measurements, reviews | [racemap/history.md](racemap/history.md) |
| Dusk colour study, C2 numbers, A2/B2 alternatives | [racemap/dusk-color-study.md](racemap/dusk-color-study.md) |
| Graphics settings spec and runtime | [graphics-settings.md](graphics-settings.md) |
| Unused rendering assets waiting for a cleanup batch | [retired.md](retired.md) |
| Last bake's inputs and outputs | [bake-manifest.json](bake-manifest.json) |
