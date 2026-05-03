# Lessons Log

Append-only record of agent failures and the rule each one created. New entries at top. When file approaches 800 lines or 20 entries, split: keep most recent 10 entries here, move older to `lessons-archive-YYYY-MM.md`. (Original 90-line threshold raised 2026-05-03 during Phase 4b retrospective; lessons-log corpus naturally runs 30-60 lines per entry, so 90 lines was a single-entry threshold and pushed premature archiving that hurt cross-reference recall.)

## How to Use This File

Read this before any edit pass. The harness helper includes it under `requiredDocs` for every intent. Each entry has:
- **Date / intent / system / severity** header
- **L# - one-line title**
- **What happened** - the observable failure
- **Cause** - the wrong assumption or skipped step
- **Rule** - the concrete check to run next time
- **Promoted to** - which authoritative doc now also carries this rule, or `log-only` if too narrow

If you are about to do something covered by a rule below, follow the rule. If you discover a new failure mode during a session, append a new entry before closing the task.

---

## L23 — Tooling/CI changes need execution-test verification, not just content-pattern verification (2026-05-03, Phase 7 RECON pre-flight; PR #42 retrospective housekeeping post-mortem)

Symptom: Phase 7 RECON began with `Tools/Harness/Get-BuddahGoHarnessContext.ps1`
per Step 3 of Mandatory Execution Order. The script aborted with a Windows
PowerShell 5.1 parser error at line 360 ("string is missing the terminator: '")
BEFORE emitting the "Active Phase Gate:" section that PR #42 (V5 closeout
retrospective housekeeping) had added. None of the phase-gate awareness
output was reachable; the entire active-contract surfacing block was dead.
Implementer's first reaction was correctly to STOP and report (per the
contract's "if no Active Phase Gate section, report bug" stop-rule), but the
discovery itself happened only because Phase 7 KICKOFF told the implementer
to actually run the helper and look for that output. Had Phase 7 RECON gone
straight to grep without the mandatory helper run, the regression would have
shipped silently into N more sessions.

Cause: PR #42 added a Where-Object filter at line 360 using the literal pause
glyph `⏸` (U+23F8) inside a single-quoted regex string. The .ps1 was saved
as UTF-8 without BOM. Windows PowerShell 5.1 on this machine reads non-BOM
.ps1 files using the OEM/ANSI code page (cp936 here); the 3-byte UTF-8
sequence for `⏸` is decoded as garbage bytes that include an unbalanced
single-quote partner, and the parser fails before the Active-Phase-Gate
block can run. PR #42's review verified the script's content pattern looked
right, but did NOT execute the script under the actual harness environment
(Windows PS 5.1, OEM cp936). Content-pattern review is necessary but not
sufficient for tooling changes; only end-to-end execution exposes encoding
+ runtime-environment regressions.

Rule:
1. **Stage 6 VERIFY for any PR that touches harness tooling, CI scripts, or
   any invocable executable file MUST execute the changed file end-to-end
   under the same environment the harness uses (Windows PowerShell 5.1 for
   `.ps1`, bash for `.sh`, etc.) and confirm exit code 0 + expected stdout.**
   Reading the diff and confirming "the new lines look right" is NOT
   sufficient.
2. **Scope of "tooling" for this rule:** `Tools/`, `Tools/Harness/`,
   `.github/workflows/`, `.claude/hooks/`, any standalone `*.ps1`, `*.sh`,
   `*.py`, `*.js`, `*.bat`, `*.cmd` not part of the Unity asset pipeline.
   It does NOT cover `*.cs` / `*.meta` / `*.prefab` / `*.unity` / `*.asset`
   — those are exercised by the Unity compile + smoke phases and the
   methodology already gates on those.
3. **Author-side self-execution before PR open:** the PR author MUST run the
   changed tool once on the harness target environment before pushing.
   Running it on a different OS, or running a syntactically-equivalent
   substitute, does not count.
4. **Reviewer cross-execution if stakes are high:** for changes to the
   helper script or any rule-encoding file that downstream phases will rely
   on, reviewer reproduces the execution independently (different working
   tree clone if needed) and pastes the actual stdout/exit-code into the
   Stage 6 verify report's Stage A pre-grep gate.
5. **Encoding + line-ending self-check:** for any `.ps1` / `.sh` change,
   author confirms file encoding is appropriate for the target shell. Quick
   discriminator: if the script contains any non-ASCII glyph, either save
   as UTF-8 with BOM (preferred for cross-platform PowerShell 5.1+) or
   replace the glyph with an ASCII sentinel and update the matching
   producer in the same PR.

Promoted to: `Docs/phase-gates/methodology.md` Rule 2 sub-clause "Tooling
change execution-test" (covers item 1-5 above with explicit scope and quick
self-check checklist).

---

## L22 — Negative-claim verification MUST query `git show HEAD:<path>` / `git status --short` / `git diff origin/<base>` — NOT Read/Grep on working tree (2026-05-03, Phase 4b V4 closeout post-mortem)

