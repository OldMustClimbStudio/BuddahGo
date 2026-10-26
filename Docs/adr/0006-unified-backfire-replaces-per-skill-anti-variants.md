# One backfire penalty replaces the per-skill anti variants

A backfire (反噬) is no longer a skill. Each of the six skills used to have an `*_anti` variant (a `Skill_*_Anti` class, asset and table rows) that ran in place of the skill. Their costs were very different (the slow trap's backfire rooted the caster for 1 s, the reverse turn's inverted the caster for 10 s), while obsession priced every backfire about the same, so the only obsession decision was whether to cast now or wait.

Now one penalty applies to every skill. The backfire is still rolled from obsession when the server accepts the request, at the key press, while the player can read the probability on the HUD. The cast keeps its 1 s confirm window with one shared backfire presentation and spends the slot cooldown, but the skill never runs, adds no obsession and earns no same-skill surcharge stack. When the window ends, a 10 s penalty starts: every skill is locked, obsession stops decaying, and max speed is multiplied by 0.85 through the predicted modifier `MaxSpeedMultiplier`. At the end, obsession is multiplied by 0.8. The penalty continues through respawn and teleports, and a freeze interrupt does not drop a rolled backfire. The user decided these rules on 2026-10-25 and 2026-10-26 (`Docs/gameplay/README.md` §6.2). The implementation also keeps a rolled backfire through ordinary cancels such as an AI abandoning its cast (`SkillCastRules.DropsPendingCast`); that rule is marked 建议（实现采用）, not user-confirmed.

## Considered Options

- **Keep the six per-skill variants**: rejected. Their costs stayed unrelated to their price, and every variant needed its own code, effects and table rows.

## Consequences

- The six `Skill_*_Anti` classes and assets, `SkillAction.antiSkillId`, `BuddahHandControl.FireChargedProjectileBurstReplicated`, the `Skill.antiSkillId` column and every `_anti` table row are gone. The shared presentation reuses the old anti assets: the `IsAnti` animator trigger and the `Anti_Reflection_Shared` / `Anti_Reflection_Local` feel events. The `_anti` values in the `SkillId` enum, the VFX registry ids and the text keys are kept.
- A backfired cast ends with `CastStage.Executed` and `CastEvent.IsBacklash = true`; the field keeps its old name.
- The values are table columns of `Skill/Obsession`: `backfireLockSeconds` 10, `backfireMaxSpeedMultiplier` 0.85 and `backfireEndKeepRatio` 0.8 (a provisional value).
- The user's "反作用" idea (buffs on the caster may flip during a backfire) is 待确认 and not implemented.
- AI Racers are not adapted yet (A7): their decisions still weigh a per-skill `BacklashSeverity`, which no longer matches the single penalty.
