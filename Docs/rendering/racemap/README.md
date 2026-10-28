# RaceMap rendering: production changes

Branch `feat/racemap-rendering` (named `refactor/vfx-optimization` until 2026-10-28) · PR
[OldMustClimbStudio/BuddahGo#65](https://github.com/OldMustClimbStudio/BuddahGo/pull/65) (continues #64, closed by the
rename) · Unity 2022.3.55f1c1 · URP 14.0.11 · started 2026-10-25.
This note tracks the phased move of the showcase look into the shipped map `Assets/Scenes/RaceMap.unity`.
The showcase that preceded it is documented in [../showcase/README.md](../showcase/README.md) (its Unity project
stays local; only the note and images are in the repository).

| Phase | Scope | State |
|---|---|---|
| 1 | Lighting and environment with stock URP Lit | done, approved, committed |
| 1b | Sunset re-tune: the user asked on 2026-10-26 for the final look to read as a sunset | done, approved, committed |
| 2 | Screen-space reflections and the shadow-distance policy | done, approved, committed |
| 3 | Hand-written `BuddahGo/Lit` for RaceMap's marble and sand | done, committed (on 2026-10-26 the user asked for phases 3 and 4 to run to the end without an approval stop) |
| 4 | Final review of the whole branch, fixes, build and docs | done, committed |
| Play-test fixes | The user's layout change (track lowered 2.37 m), the 一念光明 edge line showing while the skill is off, Buddha floating over the track, no bake for now (2026-10-27) | done, committed; see [Play-test fixes](#play-test-fixes-2026-10-27) |

## Changes outside RaceMap

Most changes are scoped to `Assets/Scenes/RaceMap*`. These are shared and reach other scenes or every build:

