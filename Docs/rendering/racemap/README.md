# RaceMap rendering: production changes

Branch `refactor/vfx-optimization` · Unity 2022.3.55f1c1 · URP 14.0.11 · started 2026-10-25.
This note tracks the phased move of the showcase look into the shipped map `Assets/Scenes/RaceMap.unity`.
The showcase itself is documented in [../showcase/README.md](../showcase/README.md).

| Phase | Scope | State |
|---|---|---|
| 1 | Lighting and environment with stock URP Lit | done, approved 2026-10-26 (afternoon checkpoint) |
| 1b | Sunset re-tune: the user asked on 2026-10-26 for the final look to read as a sunset | in progress |
| 2 | Screen-space reflections on the project renderer | not started |
| 3 | Hand-written Lit for RaceMap's materials | not started |
| 4 | Final review, docs, commit proposal | not started |

## How to compare

- `BuddahGo/Rendering/Capture RaceMap Viewpoints` (editor menu) or `RaceMapViewpointCapture.Capture(label)` renders five
  fixed viewpoints into `Docs/rendering/racemap/<label>/`. Views 01-03 use the real gameplay framing (CM_Gameplay:
  player + offset (77.34, 60, 6.6), rotation (35, 258.4, 0), FOV 40) at the start grid, a straight marble stretch and
  the sharpest corner. View 04 is intro shot CM_Shot1, the only in-game camera that shows the sky. View 05 is the
  results camera CM_End over the end field (added after the phase-0 capture, so it has no "before"). A proxy of the
  player model (baked from `Buddah.prefab` in an isolated prefab context, never instantiated with its network
  components) stands on the track; nothing is saved to the scene.
- `python -I Tools/rendering/build_racemap_compare.py <before> <after> <out>` writes labelled JPEG side-by-sides.
- `RaceMapRenderCost.Measure(viewIndex, frames)` renders one viewpoint at 1920x1080 and waits for the GPU every
  frame. Compare runs taken in the same session on the same machine; the absolute number is editor-side.

## Phase 1: lighting and environment

Images: [phase1_compare/all.jpg](phase1_compare/all.jpg) (before left, after right), per view in `phase1_compare/`.
How the look was chosen: [phase1_candidates/](phase1_candidates/) (2x2 sheets). The full-resolution PNG captures are
not committed and stay on the capturing machine (only `phase1_after/` can be regenerated from the committed scene;
the others record states that no longer exist): `phase0_before/`,
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
the same ACES and grade, 0.1 EV darker than gameplay as at HEAD (+0.31 vs +0.2), so the curtain no longer drops
tonemapping. The `Volume` GameObject also has Feel shakers, `MMWhiteBalanceShaker_URP` and `MMBloomShaker_URP`,
driven by the skill-reflection, backfire-reflection and lap feedbacks. They look up WhiteBalance and Bloom in this
profile without null checks, so both components must stay in it, and bloom keeps its HEAD values so the flashes
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
infield sand and sample the same 0.41.

**Why Baked Indirect.** Only three renderers are static (the sand plane, a plane 293 m below the map, the arch at the
lap start), so the shadowmask had almost nothing to store, and its probe occlusion is what darkened the start grid.
Realtime shadows now cover 150 m (High Fidelity); there are no baked shadows beyond that.

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
scored them; all four picked the afternoon candidate (7-7.5/10; golden hour 4-6, midday 5-7, today's look 2-5).
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

### Known limits and open items

- **Shadow distance and cascades.** High Fidelity renders shadows to 150 m in 4 cascades split at 10/30/70/150 m.
  The gameplay camera sees ground from about 70 m to 215 m, so cascades 1-3 cover nothing on screen, the player's
  shadow comes from cascade 4, and wall shadows fade out near the top of the frame. Balanced (50 m, 1 cascade) never
  shows the player's shadow at the gameplay distance (about 98 m); Performant has no main-light shadows. These are
  shared URP assets (MainMenu and PropertySelection also cast shadows with them, at close range), so nothing was
  changed. Proposal for phase 2: High Fidelity about 220 m with the near splits kept in metres, Balanced about
  150 m with 1-2 cascades.
- **End-field glint.** CM_End looks south toward the sun; the very glossy end-field floor now shows a large sun glint
  in the results shot (with the old 13 deg sun the reflection fell outside the frame). Material question for phase 3.
- **Track colour.** The lane (`地图_2/15`, `Map_subsMarble_graph_0_material`) is a polished, near-mirror dielectric
  (smoothness about 0.93) with a dark base colour (sRGB about 0.37), so at the gameplay angle it shows a dark diffuse
  layer under a sky reflection. The curb strips (`Map/2`, `Map_subsMarble 1_graph_0_material`) are a rough
  specular-workflow surface (smoothness about 0.06, 4% reflectance) with a darker base (about 0.19). Now that the
  lighting is plausible the lane reads as dark stone; a lighter or warmer lane is a material decision (phase 3).
- **Grazing reflections.** With the probe at full intensity the polished lane reflects the sky at low camera angles;
  in the intro shot the track reads slightly like water.
- **Lap-start ambient dip.** For about 40 m at the west end of the start arch the racing line gets 55-70% of the usual
  ambient (in the arch's shadow, see the probe paragraph). Plausible, not verified in a lap. If unwanted, let the track
  meshes contribute GI in the bake (static Contribute GI, Receive GI = Light Probes) so it sees the lane instead of the
  sand below it.
- **Sand stripes.** The regular stripes are the sand texture's tiling, unchanged (material question).
- **Not verified by playing.** The WhiteBalance and curtain fixes were checked in code and data, not by casting a
  reflection or a black curtain in a match; FishNet making RaceMap the active scene (ReplaceOption.All with
  `SceneManager._setActiveScene` at its default true, no override in the project) was also checked in code only.
- **Remaining Oasis references.** `MainLight` still uses the Oasis sun lens flare asset, and the disabled
  `Oasis Fog` object keeps its component; both predate this phase.
- **Edge ribbons and the curtain.** The edge ribbons no longer cast shadows. When the black curtain makes them visible
  they glow without a shadow, which suits an emissive edge; not checked in a match.
- The lightmapper reports one object with overlapping UVs (pre-existing; the lightmapped planes use UV0).
- `Assets/Scenes/RaceMap/` still holds about 900 stale `Lightmap-*` files from older bakes; only `Lightmap-0..2` are
  referenced. Not cleaned up.

### Tools added

| Path | Purpose |
|---|---|
| `Assets/Editor/Rendering/RaceMapViewpointCapture.cs` | Fixed-viewpoint capture with the player proxy |
| `Assets/Editor/Rendering/RaceMapLightProbeLayout.cs` | Track-following light probe layout |
| `Assets/Editor/Rendering/RaceMapRenderCost.cs` | Before/after frame-time measurement |
| `Tools/rendering/build_racemap_compare.py` | Labelled JPEG side-by-sides |

### Re-tuning checklist

1. Sun direction, filter colour, colour temperature or intensity, sky material, ambient intensity, lighting
   settings: rebake (`Lightmapping.BakeAsync`, about 100 s) and save the scene; otherwise probe-lit objects keep the
   old ambient.
2. Track spline or walls changed: run `Generate RaceMap Light Probes`, then rebake.
3. Post profiles: keep WhiteBalance and Bloom in `RaceMap_Environment_Profile` (Feel shakers), keep `Volume_Dark`'s
   tonemapping in step with it (black curtain).
4. Fog, post profiles, probe intensity: runtime values, no rebake.
5. Capture `phaseN_after`, build the side-by-sides, compare with `phase0_before`.
