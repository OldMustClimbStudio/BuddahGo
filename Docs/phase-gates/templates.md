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

## Template 4 — Stage 6 Independent Verify Report (post-L22)

Filename: `agent-exchange/handoff/<YYYY-MM-DD>-<pr#-or-phase-id>-verify.md`

This template subsumes the older "Smoke Verification Report" form and adds
git-plumbing verification (Stage A) + sign-off matrix + reflective lesson
section. Use this for ALL Stage 6 verify passes; the old form is deprecated.

```
# PR #<NN> — <phase-id> — Independent Stage 6 VERIFY

**Date:** <YYYY-MM-DD>
**Reviewer:** <agent-name>
**Branch:** <branch-name> @ <SHA>
**Base:** <base-branch> (post any prerequisite merges)
**Verify discipline:** L22-compliant — `git show <ref>:<path>` + raw-log
independent grep (NOT trust-digest, NOT Read on working tree as primary)
**Reviewer note:** [if contract has pre-filled reviewer rows per Rule 12,
state that this report is the actual independent verify; the contract row
content is a claim to validate, not a sign-off provided.]

---

## Result: ✅ STRICT PASS / ❌ FAIL / ⚠ PASS WITH CAVEATS

[1-2 paragraph executive summary. State the headline finding + any non-
blocking action items.]

---

## Stage A — git plumbing verify of IMPLEMENT (commit `<SHA>`)

### A.1 <First implementation deliverable, e.g., new file>
```
git show <ref>:<path>
→ [content snippet or summary]
```
✓/✗ Matches design <Q-ref>.

### A.2 <Second deliverable, e.g., insertion sites in existing file>
```
git show <ref>:<path> | grep -n "<token>"
→ :<line>  <code>
```
✓/✗ All N design-spec sites present.

### A.3 <Third deliverable>
[same pattern]

### A.4 Stage 6 pre-grep gate (L22 self-application)
```
git status --short -- <phase scope files>
→ <output>
```
[Discriminate any drift per methodology Rule 2 mount-artifact discrimination
sub-clause: CRLF-only? Mount truncation? Real edit?]
[Conclude: gate PASSES / FAILS]

---

## Stage B — Independent raw-log grep (V4-inherited gates)

| Gate | Path A | Path B HOST | Path B CLIENT | Pass? |
|---|---:|---:|---:|:---:|
| `D-LOC FATAL` | | | | |
| `D-IMP LEG/INV FATAL` | | | | |
| `leg-imp-div=[1-9]` (or inv-) | | | | |
| `DropFull` | | | | |
| `DupReject` | | | | |
| `[CommandBus]:ClearAll` | | | | |
| `FirstInvoke` | | | | |
| `schema mismatch` | | | | |

[Add or remove rows per active contract's strict-gate definitions.]

---

## Stage C — Phase-specific gate verification

### C.1 <First phase-specific gate, e.g., Q3-B engagement metric>
[evidence + threshold comparison]

### C.2 <Second phase-specific gate>
[evidence]

### C.3 Cross-peer Tier 1 chain (if 2-peer test)
[exact byte-level match evidence between HOST emit and CLIENT recv]

---

## Stage D — Anomaly resolution

### D.1 <Anomaly ID, e.g., A1>
[describe + disposition: ACCEPTED / DOCUMENTED / RETROFIT]

### D.2 <...>

[If any contract anomaly note is itself wrong upon investigation, document
the correction here and propose a contract amendment in pre-merge actions.]

---

## Stage E — Sign-off matrix

| Check | Method (L22-compliant) | Result |
|---|---|---|
| Branch tip = expected SHA | `git rev-parse origin/<branch>` | ✓/✗ |
| PR diff matches expected file count + LOC | `git diff --stat <base> <branch>` | ✓/✗ |
| Each implementation deliverable at HEAD | `git show <ref>:<path>` | ✓/✗ |
| Stage 6 pre-grep gate | `git status --short` filtered + drift discrimination | ✓/✗ |
| Strict gates × paths | independent grep (NOT trust digest) | N/M cells clean |
| Phase-specific gates | independent grep | ✓/✗ |
| Anomaly dispositions | manual reasoning | ✓/✗ |

**STRICT PASS / FAIL confirmed via L22-compliant git plumbing + independent
raw-log grep.** [Authorize or block merge.]

---

## Pre-merge action items (none block merge unless flagged)

1. [Document corrections, e.g., A3 anomaly correction in contract Smoke row]
2. [PR description amendments]
3. [Wire-format / atomic-deployment coordination reminders]
4. [Contract Verify-row source-of-truth pointer per Rule 12 if applicable]

---

## Reflective lesson on this verify pass

[1-3 short observations on what the verify discipline caught or missed this
time. Particularly: were any L21+L22+Rule 1-D rules exercised? Did any
methodology amendment surface from this verify? Any candidate L# entry?
This section is not optional — it's where the methodology stays alive.]
```

---

## Template usage notes

- All file paths use ASCII hyphens, no spaces.
- `<phase-id>` examples: `phase4b-v2b-step1`, `phase4b-v3`, `phase4b-v4`.
- `<role>` examples: `single`, `host`, `client`.
- Date format: `YYYY-MM-DD` (ISO 8601).
- Markdown tables must align with `|---|` separator rows for legibility.
- Code fences (```) preferred over indent blocks for readability.
