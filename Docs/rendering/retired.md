# Retired rendering assets (waiting for one cleanup batch)

Rendering's list of zombies, in the same spirit as [../vfx/retired.md](../vfx/retired.md): record here, delete in one
reviewed batch with a full test run. Before deleting, re-check references (search every `.unity`, `.prefab`, `.asset`,
`.mat`, `.lighting` under `Assets/` and `ProjectSettings/` for the `.meta` GUID, and scripts for the path or name;
`LightingData.asset` is binary, so check bake outputs against `LightmapSettings.lightmaps` in the editor or the
[bake manifest](bake-manifest.json) instead). Nothing below has been deleted yet.

| Asset | Why it is retired | References found (2026-11-05) | State |
|---|---|---|---|
| `Assets/Scenes/RaceMap/Lightmap-4_comp_*` … `Lightmap-228_comp_*` (225 indices x `_comp_light.exr` + `_comp_dir.png`, plus metas: 900 files, LFS) | Leftovers of the project's first import (8c2d4579, 2026-03-15); every later bake writes `Lightmap-0..n` of its own and deleted only its own previous files (9dc20717 removed 1 and 2) | none by text GUID; the current LightingData uses `Lightmap-0` only (to confirm in the manifest after the next bake) | to delete |
| `Assets/Scenes/RaceMap/ReflectionProbe-2.exr` | Output of an old bake, no probe renders into it | none by text GUID | to delete after the manifest confirms |
| `Assets/Scenes/RaceMap/RaceMap_Sky.mat` | Pre-dusk sky; RaceMap and the menu use `RaceMap_Sky_Dusk.mat` | none | to delete |
| `Assets/Scenes/RaceMap/RaceMap_Environment_Profile.asset` | Pre-dusk post profile; replaced by `RaceMap_Dusk_Profile.asset` | none | to delete |
| `Assets/Settings/MapLight.lighting` | Lighting settings from before `RaceMap_Lighting.lighting` | none | to delete |
| `Assets/Settings/TryUltra URP.asset`, `TryUltra URP_Renderer.asset` | An Ultra pipeline experiment; not in any quality level (the Ultra preset is a masked row in `Graphics.xlsx`, a user decision) | only each other | delete or reuse when the Ultra preset is decided |
| `Assets/Plugins/UnitySSReflectionURP/` | SSR, removed on 2026-10-28 (R4) | none in shipped renderers | **keep**: the plugin notes say how to bring SSR back for a non-flat glossy floor |
| `Assets/Scenes/RaceMap/Materials/RaceMap_Curb.mat` | The old curb strips' material | one prefab-instance override in `RaceMap.unity` | check whether the overridden object still exists before deleting |
