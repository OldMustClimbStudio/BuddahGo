# RaceMap rendering: current state

RaceMap (`Assets/Scenes/RaceMap.unity`) carries the dusk look C2 with a hand-written sky, `BuddahGo/Lit` on marble and
sand, planar reflections and a scripted bake. Agents start at [../HARNESS.md](../HARNESS.md); every module, its
requirement and dependencies are in [../ARCHITECTURE.md](../ARCHITECTURE.md). How it got here, phase by phase, with the
measurements and reviews: [history.md](history.md). The colour study behind C2: [dusk-color-study.md](dusk-color-study.md).

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
## Known limits and open items

- **Anti-aliasing**: TAA (Balanced) not checked on skill particles and trails in a full match; NVIDIA FXAA 3.11 would
  need a licence decision before use; an MSAA resolve after tonemapping (or a weighted resolve) would sharpen High
  Fidelity's edges without FXAA's softness, URP 14 has none.
- **Reflections**: SSR and the Deferred keep-alive renderer were removed on 2026-10-28 (the user's decision on review
  M6); a future map with glossy surfaces that are not flat would need SSR back, see the plugin's notes.
- **Menu lighting assets** (decided 2026-11-03): the menu follows the dusk look. It references RaceMap's
  `RaceMap_Sky_Dusk.mat`, `RaceMap_Dusk_Profile.asset`, Trilight ambient, fog and sun colour treatment (its own sun
  direction and intensity), written by `MenuEnvironmentImporter`. The pre-dusk `RaceMap_Sky.mat` and
  `RaceMap_Environment_Profile.asset` are referenced by no scene any more (retire in a later pass). The menu got
  about 0.25 EV darker with the shared profile (exposure +0.05 instead of +0.3); not compensated.
- **Graphics settings**: spec, model, API and a first main-menu panel (2026-11-03); see [../graphics-settings.md](../graphics-settings.md).

- **Lightmap bake (done 2026-11-03, later the same day).** Once the machine had memory again the Progressive GPU
  bake ran in about 40 s: light probes, the two lightmapped planes and the sky reflection from the C2 dusk sun and
  sky. The first result was all-orange again (start-grid chroma 31: the 2900 K sun bouncing off the planes dominates
  the probes), so the Trilight ambient went cooler, sky (0.50, 0.62, 0.90) / equator (0.50, 0.50, 0.52) / ground
  (0.18, 0.16, 0.16), which lands at chroma 24 (old pre-dusk probes: 21). The zen landscape and track still have no
  lightmap UVs (`uv2` empty) and only reflect; Contribute GI on them needs Generate Lightmap UVs on the FBX imports
  first (expect many 2048 atlases at 8 texels/unit; use a low Scale in Lightmap). Traps: the lightmapper reports
  `out of memory` when the system commit charge is near its limit, whatever the scene holds; a cancelled bake
  detaches the scene's LightingData (light probes vanish, the frame warms up): reassign
  `Lightmapping.lightingDataAsset`. Options sheet: [dusk_study/bake_options.jpg](dusk_study/bake_options.jpg),
  before/after [dusk_bake_compare/all.jpg](dusk_bake_compare/all.jpg).
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
- **Dusk colour (2026-11-03 study).** Still open after C2: the Buddha against the lane in the corner view stays at
  dE 4-6 whatever the grade (lit like the lane; needs a material or contact-shadow measure), the palm-release slot does
  not draw in edit-mode captures (cause unknown), and the origin of the warm ambient probe L0 (0.285, 0.173, 0.127)
  was not verified. Details and the A2/B2 alternatives in [dusk-color-study.md](dusk-color-study.md).
- **Horizon seam at the frame sides.** One fog colour matches the sun-side horizon; toward the edges of the intro frame,
  where the horizon is rosier, a faint seam remains, and the lane's planar reflection mirrors it.
