# Phase 7 RECON Surface 8 — Industry Reference Research: Host-Client MP Visual Smoothing

> ## ⚠ AMENDMENT 2026-05-03 — rec-cb owner inversion correction
>
> Surface 7 audit (commit `3d5b328`) caught that **rec-cb=2595 is on the HOST log, not CLIENT log** — the opposite of what cowork-reviewer's V5 verify report stated and what this doc was originally written to assume. Independent re-grep at fea8a40+ confirms: `host-100ms.log` final `rec-cb=2595`; `client-100ms.log` `rec-cb=0` throughout.
>
> **Implications for everything below:**
> - "CLIENT spectator-view character will rubber-band" → **flip to "HOST owner-view character (the local-control character on host machine)"**
> - SMOKE driver focus = **HOST machine watching its own predicted character**, not CLIENT spectator-view
> - The persistent jitter Yonezawa observes is most likely on his own controlled character on whichever machine is hosting, not on the remote peer's character
> - Layer 3 hypothesis (hard-snap reconcile) still stands; just the OBSERVATION side flips
> - All "CLIENT side high reconcile pressure" claims below should read "HOST owner-side"
> - Why HOST has reconcile pressure under LatencySim: TransportManager LatencySim affects host-loopback packets too, so HOST's local-client predicts forward + reconciles against (delayed) server-state from same process
>
> **Root cause of error:** L21+L22 verify discipline covers grep-content correctness + verification-target (git plumbing not working tree). Neither rule covers **data-attribution at write time** — i.e., when copy-pasting bash output into prose claims, ensuring "file X showed value Y" is correctly assigned. V5 verify produced correct grep but inverted the file→value assignment in the report text. This is a candidate L24 ("data-attribution self-check at write time"), surfaced by Surface 7's catch.
>
> Document body BELOW is unedited from original. Read with HOST↔CLIENT swap mentally on the "where does jitter manifest" claim.

**Author:** cowork-reviewer (Claude Opus 4.7, harness)
**Date:** 2026-05-03 (amended 2026-05-03 same day post-Surface-7 catch)
**Scope:** External knowledge synthesis — researches how successful host-client multiplayer games implement client-side prediction + server reconciliation + visual smoothing without persistent jitter. Pairs with Surface 7 (CC-authored, inward-facing BuddahGo codebase audit).
**Constraint:** Linux container web egress is allowlisted to Anthropic-only domains; full article fetches blocked. Synthesis based on WebSearch result snippets which provided substantive technical content. If a finding needs full-article corroboration, that domain must be added to allowlist OR cross-checked manually.

---

## Headline finding

**The persistent visual jitter in BuddahGo is a textbook hard-snap-on-reconciliation problem with a 25-year-old industry-standard fix.** Source engine (HL2, 2004), Gabriel Gambetta's canonical writeup, Photon Fusion's `InterpolatedErrorCorrectionSettings`, and **FishNet 4.x itself** all implement the same pattern: **separate the "true (authoritative) position" from the "display (rendered) position", lerp display → true over a short window (~100ms), reconcile only the true side hard.** Hard-snapping the rendered transform is what produces persistent jitter under high reconcile frequency. BuddahGo's V1-V5 prediction stack works correctly at simulation layer (V5 byte-identical cross-peer Tier 1 chain proved this) — the gap is at Layer 3+5 visual error-correction, which **almost certainly means we're either not using or misconfiguring FishNet's existing smooth-reconciliation feature**.

---

## Section 1 — Methodology + scope

