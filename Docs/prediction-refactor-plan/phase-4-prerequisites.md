# Phase 4 Prerequisites

Gates that must be closed before Phase 4 (first concrete adapter cut-over)
can begin. Each entry has a reference lesson ID, a blocking rationale, and
a concrete acceptance criterion.

---

## Prereq-1 — L7: BuddahMovementModeSwitcher double-fires ApplyMode on spawn

**Source**: `Docs/lessons-log.md` L7 (2026-04-18, Phase 2 V8 post-run).

**Why blocking**: Phase 4 lands the first concrete adapter (Locomotion +
Impulse cut-over). `BuddahMovementModeSwitcher.Awake()` and
`BuddahMovementModeSwitcher.OnEnable()` both call
`ApplyRuntimeMode(force:true)`, which routes through
`BuddahPredictionLegacyIsolationBridge.ApplyMode` → CommandBus `ClearAll`.
Phase 2 V8 captured 4 `[CommandBus]:ClearAll` lines per Buddah spawn (2
expected: one bus-internal, one bridge-wrap). Harmless at Phase 2 because
no adapter enqueues yet; **blocking at Phase 4** because any adapter that
enqueues during `Awake`/`OnEnable`/`OnStartNetwork` will have its enqueue
wiped by the second `ClearAll` — **causing silent data loss on Buddah
spawn** (adapter thinks it enqueued, bus is empty, motor never sees the
intent).

**Recommended fix — Option B (adapter-side defer)**:

Adapters do NOT enqueue during Unity component-init hooks. They expose an
`Initialize()` method invoked from a coordinator (likely
`BuddahPredictionBootstrap.OnNetworkStarted` or equivalent late-bind hook)
that fires AFTER the Switcher's double-`ApplyMode` sequence settles. OR
adapters register for the motor's first `[Replicate]` callback and enqueue
any pending intent at that moment.

**Why Option B over A/C** (see `agent-exchange/handoff/2026-04-19-phase3c-audit.md`
§3 for full comparison):
- **Option A (Switcher-local debounce)** fixes the symptom in one file,
  but any other component with a similar doubled-lifecycle pattern
  remains exposed. Does not generalise the invariant.
- **Option C (Switcher exposes `IsInitComplete` property, adapters poll)**
  requires per-adapter manual discipline. One forgotten check anywhere in
  Phase 4+ re-introduces the ClearAll-burst bug silently.
- **Option B (adapter defers until first replicate)** puts the invariant
  on the adapter side where the enqueue actually happens. Phase 4's first
  adapter sets the contract; all subsequent adapters inherit it. Single
  place to audit going forward.

**Acceptance criterion**: Phase 4's first adapter ships with explicit
`Initialize()` hook (or equivalent first-replicate late-bind), AND a
V8-style re-run confirms **2 `[CommandBus]:ClearAll` lines per Buddah
spawn** (not 4) — OR, equivalently, a log probe inside the adapter that
proves its pending-enqueue survives the Switcher double-fire window.

**Implementation note**: the coordinator contract (who calls
`adapter.Initialize`, when, and what happens if `Initialize` fires before
network ready) is TBD at Phase 4 design time. Recommend sketching it in
the Phase 4 pre-execution audit before any adapter code is written.

**Companion work**: once Option B lands for the first adapter, the Phase
4 audit should extract the `Initialize`-style late-bind into a shared
contract (interface or abstract base) so subsequent adapters inherit the
pattern by construction instead of by convention.

---
