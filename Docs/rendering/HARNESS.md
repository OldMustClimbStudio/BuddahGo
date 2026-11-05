# Rendering harness (agents start here)

**Read for the change you make** (nothing else is required up front):

| Change | Read |
|---|---|
| Any rendering change | [ARCHITECTURE.md](ARCHITECTURE.md): the module's card and the requirements table |
| Sun, ambient, fog, sky, probes, bake | ARCHITECTURE "Lighting and bake", "Sky and fog", "Bake reproducibility"; re-tuning checklist at the end of [racemap/README.md](racemap/README.md) |
| Colour look / grade | [racemap/dusk-color-study.md](racemap/dusk-color-study.md) (C2 is the chosen look; A2/B2 are the alternatives) |
| Pipeline, renderer, quality level, settings panel | [graphics-settings.md](graphics-settings.md) |
| Skill effects | [../vfx/README.md](../vfx/README.md) |
| A trap you hit | racemap/README "Editor and tooling traps", [../unity-mcp.md](../unity-mcp.md) |

**Invariants** (breaking one needs the user):
- `Volume`'s profile keeps **WhiteBalance and Bloom** (Feel shakers look them up without null checks).
- `Volume_Dark` follows the gameplay grade, **0.1 EV darker** (black curtain).
- **Index 1 of every pipeline's renderer list is `URP-PlanarReflection-Renderer`**; only `PlanarReflection` uses it, by name.
- Sky colours are **linear**; the fog colour has red > 1 and is **set from code** (`RenderingBake`), never with the
  Lighting window's picker.
- URP assets are never written at runtime (settings use clones).
- User decisions: dusk C2, menu shares the dusk look, baked probes and bake, planar instead of SSR, High Fidelity =
  MSAA 8x + FXAA, VSync on, Buddha's height fixed in the collider only (ARCHITECTURE requirements R1-R11).

**Standard loop**:
1. Capture before: `RaceMapDuskColorStudy.Capture("<topic>_before", "", "")` (after a bake or a play-mode round trip,
   never right after a scripted scene open).
2. Change. If lighting inputs changed: record the value in `RenderingBake` (or the scene sun / `.lighting` asset), run
   `BuddahGo/Rendering/Bake/Verify Recorded Lighting`, then `Bake RaceMap + Menu`.
3. Capture after; `python -P Tools/rendering/capture_diff.py <before> <after> --sheet <topic>_compare`; open the sheet
   for the user (visual work ships with a labelled before/after image).
4. Compile (`dotnet build Assembly-CSharp-Editor.csproj`) and run the affected tests (EditMode suite for settings or
   scene changes).
5. Small commits by category (scene, asset, code, docs); each builds on its own. Bake outputs, scenes and
   `bake-manifest.json` go in one commit. Never commit `Docs/rendering/racemap/<label>/` PNG folders.

**Rules of the shared editor**: one session drives it at a time; do not text-edit an open scene; save scenes through the
editor.

**Record a new decision** in ARCHITECTURE's requirements table (date, what, modules) and the module's Need line; record
how you got there (numbers, images) in racemap/README or a topic doc; record a retired asset in [retired.md](retired.md).
