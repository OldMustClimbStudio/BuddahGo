# Phase Gate Templates

Copy these templates for ALL phase outputs. Fill placeholders, do not invent
new sections without contract amendment.

---

## Template 1 — Recon Report

Filename: `agent-exchange/handoff/<YYYY-MM-DD>-<phase-id>-recon.md`

```
# <Phase ID> — Recon Report

**Branch:** <branch-name> (cut from <base> @ <SHA>)
**Status:** RECON ONLY — no code changes; no Q-answers yet
**Scope reminder:** <one-line scope from contract>

---

## 1. <Item 1 title — typically rb-write callsites or current-state map>
[evidence: file:line cites]
[tabular breakdown if multiple items]

## 2. <Item 2 title — typically reconcile/state contract>
[evidence + KEY FINDING flag if applicable]

## 3. <Item 3 title — typically cross-fire / asymmetry surface>

## 4. <Item 4 title — typically dependency graph>

## 5. <Item 5 title — typically integration touchpoints>

---

## Summary — what Q0–QN must answer (preview, not answers)
[one-line preview per Q with recommended lean]

Awaiting sign-off before writing Q-answer proposals.
```

---

## Template 2 — Design Q&A

Filename: `agent-exchange/handoff/<YYYY-MM-DD>-<phase-id>-design.md`

```
# <Phase ID> — Design Q&A

**Recon reference:** <recon report path>
**Status:** DESIGN PROPOSAL — no code changes yet

---

## Q0 — <Question title from contract PRE-WORK>

**Picked:** <Option letter and name>

**Justification:** <why this option, what tradeoffs accepted>

**Risk:** <what could go wrong, mitigations>

**Open questions:** <anything still unresolved, or "none">

## Q1 — <...>
[same structure]

...

---

## Cross-cutting concerns
[interactions between Q answers, if any]

## Awaiting sign-off
[what reviewer needs to approve before implementation]
```

---

## Template 3 — PR Description

Used in GitHub PR description body.

```
# <Phase ID> — <one-line title>

## Scope
[from contract — locked, not editable]

## Strict gates met
[reference contract section]

## Path A — single-machine 30s
| Metric | Value | Gate | Status |
|---|---|---|---|
| inv-imp-compared (or leg-) | | | |
| inv-imp-div (or leg-) | | | |
| INV FATAL count (or LEG) | | | |
| D-LOC FATAL | | | |
| CommandBus ClearAll | | | |
| CommandBus FirstInvoke | | | |
| CommandBus Recv ch=Impulse | | | |
| DropFull | | | |

## Path B — 2-peer LAN 60s
| Metric | HOST | CLIENT | Gate |
|---|---|---|---|
| (same metrics, both columns) | | | |

## Independent verification
[reviewer's grep results vs digest, link to verification report]

## Lessons added / updated
[list L# entries, or "No new lessons; existing rules covered all cases."]

## Carry-forward flags
[decisions deferred to future phases, with target phase ID]

## Files changed
[diff stat: file count, ±LOC]

## Sign-off
[link to active contract's sign-off ledger]
```

---

## Template 4 — Smoke Verification Report

Filename: `agent-exchange/handoff/<YYYY-MM-DD>-<phase-id>-verify.md`

```
# <Phase ID> — Independent Verification

**Raw log paths:** <list>
**Reviewer:** <agent-name>
**Verification date:** <YYYY-MM-DD>

---

## Path A grep results
[actual grep output, line counts, sample lines]

## Path B HOST grep results

## Path B CLIENT grep results

## Cross-check vs implementer's digest
[each metric: digest value vs reviewer grep value, must match]

## Anomalies
[anything unexpected, even if within gate]

## Verdict
[PASS / FAIL / PASS WITH CAVEATS, with explicit reasoning]
```

---

## Template usage notes

- All file paths use ASCII hyphens, no spaces.
- `<phase-id>` examples: `phase4b-v2b-step1`, `phase4b-v3`, `phase4b-v4`.
- `<role>` examples: `single`, `host`, `client`.
- Date format: `YYYY-MM-DD` (ISO 8601).
- Markdown tables must align with `|---|` separator rows for legibility.
- Code fences (```) preferred over indent blocks for readability.
