# Phase Gate Process Flow

The 7-stage pipeline every phase follows. Each stage has a clear owner, input,
output, and sign-off boundary.

## Stage 1 — KICKOFF
- **Input:** prior phase merged + new phase contract drafted by reviewer
- **Output:** active contract committed to `Docs/phase-gates/active/`
- **Sign-off:** reviewer confirms contract is stamped (kickoff date in ledger)
- **Owner:** reviewer (architecture decisions live with reviewer)

## Stage 2 — RECON
- **Input:** active contract
- **Output:** recon report at `agent-exchange/handoff/<date>-<phase>-recon.md`,
  using recon template
- **Sign-off:** reviewer independently verifies every recon claim (minimum 2
  spot-checks via Read/Grep on cited files)
- **Owner:** implementer does the recon, reviewer verifies
- **HALT condition:** if reviewer's spot-check disagrees, recon revised

## Stage 3 — DESIGN-QA
- **Input:** signed-off recon + contract's PRE-WORK questions
- **Output:** design doc at `agent-exchange/handoff/<date>-<phase>-design.md`
- **Sign-off:** reviewer reviews each Q answer for: solves problem? introduces
  asymmetry? meets contract constraints?
- **Owner:** implementer proposes, reviewer pushes back / approves

## Stage 4 — IMPLEMENT
- **Input:** signed-off design
- **Output:** code changes on contract's branch + commit
- **Sign-off:** reviewer reads diff, validates against design, checks Rule 7
  (PredictionRigidbody integrity) when applicable
- **Owner:** implementer

## Stage 5 — TEST
- **Input:** signed-off implementation
- **Output:** raw logs in `agent-exchange/console/raw/`, full session per Rule 1.
  Minimum: Path A (single-machine 30s) + Path B (2-peer 60s, both ends).
- **Sign-off:** raw logs landed at correct paths, file sizes non-trivial
- **Owner:** implementer or user (test driver)

## Stage 6 — VERIFY
- **Input:** raw logs
- **Output:** independent verification report. Reviewer greps raw logs (Rule 2),
  compares against digest, signs off if all match contract's strict gates.
- **Sign-off:** reviewer's verification report in PR comments or
  `agent-exchange/handoff/<date>-<phase>-verify.md`
- **Owner:** reviewer
- **HALT condition:** any FATAL not matching contract's allowed pattern, or
  any failed metric = HALT, escalate

## Stage 7 — MERGE
- **Input:** signed-off verification
- **Output:** merged PR + active contract → `archive/` + new active contract
  for next phase
- **Sign-off:** reviewer approves merge
- **Owner:** implementer merges; reviewer follows up with archive + next active

## Sign-off ledger format

Every contract has a sign-off ledger at the bottom:

```
## Sign-off ledger
| Stage | Date | Signer | Notes |
|---|---|---|---|
| Kickoff | YYYY-MM-DD | reviewer-name | |
| Recon | YYYY-MM-DD | reviewer-name | report path |
| Design | YYYY-MM-DD | reviewer-name | design doc path |
| Implementation | YYYY-MM-DD | reviewer-name | commit SHA |
| Smoke | YYYY-MM-DD | reviewer-name | log paths |
| Verify | YYYY-MM-DD | reviewer-name | verification report path |
| Merge | YYYY-MM-DD | reviewer-name | PR URL + merge SHA |
```

Reviewer maintains. Implementer can append rows but does not self-sign.
