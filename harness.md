# BuddahGo harness

Unity 2022.3.55f1c1 · FishNet · FishyFacepunch · Steam.

- Read the affected owner and callers, then complete the requested change. Necessary cross-system edits are part of the task.
- Preserve unrelated edits. For serialized fields or RPC changes, update consumers and migrate existing data where needed.
- Choose checks for the actual change and report their results or limitations. No fixed stages, scores or repeated approvals.
- Keep server authority, prediction routing, skill dispatch and scene ownership intact; details are in [architecture.md](Docs/architecture.md).

## Read when needed

- Creating a worktree, branch, commit or PR: [CONTRIBUTING.md](CONTRIBUTING.md), including validation and asset rules.
- Understanding a system or using Unity MCP: [Docs/README.md](Docs/README.md).
- Domain terms and decisions: [CONTEXT.md](CONTEXT.md), [Docs/adr/](Docs/adr/).
- Changing balance values, rules or text, or wiring a component to the design tables: [Docs/design-tables/README.md](Docs/design-tables/README.md). Edit the xlsx under `Assets/Design/Tables/`, never the generated asset, JSON or CSV mirror.
- Graphics settings, a settings menu, effect quality tiers, VSync, window mode or resolution: [Docs/rendering/graphics-settings.md](Docs/rendering/graphics-settings.md). Its "Not implemented yet" list says what is only a placeholder.
- Redoing or adding a skill VFX, its layers, shaders, textures or the VFX table: [Docs/vfx/README.md](Docs/vfx/README.md).
- Any rendering change (lighting, bake, sky, fog, post, URP pipeline or renderer assets, the project's shaders): [Docs/rendering/HARNESS.md](Docs/rendering/HARNESS.md). Bake only with `BuddahGo/Rendering/Bake/Bake RaceMap + Menu`.
- Solo Match (AI, offline flow): [Docs/single-player/HANDOFF.md](Docs/single-player/HANDOFF.md). Solo tasks do not run online tests.

This is the shared policy for `Agent.md`, `AGENTS.md` and `CLAUDE.md`.
Finished work lives in [Docs/archive/](Docs/archive/) and git history. It is evidence, not current instructions or a task queue.
