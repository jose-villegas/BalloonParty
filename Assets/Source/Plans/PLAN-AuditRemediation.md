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

**Provenance:** five parallel audit passes over `Assets/Source` at HEAD `1325333b` plus the
working-tree cinematics edits. Every finding below was verified against the code at that
state — re-verify file:line references before acting if significant time has passed.

**Key fact discovered during the audit:** the project runs on the **Built-in Render
Pipeline**, not URP (`GraphicsSettings.asset` → `m_CustomRenderPipeline: {fileID: 0}`).
Batching is sprite dynamic batching; `MaterialPropertyBlock`s break it per-renderer;
`GrabPass` executes. All rendering guidance in Phase 5 assumes Built-in. A conditional
migration plan exists at @ref plan_urp_migration — Phase 5 here is a prerequisite for it.

**Interlocks with other plans:**
- @ref plan_cinematics_architecture — Phases 1+2 of that plan are implemented (working
  tree); its Phase 3c (trail-path conversion) **gates** Phase 6a here. Do not restructure
  the score-trail message path before it.
- The level-up cinematic trail path is a known-fragile area: a previous trail GC
  optimization was reverted (commit `f27376f`) because the cinematic identifies the
  tipping trail by per-point `TrailId`. Phases 3 and 6a are written around that
  constraint.

**Verification per phase:** `dotnet build BalloonParty.Runtime.csproj -nologo
-clp:ErrorsOnly` + `python3 Tools/style_audit.py` for code phases. Phase 5 (rendering)
requires in-editor work: Frame Debugger, Profiler, overdraw view — `dotnet build` does not
compile shaders.

---

## Phase 0 — Documentation truth-up (S)

Four edits; no behaviour change. Markdown edits don't trigger the style audit.

1. **`Game/Score/README.md` — rewrite the "Selective Pause" section (~lines 57–63).**
   It describes `PauseTrailsAbove(TrailId)` and `ScorePointMessage.NextLevel`-flag gating
   that were never implemented. Actual behaviour: the tipping trail is paused
   individually; when it lands, `LevelUpTrailEffect.EndPanIn()` calls
   `Flights.CompleteAll()` on everything. Rewrite to describe reality.
2. **Delete the dead API** the wrong section referred to:
   `TrailFlightRegistry.PauseWhere` / `CompleteWhere` / `SetSpeedWhere` (never called) and
   the never-read `ScorePointMessage.NextLevel` flag *if* nothing else consumes it —
   re-grep before deleting. If selective pause is ever wanted for real, it should be
   designed inside the cinematics architecture (Phase 3c of that plan), not resurrected
   from this dead code.
3. **`Assets/Source/README.md` scope-hierarchy table** — add the three missing scopes:
   `HealthUILifetimeScope` (`UI/Health/`), `DangerUILifetimeScope` (`UI/Danger/`),
   `GameOverLifetimeScope` (`UI/GameOver/`).
4. **`Shared/README.md`** — add `ColorStreakTracker` to the `ScoreLevelUpMessage`
   consumer list; update the `ActorHitMessage` row to the current shape (`Actor`,
   `WorldPosition`, `ProjectileDirection`, `Outcome`, `Context` — a `DamageContext`
   carrying `Damage`).

Everything else (all feature READMEs, all 13 diagrams, the Plans registry) verified
accurate. The working-tree edits to `Balloon/Spawner/README.md` and
`Game/Cinematics/README.md` already fix two older drifts — keep them.

---

## Phase 1 — Risk-removal batch (S, one sitting)

Independent, small, pure correctness/robustness. No behaviour change in the happy path.

| # | Fix | Where | Why |
|---|---|---|---|
| 1a | Count items via `IHasItemSlot`, not `ActorAt<BalloonModel>` | `Item/ItemAssigner.cs:116` | Eligibility (`:96`) already uses the capability; the count works only while `BalloonModel` is the sole implementer. Any new balloon type with an item slot silently breaks cap accounting. |
| 1b | Cancel + dispose `BalloonSpawner._cts` on teardown | `Balloon/Spawner/BalloonSpawner.cs:27` | The generation guard covers *run reset* but not scope destruction; in-flight `SpawnLinesWithDelayAsync` delays outlive the scope and touch disposed pools/brokers. Mirror `GridSpawnerCoordinator.cs:31–35`, which does it correctly. |
| 1c | Thread a cancellation token through `ItemActivator.ActivateAsync` | `Item/ItemActivator.cs:68–77` | The `catch (OperationCanceledException)` is currently unreachable from teardown. |
| 1d | Stop publishing the live `_newlySpawnedBalloons` list in `ItemCheckMessage` | `Balloon/Spawner/BalloonSpawner.cs:160–167` | The list is `Clear()`ed right after `Publish`; safe only while every subscriber is synchronous. Copy into the message, or document the sync-only contract on the message type — copying is safer and cheap (per-turn, small list). |
| 1e | Move `WillLevelUp()` onto `IScoreQuery`; inject the interface | `Game/Cinematics/LevelUpTrailEffect.cs:40` | The seam already exists and is registered; the concrete `ScoreController` injection bypasses it. |
| 1f | Make `StaticActorSpawner.ModelFactories` / `StrategyCache` instance fields | `Slots/Actor/StaticActorSpawner.cs:20–28` | Registration pattern is right; static-ness survives domain-reload-off and hides from DI. Behaviour identical. |

