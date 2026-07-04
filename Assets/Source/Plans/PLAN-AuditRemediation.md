@page plan_audit_remediation Audit Remediation

# Audit Remediation

> Remediation roadmap from the 2026-07-02 full audit (architecture, scalability, logic/GC
> performance, rendering performance, documentation drift). The codebase came out healthy:
> docs ~95% accurate, the newest systems allocation-clean, and the classic Unity scaling
> traps already avoided. This plan sequences the residual findings — one wrong README
> section, a message-bus fan-out that compounds with content, a reachable item-handler
> corruption, GC hot paths on the pop/nudge path, and a fill-rate-dominated rendering
> profile. Ordered so that small risk-removals land first and the big structural change
> (the hit pipeline) lands once, deliberately.

---

## Orientation — start here

**Provenance:** five parallel audit passes over `Assets/Source`, followed by a second
adversarial verification pass (2026-07-02, after the cinematics-architecture refactor
landed) that re-confirmed, corrected, or strengthened every claim below against the
current tree. Naming note: the cinematics refactor renamed `LevelUpTrailEffect` →
`LevelUpCinematic` and moved the timeScale tween into `CameraRigCinematic`; this plan
uses the current names.

**Status:** Phases 0 + 1 **implemented 2026-07-02** — docs truthed up (Score README,
scope table, Shared message rows, plus stale `LevelUpTrailEffect`/`NextLevel` references
in the LevelUp README, Game README, Pool README, Tests README, and
`arch_score_cinematic`), dead trail API deleted (`PauseWhere`/`CompleteWhere`/
`SetSpeedWhere`, `ScorePointMessage.NextLevel` + its three EditMode tests reworked), and
all six Phase 1 fixes applied (1a capability count via `_grid.At(...) is IHasItemSlot`;
1b/1c `IDisposable` + CTS on `BalloonSpawner`/`ItemActivator`; 1d message list copy;
1e `IScoreQuery.WillLevelUp`; 1f instance registries). EditMode suite confirmed green
in-editor. **Phase 2 implemented 2026-07-02**: `Setup`+`Activate()` collapsed into
`IBalloonItem.Activate(balloon, worldPosition)` across all five handlers; Lightning's
target/position buffers and Paint's splash state are per-activation locals captured by
the deferred callbacks; Laser's cross-activation `_capturedRotations` kept by design;
`ShieldItemHandlerTests`/`LightningItemHandlerTests` (which had appeared since the
review) and Paint/Bomb tests ported, plus a new `Activate_OverlappingChains_KeepIndependentTargets`
regression test driving two chains through a fake `IChainEffect`. **Phase 3 implemented
2026-07-03** except 3c: 3a (pool-key cache + `IsRegistered` on `PoolManager`, cached
self-return delegates on `PoolableParticle`/`EffectView` — the pop-VFX path is now
allocation-free per call), 3b (loop-based `FindOverride` + combined `Resolve(out,out)`;
the split `Resolve*` wrappers remain for their tests), 3d (static reused BFS collections
in `PressureCascade`), 3e (all four). 3c was re-scoped to pair with 5e (see the 3c entry
— it needs the central balloon-motion ticker, not a batch patch). Profiler verification
of a multi-pop + bomb turn still pending in-editor. **Phase 4 implemented 2026-07-03**
in the planned two parts: (1) `IHitDispatcher` (`Shared/Messages`) + `HitPipeline`
(`Game/`) — all producers dispatch instead of publishing; the score/streak stage runs
first (structural contract for the resolver's streak read), then the bus broadcast for
order-independent observers (`ItemActivator` stays on the bus: its frame-yield already
order-decouples it, and making it a stage would create the DI cycle
pipeline→activator→handlers→pipeline); (2) `BalloonControllerRegistry` — controllers
register per spawn, the pipeline routes hits by model lookup (per-balloon
`ActorHitMessage`/`BoardClearMessage` subscriptions deleted; a `_popped` guard replaces
dispose-to-stop-double-pop; popped-but-waiting item balloons stay registered so board
clear still reaches them; the registry's single snapshot-iterating board-clear pass
replaces the re-entrant-disposal dance). Docs updated (`arch_turn_pipeline`,
Balloon/Game/Shared READMEs + the dispatch-only rule in the style guide). Ordering
deltas from the old subscription order, analyzed safe: score now runs before
nudge/grid-actor removal (no shared state), and the owner reaction runs before the bus
broadcast (nudge reads model slot values, not the removed grid entry). In-editor
EditMode run + focused playtest (streak-shield, item balloons, board clear/restart)
confirmed green 2026-07-03; the hit-routing registry was subsequently upgraded to the
int-handle/flat-array design (commit 8a9e35af — see the watch-items entry).
**Phase 5a DONE 2026-07-03** via the `SpriteShadowBaker` component (9baa87b3 + hardening:
readback orientation flip, bake-before-swap `_SpriteScale` ordering, PPU-mismatch
detection, ToughBalloon `_SHADOW_OFF` toggle) — baked shadows rolled out across
Balloon/Balloon5/Balloon10/ToughBalloon/Unbreakable, score/shield/heart trails, thrower
trace (1f7f24d2, 14a63876, 76ff7c69). The Unbreakable gained a shadow it never had
(its shaders never implemented one); rotating-shell prefabs parent the shadow under a
non-rotating ancestor per the ShadowBake README. **Residual 5a verification:** Frame
Debugger/device pass to bank the tap-count win, and a sweep for any family-material
stragglers still in use. **Phase 5b DONE 2026-07-03**: GrabPass deleted from
`UnbreakableBalloon.shader`; the chrome samples the shared `SceneCaptureService`
capture (`Display/`, on the main camera — generic by design, Unbreakable is merely the
first `Acquire`/`Release` consumer), verified in-editor with a clear performance win.
The capture's narrow layer mask also exposed a latent bug: instanced bush leaves were
submitted on layer 0 (the short `DrawMeshInstanced` overload's default) instead of the
bush's layer — fixed in `BushView`, with shadow casting pinned off. **Phase 5c DONE
2026-07-04 (both tiers)**. Tier 1 (procedural trims, verified in-editor): lighting
normals from screen-space derivatives of the density pass's low-frequency partial
(−12 evals/px; gradient computed before divergent flow where ddx/ddy are undefined;
scaled by 2·ε·strength so authored tuning keeps meaning), two-octave shadow noise (−1),
and a disturbance-gated displaced evaluation (−3 on calm pixels). Worst case 21 → 8
simplex evals/px, calm pixels 5. Tier 2 (baked noise): every octave is now one fetch of
a tileable 16-bit-PNG periodic-Perlin texture (`CloudNoiseTextureGenerator`, menu Tools >
BalloonParty), histogram-matched to the old simplex so the authored thresholds keep
meaning; measured **267 → 353 FPS in-editor A/B** on the same scene, after which the
procedural simplex path was removed outright. Two latent shader defects were found and
fixed en route (both existed before, masked by simplex's high-frequency contrast):
per-cluster noise seed offsets cut the field along Voronoi borders — removed in favor of
continuous world-space sampling (scroll already provides respawn variety) — and
`SlotFalloff`'s hard `min()` creased the fade band along slot midlines into a hexagonal
web — replaced by a polynomial smooth-min union (`_SlotBlend` knob). Gotchas recorded:
EXR import in a Gamma-color-space project bakes a linear→gamma lift into texel data (use
16-bit PNG); the `_NOISE_DEBUG` grayscale toggle stays in the shader for field
inspection. **5d combiner experiment underway** (tooling + prefab flattening committed;
atlas + Frame Debugger measurement pending). **3c DONE 2026-07-04**
(`BalloonMotionTicker`, nudge GC); **5e built, evaluated, CANCELLED** — no measurable
Animator win, see its entry. 5f / Phase 6 not started.

**Key fact discovered during the audit:** the project runs on the **Built-in Render
Pipeline**, not URP (`GraphicsSettings.asset` → `m_CustomRenderPipeline: {fileID: 0}`).
Batching is sprite dynamic batching; `MaterialPropertyBlock`s break it per-renderer;
`GrabPass` executes. All rendering guidance in Phase 5 assumes Built-in. A conditional
migration plan exists at @ref plan_urp_migration — Phase 5 here is a prerequisite for it.

**Interlocks with other plans:**
- @ref plan_cinematics_architecture is **implemented** (Phases 1–5 landed 2026-07-02).
  Its Phase 3c kept trail puppeteering local, so the per-point `TrailId` coupling
  described in Phase 6a **survived the refactor intact** — 6a is gated on a designed
  replacement for per-point trail identification, not on the cinematics plan anymore.
- The level-up cinematic trail path remains a known-fragile area: a previous trail GC
  optimization was reverted (commit `f27376f`) because the cinematic identifies the
  tipping trail by per-point `TrailId`. Phases 3 and 6a are written around that
  constraint.

**Verification per phase:** `dotnet build BalloonParty.Runtime.csproj -nologo
-clp:ErrorsOnly` (plus `.Tests.EditMode.csproj` when tests are touched) + `python3
Tools/style_audit.py` for code phases. Phase 5 (rendering) requires in-editor work:
Frame Debugger, Profiler, overdraw view — `dotnet build` does not compile shaders.

---

## Phase 0 — Documentation truth-up (S)

No behaviour change (except deleting dead API, which touches tests). Markdown edits
don't trigger the style audit.

1. **`Game/Score/README.md` — the trail-tracking/selective-pause story is wrong
   throughout, not just in one section.** Verified drift: line 12 claims
   `ScoreTrailService` is `ICinematicAware` (it's `IStartable, IDisposable`) and that a
   `NextLevel` flag gates spawns (nothing gates spawns — `OnScorePoint` spawns
   unconditionally); the "Selective Pause" section (~57–63) describes
   `PauseTrailsAbove(TrailId)` and `Cinematic.IsPlaying && NextLevel` gating, neither of
   which exists; lines 65–67 and 75 reference `TrackTrail` / `ResumeTrail` /
   `ClearTrackedTrail`, none of which exist; line 72 names the deleted
   `LevelUpTrailEffect`. Actual behaviour: the tipping trail is paused individually
   (`LevelUpCinematic.cs:169–170`) and `EndPanIn()` calls `Flights.CompleteAll()`
   (`:245–249`). Rewrite the Contents row, Spawn & Tracking, Selective Pause, and
   Interactions sections against the current code.
2. **Delete the dead API** the wrong sections referred to:
   - `TrailFlightRegistry.PauseWhere` / `CompleteWhere` / `SetSpeedWhere` — zero
     references anywhere including tests; delete freely.
   - `ScorePointMessage.NextLevel` — never read in production, **but asserted in
     `Assets/Tests/EditMode/Game/ScoreControllerTests.cs:310–343`** (three tests) and
     part of the message constructor. Deleting it means updating those tests and every
     construction site; keep the `Score`/`Level` renumbering assertions — that logic is
     real. If selective pause is ever wanted for real, design it fresh (see 6a); don't
     resurrect this dead code.
3. **`Assets/Source/README.md` scope-hierarchy table** (lines ~63–68) — add the three
   missing scopes (9 exist, 6 listed): `HealthUILifetimeScope` (`UI/Health/`),
   `DangerUILifetimeScope` (`UI/Danger/`), `GameOverLifetimeScope` (`UI/GameOver/`).
4. **`Shared/README.md`** — add `ColorStreakTracker` (subscribes in its constructor) to
   the `ScoreLevelUpMessage` consumer row (line ~32); update the `ActorHitMessage` row
   (line ~29) to the current shape (`Actor`, `WorldPosition`, `ProjectileDirection`,
   `Outcome`, `Context` — a `DamageContext` carrying `Damage`).

Everything else (all feature READMEs, all 13 diagrams, the Plans registry) verified
accurate.

---

## Phase 1 — Risk-removal batch (S, one sitting)

Independent, small, pure correctness/robustness. No behaviour change in the happy path.

| # | Fix | Where | Why |
|---|---|---|---|
| 1a | Count items via `IHasItemSlot`, not `ActorAt<BalloonModel>` | `Item/ItemAssigner.cs:116` | Eligibility (`:96`) already uses the capability; `BalloonModel` is today the sole implementer (`UnbreakableBalloonModel`/`ToughBalloonModel`/`BubbleClusterModel` extend `BalloonModelBase` without it), so the fix is behaviour-neutral now and removes the latent cap-accounting bug. |
| 1b | Make `BalloonSpawner` `IDisposable`; cancel + dispose `_cts` | `Balloon/Spawner/BalloonSpawner.cs:27` | The class is not `IDisposable` and its registration adds no disposal path, so VContainer never cancels it. The generation guard (`:232,243`) covers *run reset* only; in-flight `SpawnLinesWithDelayAsync` (`:221–255`) survives scope teardown and touches pools/publishers. Mirror `GridSpawnerCoordinator.cs:30–35` (or `ScoreTrailService.cs:54–55`) which do it correctly. |
| 1c | Cancel `ItemActivator.ActivateAsync`'s `UniTask.Yield` | `Item/ItemActivator.cs:68–88` | No token anywhere; all five handlers' `Activate()` are fully synchronous, so the `catch (OperationCanceledException)` (`:80–83`) is unreachable, period. **Scope 1c to the activator-side yield only** — threading a token into the handlers touches the same `IItem.Activate()` signature Phase 2 rewrites; do that part with Phase 2. |
| 1d | Stop publishing the live `_newlySpawnedBalloons` list in `ItemCheckMessage` | `Balloon/Spawner/BalloonSpawner.cs:160–168` | The list is `Clear()`ed on the next line (also cleared at `:96,:106`); sole subscriber (`ItemAssigner.OnItemCheck`) is synchronous today. Copy into the message — cheap (per-turn, small list) and removes the trap. |
| 1e | Move `WillLevelUp()` onto `IScoreQuery`; inject the interface | `Game/Cinematics/LevelUpCinematic.cs:38,65,120` | `WillLevelUp()` is the *only* member the cinematic uses on the concrete `ScoreController`; `IScoreQuery` exists and is registered (`GameLifetimeScope.cs:146`). Update the interface's doc comment (currently scoped to "HUD bars"). |
| 1f | Make `StaticActorSpawner.StrategyCache`/`ModelFactories` instance fields | `Slots/Actor/StaticActorSpawner.cs:20–28` | No external references (tests reference the class only); the test-only constructor (`:55–59`) is unaffected. Mechanical consequence: `GetStrategy`/`CreateModel` (`:189–214`) become instance methods. |

Acceptance: `Runtime` + `Tests.EditMode` builds green, style audit green, EditMode tests
pass.

---

## Phase 2 — Item handler reentrancy (S–M)

**Problem:** item handlers are `Lifetime.Singleton` (`GameLifetimeScope.cs:159–163`)
with two-phase mutable state (`Setup(balloon, pos)` on `IBalloonItem` →
`Activate()` on `IItem`). Two live corruptions:

- **Lightning** (`Item/Lightning/LightningItemHandler.cs`): `OnJump` (`:117–129`)
  captures the shared `_targetsBuffer` (`:110`), and `ChainLightningView` drives jumps
  over `JumpTime` intervals long after `Activate()` returned — a second activation
  `Clear()`s and refills the list mid-chain, hitting wrong balloons. **Also** the shared
  `_positionsBuffer` (`:37,90–95`) is stored *by reference* in `ChainLightningView`
  (`ChainLightningView.cs:80`), so the first chain's visuals corrupt too. Reachable
  today: a bomb blast containing two lightning balloons activates both on the same
  frame (both resume after the same `UniTask.Yield`), or a chain pops another
  same-color lightning balloon.
- **Paint** (`Item/Paint/PaintItemHandler.cs`): the per-activation lists are safely
  fresh, **but `OnSplash` reads the `_worldPosition` field at splash time (`:118`)** —
  an overlapping second Paint's `Setup()` overwrites it and skews the first
  activation's disturbance-stamp directions. Paint is currently wrong under overlap,
  not merely fragile.

**Fix:** collapse to a single `Activate(IBalloonModel balloon, Vector3 position)` (or
an activation-context struct, which is also where Phase 1c's cancellation token
belongs) and delete `Setup`. Each activation copies what it needs into a local/pooled
activation record; the singleton handler holds no per-activation mutable state. Makes
every future item safe by construction and unlocks the buffer-reuse optimization Paint
was previously denied.

**Touch list:** `IItem` (declares `Activate`), `IBalloonItem` (declares `Setup`), all
five handlers — Bomb, Laser, Lightning, Paint, **Shield** — `ItemActivator.cs:76–77`,
and the tests that drive the current two-phase API:
`Assets/Tests/PlayMode/BombActivationPlayModeTests.cs:68–70` and
`Assets/Tests/EditMode/Item/PaintItemHandlerTests.cs` (four `Setup`/`Activate` pairs).
**Preserve:** `LaserItemHandler._capturedRotations` (`:55`) is *legitimately*
cross-activation state (fed by `TransformCapturedMessage`, keyed per balloon) — it
stays on the singleton.

Sequence this while the item count is still five. Acceptance: builds green including
tests; manual playtest of each item; an EditMode test running two overlapping lightning
activations against a fake grid asserting target sets don't cross-contaminate.

---

## Phase 3 — Logic/GC hot paths (M)

Per the project's optimization priorities: GC hitches during action matter most. All
are localized; none touch the fragile trail-identity path.

- **3a — `PoolManagerExtensions.PlayParticle`/`PlayEffect`**
  (`Shared/Extensions/PoolManagerExtensions.cs:8–67`). Every overload allocates
  `prefab.name` (fresh managed string per access), a factory closure (created at the
  call site even when the channel already exists), and a completion closure — per call,
  on the default balloon-pop VFX path (`BalloonView.cs:237,263,268` via
  `BalloonController.Pop`; `ProjectileShieldView.cs:132,142`;
  `BushRustleController.cs:120`). No key caching exists anywhere on this path. Fix:
  cache pool keys per prefab, pre-register channels once, and have `PoolableParticle`
  carry its pool+key and return itself (cached delegate) instead of a fresh closure.
- **3b — Nudge resolver LINQ** (`Nudge/NudgeOverrideResolver.cs:64`).
  `FirstOrDefault(o => o.AppliesTo.HasFlag(source))` = closure + delegate + enumerator
  per call. Per projectile hit: **~12** allocating invocations (each neighbor does
  `ResolveDistance` + `ResolveDuration`, one allocating `FindOverride` each — the
  publisher-side `FindOverride(null, …)` short-circuits before allocating). Per bomb
  shockwave: 3 base resolves + one per occupied slot (`NudgeService.cs:106–119,142`) —
  ~40–66 typical, worse on full boards. Fix: plain `for` over the `IReadOnlyList` with
  `(o.AppliesTo & source) != 0` (`NudgeType` is a flags enum), plus a combined
  `Resolve(out distance, out duration)` so the list is walked once per neighbor.
- **3c — Nudge tween storm** (`Balloon/View/BalloonView.cs:172–184`). Sequence + two
  `DOMove` tweeners + `OnComplete` closure (+ conditional `DOScale`) per nudged
  neighbor; `Assets/Resources/DOTweenSettings.asset` has `defaultRecyclable: 0`.
  **Re-scoped after implementation attempt (2026-07-03): this is a motion-system
  change, not a batch GC patch — execute it together with 5e.** Two constraints found:
  (1) `SetRecyclable(true)` does not work — the nudge sequence is stored via
  `TweenTracker.Replace(sequence)` (`:179`) and `TweenTracker` retains `_active` past
  completion and later `Kill()`s it (stale-recycled-ref hazard); other stored-tween
  holders (`FlyingTrail._moveTween`, `CameraShakeService._shakeTween`,
  `CinematicCameraRig._tween`, `CameraRigCinematic._timeScaleTween`) must also stay
  non-recyclable. (2) A manual lerp needs new arbitration: today `transform.DOKill()` +
  `TweenTracker.Kill()` (called by `BalloonBalancer.cs:83` before balance moves and by
  `OnDespawned`) implicitly cancel a running nudge — a manual lerp escapes both, so the
  balancer/despawn paths need explicit cancellation, and the driver must be a single
  central `ITickable` calling view setters (a per-view `Update` would add the standing
  per-balloon cost that 5e exists to remove). Design it as one central balloon-motion
  ticker that owns both the nudge out-and-back and 5e's idle bob.
  **DONE 2026-07-04** — `BalloonMotionTicker` (Balloon/Controller, `ITickable`, pooled
  entries, zero steady-state allocation) owns the nudge out-and-back (two OutQuad
  halves matching the old DOMove pair); `StartNudge` replaces a running nudge silently
  (old `TweenTracker.Replace` semantics), and both found constraints are honored:
  `BalloonBalancer.AnimatePaths` and `BalloonView.OnDespawned` call `CancelNudge`
  explicitly. (The ticker briefly also carried 5e's idle bob — cancelled, see 5e.)
  Needs playtest: nudge feel, nudge-vs-balance interruption, restart mid-storm.
- **3d — `PressureCascade.TryFindChain`** (`Balloon/Controller/PressureCascade.cs:52–54`).
  Fresh `Dictionary` + `HashSet` + `Queue` per call. Call path:
  `BalloonSpawner.SpawnLineInternal` → `BalloonPlacementResolver.Resolve` → on
  blockage, `TryNearestColumn` probes up to Columns candidates, each running one BFS —
  worst case **Columns² × lines per turn**, precisely during the overflow/danger
  crunch. Fix: reusable member/static collections cleared at entry (main-thread only)
  or `UnityEngine.Pool` — matching the `ListPool` pattern already in
  `BalloonBalancer.cs:194`.
- **3e — Low, batch opportunistically:** cache `ThrowerController.ProjectilePoolKey`
  string (`:42`); dictionary-backed `ItemConfiguration` indexer (`:14`, `First()` per
  activation); `FPSCounter.OnGUI` string/`GUIContent` caching (`:60–63` — it pollutes
  the very GC profiles used to validate this phase); `PauseService` `.Any()` → loop
  (`:73`).

**Explicitly out of scope:** the per-point `ScorePointMessage`/trail-spawn storm
(`ScoreController.PublishPoints`, `:248–268`) — see Phase 6a. `SlotClusterRegistry`
transient collections stay deferred (flood-fill correctness risk, medium payoff).

Acceptance: builds green; Profiler capture of a multi-pop + bomb turn shows the
`PlayParticle`/nudge allocation spikes gone (in-editor).

---

## Phase 4 — Hit pipeline restructure (H1 + H2) (M–L, the big one)

Two problems, one fix; do them together and deliberately.

**H1 — broadcast fan-out.** Every live balloon subscribes to the global
`ActorHitMessage` and filters by `ReferenceEquals`
(`Balloon/Controller/BalloonController.cs:66`, `:106–111`) → O(active balloons)
delegate invocations per hit, plus 2–3 subscription allocations per spawn (`:66,69`,
third on item-balloon pop `:171`). Compounding: AoE items publish per-target in loops
(`Item/Bomb/BombItemHandler.cs:109`, `Item/Lightning/LightningItemHandler.cs:81,125`)
— one bomb popping 15 balloons on a busy board ≈ ~1,000 no-op filter invocations, and
each pop re-enters the bus (`TransformCapturedMessage` `:164`, `NudgeMessage` `:100`).

**H2 — implicit dispatch contracts.** Three systems depend on undocumented MessagePipe
dispatch behaviour (synchronous delivery; de facto order = subscription order =
registration order in `GameLifetimeScope`; no ordering API exists):
- `Projectile/Controller/ProjectileHitResolver.cs:63–67` reads `ColorStreakTracker`
  immediately after `Publish` (`:60`). This works because `Publish` dispatches
  synchronously and `ScoreController.OnActorHit → _streakTracker.Record` is
  synchronous — **the contract is "streak recording stays synchronous during
  dispatch"**, and nothing enforces or documents it; any deferral breaks the
  streak-shield rule silently.
- `Item/ItemActivator.cs:72–74` yields a frame so all synchronous subscribers finish —
  here registration order *does* matter: the activator (registered at startup) handles
  the hit before the per-balloon controller publishes `TransformCapturedMessage`,
  which `LaserItemHandler._capturedRotations` needs before `Setup`.
- `BalloonController.cs:75–77` hand-rolls a re-entrant-disposal dance for
  `BoardClearMessage`.

**Direction:**
1. Introduce an explicit **hit pipeline** object owning turn resolution: invokes the
   order-dependent stages (streak/score recording → item handling → owning-balloon
   reaction) in declared order, synchronously — the pipeline *owns* streak recording so
   the resolver's read-after-publish contract becomes structural. The MessagePipe
   broadcast remains for genuinely order-independent observers (nudge, VFX, danger,
   diagnostics).
2. Route the **owning-balloon reaction** directly: resolve `msg.Actor → controller`
   via a map keyed by model (or via `SlotGrid`) and call it — balloons stop
   subscribing individually. `GridActorHitController`
   (`Slots/Actor/GridActorHitController.cs`) already does exactly this for non-balloon
   actors and is the in-repo precedent.
3. The re-entrant-disposal dance belongs to the **`BoardClearMessage`** subscription
   (`:69`), which owner-routing of *hits* doesn't touch — deleting it additionally
   requires centralizing board-clear teardown through the same model→controller map
   (or `BoardClearController`). Plan it as part of this phase, not a freebie.
4. Document the pipeline in `Diagrams/arch_turn_pipeline.md` and the affected READMEs
   (responsibility shift — the living-docs rule applies).

Risk management: land in two commits (pipeline extraction with unchanged semantics,
then owner-routing); the streak-shield rule and item activation each need a focused
playtest + EditMode coverage where the seams allow.

Do this **before** the next wave of content (new balloon types / items) — it's the
scalability purchase everything else multiplies against.

---

## Phase 5 — Rendering fill-rate program (M–L, in-editor)

Fill rate dominates; draw calls second; standing CPU third. Each item needs Frame
Debugger / Profiler / overdraw confirmation in the editor — **measure before and
after, on device where possible**. Items are independent; ordered by expected payoff.

- **5a — Bake the blur/shadow out of the sprite shaders.** The balloon prefab renders
  4 transparent layers: Body/Knot via `SpriteShadow.shader` (softness > 0 takes the
  9-tap branch + base tap = 10 samples/px; the branch is uniform-based, so
  softness-0 materials are genuinely cheap), Shine + Specular share
  `SpecularBalloonBlur.mat` on `SpriteBlur.shader`, which has **no cheap branch — 9
  unconditional taps even at `_BlurAmount 0`**. Central overlap pays ~38 taps/px ×
  66-slot board, every frame, blurring static images. Bake shadow/blur into the
  authored sprite textures (the bush bake pipeline is the in-repo precedent); runtime
  materials become plain sprite shaders. **Sweep scope: 28 family materials** (18
  SpriteShadow, 5 SpriteBlur, 1 Composite, 2 Shine, 2 ShineShadow; 26 with
  softness > 0), including `PS_Material_BalloonPop.mat` (pop bursts pay 10 taps/px per
  particle), `ScoreTrail`/`ShieldTrail` orbs, and `TrailMaterial_Projectile`/`_Shield`
  ribbons. Note: `HeartTrail.mat` and several ribbons
  (`TrailMaterial_ScoreTrail`/`_ScoreTrailShadow`/`_Prediction`) are already on cheap
  stock `Mobile/Particles` shaders — don't count them. **Free win found during
  verification:** the Knot draws a full-size 1×1 sliced quad with `_SpriteScale 0.1` —
  it pays 10 taps/px over a quad 10× its visible size; shrink the quad regardless of
  the baking work. **Approach (settled 2026-07-03 after one rejected iteration):** a
  generalized bake suite (report/definition assets/two-mode baker) was built and
  rejected as over-engineering; the shipped approach is the **`SpriteShadowBaker`
  component** (`Shared/Rendering/`, editor logic in `Editor/ShadowBake/`) — drop it on
  a prefab, tune, press Bake: it renders the union silhouette of the child sprites,
  blurs it offline (quality knobs — it's bake-time), writes the sprite to
  `Assets/Sprites/Baked/Shadows/<prefab path>/<prefab>_Shadow.png`, optionally swaps
  the family materials for a plain one (compensating each renderer by the material's
  `_SpriteScale`), and wires a `BakedShadow` child. Facts that shaped it:
  `SpriteShadow` multiplies both sprite and shadow by the renderer tint, so baked
  shadows tint like shader shadows did; `PaintSplashView` drives `_SpriteScale` at
  runtime (exclude); ToughBalloon's body animates material properties (exclude).
- **5b — Kill the `GrabPass`, keep the live reflection**
  (`Assets/Shaders/BalloonParty/Balloon/UnbreakableBalloon.shader:88`). Full-screen
  mid-frame resolve whenever an unbreakable is visible, and **8 of the prefab's 12
  SpriteRenderers** run the GrabPass shader (4 outer + 4 inner chrome layers; 3 more
  on the rim shader), each with the 3×3 `EdgeMask` loop and `pow`/`atan2` chains.
  **Prebaking is ruled out by design intent (decided 2026-07-03): the real-time
  reflection is the chrome ball's identity.** **Implemented 2026-07-03** as the
  general `SceneCaptureService` (`Display/`, on the main camera): a scheduled low-res
  capture of configurable layers at the main camera's framing, bound globally as
  `_SceneCaptureTex`, ref-counted by consumers (`Acquire`/`Release` — the Unbreakable
  variant is the first; renders nothing with zero consumers) and rendered every Nth
  frame (downscale + interval on `IGameDisplayConfiguration`, defaults 8/2). The
  shader swapped `GrabPass`/`_GrabTexture` for the capture (`ComputeGrabScreenPos` →
  `ComputeScreenPos` — camera RTs carry no grab-flip; dead `grabPos` interpolator
  removed). Keeps reflections of transparents (an after-opaque copy would reflect only
  the sky). **In-editor setup required:** add `SceneCaptureService` to the Main Camera
  prefab + set the captured-layers mask (exclude the Unbreakable's own layer); verify
  reflection orientation on Metal and tune downscale. Also the URP-migration blocker
  (see @ref plan_urp_migration).
- **5c — PuffCloud noise → texture** (`Grid/PuffCloud.shader`). DONE — see the Status
  header. Original sizing: worst case 7 `CloudNoise` calls × 3 simplex octaves ≈ 21
  octaves/px (2 density + 1 shadow + 4 lighting central-differences) plus the per-pixel
  `_SlotCount` loop executed twice when shadow is on, with `PuffMain.mat` shipping both
  `_DENSITY_ON` and `_SHADOW_ON`. Landed as Tier 1 trims (derivative lighting, 2-octave
  shadow, disturbance gate) + Tier 2 texture fetches, 267 → 353 FPS in-editor.
- **5d — Restore balloon batching via atlas-collapse.** `BalloonView.ApplySortingOrder`
  (`:272–276`) + `SortingHelper.SlotBaseSortingOrder` give each balloon a unique
  50-wide order band with layers interleaved at +1..+4 → ~3 draws per balloon
  (Shine+Specular share a material and batch) ≈ 120–200 draws for a 40–66 balloon
  board. **Prefer collapsing Knot+Body(+baked shine) into one atlas sprite** (pairs
  with 5a). The cheaper-looking alternative — per-layer global ordering (all knots,
  then all bodies, …) — is **not artifact-free**: rest gap between neighbors is ~0.11
  world units vs `_nudgeDistance` 0.15, and balance moves travel full slots, so
  balloons transiently overlap and a neighbor's Shine would draw over a passing
  balloon's Body. Confirm actual batch counts in Frame Debugger before and after.
  **Status 2026-07-04: combiner tooling built** (`SpriteLayerCombiner` +
  `Editor/SpriteCombine/`), scoped by decision to the **colored normal balloons only**
  (their five layers are rigid — base `StableIdle` animates only the shared "Ballon"
  child; Tough/Unbreakable/items stay live). Two-group flatten: Knot+Body baked
  tint-neutral (runtime palette tint stays), Shine+Specular baked with authored
  colors. Remaining: bake + manual prefab wiring, one SpriteAtlas + shared material
  across the combined sprites (batching does not materialize without it), Frame
  Debugger before/after.
- **5e — Replace per-balloon idle Animators. CANCELLED 2026-07-04** after being built
  and evaluated in-editor: no measurable win — the 40–66 idle Animators evaluate a
  single 3-key euler curve each and their cost is noise next to fill rate on this
  game's profile. The implementation (dual-sine ticker bob replicating
  `StableIdle.anim`, Animator disabled-while-stable with a wake-for-wiggle handoff)
  was removed; the nudge half survives as 3c's `BalloonMotionTicker`. Findings worth
  keeping if this is ever revisited: both `StableIdle` and `UnstableIdle` animate only
  the "Ballon" child's euler (no property conflict with nudges writing root position);
  parameters written to a disabled Animator are lost — set them after enabling; and
  `ToughStableIdle.anim` animates material properties (`_SphereWarp`,
  `_CrackThreshold`), so the Tough variant can never be transform-sine-driven (its
  material-instantiation batching cost remains a separate open item).
- **5f — Self-derived shader clocks for balloon variants.**
  `Balloon/Type/UnbreakableBalloonVariant.cs:49–70,90–110` and
  `SoapBubbleClusterVariant.cs:55–83,144–155` do Get/Set/SetPropertyBlock per renderer
  per frame just to advance `_TimeOffset`/`_Rotation` (up to 11 renderers for
  Unbreakable). No other code writes those MPBs, and `_SphereCenter` only changes on
  movement — push it on change only. Two verified gotchas: (1)
  `UnbreakableBalloon.shader:271` *already* adds `_Time.y + _TimeOffset` while C#
  pushes `Time.time + phase` — the clock currently runs ~2× speed, so a phase-only
  push halves the animation speed; retune the authored speed to match current visuals.
  (2) `SoapBubbleCluster.shader:275` uses `_TimeOffset` as the *whole* clock and
  `_Rotation` is a C#-integrated angle — this one needs the shader edit (add a
  `_Time.y` term + a `_RotationSpeed` uniform; the speed is constant after `Bind`).

Shader edits cannot be validated by `dotnet build` — every item here ends with an
in-editor check and a device profile.

---

## Phase 6 — Deferred / gated

- **6a — Score-trail message aggregation** (gated on a tipping-trail identification
  design). `ScoreController.PublishPoints` publishes one `ScorePointMessage` per point
  (points × streak multiplier, `:228,255`); each spawns a pooled trail + DOTween
  sequence + several closures. Scales with score inflation, not content. The
  cinematics refactor **kept** per-point trail identity: `LevelUpCinematic` builds
  `_tippingTrailId = new TrailId(msg)` from a single point message (`:126`), polls
  `Flights.Contains` (`:136`), and matches arrival by `(Color, Score, Level)`
  (`:182–184`). Aggregating to one-message-per-group breaks all of that — this item
  needs its own design for how the cinematic identifies the tipping trail (e.g. the
  group message carries a designated tipping index, or the trail service nominates the
  crossing trail) before any restructuring. The reverted attempt (`f27376f`) is the
  cautionary precedent.
- **6b — Extract projectile motion rules from the view.**
  `Projectile/View/ProjectileView.cs:138–165` owns movement, wall-bounce, shield
  decrement, and the destroy decision (plus `ShieldLostMessage` and disturbance
  stamping), called from `FixedUpdate` — gameplay rules locked in a MonoBehaviour,
  untestable headless (the one real MVC deviation; `ProjectileHitResolver` shows the
  target pattern). Extract into an `IFixedTickable` controller or a plain bounce-rule
  object alongside `WallLimits`.
- **6c — Split `GameLifetimeScope`** (`Game/GameLifetimeScope.cs`, ~60 registrations,
  24 brokers) into per-feature `IContainerBuilder` extension methods
  (`builder.RegisterItemFeature()` …). Pure code motion; do it whenever the file next
  gets annoying.
- **Watch items (no action):** `Cinematic._listeners` is an unbounded static list
  relying on manual `Unregister`; `ThrowerController.cs:84–87` subscribes to static
  `Navigation.Current` bounded only by `Take(1)` — fine as-is, dangerous if copied
  without the `Take`. `SlotClusterRegistry` transient collections stay deferred.
- **PuffCloud mega-quad (mitigated in-shader 2026-07-04, structural fix deferred):**
  `ClusterViewController.Reconfigure` drives ONE view whose quad spans the bounding
  box of **all** puff clusters on the board — fill cost scales with cluster *spread*,
  not cloud area (measured ~500 vs ~300 FPS in-editor depending purely on layout).
  The shader now discards dead-span fragments after only the falloff loops (before
  all texture fetches) and skips shadow evaluation where the shadow is provably
  invisible (outside its falloff or fully covered by opaque cloud — exact when
  `_CloudColor.a` = 1). If clouds still show in a device GPU profile, the structural
  fix is per-cluster views/quads — trade-off: N draw calls + overlap overdraw between
  nearby clusters vs today's 1 call + dead span. Related latent cap:
  `PopulatePositions` silently drops slots past the 16-entry buffer (MAX_SLOTS) —
  boards with >16 puff slots would render some clusters cloudless.
- **Hit-routing handle registry (implemented 2026-07-03, by choice ahead of profiling
  evidence):** `BalloonControllerRegistry` is backed by flat arrays indexed by an
  opaque `IBalloonModel.RegistryHandle` (int, -1 unregistered, written only by the
  registry) with a free list — array indexing, zero hashing, still move-invariant. A
  reference compare on resolve stands in for a generation counter: models are never
  pooled, so a stale handle can only point at null or a different model.
  `BalloonControllerRegistryTests` covers the handle/free-list mechanics (resolve,
  unregister, stale-handle rejection on index reuse, growth past capacity).
  Do **not** switch to slot-indexed routing: balloons change slots constantly
  (balance, pressure shoves, spawn paths), so a slot-keyed array must be updated on
  every move — either per-balloon `SlotIndex` subscriptions (reintroducing the fan-out
  Phase 4 removed) or coupling the balance path to hit routing, with a mid-move hit
  routing to the wrong balloon as the failure mode. Identity keys can't have that bug.

---

## Suggested sequence

1. **Phase 0 + Phase 1** — one sitting each; pure risk/drift removal (Phase 0 now
   includes an EditMode-test update for the `NextLevel` deletion).
2. **Phase 2** — while the item count is small; carries the handler-side half of 1c.
3. **Phase 3** — GC wins; verifiable in one profiling session.
4. **Phase 5a/5b** — the two biggest GPU wins (and 5b unblocks URP later); rest of
   Phase 5 as playtest sessions allow.
5. **Phase 4** — the structural change, before the next content wave.
6. **Phase 6a** — only after its tipping-trail identification design exists.
