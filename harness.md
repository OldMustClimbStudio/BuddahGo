# BuddahGo harness

Unity 2022.3.55f1c1 · FishNet · FishyFacepunch · Steam.

- Read the affected owner and callers, then complete the requested change. Necessary cross-system edits are part of the task.
- Preserve unrelated edits. For serialized fields or RPC changes, update consumers and migrate existing data where needed.
- Choose checks for the actual change and report their results or limitations. No fixed stages, scores or repeated approvals.
- Keep server authority, prediction routing, skill dispatch and scene ownership intact; details are in [architecture.md](Docs/architecture.md).

## Read when needed

- Creating a worktree, branch, commit or PR: [CONTRIBUTING.md](CONTRIBUTING.md), including validation and asset rules.
- Understanding a system or using Unity MCP: [Docs/README.md](Docs/README.md).
- Executing the architecture optimization plan: [Docs/optimization/HANDOFF.md](Docs/optimization/HANDOFF.md).
- Domain terms and decisions: [CONTEXT.md](CONTEXT.md), [GLOSSARY.md](GLOSSARY.md) (skill and risk terms), [Docs/adr/](Docs/adr/).
- Changing skills, 缘起, obsession/backfire, cast rules or player controls: rules and their status in [Docs/gameplay/README.md](Docs/gameplay/README.md) (已定 = user-confirmed, 建议 = proposal; only the user upgrades a 建议), module boundaries in [yuanqi-architecture.md](Docs/gameplay/yuanqi-architecture.md) (no `Skill_*` or `SkillExecutor` reference to a pair; a pair reaches skills only through `ISkillRuntimeHandle` and small rewrite interfaces such as `IHandsImpulseScale`), and the re-runnable Practice checks in [practice-verification.md](Docs/gameplay/practice-verification.md).
- Changing balance values, rules or text, or wiring a component to the design tables: [Docs/design-tables/README.md](Docs/design-tables/README.md). Edit the xlsx under `Assets/Design/Tables/`, never the generated asset, JSON or CSV mirror.
- Implementing or validating Solo Match: [Docs/single-player/HANDOFF.md](Docs/single-player/HANDOFF.md). Solo tasks exclude online tests (including Steam two-client smoke and Solo/Online alternation); use the independent Solo benchmark requirements in [phases.md](Docs/single-player/phases.md) (the dedicated protocol and tools are integrated; runtime qualification remains open) and Solo evidence. Preserve existing online test assets for separately requested online work.

This is the shared policy for `Agent.md`, `AGENTS.md` and `CLAUDE.md`.
Historical reports in `agent-exchange/` are evidence, not current instructions or a task queue.