Acceptance: builds green, style audit green, existing EditMode tests pass.

---

## Phase 2 — Item handler reentrancy (H4) (S–M)

**Problem:** item handlers are `Lifetime.Singleton` with two-phase mutable state
(`Setup(balloon, pos)` → `Activate()`). `LightningItemHandler`'s `OnJump` callback
(`Item/Lightning/LightningItemHandler.cs:110–129`) captures the **shared**
`_targetsBuffer` and fires per-jump for seconds after `Activate()` returns; a second
lightning activation in that window `Clear()`s and refills the list mid-chain — wrong
balloons get hit. Reachable today: two lightning balloons caught in one bomb blast.
`PaintItemHandler` has the same shape (its `OnSplash` closure captures per-activation
lists — currently *correct* only because those lists are freshly allocated per
activation, which is also why they were exempted from buffer-reuse optimization).

**Fix:** change `IBalloonItem` to a single `Activate(IBalloonModel balloon, Vector3
position)` (or an activation-context struct) and delete `Setup`. Each activation copies
what it needs into a local/pooled activation record; the singleton handler holds no
per-activation mutable state. This makes every *future* item safe by construction, and
unlocks the buffer-reuse optimization Paint was denied.

Sequence this while the item count is still five. Touches: `IBalloonItem`, all handlers
(`Bomb`, `Laser`, `Lightning`, `Paint`, + any newer), `ItemActivator`.

Acceptance: builds green; manual playtest of each item; ideally an EditMode test that
runs two overlapping lightning activations against a fake grid and asserts target sets
don't cross-contaminate.

---

## Phase 3 — Logic/GC hot paths (M)

Per the project's optimization priorities: GC hitches during action matter most. All are
localized; none touch the fragile trail-identity path.

- **3a — `PoolManagerExtensions.PlayParticle`/`PlayEffect`**
  (`Shared/Extensions/PoolManagerExtensions.cs:13–65`). Allocates `prefab.name` (fresh
  managed string per access), a factory delegate, and a completion closure **per call** —
  and this is the default balloon-pop VFX path (`BalloonView.PlayHitVfxForOutcome` →
  `:237, 263, 268`; `ProjectileShieldView.cs:132, 142`), the hottest event in the game.
  Fix: cache pool keys per prefab (static `Dictionary<Object, string>` or explicit key
  params), pre-register channels once, and have `PoolableParticle` carry its pool+key and
  return itself (cached delegate) instead of a fresh closure.
- **3b — Nudge resolver LINQ** (`Nudge/NudgeOverrideResolver.cs:64`).
  `FirstOrDefault(o => o.AppliesTo.HasFlag(source))` = closure + delegate + enumerator per
  call; ~24 calls per projectile hit via `NudgeService.OnActorHit` (`:60–73`), 60–100+
  per bomb shockwave (`:106–119`). Fix: plain `for` over the `IReadOnlyList` with
  `(o.AppliesTo & source) != 0`; add a combined `Resolve(out distance, out duration)` so
  the list is walked once per neighbor.
- **3c — Nudge tween storm** (`Balloon/View/BalloonView.cs:172–184`). Sequence + two
  Tweeners + `OnComplete` closure per nudged neighbor (~25–30 heap objects per hit);
  `Assets/Resources/DOTweenSettings.asset` has `defaultRecyclable: 0`. Fix, pick one:
  (i) `SetRecyclable(true)` on fire-and-forget tweens only — **do not** flip the global
  default: `FlyingTrail._moveTween`, `CameraShakeService._shakeTween`,
  `LevelUpTrailEffect._timeScaleTween`, `CinematicCameraRig._tween` store tween refs and
  recycled tweens make stale refs dangerous; or (ii) replace the nudge with a manual
  eased lerp (it's a simple out-and-back; `FlyingTrail.SetupFollow` already demonstrates
  the pattern).
- **3d — `PressureCascade.TryFindChain`** (`Balloon/Controller/PressureCascade.cs:52–54`).
  Fresh `Dictionary` + `HashSet` + `Queue` per call, up to Columns × spawn-lines BFS runs
  per turn — precisely during the overflow/danger crunch. Fix: reusable member/static
  collections cleared at entry (main-thread only), or `UnityEngine.Pool`
  `DictionaryPool`/`HashSetPool`.
