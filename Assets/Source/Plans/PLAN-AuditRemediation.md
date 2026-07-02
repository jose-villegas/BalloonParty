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
  **Fix: replace the nudge with a manual eased lerp** (it's a simple out-and-back;
  `FlyingTrail.SetupFollow` demonstrates the pattern). The `SetRecyclable(true)`
  alternative does **not** work as written: the nudge sequence is not fire-and-forget —
  `BalloonView.Nudge` stores it via `TweenTracker.Replace(sequence)` (`:179`), and
  `TweenTracker` retains `_active` past completion and later `Kill()`s it — exactly the
  stale-recycled-ref hazard. Recycling would require `TweenTracker` to clear its ref on
  completion first. (Other stored-tween holders, for reference: `FlyingTrail._moveTween`,
  `CameraShakeService._shakeTween`, `CinematicCameraRig._tween`,
  `CameraRigCinematic._timeScaleTween` — keep all non-recyclable.)
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
  the baking work.
- **5b — Kill the `GrabPass`**
  (`Assets/Shaders/BalloonParty/Balloon/UnbreakableBalloon.shader:88`). Full-screen
  resolve per frame whenever an unbreakable is visible, and **8 of the prefab's 12
  SpriteRenderers** run the GrabPass shader (4 outer + 4 inner chrome layers; 3 more
  on the rim shader), each with the 3×3 `EdgeMask` loop and `pow`/`atan2` chains.
  Replace the convex-mirror reflection with a prebaked reflection texture or a
  `shader_feature` gated off on mobile. Also the URP-migration blocker (see
  @ref plan_urp_migration).
- **5c — PuffCloud noise → texture** (`Grid/PuffCloud.shader`). Worst case 7
  `CloudNoise` calls × 3 simplex octaves ≈ 21 octaves/px (2 density + 1 shadow + 4
  lighting central-differences) plus the per-pixel `_SlotCount` loop **executed twice**
  when shadow is on — and `PuffMain.mat` ships with both `_DENSITY_ON` and
  `_SHADOW_ON` enabled, so the worst case is the shipped case (discarded pixels exit
  after ~9 octaves). Bake the noise field into a small tileable scrolling texture (1
  fetch), derive the lighting normal from its channels, skip the dual orig/displaced
  evaluation when local disturbance ≈ 0.
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
- **5e — Replace per-balloon idle Animators (base balloon only).** Balloon.prefab's
  Animator is enabled, Culling Mode = Always Animate, never disabled by `BalloonView`
  — so every on-board balloon evaluates `StableIdle.anim` (a looping 3 s euler curve
  on the child "Ballon") every frame forever. The clip animates **only child
  localEulerAngles** while nudge tweens write **root position/scale** — no property
  conflict, so a single `ITickable` sine-driver over stable balloons is safe; keep the
  Animator for the unstable wiggle (or disable-while-stable + Cull Completely for the
  offscreen case). **Exception: `ToughStableIdle.anim` animates material properties**
  (`material._SphereWarp`, `_CrackThreshold`) — that both instantiates materials
  (batching-breaking) and can't be transform-sine-driven; the Tough variant needs its
  own treatment (MPB- or shader-side animation). Profile Animator ms first to size the
  win.
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