Symptom: V4 verify report ("17 dead identifier strings: 0 hits in
Assets/* runtime code") + V5 recon Surface 3 ("ImpulseQueueState
confirmed absent post-V4 deletion") both built on Read/Grep tool
inspection of working tree. PR #37 was merged into dev with V4 commits
present, BUT several intended deletions (`BuddahPredictedReconcileData
.ImpulseQueueState` field, `ProjectSettings/ProjectSettings.asset`
LEGACY_SHADOW define removal, comment cleanup in CombatAdapter +
ShadowScratch + EventChannel + Motor) were never staged and never made
it into the merged PR. Working tree had the deletions → Read showed
the deleted state → verify "0 references" passed. dev branch retained
the field/define + stale comments. The discrepancy was discovered post
V5 IMPLEMENT staging when `git diff HEAD` showed 6 source/asset files
modified that should have been committed weeks earlier.

Cause: implementer applied V4 deletions via Edit tool (which modify
working tree only). `git add` step was incomplete — some files staged,
some not. Multi-file deletion-heavy phase + manual staging = staging
error mode is silent (no compile error; file just doesn't get added).
Verify methodology then read working tree as the "current state of the
codebase" — but working tree includes UNSTAGED edits + UNTRACKED files
on top of what's actually committed. L21 said "negative claims demand
positive verification" but didn't specify the verification target —
defaulted to Read/Grep on working tree, which is the wrong source-of-
truth for "is X present in the merged codebase".

Rule:
1. **Negative-claim verification MUST use git plumbing as primary
   source-of-truth.** Canonical commands:
   - `git show HEAD:<path>` — exact committed file content at branch tip
   - `git status --short` — working tree drift from HEAD
   - `git diff origin/<base> -- <path>` — what this branch will land
     vs. base when PR merges
   - `git log --oneline -- <path>` — confirms file's last-touched commit
2. **Read/Grep on working tree is FORBIDDEN as primary verification on
   deletion-heavy phases.** Working tree may include unstaged edits that
   misrepresent the merged codebase. Read/Grep can be used for navigation
   but not for "X was successfully removed" sign-off claims.
3. **Pre-grep gate for Stage 6 VERIFY:** reviewer MUST run
   `git status --short` for the phase's declared scope. Any `M` or `??`
   line in scope = phase is NOT complete; halt VERIFY and escalate to
   IMPLEMENT for completion before re-running smoke.
4. **PR diff vs base is the ground truth for what gets merged.** Before
   sign-off, reviewer should `gh pr diff <pr#>` or `git diff <base>...
   <branch>` to see the actual change set. If a file declared in scope
   does not appear in the PR diff, the deletion/edit didn't land.

Promoted to: `Docs/phase-gates/methodology.md` Rule 2 sub-clause
(verification command targets) + Stage 6 VERIFY checklist amendment
(pre-grep `git status --short` gate). Applies retroactively: V4 archive
contract receives a "partial-merge state" amendment + corrective PR
restores the missed deletions.

---

## L21 — Risk:HIGH phases require FULL claim-by-claim grep verification, not sampled spot-check; negative claims ("X is NOT in Y") demand independent positive verification (2026-05-03, Phase 4b V4 design Q&A second-pass)

Symptom: V4 design Q&A v1 contained MEDIUM-severity factual error at Q3.1
line 121 ("`_combatAdapterInitialized` field at motor.cs:148 is NOT in any
LEGACY_SHADOW block"). Independent grep confirmed field IS inside
`#if BUDDAH_PREDICTION_LEGACY_SHADOW` block (motor.cs:143-149). Reviewer's
first-pass sign-off accepted the claim on faith because the wording sounded
confident. Second-pass deep-verify (triggered by user's "trust but verify"
follow-up) caught the error before IMPLEMENT phase. If first-pass had been
final, implementer following design literally would have only stripped
:382-396 wrapper, leaving :143-149 wrapper intact → post-define-strip
compile failure at :389/:392 (recoverable but doc-misleading; ~30min lost
debugging).

Cause: spot-check sampling sufficient for normal phases but insufficient
for risk:HIGH phases that touch atomic delete + race-condition-sensitive
code. Reviewer's trust-but-verify defaulted to "verify what the design
asserts is present" rather than "verify what the design asserts is
absent". Confident negative claims are structurally harder to invalidate
because they require exhaustive search to disprove.

Rule:
1. **Risk:HIGH phases (authority-flip, atomic-delete, wire-format-change,
   race-condition-sensitive) require FULL claim-by-claim grep verification
   at every sign-off boundary.** Spot-check sampling is forbidden.
2. **Negative claims demand positive verification.** Any "X is NOT in Y"
   statement in recon, design, or PR description must be independently
   grep-verified before sign-off accepts it. Reviewer cannot defer to
   implementer confidence; the absence-claim is the easier-to-miss class.
3. **Apply trust-hierarchy (Rule 2 sub-clause)** also to negative claims:
   downstream-evidence absence (no log line, no metric increment) does
   NOT prove upstream-state absence — verify the upstream directly.

Promoted to: `Docs/phase-gates/methodology.md` Rule 2 — "Independent
verification" sub-clauses (full-grep mandate + negative-claim verification).

---

## L20 — `git rm` of source files produces transient CS2001 "Source file could not be found" compile errors that recover automatically; these are downstream evidence of successful deletion, NOT runtime live references (2026-05-03, Phase 4b V4 smoke verify)

Symptom: V4 CLIENT smoke log contained 6 grep hits for symbols-supposed-to-be-deleted
(`BuddahPredictionCombatRouting`, `BuddahImpulseStep`, `BuddahPredictedImpulseEventQueue`)
at lines 46198/46232/46248 (file-path mentions) + 46357/46359/46361
(CS2001 errors: "Source file '...' could not be found"). Initial reviewer
grep `count` returned 6 hits — appeared to violate the V4 "0 dead-symbol
references in production logs" gate.

Cause: Unity's incremental compiler caches asset metadata between PlayMode
sessions. When `git rm` removes .cs files mid-checkout-cycle, the compiler
tries the cached path on next refresh, fails with CS2001, then re-scans
the asset database and recovers (Tundra ExitCode: 0). The error mentions
the deleted symbol's file path in the diagnostic, producing log hits that
look identical to runtime stack frames at grep level.

Rule:
1. **Distinguish compile-time evidence from runtime live references.**
   Grep hits in raw logs may be EITHER:
   - (a) Live runtime stack frames → real evidence of dead-code execution
         (BAD; merge blocker)
   - (b) Compile-time error context strings (CS2001 file-not-found,
         pre-recompile metadata cache stale-state) → downstream evidence
         that deletion succeeded (NEUTRAL; expected during transitions)
   - (c) Pre-deletion historical session data → multi-session pollution
         (covered by Methodology Rule 1 sub-rule on session-scoping)
2. Reviewer must inspect each hit's CONTEXT (line content, not just count)
   before flagging as gate violation. CS2001 + file-path-mention pattern
   is a compile-cache transient, not a runtime violation.
3. Cross-validate by checking cold-start recovery indicators (Tundra
   ExitCode: 0, subsequent successful PlayMode tick, wire format
   compatibility holding). Self-recovery within same session = benign.

Promoted to: `Docs/phase-gates/methodology.md` Rule 2 — "Independent
verification" should add a sub-clause on grep hit context inspection
for risk:HIGH cleanup phases.

---

## L19 — Authority-flip retest gates compare LEG axis only; pre-flip per-axis D-LOC impulse compare becomes structurally dead and must be removed (not gated) to avoid spurious warnings (2026-05-02, Phase 4b V2b Step 1 implementation)

Symptom: at the design phase of V2b Step 1 authority flip (NEW = rb-writing
authority, OLD = legacy shadow), the existing Phase 3b D-LOC impulse compare
at motor.cs:1481-1494 (`_realScratch.ImpulseRan != _shadowScratch.ImpulseRan`
+ `_realScratch.ShadowLastConsumedImpulseId != _shadowScratch.ShadowLastConsumedImpulseId`)
would fire spurious `[D-LOC] T=... impulse-` warnings on every consumed event
under post-flip semantics. Pre-flip, both `_realScratch` (OLD/authority) and
`_shadowScratch` (early-step pure-functional shadow) read OLD's
`_impulseEventQueue` with the same `eventData.EventId` space → cursor compare
was meaningful. Post-flip, `_realScratch` reads NEW's `CommandBus.ImpulseChannel`
with channel-internal `entry.Id` (and orthogonal cmd `LogicalId`); `_shadowScratch`
would still be OLD-driven → different ID spaces, cursor would always mismatch.

Cause: the early-shadow `BuddahImpulseStep.Run(...ref _shadowScratch)` call at
motor.cs:426-428 was Phase 3b's pure-functional determinism check on OLD's
impulse step function. NEW path's drain (channel ConsumeReady) is custom rb-
writing code that does NOT route through `BuddahImpulseStep.Run`. The early-
shadow's purpose evaporates once the authority is flipped — there's no NEW
counterpart to compare it against. Half-stripping (keep early-shadow but only
fire FATAL on `ImpulseRan` mismatch, not cursor) leaves dead code reachable
from the diff, which invites confusion.

Rule: any phase that flips authority (or otherwise repoints which side a
shadow scratch reads) must remove dead per-axis compare paths, not gate them
behind feature flags. Specifically: if the cursor space changes (different
ID generator), drop the cursor compare entirely. If the cross-state
identifier was only meaningful pre-flip, drop it. The replacement compare
(post-flip: `_realScratch` vs `_legacyShadowScratch` ran-flag + count) is
the sole authority for that axis going forward.

**Phase 4b applicability**: V2b Step 1 (this commit) removes
`BuddahImpulseStep.Run` early-shadow + `_shadowImpulseConsumedCount` counter
+ `_dLocImpulseDivCount` window counter + the D-LOC impulse compare block
+ the `imp-div=` / `imp-compared=` fields from `[D-LOC HEARTBEAT]`. Impulse
correctness verified exclusively via `[D-IMP LEG FATAL]` / `[D-IMP LEG HEARTBEAT]`
post-flip. Full session strict gate: `leg-imp-div = 0`, `[D-IMP LEG FATAL] = 0`,
`leg-imp-compared > 0`, no `[D-LOC] T=... impulse-` warnings (proves removal landed).

Promoted to: `Docs/phase-gates/active/v2b-step1-contract.md` Q4 amendment
(strict gate added "Zero spurious `[D-LOC] T=... impulse-` warnings"). Future
authority-flip phases must include explicit "dead-compare audit" in their
recon report.

---

## L18 — Dual-path migration must mirror OLD's full side-effect surface before tightening strict FATAL gate (2026-05-02, Phase 4b V2b Step 0 closeout)

(Naming note — V2b Step 1: HEARTBEAT prefix renamed `inv-imp-*` → `leg-imp-*`
and FATAL renamed `[D-IMP INV FATAL]` → `[D-IMP LEG FATAL]` to reflect the
post-flip semantics where OLD path = legacy shadow. Quoted strings below
preserved as the V2b Step 0 wire format for historical fidelity.)

Symptom: V2b Step 0 channel tick-stamping landed cleanly (`fb9d055` + fix-2
`0e67d2e`); Path A host-only passed strict (FATAL=0). Path B 2-peer fresh
build: CLIENT clean (compared=11, FATAL=0), HOST 11 reverse FATAL
(`ranOld=True ranNew=False`). Volume audit: HOST OLD `imp-compared=9`, NEW
`inv-imp-compared=9` — counter-aligned, but FATAL-per-tick because the
HOST-side serverside replica of the CLIENT-owned victim never received a
NEW-channel entry. OLD path on host enqueued via `TryQueueImpulseEvent`
(server-local) AND `QueueImpulseEventTargetRpc` (to remote owner). NEW
adapter β-branch only emitted the RPC.

Cause: V2a → V2b migration audited the rb-affecting drain semantics
(replay safety, tick gate) but NOT the **enqueue-site side-effect
surface**. OLD's `TryApplyServerAuthoritativeImpulse` does six things
unconditionally beyond constructing the event:
1. server-local `TryQueueImpulseEvent` (peer-instance #1 entry)
2. `QueueImpulseEventTargetRpc` to Owner (peer-instance #2 entry, dedup'd
   on host-owner case via EventId)
3. `IsPredictionModeActive()` gate (early-bail on Legacy mode)
4. cmd-side `_nextImpulseEventId++` (cross-peer Id alignment)
5. 6 DebugState field writes (Inspector/HUD observation)
6. 3 `LogVerbose` lines (audit trail)

NEW adapter mirrored only #2. Items #1 and #3 produce real cross-peer
divergence under strict FATAL gate. Items #4-#6 are observation-only and
can defer, but must be tracked. V2a's tolerant gate hid all of this; V2b
strict gate exposed it as the FATAL volume the audit assumed was
phase-skew residue.

Rule: any dual-path migration must perform a line-by-line OLD-side
side-effect audit before tightening strict gate. Categorize each item as
(a) compare-relevant — mirror in NEW immediately, (b) observation-only —
mirror or document drop with explicit justification, (c) architecturally
moot — explain why NEW design makes it irrelevant. "Looks unrelated" is
not a category. Per-peer enqueue topology asymmetry is THE most common
failure mode in this category — always verify both peer instances of the
target channel see the right entries.

Promoted to: `Docs/prediction-refactor-plan/07-side-effect-migration.md`
will be updated to require this audit as a phase-gate checklist item.
V2b Step 1 PRE-WORK: cross-fire dedup decision (dead `_recentEventIds`
check + cmd-side EventId field). V3 PRE-WORK: DebugState mirror or
audit-and-drop decision.

---

## L17 — Bidirectional phase-skew is the fingerprint of "observation-only" cross-phase drains, not real divergence (2026-05-02, Phase 4b V2a fix Path B verification)

Symptom: After L16 fix relocated `ConsumePendingImpulseEvents_InvertedShadow`
from `RunInputs` (replay-unsafe) to `TimeManager_OnPostTick` (replay-safe),
Path B 2-peer LAN smoke produced **5 forward `[D-IMP INV FATAL]` on CLIENT**
(`ranOld=False ranNew=True cntOld=0 cntNew=1`) and **6 reverse FATAL on
HOST** (`ranOld=True ranNew=False cntOld=1 cntNew=0`). Volume audit on both
sides was clean: CLIENT `inv-imp-compared = OLD imp-compared = Recv = 6`;
HOST `inv-imp-compared = OLD imp-compared = 7`. `inv-imp-div = 0` on both
sides at final HB. `D-LOC FATAL = 0` on both sides. The strict `FATAL = 0`
gate appeared violated, but no real volume or value divergence exists.

Cause: OLD path's `ConsumePendingImpulseEvents` drains during the forward
`RunInputs` pass; NEW path (post-L16) drains during `PostTick`. When an
RPC arrives between these two phases on a given tick, the side whose
phase has already passed observes the event one tick later than the
other side. The cross-tick lag manifests as `ranOld != ranNew` for a
single tick, producing a FATAL emit. The total-event audit
(`compared == OLD imp-compared == Recv`) is unaffected because both
paths still observe every event; only the tick-of-observation differs by
at most one. Two separate motors (HOST vs CLIENT) each see their own
buddah's events; the FATAL direction depends on which side's phase the
RPC arrives between.

Rule: **A bidirectional FATAL pattern (forward FATAL on one side +
mirror-image reverse FATAL on the other side, with both sides showing
matching `compared == OLD == Recv` totals and `div = 0` at final HB) is
the phase-skew fingerprint, NOT a real divergence.** Do not block on
strict `FATAL = 0` for observation-only V2a-style gates when this
pattern holds. ALWAYS verify the bidirectional symmetry: a unidirectional
FATAL pattern (only forward, only reverse) OR a volume mismatch
(`compared != OLD imp-compared`) IS a real divergence and must block.
Critically: **phase-skew is harmless under V2a observation** (OLD path
remains rb authority, NEW path is diagnostic-only) but **would be a real
desync under V2b authority** because each peer would write rb on a
different tick. V2b STEP 0 (non-negotiable) is to tick-stamp
`BuddahPredictionEventChannel<T>.Entry` with `EventTick`, port OLD's
`ConsumeReady(currentTick, callback)` semantics into
`BuddahPredictionCommandBus`, and re-verify Path A + Path B with strict
`FATAL = 0` BEFORE the authority flip. The phase-skew tolerance lives in
V2a-era observation gates only; production-path gates require strict
zero.

Promoted to: log-only (V2b STEP 0 gate cross-referenced from
`Docs/prediction-refactor-plan/06-command-bus.md` when V2b is scoped;
V2a-fix PR description carries the bidirectional phase-skew block as the
re-test gate template for any future observation-only inverted-shadow
phases).

---

## L16 — Single-consumer FIFO channel + FishNet reconcile replay = structural blindness (2026-05-02, Phase 4b V2a 2-peer post-merge fix)

Symptom: V2a observation gate (`[D-IMP INV HEARTBEAT] inv-imp-compared`)
passed in host-only single-peer (compared=1, div=0) but produced
**inv-imp-compared=0 across 73 heartbeats** in the 2-peer LAN test on
the CLIENT, even with `[CommandBus]:Recv ch=Impulse` arriving twice on
the bus and `_realScratch.ImpulseRan` confirmed true twice (OLD-side
`imp-compared=2`). Both `inv-imp-div=0` and `[D-IMP INV FATAL]=0` were
vacuously satisfied — the compare predicate never fired because both
sides of `_realScratch.ImpulseRan || _legacyShadowScratch.ImpulseRan`
read false at the compare moment.

Cause: `BuddahPredictionEventChannel<T>` is a pure FIFO ring buffer
(no `EventTick` field, no `ConsumeReady(currentTick, ...)` semantics).
The drain `ConsumePendingImpulseEvents_InvertedShadow` was placed
inside `RunInputs` (the `[Replicate]` callback). On non-server peers
under FishNet CSP, RunInputs is replayed N times per tick during
reconcile. Replay 1 calls `channel.TryDequeue` and removes the entry
permanently; replays 2..N see an empty channel and the per-replay
reset `_legacyShadowScratch = default` (motor.cs:388-394) clears the
ImpulseRan flag set by replay 1. PostTick `Shadow_CompareAndReport`
reads the LAST replay's scratch — which is empty — so the compare
predicate stays false and `_legacyShadowImpulseComparedCount` never
increments. OLD path was unaffected because `_impulseEventQueue`
is tick-stamped and `ConsumeReady` re-emits the same event on every
replay until the EventTick passes.

Rule: **Any single-consumer FIFO that participates in shadow/observation
under reconcile MUST drain in a lifecycle hook that fires exactly once
per tick (PostTick), not in `[Replicate]`.** OR, if the consumer must
run inside the replay loop (e.g., V2b authority-flip where the channel
drives rb writes), the channel itself MUST adopt a tick-stamped model
mirroring OLD's `EventTick` + `ConsumeReady` — events stay queued across
replays until their stamped tick passes, then a deterministic single
consume fires per tick. **Tick-stamping (OLD path's `EventTick` +
`ConsumeReady`) is the gold standard for any V2b/V3/V4 channel
consumer that needs to survive reconcile replay.** The PostTick
relocation in this fix is a narrow observation-only patch; promoting
NEW path to authority (V2b) WITHOUT first tick-stamping the channel
will reintroduce this same blindness in production code where it
CANNOT be a false-pass — it will be a real desync.

Promoted to: log-only (V2b prerequisite gate to be added to
`Docs/prediction-refactor-plan/06-command-bus.md` when V2b is scoped).

---

## L15 — Shadow comparators must defend against `Compare(default, default) != equals` foot-guns (2026-04-19, Phase 3d V2 rerun)

Symptom: Phase 3d V2 host-only rerun produced 4550 `[D-LOC]` per-field
warnings + 74 `[D-LOC FATAL]` emissions, ALL concentrated on a single
field (`SnapshotRotation`) with a fixed 180.000000° delta. All 14 other
handoff fields were bit-identical. Divergence vanished exactly the
instant `hof-compared` flipped 0→1 (real handoff consume fires on both
sides). Raw Editor.log scrape confirmed zero variance in the delta value
— every warning reported `angle-deg=180.000000`.

Cause: the shadow compare at `BuddahPredictedMotor.cs:1599` used
`Quaternion.Angle(realHof.SnapshotRotation, shadowHof.SnapshotRotation)`.
C# / Unity's `default(Quaternion)` is `(0, 0, 0, 0)` — NOT the identity
quaternion `(0, 0, 0, 1)`. When both sides hold the uninitialized zero
quaternion (the typical steady state while `_handoffState.IsActive ==
false`, since `BuddahPredictedLaunchHandoffResolver.Advance` only writes
`CurrentState` / `BlendAlpha` / `IsActive` and never touches
`SnapshotRotation`), the dot product is `0`, so `Quaternion.Angle`
returns `2 * acos(0) * Rad2Deg = 180°`. Both quaternions ARE equal. Only
the compare tool misreports. The underlying shadow math (resolver +
snapshot flow) was correct — DP-8 refactor was behavior-neutral, all
3a/3b/3c parity still held.

Rule going forward:
  (a) Before `Quaternion.Angle(a, b)` in any shadow or parity compare,
      normalize zero-quaternions to identity — or verify the compared
      slot is gated such that `default(Quaternion)` is structurally
      impossible. The minimal fix shape is:
      ```csharp
      if (a.x == 0f && a.y == 0f && a.z == 0f && a.w == 0f) a = Quaternion.identity;
      if (b.x == 0f && b.y == 0f && b.z == 0f && b.w == 0f) b = Quaternion.identity;
      float delta = Quaternion.Angle(a, b);
      ```
  (b) Do NOT fix this by gating the compare on an `IsActive` flag alone.
      That masks potential real divergence when the state is legitimately
      uninitialized on only one side (asymmetric init bugs). The
      normalization fix preserves divergence detection for any non-zero
      mismatch while defusing the zero-vs-zero foot-gun.
  (c) Audit every `Quaternion.Angle` / `Quaternion.Dot` / `Quaternion.Lerp`
      used in shadow compare or parity validation paths for the same
      vulnerability before the next shadow phase lands.
  (d) This foot-gun generalizes: any `Compare(default, default) !=
      "equals"` shape is a candidate false-positive generator. Examples
      beyond Quaternion: custom `Distance(Color, Color)` implementations
      that divide by magnitude, serialized-field hash functions that
      special-case zero, etc. Add an explicit "is populated" check OR a
      normalize step at the comparator boundary.

Applies to: Phase 3d handoff shadow SnapshotRotation compare (fixed in
this patch), Phase 3b teleport rotation compare (`motor.cs:1376`,
structurally vulnerable — `eventData.TargetRotation = default(Quaternion)`
would trigger the same false positive; gate at motor.cs:1361 requires
`TeleportRan=true` which makes real-session occurrence improbable but
not impossible — tracked as
`agent-exchange/handoff/phase-8-cleanup-queue.md` Entry 2), future
Phase 4-6 reconcile-delivery compare paths.

Promoted to: `log-only` (phase-specific compare fixes are localized).
`agent-exchange/handoff/phase-8-cleanup-queue.md` Entry 2 carries the
3b teleport normalization task. If a broader audit finds additional
sites, promote to `Docs/prediction-refactor-plan/13-validation-gates.md`
as a general shadow-comparator hygiene rule.

## L13 — Shadow PASS requires compared-count > 0, not just divergence = 0 (2026-04-19, Phase 3b V5 R2 close-out)

Symptom: Phase 3b V5 R2 CLIENT peer showed teleport shadow with 0 divergence
across every captured heartbeat (tel-div=0), 0 non-heartbeat `[D-LOC]`, 0
FATAL — but `tel-compared=0` across the entire session window. If we had
used only divergence=0 as the PASS criterion, we would have shipped 3b
without ever validating the teleport shadow code path once.

Cause: the cumulative `tel-compared` counter (added late in 3b) exposed the
pre-existing owner→server→owner teleport RPC silent-drop (see L12). Before
that counter existed, "shadow quiet" was indistinguishable from "shadow
never exercised". Visual respawn still worked on client because FishNet's
reconcile broadcasts the server-side post-teleport rigidbody state — so the
absence of prediction-local teleport side-effects (`ResetModifiers`,
`ResetImpulseQueue`, `ResetPushGrace`, `RebaseTrails`, `SnapProgress`) was
invisible to gameplay. This is a plausible candidate root cause for the
V3 / V4 all-Buddah-shake and remote-flicker symptoms Phase 7 is deferred
to address.

Rule going forward:
  (a) Every shadow-path PASS judgement must assert BOTH divergence=0 AND
      compared-count > 0 (per-peer, with per-peer coverage combinations
      allowed to satisfy the gate for paths whose RPC routing only fires
      on a subset of peers).
  (b) Phase 3c / 3d / future-phase shadow gates MUST include a compared-
      count assertion. "Loc-div=0" style gates are not sufficient.
  (c) When a shadow stays completely silent across a full session, the
      default assumption is "path was never exercised", NOT "path is
      parity-clean". Investigate coverage before celebrating.

Applies to: Phase 3c Modifier shadow, Phase 3d Handoff shadow, Phase 4-6
cutover validation gates.

Promoted to: `Docs/prediction-refactor-plan/13-validation-gates.md` (Phase
3/4+ shadow gate definition should reference this rule).

## L12 — Owner→Server→Owner two-hop predicted-motor RPC chain must have failure visibility (2026-04-19, Phase 3b V5 R2)

Symptom: V5 R2 CLIENT peer `tel-compared=0` across full session despite
user-confirmed visual fall-respawn. HOST peer `tel-compared=3` in same
session proves the server-direct teleport enqueue path works end-to-end.
The client-side chain is: `BuddahRespawn` → bridge → motor.
`RequestAuthoritativeTeleportFromOwner` (BuddahPredictedMotor.cs:886-931) →
ServerRpc → server-side enqueue + TargetRpc back to owner → owner-side
enqueue. On client, the chain makes TWO RPC hops (outbound ServerRpc, return
TargetRpc). Impulse path is a single hop (server is the originator) and
works cleanly (`imp-compared=2` on client).

Cause: `RequestAuthoritativeTeleportFromOwner` at motor.cs:930 unconditionally
returns `true` on the non-server branch — caller (bridge → BuddahRespawn)
treats this as "primary path succeeded" and commits, skipping the legacy
fallback at `BuddahRespawn.cs:168` (`TeleportToWorldPose`, direct
`rb.position = targetPosition`). Whether the ServerRpc actually reached
server, whether the TargetRpc return-leg actually reached client, neither
is reported back. Visual respawn still happens because server-side
`TryApplyServerAuthoritativeTeleport` (motor.cs:713-766) enqueues on
server-side motor → server's tick consumes → server's `rb.position` moves
→ FishNet reconcile broadcasts position delta → client's reconcile snaps
rb.position to match. The client's own `_pendingTeleportEvent` slot is
never populated, so shadow's pre-consume snapshot stays empty.

Rule going forward:
  (a) Any owner→server→owner two-hop predicted-motor RPC chain must EITHER
      (i) propagate the server-side enqueue result back to the owner via
      the return-leg TargetRpc (or a separate result TargetRpc), and have
      the owner commit only on confirmed success, OR (ii) not commit to
      the primary path on the owner side — let the legacy fallback cover
      silent-drop scenarios until cut-over is complete.
  (b) `RequestAuthoritativeTeleportFromOwner` at motor.cs:886-931 needs
      fix before Phase 6 teleport cutover. Candidates (ranked by evidence):
      (1) FishNet `RequestTeleportServerRpc` outbound silent drop due to
      observer state or RequireOwnership race; (2) `QueueTeleportEventTargetRpc`
      return-leg drop due to payload size (14 fields incl Quaternion) or
      tick-timing. Verbose micro-run before writing fix is mandatory.
  (c) Until (a) is implemented, validation of client-initiated predicted
      teleport paths is blocked. Phase 3b accepts V5 R2 as conditional
      PASS on host-validated shadow + defers client-path validation to
      post-Phase-6.

Applies to: Phase 6 teleport cutover (blocking fix), Phase 3d handoff
shadow (same owner→server→owner pattern in `RequestAuthoritativeLaunchHandoffFromOwner`
motor.cs:768-817; verify same silent-return pattern and plan the same
L12 fix shape there).

Promoted to: `Docs/prediction-refactor-plan/phase-6-prerequisites.md`
(new file, this one bullet).

---

## L11 — Self-cast push projectile fizzles silently at the attacker-hit gate (2026-04-19, Phase 3b V5 Run 2)

Symptom: V5 Run 2 CLIENT peer shows 0 non-heartbeat `[D-LOC]`, 0 FATAL, clean
parity — but cumulative `imp-compared` and `tel-compared` stay at 0 across 8
heartbeats spanning the full captured window. User confirmed self-push with the
normal `push_projectile_hands` skill and at least one respawn, so shadow ought to
have observed events.

Root cause: `Skill_PushProjectileHands` (the normal, non-anti variant) fires
through `BuddahHandControl.SpawnProjectilePushServer`, which leaves
`allowSelfHit` at its default `false` (BuddahHandControl.cs:804 default,
invoked at BuddahHandControl.cs:593 with 7 positional args). The projectile's
trigger handler at HandPushProjectileRuntime.cs:204 then gates
`if (!_allowAttackerHit && victimNO == _attacker) return;` — self-hits are
early-returned BEFORE `BuddahPredictionCombatRouting.TryRouteImpulse`. Nothing
reaches `TryApplyServerAuthoritativeImpulse`, nothing enqueues on either peer.
Only the Anti variant (`Skill_PushProjectileHands_Anti.cs:50`) sets
`allowSelfHit: true`. No ScriptableObject field exposes this — it is hard-wired
per-skill in C#.

Rule going forward:
  (a) Shadow coverage for impulse paths cannot be validated by self-push with
      the normal `push_projectile_hands`. Coverage tests must use cross-peer
      pushes (peer A casts toward peer B's Buddah) OR the Anti skill.
  (b) When a V5-style gate asserts `imp-compared > 0` and the counter stays 0,
      do NOT assume the shadow snapshot/queue copy is broken. First verify that
      the skill actually reached `TryRouteImpulse` — easiest proof is to cross-
      peer push and watch the counter jump.
  (c) `allowAttackerHit` being hardcoded in C# (not on the asset) means any
      future "allow self-push for testing" toggle requires a code change. Do
      not add such a toggle to production skill configs.

Applies to: Phase 3b V5 coverage gate, all future cross-peer shadow coverage
phases (3c modifier cast-on-self, 3d handoff cast-on-self).

Promoted to: log-only (scope is narrow to impulse-cast tests).

## L10 — Skill event-queue routing is gated by `IsPredictionModeActive()`; fallback paths bypass the motor queue (2026-04-19, Phase 3b audit)

Symptom: V5 Run 2 CLIENT peer ran for 60s+, shadow locomotion heartbeats
emitted with divergences=0 throughout, yet `imp-compared=0 tel-compared=0` for
the whole session. Shadow impulse/teleport step invocation was verified (no
`IsOwner`/`IsServerInitialized` gate on the pre-consume block at
BuddahPredictedMotor.cs:323-336). The gap was in the upstream routing.

Root cause:
  (1) Push projectile → `BuddahPredictionCombatRouting.TryRouteImpulse`
      (BuddahPredictionCombatRouting.cs:25) gates routing on
      `bootstrap.IsPredictionModeActive() == true`. On true, calls
      `predictedMotor.TryApplyServerAuthoritativeImpulse` →
      `TryQueueImpulseEvent` → `_impulseEventQueue.TryEnqueue` — shadow sees it.
      On false, falls back to `PushTargetBox` or the legacy
      `BuddahMovement.ApplyPushImpulseAndTorqueTargetRpc` →
      `ApplyPushAndTorqueLocal` → direct `rb.AddForce(impulse, Impulse)` at
      BuddahMovement.cs:445 — queue bypassed, shadow blind.
  (2) Respawn → `BuddahRespawn.TeleportToTrackProgress` (line 156) gates on
      `predictionBootstrap.IsPredictionModeActive() && predictionRespawnBridge
      != null`. On true, bridges into `predictedMotor
      .RequestAuthoritativeTeleportFromOwner` → `TryQueueTeleportEvent` →
      `_pendingTeleportEvent` — shadow sees it. On false, falls back to
      `BuddahRespawn.TeleportToWorldPose` — direct `rb.position = targetPosition`
      at line 187 — queue bypassed, shadow blind.
  Each gameplay path has a predicted-queue branch AND a legacy-direct-rb
  branch. Shadow only sees the predicted-queue branch.

Rule going forward:
  (a) Shadow coverage assertions (`imp-compared > 0`, `tel-compared > 0`) prove
      the predicted-queue branch was exercised, not just that the gameplay
      action happened. A green game action + 0 counter = the action took the
      legacy branch.
  (b) When writing shadow coverage tests, explicitly note whether the test
      depends on `IsPredictionModeActive() == true` on every peer involved. For
      push, both the attacker's server-side and the victim's owner-side must be
      in PredictionV2 mode; for respawn, the bootstrap and the respawn bridge
      must both be present and active.
  (c) Phase 4 authority cut-over should collapse the dual-branch pattern by
      deleting the legacy-direct-rb branches. Until then, coverage tests must
      verify the predicted branch wins, not just that the gameplay succeeded.

Applies to: Phase 3b coverage gate (impulse, teleport), Phase 3c modifier
coverage gate (same pattern: modifier commands have legacy + predicted paths),
Phase 3d handoff coverage gate.

Promoted to: log-only (narrow to dual-branch coverage gating).

---

## L9 — Pre-existing motor quirk: ClampPlanarSpeed strips same-tick queued impulses (2026-04-18, Phase 3b audit)

Observed during Phase 3b pre-execution audit (no failure yet, preventive entry).

Motor flow in `BuddahPredictedMotor.RunInputs`:
  1. `ConsumePendingImpulseEvents(currentTick)` — queues `_predictionRigidbody.AddForce(impulse, ForceMode.Impulse)` into `_pendingForces`. Not yet drained to PhysX.
  2. `ClampPlanarSpeed(...)` — if planar speed over cap, calls `_predictionRigidbody.Velocity(clampedVel)`. Per `PredictionRigidbody.Velocity` (PredictionRigidbody.cs:358), this writes `rb.velocity` immediately AND calls `RemoveForces(nonAngular: true)`, which STRIPS pending `AddForce` / `AddRelativeForce` / `AddExplosiveForce` entries — including the impulse queued one step prior.
  3. `_predictionRigidbody.Simulate()` — drains remaining queue.

So: when an impulse arrives on a tick that also triggers clamp, the linear component of the impulse is silently dropped at the PhysX layer. The torque component survives (RemoveForces(nonAngular:true) preserves AddTorque entries).

Rule going forward:
  (a) This is pre-existing motor behavior. Phase 3b shadow MUST mirror it bit-for-bit — shadow's `ShadowLastConsumedImpulseId` still marks the event consumed (it WAS removed from the queue), only its PhysX effect is partially lost. Do NOT "fix" this in 3b. Commanded-value compare tolerates it; velocity-delta compare would not, which is one reason 3b defers velocity-delta.
  (b) Phase 4 authority cut-over must explicitly evaluate whether to preserve or change this behavior. Candidates: re-order consume + clamp so impulse runs after clamp; or scale impulse before clamp instead of dropping. Decision belongs to Phase 4 design, not 3b.
  (c) If Phase 4 changes the behavior, an L-entry for the behavior flip is mandatory so shadow comparators in later phases stay aligned.

Applies to: Phase 3b shadow (accept as-is), Phase 4 authority cut-over (must decide), Phase 3c/3d shadows (no direct impact).

---

## L8 — Shadow parity must mirror motor's intermediate transformations, not idealized Euler (2026-04-18, Phase 3a V2 Run 1)

Symptom: 99 [D-LOC] warnings on V2 Run 1. Two distinct patterns:
  - fwd=7.5→5.0→2.5 three-tick ramp on T=3245-3247
  - clamp-gate mismatch realClamp=True shadowClamp=False sustained across ~300 ticks

Root cause 1: Shadow consumed raw input.Throttle / input.Steering. Motor applies
ApplyLaunchHandoffInputScaling (motor.cs:1537) which decays these values during
Blend/Inherit launch phases. Shadow was mathematically correct against idealized
input but wrong against motor's actual input.

Root cause 2: BuildTickContext read rb.velocity AFTER ClampPlanarSpeed ran.
Motor's ClampPlanarSpeed writes via _predictionRigidbody.Velocity() which updates
rb.velocity IMMEDIATELY (PredictionRigidbody.cs:363). Shadow saw post-clamp state
and its ClampingApplied flag could never evaluate True when motor's did.

Rule going forward:
  (a) Shadow steps take the motor's POST-transformation values as inputs via
      TickContext parameters — not raw replicate data. If motor transforms input
      X into X' before consuming it, shadow must receive X', not X.
  (b) Any motor state that is read during gate evaluation or formula execution
      AND mutated later in the same tick must be snapshotted at the read-point,
      not looked up live by shadow. Precedent: _shadowPreClampVelocity (state
      timing). The earlier _shadowAuthHandoffSnapshot precedent was designed but
      dropped once the motor's own early-return gate was deemed sufficient — see
      Phase 3a commit "Gate mirror" section for details.
  (c) "Independence gaps" (places where shadow still consumes motor's computed
      value instead of recomputing) are acceptable as long as they are explicitly
      called out in the commit message and deferred to a named later phase. 3a
      defers resolver math to 3c and handoff scaling to 3d.

Applies to: all subsequent shadow phases (3b Impulse+Teleport, 3c Modifier, 3d Handoff).

---

## 2026-04-18 | refactor | prediction | med (Phase 4 watchpoint)

**L7 - Lifecycle hooks can silently double-fire state-clearing calls**
- **What happened**: Phase 2 V8 expected 2 `[CommandBus]:ClearAll` lines per Buddah spawn (one bus-internal, one bridge-wrap). Actual log showed 4 lines per spawn across all 3 runs. Cause: `BuddahMovementModeSwitcher.Awake()` and `BuddahMovementModeSwitcher.OnEnable()` both call `ApplyRuntimeMode(force:true)`, so `BuddahPredictionLegacyIsolationBridge.ApplyMode` runs twice on spawn, each producing a ClearAll pair. Harmless at Phase 2 because no adapter enqueues yet, so channels are empty both times.
- **Cause**: Defensive duplication in a lifecycle-sensitive component. Awake and OnEnable normally fire sequentially on an enabled GameObject; the author hedged by calling ApplyMode from both.
- **Rule**: When a component's role includes clearing or resetting state, audit every lifecycle hook (`Awake`, `OnEnable`, `OnNetworkStarted`, `Start`) for calls into that clear. Document whether double-fire is intended. For Phase 4: any adapter that enqueues into the CommandBus during init (`Awake`/`OnEnable`/`OnStartNetwork`) must either (a) enqueue AFTER Switcher's double-ApplyMode completes (e.g., defer to first `[Replicate]` tick), or (b) add an "init-complete" gate in the Switcher so ApplyMode short-circuits on repeat calls within one frame. Do not silently accept the double-fire, because the second ClearAll will nuke legitimate pending events.
- **Promoted to**: log-only (low severity at Phase 2, revisit at Phase 4 with concrete adapter code).

## 2026-04-18 | refactor | prediction | high

**L6 - Tests must match what the phase actually wires up**
- **What happened**: Phase 1 Serialization Gate was designed as a gameplay-driven check expecting `[SerGate]` log to surface non-default values when teleport/handoff/skills triggered. Two 20+ second 2-peer playtests both showed all-default values (`pendingTp=False`, `impHead=0`, etc.); second test confirmed a skill cast occurred but `impHead` stayed 0. Initially misread as either a gameplay-window issue or a serializer bug.
- **Cause**: Phase 1 deliberately zero-inits new reconcile fields and does NOT mirror motor private state (`_impulseEventQueue`, `_pendingTeleportEvent`, `_awaitingAuthoritativeLaunchHandoff`, etc.) into them - that mirroring is Phase 2/3 work, and the new window's Phase 1 report stated this explicitly. The test was designed against state the phase explicitly does not touch, so no amount of gameplay can exercise the new fields.
- **Rule**: Before designing a verification test for a phase, re-read the phase's scope contract. Identify which old paths still own state and which new paths are not yet wired. If the test depends on data flowing through code that this phase explicitly does not touch, the test is invalid for this phase. For "shape exists but nothing writes to it yet" phases, use a forced-value probe: server-side direct write of distinctive constants (e.g., `0xDEADBEEF`, `(123.45, 67.89, -12.34)`) into the exact field inside `CreateReconcile()`, then verify the constants arrive on a `!IsOwner` peer. Remove the probe before phase commit.
- **Promoted to**: log-only.

## 2026-04-17 | refactor | prediction | high (preventive)

**L5 - Reconcile/Input data serializer must include new fields**
- **What happened**: Not yet observed; flagged during Phase 1 authorization.
- **Cause**: When extending FishNet `IReconcileData` / `IReplicateData`, compile passes and a quick playtest may also pass, but reconcile silently loses the new field's value if the serializer (auto-attribute or hand-written Write/Read) does not include it. Symptom is a delayed desync, not a build error.
- **Rule**: After adding any field to ReconcileData or InputData, audit the serialization path. If auto-attribute, confirm the attribute is present and the type is supported. If hand-written, confirm both Write and Read cover the new field. Add a one-shot `Debug.Log` of one new field value in `[Reconcile]` for the first playtest and confirm a non-default value arrives on a remote peer.
- **Promoted to**: `Docs/prediction-refactor-plan/03-data-contracts.md` Serialization Gate section.

## 2026-04-17 | refactor | prediction | med

**L4 - Strategy pivot must update onboarding artifacts**
- **What happened**: Plan pivoted from "run FishNet API spike" to "trust-and-warn at call sites". `16-api-spike-checklist.md` and `handoff-context.md` were updated, but the opening prompt template handed to the next agent still said "first run the spike", causing the new window to (correctly) flag the contradiction and refuse to proceed.
- **Cause**: Pivot didn't propagate to all entry points (prompts, status headers, skill stubs).
- **Rule**: When a strategy pivots, grep the plan for the pre-pivot phrasing and update every hit. Update onboarding/handoff prompts, index status header, and any skill files that reference the old strategy. Treat the pivot as not landed until grep is clean.
- **Promoted to**: `Docs/prediction-refactor-plan/handoff-context.md` Pivot Sync Checklist footer.

## 2026-04-17 | any | any | high

**L3 - Delete tracked files through the VCS, not around it** *(updated 2026-04-18: repo migrated to git-only; Perforce is now decoration)*
- **What happened**: `rm Assets/Scripts/Sandbox/PredictionApiSpike.cs` returned `Operation not permitted` because Perforce kept tracked files read-only at the time. On 2026-04-18 the user confirmed the project now runs on git; `p4` is not used for commits anymore even though the workspace path still contains "Perforce".
- **Cause**: Agent reached for a bare `rm` without thinking about which VCS owns the index and how the deletion becomes a staged change.
- **Rule**: This repo is git. Delete tracked files with `git rm <path>` so the deletion is staged automatically. If you used plain `rm` (allowed under git because files are writable), immediately follow with `git add -A` to stage the delete. Never commit a state where a tracked file is gone from disk but still present in the index. Do not run any `p4` command - it will either do nothing useful or create noise. The directory name "Perforce" in the absolute path is historical; trust git.
- **Promoted to**: `Docs/safety.md` Absolutely Forbidden (rewritten 2026-04-18).

## 2026-04-17 | refactor | prediction | high

**L2 - FishNet API names: grep source, do not infer**
- **What happened**: Wrote `PredictionRigidbody.Position(...)` and `.Rotation(...)` in plan docs and spike code. Actual API is `MovePosition(Vector3)` / `MoveRotation(Quaternion)` (Unity-Rigidbody-style naming). Caught at compile, but propagated to four plan docs first.
- **Cause**: Inferred the API name from naming intuition instead of grepping the FishNet source tree.
- **Rule**: Before the first call site of any FishNet API symbol, grep `Assets/FishNet/Runtime/` for the exact method name. Never write a FishNet API call from memory. If the symbol is not in the source tree, the assumption is wrong - stop and resolve before writing.
- **Promoted to**: `Docs/prediction-refactor-plan/16-api-spike-checklist.md` preamble.

## 2026-04-17 | refactor | prediction | med

**L1 - Phase stub completeness: cross-reference the API spec, not the example list**
- **What happened**: Phase 0 created 3 payload stubs (Impulse / Teleport / Modifier) when the canonical command bus API requires 4 (missing `HandoffCmd`).
- **Cause**: Implementer treated the `Payloads/*.cs (ImpulseCmd, TeleportCmd, ModifierCmd...)` example in `02-directory-structure.md` as exhaustive.
- **Rule**: When a phase creates a family of files (steps, adapters, payloads, channels), the count comes from the spec that defines the full API surface, not from an example list. For Phase 0-2 in this refactor that means: structure (02) + data contracts (03) + command bus API (06) + adapters (09).
- **Promoted to**: `Docs/prediction-refactor-plan/12-migration-sequence.md` Phase 0 cross-reference gate.
