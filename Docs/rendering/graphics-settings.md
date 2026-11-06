# Graphics settings

Client-side graphics settings for BuddahGo: a preset plus per-option overrides, with tiers per option, defined in the
design tables and applied at runtime. Status (2026-11-05): the spec, the runtime model, persistence, the URP mapping,
VSync, window mode, a resolution list from the display, VFX tiers that change what effects draw, the API, a settings
panel in the main menu with restart tags and dimmed rows, and the module contract (every quality level renders the
gameplay passes) are done; the F9 panel stays as the in-race fallback. The menu binds to `GraphicsSettingsService`.

![The settings panel](graphics-settings-panel.jpg)
Re-verified on 2026-11-03 in play mode after the dusk re-tune and the bake: presets switch the quality level and its
real URP asset, overrides run on a runtime clone, Reset puts the real asset back, and every quality level points at its
own asset again after play mode (`GraphicsSettingsTests` 10/10).
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
| Window mode, resolution | `Screen.SetResolution` (player only) | borderless, native | `window_mode`, `resolution` (plus one level per display size, generated at startup) |
| VFX | slot layers and their particle systems (`SkillVfxPhaseController` / `SkillVfxLayer`), the 如来神掌 ink smoke (`InkFluidVolume`), real-time lights in slots; see [VFX tiers](#vfx-tiers) | Low / Medium / High | `vfx_quality` |
| Gameplay passes (curtain, racers, edges, trails, skill VFX) | renderer features on every level's default renderer ([Modules](#modules)) | the same on all three since 2026-11-05 | never exposed (gameplay) |
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

| Option (group) | Levels, low to high | Low / Medium / High preset | Values per level | Notes | Restart |
|---|---|---|---|---|---|
| `render_scale` (display) | p50, p59, p67, p77, p85, p100 | p100 / p100 / p100 | renderScale | | no, live (checked 2026-11-05) |
| `upscaler` (display) | bilinear, fsr | bilinear ×3 | upscalingFilter Auto / FSR | `bilinear` is URP's Auto (the assets' value): linear, but point sampling when the screen is an exact multiple of the render size (p50 on even resolutions). FSR at 100% only sharpens; needs SM 4.5 | no, live (checked 2026-11-05) |
| `anti_aliasing` (quality) | off, fxaa, taa, msaa2, msaa4, msaa8, msaa8_fxaa | fxaa / taa / msaa8_fxaa | msaaSamples, postAntialiasing | TAA never runs with MSAA; FXAA may (it halves the shimmer MSAA leaves, see the RaceMap notes) | no, live (checked 2026-11-05) |
| `sharpening` (quality) | off, low, high | off ×3 | taaSharpening 0 / 0.4 / 0.8 | only with TAA | no, live (checked 2026-11-05) |
| `shadow_quality` (quality) | off, low, medium, high | off / medium / high | on, distance, cascades, splits (m), map size, soft | low = 100 m, 1 cascade, 1024, hard | no, live (checked 2026-11-05) |
| `texture_quality` (quality) | quarter, half, full | full ×3 | textureMipmapLimit 2 / 1 / 0 | | no, live (checked 2026-11-05) |
| `anisotropic_filtering` (quality) | off, per_texture, forced | off / per_texture / forced | Disable / Enable / ForceEnable | | no, live (checked 2026-11-05) |
| `reflections` (effects) | off, medium, high | off / medium / high | planarScale 0 / 0.5 / 1 | planar reflections on floors with a `PlanarReflection` component; off leaves the reflection probe | no, live (checked 2026-11-05) |
| `ambient_occlusion` (effects) | off, low, high | off / low / high | ssaoEnabled, downsample, normals, samples | not available on low (no SSAO feature) | no, live (checked 2026-11-05) |
| `vfx_quality` (effects) | low, medium, high | low / medium / high | vfxTier and the vfx* columns ([VFX tiers](#vfx-tiers)) | applies to the next effect that starts (the ink budget at once); gameplay-information layers stay readable | no, live (checked 2026-11-05) |
| `vsync` (performance) | off, on | on ×3 | vSyncCount 0 / 1 | on: frame rate follows the display, the cap is ignored | no, live (checked 2026-11-05) |
| `frame_rate_cap` (performance) | fps60, fps120, fps240 | fps120 ×3 | frameRate | only with VSync off; multiple of the 60 Hz tick; the default must equal Global/GlobalRule `target_frame_rate` | no, live (checked 2026-11-05) |
| `window_mode` (display) | fullscreen, borderless, windowed | borderless ×3 | fullScreenMode | player only; skipped when the command line sets the window (`-screen-width`, `-screen-height`, `-screen-fullscreen`, `-window-mode`, `-popupwindow`) | no, live in Unity (not checked in a player) |
| `resolution` (display) | native, then one level per display size (`1920x1080`, ...) | native ×3 | — / parsed from the level id | `native` follows the display; the sizes come from `Screen.resolutions` at startup (`GraphicsSettingsCatalog.WithDisplayResolutions`), largest first, no label key (the id is shown); a saved size the display no longer offers is dropped; same skip rule | no, live in Unity (not checked in a player) |

**Restart.** `GraphicsOption.restartRequired` marks an option that takes effect only after a restart; the panel then
shows "需要重启" after its name and, after Apply, the notice "标有“需要重启”的设置将在重启游戏后生效" until the next
start (`GraphicsSettingsService.PendingRestart`). On 2026-11-05 every option was stepped through every level of every
preset in play mode and read back on the same frame (pipeline values on the clone, SSAO feature state, camera AA and
sharpening, planar scale, mip limit, anisotropic filtering, VSync, frame cap, VFX tier); texture quality was also
checked on the rendered image (edge energy 0.0138 at full, 0.0109 at quarter, back to 0.0138). All apply live, so no
option is flagged; window mode and resolution are only applied in a player and were not checked there.
`GraphicsSettingsRulesTests.RestartFlagsMatchThePlayModeCheck` pins the flags to that check.

**Greyed out.** `GraphicsSettingsService.IsActive` (used by the panel and the F9 panel): `upscaler` at 100 % render
scale, `sharpening` without TAA, `frame_rate_cap` with VSync on (`GraphicsOptionDependencies`), an option whose preset
allows only one level (SSAO on the low preset: no SSAO feature), and window mode / resolution where the applier leaves
the window alone (the editor, a command line that sets the window). The value is kept and applies again once the other
option changes.

Every preset's pipeline defaults equal its quality level's asset, so choosing a preset
without overrides uses the real assets (tested: `EachPresetDefaultReproducesItsQualityLevel`). The import rule
`GraphicsSettingsAreComplete` (`DesignTableDomainRules`) rejects a preset without exactly one default per option, TAA on
an MSAA level, a frame rate that is not a multiple of 60, a default frame rate other than GlobalRule
`target_frame_rate` (FrameRateLock's value, which the settings replace at startup), cascade splits that do not match the
cascade count, do not increase or reach the shadow distance, a planar scale outside 0-1, unknown enum-like values, and a
vfx_quality level with a blank vfx* column (a blank reads as 0).

## Modules

Each rendering module can be turned off or removed without breaking another; the contract is code
(`BuddahGo.Rendering.RenderingModules`) and tests (`RenderingModulesTests`).

| Module | Toggle | Depends on / what breaks without it |
|---|---|---|
| Gameplay renderer features: `BlackCurtain`, `RenderBuddah`, `RenderMapEdge`, `RenderTrails`, `RenderSkillVfx` | none: on every level's default renderer (index 0), active, in this order after the cosmetic features | carry or protect gameplay information. Until 2026-11-05 only High Fidelity had them, so on the low and medium presets a 一念光明 victim saw no curtain. `Tools/BuddahGo/Rendering/Sync Gameplay Passes` (`RendererGameplayPasses`) copies them from `URP-HighFidelity-Renderer`; a startup check logs a warning when a level breaks the contract |
| Gameplay layers `Buddah` (6), `BlackCurtainTrail` (7), `TrackEdge` (8) | — | kept out of every default renderer's transparent pass; the gameplay passes draw them after the curtain (racers opaque, effects, trails and edges transparent) |
| 一念光明 curtain (`BlackCurtainRenderFeature`) | plays per camera while `BlackCurtainScreenEffect` runs | runs at AfterRenderingTransparents, before the other gameplay passes at that event, so it covers the whole map, transparent scenery included (the landscape's contact-shadow decals showed through while it ran before the transparent pass). Who sees what stays with `BlackCurtainViewController`: the caster sees the edges and the racers, a victim sees the red trails and only itself; the anti skill swaps the two |
| SSAO | `ambient_occlusion` (feature on High Fidelity and Balanced; none on Performant) | cosmetic; an absent feature greys the option out |
| Planar reflections (`PlanarReflection`) | `reflections` (scale 0 = off, the reflection probe stays) | mirror cameras use `URP-PlanarReflection-Renderer` (index 1) when listed, else the default renderer; the curtain is per camera, so a mirror never draws it |
| Camera AA (`CameraAntialiasingPolicy`) | `anti_aliasing`, `sharpening` | never SMAA or TAA on top of MSAA |
| `OccluderFade` | component on RaceMap's occluders | client-side, independent of the pipeline |
| Post volumes and Feel shakers | volume profiles (not a setting) | Feel's URP shakers (WhiteBalance, Bloom, ColorAdjustments on RaceMap's volumes) take their component from the profile in Awake without a null check; `RenderingModulesTests.PostVolumesHoldWhatTheirFeelShakersDrive` checks every build scene |

![一念光明 after the fix: caster, victim and anti views on every quality level](graphics-settings/curtain_roles_after.jpg)

Cost of the passes added to Balanced and Performant (RaceMap start grid, 1920x1080, six racers in view, editor, median
of 200 frames, two runs): the four always-on passes stay inside the bench's noise of about 0.5 ms (Performant 3.33 /
2.83 ms before, 3.89 / 3.53 and 3.24 / 3.55 after; Balanced 4.85 / 4.62 before, 5.27 / 4.93 and 4.59 / 4.90 after); the
curtain costs 0.3-0.7 ms while it plays (two full-screen blits). Editor numbers, relative only.

## VFX tiers

`vfx_quality` sets `VfxQuality.Current` and `VfxQuality.Settings` (`VfxTierSettings`, from the level's vfx* columns;
without tables `VfxTierSettings.Fallback` keeps the pre-table layer rules). Proposed defaults (2026-11-05, stated
assumptions for the user to approve or adjust):

| Column | Low | Medium | High | Reader |
|---|---|---|---|---|
| `vfxKeepPriority` (highest slot-layer priority kept) | 1 | 2 | 3 | `SkillVfxRules.Keeps`; gameplay layers always kept |
| `vfxEmissionScale` (detail systems on every layer; all systems of cosmetic layers) | 0.4 | 0.7 | 1 | `SkillVfxLayer.Initialize` |
| `vfxGameplayEmissionScale` (shape systems of gameplay layers; range 0.7-1) | 1 | 1 | 1 | same |
| `vfxRealtimeLights` | off | on | on | `SkillVfxRules.Keeps` (Light function) |
| `vfxTrailScale` (cosmetic layers' own trails; gameplay and route trails unchanged) | 0.5 | 0.75 | 1 | `SkillVfxRules.TrailSeconds` |
| `vfxInkBudget` (如来神掌 ink-smoke simulations at once, nearest first) | 2 | 4 | 8 | `InkFluidVolume.PickSimulating`, every frame |
| `vfxInkCellScale` (ink grid: same box, cells this much larger) | 1.5 | 1.25 | 1 | `InkFluidVolume.TierGrid`, when a palm spawns |

Why detail systems: every slot shipped so far (14 in `SkillVfxDatabase`) is a single layer marked `gameplayInfo`, so
dropping layers by priority changed nothing. A layer's particle systems are now split by what they draw: a system that
emits more than 4 particles per cycle (`SkillVfxRules.DetailParticleThreshold`; sparks, flecks, smoke puffs, leaves) is
detail and follows `vfxEmissionScale`; one that draws 1-4 large sprites carries the effect's shape and keeps
`vfxGameplayEmissionScale` on gameplay layers; a scaling zone's `areaSystems` (禅定缓流's slow zone) always count as
shape. Every slot with particles keeps at least one shape system (`VfxTierTests`). Measured on a bench (same camera,
1/30 s steps, same seed; live particles at 0.4 s, Low / Medium / High): 神足通 34 / 52 / 72, 如来神掌 release 126 / 208 /
292, 禅定缓流 56 / 74 / 93 with the zone itself unchanged, 通用前摇 26 / 44 / 62. Ink grid on Low: 56×56×128 cells of 3
instead of 80×80×192 of 2, about a third of the simulation cost (measured in
[push-projectile-hands.md](../vfx/push-projectile-hands.md) section 15.1). Soft particles and screen distortion have no
user in the shipped effects, so they have no tier.

![VFX tiers at 0.4 s, Low / Medium / High](graphics-settings/vfx_tiers_t040.jpg)

## Runtime (`Assets/Scripts/Rendering/Settings/`)

| Type | Role |
|---|---|
| `GraphicsSettingsCatalog` | Options, levels, presets and their label keys from the tables; an empty catalog means the layer does nothing. `WithDisplayResolutions` appends the display's sizes as levels of `resolution` |
| `GraphicsSettingsModel` | Preset plus overrides; Get / Set / SetPreset / ResetToPreset; "Custom" while any override exists; JSON state |
| `PlayerPrefsGraphicsSettingsStore` | Saves the JSON under `BuddahGo.GraphicsSettings.v1` on this machine |
| `GraphicsSettingsApplier` | Applies the choice: quality level, a runtime clone of that level's URP asset (and its renderer data and features) only when a value differs, camera AA and sharpening through `CameraAntialiasingPolicy`, planar reflection scale, texture limit, anisotropic filtering, VSync or the frame-rate cap, window mode and resolution (player only; `ResolveResolution` parses a generated level's size, `native` uses the display), VFX tier. With VSync on `Application.targetFrameRate` is left as it was (Unity ignores it then), so it can show a stale cap in the F9 panel |
| `GraphicsPipelineMapper` | Option levels -> URP asset fields and SSAO feature settings (internal URP 14 setters and the SSAO settings through their serialized fields) |
| `GraphicsSettingsService` | Static entry point for the menu; loads and applies the saved choice after the first scene loads; `IsActive` (greyed out), `RequiresRestart`, `PendingRestart` |
| `GraphicsOptionDependencies` | Which option makes another one moot (upscaler, sharpening, frame cap) |
| `GraphicsSettingsDebugPanel` | Temporary: F9 in any scene opens an IMGUI panel listing presets and options; each change applies and saves; greys out and tags options with the same calls as the menu. Remove or limit to development builds when the menu ships |
| `BuddahGo.Rendering.PlanarReflection` | On a flat floor's renderer: renders the mirrored scene for each game camera and binds it to the `BuddahGo/Lit` material (`_PLANAR_REFLECTION`); `SettingsScale` comes from `reflections` |
| `BuddahGo.Rendering.VfxQuality` | VFX tier: `Current`, `Settings` (`VfxTierSettings`), `Changed`; read by `SkillVfxPhaseController` when a slot starts and by `InkFluidVolume` every frame |
| `BuddahGo.Rendering.RenderingModules` | The gameplay-feature and gameplay-layer contract every quality level's default renderer keeps ([Modules](#modules)) |

**Assets are never written.** Values change only on runtime clones (`HideFlags.DontSave`, made and destroyed by
`GraphicsPipelineMapper.CreateDetachedClone` / `DestroyDetachedClone`), assigned through `QualitySettings.renderPipeline`.
Comparisons (`DiffersFrom`) only read fields, because creating or destroying any pipeline asset disposes the active
render pipeline; a clone that already holds the wanted values is kept, so Apply rebuilds the renderer only when a
pipeline value changes.
Pipeline, texture limit, anisotropic filtering and VSync belong to the current quality level, so the applier records what
each level held before its first write. In the editor those writes would outlive play mode: they are put back on
`Application.quitting`, which fires when play mode ends, and the start quality level is selected again. A recompile
during play mode drops that subscription; the record is also kept in `SessionState`, and the editor restores from it
when it is back in edit mode. Verified in play mode (each level points at its real asset again, `git status` clean) and
by `RestorePutsBackEveryQualityLevelItChanged`. Because the default preset uses the real assets, editing a URP asset
during play mode still shows up unless a custom choice is applied.

**Never networked.** Nothing here touches FishNet, RPCs, prediction or server authority. `vfx_quality` is client-only,
so effects that carry gameplay information must not depend on it.

## The settings panel (main menu)

`MenuSettingsPage` (`Assets/Scripts/UI/MenusUI/`) in `MenuEnvironmentSetting.unity`, opened from 设置 on the home
menu: tabs 画面 / 音频 / 操作. The 画面 tab builds its rows from `GraphicsSettingsService` at runtime: a preset row, then
one row per option; stepping changes the model only ("＊" while an option differs from the preset), Enter applies and
saves, R puts the preset back, Esc closes (asking first when there are unapplied changes). Options that do not take
effect now are dimmed; restart-only ones carry a tag. The page is built by `Assets/Editor/UI/MenuTitleHomeBuilder.cs`
(`Tools/Menu Environment/Build Title + Home UI`). The old uGUI `SettingsPanelUI` and its builder were retired with the
old `MainMenu` scene on 2026-11-06 ([Docs/ui/retired.md](../ui/retired.md)).

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

Restart-only options carry a "需要重启" tag and the panel shows the restart notice after Apply (see the spec); options
that do not take effect are dimmed and cannot be stepped. Checked in play mode on 2026-11-05, with texture quality
flagged in memory to show the tag:

![Panel with dimmed rows, a restart tag and the restart notice](graphics-settings/panel_restart_tag_demo.jpg)

Still to do on the UI side: a styled version of the panel (today it is the plain uGUI look of the other main-menu
panels); a label for the generated resolution levels if
the id is not enough; an in-race entry (pause menu) instead of F9. The SoloCJK subset was regenerated on 2026-11-03
with every `settings.*` label character (539 codepoints); a new label still needs a coverage check first.

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
| Settings menu | first version in the main menu (2026-11-03); F9 panel kept for races | `SettingsPanelUI` + `SettingsPanelBuilder` | styling, an in-race entry, then remove `GraphicsSettingsDebugPanel` |
| VSync online | on by default, decided 2026-10-28 | `pending` on `vsync` | check online prediction smoothing at refresh rates that are not a multiple of 60 Hz (`FrameRateLock`'s concern); Solo interpolates by time |
| Ultra preset | **user decision** | masked `ultra` preset (High Fidelity base) | a default level for every option (`defaultFor` gains `ultra`) |
| VFX tiers | implemented 2026-11-05 with proposed values ([VFX tiers](#vfx-tiers)) | `pending` on `vfx_quality` | **user** approves or adjusts the values; not done: fewer ray-march steps or half resolution for the ink palm's body volume (the largest cost at 24 palms), distance LOD for other players' effects (`far_threshold_m`), a "reduce other players' effects" option, bloom quality |
| First-run preset | not implemented | `pending` on the `high` preset | hardware detection; today the preset of the starting quality level is used |
| NVIDIA FXAA 3.11 | **user decision** | `pending` on `anti_aliasing` | licence check before using it in place of URP's FXAA |

Not verified yet:
- TAA (medium preset) on skill particles and trails in a full match.
- FSR 1.0 in a player build (URP may strip the variants it needs).
- The per-level restore after the code review in real play mode (covered by `RestorePutsBackEveryQualityLevelItChanged`),
  and the restore after a recompile during play mode.
- Planar reflections on the end-field floor during the results ceremony, and of skill effects in a full match.
- Window mode and resolution in a player (EditMode tests cover only the command-line skip), including whether they
  apply live there (the restart column assumes Unity's live `Screen.SetResolution`).
- The added gameplay passes and the curtain fix in a player build and online (checked in the editor's Solo race only).
- VFX tiers in a full six-racer match; the ink grid tiers only by the earlier measurement, not re-measured.

## Adding an option

1. Add a row to `Graphics/GraphicsOption` (`optionId`, `groupId`, `labelKey`, `restartRequired`) and its levels to
   `GraphicsOptionLevel`, with `defaultFor` naming each preset exactly once across the option's levels, and a `summary`
   saying what each level changes. Keep the rows in the panel's order (group, then `sortOrder`). Add the label keys to
   `UI/Text` and check them against the SoloCJK font subset.
2. If the option needs values no column holds, add typed columns to `GraphicsOptionLevel` (each option reads only its
   own columns), then run `python Tools/design-tables/gen_records.py .` and let Unity recompile and import.
3. Apply it: an option id constant in `GraphicsPipelineMapper` and its handling there (URP asset or renderer feature, on
   the clone) or in `GraphicsSettingsApplier.Apply` (QualitySettings, camera, other systems). Values that differ from the
   asset must make `DiffersFrom` return true.
4. Extend `GraphicsSettingsAreComplete` for its values, `GraphicsTableTests.Columns` (which columns the option owns;
   a column without an owner fails), and `GraphicsSettingsTests` (the default-preset test catches a default that does
   not match the quality level). Set `restartRequired` only after checking in play mode that the option does not
   apply live (`GraphicsSettingsRulesTests.RestartOptions`).
5. Check the build: a toggle must be on in at least one shipped URP asset and off in another, or URP strips the variants
   it needs (FSR in a player build is not verified yet).

## Open decisions

- **Ultra preset** above High Fidelity (for example a longer shadow distance and a render scale above 100%): possible
  with the clone approach; a masked placeholder row exists.
- **VFX tiers**: the proposed values in [VFX tiers](#vfx-tiers) (detail particles 40 / 70 / 100 %, ink budget 2 / 4 / 8,
  ink grid 1.5 / 1.25 / 1); whether to add the body-volume, distance and "reduce other players' effects" levers.
- **First-run preset**: currently the quality level the game starts with; hardware detection is not implemented.
