@page plan_shot_solver_accuracy Shot Solver Accuracy

# Shot Solver Accuracy

Close the fidelity gaps between the shot solver (`Assets/Source/Solver/`, rule-mirroring doc in
`Assets/Source/Editor/ShotSolver/README.md`) and the live game — interactive statics, the balance
weight system, item carriers, rainbow/wildcard rules, and flight residuals — then open the sim to
headless level diagnostics. Produced 2026-07-25 from a four-way pass: code diagnostic → architect
refinement → reviewer feasibility (with code diagnostics) → test plan. Assumes the uncommitted
speed-based-balance-duration WIP lands first.

**Governing principle (non-negotiable, inherited from the dynamic-board work):** every new sim
behavior either reuses a live pure core, or is a documented mirror with a fidelity test — never a
third, independently re-implemented copy.

---

## 1. Diagnostic

**Already faithful:** event-to-event billiard (exact wall mirror; analytic deflect normals via the
real `ProjectileMotionResolver.TryComputeContactNormal`), deflect/pop durability, streak scoring +
color adoption + shield refunds, cruise ramp/lookahead/tap-lag/piercing arm, dynamic board running
the REAL `BalancePlanner` over a headless `SlotGrid` with flight-rebalance pulses, per-type +
spawn-rolled move speeds, full nudge-impulse mirroring, pops opening grid gaps.

**Gap list (impact-ordered):**

| # | Gap | Live behavior | Sim behavior today |
|---|-----|---------------|--------------------|
| G1 | Interactive statics invisible | `AbsorberActorModel` ends the turn; `DeflectorActorModel` deflects forever; `GatekeeperActorModel` deflects until `HitsRemaining==0` then pops (scoreless, streak-neutral — see Phase A) | No collision geometry — the sim flies through; Absorber is worst (phantom pops after the turn already ended) |
| G2 | Weight system stubbed | Color-diagonal / line-forming / clumping `WeightBias` (config: bias 2–3 on every type) + `OmnidirectionalBalance` feed `MoveWeightEvaluator` | Stub returns 0 / false → balance moves can diverge from the live planner, compounding per pulse |
| G3 | Item carriers unmodeled | Items are assigned at spawn (`ItemAssigner`) — knowable at gather. On host pop: Shield +1 projectile shield; Bomb blast (hex neighbours guaranteed-kill within overlap); Laser 4 casts at captured spin; Lightning same-color chain; Paint triangle recolor; Snipe arms pierce+speed on ANY pop, no DirectHit gate (José's ruling, 2026-07-25 — corrected from this row's earlier claim); rainbow-host variants convert to rainbow | Nothing (diagnostic predates Phase C — see §4 Phase C, now shipped) |
| G4 | Rainbow/wildcard out of scope | Wildcard pop keeps projectile color; colorless-projectile rainbow pop defers streak; rainbow-buffed shot streaks color-agnostically + converts pop neighbours; rainbow attribution pays every allowed color; soap washes projectile colorless | Rainbow = ordinary color string; soap wash unmodeled |
| G5 | Pierce discharge timing | Plowed toughs stay ON the grid until wall-bounce discharge — and a death wall never discharges them (death check precedes the discharge branch, `ProjectileMotionResolver.Step`) | Sim pops+removes at contact → balance pulses see a different grid |
| G6 | Sweep taps missing | A straight segment whose corridor holds only 1-HP pops awards a cruise tap (same speed/pierce counter as wall taps) | Only wall-bounce taps |
| G7 | Last-shield approach | Rebalance frozen while the doomed shot glides on a fixed wall-clock ease | Sim keeps pulsing at flight speed |
| G8 | Initial buff state | *(Dissolved by review.)* A freshly loaded shot is a `new ProjectileModel` at config defaults — no buffs, null color. Gather has no live-projectile handle and needs none | Config defaults ARE correct; buff fields become in-sim state driven by item activations (Phase C/D-core) |
| G9 | Mid-flight RNG | Direct pops roll pop-spawn (adds a balloon mid-flight); spawned balloons may later receive items | Unmodelable — policy, not simulation |

