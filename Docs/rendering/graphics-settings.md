# Graphics settings

Client-side graphics settings for BuddahGo: a preset plus per-option overrides, with tiers per option, defined in the
design tables and applied at runtime. Status (2026-10-28): the spec, the runtime model, persistence, the URP mapping,
VSync, window mode and resolution, and the API are done; there is no settings menu yet, only a temporary F9 panel. The
menu binds to `GraphicsSettingsService` (see Binding).
**Before working on graphics settings, the settings menu or effect tiers, read [Not implemented yet](#not-implemented-yet):**
the Ultra preset exists only as a masked placeholder row in `Graphics.xlsx`, and several points wait for the user.

## What the project uses (inventory)

| Knob | Where it lives | Per quality level today (Performant / Balanced / High Fidelity) | Exposed as |
|---|---|---|---|
| Quality level | `ProjectSettings/QualitySettings.asset`, one URP asset each | the three levels | the preset |
| Render scale, upscaling filter | URP asset | 100% / 100% / 100%, Auto | `render_scale`, `upscaler` |
| MSAA | URP asset | 1 / 1 / 8 | `anti_aliasing` |
| Post AA (FXAA, SMAA, TAA), TAA sharpening | camera, set by `CameraAntialiasingPolicy` | FXAA / TAA / FXAA on top of MSAA (SMAA removed, it crawls on the sand) | `anti_aliasing`, `sharpening` |
| Main-light shadows: on, distance, cascades, map size, soft | URP asset | off / 150 m, 2, 1024, soft / 220 m, 4, 4096, soft | `shadow_quality` |
| SSAO | renderer feature | none / downsampled, depth, Low / full, normals, High | `ambient_occlusion` |
| Reflections on flat floors | `PlanarReflection` on RaceMap's lane and end-field floor (SSR before 2026-10-28) | probe / 50% / 100% | `reflections` |
| Texture mip limit | `QualitySettings.globalTextureMipmapLimit` | 0 / 0 / 0 | `texture_quality` |
| Anisotropic filtering | `QualitySettings.anisotropicFiltering` | off / per texture / forced | `anisotropic_filtering` |
| VSync, frame-rate cap | `QualitySettings.vSyncCount`; `FrameRateLock` (Global/GlobalRule `target_frame_rate` 120) | VSync on; the cap applies with VSync off | `vsync`, `frame_rate_cap` |
| Window mode, resolution | `Screen.SetResolution` (player only) | borderless, native | `window_mode`, `resolution` |
| VFX | particles, VFX Graph, Feel | not tiered yet | `vfx_quality` (stub) |
| HDR, colour-grading LUT size, additional-light mode, LOD bias, skin weights | URP asset / QualitySettings | per level | fixed by the preset |
| ACES tonemapping, grade, fog colour, sky, bloom (skill flashes) | volumes, lighting settings | — | not exposed (art direction) |

## How comparable games lay it out (summary)

Sources: PCGamingWiki and publisher pages for Forza Horizon 5, F1 22/24, Trackmania, Rocket League, Need for Speed
Unbound, Hot Wheels Unleashed, Riders Republic, Genshin Impact, Fall Guys and others; Unity and URP 14 documentation and
source. Conventions taken over:
- A preset row (Low / Medium / High) plus per-option settings; changing any option shows the preset as "Custom",
  choosing a preset overwrites every option. Custom is a state, not a preset.
- Tiers named Off / Low / Medium / High, sample counts for MSAA, percentages for render scale. Render-scale steps follow
  AMD FSR's factors (59 / 67 / 77%) and avoid 95-105%, which URP snaps to 100%.
- One anti-aliasing list that mixes methods, so illegal combinations cannot be chosen (URP turns TAA off with MSAA).
- Dependencies are enforced by availability, not explained: SSAO levels are unavailable when the preset's renderer has
  no SSAO feature.
- Almost nothing needs a restart; MSAA and quality-level changes can hitch for a frame, so apply from a menu.
- Fixed-tick racers break when the frame rate is not a multiple of the tick; BuddahGo's cap stays a multiple of 60. With
  VSync on (the default since the 2026-10-27 play test showed tearing) the display's refresh paces frames instead.

## The spec (design tables)

Workbook `Assets/Design/Tables/Graphics/Graphics.xlsx` (records generated, database lists `graphicsPresets`,
`graphicsOptions`, `graphicsOptionLevels`); labels in `UI/Text` under `settings.graphics.*` (zh; en is a doc column).

| Preset | Quality level | Default |
|---|---|---|
| `low` | Performant | |
| `medium` | Balanced | |
| `high` | High Fidelity | first run without a saved choice, unless the current quality level matches another preset |

| Option (group) | Levels, low to high | Low / Medium / High preset | Values per level | Notes |
|---|---|---|---|---|
| `render_scale` (display) | p50, p59, p67, p77, p85, p100 | p100 / p100 / p100 | renderScale | |
| `upscaler` (display) | bilinear, fsr | bilinear ×3 | upscalingFilter Auto / FSR | `bilinear` is URP's Auto (the assets' value): linear, but point sampling when the screen is an exact multiple of the render size (p50 on even resolutions). FSR at 100% only sharpens; needs SM 4.5 |
| `anti_aliasing` (quality) | off, fxaa, taa, msaa2, msaa4, msaa8, msaa8_fxaa | fxaa / taa / msaa8_fxaa | msaaSamples, postAntialiasing | TAA never runs with MSAA; FXAA may (it halves the shimmer MSAA leaves, see the RaceMap notes) |
| `sharpening` (quality) | off, low, high | off ×3 | taaSharpening 0 / 0.4 / 0.8 | only with TAA |
| `shadow_quality` (quality) | off, low, medium, high | off / medium / high | on, distance, cascades, splits (m), map size, soft | low = 100 m, 1 cascade, 1024, hard |
| `texture_quality` (quality) | quarter, half, full | full ×3 | textureMipmapLimit 2 / 1 / 0 | |
| `anisotropic_filtering` (quality) | off, per_texture, forced | off / per_texture / forced | Disable / Enable / ForceEnable | |
| `reflections` (effects) | off, medium, high | off / medium / high | planarScale 0 / 0.5 / 1 | planar reflections on floors with a `PlanarReflection` component; off leaves the reflection probe |
| `ambient_occlusion` (effects) | off, low, high | off / low / high | ssaoEnabled, downsample, normals, samples | not available on low (no SSAO feature) |
| `vfx_quality` (effects) | low, medium, high | low / medium / high | vfxTier | stub, see VFX hook |
| `vsync` (performance) | off, on | on ×3 | vSyncCount 0 / 1 | on: frame rate follows the display, the cap is ignored |
| `frame_rate_cap` (performance) | fps60, fps120, fps240 | fps120 ×3 | frameRate | only with VSync off; multiple of the 60 Hz tick; the default must equal Global/GlobalRule `target_frame_rate` |
| `window_mode` (display) | fullscreen, borderless, windowed | borderless ×3 | fullScreenMode | player only; skipped when the command line sets the window (`-screen-width`, `-screen-height`, `-screen-fullscreen`, `-window-mode`, `-popupwindow`) |
| `resolution` (display) | native | native ×3 | — | the display's resolution; same skip rule |

