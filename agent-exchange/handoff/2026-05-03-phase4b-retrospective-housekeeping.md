# Phase 4b Retrospective — Housekeeping Handoff for Claude Code

**Date:** 2026-05-03
**Author:** cowork-reviewer (Claude Opus 4.7, harness)
**Audience:** Claude Code (will commit + open PR)
**Branch suggestion:** `chore/phase4b-retrospective-housekeeping` (cut from
`feat/phase4b-v5-latency-terminal-gate` post PR #40 merge, OR cut from `dev`
after V5 merge — see "Branch + commit strategy" below)

---

## Summary

cowork-reviewer ran a Phase 4b full-stack retrospective (V1 → V5 chain
review) and applied 9 small-to-medium adjustments across the harness +
phase-gate system. **7 of 9 edits already landed in the working tree**
via Edit tool calls during the retrospective session. **2 edits could not
be applied from the cowork session** (mount layer can't see
`Docs/phase-gates/active/v5-contract.md` from the Linux container; that
file is visible only to the Windows-side perforce checkout). Those 2 edits
are specified below as exact `old_string` / `new_string` pairs for Claude
Code to apply.

The retrospective also surfaced 1 housekeeping item (active/ stale
contracts) that requires `git mv`, which cowork-reviewer doesn't perform —
specified below for Claude Code.

---

## Already-applied edits (verify these landed via `git status` then commit)

| File | What changed | Verify command |
|---|---|---|
| `Docs/lessons-log.md` | Threshold 90 lines → 800 lines or 20 entries | `grep "approaches 800 lines or 20 entries" Docs/lessons-log.md` |
| `skills/debug-bug.md` | Added "Verification Discipline" subsection (L21+L22+Rule 1-D) | `grep "Verification Discipline" skills/debug-bug.md` |
| `skills/safe-refactor.md` | Added "Verification Discipline" subsection (L22+L21) | `grep "Verification Discipline" skills/safe-refactor.md` |
| `skills/feature-implementation.md` | Added "Verification Discipline" subsection (L22+Rule 1-D) | `grep "Verification Discipline" skills/feature-implementation.md` |
| `Docs/phase-gates/methodology.md` | Rule 12 — Reviewer sign-off rows: independent authorship | `grep "Rule 12 — Reviewer sign-off rows" Docs/phase-gates/methodology.md` |
| `Docs/phase-gates/methodology.md` | Rule 2 sub-clause — Pre-grep gate drift discrimination (CRLF / mount-truncation) | `grep "Pre-grep gate drift discrimination" Docs/phase-gates/methodology.md` |
| `Docs/phase-gates/templates.md` | Template 4 replaced with post-L22 form (Stage A git plumbing + Stage B-E + reflective lesson) | `grep "Stage 6 Independent Verify Report (post-L22)" Docs/phase-gates/templates.md` |
| `Docs/harness-manifest.json` | Added `phaseGates` top-level object + `Docs/phase-gates/README.md` + `methodology.md` to all 3 intent `requiredDocs` | `grep "phaseGates" Docs/harness-manifest.json` |
| `Tools/Harness/Get-BuddahGoHarnessContext.ps1` | Appended "Active Phase Gate" output section (auto-discovers active contracts, prints status + last sign-off ledger row + verify discipline reminder) | `grep "Active Phase Gate" Tools/Harness/Get-BuddahGoHarnessContext.ps1` |

If any verify command above returns 0 hits in your working tree, that means
the cowork Edit tool call didn't land on your side — re-apply that edit
manually (see "Re-apply specs" appendix at the bottom of this doc).

---

## Edits that need Claude Code to apply (mount layer blocked)

### EDIT 1 — `Docs/phase-gates/active/v5-contract.md` Smoke row A3 correction

**Find** (the entire Smoke ledger row in the sign-off table — paste-search the unique substring `file-named-host=37MB CONTENT-IS-CLIENT`):