**Nondeterminism boundary (declared, never simulated exactly):** pop-spawn RNG; items arriving
with mid-flight loose spawns (policy decision 2026-07-25: ignored — items already on the board at
solve time are deterministic facts and are modeled fully); time-staged item application (bomb
rainbow ring at effectDuration×0.5, paint blob flight, lightning per-jump); laser idle-spin
capture (extrapolated estimate); tough/cluster score color-attribution RNG (display-only);
pop-pressure shoves (`ShoveVector` — never exercised by flight pulses, unsimulated; README note).

## 2. Goals & non-goals

**Goal:** predicted (score, pops, death/absorb, timeline) matches live outcomes on boards with
statics, item carriers, and rainbows. Acceptance: 20 mixed-board Fire Best shots, ≤1 shot with an
*unexplained* divergence (not attributable to a declared RNG boundary).

**Follow-up goal (Phase G):** the same sim, run outside play mode, brute-tests level configs for
pacing metrics (max/avg color streak, shots-to-clear distribution, score-per-shot, absorb/death
rates).

**Non-goals:** predicting pop-spawn RNG; exact per-blob paint timing; visual-only motion (idle
sway, Catmull-Rom corner rounding); v2 exact tangency enumeration; a general buff framework (the
two concrete end conditions only).

## 3. Architecture decisions (settled)

- **Snapshot growth by composition:** `ShotBalloonSnapshot` = geometry/scoring core +
  `ColorProfile { ColorId, IsRainbow, AllowedColors }` + optional `BalanceProfile { SlotIndex,
  BalancePriority, MaxBalanceSteps, MoveSpeed, DirectBalanceMotion, Omnidirectional, BiasKind,
  BiasValue, TypeTag, NudgeOverrides }` + scalars `ShotContactKind ContactKind`, `ItemType Item`.
  Named factories (`ForColorTarget`, `ForToughTarget`, `ForStaticContact`) replace telescoping
  ctors. Rejected: parallel arrays (index-fragile), tagged union (god-struct/boxing).
- **Statics without stubs need explicit null-gating (reviewer-critical):** every centre/dynamics
  path currently branches on `dynamics != null`, NOT `Actor != null` — a stub-less static target
  NPEs at `CurrentBalloonCenter`, `OnBalloonHit`, `OnBalloonDeflected`, `RemoveFromGrid`, and
  `TryFindNearestBalloonEntryDynamic`. Required: (a) `Actor == null` ⇒ fixed-position centre and
  static entry solve; (b) slot-based variants of the dynamics entry points (nudge neighbours of /
  remove by `SlotIndex`); (c) `TargetActors` extended 1:1 with the full collision board with null
  slots for statics; (d) `ShotBalloonState` gains `Vector2Int SlotIndex` (also required by the
  item layer — swap-remove scrambles index identity and statics have no `Actor` backref).
- **Item layer is opt-in, a peer of dynamics:** nullable `ShotItemLayer` param on `Simulate` +
  field on `ShotSolveContext`. `items: null` ⇒ core loop byte-for-byte unchanged (same pattern as
  `dynamics: null`). `dynamics × items` = four independently valid combinations. The layer
  consumes only snapshot/working-set data — never live scene state (keeps Phase G possible).
- **`ShotSimEffectBoard` runs on the working set + `HexCoordinates` only** — NOT the dynamics
  grid (architect addendum superseding the earlier bridge note). The layer returns `EffectHit`s;
  the LOOP applies them (including `dynamics?.RemoveFromGrid` / per-pop nudges), so gaps still
  open when dynamics is present.