No option needs a restart. Every preset's pipeline defaults equal its quality level's asset, so choosing a preset
without overrides uses the real assets (tested: `EachPresetDefaultReproducesItsQualityLevel`). The import rule
`GraphicsSettingsAreComplete` (`DesignTableDomainRules`) rejects a preset without exactly one default per option, TAA on
an MSAA level, a frame rate that is not a multiple of 60, a default frame rate other than GlobalRule
`target_frame_rate` (FrameRateLock's value, which the settings replace at startup), cascade splits that do not match the
cascade count, do not increase or reach the shadow distance, a planar scale outside 0-1, and unknown enum-like values.

## Runtime (`Assets/Scripts/Rendering/Settings/`)

| Type | Role |
|---|---|
| `GraphicsSettingsCatalog` | Options, levels, presets and their label keys from the tables; an empty catalog means the layer does nothing |
| `GraphicsSettingsModel` | Preset plus overrides; Get / Set / SetPreset / ResetToPreset; "Custom" while any override exists; JSON state |
| `PlayerPrefsGraphicsSettingsStore` | Saves the JSON under `BuddahGo.GraphicsSettings.v1` on this machine |
| `GraphicsSettingsApplier` | Applies the choice: quality level, a runtime clone of that level's URP asset (and its renderer data and features) only when a value differs, camera AA and sharpening through `CameraAntialiasingPolicy`, planar reflection scale, texture limit, anisotropic filtering, VSync or the frame-rate cap, window mode and resolution (player only), VFX tier |
| `GraphicsPipelineMapper` | Option levels -> URP asset fields and SSAO feature settings (internal URP 14 setters and the SSAO settings through their serialized fields) |
| `GraphicsSettingsService` | Static entry point for the menu; loads and applies the saved choice after the first scene loads |
| `GraphicsSettingsDebugPanel` | Temporary: F9 in any scene opens an IMGUI panel listing presets and options; each change applies and saves. Remove or limit to development builds when the menu ships |
| `BuddahGo.Rendering.PlanarReflection` | On a flat floor's renderer: renders the mirrored scene for each game camera and binds it to the `BuddahGo/Lit` material (`_PLANAR_REFLECTION`); `SettingsScale` comes from `reflections` |
| `BuddahGo.Rendering.VfxQuality` | VFX tier hook: `Current`, `Changed` |

**Assets are never written.** Values change only on runtime clones (`HideFlags.DontSave`, made and destroyed by
`GraphicsPipelineMapper.CreateDetachedClone` / `DestroyDetachedClone`), assigned through `QualitySettings.renderPipeline`.
Pipeline, texture limit, anisotropic filtering and VSync belong to the current quality level, so the applier records what
each level held before its first write. In the editor those writes would outlive play mode: they are put back on
`Application.quitting`, which fires when play mode ends, and the start quality level is selected again. A recompile
during play mode drops that subscription; the record is also kept in `SessionState`, and the editor restores from it
when it is back in edit mode. Verified in play mode (each level points at its real asset again, `git status` clean) and
by `RestorePutsBackEveryQualityLevelItChanged`. Because the default preset uses the real assets, editing a URP asset
during play mode still shows up unless a custom choice is applied.

**Never networked.** Nothing here touches FishNet, RPCs, prediction or server authority. `vfx_quality` is client-only,
so effects that carry gameplay information must not depend on it.

## Binding (for the settings menu)

```csharp
using BuddahGo.Rendering.Settings;

foreach (var option in GraphicsSettingsService.Catalog.Options)   // LabelKey, GroupId, RestartRequired, Levels
    foreach (var level in option.Levels)                            // Id, LabelKey
    {
        bool selectable = GraphicsSettingsService.IsAvailable(option.Id, level.Id);
        bool selected = GraphicsSettingsService.Get(option.Id) == level.Id;
    }
string presetLabel = DesignTableOverlay.Text(GraphicsSettingsService.PresetLabelKey, "Custom");
GraphicsSettingsService.Set("anti_aliasing", "taa");   // or SetPreset("medium"), ResetToPreset()
GraphicsSettingsService.Apply();                        // applies and saves; Revert() reloads the saved (or startup) choice
GraphicsSettingsService.Changed += Refresh;             // model changed; Applied fires after Apply/Revert
```

Still to do on the UI side: the menu itself (owned by the UI work, not by `feat/ingame-ui-updates`, which has no
settings screen); a resolution list from `Screen.resolutions` (the table has only `native`); an Apply button, because
MSAA and quality-level changes can hitch; greying out `upscaler` at 100% and `sharpening` without TAA; checking that
the new Chinese labels exist in the static font subset `Assets/Fonts/SoloCJK`. On 2026-10-27, 36 characters of the
`settings.graphics.*` labels, placeholders included, were missing from it
(义全原双口各垂境屏帧异式恢抗撤染框模渲滤率环窗蔽质贴辨边遮销锐锯阴限预齿); regenerate the subset and rebake its TMP
atlas as its README describes before the menu shows them.

## Not implemented yet

What exists only as a placeholder or is not verified (2026-10-28). Keep this list current: remove an entry when it
lands, add one when something is deferred. Window mode, resolution (native) and VSync were placeholders on 2026-10-27
and are implemented since 2026-10-28.

**How placeholders are stored.** A row with `##` in column A of `Graphics.xlsx` is masked, the project-wide convention
(see `Match/SelectionStage`): the importer skips it, the CSV mirror omits it, so the runtime and a menu never see it.
Every Graphics sheet has a `pending` doc column that says what is missing or undecided, also on implemented rows that
still have an open point. To implement a placeholder: add the typed value columns it needs, unmask the rows, then follow
[Adding an option](#adding-an-option).

| Item | State | Placeholder | What it needs |
|---|---|---|---|
| Settings menu | not implemented; F9 debug panel stands in | API `GraphicsSettingsService`; labels `settings.graphics.title`, `settings.graphics.action.apply` / `revert` / `reset_to_preset` in `UI/Text` | a screen that binds as in [Binding](#binding-for-the-settings-menu), an Apply button, the font subset; then remove `GraphicsSettingsDebugPanel` |
| Resolution list | only `native` | `resolution` option with one level | levels built at runtime from `Screen.resolutions` |
| VSync online | on by default, decided 2026-10-28 | `pending` on `vsync` | check online prediction smoothing at refresh rates that are not a multiple of 60 Hz (`FrameRateLock`'s concern); Solo interpolates by time |
| Ultra preset | **user decision** | masked `ultra` preset (High Fidelity base) | a default level for every option (`defaultFor` gains `ultra`) |
| VFX tiers | hook only | `vfx_quality` is applied; `VfxQuality.Current` / `Changed` have no reader | **user** specifies what each tier changes; effects then read the tier |
| First-run preset | not implemented | `pending` on the `high` preset | hardware detection; today the preset of the starting quality level is used |
| Font subset | missing glyphs | — | regenerate `Assets/Fonts/SoloCJK` for the 36 characters listed under Binding |
| NVIDIA FXAA 3.11 | **user decision** | `pending` on `anti_aliasing` | licence check before using it in place of URP's FXAA |

Not verified yet:
- TAA (medium preset) on skill particles and trails in a full match.
- FSR 1.0 in a player build (URP may strip the variants it needs).
- The per-level restore after the code review in real play mode (covered by `RestorePutsBackEveryQualityLevelItChanged`),
  and the restore after a recompile during play mode.
- Planar reflections on the end-field floor during the results ceremony, and of skill effects in a full match.
- Window mode and resolution in a player (EditMode tests cover only the command-line skip).

## Adding an option

1. Add a row to `Graphics/GraphicsOption` (`optionId`, `groupId`, `labelKey`, `restartRequired`) and its levels to
   `GraphicsOptionLevel`, with `defaultFor` naming each preset exactly once across the option's levels. Add the label keys
   to `UI/Text`.
2. If the option needs values no column holds, add typed columns to `GraphicsOptionLevel` (each option reads only its
   own columns), then run `python Tools/design-tables/gen_records.py .` and let Unity recompile and import.
3. Apply it: an option id constant in `GraphicsPipelineMapper` and its handling there (URP asset or renderer feature, on
   the clone) or in `GraphicsSettingsApplier.Apply` (QualitySettings, camera, other systems). Values that differ from the
   asset must make `DiffersFrom` return true.
4. Extend `GraphicsSettingsAreComplete` for its values, and `GraphicsSettingsTests` (the default-preset test catches a
   default that does not match the quality level).
5. Check the build: a toggle must be on in at least one shipped URP asset and off in another, or URP strips the variants
   it needs (FSR in a player build is not verified yet).

## Open decisions

- **Ultra preset** above High Fidelity (for example a longer shadow distance and a render scale above 100%): possible
  with the clone approach; a masked placeholder row exists.
- **VFX tiers**: what Low / Medium / High change, and whether to add a "reduce other players' effects" option
  (Genshin Impact's precedent).
- **First-run preset**: currently the quality level the game starts with; hardware detection is not implemented.