```
| Smoke | 2026-05-03 | Yonezawa (driver) + Claude (scrape) | 3 raw logs at `agent-exchange/console/raw/2026-05-03-phase4b-v5-{single,host-100ms,client-100ms}.log` (single=22MB, file-named-host=37MB CONTENT-IS-CLIENT, file-named-client=57MB CONTENT-IS-HOST per role markers). 3 digests at `agent-exchange/console/2026-05-03-phase4b-v5-*.log`. Path A baseline (HOST role, 2-peer no-LatencySim, 142 HBs, rec-cb=0 structurally). Path B HOST (212 HBs, 13 PushHitbox Hits, 14 Router stack frames, rec-cb=0 expected). Path B CLIENT (53 HBs, 16 Recv monotonic logicalId 1→16, **rec-cb=2595** under 100ms LatencySim — Q3-B engagement gate 519× exceeded). A1 CS2001 cold-start per L20 + Tundra ExitCode:0. A2 spawn-window 60-tick partial-pass 3rd occurrence. A3 file-name role swap (raw rename recommended for archival). |
```

**Replace with:**

```
| Smoke | 2026-05-03 | Yonezawa (driver) + Claude (scrape) | 3 raw logs at `agent-exchange/console/raw/2026-05-03-phase4b-v5-{single,host-100ms,client-100ms}.log` (single=22MB, host=37MB, client=57MB; file names verified correct via independent role-marker grep — see verify report). 3 digests at `agent-exchange/console/2026-05-03-phase4b-v5-*.log`. Path A baseline (HOST role, 2-peer no-LatencySim, 142 HBs, rec-cb=0 structurally). Path B HOST (212 HBs, 13 PushHitbox Hits, 14 Router stack frames, rec-cb=0 expected). Path B CLIENT (53 HBs, 16 Recv monotonic logicalId 1→16, **rec-cb=2595** under 100ms LatencySim — Q3-B engagement gate 519× exceeded). A1 CS2001 cold-start per L20 + Tundra ExitCode:0. A2 spawn-window 60-tick partial-pass 3rd occurrence — addressed via methodology Rule 11 (SMOKE driver hard-precondition). A3 file-name role swap REVERTED post Stage 6 verify — original A3 was operator confusion at digest-write time; independent role-marker grep proves files correctly named (host file = "Created My Room" + "Host started"; client file = "Joining 109775243814677295" + "Joined My Room"). |
```

### EDIT 2 — `Docs/phase-gates/active/v5-contract.md` Verify row source-of-truth pointer

**Find** (the entire Verify ledger row — paste-search unique substring `Q3-B gate ABSOLUTELY EXCEEDED. STRICT PASS`):

```
| Verify | 2026-05-03 | cowork-reviewer | Independent FULL-grep verification per L21 risk-aware + L22 git-plumbing primary self-application. Verify report at `agent-exchange/handoff/2026-05-03-phase4b-v5-verify.md`. All 9 metric classes match implementer digests 100%. Cross-peer Tier 1 chain verified via exact impulse-vector match HOST→CLIENT for logicalId 1+2 (`(83.34, 0.00, -55.27)` and `(-67.63, 0.00, 73.67)`). L7 latch survived V4 corrective merge (16 Recvs would be 0 if broken). Q0 race NOT manifested at 100ms (0 DupReject + 16 monotonic consumes). Q1 protocol-version handshake LIVE verified (v1↔v1 join successful, 16 cross-peer Recvs would be 0 if Q1 broke). Q3-B gate ABSOLUTELY EXCEEDED. STRICT PASS. Q2 200ms expansion eligible per design Q2-B trigger criteria; user decides whether to run optionally. |
```

**Replace with** (per methodology Rule 12 — points at independent verify report as source-of-truth, paraphrases conclusions, acknowledges pre-fill happened):

```
| Verify | 2026-05-03 | cowork-reviewer | See `agent-exchange/handoff/2026-05-03-pr40-v5-independent-verify.md` for independent grep evidence — this row paraphrases that report (Rule 12: contract pre-fill of reviewer-signed rows; reviewer's independent findings landed AFTER pre-fill but agree). Stage 6 verify discipline: L22-compliant git plumbing + raw-log independent grep (NOT trust-digest). Result: STRICT PASS. All 9 metric classes clean × 3 logs = 27 cells green. Cross-peer Tier 1 chain byte-identical match for logicalId 1+2. Q0 race NOT manifested at 100ms (0 DupReject + 16 monotonic consumes). Q1 protocol-version handshake LIVE-verified via downstream evidence (16 cross-peer Recvs would be 0 if v1↔v1 mismatch hard-blocked join). Q3-B engagement gate exceeded 519× threshold (rec-cb=2595 vs ≥5). A2 partial-pass 3rd recurrence — addressed by methodology Rule 11 going forward. A3 anomaly note in Smoke row was itself wrong — files correctly named — see EDIT 1 correction in this PR. Q2 200ms expansion eligible per design Q2-B trigger; user decides whether to run optionally. |
```