| Change | Effect elsewhere | Checked |
|---|---|---|
| `Buddah.prefab` `model` renderer casts two-sided shadows (phase 1) | the player's shadow in every scene | RaceMap captures |
| `URP-HighFidelity` / `URP-Balanced`: shadow distance 220 m / 150 m (phase 2) | MainMenu and PropertySelection use the same assets | MainMenu identical, PropertySelection 1-px edge jitter |
| SSR (phase 2) **removed on 2026-10-28** (PR #64 review M6, the user's decision): the `ScreenSpaceReflectionURP` feature on `URP-HighFidelity-Renderer` / `URP-Balanced-Renderer`, the SSR override in `RaceMap_Environment_Profile` and `URP-KeepDeferredVariants-Renderer` (the Deferred renderer that kept SSR's GBuffer pass in builds) | planar reflections had replaced SSR, nothing shipped could turn it on. URP's stripping report (`Temp/shader-stripping.json`, release player build): 7609 -> 6402 variants (-1207): every GBuffer pass (875), URP's StencilDeferred (319), the SSR shader (13). The plugin stays for a later return, see "Adding SSR back" in its notes | stripping reports of two editor builds, EditMode tests, player build |
| `Assets/Plugins/UnitySSReflectionURP/` (phase 2) | scripts compile into `Assembly-CSharp-firstpass` | build |
| `Assets/Rendering/Shaders/` (sky, `BuddahGo/Lit`) and `Assets/Editor/Rendering/` | used only by RaceMap's materials; editor tools | build |
| `SG_MapEdge` shader graph: Preserve Specular Lighting off, emission no longer multiplied by `_EdgeVisible` (play-test fix) | only RaceMap's edge ribbons use it (the air-wall prefab `反转法线空气墙` also does, but no scene places it) | HDR captures, High Fidelity and Balanced |
| Buddha collider refitted to the animated model (play-test fix): `Movement/Movement` `colliderCenter` y +0.998, prefab capsule and `RollbackHurtbox` with it, prefab `fotou` 0.2515, `Movement/Respawn` `heightOffset` 3 -> 2.002 | every racer, player and AI, in every scene; the body origin rests 0.998 m lower inside an unchanged world-space capsule | autopilot Solo lap, design-table tests |
| RaceMap gameplay camera: post AA from `CameraAntialiasingPolicy` (FXAA on top of 8x MSAA on High Fidelity since 2026-10-28, TAA on Balanced, FXAA on Performant) instead of SMAA | RaceMap only; no pipeline or renderer asset changed | shimmer sweeps with real MSAA, race clips, cost runs |
| `URP-PlanarReflection-Renderer` (no features) appended to all three pipeline assets (index 1 on Performant, 2 on Balanced and High Fidelity) | only `PlanarReflection` cameras select it, by name; the renderer lists grow by one | editor, player build |
| `BuddahGo/Lit`: `_PLANAR_REFLECTION` material keyword and `PlanarReflection` component | only `RaceMap_Lane` enables it (lane top and end-field floor) | freeze-frame comparisons, cost runs |
| Graphics settings now also set VSync (on by default), window mode (borderless) and resolution (native) | every scene; window and resolution only in a player whose command line does not set them | EditMode tests |
| Graphics settings layer (`Assets/Scripts/Rendering/Settings/`, spec in `Graphics.xlsx`; temporary F9 panel): loads the saved choice after the first scene loads and applies it | every scene; with no saved choice or a preset without overrides it changes nothing, otherwise it uses runtime clones of the URP assets and restores `QualitySettings` when play mode ends ([../graphics-settings.md](../graphics-settings.md)) | EditMode tests, play-mode check |

Each pipeline asset lists its default renderer at index 0 and `URP-PlanarReflection-Renderer` at index 1, which only
`PlanarReflection`'s mirror cameras select, by name. No other camera selects a renderer by index
(`BlackCurtainViewController`'s renderer switching is off in RaceMap).


## How to compare

- `BuddahGo/Rendering/Capture RaceMap Viewpoints` (editor menu) or `RaceMapViewpointCapture.Capture(label)` renders five
  fixed viewpoints into `Docs/rendering/racemap/<label>/`. Views 01-03 use the real gameplay framing (CM_Gameplay:
  player + offset (77.34, 60, 6.6), rotation (35, 258.4, 0), FOV 40) at the start grid, a straight marble stretch and
  the sharpest corner. View 04 is intro shot CM_Shot1, the only in-game camera that shows the sky. View 05 is the
  results camera CM_End over the end field (added after the phase-0 capture, so it has no "before"). A proxy of the
  player model (baked from `Buddah.prefab` in an isolated prefab context, never instantiated with its network
  components) stands on the track; nothing is saved to the scene.
- **Two caveats found on 2026-10-27, not yet fixed in the tool.** (1) It renders without HDR: URP 14 takes the camera
  buffer format from the target texture (`UniversalRenderPipeline.CreateRenderTextureDescriptor`), and the tool's
  target is 8-bit, so values above 1 (sun glints, emission, the sun disk) clip before blending and tonemapping. The game
  renders to the screen with HDR on (High Fidelity and Balanced). An HDR and an 8-bit capture of the same frame differ
  on 24-32% of the pixels of views 01-04 (mean 13-16 levels where they differ), so every phase image in this note shows
  highlights and transparent effects slightly differently from the game. The play-test captures render into a
  `DefaultHDR` target and blit to sRGB. (2) `TrackHeight` is still 2.62; since the user's layout change the lane top
  is at 0.25, and the proxy root should stand 1.296 m above the lane (the capsule's clearance), not on it.
- `python -I Tools/rendering/build_racemap_compare.py <before> <after> <out>` writes labelled JPEG side-by-sides.
- `RaceMapRenderCost.Measure(viewIndex, frames)` renders one viewpoint at 1920x1080 and waits for the GPU every
  frame. Compare runs taken in the same session on the same machine; the absolute number is editor-side.

## Phase 1: lighting and environment

This section records the approved afternoon checkpoint. Phase 1b replaced its sun, sky, ambient, fog and grade
values with a sunset; phase 2 changed the shadow distance. The current values are in those sections.

Images: [phase1_compare/all.jpg](phase1_compare/all.jpg) (before left, after right), per view in `phase1_compare/`.
How the look was chosen: [phase1_candidates/](phase1_candidates/) (2x2 sheets). The full-resolution PNG captures are
not committed and stay on the capturing machine (`phase1_after/` can be regenerated from commit 51af7d5; the others
record states that no longer exist): `phase0_before/`,
`phase1_after/`, the candidates `cand_golden/`, `cand_afternoon/`, `cand_midday/`, `cand_c_tuned1/`, and two diagnostic
captures, `exp_a_sun_is_mainlight/` (MainLight made the sun on the old bake: players get shadows, but the whole track
goes dark, because the old probes' occlusion marked it as shadowed) and `exp_b_plus_skybox_ambient_no_rebake/`
(ambient switched to skybox without a rebake: no visible change, because every lit object reads baked probes or
lightmaps).

### What was wrong before

- **No player shadows.** `RenderSettings.sun` pointed at the 0.31-intensity fill light `阳光`, which casts no shadows.
  URP picks the scene sun as its main light (`UniversalRenderPipeline.GetMainLightIndex`), so the bright `MainLight`
  was only an additional light, and URP 14 never renders shadows for additional directional lights.
- **Borrowed, inconsistent environment.** The sky was the Oasis sunset cubemap, overexposed enough that the intro shot
  clipped to white. Ambient was trilight with a 1.3-intensity blue-white horizon colour. The lighting settings and the
  priority-1 post profile were the Oasis sample's assets. That profile gave exposure +0.31, no tonemapping, bloom
  off (intensity 0), and an Oasis height fog (`OasisFogVolumeComponent`, drawn by the `Lighting/Post_Effects/Oasis Fog`
  pass): density 0.0012, from 20 m, 0-20 m high, orange tint (1, 0.52, 0.33). That orange fog over the track's
  height band, plus the warm sun mixed with the blue-white trilight, produced the pink/lavender track that grows
  pinker with distance. Built-in distance fog was off.
- **Broken light probes.** The 261 probes were scattered from y = -306 to 862 m by a group scaled 19.6x. Ambient on
  the track varied about 5x along the lap, and the baked probe occlusion marked the start grid as shadowed (0.05).
  Every probe-lit object (the lane, the curb strips, the end-field floor, the four edge ribbons, all players) read
  this data, so ambient changes had no effect without a rebake.

### What changed (RaceMap-scoped)

| Item | Before | After |
|---|---|---|
| Scene sun | the fill light `阳光` | `Lighting/MainLight` |
| `MainLight` | rotation (13, 340.7, 0), filter (1, 0.76, 0.48), intensity 1, pipeline shadow bias | rotation (35, 340.7, 0), filter (1, 0.92, 0.82), intensity 2.6, own shadow bias depth 0.6 / normal 0.4 |
| Fill light `阳光` | active | GameObject disabled |
| Sky | `OasisSunsetSkybox_Mat` (cubemap) | `Assets/Scenes/RaceMap/RaceMap_Sky.mat`, `Skybox/Procedural`: sky tint (0.47, 0.50, 0.57), ground (0.45, 0.38, 0.30), atmosphere 0.85, exposure 1.1, sun size 0.035 |
| Ambient | trilight x1 | skybox x1.3 |
| Reflections | skybox x0.498, 128 px; `Reflection Probe Lower` intensity 0.5 | skybox x1, 256 px; probe intensity 1.0, rebaked |
| Distance fog | off | exponential squared, density 0.0008, colour (0.756, 0.988, 1.14) (HDR, see below) |
| Height fog | Oasis fog pass, density 0.0012, orange | removed: `Post_Effects/Oasis Fog` GameObject disabled |
| Post, `Volume` (priority 1) | `Oasis_Outdoor_Volume_Profile` | `Assets/Scenes/RaceMap/RaceMap_Environment_Profile.asset`: ACES; exposure -0.1, contrast +5, saturation -5; bloom exactly as before (threshold 0.6, intensity 0, grey tint); WhiteBalance with no overrides |
| Post, `Volume_Dark` (priority 0) | bloom 0.6/0.5, exposure +0.2, film grain (no overrides) | adds ACES, contrast +5, saturation -5; exposure +0.2 -> -0.2 |
| Lighting settings | `Oasis_LightingSettings.lighting` (Shadowmask) | `Assets/Scenes/RaceMap/RaceMap_Lighting.lighting` copied from it: Mixed = Baked Indirect, 32/256/128 samples (direct/indirect/environment), 2 bounces, albedo boost 2 -> 1, own `RaceMap_LightmapParameters.giparams` (copy of the Oasis one) |
| Light probes | 261 scattered | 1480 along the track, over the sand and over the end field (see below) |
| Lane `Map/地图场景_3.9/地图_2/15` and curb strips `Map/2` | Cast Shadows On | Cast Shadows Two Sided |
| Edge ribbons (`地图外Edge`, `地图内Edge`, `特效外圈`, `特效内圈`, layer TrackEdge, `SG_MapEdge`) | Cast Shadows On | Cast Shadows Off (they are invisible outside the black curtain, but their shadow pass drew the full mesh) |
| Player `model` renderer in `Assets/Character/Prefab/Buddah.prefab` (shared prefab, own commit) | Cast Shadows On: light holes in the head's shadow where the curl pattern is | Cast Shadows Two Sided: the holes are gone |
| Baked lighting | 3 lightmaps + shadowmasks | rebaked: 3 lightmaps (no shadowmasks with Baked Indirect), probes, reflection probe, sky reflection |

Unchanged on `MainLight`: Mixed mode, soft shadows, lens flare `OasisSun_Flare` with occlusion (already enabled at
HEAD; the flare now belongs to the scene sun), and colour temperature 5500 K. Because URP multiplies the colour filter
by the colour temperature, the effective sun is about linear (1, 0.70, 0.48), roughly 4400 K, which reads as late
afternoon. Re-tune with both values, not the filter alone.

**Gameplay hooks on the post volumes.** `BlackCurtainViewController` (on Main Camera) holds a direct reference to
`Volume` as its "outdoor volume" and disables it for the player who sees the track edges, between the curtain's
expand and fade-out; `Volume_Dark` is its "black-curtain active volume" and stays on. `Volume_Dark` therefore carries
the same ACES and grade, 0.1 EV darker than gameplay as before phase 1 (5f91e51: +0.31 vs +0.2), so the curtain no longer drops
tonemapping. The `Volume` GameObject also has Feel shakers, `MMWhiteBalanceShaker_URP` and `MMBloomShaker_URP`,
driven by the skill-reflection, backfire-reflection and lap feedbacks. They look up WhiteBalance and Bloom in this
profile without null checks, so both components must stay in it, and bloom keeps its pre-phase-1 values so the flashes
keep their tuned strength.

**Light probe layout** (`BuddahGo/Rendering/Generate RaceMap Light Probes`): every 20 m along the `MapPathLine`
spline, probes 1.5 m above the lane at the centre and 2 m inside each wall (wall distance measured per sample with
`BuddahGo.AI.TrackWallProbe`), plus a row 10 m above the lane for the top of the player model; a 160 m grid over the
rendered sand (y = 0; its collider top is at -2.33) at 1.5 m and 40 m, skipping points within 40 m of the track
centre line; a 240 m x 240 m grid at 40 m spacing over the end field, 1.5 m and 10 m above its floor. Probes touching
colliders or inside closed geometry are dropped (collider-less static props get temporary mesh colliders for the test;
the structure over the lap start is an arch with open air beneath). After the bake the interpolated ambient (L0
luminance) is 0.38-0.42 along the whole lap and at the end field, except about 40 m at the west end of the start arch,
where the racing line drops to 0.23-0.28: that stretch lies in the 47 m arch's shadow, and the bake sees the shadowed
sand under the lane (the track meshes are not static, so they take no part in the bake). Each probe-lit mesh takes one
ambient sample at its bounds centre; for the kilometre-long lane and curb meshes those centres are in open air over the
infield sand and sample the same 0.41. (These are the phase-1 afternoon values; the current numbers are under known
limits.)

**Why Baked Indirect.** Only three renderers are static (the sand plane, a plane 293 m below the map, the arch at the
lap start), so the shadowmask had almost nothing to store, and its probe occlusion is what darkened the start grid.
Realtime shadows cover the shadow distance (150 m in phase 1, 220 m on High Fidelity since phase 2); there are no
baked shadows beyond it.

**Fog.** Exp2 at 0.0008 is 0.6% at 98 m (the player), 1.4% at 150 m and 2.9% at 215 m (the farthest ground the
gameplay camera sees), so it is decorative: it gives no aerial perspective in gameplay and only hides the edge of the
sand plane on the intro horizon. Its colour was calibrated on the final graded intro capture so the fully fogged far sand matches
the sky directly above the edge of the sand plane (rows 284-292 against 298-302, averaged over 32 columns):
(0.756, 0.988, 1.14), about 2 levels off in total with a 0.6 deg hue shift. The blue component is above 1; the Lighting
window's colour picker is LDR and clamps it on a manual edit (capping only blue leaves a visible teal line), so after
an edit re-run the calibration or set the value from code.

### How the look was chosen

Three candidates were baked and rendered from the four viewpoints: golden hour (13 deg), afternoon (32 deg), midday
(55 deg). Four independent judges (physical plausibility, gameplay readability, art direction, technical artifacts)
scored them; all four picked the afternoon candidate (7-7.5/10; golden hour 4-6, midday 5-7, the original look 2-5).
The final values merge their tuning advice; where they conflicted (sun elevation 26-40 deg) the middle was taken.

### Review

An adversarial review (four lenses, then one skeptic per high/medium finding) checked the first version of this
phase. Fixed from it: the missing WhiteBalance component (the Feel shaker would have thrown a NullReferenceException
every frame after the first skill reflection), the curtain dropping tonemapping, the silently removed height fog and
its still-running empty pass, probes under the visible sand and outside the walls (black probes that darkened the
lane and the lap start), the shared Oasis lightmap parameters, albedo boost 2, a texture leak in the capture tool, and
wrong statements in this note. Refuted by its skeptic: "the bloom flashes are 3x too strong"; URP normalises the bloom
tint by its luminance, so the old grey tint was effectively white. A second round confirmed those fixes and found the
real cause of thin lit lines inside the track's shadow on the sand: the invisible edge ribbons cast shadows next to the
slab's, and the lines were the gaps between them (fixed by turning their shadows off). It also corrected the
lane/curb material description below, the fog calibration, the probe-layout wording, the cost wording and the
end-field capture pose. A probe anchor added in the first fix round was removed because it no longer changed
anything.

### Cost

`RaceMapRenderCost`, viewpoints 01-03, 3 rounds of 200 frames each, 1080p with 8x MSAA (High Fidelity, the PC
default), RTX 4070 Laptop, editor, one session:

| State | Median frame time |
|---|---|
| Before (Oasis profile with its height-fog pass, fill light as sun, no distance fog) | 4.69 ms |
| After | 4.96 ms |
| After, main-light shadows off | 4.52 ms |

Net +0.3 ms. The main-light shadow map adds about 0.45 ms (after vs. after without shadows). The remaining -0.17 ms
mixes several changes that were not measured separately: the Oasis height-fog pass removed, the per-pixel additional
directional light (`MainLight` before it became the sun) and the shadowmask sampling gone, distance fog and ACES
added. The capture images are rendered without MSAA so that they stay comparable with the phase-0 captures. Bake:
about 100 s with the GPU lightmapper.

## Phase 1b: sunset

Images: [phase1b_compare/all.jpg](phase1b_compare/all.jpg) (phase 1 afternoon left, sunset right) and the candidate
sheets in [phase1b_candidates/](phase1b_candidates/) (A afternoon, B golden hour 18 deg, C sunset 12 deg, D late sunset
7 deg). Local only: `phase1b_after/`, `cand_s_golden/`, `cand_s_sunset/`, `cand_s_late/`.

### Why a hand-written sky

A sweep of `Skybox/Procedural` (sun 7-12 deg, atmosphere 1.2-3.5, several tints) never produced a warm horizon under a
cool zenith: thin atmosphere stays pale blue, thick atmosphere turns the whole sky flat yellow and then muddy
red-brown. A brown sky would also make the baked ambient brown and remove the cool shadows that make a low sun read as
a sunset. `Assets/Rendering/Shaders/SunsetSky.shader` (`BuddahGo/Skybox/Sunset Sky`, hand-written URP 14 HLSL) is a
zenith-to-horizon gradient whose horizon warms toward the sun's azimuth, plus a tight glow, a wide halo and a sun disk
that replaces the sky behind it. It reads the sun direction from `_WorldSpaceLightPos0`, which Unity sets from the Sun
Source for every skybox draw; the glow follows `MainLight` in camera renders and the bake picks it up for ambient and
reflections. Its colours do not follow the sun's elevation: they are tuned for this time of day. `RaceMap_Sky.mat` was
reused (its content replaced in place, so the GUID and the scene reference stay).

**Colour-space trap.** The sky's colour properties are `[HDR]`, and Unity passes them to the shader unconverted, so
the material values are linear (verified by rendering a 0.5 grey sky into a float target: it reads 0.5).
`RenderSettings.fogColor` is linearised instead, so the same numbers render far more saturated as fog than as sky.

### What changed (against phase 1)

| Item | Phase 1 | Phase 1b |
|---|---|---|
| `MainLight` | rotation (35, 340.7, 0), 5500 K, intensity 2.6 | rotation (10, 340.7, 0), 3600 K (filter unchanged), intensity 4.0 |
| Sun lens flare | intensity 1 | 0.25 on `MainLight`'s component (its ghost ring crossed the player in the intro); the shared Oasis flare asset is unchanged |
| Sky | `Skybox/Procedural` | `BuddahGo/Skybox/Sunset Sky`, linear values: zenith (0.17, 0.24, 0.52), horizon away from the sun (0.82, 0.50, 0.44), toward the sun (1.25, 0.45, 0.14), band falloff 7, spread 1.5, glow (0.85, 0.32, 0.08) power 64, halo 0.2 (power 3), disk (2.2, 0.95, 0.30) radius 1.6 deg, below the horizon the fog colour's linear value `RenderSettings.fogColor.linear` (1.083, 0.627, 0.224), exposure 1.2 (it scales the below-horizon colour too) |
| Ambient | skybox x1.3 | skybox x1.2 |
| Distance fog | 0.0008, (0.756, 0.988, 1.14) | 0.0009, (1.037, 0.813, 0.511), recalibrated as in phase 1 (1.6 levels off in total, -0.7 deg hue) |
| `Volume` profile | exposure -0.1, contrast 5, saturation -5, no white balance | exposure +0.3, contrast 10, saturation 5, white balance temperature 4, tint -3 (the Feel shaker shakes relative to these) |
| `Volume_Dark` profile | exposure -0.2, contrast 5, saturation -5 | exposure +0.2, contrast 10, saturation 5, white balance 4 / -3 added: still 0.1 EV under gameplay |
| Baked lighting | | rebaked: lightmaps, probes, reflection probe, sky reflection |

### How it was chosen

The three sunset candidates were baked and captured (sheets above). A judge panel with three lenses (art direction,
gameplay readability, technical artefacts) and a synthesis agent reviewed them; all three judges picked C (12 deg).
B never shows the sun in the intro frame and reads as a hazy afternoon. D has the strongest sky but turns sand, lane and
player magenta, has the lowest lane/curb contrast and clips the sand crests. The synthesis fitted a model of the sky
and the grade to the sheets and merged the judges' advice into one set of changes:

- The sun moves to 10 deg so the disk sits fully inside the intro frame.
- The zenith turns azure and the anti-sun horizon puts blue below green. This takes the magenta out of the baked
  ambient and out of the lane's sky reflection.
- The glow is tighter and dimmer, and the flare goes down to 0.25.
- White balance moves to 4 / -3, ambient to 1.2 and sun intensity to 4.0.

One more step raised the sky exposure from 1.0 to 1.2, because the darker sky had cut the lane's reflection and its
contrast against the curbs.

| Start grid, gameplay camera | C (12 deg candidate) | Final |
|---|---|---|
| Lane against curb (WCAG contrast) | 2.00 | 1.88 (afternoon 2.02) |
| Player shadow against lane | 1.33 | 1.28 |
| Brightest sand crest, red | 235 | 233 (no clipping) |

The low sun also removed the large sun glint that the 35 deg afternoon sun put on the glossy end-field floor in the
results shot: the reflection now falls outside the frame.

## Phase 2: screen-space reflections and shadow distance

> SSR was replaced by planar reflections on 2026-10-28 and removed from the renderers, the RaceMap profile and the
> renderer lists the same day (see "Changes outside RaceMap"). This section records how it was set up; the plugin's
> notes say how to add it back.

Images: [phase2_compare/all.jpg](phase2_compare/all.jpg) (phase 1b left, phase 2 right). Evidence in
[phase2_evidence/](phase2_evidence/): `ssr_off_on.jpg`, `ssr_msaa8_off_on.jpg` (the shipped 8x MSAA path, captured
with `RaceMapViewpointCapture.Capture(label, 8)`), `ssr_colour_mipmaps.jpg`, `shadow_distance_150_vs_220.jpg`,
`quality_levels.jpg`, `propertyselection_shadows.jpg`. Local only: `phase2_after/`. The phase captures render
without MSAA, so they show SSR's no-MSAA path.

### SSR package

jiaozi158/UnitySSReflectionURP moved from `Assets/Showcase/ThirdParty/` to `Assets/Plugins/UnitySSReflectionURP/` with
`AssetDatabase.MoveAsset`, so every GUID is kept and the showcase keeps using the same copy. It carries its `LICENSE`
(MIT) and `BuddahGo_NOTES.md` (upstream commit `555d946`, what was copied, the build requirement below and the
`[BuddahGo]` patches). The patches: null checks in `ForwardGBufferPass.FrameCleanup`; a runtime copy of the post
material, because the passes wrote per-frame values into the shared `.mat` (now byte-identical to upstream); and no
SSR in reflection-probe captures, preview cameras or on OpenGL. Scripts under `Assets/Plugins` compile into `Assembly-CSharp-firstpass`; the
showcase finds the types by scanning every assembly.

### Setup

| Where | Setting |
|---|---|
| `URP-HighFidelity-Renderer` | `ScreenSpaceReflectionURP` feature, 75% resolution, no colour mipmaps, the package's `ScreenSpaceReflection.mat`. Order: SSAO, SSR, then BlackCurtain, RenderBuddah, RenderMapEdge, RenderTrails (unchanged) |
| `URP-Balanced-Renderer` | the same feature at 50%, trilinear colour mipmaps |
| `URP-Performant-Renderer` | none |
| `URP-HighFidelity`, `URP-Balanced` renderer lists | `URP-KeepDeferredVariants-Renderer` (Deferred, no features) appended after the default renderer; no camera selects it (default index 0, no camera or script picks index 1) |
| `RaceMap_Environment_Profile` | `ScreenSpaceReflection`: Enabled, Approximation, quality High (64 steps), constant thickness 0.75, minimum smoothness 0.4, smoothness fade 0.6, edge fade 0.1 |

The feature only enqueues its passes while a volume enables the component, and only RaceMap's profile has it. MainMenu
and PropertySelection have no SSR component in their volumes; the showcase uses its own renderer. Surfaces with
smoothness 0.4 or more reflect: the lane (0.93) with its glossy side walls, and the end-field floor. The curbs (0.06)
and the sand do not. Players are drawn in the opaque pass too, so they appear in the reflections (`ssr_off_on.jpg`).

**Build requirement.** In Forward, SSR redraws opaque objects with URP Lit's `GBuffer` pass, and URP 14 strips every
pass named `GBuffer` from a build unless some renderer in a pipeline asset is Deferred
(`ShaderScriptableStripper.StripUnusedFeatures_DeferredRendering`); the editor never strips. Without the extra
Deferred renderer SSR therefore showed in the editor but would show nothing in a build. The upstream documentation
asks for exactly this renderer. Checked with two Windows player builds of a one-material probe scene, reading URP's
exported `Temp/shader-stripping.json`: without the Deferred renderer URP Lit's GBuffer pass kept 0 of 8 vertex
variants (no fragment variants); with it, 4 of 8 vertex and 32 of 40 fragment variants. It keeps the deferred shader
variants in the build, so shader compile time and build size grow (the probe scene's variants went from 1945 to 2576);
the renderer is created with the pipeline but never draws. The full player builds in phase 4 keep the GBuffer passes
of URP Lit, `BuddahGo/Lit` and the player's `DefaultSkin` (see phase 4).

**Tuning.** Object thickness was swept at 0.1, 0.25, 0.5, 0.75 and 1.0. Thin values speckle the lane's glossy side
walls and stipple the grazing reflections in the intro. 1.0 stretches the head's reflection in gameplay. 0.75 was
kept. High is cleaner than Medium in the intro and costs the same on Balanced. 75% resolution looks nearly identical
to 100% and saves 0.27 ms. Trilinear colour mipmaps (the blur for rough reflections) bled the bright player into the
lane pixels around it as a halo; without them the reflection is crisper, slightly stair-stepped at 75%, and the halo
is gone (`ssr_colour_mipmaps.jpg`), so High Fidelity runs without them. Balanced keeps them because its 50%
reflections need the blur.

**Black curtain.** The curtain is a full-screen pass at BeforeRenderingTransparents, the same event as SSR's
composite, and URP keeps same-event passes in feature order. SSR sits before BlackCurtain, so the curtain darkens the
reflections along with the rest of the frame. Placed after it, SSR would composite reflections on top of the curtain;
at full opacity there is nothing left to reflect (an editor simulation that drove the curtain material showed no
difference), but during the expand and fade-out the reflections would skip the curtain's darkening. Who sees what: the
controller disables `Volume`, and SSR with it, only for the player who sees the track edges and only during the hold;
victims keep `Volume` (and SSR) for the whole curtain but never see the edge ribbons. RenderBuddah, RenderMapEdge and
RenderTrails run after transparents and are not affected. All canvases are Screen Space Overlay; there is no UI camera.

**Forward GBuffer pass.** In Forward, SSR redraws the opaque objects with their `UniversalGBuffer` pass (URP Lit has
one) to get smoothness and normals. With MSAA (8x on High Fidelity) it uses its own depth target. Without MSAA it redraws
into the camera depth with an Equal test. On OpenGL upstream clears the camera depth and refills it only from objects
whose GBuffer pass survives, so the patch skips SSR on OpenGL entirely (WebGL defaults to Balanced; OpenGL is not a
shipped target).

### Shadow distance (approved policy)

| Asset | Before | After |
|---|---|---|
| `URP-HighFidelity` | 150 m, 4 cascades at 10/30/70 m | 220 m, splits kept at 10/30/70 m (fractions 0.0455 / 0.1364 / 0.3182) |
| `URP-Balanced` | 50 m, 1 cascade | 150 m, 2 cascades split at 30 m |

At a 10 deg sun the track's shadows are long. At 150 m the far track shadows on the sand faded out halfway up the
gameplay frame (`shadow_distance_150_vs_220.jpg`). A 10/60/120 m layout gave the player, about 98 m from the gameplay
camera, a slightly crisper shadow edge (3%), but it softened PropertySelection's shadows in its 10-24 m band, so the
approved splits were kept. MainMenu (its only caster in view is 354 m away) renders identically. PropertySelection
differs only by single-pixel jitter along shadow edges (`propertyselection_shadows.jpg`). Balanced now shows the
player's shadow at the gameplay distance (`quality_levels.jpg`). Neither change measurably costs frame time.

Saving `URP-Balanced-Renderer` also persisted URP 14's load-time migration of that older asset (SSAO sample-count
mapping and blue-noise textures, intermediate texture mode, shader references). URP applies the same migration in
memory on load, so runtime behaviour is unchanged.

### Cost

`RaceMapRenderCost`, viewpoints 01-03, 3 rounds of 200 frames, 1080p, RTX 4070 Laptop, editor, one session:

| Configuration | Median frame time |
|---|---|
| High Fidelity (8x MSAA), shadows 150 m, SSR off | 5.14 ms |
| High Fidelity, shadows 220 m, SSR off | 5.11 ms |
| High Fidelity, 220 m, SSR 100% High | 6.22 ms |
| High Fidelity, 220 m, SSR 75% High, trilinear mipmaps | 5.95 ms |
| Balanced (no MSAA), shadows 50 m, SSR off | 3.05 ms |
| Balanced, 150 m 2 cascades, SSR off | 2.89 ms |
| Balanced, 150 m, SSR 50% High (shipped) / Medium | 3.50 / 3.51 ms |
| Performant | 2.50 ms |

A before/after pair taken twice in the same session: 5.33 / 5.41 ms before phase 2 and 6.23 / 6.17 ms after, so
phase 2 costs about 0.8-0.9 ms (16%) on High Fidelity. A later run (GPU in a faster state) compared the mipmap
setting: SSR off 4.45 ms, 75% with trilinear mipmaps 5.22 ms, 75% without mipmaps (shipped) 5.16 ms. Most of SSR's cost is the extra opaque redraw and the copies,
not the ray steps (High and Medium cost the same on Balanced). The sky shader was not measured on its own; the gameplay
views show almost no sky.

### Editor trap: captures after a scripted scene reload

After a script reopens RaceMap (`EditorSceneManager.OpenScene`), the editor renders the sky's environment reflection
wrongly until a bake runs or play mode is entered and left once. The lane comes out about 20% darker, and the curbs
and shadowed sand darker too. Play mode, which reloads the domain and the scene as a build does, shows the correct
state, and so does RaceMap opened at editor start-up (checked in phase 3); a RaceMap player build was not checked, and
opening the scene by hand was not tested.
Quick check: in `01_start_grid` the lane patch x 1000-1200, y 760-860 averages about (93, 68, 78) when correct and
(76, 55, 66) in the wrong state. Ambient, probes, probe blending, reflection textures and the volume stack were identical in both states. A
first phase-2 capture taken in that state looked like a shadow-distance problem until this was found. Capture after a
bake or after a play-mode round trip.

### Review

An adversarial review of phases 1b and 2 (four lenses, then one skeptic per high or medium finding) found one high
finding: URP strips Lit's `GBuffer` pass from player builds unless a pipeline asset lists a Deferred renderer, so SSR
would have done nothing in a build. The Deferred keep-alive renderer (see Setup) fixes it, and the phase 4 builds keep
the passes. Also fixed from it: SSR in reflection-probe captures, preview cameras and OpenGL, and the per-frame writes
into the shared `.mat` (the `[BuddahGo]` patches above); the sky's sun maths, now in full float precision; an evidence
image cited for a curtain leak that it did not show (removed); the local PNG capture folders, now git-ignored; a
capture-tool comment that claimed builds had been checked; SSR evidence on the shipped 8x MSAA path; and several
statements in this note. Refuted by its skeptic: that the sky below the horizon, rendered brighter than the fog colour,
shows on screen; no view shows that part of the sky.

## Phase 3: BuddahGo/Lit for the marble and sand

Images: [phase3_compare/all.jpg](phase3_compare/all.jpg) (phase 2 left, phase 3 right) and
[phase3_evidence/](phase3_evidence/): `shadowed_reflections_sweep.jpg`, `ssr_on_buddahgo_lit.jpg`,
`occlusion_terms_intro_sand.jpg`. Local only: `phase3_after/`. The change is deliberately small: the materials look
the same, and only shadows on glossy surfaces and occluded spots differ.

### The shader

`Assets/Rendering/Shaders/BuddahGoLit.shader` (`BuddahGo/Lit`) with `BuddahGoLitInput.hlsl`, `BuddahGoLighting.hlsl`
and `BuddahGoLitForwardPass.hlsl`, hand-written URP 14 HLSL promoted from the showcase's `Showcase/Lit`. What changed
on the way to production:

- **URP Lit's full input layout.** Metallic and specular workflows, parallax, occlusion and emission, with URP Lit's
  texture and value property names (`_WorkflowMode`, `_BumpMap`, `_Smoothness`, ...), so URP Lit values carry over.
  The showcase version was metallic-only without parallax, and the curbs use the specular workflow while all three
  materials use parallax. Limits: opaque or alpha-clipped only (no transparency, detail maps or clear coat, no
  alpha-to-coverage).
- **URP 14.0.11's complete keyword set.** Soft-shadow quality levels, mixed and vertex SH evaluation, decal buffers,
  light layers, rendering layers and debug display. The showcase version declared only `_SHADOWS_SOFT`, so URP's soft
  shadow quality keywords would have fallen back to hard shadows.
- **A GBuffer pass** (URP's `LitGBufferPass.hlsl`). SSR redraws opaque objects with it in the Forward path; the
  showcase version had none, so SSR would have treated those surfaces as non-reflective.
- **Stock passes reused unchanged:** ShadowCaster, GBuffer, DepthOnly, DepthNormals, Meta. Direct lighting, light
  layers, Forward+ and the colour assembly mirror URP's `UniversalFragmentPBR`.

The indirect term has four material weights; 0 reproduces stock URP Lit for each:

| Weight | Effect |
|---|---|
| `_SpecularOcclusion` | Specular occlusion after Lagarde & de Rousiers (2014), from URP's indirect occlusion min(material AO, SSAO): rough surfaces are occluded like diffuse, smooth ones only where the view grazes an occluded area |
| `_MultiBounceAO` | Albedo-aware multi-bounce diffuse occlusion (Jimenez et al. 2016), so creases keep bounced colour |
| `_HorizonOcclusion` | Fades reflection vectors that the normal map pushes below the surface |
| `_ShadowedReflections` | New in production: darkens the environment reflection inside the main light's shadow (its shadow attenuation: realtime, plus baked shadowmask in Shadowmask or Subtractive scenes; skipped where light layers exclude the mesh). Not physical (a shadowed floor still sees the sky), but on a polished floor, whose brightness is mostly reflected sky, it is what lets a cast shadow read. A gameplay-readability control |

New materials start with the weights at 1 / 1 / 1 / 0; set all four to 0 to match stock Lit exactly. The workflow
toggle is URP's own `_WorkflowMode`; the map toggles (`_NormalMapOn`, `_ParallaxMapOn`, `_SpecGlossMapOn`,
`_OcclusionMapOn`, `_EmissionOn`) are BuddahGo's own, because URP Lit derives those keywords in its custom inspector.
`Assets/Editor/Rendering/BuddahGoLitShaderGUI.cs` draws the default inspector, carries a URP Lit material's keywords
over to these toggles when its shader is switched (checked with a material using all seven: normal, parallax,
occlusion, emission, specular map, specular workflow, alpha clip) and keeps the alpha-clip queue and RenderType tag in
step. Scripts that set the toggle floats call `BuddahGoLitShaderGUI.CarryOverKeywords` or
`MaterialEditor.ApplyMaterialPropertyDrawers` afterwards.

### The materials

| Material (`Assets/Scenes/RaceMap/Materials/`) | Copied from | Used by | Weights (specular / multi-bounce / horizon / shadowed reflections) |
|---|---|---|---|
| `RaceMap_Lane` | `Map_subsMarble_graph_0_material` | the lane `地图_2/15` and `EndField/Floor` | 1 / 1 / 1 / 0.75 |
| `RaceMap_Curb` | `Map_subsMarble 1_graph_0_material` (specular workflow) | the curb strips `Map/2` | 1 / 1 / 1 / 0 |
| `RaceMap_Sand` | `Desert_Sand 1_graph_0_material` | the sand plane `沙地` (static, lightmapped) | 1 / 1 / 1 / 0 |

The originals stay untouched: the Substance plugin's graph assets (`MapSubstances/*_graph_0/graph_0.asset`) reference
them and may regenerate them. The copies keep the Substance textures, so a re-rendered graph still updates them.

**Checks.**

- **Identity.** With all four weights at 0, the five views differ from the stock materials by at most 2 levels
  (dithering noise) and the keyword sets match exactly.
- **Shadowed reflections on the lane.** Player shadow against lane (WCAG): 1.28 at 0, 1.48 at 0.5, 1.59 at 0.75 and
  1.71 at 1.0. At 1.0 the shadowed lane turns matte, so 0.75 was kept (`shadowed_reflections_sweep.jpg`). Lane, curb
  and sand in open areas do not change.
- **Occlusion terms.** They mostly calm the sand's grazing glitter: the foreground ripple crests and the far infield
  ripples (`occlusion_terms_intro_sand.jpg`). The lane barely changes.
- **SSR.** It still reflects the player on the lane with both the no-MSAA and the 8x MSAA paths
  (`ssr_on_buddahgo_lit.jpg`).
- **Cost.** No measurable difference in two paired runs at 1080p 8x MSAA: stock 4.60 / 4.65 ms, BuddahGo/Lit
  4.52 / 4.65 ms.
- **Rebaked.** The reflection probe sees the new shading. The sand's Meta pass is unchanged, so its lightmap only
  shows bake noise.

**Water.** RaceMap has none: no object, material or shader matches water, ocean, river, lake, pool or 水, and its
renderers use only `BuddahGo/Lit`, URP Lit and the edge ribbons' `SG_MapEdge`. The showcase's `Showcase/Water` is
therefore not needed here.

**Not migrated** (outside the brief's three materials, all URP Lit): the torii arch (URP's default `Lit.mat`,
static), the plane 293 m below the map (default `Lit.mat`, static), the FBX pillar material `材质.004`, and three
`LightDebugObject` spheres (see known limits).

## Phase 4: final review, builds and tests

**Review.** An adversarial review of the whole branch (three lenses: `BuddahGo/Lit` against URP 14 Lit, phase 3
integration, docs and PR readiness; one skeptic per finding) left no high or medium finding standing. Fixed from it:
`_Metallic` lost a stray `[Gamma]`; switching a URP Lit material to `BuddahGo/Lit` kept the values but dropped the map
and workflow keywords, so the workflow toggle is now URP's `_WorkflowMode` and `BuddahGoLitShaderGUI` carries the
keywords over; specular occlusion uses the core library's guarded function; shadowed reflections respect light
layers; an empty added-component override that phase 1's save left on the `UIprefap` instance was removed; URP's
build keyword prefiltering in the two pipeline assets was committed; and several statements in this note were
corrected (material names, the ambient dip on the sunset bake, the occlusion input, the showcase status).

**Builds.** Two Windows (x64) player builds of MainMenu, PropertySelection and RaceMap:

| Build | Result | Warnings | Time |
|---|---|---|---|
| With the local showcase in `Assets/` | succeeded, 0 errors | 66 (59 from the showcase's planar-reflection compute shader) | 7 min 22 s |
| Showcase moved out of the project (the repository's tree) | succeeded, 0 errors | 7: the project's `SG_BlackCurtain` (2), URP's `StencilDeferred` (compiled because of the Deferred keep-alive renderer), the URP sample `Garden` scripts | 4 min 37 s |

URP's stripping report (`Temp/shader-stripping.json`) for the second build keeps 7609 of 17870 variants, among them
`BuddahGo/Lit` ForwardLit 48 vertex / 2176 fragment and GBuffer 16 / 256, URP Lit GBuffer 8 / 96, the player's
`DefaultSkin` GBuffer 16 / 64, every pass of the SSR shader and the sunset sky. Player size 1.18 GB; no build of `dev`
was made for comparison.

**Tests.** The EditMode suite (`BuddahGo.Tests`) ran headless in batch mode on the final tree: 314 of 314 passed, none failed or skipped. It includes the serialized-RaceMap checks (spawns, layouts).

**Not verified.** A match played in a player build (SSR, the black curtain, trails and the Feel shakers on screen),
and Balanced or Performant in a player build.

## Play-test fixes (2026-10-27)

The user play-tested the PR and sent four items: the edge line of 一念光明 faintly visible while the skill is off, no
bake for now, their saved layout change, and Buddha floating over the track after it. Commits 1008e82 (the user's own
change), 965392f, 5986813, e1a6eb4, e56b0a4 and the docs commits; 27dad27 was reverted by 21a7bd8.

### The user's layout change (1008e82)

`Map` (lane, curbs, edge ribbons, air walls) moved from y 1.37 to -1, so the lane top is at 0.25 instead of 2.62. The
sand (render plane y 0, collider top -2.33), `SpawnPoints`, the intro layouts, `MapPathLine`, the checkpoints, the
light probes and the intro cameras stayed where they were. The edge-ribbon instance `赛道重制_3_20` was renamed
`发光线条`; it holds the outer and inner effect ribbons (`特效外圈` / `特效内圈`) that 一念光明 lights up.

### 发光线条 visible while the skill is off (965392f)

Images: [fix_edge_line/skill_off_dev_before_after.jpg](fix_edge_line/skill_off_dev_before_after.jpg) (1:1 crops of
views 01-04 with the skill off: dev, this branch before the fix, after the fix, each above the line's contribution x4,
i.e. the frame minus the same frame with the ribbon renderers disabled),
[fix_edge_line/skill_active_unchanged.jpg](fix_edge_line/skill_active_unchanged.jpg) (25% fade and full visibility,
before and after) and [fix_edge_line/balanced_before_after.jpg](fix_edge_line/balanced_before_after.jpg).

**Cause.** The ribbons are always rendered; `BlackCurtainViewController` / `TrackEdgeVisibility` only drive
`_EdgeVisible` (0 hidden, 1 visible) through property blocks. `SG_MapEdge` is a transparent *Lit* graph with Alpha
blending and **Preserve Specular Lighting** on. URP 14 then blends `One, OneMinusSrcAlpha` and multiplies only the
diffuse term by alpha (`_ALPHAPREMULTIPLY_ON`, `BRDF.hlsl`), so specular, reflection-probe reflections and
`MixFog` (applied after lighting, `PBRForwardPass.hlsl`) are added at full strength when alpha is 0. A breakdown with
the fog switched off in memory: the Exp2 fog accounts for most of it in the intro and corner views, sun glints from the
low sunset sun for the rest; sky-reflection intensity hardly matters.

**New in this PR?** The mechanism is not: the graph and the toggle scripts are unchanged since 8c2d457. Its strength
is. Measured with HDR captures (skill off minus ribbons disabled, views 01-04, High Fidelity):

| State | Mean change where it shows | Peak | Pixels changed by more than 10 levels |
|---|---|---|---|
| dev 5f91e51 (its scene file opened in this project as a scratch copy) | 5-6 levels | 13-33 | 0.00-0.13% |
| This branch before the fix | 9-17 levels | 57-126 | 0.44-1.05% |
| After the fix | none (≤ 2, render noise) | 2 | 0% |

The dev numbers use dev's lights, fog (off), sky material, post volumes and Map height, but this branch's baked GI and
reflection cubemaps, which share their assets. Balanced shows the same leak and the same result after the fix.

**Fix.** Preserve Specular Lighting is off (`SrcAlpha, OneMinusSrcAlpha`, no premultiply), so everything the shader
outputs, specular and fog included, is scaled by alpha. Emission no longer multiplies by `_EdgeVisible` itself: the
alpha blend now applies it, so emission still ramps linearly with visibility as before (diffuse ramps as visibility²
both before and after). Hidden now equals "ribbons disabled" within 2 levels on High Fidelity and Balanced, and the
skill's look at full visibility is unchanged within 2 levels. Across the fade, the ribbon pixels match within 0-2 levels
at 5, 10, 25, 50 and 75% (HDR). Skill dispatch, networking, `TrackEdgeVisibility` and `BlackCurtainViewController` are
untouched. An 8-bit capture would show a slower fade-in after the fix; that is the capture's clipping, not the game.

### Buddha floating over the track (5986813, e1a6eb4, e56b0a4)

Images: [fix_buddha_ground/side_closeups_before_after.jpg](fix_buddha_ground/side_closeups_before_after.jpg) (side
camera 24 m from the base, 0.9 m above the lane: start grid at the end of the intro, 1 s after GO, 30% and 60% of the
lap) and [fix_buddha_ground/gameplay_before_after.jpg](fix_buddha_ground/gameplay_before_after.jpg) (gameplay camera).
Both come from autopilot Solo laps started from MainMenu (`A1DrivingTools.Run`: the real intro, GO handoff and
physics), with a sampler that baked the skinned mesh every 0.25 s and measured its lowest vertex against the lane
collider below it:

| Phase | Mesh base above the lane, before | After | Body origin to mesh base, before / after |
|---|---|---|---|
| Intro (kinematic, start grid) | 2.76 m | 0.009 m (range -0.07 to +0.02) | 0.289 / 0.289 m |
| Racing, after GO | 1.01 m, plus a 1.5 m drop in the first 0.5 s after GO | 0.009 m (range -0.07 to +0.02), no drop | 0.289 / 0.289 m |

The ±7 cm is the idle animation's own sway. The last column is the point of the fix: the model sits exactly where it
did relative to the body origin, so everything placed relative to the origin or to `VisualRoot` (the palm push spawn
`pushHeightOffset`, skill effects, trails, the overhead marker, the camera target) keeps its relation to the model.

**Two causes.**

1. **Collider fitted to a pose the game never shows (pre-existing, every scene).** All five Buddha clips hold the
   armature root `fotou` at local y 0.2515; `Buddah.prefab` had 0.002, and the capsule collider was fitted to that
   prefab pose (base 2 cm above the capsule bottom). Under `VisualRoot`'s x4 scale the animated model played
   (0.2515 - 0.002) x 4 = 0.998 m above its collider, so the capsule stood on the lane and the model floated. The
   effects and the push spawn were tuned in play mode, against the animated model.
   Fix (e1a6eb4): the collider moves up to the model instead of the model moving down. `Movement/Movement`
   `colliderCenter` y 2.950664 -> 3.948738 and the prefab capsule with it; `RollbackHurtbox` moves by the same 0.998;
   the prefab's `fotou` shows the clips' 0.2515, so prefab mode matches play mode; `Movement/Respawn` `heightOffset`
   3 -> 2.002 so a respawned capsule starts at the same height as before. The capsule fills the same space in the world
   as before and the centre of mass is implicit (computed from the colliders), so physics and prediction behave the
   same on server and clients; only the body origin sits 0.998 m lower inside the capsule (now 0.298 m above its
   bottom). The giant skill scales the body about its origin, so its growth now pushes the capsule up by 0.30 x (scale - 1)
   instead of 1.30 x (scale - 1).
   A first attempt (27dad27, reverted by 21a7bd8) lowered `fotou` in the clips instead; it put the model on the
   collider but left the push spawn and the effects about 1 m above where they were relative to the model.
2. **Intro layout (from the layout change).** The intro launch points and intro splines (`IntroSequenceRoot/Layout_2P..6P`)
   and `SpawnPoints` were authored at y 3.30. On the lowered track that is far above the rest height, beyond the launch
   ground snap's 1.5 m, so racers floated through the intro and dropped after GO. Fix (5986813, e56b0a4): the layouts and
   spawn points sit at Buddha's rest height, lane 0.2499 + capsule clearance 0.2979 = 0.5478, so the intro, the GO
   handoff and physics agree. At runtime `MatchSpawnManager` moves the spawn points to `Match/TrackSpawn`, which still
   said y 3.3 until the PR #64 review (M2) caught it; the table now says 0.5478 (5e20255), and
   `SixRacerFoundationTests.TrackSpawnTableKeepsTheSceneSpawnAndLaunchHeight` keeps table, scene spawns and launch points
   together. `Match/TrackResultAnchor` (the results ceremony) moved by the collider's 0.998 instead, 4.4 -> 3.401926, so
   racers land on the end field from the same height relative to the capsule as on dev (review N8). `MapPathLine` moved down by the same 2.37 m as the track, which keeps the AI wall probe and
   (with the new `heightOffset`) the respawned capsule at their old height above the lane. The intro cameras did not
   move.

Tests: the RaceMap scene, spline-tracker, launch-handoff, intro and design-table fixtures (99 tests) pass. A full
EditMode run in the open editor finished, but the MCP job tracker lost it when a play-mode test reloaded the domain, so
there is no full count for this commit set.

### No bake for now (user decision, 2026-10-27)

The realtime shadows are accepted as they are. A full lightmap bake is wanted once the whole map is finished. Known
blocker: on the development machine the GPU lightmapper skips jobs with "out of memory" while the editor holds most of
the commit charge (about 48 of 51-52 GB on 2026-10-24 and 2026-10-27). Close other memory-heavy programs or use the CPU
lightmapper for that bake. The current bake predates the layout change: the lane-centre probes are now 3.87 m above
the lane instead of 1.5 m, and the reflection probe and sky reflection still show the old track height. Run
`Generate RaceMap Light Probes` before that bake (re-tuning checklist, step 3).

## Anti-aliasing noise in game (2026-10-27)

The user reported that the in-game anti-aliasing looked noisy ("很多噪点"). Images:
[aa_fix/flicker_heatmaps.jpg](aa_fix/flicker_heatmaps.jpg) (view 01, sand ripples: frame above its flicker map, i.e.
the largest change between consecutive frames of a slow sub-pixel pan, x6; dev, before and after on both quality levels)
and [aa_fix/motion_frames.jpg](aa_fix/motion_frames.jpg) (four consecutive frames at race speed). Animated versions
are local only (`.webp` is not under Git LFS): `Logs/rendering/aa_clips/`, rebuilt by
`python -I Tools/rendering/build_aa_evidence.py` from the local captures.

**How it was measured.** `RaceMapTemporalNoise` (`Assets/Editor/Rendering/`) renders the gameplay viewpoints in play
mode, on real frames so TAA's jitter and history work as in the game, into an HDR target like the game. The camera slides
1/16 pixel per frame (shimmer) or 6 pixels per frame (race speed), with one factor changed per run;
`Tools/rendering/temporal_noise.py` scores the share of pixels that change by more than 8 levels between frames. It also
records clips of the real gameplay camera during an autopilot Solo lap (`BeginClip`).

**Attribution** (pixels flickering by more than 8 levels per frame, views 01 / 02 / 03, 1/16 px pan):

| Configuration | dev 5f91e51 | This branch |
|---|---|---|
| High Fidelity as shipped (8x MSAA + SMAA High) | 0.12 / 0.10 / 0.11% | 1.41 / 1.16 / 1.08% |
| High Fidelity, SMAA off | 0.03 / 0.03 / 0.02% | 0.11 / 0.10 / 0.12% |
| Balanced as shipped (SMAA High, no MSAA) | 0.09 / 0.09 / 0.07% | 1.20 / 0.97 / 0.90% |
| Performant as shipped (SMAA High) | not measured | 1.19 / 0.97 / 0.90% |

Removing SSR, SSAO, fog, shadows or `BuddahGo/Lit` (back to dev's materials) changed nothing (1.40-1.48% with SMAA).
Without normal maps the sand loses its ripples and the flicker drops, which locates it: SMAA crawls on the sand ripples.
SMAA at Medium and Low only reduces it (0.94% and 0.69% on Balanced, view 01). The camera's SMAA setting predates this
PR; the sunset made it visible. The grazing 10 deg sun turns the sand's normal-mapped ripples into dense, regular,
high-contrast stripes (the image is about twice as contrasty as dev's), every ripple boundary passes SMAA's edge
threshold, and the shapes SMAA reconstructs along them change as the stripes move under the pixel grid.

**Reference checked (Raphael2048/AntiAliasing, 2021, Unity 2018.4 built-in pipeline, no licence file).** It implements MSAA,
NVIDIA FXAA 3.11, a hand-written FXAA, NVIDIA SMAA, a hand-written SMAA and a simple TAA, all as `OnRenderImage` effects on
the final image. What was taken from it:
- SMAA on the display image. URP runs SMAA as the first post step on HDR colour; the reference runs it on the final
  image. A prototype ran URP's own SMAA shader after tonemapping. It crawled just as much (Balanced 1.26 / 1.01 / 0.90%,
  High Fidelity with MSAA 1.37 / 1.14 / 1.07%), so the HDR position is not the cause and the prototype was removed.
- Its TAA (Halton jitter, closest-depth motion vectors, YCoCg box clip toward the centre, history reset off screen) is
  what URP 14's built-in TAA already does, plus variance clamping and optional sharpening. The one idea URP lacks is
  leaning on the current frame as motion grows; nothing showed ghosting in these tests, so it was not needed.
- NVIDIA FXAA 3.11's quality preset is usually sharper than URP's simplified FXAA, but the file says "all rights reserved"
  with no redistribution grant, so it was not copied. The author's own code has no licence and was only read.

**Fix.** `CameraAntialiasingPolicy` (`Assets/Scripts/Rendering/`) on RaceMap's gameplay camera picks the post AA from
the quality level: none when the pipeline runs MSAA (High Fidelity: 8x MSAA alone; URP also refuses TAA with MSAA and
warns every second), TAA on Balanced, FXAA on Performant. The camera's stored mode is None. `Apply(mode)` lets a
graphics-settings layer override it. Results (same measure):

| Quality level | Before | After | Sharpness (edge mean, view 01) | Frame time, 1080p (median, views 01 / 02) |
|---|---|---|---|---|
| High Fidelity | SMAA: 1.41 / 1.16 / 1.08% | 8x MSAA alone: 0.11 / 0.10 / 0.12% | 15.6 -> 16.8 | 5.11 / 5.03 -> 4.73 / 4.89 ms |
| Balanced | SMAA: 1.20 / 0.97 / 0.90% | TAA: 0.10-0.28% over two runs | 13.5 -> 12.8 | 3.46 / 3.56 -> 3.45 / 3.60 ms |
| Performant | SMAA: 1.19 / 0.97 / 0.90% | FXAA: 0.11 / 0.10 / 0.10% | 13.9 -> 12.2 | 2.08 / 2.16 -> 1.73 / 1.78 ms |

Alternatives measured on Balanced: FXAA 0.09-0.10% (softest, 11.9, 0.3 ms cheaper), TAA with URP's sharpening (CAS
0.5) 0.14-0.22% (13.0, +0.2 ms), no AA 0.10-0.12% (jagged geometry edges). They are candidates for the graphics settings.
Frame times are editor-side, HDR target, RTX 4070 Laptop, one session, configurations interleaved.

**Not verified.** TAA on moving racers, trails and particles during a full match with skills (the autopilot lap has no
skills; static geometry showed no ghosting); Balanced and Performant in a player build; other scenes (MainMenu and
PropertySelection already use no post AA).

**Correction (2026-10-28).** The High Fidelity numbers above were measured without MSAA: URP takes a camera's MSAA
sample count from its target texture, and the probe's target had one sample. "8x MSAA alone" was really no AA, and the
frame times lacked MSAA's cost. Re-measured with the target carrying the pipeline's samples (the probe now does this),
see the next section. The SMAA attribution and the Balanced and Performant rows are unaffected.

## Play-test round 2 (2026-10-28): tearing, blurry reflections, edges in motion

The user tried the PR build and reported: anti-aliasing not good enough, edges looking pixelated at high speed; the
reflections "very, very blurry, broken up like faults"; tearing, VSync not working. Evidence:
[quality_fix/reflections_ssr_vs_planar.jpg](quality_fix/reflections_ssr_vs_planar.jpg) and
[quality_fix/aa_edges_msaa_fxaa_taa.jpg](quality_fix/aa_edges_msaa_fxaa_taa.jpg).

**What the build showed.** The player's saved window state (registry `HKCU\Software\OMCStudio\BuddahGo`) was a
1280x720 window, left there by earlier benchmark builds that share the company and product name, on a 2560x1440 143 Hz
monitor. `FrameRateLock` forces VSync off and caps at 120 fps, which tears on any display. At 720p, SSR at 75% traced at
960x540 and its reflections turned blocky; at native resolution they still speckle and break off at every SSR setting
(full resolution, colour mipmaps, 128 steps, thickness 0.5; `FreezeCompare` of one frozen race frame).

**Changes.**
- **Reflections: planar instead of SSR.** The lane top (y 0.25) and the end-field floor (y 1.8) are planes, so a mirror
  render is exact. `PlanarReflection` (`Assets/Scripts/Rendering/`) sits on both receivers; for each base game camera
  that sees them from above, a mirrored camera (oblique near plane at the receiver's top, which it reads from the
  renderer's bounds so it follows map moves; no shadows, no post-processing, renderer `URP-PlanarReflection-Renderer`)
  renders into a mip-mapped HDR texture, and `BuddahGo/Lit` with `_PLANAR_REFLECTION` samples it in screen space in place
  of the probe, blurred by roughness through the mips and bent slightly by the normal map. Only up-facing pixels at the
  plane's height take it; the lane's side walls keep the probe. SSR is off in `RaceMap_Environment_Profile`. The graphics
  setting `reflections` now chooses the planar texture's scale: off (probe), 50%, 100%.
- **Edges: FXAA on top of 8x MSAA on High Fidelity.** Measured with real MSAA (1/16 px pan, views 02 / 03), 8x MSAA does
  not lower shimmer here: 0.13 / 0.15% of pixels flicker by more than 8 levels, against 0.09 / 0.11% with no AA. MSAA
  resolves samples before tonemapping, so bright-sand-against-dark edges and the ripple bands stay stepped, and it does
  nothing for shading inside surfaces. FXAA after it: 0.05 / 0.06%, sharpness 15.1 -> 12.2 (softer). TAA without MSAA:
  0.10 / 0.13%, 12.7, and it smears in fast motion. `CameraAntialiasingPolicy` now allows FXAA on MSAA (TAA and SMAA
  still not); High Fidelity's default is MSAA 8x + FXAA, and `msaa8` alone stays a choice.
- **Tearing: VSync on.** The graphics settings set VSync per choice (default on) and the frame cap only applies with
  VSync off; window mode (borderless) and resolution (native) are applied in a player unless its command line sets the
  window, so the benchmark tools keep their 1280x720 windows. See [../graphics-settings.md](../graphics-settings.md).
- **Comparing in a build.** F9 opens a temporary graphics panel (`GraphicsSettingsDebugPanel`) until the settings menu
  exists.

**Cost** (1080p, High Fidelity with 8x MSAA, view 02, median of 4 x 40 renders, editor):

| Configuration | Frame time |
|---|---|
| Reflection probe only | 6.07 ms |
| Planar 100% | 6.73 ms |
| Planar 50% | 6.76 ms |
| Old SSR (75%, no mips) | 7.06 ms |
| Planar 100% + FXAA on MSAA (new default) | 6.92 ms |

Planar costs about 0.65 ms and is 0.3 ms cheaper than the SSR it replaces; 50% saves nothing on this GPU (the mirrored
draw, not its fill rate, dominates). With real MSAA High Fidelity costs about 1 ms more than the earlier table said.

**Not verified.** The end-field floor's reflection during the results ceremony and the intro shots; reflections of skill
effects, trails and the black curtain in a full match; VSync's effect on online prediction smoothing at refresh rates that
are not a multiple of 60 Hz (`FrameRateLock`'s original concern; Solo's presentation timeline interpolates by time);
the F9 panel's clicks also reaching gameplay input.

## Known limits and open items (current state)

- **Anti-aliasing**: TAA (Balanced) not checked on skill particles and trails in a full match; NVIDIA FXAA 3.11 would
  need a licence decision before use; an MSAA resolve after tonemapping (or a weighted resolve) would sharpen High
  Fidelity's edges without FXAA's softness, URP 14 has none.
- **Reflections**: SSR and the Deferred keep-alive renderer were removed on 2026-10-28 (the user's decision on review
  M6); a future map with glossy surfaces that are not flat would need SSR back, see the plugin's notes.
- **Graphics settings**: spec, model and API only; no menu yet (see [../graphics-settings.md](../graphics-settings.md)).

- **Lightmap bake deferred** until the whole map is finished (see [No bake for now](#no-bake-for-now-user-decision-2026-10-27)).
- **Capture tool** renders without HDR and still assumes the old track height (see How to compare).

- **Not verified by playing a match.** The WhiteBalance and curtain fixes, planar reflections under a real black
  curtain (including its fade phases) and of skill effects, the trails, FishNet making RaceMap the active scene, and the sunset in motion were checked in code,
  data, editor captures and player builds only.
- **Player shadow on the lane.** At a 10 deg sun the player's shadow is a long, thin streak across the lane, curb and
  sand. Phase 3's shadowed reflections raised it from 1.28 to 1.59 against the lane; a contact shadow would add more.
- **Track colour.** The lane (`地图_2/15`, `RaceMap_Lane`) is a polished, near-mirror dielectric
  (smoothness about 0.93) with a dark base colour (sRGB about 0.37). At sunset it reads mauve-violet, and lane/curb
  contrast is 1.88 (unchanged by phase 3). The curb strips (`Map/2`, `RaceMap_Curb`) are a rough specular-workflow surface
  (smoothness about 0.06, 4% reflectance) with a darker base (about 0.19). A lighter lane is a material (art) decision; phase 3
  kept the colours. Values (tint, smoothness, tiling) are edited on the `RaceMap_*` copies; the textures come from the
  Substance graphs in `Assets/Sources/地图/MapSubstances/`.
- **The track's own shadow.** The raised track (its top 2.62 m above the sand plane until the user lowered it to 0.25 m
  on 2026-10-27) cast a band about 15 m wide onto the sand on the side away from the sun, which read as a second edge;
  with the lowered track the band is much narrower (not re-measured). Inside the corners it shows the slab outline's straight
  segments as blocky shapes. This is geometry, traced to the lane mesh's side walls.
- **Sun disk under ACES.** The disk renders pale gold, not saturated orange. ACES desaturates bright values, so no disk
  colour reaches deep orange without a lower exposure.
- **Lens flare ghost.** The Oasis flare's ring ghost still crosses the player in the intro at 0.25 intensity. Removing
  it means editing or replacing the flare asset (shared with Oasis).
- **Horizon seam at the frame sides.** One fog colour matches the sun-side horizon; toward the edges of the intro frame,
  where the horizon is rosier, a faint seam remains, and the lane's planar reflection mirrors it.
- **Aliasing.** Specular glitter on the lane and end-field floor and moiré in the far sand come from high-frequency
  normal maps. 8x MSAA does not reach them (it resolves before tonemapping); FXAA on top halves the shimmer at some
  softness, TAA would cover them but smears at race speed (see Play-test round 2).
- **Colour grading mode.** All pipeline assets grade in low dynamic range (ACES first, then the grading LUT). HDR
  grading suits ACES better but is a shared-asset change for every scene; not changed.
- **Balanced and Performant renderers.** Balanced has SSAO; Performant has no renderer features and no
  main-light shadows. RenderBuddah, BlackCurtain, RenderMapEdge and RenderTrails exist only on High Fidelity, so those
  effects are missing on the lower levels. Players
  still render (every renderer draws all layers). This predates these phases; Standalone defaults to High Fidelity and
  no game script switches quality levels.
- **Ambient dip under the torii.** On the current sunset bake the lane-centre probes sit at a median ambient of 0.428
  (L0 luminance; 10th-90th percentile 0.419-0.435). The only dip is under the torii arch, around x 280-320, z 150-215:
  13 lane probes below 95% of the median, 4 below 85%, the lowest at 71%. The phase-1 afternoon bake had a deeper dip
  (55-70%) at the west end of the arch. If unwanted, let the track meshes contribute GI in the bake (static Contribute
  GI, Receive GI = Light Probes).
- **Sand stripes.** The regular stripes are the sand texture's tiling, unchanged (material question).
- **Debug spheres in RaceMap.** `LightDebugObject` (three 20 m spheres with `Mat_Metal`, `Mat_Gray18`, `Mat_White`)
  is active at about (320, 37, 0), near the torii, since the project's first import (8c2d457). Not touched; check
  whether any in-game camera sees it.
- **Remaining Oasis references.** `MainLight` still uses the Oasis sun lens flare asset, and the disabled `Oasis Fog`
  object keeps its component.
- **Edge ribbons and the curtain.** The edge ribbons cast no shadows. When the black curtain makes them visible they
  glow without a shadow, which suits an emissive edge.
- The lightmapper reports one object with overlapping UVs (pre-existing; the lightmapped planes use UV0).
- `Assets/Scenes/RaceMap/` still holds about 900 stale `Lightmap-*` files and `ReflectionProbe-2.exr` from older bakes;
  only `Lightmap-0..2` and `ReflectionProbe-0/1` are referenced. Not cleaned up.

## Editor and tooling traps

Traps that cost time during this work; each applies to any RaceMap or pipeline change.
- **Capture and probe targets decide HDR and MSAA.** URP takes a camera's colour format and MSAA sample count from its
  target texture, not the pipeline asset: an 8-bit target renders LDR, a one-sample target renders without MSAA.
  `RaceMapTemporalNoise` matches the pipeline's samples; `RaceMapViewpointCapture` still renders LDR (How to compare).
- **Creating or destroying any `RenderPipelineAsset` disposes the active render pipeline** (its `OnDisable` cleans the
  pipeline up), even a throwaway copy that is never assigned. Compare pipeline values by reading fields.
- **URP's stripping report** (`Temp/shader-stripping.json`, `m_ExportShaderVariants` is on in the global settings) is
  only readable after a build from an open editor: a batchmode build deletes `Temp` when it exits. Two editor builds
  give a before/after; the build log's per-pass "After scriptable stripping" lines are the fallback.
- **Do not text-edit an open scene.** The next asset refresh opens a modal "scene modified externally" dialog that
  blocks every editor automation until someone presses Ignore (keeps the editor's copy) or Reload.
- **Opening RaceMap marks it dirty** with an empty added-component override on the `UIprefap` instance (`addedObject:
  {fileID: 0}`); a save writes or drops it. Check what a dirty scene holds with `EditorSceneManager.SaveScene(scene,
  tmpPath, saveAsCopy: true)` and a diff before discarding, and keep that override out of commits.
- **Do not write a runtime `.cs` file while a lap records in play mode**: the editor recompiles and the recording harness
  dies. `A1DrivingTools.Run()` from an idle MainMenu drives a real autopilot Solo lap for checking intro, GO and visuals.
- **A player keeps its last window in the registry** (`HKCU\Software\OMCStudio\BuddahGo`), shared by every build with
  that company and product name; benchmark builds left a 1280x720 window there. The graphics settings now apply
  borderless native unless the command line sets the window.

## Tools and shaders added

| Path | Purpose |
|---|---|
| `Assets/Editor/Rendering/RaceMapViewpointCapture.cs` | Fixed-viewpoint capture with the player proxy (copies the prefab's shadow mode) |
| `Assets/Editor/Rendering/RaceMapLightProbeLayout.cs` | Track-following light probe layout |
| `Assets/Editor/Rendering/RaceMapRenderCost.cs` | Before/after frame-time measurement with the pipeline's MSAA |
| `Tools/rendering/build_racemap_compare.py` | Labelled JPEG side-by-sides |
| `Tools/rendering/build_fix_evidence.py` | The play-test fix images from the local HDR captures (inputs listed in its docstring) |
| `Assets/Editor/Rendering/RaceMapTemporalNoise.cs` | Shimmer sweeps (sub-pixel or race-speed camera motion, one factor per run), clips of the gameplay camera, HDR cost runs |
| `Tools/rendering/temporal_noise.py`, `Tools/rendering/build_aa_evidence.py` | Flicker scores and the anti-aliasing evidence images |
| `Assets/Scripts/Rendering/CameraAntialiasingPolicy.cs` | Post AA per quality level on RaceMap's gameplay camera |
| `Assets/Rendering/Shaders/SunsetSky.shader` | RaceMap's sunset sky (phase 1b) |
| `Assets/Plugins/UnitySSReflectionURP/` | Screen-space reflections (third party, MIT, phase 2); unused since 2026-10-28, kept with notes on adding it back |
| `Assets/Scripts/Rendering/PlanarReflection.cs`, `Assets/Settings/URP-PlanarReflection-Renderer.asset` | Planar reflections for flat floors and the feature-less renderer their mirror cameras use |
| `Assets/Scripts/Rendering/Settings/` | Graphics settings, including the temporary F9 panel ([../graphics-settings.md](../graphics-settings.md)) |
| `Assets/Rendering/Shaders/BuddahGoLit.shader` (+ 3 `.hlsl`) | The project's hand-written Lit (phase 3) |

## Re-tuning checklist

1. Sun direction, filter colour, colour temperature or intensity, sky material, ambient intensity, lighting
   settings: rebake (`Lightmapping.BakeAsync`, about 100 s) and save the scene; otherwise probe-lit objects keep the
   old ambient and the lane keeps the old sky reflection.
2. Sky colours are linear values; the fog colour is linearised. After a sky change: rebake, capture
   `04_intro_shot1` after the bake, then iterate the fog colour until rows 284-292 (sky) match rows 298-302 (fully
   fogged far sand), each averaged over the 32 columns x = 40, 100, ..., 1900 (multiply each channel by
   (sky / band)^1.2 and re-capture; two or three steps converge). Set the sky's below-horizon colour to
   `RenderSettings.fogColor.linear`, rebake once more and recapture. The fog's red channel is above 1 and the Lighting
   window's LDR colour picker clamps it on a manual edit, so set it from code.
3. Track spline or walls changed: run `Generate RaceMap Light Probes`, then rebake.
4. Post profiles: keep WhiteBalance and Bloom in `RaceMap_Environment_Profile` (Feel shakers), keep `Volume_Dark`'s
   tonemapping and grade in step with it, 0.1 EV darker (black curtain).
5. Reflections are planar (`PlanarReflection` on the lane and the end-field floor): if the track's floor moves, the
   plane follows the receivers' bounds; a floor that is not flat needs SSR back (plugin notes).
6. Fog, post profiles, probe intensity: runtime values, no rebake.
7. Capture `phaseN_after` after a bake or a play-mode round trip (see the editor trap), build the side-by-sides, and
   compare with the previous phase.
