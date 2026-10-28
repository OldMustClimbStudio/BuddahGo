# 缘起 pairs are third-party modules registered per loadout

A 缘起 (Yuanqi) is the effect two skills produce when one becomes active while the other is still active. Each pair lives outside both skills as its own module: one `YuanqiAction` subclass (`Yuanqi_*`) and one asset, listed in `SkillDatabase.yuanqis` and configured by a row of the `Skill/Yuanqi` table (pair, consume flags, Instant or Sustained, enabled). When a racer's loadout is set, the server-only `YuanqiRegistry` creates a per-racer copy of each enabled pair whose two skills are both in that loadout, so combinations outside the loadout are never checked. `SkillExecutor` records every executed skill on the racer's server-only `SkillStateBoard` with one generic call, and the registry reacts to the board's `Activated` and `Ended` events instead of polling. The user chose this "glue" model on 2026-10-25 (`Docs/gameplay/README.md` §5.2): pairs are added one at a time where they fit, and the existing skills are reused and only have to accept being consumed or rewritten by the third party.

## Considered Options

- **Each skill handles its partners itself**: rejected. Six skills give 15 possible pairs, so every skill would carry branches for other skills, and each new pair would edit two skill classes and could break pairs that already work.
- **Detecting every skill combination at runtime**: rejected. Loadouts are fixed before the race, so the pairs a racer can trigger are known when it spawns; checking only the registered pairs keeps detection cheap and makes "this loadout has no 缘起" mean that nothing runs.

## Consequences

- Dependency rules: no `Skill_*` class references a `Yuanqi_*`, `SkillExecutor` knows no pair, and pairs never reference each other or any `Skill_*`. A pair touches skills only through the `ISkillRuntimeHandle` each skill creates (`Consume`; `IScaleRuntimeHandle` adds the live body scale) and small rewrite interfaces such as `IHandsImpulseScale`, which `BuddahHandControl` implements; movement only through the `SkillExecutor.ApplyXToOwner` family of predicted modifiers, impulses only through `BuddahPredictionRouter.RouteImpulse`, and presentation only through observer events.
- Adding a pair means one `Yuanqi_*` class, one asset and one `Skill/Yuanqi` row, plus a `Skill/YuanqiEffect<Pair>` family sheet when the pair has parameters. A skill that a pair consumes or rewrites must override `SkillAction.CreateRuntimeHandle`; 神足通, 禅定缓流, 法相金身 and 如来神掌 do, 一念光明 and 逆姻缘 do not yet.
- What is consumed is decided only by the table's `consumeA` / `consumeB`, and consumed skills refund neither cooldown nor obsession. A pair triggers once per overlap; it can trigger again only when one of its skills starts a new board entry.
- Detection is server-only and the board is not synced. Clients learn about a 缘起 from observer events (`SkillExecutor.YuanqiObserved`, `YuanqiImpactObserved`) and from synced motor state. The board and registry are plain MonoBehaviours added at runtime, so the racer prefab gains no NetworkBehaviour.
- Training Dummies get no board or registry because they never cast, but they are valid targets.