---

## Housekeeping that needs Claude Code git ops

### GIT-OP 1 — Stale `active/` contracts move to `archive/`

The Stage 6 verify of PR #40 surfaced that `Docs/phase-gates/active/`
currently contains 4 contracts (v2b-step1, v3, v4, v5), but only `v5` is
actually active. The other 3 are merged phases that were never moved to
archive.

**Pre-check** (before moving — confirm content equality with archive copies
to know which is authoritative):

```bash
diff Docs/phase-gates/active/v2b-step1-contract.md Docs/phase-gates/archive/v2b-step1-contract.md
diff Docs/phase-gates/active/v3-contract.md          Docs/phase-gates/archive/v3-contract.md
diff Docs/phase-gates/active/v4-contract.md          Docs/phase-gates/archive/v4-contract.md
```

Expected outcomes:
- If `diff` returns empty → active and archive are identical; `git rm` the active copy
- If `diff` shows the active copy has more recent ledger updates (Smoke / Verify / Merge rows) → `git mv` active over archive (active is authoritative)
- If `diff` shows divergence neither side dominates → review manually before either op

After resolving each, the `active/` folder should contain only:
- `v5-contract.md` (will move to archive AFTER V5 merge per Phase 4b CLOSEOUT, not in this PR)

### GIT-OP 2 — README phase pointer (if it references active phase)

Check `Docs/phase-gates/README.md` for any "current active phase" pointer
mentioning v2b-step1, v3, or v4. If found, update to point at v5 only. If
README only generically describes the system, no edit needed.

---

## Branch + commit strategy

Recommend ONE of these two options:

**Option A — Bundle into V5 merge** (if PR #40 not yet merged): apply the
9 already-landed edits + 2 v5-contract edits + 1 git-mv as a single
"V5 closeout housekeeping" commit on the V5 branch BEFORE merging PR #40.
Cleanest history, but bloats PR #40 review surface.

**Option B — Separate housekeeping PR after V5 merges** (if PR #40 already
merged or is about to merge): cut `chore/phase4b-retrospective-housekeeping`
from updated dev, apply all changes, open small focused PR. Better review
surface, costs one extra PR cycle.

**cowork-reviewer recommendation: Option B.** PR #40 already passed Stage 6
with substantive changes; bundling housekeeping risks tangling review focus.
Phase 4b CLOSEOUT can absorb the housekeeping PR as part of its closeout
checklist (V5 contract Q4 item).

---

## PR description suggested skeleton (for the housekeeping PR)

```
# Phase 4b retrospective — harness + phase-gate housekeeping

Post Phase 4b chain (V1→V5) retrospective applied 11 small adjustments:

## Methodology + lessons-log
- Rule 12 (NEW) — Reviewer sign-off rows: independent authorship (from V5 closeout pre-fill incident)
- Rule 2 sub-clause — Stage 6 pre-grep gate drift discrimination (CRLF + mount-truncation classes)
- lessons-log split threshold raised 90 → 800 lines / 20 entries (instruction was unrealistic for typical entry size)

## Phase Gate System
- Template 4 (Stage 6 Verify Report) replaced with post-L22 form (Stage A git plumbing + B-E sections + reflective lesson)
- v5-contract Smoke row A3 anomaly corrected (file names verified correct, A3 was operator confusion)
- v5-contract Verify row revised to point at independent verify report as source-of-truth (per Rule 12)

## active/ housekeeping
- Moved v2b-step1-contract / v3-contract / v4-contract from active/ to archive/ (had been left in active/ since respective merges)

## Harness
- harness-manifest.json: added phaseGates top-level + Docs/phase-gates/README.md + methodology.md to all 3 intent requiredDocs
- Get-BuddahGoHarnessContext.ps1: added "Active Phase Gate" output section (auto-discovers contracts, prints status + last ledger row + verify-discipline reminder)
- skills/debug-bug.md + safe-refactor.md + feature-implementation.md: added "Verification Discipline" subsection (L21 + L22 + Rule 1-D references)

Cross-ref: Phase 4b retrospective handoff at `agent-exchange/handoff/2026-05-03-phase4b-retrospective-housekeeping.md`
```

---

## Items deliberately NOT in this housekeeping PR (deferred)

1. **task #38 — archive backfill V1/V2a/V2a-fix/V2b-Step0 contracts**: 3-5 hour task; Yonezawa to schedule as separate PR, recommended before Phase 7 KICKOFF
2. **L23 spawn-window probe handling**: Yonezawa's Rule 11 already addresses the operator-discipline angle; no further change needed unless Rule 11 itself fails to fix the recurrence in Phase 5+ smokes
3. **Q2 200ms LatencySim expansion**: design-eligible per Q2-B trigger but not gating; user decision

---

## Re-apply specs (only if any "already applied" edit didn't land in your working tree)

If `grep` verify above returns 0 hits, re-apply manually using these exact
old_string / new_string pairs:

### Re-apply Spec 1 — `Docs/lessons-log.md` line 3
**old_string:**
```
Append-only record of agent failures and the rule each one created. New entries at top. When file approaches 90 lines, split: keep recent entries here, move older to `lessons-archive-YYYY-MM.md`.
```
**new_string:**
```
Append-only record of agent failures and the rule each one created. New entries at top. When file approaches 800 lines or 20 entries, split: keep most recent 10 entries here, move older to `lessons-archive-YYYY-MM.md`. (Original 90-line threshold raised 2026-05-03 during Phase 4b retrospective; lessons-log corpus naturally runs 30-60 lines per entry, so 90 lines was a single-entry threshold and pushed premature archiving that hurt cross-reference recall.)
```

### Re-apply Spec 2-4 — `skills/{debug-bug,safe-refactor,feature-implementation}.md`
Insert "Verification Discipline" subsection BEFORE the existing `## Hard Stops` section. Exact content varies per skill — see actual diff in working tree, or reproduce from this retrospective doc's structure.

### Re-apply Spec 5 — `Docs/phase-gates/methodology.md` Rule 12 (append after Rule 10 at end of file)
Full content of Rule 12 is in the retrospective chat. Approximately 30 lines. Cross-references L22 + Rule 2 + Rule 4.

### Re-apply Spec 6 — `Docs/phase-gates/methodology.md` Rule 2 sub-clause
Insert after "**Stage 6 VERIFY pre-grep gate:** ... canonical failure mode." paragraph (around line 116). Adds discrimination rules for CRLF-only drift + mount-layer truncation. Approximately 20 lines.

### Re-apply Spec 7 — `Docs/phase-gates/templates.md` Template 4 replacement
Replace the existing "## Template 4 — Smoke Verification Report" section with the new "## Template 4 — Stage 6 Independent Verify Report (post-L22)" section. New form is approximately 110 lines, includes Stage A (git plumbing) + Stage B (V4-inherited gates table) + Stage C (phase-specific) + Stage D (anomalies) + Stage E (sign-off matrix) + reflective lesson section.

### Re-apply Spec 8 — `Docs/harness-manifest.json`
Two changes:
1. Add `Docs/phase-gates/README.md` and `Docs/phase-gates/methodology.md` to all 3 intent `requiredDocs` arrays (debug, refactor, feature)
2. Add a new top-level `phaseGates` object after `taskRouting` with: description, systemDocs (4 phase-gate files), activeContractGlob, archiveContractGlob, stages (7-stage list), verifyDiscipline (negativeClaimsTarget + preGrepGate + templateRef)

### Re-apply Spec 9 — `Tools/Harness/Get-BuddahGoHarnessContext.ps1`
Append after the existing "Stop conditions:" output block (around line 331). Adds an "Active Phase Gate" output section that:
1. Discovers `Docs/phase-gates/active/*-contract.md` via Get-ChildItem
2. For each contract: prints relative path + first `**Status:**` line + last sign-off ledger row (truncated to 200 chars)
3. Prints a "Verify discipline (Phase Gate System)" footer with 3 reminders (negative claims = git plumbing, Stage 6 pre-grep gate, Template 4 reference)

Approximately 45 lines of PowerShell.