- **3e — Low, batch opportunistically:** cache `ThrowerController.ProjectilePoolKey`
  string (`:42`); dictionary-backed `ItemConfiguration` indexer (`:14`, `First()` per
  activation); `FPSCounter.OnGUI` string/`GUIContent` caching (`:60–63` — it pollutes the
  very GC profiles used to validate this phase); `PauseService` `.Any()` → loop (`:73`).

**Explicitly out of scope:** the per-point `ScorePointMessage`/trail-spawn storm
(`ScoreController.PublishPoints`, `:248–268`) — gated behind cinematics Phase 3c, see
Phase 6a. `SlotClusterRegistry` transient collections stay deferred (flood-fill
correctness risk, medium payoff).

Acceptance: builds green; Profiler capture of a multi-pop + bomb turn shows the
`PlayParticle`/nudge allocation spikes gone (in-editor).

---

## Phase 4 — Hit pipeline restructure (H1 + H2) (M–L, the big one)

Two problems, one fix; do them together and deliberately.

**H1 — broadcast fan-out.** Every live balloon subscribes to the global
`ActorHitMessage` and filters by `ReferenceEquals`
(`Balloon/Controller/BalloonController.cs:66`, `:106–111`) → O(active balloons) delegate
invocations per hit, plus 2–3 subscription allocations per balloon spawn. Compounding:
AoE items publish per-target in loops (`Item/Bomb/BombItemHandler.cs:109`,
`Item/Lightning/LightningItemHandler.cs:81, 125`) — one bomb popping 15 balloons on an
80-balloon board ≈ 1,200 no-op filter invocations, and each pop re-enters the bus
(`TransformCapturedMessage`, `NudgeMessage`).

**H2 — implicit ordering contracts.** Three systems only work because of undocumented
synchronous dispatch order, which equals `GameLifetimeScope` registration order:
- `Projectile/Controller/ProjectileHitResolver.cs:63–67` reads `ColorStreakTracker`
  immediately after `Publish` — relies on `ScoreController` subscribing earlier.
- `Item/ItemActivator.cs:72–74` yields a frame so "all synchronous subscribers finish".
- `BalloonController.cs:75–77` hand-rolls a re-entrant-disposal dance for
  `BoardClearMessage`.
Adding one subscriber or reordering registrations can silently break the streak-shield
rule. Nothing enforces or documents the order.

**Direction:**
1. Introduce an explicit **hit pipeline** object owning turn resolution: invokes the
   order-dependent stages (score/streak → item assignment trigger → owning-balloon
   reaction) in declared order, synchronously. The MessagePipe broadcast remains for
   genuinely order-independent observers (nudge, VFX, danger, diagnostics).
2. Route the **owning-balloon reaction** directly: one subscriber (or pipeline stage)
   resolves `msg.Actor → controller` via a map keyed by model (or via `SlotGrid`) and
   calls it — balloons stop subscribing individually. This removes both the O(n) fan-out
   and the per-spawn subscription allocations.
3. Delete the three workarounds above once ordering is explicit; document the pipeline
   in `Diagrams/arch_turn_pipeline.md` and the affected READMEs (this is a
   responsibility shift — the living-docs rule applies).

Risk management: land in two commits (pipeline extraction with unchanged semantics, then
owner-routing); the streak-shield rule and item activation each need a focused playtest +
EditMode coverage where the seams allow.

Do this **before** the next wave of content (new balloon types / items) — it's the
scalability purchase everything else multiplies against.

---

## Phase 5 — Rendering fill-rate program (M–L, in-editor)

Fill rate dominates; draw calls second; standing CPU third. Each item needs Frame
Debugger / Profiler / overdraw confirmation in the editor — **measure before and after,
on device where possible**. Items are independent; ordered by expected payoff.

- **5a — Bake the blur/shadow out of the sprite shaders.** Balloon prefab renders 4
  transparent layers: Body/Knot via `SpriteShadow.shader` (softness > 0 → 9-tap branch +
  base = 10 samples/px), Shine + Specular via `SpriteBlur.shader` (9 taps each) — ~38
  taps/px across the balloon footprint × 66-slot board, every frame, blurring a *static
  image*. Bake shadow/blur into the authored sprite textures (an editor bake tool already
  exists as precedent: the bush pipeline); runtime materials become plain sprite shaders.
  Sweep the other `SpriteShadow`/`SpriteBlur`/`SpriteShine*` materials (~14, including
  `PS_Material_BalloonPop.mat` and all trail orb/ribbon materials — pop bursts and trail
  storms currently pay 10 taps/px *per particle*).
