"""Summarize the explicitly controlled AISkillEffectHarness samples (never natural race proof)."""
import argparse
import collections
import json
from pathlib import Path


def read_rows(path):
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]


def active_effects(racer, tick):
    stats = racer["Stats"]
    effects = set()
    if stats["IsRooted"]:
        effects.add("root")
    if stats["IsInvertTurnActive"]:
        effects.add("reverse")
    if stats["FinalForwardForce"] < 49:
        effects.add("slow")
    if stats["FinalForwardForce"] > 51:
        effects.add("acceleration")
    if stats["ScaleMultiplier"] > 1.01:
        effects.add("giant")
    if stats["ScaleMultiplier"] < .99:
        effects.add("small")
    if racer["VisionUntil"] > tick:
        effects.add("vision")
    if racer["BuffLeft"] > 0:
        effects.add("hands-buff")
    if racer["HasTrap"]:
        effects.add("trap-zone")
    # Unified backfire penalty (Docs/gameplay/README.md 6.2): the 5 s skill lock and/or the x0.85 max-speed modifier.
    if racer.get("BackfireLeft", 0) > 0 or racer.get("MaxSpeedPenalty", False):
        effects.add("backfire")
    return effects


def analyze(directory):
    samples = read_rows(directory / "effect-samples.jsonl")
    events = read_rows(directory / "effect-events.jsonl")
    combat = read_rows(directory / "combat.jsonl")
    groups = collections.defaultdict(list)
    for row in samples:
        groups[row["Case"]].append(row)
    cases = []
    for name, rows in groups.items():
        variant, source_text, skill = name.split("-", 2)
        source = int(source_text)
        # Backlash (6.2, 2026-10-31): the unified penalty on the caster plus the skill's own _anti variant, which is a
        # self effect for five skills (self slow, root then boost, shrink, self invert, own curtain) and a reverse
        # palm burst of five projectiles for push_projectile_hands.
        expected_self = {
            "normal": {"acceleration": {"acceleration"}, "slowtrap": {"trap-zone"}, "giant": {"giant", "acceleration"},
                       "push_projectile_hands": {"hands-buff"}, "reverseturn": set(), "blackcurtain": set()},
            "backlash": {"acceleration": {"slow", "backfire"}, "slowtrap": {"root", "acceleration", "backfire"},
                         "giant": {"small", "backfire"}, "push_projectile_hands": {"backfire"},
                         "reverseturn": {"reverse", "backfire"}, "blackcurtain": {"vision", "backfire"}},
        }[variant][skill]
        # The normal skill's own effect on the caster, which a backfired cast must not produce.
        normal_only = {"acceleration": {"acceleration"}, "slowtrap": {"trap-zone"}, "giant": {"giant"},
                       "push_projectile_hands": {"hands-buff"}, "reverseturn": set(), "blackcurtain": set()}[skill]
        seen = collections.defaultdict(set)
        for row in rows:
            for racer in row["Racers"]:
                seen[racer["RacerId"]].update(active_effects(racer, row["Tick"]))
        final = {r["RacerId"]: active_effects(r, rows[-1]["Tick"]) for r in rows[-1]["Racers"]}
        actual_targets = [racer for racer, effects in seen.items() if racer != source and effects]
        start_tick, end_tick = rows[0]["Tick"], rows[-1]["Tick"]
        impacts = [r for r in combat if start_tick <= r["Tick"] <= end_tick and r["SourceId"] == source]
        launches = sum(r["Stage"] == "launched" for r in impacts)
        hit_targets = sorted({r["TargetId"] for r in impacts if r["Stage"] == "hit" and r["Routed"]})
        case_events = [e for e in events if e["Case"] == name]
        executed = sum(e["Event"] == "cast" and e["Detail"].startswith("Executed;") for e in case_events)
        repeated_rejected = any(e["Event"] == "immediate-repeat" and e["Detail"] == "accepted=False" for e in case_events)
        self_ok = expected_self.issubset(seen[source])
        if variant == "backlash":
            self_ok = self_ok and not (seen[source] & normal_only)
        target_ok = True
        if skill in ("reverseturn", "blackcurtain"):
            effect = "reverse" if skill == "reverseturn" else "vision"
            # A backfired global skill hits only the caster; a normal one reaches every opponent.
            expected = {source} if variant == "backlash" else set(seen) - {source}
            actual = {r for r, effects in seen.items() if effect in effects}
            target_ok = actual == expected
        if skill == "slowtrap" and variant == "normal":
            target_ok = bool(actual_targets) and "slow" not in seen[source]
        if skill == "push_projectile_hands":
            # Normal: the one buffed push launches a projectile. Backlash: no hands buff; the variant's reverse burst
            # launches its five projectiles backwards (Skill/Push charged_anti.projectileCount).
            target_ok = launches == (5 if variant == "backlash" else 1)
        elif variant == "backlash":
            # Every other variant is a self effect: no opponent is touched.
            target_ok = target_ok and not actual_targets
        # 2 s backfire wind-up + max(5 s penalty, longest variant 10 s) < 18 s: everything is over at the end of the case.
        recovered = rows[-1]["Elapsed"] >= 18 and not any(final.values())
        cases.append({"case": name, "executions": executed, "repeatRejected": repeated_rejected,
                      "selfEffects": sorted(seen[source]), "affectedOpponents": actual_targets,
                      "launches": launches, "routedHitTargets": hit_targets,
                      "selfEffectObserved": self_ok, "targetScopeObserved": target_ok,
                      "recoveredAtEnd": recovered, "lastElapsed": rows[-1]["Elapsed"],
                      "passed": self_ok and target_ok and recovered and executed == 1 and repeated_rejected})
    return {"evidenceKind": "controlled-effects-not-natural-racing", "cases": cases,
            "passed": len(cases) == 12 and all(c["passed"] for c in cases)}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = analyze(args.directory)
    rendered = json.dumps(result, indent=2, ensure_ascii=False)
    if args.output:
        args.output.write_text(rendered + "\n", encoding="utf-8")
    print(rendered)
    raise SystemExit(0 if result["passed"] else 1)