- **Aliasing.** Specular glitter on the lane and end-field floor and moiré in the far sand come from high-frequency
  normal maps. 8x MSAA does not reach them (it resolves before tonemapping); FXAA on top halves the shimmer at some
  softness, TAA would cover them but smears at race speed (see [history](history.md#play-test-round-2-2026-10-28-tearing-blurry-reflections-edges-in-motion)).
- **Colour grading mode.** All pipeline assets grade in low dynamic range (ACES first, then the grading LUT). HDR
  grading suits ACES better but is a shared-asset change for every scene; not changed.
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
  only `Lightmap-0..2` and `ReflectionProbe-0/1` are referenced. Not cleaned up. (Since bake 9dc20717 only
  `Lightmap-0` and `ReflectionProbe-0` are bake outputs; the rest is listed in [../retired.md](../retired.md).)

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
| `Assets/Editor/Rendering/RaceMapDuskColorStudy.cs` | Dusk colour study probe: HDR captures of the five viewpoints with temporary overrides of sun / fog / sky / ambient / probes / post (tokens in the file header), material-id mask, skill VFX on the proxy; nothing saved |
| `Assets/Editor/Rendering/RaceMapTemporalNoise.cs` | Shimmer sweeps (sub-pixel or race-speed camera motion, one factor per run), clips of the gameplay camera, HDR cost runs |
| `Tools/rendering/temporal_noise.py`, `Tools/rendering/build_aa_evidence.py` | Flicker scores and the anti-aliasing evidence images |
| `Assets/Scripts/Rendering/CameraAntialiasingPolicy.cs` | Post AA per quality level on RaceMap's gameplay camera |
| `Assets/Rendering/Shaders/SunsetSky.shader` | RaceMap's sunset sky (phase 1b) |
| `Assets/Plugins/UnitySSReflectionURP/` | Screen-space reflections (third party, MIT, phase 2); unused since 2026-10-28, kept with notes on adding it back |
| `Assets/Scripts/Rendering/PlanarReflection.cs`, `Assets/Settings/URP-PlanarReflection-Renderer.asset` | Planar reflections for flat floors and the feature-less renderer their mirror cameras use |
| `Assets/Scripts/Rendering/Settings/` | Graphics settings, including the temporary F9 panel ([../graphics-settings.md](../graphics-settings.md)) |
| `Assets/Rendering/Shaders/BuddahGoLit.shader` (+ 3 `.hlsl`) | The project's hand-written Lit (phase 3) |
| `Assets/Editor/Rendering/RenderingBake.cs` | The scripted, recorded bake of RaceMap and the menu, its Verify and the bake manifest (2026-11-05, ARCHITECTURE "Bake reproducibility") |
| `Tools/rendering/capture_diff.py` | Per-view difference statistics of two capture folders and a before / after / difference sheet |

## Re-tuning checklist

1. Sun direction, filter colour, colour temperature or intensity, sky material, ambient, lighting settings: record the
   value in `RenderingBake` (the sun also in the scene), then `BuddahGo/Rendering/Bake/Bake RaceMap + Menu` (about 40 s
   plus the probes; it saves both scenes and writes the manifest); otherwise probe-lit objects keep the old ambient and
   the lane keeps the old sky reflection. (Before 2026-11-05: `Lightmapping.BakeAsync` by hand.)
2. Sky colours are linear values; the fog colour is linearised. After a sky change: rebake, capture
   `04_intro_shot1` after the bake, then iterate the fog colour until rows 284-292 (sky) match rows 298-302 (fully
   fogged far sand), each averaged over the 32 columns x = 40, 100, ..., 1900 (multiply each channel by
   (sky / band)^1.2 and re-capture; two or three steps converge). Set the sky's below-horizon colour to
   `RenderSettings.fogColor.linear`, rebake once more and recapture. The fog's red channel is above 1 and the Lighting
   window's LDR colour picker clamps it on a manual edit, so set it from code (`RenderingBake.FogColor`, written to
   both scenes by the bake).
3. Track spline or walls changed: run `Generate RaceMap Light Probes`, then rebake.
4. Post profiles: keep WhiteBalance and Bloom in `RaceMap_Dusk_Profile` (`RaceMap_Environment_Profile` until the dusk; Feel shakers), keep `Volume_Dark`'s
   tonemapping and grade in step with it, 0.1 EV darker (black curtain).
5. Reflections are planar (`PlanarReflection` on the lane and the end-field floor): if the track's floor moves, the
   plane follows the receivers' bounds; a floor that is not flat needs SSR back (plugin notes).
6. Fog, post profiles, probe intensity: runtime values, no rebake.
7. Capture `phaseN_after` after a bake or a play-mode round trip (see the editor trap), build the side-by-sides, and
   compare with the previous phase.