- **5b — Kill the `GrabPass`** (`Assets/Shaders/BalloonParty/Balloon/UnbreakableBalloon.shader:88`).
  Full-screen resolve per frame on tile GPUs whenever an unbreakable is visible. Replace
  the convex-mirror reflection with a prebaked reflection texture (visually
  indistinguishable on a fast 2D board) or a `shader_feature` gated off on mobile. Also
  the URP-migration blocker (see @ref plan_urp_migration).
- **5c — PuffCloud noise → texture** (`Grid/PuffCloud.shader`). Up to ~7 simplex calls
  (3 octaves each ≈ 21 octaves/px) + a per-pixel loop over `_SlotCount` on large
  transparent quads. Bake the noise field into a small tileable scrolling texture (1
  fetch), derive the lighting normal from its channels, skip the dual orig/displaced
  evaluation when local disturbance ≈ 0.
- **5d — Per-layer sorting to restore batching.** `BalloonView.ApplySortingOrder`
  (`:272–276`) + `SortingHelper.SlotBaseSortingOrder` give each balloon a unique order
  band with interleaved Knot/Body/Shine/Specular → adjacent draws almost never share a
  material ≈ 150–200 draw calls/board. Balloons on a grid barely overlap: give each
  *layer* a global order (all knots, then all bodies, …), or collapse Knot+Body(+baked
  shine) into one atlas sprite. Confirm actual batch counts in Frame Debugger first —
  batching also depends on texture/atlas sharing.
- **5e — Replace per-balloon idle Animators.** 40–66 Animators evaluate a 2-curve
  3-second bob (`StableIdle.anim`) every frame forever. Replace the stable-idle bob with
  one `ITickable` sine-driver over stable balloons; keep the Animator for the unstable
  wiggle only (or at minimum: Culling Mode = Cull Completely + disable while stable).
  Profile Animator ms first to size the win.
- **5f — Self-derived shader clocks for balloon variants.**
  `Balloon/Type/UnbreakableBalloonVariant.cs:49–70, 90–110` and
  `SoapBubbleClusterVariant.cs:55–83, 144–155` do Get/Set/SetPropertyBlock per renderer
  per frame just to advance `_TimeOffset`/`_Rotation`. Apply the fix already proven on
  the cluster cloud: push phase (and rotation *speed*) once on `Bind`, derive time
  in-shader from `_Time.y`; push `_SphereCenter` only on movement.

Shader edits cannot be validated by `dotnet build` — every item here ends with an
in-editor check and a device profile.

---

## Phase 6 — Deferred / gated

- **6a — Score-trail message aggregation** (gated on @ref plan_cinematics_architecture
  Phase 3c). `ScoreController.PublishPoints` publishes one `ScorePointMessage` per point
  (points × streak multiplier); each spawns a pooled trail + DOTween sequence + several
  closures. Scales with score inflation, not content. The cinematic currently identifies
  the tipping trail by per-point `TrailId` — the reverted attempt (`f27376f`) proved
  this coupling bites. After 3c: one message per attribution group carrying a count; UI
  fans out visuals (and can cap concurrent trails, which also serves 5a's trail-storm
  concern).
- **6b — Extract projectile motion rules from the view.**
  `Projectile/View/ProjectileView.cs:138–165` owns movement, wall-bounce, shield
  decrement, and the destroy decision — gameplay rules locked in a MonoBehaviour,
  untestable headless (the one real MVC deviation; `ProjectileHitResolver` shows the
  target pattern). Extract into an `IFixedTickable` controller or a plain bounce-rule
  object alongside `WallLimits`.
- **6c — Split `GameLifetimeScope`** (`Game/GameLifetimeScope.cs:67–189`, ~60
  registrations, 24 brokers) into per-feature `IContainerBuilder` extension methods
  (`builder.RegisterItemFeature()` …). Pure code motion; do it whenever the file next
  gets annoying.
- **Watch items (no action):** `Cinematic._listeners` is an unbounded static list relying
  on manual `Unregister`; `ThrowerController.cs:84–87` subscribes to static
  `Navigation.Current` bounded only by `Take(1)` — fine as-is, dangerous if copied
  without the `Take`. `SlotClusterRegistry` transient collections stay deferred.

---

## Suggested sequence

1. **Phase 0 + Phase 1** — one sitting each; pure risk/drift removal.
2. **Phase 2** — while the item count is small.
3. **Phase 3** — GC wins; verifiable in one profiling session.
4. **Phase 5a/5b** — the two biggest GPU wins (and 5b unblocks URP later); rest of
   Phase 5 as playtest sessions allow.
5. **Phase 4** — the structural change, before the next content wave.
6. **Phase 6a** — only after cinematics Phase 3c lands.
