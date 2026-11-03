# The menu environment layout is owned by Blender

The menu environment (`Assets/Scenes/MenuEnvironmentSetting.unity`, still a prototype outside the build list) is laid out in the Blender file 游戏外场景搭建.blend and exported by its `unity_export.py` text block. `Tools ▸ Menu Environment ▸ 3 Sync Changed Objects` applies only what changed since `MenuEnvironmentLayout.applied.json`. It moves changed empties, updates changed meshes in place from their re-saved prefabs, rebuilds changed lights, and deletes objects that are gone from the export, together with their children. Blender is therefore the only place to change where an exported object stands, and whether it exists. This includes the six Seat anchors (`Anchor_LobbySlot_N`) and their facing. A position tuned in Unity is overwritten by the next sync. We chose this over making Unity the source of truth after a one-off import, because the hall dressing is still being iterated in Blender, and re-importing by hand would lose or duplicate those changes.

Objects in the scene fall into three kinds:

- **Blender objects**: architecture, props and Seat anchors. Blender owns their mesh, pose and existence.
- **Unity-only objects**: the simulated bell rope, cameras and pose markers, UI, the extra lighting rig, Seat preview stand-ins and `TEMP_` controls. The export does not know them, and sync leaves them alone.
- **Attached objects**: exported objects that Unity extends. The bell cluster `Prop_Suzu_Honden` is reparented under `BellRope_Honden` and referenced by `MenuBellRope`. The skill ema get colliders and click components. The skin anchor carries the menu Buddha. Blender still owns their mesh and pose, but the Unity-side setup must survive a sync.

## Consequences

Sync never destroys a placed mesh. It updates the prefab instance in place, so the components and children Unity added, the Unity parent and every reference to the object survive. If the export gives the object a different asset, Sync swaps the prefab under the instance. It then re-applies the setup that depends on the object's shape or pose:

- the bell rope group sits on the bells' hook, and the rope's hang point, length and wall point are recomputed;
- skill ema and their cords stay non-static, and the ema colliders are refitted to the mesh;
- the hokora copies stay hidden, and their loadout slots move with them;
- the ema wall stays out of static batching, and its click proxy is refitted;
- the menu Buddha, its halo and the skin shot follow the skin anchor and plinth.

The placeholder statue under the skin anchor is never rebuilt, because the menu Buddha replaces it. Use `2 Build Scene` when Blender adds a new attached object, such as a new skill ema, or a new Seat anchor. Sync places the new object but does not wire it into the menu components. A full rebuild loses Unity-side edits that are not saved elsewhere. Camera poses survive because they are saved to JSON. Changed lights are still rebuilt, so the Unity lighting rig's adjustments to the sun and lanterns are lost until the next Build Scene.