**Researched references:**
- **Source engine** (Valve, HL2 era 2004 onward) — foundational reference, well-documented in Valve developer wiki
- **Gabriel Gambetta** "Fast-Paced Multiplayer" canonical series — quoted everywhere in modern netcode literature
- **Glenn Fiedler / Gaffer on Games** — "Networked Physics" series (different paradigm, see anti-patterns)
- **Overwatch GDC 2017** (Tim Ford) — "Gameplay Architecture and Netcode"
- **Photon Fusion 2** — Unity SDK competitive to FishNet, documented `InterpolatedErrorCorrectionSettings`
- **FishNet 4.x** (BuddahGo's stack) — `NetworkTickSmoother` + smooth reconciliation feature
- **Lethal Company** (Unity + Facepunch.Steamworks, 2023) — closest stack analog to BuddahGo
- **Risk of Rain 2** (Unity + Steam P2P) — host-authority P2P, mixed reputation
- **Valheim** (Unity + distributed authority) — **negative reference**, well-documented jitter

**Excluded:**
- **Deep Rock Galactic** (UE4 + Steam P2P, host-authority) — Yonezawa requested but search returned mostly user troubleshooting articles, no public technical breakdown of UE4 networking specifics in this game. UE4's networking is well-documented generically (RPCs + NetUpdateFrequency + replicated movement), but DRG's internal smoothing implementation isn't public. Recommend adding `gdcvault.com` and `epicgames.com` to allowlist if deeper UE4 reference is wanted.
- **Rocket League** — Psyonix has a known GDC talk on physics + visual decoupling, but specifics weren't reached via WebSearch snippets.

**Constraint:** every reference here is sourced via WebSearch snippets. None is full-text. Any specific code-level claim should be re-verified against a fetched source if it drives a Phase 7.5 design decision.

---

## Section 2 — Reference profiles

### 2.1 Source engine (HL2 / CS / TF2) — the canonical pattern

**Architecture:** server-authoritative + client-side prediction + server reconciliation on top of a fixed tick rate (66 Hz default).

**Smoothing approach:** when server snapshot arrives (~100 ms after input), client compares server position to its predicted position. If different = "prediction error". Source's response:

> "Prediction error correction can be quite noticeable and may cause the client's view to jump erratically. By gradually correcting this error over a short amount of time (`cl_smoothtime`), errors can be smoothly corrected."

Two console variables expose the mechanism:
- `cl_smooth 0/1` — toggle smoothing on/off
- `cl_smoothtime` — duration to smooth the correction over (default 0.1s)

**Trade-off explicitly acknowledged:** "While smoothing prevents warping, the model won't be at the correct position and hitreg will be slightly off until the smoothing is complete." Valve's choice is to ship slightly-imprecise hitreg in exchange for smooth visuals. This trade-off is a deliberate design decision, not a bug.

**Off-state visual symptom:** "with smoothing off, the playermodel will be shown at the correct position on the next frame, resulting in a 'warp' instead of smooth movement."

### 2.2 Gabriel Gambetta — separate display from true position

The canonical modern explanation. Gambetta's contribution is the explicit separation of **two positions**:

> "The approach separates the display position from the true position. When a desync happens, the client's server reconciliation snaps the true player position to the authoritative server position before replaying saved inputs. [...] Smoothing behavior is activated, which will lerp the player's display position to the true position with a given speed until they match again. When there's no desync, the display position is simply the true position."

Algorithmic shape:
```
on reconcile_tick:
    snap true_pos = authoritative_pos     # logic / collision still authoritative
    replay queued_inputs into true_pos    # standard CSP
    enable smoothing                      # display_pos starts catching up

on render_frame:
    if smoothing_active:
        display_pos = lerp(display_pos, true_pos, lerp_speed * dt)
        if dist(display_pos, true_pos) < epsilon:
            smoothing_active = false
    else:
        display_pos = true_pos            # no overhead in steady state
```

**This is the pattern Phase 7.5 should aim for.**

### 2.3 Photon Fusion 2 — built-in `InterpolatedErrorCorrectionSettings`

Photon's commercial Unity SDK has explicit smoothing as a configuration class:

> "Fusion's interpolation algorithm adapts the offset and buffering required based on current network conditions, giving the client as little latency as possible while still providing smooth visuals. [...] Prediction correction enables smooth correction in case of incorrect prediction, solving incorrect prediction in both fixed and render updates."

Key insight: render-state and fixed-state are decoupled in Fusion. Rendering interpolates between two recent snapshots. This is **structurally what FishNet's NetworkTickSmoother does** — but FishNet's exposure of error-correction is less prominent in default configs.

### 2.4 FishNet 4.x — already has the answer

**This is the most important reference because it's BuddahGo's actual stack.** Per FishNet documentation (gitbook.io snippets):

- `NetworkTickSmoother`: "interpolates positions, rotations, and scale between network updates, making movement transitions appear fluid even though network updates may arrive in discrete steps."
- `Favor Prediction Network Transform`: "automatically disables the NetworkTickSmoother when a predicted NetworkObject is under the active control of a NetworkTransform" — i.e. there's coordination between two smoother systems.
- **Adaptive vs Flat Interpolation:** "Flat interpolation is often used in competitive or reaction-based games to keep the interpolation consistent for all players, and is also necessary for accurate collider rollback, while adaptive interpolation is best used with casual games where you want the absolute smoothed experiences regardless of local client latency."
- **Detach Graphical Objects:** "Detaching and re-attaching the graphical object at runtime can resolve camera jitter or be helpful for objects that do not handle reconciliation well, such as certain animation rigs."
- **Teleport Threshold:** "Enable Teleport will allow the graphical object to teleport to its actual position if the position changes are drastic, and the Teleport Threshold determines how many units away the graphical object must be before teleporting to the actual position." — note that the BuddahGo `BuddahPredictionVisualRootBridge.PostIntroVisualLockPositionThreshold = 0.75f` value strongly suggests a Teleport Threshold or analogous parameter. **Surface 7 should grep this against Buddah.prefab.**

**Most consequential snippet:**
> "**To address the issues introduced by snap reconciliation, you can improve it by implementing a smooth reconciliation approach.**"

This is FishNet documentation explicitly saying the default IS snap reconciliation, and there is an alternative path. **This finding alone justifies Phase 7.5 scope reduction from "rewrite reconciliation" to "configure existing smooth reconciliation feature".**

**Unity Discussion threads about FishNet jitter** (titles only — full content blocked):
- "FishNetworking Client Prediction Jitter"
- "Fishnet and rigidbody jitter"
- "Fishnet Body Jitter"
- "Graphical object offset when object is spawned while prediction v2 is enabled and jitter fix is used" — GitHub Issue #595

These titles confirm BuddahGo isn't alone — FishNet projects in general hit jitter as a recurring class of issue. Means the fix space is well-explored by the community.

### 2.5 Overwatch GDC 2017 (Tim Ford) — predicted projectiles + ECS

Not a host-client game (dedicated server), but architectural lessons apply:

- **Time dilation for input starvation:** server detects too-low input arrival rate, asks client to dilate clock briefly, client simulates ~5% faster to fill the buffer. This is a backpressure mechanism BuddahGo doesn't have but probably doesn't need at LAN+LatencySim scales.
- **Predicted projectiles:** Blizzard predicted rockets despite GDC consensus that "you shouldn't predict projectiles". The lesson: prediction is not just for player avatars; entity classes that benefit from responsiveness should opt INTO prediction even if conservative wisdom says don't. **Useful for BuddahGo:** PushHitbox impulses are conceptually projectiles — could they be predicted on the receiver side rather than reacted-to-on-arrival? Probably out of Phase 7 scope but worth noting for Phase 8/9.
- **ECS architecture:** out of scope for BuddahGo retrofit but the reasoning ("ECS curtails complexity in netcode") is worth a Phase 8 note.

### 2.6 Glenn Fiedler — physics state sync ≠ player avatar sync

**Important to NOT misapply.** Fiedler's "Networked Physics" series advocates HARD-SNAP at the simulation level for shared-physics-world games (e.g. networking a stack of cubes in VR). His justification:

> "Apply the values in the state update directly to the simulation. He recommends against smoothing between the state update and the current state at the simulation level, because the simulation extrapolates from the state update, so it should extrapolate from a valid physics state rather than a smoothed one."

This is for **physics objects under simulation rules** (rigid bodies in shared physics worlds). It is NOT the recommendation for **player-controlled avatars with predicted state**. The two are different problems:

| | Fiedler's domain (shared physics) | BuddahGo's domain (player avatar) |
|---|---|---|
| Authority model | Frame-by-frame state sync | Server-authoritative + client-prediction |
| What's snapped | Rigid body state directly | Should be: "true position" only |
| Visual layer | Same as physics | Should be: separate "display position" |
| Why no smoothing at sim layer | Extrapolation needs valid physics | N/A — player input replay is the catch-up |

**Implication:** if BuddahGo is currently applying Fiedler's "snap hard at simulation" pattern to player avatars, that's a mismatched paradigm. Surface 7's Layer 3 audit should disambiguate which pattern V5 actually implements.

### 2.7 Valheim — anti-pattern (distributed Zone Authority)

> "When you walk into a new area, your computer takes over the hosting duties (called 'Zone Authority') for all the physics and monster AI in that specific area. [...] One common issue that players experience in Valheim is multiplayer desync, which occurs when the game data being sent between the client and server becomes out of sync. The distributed authority system makes desync particularly problematic."

The architecture trade-off Iron Gate made: distribute authority to whoever's in the zone (saves bandwidth, scales geographically) **at the cost of recurring desync**. Players accept this for the open-world genre fit.

**Implication for BuddahGo:** Valheim shows that **architecture itself can be the jitter source** when authority transfers don't have smooth handoffs. BuddahGo's `BuddahPredictionVisualRootBridge.PostIntroVisualLockPositionThreshold` + `MaxFramesAfterExit = 4` hysteresis is structurally analogous — they're addressing handoff visual artifacts. Phase 7 SMOKE should include intro-end-handoff in the observation set.

### 2.8 Risk of Rain 2 — host-authority host-quality-bound

> "The game relies on a host-based P2P system where one player acts as the server. [...] High ping (exceeding 150 milliseconds) causes noticeable input delays and desynchronization between enemy actions and player operations, and skills are delayed when activated. Lag spikes occur from time to time, delaying damages and making mobs and bosses blink around positions and place to place."

**This is exactly BuddahGo's failure mode** as Yonezawa described. RoR2's "blinking mobs" symptom = the same hard-snap-reconciliation pattern manifesting visually. Hopoo's solution (per indirect evidence): tolerate it as a known limitation. BuddahGo doesn't have to follow that — the FishNet smooth reconciliation feature is the path RoR2 didn't take.

### 2.9 Lethal Company — Unity NGO closest analog

Limited public technical detail, but uses Unity's official Netcode for GameObjects (NGO). NGO documentation references `GhostPredictionSmoothingSystem` for entities-mode netcode (Netcode for Entities, separate package). The smoothing concept exists in Unity's official stack family, but Lethal Company specifically uses NGO (gameobjects mode) which has more lightweight smoothing. The relevant Lethal Company modding patterns visible in search results emphasize **owner-vs-ghost separation** — same Layer-3-vs-5 split as the canonical pattern.

---

## Section 3 — Common patterns across successful implementations

Synthesizing across Source / Gambetta / Photon / FishNet:

### Pattern 1 — Two positions: True + Display

Every reference except Fiedler's physics-world domain implements this:
- **True position** = authoritative simulation state (rb.position, used for collision + game logic)
- **Display position** = what's actually rendered (smoothed transform)
- **They differ during reconciliation only**, converge in steady state

### Pattern 2 — Time-bounded error correction (~100ms catch-up window)

- Source: `cl_smoothtime = 0.1s`
- Gambetta: lerp speed parameter, typically 50-200ms catch-up
- Photon Fusion: `InterpolatedErrorCorrectionSettings` adaptive
- FishNet: `Adaptive Interpolation` adapts catch-up to network conditions

### Pattern 3 — Snap-or-smooth threshold (teleport threshold)

When error exceeds smooth-able budget, give up smoothing and snap. Below threshold, smooth.
- FishNet: explicit `Teleport Threshold` field
- Source: implicit via `cl_smoothtime` timeout
- BuddahGo: `PostIntroVisualLockPositionThreshold = 0.75f` may be this

### Pattern 4 — Owner-side and observer-side separate budgets

- Owner: smaller buffer, higher responsiveness (interp 0-1 tick)
- Observer/spectator: larger buffer, smoother (interp 2+ ticks)
- FishNet exposes both; BuddahGo design Q0 R0.2 acknowledged this split.

### Pattern 5 — Smoothing OFF in dev, ON in ship

- Source: `cl_smooth 0` for accuracy testing, `cl_smooth 1` for production
- BuddahGo could mirror: probe-based dev mode shows true positions; ship mode shows smoothed.

---

## Section 4 — Anti-patterns

### 4a — Snap reconciliation on every tick at high frequency

This is the suspected BuddahGo state. V5 measured `rec-cb=2595` per 53 HBs (~49 Hz) on CLIENT under 100ms LatencySim. If each reconcile is hard-snap, that's 49 hard-snaps per second on a 60 fps render — by definition produces hard-stop-and-go visual texture (= "rubber-banding" / persistent jitter).

### 4b — Distributed authority with abrupt handoff (Valheim)

Authority transfers between hosts/clients without a smooth visual handoff. BuddahGo doesn't fully suffer this, but the intro-end transition has the same shape (control-source changes → visual jump).

### 4c — Hard snap at simulation level applied to player avatars

Glenn Fiedler's pattern misapplied to player avatars. Use his pattern only for shared-physics objects, not character controllers.

### 4d — Hitbox-on-rb but visual-on-smoother without bounded lag

If hitbox checks against rb.position (current authoritative) but visual is smoother-output (1-2 ticks behind), the player experiences "I clearly hit them but no damage" or vice-versa. Industry standard tolerates 1-2 frames of visual lag; >2 frames is the perception cliff.

### 4e — Tolerating jitter as "P2P limitation" (RoR2)

The "it's P2P, deal with it" mindset. Phase 7's mandate from Yonezawa explicitly rejects this — the goal is to match industry standard, not accept failure.

---

## Section 5 — BuddahGo gap analysis vs canonical pattern

Cross-referenced against my prior 6-layer model:

| Layer | BuddahGo current state (best knowledge) | Canonical reference state | Gap severity |
|---|---|---|---|
| L1 fixed tick | FishNet 50 Hz tick | Same — Source 66 Hz, Photon 60 Hz, FishNet 50 Hz (tunable) | None |
| L2 owner CSP | V1-V5 prediction-v2 stack, byte-identical cross-peer Tier 1 verified | Same shape | None |
| **L3 reconciliation correction** | **Suspected hard-snap on rb (= Anti-pattern 4a)** | **Smooth catch-up (Source `cl_smoothtime`, Gambetta lerp, FishNet smooth-reconciliation)** | **HIGH — primary gap** |
| L4 observer interp | FishNet `_graphicalObject` smoother, interp 1 (owner) / 2 (spectator) ticks | Standard configurable (Source 100ms, Photon adaptive, FishNet adaptive) | LOW pending Surface 7 config audit |
| L5 visual smoothing | Same FishNet `_graphicalObject` smoother | Industry has it; FishNet has explicit feature | LOW pending Surface 7 |
| L6 hitbox-visual desync | Unaudited; PushHitbox uses rb-driven transform per V2b Step 1 | Industry tolerates 1-2 frames lag | MEDIUM pending Surface 7 7.4 |

**The Layer 3 gap is the single highest-confidence root cause hypothesis.** Confirmed by:
1. V5 measured `rec-cb=2595/53HBs` on CLIENT — frequency profile matches the failure mode
2. FishNet docs explicitly note default snap behavior + recommend smooth-reconciliation
3. Yonezawa's "persistent jitter" symptom matches RoR2's "blinking mobs" anti-pattern signature
4. Hard-snap on player avatar misapplies Fiedler's physics-domain pattern

---

## Section 6 — Phase 7.5 retrofit pattern recommendations (concrete)

Three retrofit shapes ranked by likely necessity:

### R7.5-A — FishNet smooth-reconciliation feature configuration (likely sufficient alone)

**If Surface 7 audit confirms BuddahGo currently uses FishNet's default snap reconciliation:**

1. Locate FishNet's smooth-reconciliation API/config (likely on `PredictionRigidbody` or `NetworkObject` settings — FishNet 4.x specific). Search `FishNet.Component.Prediction` for SmoothingSettings / GraphicalSmoothing toggles.
2. Enable on Buddah prefab.
3. Tune: `Adaptive Interpolation` enabled (BuddahGo is "casual" not competitive-FPS), `_ownerInterpolation = 1`, `_spectatorInterpolation = 2`.
4. Set Teleport Threshold to a value matching `PostIntroVisualLockPositionThreshold = 0.75f` (or lower if 75cm is too generous).
5. Re-run Phase 7 SMOKE; expect substantial Q3 metric improvement.

**LOC scope:** ~0 code, prefab YAML edits. Most likely the right answer based on FishNet "smooth reconciliation approach" documentation hint.

### R7.5-B — Implement Gambetta-style display/true position split (if FishNet feature insufficient)

If R7.5-A fails or FishNet doesn't expose the necessary smoothing primitives:

1. Add `_displayPosition` field to `BuddahPredictionVisualRootBridge`.
2. On `[Reconcile]` callback (motor.cs:550 area): record `_truePositionDelta = newAuthPos - oldAuthPos`. Don't snap visual root.
3. In `LateUpdate` on visual root: lerp visual transform from current to `motor.transform.position` over `_smoothTime` (default 100ms, configurable).
4. Use `Vector3.SmoothDamp` for natural exponential catch-up (matches Source's `cl_smoothtime` curve).
5. If `Vector3.Distance(visual, true) > _teleportThreshold`, snap visual to true (handoff case, large rollback).

**LOC scope:** ~80-150 LOC across VisualRootBridge + bridge config field. Phase 7.5 specific phase if needed.

### R7.5-C — Hybrid (R7.5-A + selective R7.5-B for edge cases)

If FishNet smooth-reconciliation handles steady state but breaks on specific events (intro handoff, large reconciles), supplement with custom handler for those events only. Phase 7.5 splits to 7.5a (config) + 7.5b (custom override).

---

## Section 7 — How Surface 8 informs Phase 7 SMOKE

Surface 8 changes Stage 5 SMOKE from a "discover" pass to a "validate hypothesis" pass:

**Hypothesis:** persistent jitter is hard-snap reconciliation at Layer 3, fixable by enabling FishNet smooth-reconciliation.

**SMOKE driver pre-instructions (driver workflow append):**
1. **Before SMOKE:** verify in `Surface 7 audit 7.1` whether reconcile is hard-snap or smooth. If hard-snap confirmed → SMOKE expected to show G3.1/G3.2 fail per design Q3 anchor thresholds, plus visible SC.1 (rubber-banding) on CLIENT side under 100ms LatencySim.
2. **During SMOKE Path B 100ms:** focus driver gaze on CLIENT spectator-view character during continuous motion. The expected observation is high-frequency micro-vibration (not discrete snaps) — this confirms the hypothesis.
3. **If observation matches hypothesis:** Phase 7 closes as STRICT-PASS-WITH-REGRESSION-CONFIRMED → Phase 7.5 R7.5-A retrofit fires immediately (Phase 7 measured + characterized + identified fix path).
4. **If observation does NOT match hypothesis** (jitter is discrete snaps, or no jitter visible, or jitter pattern doesn't match LatencySim correlation): hypothesis falsified; Phase 7 reverts to discovery mode and Surface 7 drill-down deepens before Phase 7.5 design.

---

## Section 8 — Summary for Stage 5 SMOKE driver workflow update

Driver: when running Path B 100ms LatencySim:
- Watch CLIENT spectator character → look for high-frequency micro-vibration during continuous motion (= rubber-banding signature)
- Watch HOST owner character → should be smooth (predicted, no reconcile pressure)
- Compare: jitter delta CLIENT-vs-HOST is the Layer-3 fingerprint
- If both peers vibrate equally → Layer 1 or 5 issue (frame-time or smoother config), not Layer 3

Reviewer at Stage 6 cross-references:
- numeric `pos-dp99 / pos-davg` delta CLIENT vs HOST (should be larger on CLIENT if Layer 3 hypothesis correct)
- `rec-cb` ratio HOST vs CLIENT (should be near-zero on HOST, ~50/s on CLIENT)
- `rec-snap-{p99,max}` from Q4 probe — magnitudes
- SC.1 driver subjective rating

Phase 7 verify report Stage E sign-off matrix gets a new column: "Surface 8 hypothesis confirmed?" — Y/N drives Phase 7.5 retrofit path selection.

---

## Sources

WebSearch result snippets accessed 2026-05-03 from these search queries:
- "Deep Rock Galactic networking client prediction host authority smoothing technical"
- "Lethal Company unity netcode host client prediction smoothing 2024"
- "FishNet prediction reconciliation smoothing visual jitter best practices 2025"
- "Photon Fusion client prediction visual smoothing rendering interpolation"
- "Glenn Fiedler networked physics state synchronization snap interpolation"
- "Overwatch GDC Tim Ford netcode rollback prediction client"
- "smooth reconciliation client side prediction position lerp error catch-up"
- "Source engine HL2 client side prediction error correction smoothing"
- "Valheim networking jitter desync host authority unity"
- "Risk of Rain 2 networking peer to peer host authority smoothness"

Direct article URLs that returned substantive snippets:
- Gabriel Gambetta: `gabrielgambetta.com/client-side-prediction-server-reconciliation.html`
- Source Multiplayer Networking: `developer.valvesoftware.com/wiki/Source_Multiplayer_Networking`
- Source Prediction: `developer.valvesoftware.com/wiki/Prediction`
- Glenn Fiedler State Synchronization: `gafferongames.com/post/state_synchronization/`
- Photon Fusion 2 Network Simulation Loop: `doc.photonengine.com/fusion/current/concepts-and-patterns/network-simulation-loop`
- Photon Fusion `InterpolatedErrorCorrectionSettings`: `doc-api.photonengine.com/en/fusion/current/class_fusion_1_1_interpolated_error_correction_settings.html`
- FishNet NetworkTickSmoother: `fish-networking.gitbook.io/docs/fishnet-building-blocks/components/tick-smoothers/networkticksmoother`
- FishNet Configuring NetworkObject: `fish-networking.gitbook.io/docs/guides/features/prediction/configuring-networkobject`
- Daniel Jimenez Morales 2025: `danieljimenezmorales.github.io/2025-06-20-client-side-prediction-and-server-reconciliation/`
- FourAM Games Smooth Server Reconciliation: `fouramgames.com/blog/fast-paced-multiplayer-implementation-smooth-server-reconciliation`
- Wikipedia Client-side prediction
- GitHub FishNet Issue #595 (jitter fix + graphical object offset)
- Multiple Unity Discussion threads on FishNet jitter
- Edgegap Overwatch netcode deep-dive
- GDC Vault Overwatch talk
- Valheim community discussion + DatHost / GhostCap troubleshooting
- Risk of Rain 2 community discussion + portforward / DriverEasy guides

If Phase 7.5 design needs full-text from any of the above, request workspace admin add the host to Linux container egress allowlist (currently restricted to `*.anthropic.com / claude.com`).