- **Item-effect reuse seam:** port `IEffectBoard { Occupants; TryHexNeighbors }` over
  `EffectOccupant { Handle, Slot, Position, Radius, ColorId, TypeTag, HitsRemaining, Caps }` and
  `EffectHit { Handle, EffectKind (Pop|Damage|ConvertRainbow), damage }`. Pure static cores
  beside each item (`Item/Bomb/BombBlast.cs`, `Item/Laser/LaserCross.cs`,
  `Item/Lightning/LightningChain.cs`, `Item/Paint/PaintSpread.cs`; `PaintTriangle` is the
  precedent; Lightning's selection is inline in `CollectSortedTargets` and must move). Cores are
  config-free, Physics2D-free; scalars via an immutable `ItemEffectParams` snapshot from
  `IItemConfiguration`. Adapters: `GridEffectBoard` (live, `Item/`) — positions from
  `grid.IndexToWorldPosition`, NOT view transforms, so it stays EditMode-testable — and
  `ShotSimEffectBoard` (`Solver/`). Rejected: sim re-implementing selection (drift ×6).
- **Weight-bias sharing:** `IBalanceBiasSource` (in `Slots/Actor/`, beside `IBalanceInfluence`)
  exposing the read-set the count helpers need — color id + a type tag (Slots-owned identifier,
  not `Balloon.Type`, to avoid the namespace-direction wrinkle). Implemented by
  `BalloonModelBase` and `ShotSimDynamicActor`. The three count helpers retarget from
  `is IBalloonModel` to `is IBalanceBiasSource` and remain in `Shared/Extensions` as
  `IBalanceBiasSource` extensions (repo rule: helpers belong in Shared/Extensions; architect's
  `Slots/Grid/BalanceBiasMath` co-location noted as the alternative), plus formula entry points
  (`ColorDiagonal`/`Line`/`Clump`) and an `Evaluate(BalanceBiasKind, …)` dispatch the stub calls.
  Rejected: strategy objects (YAGNI).
- **`ShotFlightState`:** collapse `Simulate`'s 15 params / ~18 threaded ref locals into one
  mutable struct, passed by `ref` (by-value would silently drop mutations). Inputs/outputs
  (board, workingSet, walls, dynamics, cruiseConfig, pathOut, timestampsOut) stay OUT of it.
- **Shared radius helper:** extract `ContactRadius.FromCollider(Collider2D, lossyScaleX)` from
  `ResolveProjectileContactRadius` — used by live gather, Phase A statics, and Phase G's
  synthetic gather.

## 4. Phases

### Phase 0 — Prerequisite refactors (no behavior change; the existing 22-test suite stays green)
- **0a `ShotFlightState`** (see §3). The cruise/pierce multi-event tests are the tripwire for a
  dropped-`ref` bug.
- **0b Snapshot composition** (see §3) + `ShotBalloonState.SlotIndex` + the null-`Actor` gating
  and slot-based dynamics variants + `TargetActors` 1:1-with-nulls extension. Add the
  `ShotBoardBuilder` test helper FIRST (forwards to the production factories — never
  re-implements construction) and migrate the existing board literals onto it; the suite staying
  green is the field-mapping test.

### Phase A — Interactive static geometry (G1) — depends on 0b

**Reachability note (architect design pass, 2026-07-25):** the three archetypes are code-complete
but NOT live-reachable today — `StaticActorSpawner` registers only Puff/Bush, no
Deflector/Absorber/Gatekeeper prefabs exist, `GridActorView` has no collider, and
`ProjectileView.TryGetHitBalloon` only recognizes `BalloonView` on the Balloons layer. Phase A is
therefore EditMode-verifiable only; its Fire Best acceptance batch is N/A until the live wiring
(PLAN-GridActorExpansion §8.3-ish) ships, and G1's practical impact is deferred until then.
Also fix in Phase A: the 0b scaffolding calls `OnBalloonHitAt` on every static contact, but live
statics never nudge neighbours (`NudgeService` requires `IHasNudge`) — drop the call and pin it
with a test.
- Gather: static archetypes implementing `IHitable`/`IHasDurability` become collision targets
  while still occupying their slot for the planner (`CollectBoard` currently `continue`s on
  `Kind == Static` before `TryBuildTargetSnapshot`; routing changes). New static snapshot factory
  WITHOUT the `IHasScore` requirement (Gatekeeper isn't `IHasScore`).
- **Radius:** `GridActorView` has NO `ContactRadius` today — add one (serialized `Collider2D` +
  the shared `ContactRadius.FromCollider` helper) or derive at gather from the prefab collider.
- Mapping: **Deflector** = existing `int.MaxValue`-durability colorless deflector.
  **Gatekeeper** = a THIRD pop path — verified: it lacks `IHasScoreColor`, so `ScoreController`
  ignores its pops entirely — scores nothing AND leaves the streak untouched (the tough path's
  streak reset would be wrong). Final hit pops it; removal by slot (mirrors
  `GridActorHitController`). **Absorber** = `ShotContactKind.Absorb`: flight terminates; new
  `bool Absorbed` on `ShotSimulationResult` (distinct from `Died`; surface it in the window UI —
  the Fire Best cheat reads `RawScore` only and needs nothing).
- Extract `ClassifyContactKind(actor)` as a pure function so archetype→kind is EditMode-testable
  without grid/views.
- **Landmine to close (from the 0b review):** `ForStaticContact` currently carries no slot, so a
  Balance-less `ShotBalloonState.SlotIndex` defaults to (0,0) and the snapshot is NOT placed on
  the dynamics grid. Phase A must add a real `SlotIndex` param to `ForStaticContact`, place the
  static occupant on the dynamics grid at it, and only then may static contacts share a board
  with real movers — otherwise `RemoveFromGridAt(default)`/`OnBalloonHitAt(default)` would
  corrupt whatever legitimately occupies slot (0,0).

### Phase B — Weight-system fidelity (G2) — depends on 0b; parallel to A
- The confirmed silent-drift trap: the three count helpers test `grid.At(pos) is IBalloonModel`
  — sim stubs aren't, so "shared" code would return 0/`MaxValue` while looking shared. Fix via
  `IBalanceBiasSource` (§3). Only three callers exist (the three `WeightBias` overrides).
- Stub honor-list is exactly `WeightBias` + `OmnidirectionalBalance`: the support-cone weight is
  pure grid occupancy (free); `ShoveVector` is never exercised by flight pulses (README note:
  pop-pressure shoves unsimulated).
- Mirror-fidelity tests: live model vs stub bit-identical per formula on a shared headless
  `SlotGrid`; plus a `MoveWeightEvaluator` tie-break test (bias flips which equal-support
  neighbour wins) and a `ShotBoardDynamics`-level test (bias changes which balloon enters the
  shot's path).
- Pre-phase lock: pin today's non-`IBalloonModel`-neighbour = 0 behavior as a failing-then-
  inverted test (bug protocol).

### Phase D-core — Rainbow scoring + in-sim buff state (G4-scoring, G8) — depends on 0a
- `ShotFlightState` gains pierce/speed-buff/RainbowShield fields, initialized from config
  defaults (gather does NOT read the live projectile — impossible and unnecessary; reviewer).
- Scoring mirrors (fidelity-tested against `ProjectileHitResolver` + `ColorStreakTracker`):
  wildcard pop keeps projectile color; colorless-projectile rainbow pop defers streak
  (`RecordDeferred`, folds in at the next real-color pop); rainbow-buffed = color-agnostic streak
  (`RecordWildcard`) + pop-neighbor conversion; rainbow attribution pays ScoreValue × every
  allowed color (Target Colour filter counts rainbows for any target color); soap
  (`IWashesProjectileColor`) washes projectile color on contact without popping.
- Buff end conditions, two concrete cases only: RainbowShield ends on a wall bounce that costs a
  shield; Snipe-granted variants end on pierce end.
- Rainbow scoring is CORE (always on — a rainbow balloon on the board scores correctly with
  `items: null`); effect-driven conversions and buff GRANTS live in the item layer.

### ✅ Phase C — Item carriers (G3) — depends on B + D-core

**Status: Complete** (C0–C6, `235fc04a..289d90d3`).

- Gather reads `IHasItemSlot.Item` per balloon → snapshot `ItemType`; `ItemEffectParams`
  snapshotted once from `IItemConfiguration` (+ `IGamePalette` for rainbow ids) — both already
  registered in `GameLifetimeScope`.
- Effect seam per §3. The loop applies `EffectHit`s: remove the popped HOST from the working set
  BEFORE running its effect core (live: one-frame `ItemActivator` delay + explicit host
  exclusion); item pops accrue streak SEQUENTIALLY per pop through the existing
  green/tough pop paths **with the shield-refund branch suppressed** (verified: refunds live
  only in `ProjectileHitResolver.ResolveContactPop`, which item handlers never reach); item pops
  DO nudge neighbours (`NudgeService` fires for any `IHasNudge` hit) → call the slot-based
  nudge per popped actor.
- **Bomb selection is two rules, not one** (verified): normal blast = collider overlap
  (centre ≤ radius + occupant.Radius); rainbow path = bare centre-distance (≤ radius kill,
  ≤ radius+range convert); guaranteed-kill = hex-neighbour AND within overlap. `EffectOccupant`
  carries `Radius`. Bomb also publishes a board-wide Shockwave nudge — **decision needed:** add
  a shockwave impulse pass to `ShotBoardDynamics` or document as accepted divergence (default:
  document first, measure in Phase F, model only if it shows up).
- **Laser:** live `CircleCast` ≡ the sim's `SegmentHitsAnyBalloon` with combinedRadius =
  balloon.Radius + castRadius (verified equivalent; include the overlapping-at-origin case).
  Capture rotation + spin rate at gather; extrapolate to predicted hit time (declared estimate).
- **Repointing the LIVE handlers onto the analytic cores is a live behavior change** — gated by
  PlayMode equivalence tests (extend `BombActivationPlayModeTests`; add
  `LaserActivationPlayModeTests`) + an in-editor playtest; `dotnet build` cannot catch it. If
  equivalence fails on unsettled boards, live keeps Physics2D and only the sim uses the core,
  with the delta documented.
- Sub-order: **C1 Shield** (host pop → shields++; rainbow host grants RainbowShield via D-core)
  → **C2 Bomb** → **C3 Laser** → **C4 Lightning** (distance-ordered same-color chain; rainbow
  host converts the group — colorless-projectile fallback mirrors `FindNearestColorId`)
  → **C5 Paint** (reuse `PaintTriangle.Build`/`PackBlobs` + shared `PaintSpread` bucketing;
  recolor the working set instantly and BEFORE the next contact resolves; `IResistsPaint`
  respected; rainbow holder paints rainbow) → **C6 Snipe** (host arms pierce + a non-stacking
  speed buff on ANY pop — no DirectHit gate, matching live; a pop taken while ALREADY piercing
  banks the whole grant for the discharge instead of applying it, and an armed shot's cruise ramp
  freezes at the arming tap — both mirrored 2026-07-27; folds into E2).

### Phase E — Flight residuals (G5, G6, G7 + E4) — depends on 0a; E2 folds C6
- **E4 (found during the D-core review, pre-existing gap):** the sim's non-piercing
  `HitsRemaining > 1` branch always DEFLECTS, but live only `ToughBalloonModel`/
  `UnbreakableBalloonModel` return `HitOutcome.Deflect` — a surviving multi-HP
  `BubbleClusterModel` returns `PassThrough` (no physical redirect; `BalloonController` only
  redirects on Deflect). The snapshot needs a survive-outcome discriminator (Deflect vs
  PassThrough) so soap contacts fly straight through, as live.
- **E1 Sweep taps:** mirror `SegmentSweepValid` (a single tough/deflect anywhere invalidates the
  whole segment). Requires unifying wall taps + sweep taps into one `TotalCruiseTaps`-style
  counter feeding speed — the sim currently derives speed from the bounce count alone.
- **E2 Pierce discharge:** plowed toughs stay in the grid marked `PendingPierce` until the next
  wall bounce; discharge pops all pending at once; pierce + buffs end there. **Faithful quirk
  (verified live, keep it):** shields-- and the death check run BEFORE the discharge branch — a
  death wall never discharges; pending toughs stay unpopped. Cite
  `ProjectileMotionResolver.cs` in the test so nobody "fixes" it.
- **E3 Last-shield glide:** shields==0 + clear lookahead ⇒ suppress `TryRunPulseIfDue` and run
  the final segment on the fixed-duration eased timeline (same technique as `TapLagSeconds`).

### Phase F — Nondeterminism policy + instrumentation (G9) — last
- README: accepted-divergence list gains pop-spawn, loose-spawn items, staged item timing, laser
  spin estimate, shockwave (if undecided), pop-pressure shoves.
- Fire Best divergence readout: per-event breakdown (first divergent event kind + time) —
  implemented as a pure diff-two-timelines function (unit-testable) surfaced by the editor
  window (the cheat lacks path/timestamp capture; instrumentation is the window's job).
- Acceptance run per §2 and the manual protocol in §5.

### Phase G — Headless level diagnostics (follow-up tier; scoped 2026-07-25)
Purpose: brute-test level configurations outside play mode → pacing metrics (max/avg streak,
shots-to-clear avg/distribution, score-per-shot, absorb/death rates) for level-pacing tuning.
- Placement: `Solver/Headless/` (runtime asmdef — runs under `-executeMethod` batch mode / CI;
  editor GUI is only an entry point in `Editor/ShotSolver/LevelDiagnosticsWindow.cs`). Types:
  `SyntheticBoardGather`, `ShotTurnRunner`, `PacingMetrics`.
- **G1 Synthetic gather:** build `ShotSolveContext` from configs alone — board layout from a
  level/scenario definition, lattice positions via `SlotGrid.IndexToWorldPosition`, contact radii
  via the shared `ContactRadius.FromCollider` over `BalloonPrefabEntry` prefabs, walls/thrower
  from config, move speeds rolled with a SEEDED RNG. (The sim core already runs headless — the
  EditMode suite proves it; only the live gather is play-mode-bound.)
- **G2 Turn-loop runner:** aim policy (best-window / centre-of-widest / random baseline) →
  flight → apply consequences → seeded next-turn spawns + item assignment → repeat to
  clear/loss/cap; Monte Carlo over seeds.
- **G3 Metrics + reporting:** per-level aggregates; editor window + batch entry point.
- G-local prerequisites (NOT 0–F work — nothing in 0–F invokes spawn/assign): extract
  `ItemAssignmentPlanner` and `SpawnPlanner` (pure decision logic out of the MessagePipe-coupled
  `ItemAssigner`/`BalloonSpawner`, injectable `IRandomSource`), and route `RollMoveSpeed`
  through the injectable seed.
- **Biggest G risk — turn SEQUENCING drift, not per-shot fidelity:** a hardcoded
  "flight→apply→spawn→assign" loop silently diverges from the live MessagePipe choreography
  (item cadence turns, level-up fills + streak resets, doomed-shot gating), corrupting exactly
  the metrics G produces. Mitigation: extract/pin the turn-sequence contract and assert the
  runner against a play-mode-captured turn trace before trusting any aggregate.
- Phases 0–F keep this door open via the layering constraint; the only early door-openers are
  `ShotBalloonState.SlotIndex` (0b) and `ContactRadius.FromCollider` (A) — both already required.

## 5. Test plan (per test-everything; full detail in the review transcript)

- **Helper first:** `ShotBoardBuilder` in `Assets/Tests/EditMode/ShotSolver/` forwarding to the
  production factories; migrate existing literals in 0b (suite-green = the mapping test).
- **New test files per surface:** `ShotStaticContactTests` (A), bias tests beside the balance
  tests (B), `ShotBuffScoringTests` (D-core), `ShotItemEffectTests` (C, split per item if it
  grows), `ShotFlightResidualTests` (E).
- **Headline cases:** absorber ends flight (`Absorbed`, not `Died`) + no phantom pop behind it +
  mid-cruise absorb; gatekeeper final-hit pop scores 0 with streak UNTOUCHED (+ a live-side
  `ScoreControllerTests` pin of that contract); bias tie-break flips a balance move (live +
  dynamics-level); wildcard/deferred/soap/rainbow-attribution/buff-end cases; shield granted
  then spent on the same flight; bomb radius boundary + rainbow ring converts-not-kills; paint
  recolors the balloon the shot is flying toward (stale-snapshot lock); item pop uses direct-hit
  streak rules but NO shield refund; second Snipe pickup doesn't stack; sweep-tap awarded /
  invalidated-by-tough; discharge at surviving wall vs death wall (never discharges — faithful);
  last-shield freeze + its blocked-lookahead negative.
- **Mirror-fidelity:** bias formulas live-vs-stub bit-identical; each effect core through
  `GridEffectBoard` vs `ShotSimEffectBoard` → identical `EffectHit` sets (EditMode-feasible
  because `GridEffectBoard` reads `IndexToWorldPosition`, not views).
- **PlayMode gates:** Bomb/Laser analytic-vs-Physics2D equivalence on a settled board (extend
  `BombActivationPlayModeTests`, add `LaserActivationPlayModeTests`) — the go/no-go for
  repointing live handlers.
- **Rubric exclusions:** no tests for inert profile fields before Phase B reads them, the
  deflector path (identical to the existing max-durability pattern), the buff abstraction
  (two concrete cases only), or RNG-boundary outcomes.
- **Manual protocol (acceptance):** 20-shot Fire Best run over a gap-tagged board matrix (each
  shot tagged with which gaps it exercises); per-shot log of predicted vs live timeline, first
  divergence (kind + Δt), divergence class (declared-RNG vs unexplained), outcome match.
  Pass: ≤1 unexplained divergence; any unexplained divergence → failing EditMode repro first,
  then fix. Per-phase: a 3–5-shot targeted batch as the early-warning checkpoint.

## 6. Verification workflow

Per phase: `dotnet build BalloonParty.Runtime.csproj` (+ Tests.EditMode) → EditMode suite →
`python3 Tools/style_audit.py` on touched files (field order on the new structs!) → targeted
Fire Best batch in-editor → scribe updates the ShotSolver README (accurate today; stale from
Phase A onward — the rule-mirroring + accepted-approximations sections gain entries per phase,
including E2's death-wall quirk verbatim).

## 7. Open decisions

1. Bomb shockwave nudge: document-first (default) vs model an impulse pass — decide on Phase F
   divergence data.
2. Live Bomb/Laser repoint onto analytic cores: gated by the PlayMode equivalence tests; fall
   back to sim-only cores + documented delta if unsettled-board behavior differs.
3. ~~Phase B helper placement~~ RESOLVED: `Shared/Extensions/BalanceBiasExtensions.cs` (repo rule
   won; shipped in fdc74b8b).

## 8. Remaining work — detailed status (2026-07-26)

Shipped: Phases 0/A/B/D-core ✅, Phase C ✅ (C0–C6, 235fc04a..289d90d3), plus two live fixes the
work surfaced — the graze-deflect teleport (5d401097: capsule-nose mismatch sent ~47% of deflects
down a never-re-anchoring fallback diverging up to 21.8° from the exact billiard; the sim was
immune) and the laser `_damageFlags: -1` config error (9d99272a → Piercing only). The trio
cadence (architect memo → implement → test audit + review → commit) applies to everything below.

### E — flight residuals (next; ONE architect memo for all four, they interact)
- **E1 sweep taps:** mirror `TryAwardSweepTap`/`SegmentSweepValid`; REQUIRES unifying wall +
  sweep taps into one `TotalCruiseTaps` counter (the sim derives speed from bounce count today);
  item pops must not count toward sweep validity.
- **E2 pierce discharge:** pending plowed toughs stay on the grid until the surviving wall, then
  discharge together (strike order; `+WildcardStreak` if the pierce was rainbow — mirror
  `PierceWasRainbow`); balance pulses must see pending toughs; KEEP the death-wall-never-
  discharges quirk (test cites `ProjectileMotionResolver.Step`); C6's pierce-end flag clears
  move/extend here; retires the snipe-pierce-never-ends approximation; verify the doomed-shot
  pending-flush (`DestroyProjectile`) interaction with E3; good moment to split ShotSimulator's
  over-complexity methods (advisory WARNs).
  - **Inherited by the 2026-07-27 pierce-banking rule:** a Snipe taken mid-pierce banks a charge
    that activates at the discharge (`BankedPierceCharges`/`BankedRainbowPierceCharges`, mirrored
    in `ShotFlightState`). The sim spends a charge in `HandleWallBounce`'s cruise-ending branch —
    its ONLY pierce end — so a charge banked on a *Snipe lance* (piercing without cruising) never
    activates in prediction, and a second charge never activates at all (the re-armed lance isn't
    cruising either). E2 is what makes both reachable; until then the divergence is one-sided
    (the sim under-predicts a banked lance, never over-predicts it).
- **E3 last-shield glide:** `IsLastShieldApproach` mirror — suppress `TryRunPulseIfDue` + switch
  the final segment to the fixed eased timeline (gather Duration/Curve; TapLagSeconds technique);
  negative case: blocked lookahead keeps pulsing.
- **E4 soap pass-through:** survive-outcome discriminator on the snapshot (surviving multi-HP
  cluster = PassThrough, unbent; tough/unbreakable = Deflect); wash still applies; mirror-test
  against live `SurviveOutcome` values.

### F — instrumentation + acceptance (after E)
Pure diff-two-timelines function (unit-tested) surfaced in the window; final README divergence
sweep (add the shockwave decision outcome); the §5 20-shot acceptance protocol (statics rows N/A
until live wiring); per-phase 3–5-shot batches for E first; optional ≥1-balloon robustness tag.

### Live repoint track (any time; separable commits)
(1) test-everything WRITES the PlayMode set-equality tests (specced in §5, not yet written);
(2) José runs them in-editor; (3) green ⇒ one revertable commit per handler (Bomb/Laser
selection → core + `GridEffectBoard`; VFX/timing/dispatch untouched); red on unsettled boards ⇒
cores stay sim-only, delta documented; (4) then repoint `FindNearestColorId`'s single caller
onto `LightningChain.FindNearestConcreteColor`.

### José's gates (accumulated)
1. EditMode suite run (~150 new tests since the last green run; the two graze-deflect tests flip
   red→green). 2. Play-mode graze check + device test (5d401097). 3. B3 decision: projectile
   CircleCollider2D swap — eliminates the capsule-nose mismatch class but changes contact feel;
   optional post-fix. 4. Laser scoring feel sanity (Piercing-only flags change streak behavior
   around laser clears). 5. Statics live wiring (GridActorExpansion: prefabs + colliders on
   `GridActorView._collider` + spawner registration + projectile→IHitable routing) — Phase A is
   sim-only until then.

### Deferred code follow-ups (small, none blocking)
Factor `ProjectileMotionResolver`'s duplicated quadratic setup (reviewer MEDIUM); hoist the
segment-vs-circle formula (`SegmentHitsAnyBalloon` + `LaserCross.SegmentHitsCircle`) to Shared;
H2 cruise-remainder clamp (only if playtests show pop-skipping at high cruise speed); **H6
hex-seam double-deflect** (adjacent contact circles overlap 0.75 vs 0.875 ⇒ two synchronous
deflects, ~145° heading corruption — needs its own investigation; predates all this work);
cosmetics (always-true Try-pattern flatten in gather, `ResolveSnipe` declaration order,
`ResolvePaint` Bind ordering, `GridActorView.ContactRadius` visibility pass, older lock test →
`AssertResultsMatch`); optional careful `BalloonView.ContactRadius` unification (behavior
change — feel check required). `ShotItemActivation.IsDirectHit` is stored-but-unread (kept as
the seam for E2 discharge bookkeeping / a future pop-spawn model).

### G — headless level diagnostics (follow-up tier; unchanged spec in §4 Phase G)
G-local prereqs first: `ItemAssignmentPlanner` + `SpawnPlanner` pure-core extractions with an
injectable `IRandomSource`, and `RollMoveSpeed` through the seed. Biggest risk stays
turn-SEQUENCING drift — pin the turn-sequence contract against a play-mode-captured trace before
trusting any aggregate metric.

### Design questions parked for José
Cruise ramp × snipe speed buff stacking multiplicatively (live behavior; sim mirrors it —
intended?); statics wiring priority; whether pop-spawn deserves more than documentation in F.
