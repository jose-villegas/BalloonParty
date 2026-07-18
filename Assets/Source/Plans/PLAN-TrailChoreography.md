@page plan_trail_choreography Trail Choreography

# Trail Choreography

> Replaces the fixed one-point-one-trail score pipeline with a **choreography seam**: score
> groups publish as one message, and a per-group **behaviour handler** — selected by score
> magnitude through a config table — owns everything between spawn and arrival. The default
> handler reproduces today's look byte-for-byte; a `BigScore` handler makes large awards
> spectacular *and* cheap. Resolves the constraint that reverted f27376f (the level-up
> cinematic's tipping-trail dependency) — verified against the current code, the cinematic
> needs less than that revert assumed.

---

## Orientation — start here

### The problem (measured)

One score point = one per-point score message = one pooled `FlyingTrail` = one arrival = one
notice. The streak multiplier scales the **point count** before the per-point publish loop
(`ScoreController.ResolveAttributions` → `PublishPoints`), so a 5× multiplier on a 50-point
unbreakable hit produces **250 trails, 500 tweens, 250 arrivals, 250 notices over 5 s** of
scatter stagger. José's profiler capture during such a peak: `Canvas.SendWillRenderCanvases`
/ `Canvas.BuildBatch` dominate (arrival-side UI), ~10% `DOTween` update (flight tweens).
The notice Animator→ticker rewrite (629951fe) removed the worst canvas offender; the
remaining costs all scale linearly with point count.

Trail count itself is **not** a hard limit — trails are world-space (no canvas), and
~100–150 concurrent is comfortable. What must be bounded is the *arrival* count (each one
touches UI) and the worst-case in-flight count (DOTween capacity growth past ~200 tweeners,
the 128/color pool cliff, spawn-time closure churn).

### The design in one paragraph

`ScoreController` publishes **one message per attribution entry** carrying the group's
total points. `ScoreTrailService` resolves the group's score against a config table to a
**behaviour id** (`DefaultScore`, `BigScore`, …) and delegates to that id's registered
`IScoreTrailBehaviour`. The handler spawns and animates trails however it wants, reports
arrivals through a service-owned reporter (partial `+1`s or a single `+N` — its choice, as
long as the reports sum to the group total), and nominates one **principal trail** that the
level-up cinematic can track and pause. Notices, slider, score, and level progression all
keep their existing seams — they just receive valued arrivals instead of unit ones.

---

## Verified constraints (what the current code actually requires)

These were established by reading the code on 2026-07-18 — they are the load-bearing facts
this design is built on:

1. **The per-point score message had exactly two subscribers**: `ScoreTrailService` (spawns)
   and `LevelUpCinematic` (tipping capture). No other system read the per-point stream.
2. **The cinematic does not need the exact threshold-crossing score.** It captures
   `TrailId(msg)` from the *first* message it sees while `_levelProgress.WillLevelUp()` is
   true (`LevelUpCinematic.OnScorePoint`), waits for that id in the `Flights` registry,
   then grabs the flight, calls `DisableMoveTween()` on its `FlyingTrail`, pauses it, and
   camera-follows its transform. Arrival is matched by `(ColorName, Score)` equality.
   → Any scheme works if: the id is derivable from the message, a flight registers under
   it, its motion is pausable, and one arrival report carries exactly that score.
3. **`LevelController.OnTrailArrived` is a watermark**, not a counter:
   `confirmable = Min(msg.Score, projected)` then `Max` into confirmed progress. Arrivals
   whose `Score` is the *cumulative per-color point number* confirm progress through that
   point, monotonically and out-of-order-safely. → Zero changes needed there, ever, as
   long as reported scores are the true cumulative numbers.
4. **`ScoreController.OnTrailArrived` and `ColorProgressBar.OnTrailArrived` are unit
   counters** (`++`, `slider + 1`, `Show(1)`) → they need the arrival's point value.
5. **Pooling rule**: the consumer that `Get()`s returns. Handlers own their instances.
6. `ProgressNotice.Show(int)` already scales/offsets by value — the notice UI is K>1-ready.

---

## Contracts

### Message: `ScorePointsGroupMessage` (replaces the per-point score message)

```csharp
internal readonly struct ScorePointsGroupMessage
{
    public readonly string ColorName;
    public readonly Vector3 WorldPosition;   // pop origin (scatter/burst center)
    public readonly int Points;              // total points this group carries (post-multiplier, post-cap)
    public readonly int LastScore;           // cumulative per-color number of the group's LAST point
    public readonly int Multiplier;          // streak multiplier that produced Points (discrimination input)
    public readonly Vector3 HitDirection;    // shot direction at impact; BigScore rolls its shapes about it
}
```

- Published by `ScoreController.PublishPoints` — one per resolved attribution entry
  (multi-color pops publish one per color, as today's groups already do).
- `LastScore = baseProgress + granted` — the same numbering the watermark consumes.
- `TrailId` for the group = `(ColorName, LastScore)`; uniqueness holds because per-color
  numbering is strictly increasing within a level.
- `GroupSize`/`GroupIndex` (today's scatter-fan inputs) move INTO the handler's domain —
  the handler decides how many visual objects exist, so the fan parameters are its own.

### Arrival: `ScoreTrailArrivedMessage` gains `Points`

```csharp
public readonly struct ScoreTrailArrivedMessage
{
    public readonly string ColorName;
    public readonly int Score;      // cumulative number of the LAST point this arrival confirms
    public readonly int Points;     // how many points land with this arrival (was implicitly 1)
    public readonly Vector3 WorldPosition;
}
```

Consumer changes: `ScoreController` `+= Points` (persistent + total), `ColorProgressBar`
slider `+= Points` and `Show(Points)`. `LevelController` unchanged (watermark reads
`Score`). `LevelUpCinematic` unchanged (matches `Score` equality).

### The seam: `IScoreTrailBehaviour`

```csharp
internal interface IScoreTrailBehaviour
{
    /// Owns the group from spawn to final arrival. MUST: register exactly one principal
    /// flight under context.TrailId before returning (the cinematic may pause/track it);
    /// report arrivals via context.Reporter with true cumulative Score values summing to
    /// context.Points; return every pooled instance it spawned.
    void Begin(in ScoreTrailContext context);
}

internal readonly struct ScoreTrailContext
{
    public readonly string ColorName;
    public readonly Color Color;              // palette-resolved
    public readonly Vector3 Origin;
    public readonly int Points;
    public readonly int LastScore;
    public readonly TrailId TrailId;          // (ColorName, LastScore) — the principal's id
    public readonly ITrailEndpoint Target;
    public readonly TrailSpawner Spawner;     // the color's pooled trail channel
    public readonly IScoreTrailReporter Reporter;
}

internal interface IScoreTrailReporter
{
    /// Publishes one ScoreTrailArrivedMessage(color, score, points, at). The service
    /// asserts (dev builds) that reports per group never overshoot and finally sum to
    /// the group total. Flight (un)registration stays fully handler-owned — the reporter
    /// does NOT touch Flights (step 2: DefaultScore registers/unregisters every trail
    /// itself, exactly as the pre-seam inline code did, for parity).
    void ReportArrival(int score, int points, Vector3 at);
}
```

- Handlers are **plain C#**, registered in VContainer keyed by `ScoreTrailBehaviourId`
  (enum). Stateless per invocation — per-group state lives in a small pooled state object
  or in the closures/tweens of the flight itself (two `BigScore` groups can be in flight
  concurrently).
- The **principal-flight rule** exists solely for the cinematic (constraint 2). The
  service provides `RegisterPrincipal(Transform)` on the context or reporter so the
  registry bookkeeping stays in one place.

### Discrimination: `ScoreTrailBehaviourConfig` (SO, on the existing settings pattern)

```
Entries (evaluated highest-first):
  { Id: BigScore,     MinPoints: 40,  ...visual knobs (tier thresholds, visual cap, merge window) }
  { Id: DefaultScore, MinPoints: 0 }
```

Resolver: highest `MinPoints <= group.Points` wins. **Decided (José, 2026-07-18):
discrimination is by group total ("big because cluster"), not by multiplier** — the
`Multiplier` field stays in the message purely as future data. Injected as a read-only
interface per repo convention.

#### Value/visual decoupling — why there is no "dividend"

The reporter contract separates value (what arrivals deliver) from visuals (what flies).
In `BigScore`, the carrier reports the ENTIRE group value in one arrival; tributaries are
worth zero — their count is a density choice (`VisualCap`), never arithmetic. Nothing
divides, so no remainder exists and handlers never need an internal default path.

If mixed representation is ever wanted (e.g. 47 points as a "+40" carrier plus 7 ordinary
trails), the split belongs in the RESOLVER, not inside a handler: it partitions the group
into sub-groups over contiguous score ranges and routes each to its own handler
(`base+1..base+7 → DefaultScore`, `base+8..base+47 → BigScore`). The reporter invariant
(true cumulative scores, reports sum to the total) is already partition-shaped, the watermark
doesn't care who reports what, and the principal rule stays unambiguous: the sub-group
containing `LastScore` owns the principal — so the big chunk always takes the top of the
range. Handlers stay single-purpose; composition has exactly one home. Not built in v1 —
this is the designed extension point.

---

## Handlers

### `DefaultScore` — byte-for-byte parity with today

Spawns `Points` trails exactly as `ScoreTrailService` does now: scatter-fan origins
(`2π·i/Points` at 1.5× slot separation), 0.02 s stagger, `SpawnBurst` bloom + trace, each
trail reporting `(baseScore + i + 1, points: 1)` on landing. The FIRST trail is the
principal (id score = `FirstScore`) — it spawns immediately, so its registration is
timeout-safe under scatter stagger.

Principal selection is handler-specific, not a fixed `LastScore` rule. DefaultScore staggers
its spawns, so its principal must be the FIRST trail: the group's last trail can spawn seconds
later and would race `WaitForTippingTrailAsync`'s bounded wait. Only a handler whose principal
spawns immediately (BigScore's carrier) can nominate `LastScore`. **Step-2 contract detail:**
the cinematic must derive the tipping id the same way the handler nominates its principal — the
context should carry the principal's id, chosen by the resolver/handler pairing, rather than
hardcoding `LastScore`.

One accepted micro-delta from today, invisible in play:
- Only the principal registers in `Flights` (today every trail registers). The registry's
  only consumers are the cinematic (principal only) and `CompleteAll` (loss/level paths) —
  handlers must also expose in-flight cleanup for `CompleteAll`; the service keeps a list
  of live group states for that.

### `BigScore` — the first real implementer (confluence)

> *Historical — the carrier/tributary/merge-flash model below is superseded by the anchored
> simulation-space design in the next subsection (no carrier trail, no flash; a bare anchor
> Transform is the center and the collapsed cluster itself flies to the bar). Kept for the
> worst-case sizing and the value/visual-decoupling rationale.*

One **carrier** trail carries the whole group value to the bar; up to `VisualCap`
(~16–24) **tributaries** provide the spectacle: they bloom outward on the existing burst
path, then converge onto the moving carrier via the existing `FlyingTrail.SetupFollow`
(`Func<Vector3>` target = carrier transform) and are absorbed — flash, return to pool,
carrier grows a tier. Tributaries are plain sprite motes (no TrailRenderer) — at ~0.3 s
lifetimes the trail ribbon reads as noise anyway. The carrier is the principal; it reports
once: `(LastScore, Points)` → one "+N" notice, one slider step, one score bump.

Carrier **tiers** (config): value thresholds → scale/glow/gradient escalation, evaluated
as absorbed value accumulates — "the carrier crossing a tier mid-flight" is the hook for
score-milestone animation ideas.

Worst case rerun (5× multiplier × 50-point unbreakable): today 250 trails / 500 tweens /
250 arrivals / 5 s tail → **1 carrier + ≤24 tributaries / ~50 tweens / 1 arrival / ~1.2 s**.

### `BigScore` shape choreography — anchored simulation space (José, 2026-07-18)

> *Superseded 2026-07-19 by the **3D shape catalog + score decomposition** model below. The
> anchored simulation-space core (bare pooled anchor Transform as the frame centre + principal, the
> collapsed cluster flying to the bar, `TransformRibbon` rigidity, the transport bridge) all carried
> forward; what changed is the geometry (a hand-authored 3D catalog, not a single {n/k} star with
> nesting), the drawing model (pens orbit closed walks forever, not one chord sweep), the phase
> machine (one curve-driven Travel phase, not deploy/draw/collapse/final-flight), and score handling
> (a group **decomposes** into many simultaneous shapes rather than one tier). Kept for the
> derivation of the anchor/ribbon/transport machinery.*

The spectacle is a drawn **star polygon {n/k}**, using the trails' ribbons as the pen
(reference: the classic nested-pentagram construction — golden-ratio self-similarity).
Tiers map score → vertex count: triangle {3/1}, square {4/1}, pentagram **{5/2}**
("next+1" vertex skipping), and richer stars above ({6/1}, {7/2}…).

**There is no central carrier trail.** The shape-system space has a *center* — the
spawn-axis origin, carried by a bare pooled **anchor Transform**, not a trail — and the
simulation is pure math: n vertices in a local frame that a translate/rotate/scale is
applied to. We move/rotate/scale that frame; the vertex trails only read the resulting
world positions and move between them. The anchor's transform is what the registry and
cinematic track/pause/complete; nothing visible rides it.

World vertex: `C(t) + R(Ω(t)) · (r(t) · dir(φᵢ + repRotation))` — the path rotation Ω
folds into the angle (`R(Ω)·dir(φ) = dir(φ + Ω)`).

Three phases per repetition, all closed-form:
1. **Deploy** — n trails fly from the pop origin to the vertices of a regular n-gon:
   `vᵢ = C + r·dir(φᵢ)`, `φᵢ = θ₀ + repRotation + Ω + 2πi/n`.
2. **Draw** — each trail traverses ONE chord simultaneously: `vᵢ → v₍ᵢ₊ₖ₎`. For
   gcd(n,k)=1 the n concurrent chords complete the whole star in a single sweep — no
   sequential pen-tracing.
3. **Collapse** — a pure radial `r(t) → 0` *inside the rotating frame*; every trail pulls
   inward, ribbons drawing the collapse, meeting at C.

**Ω (path rotation)** is 0 until the FIRST Draw completes, then advances at the tier's
`RotationSpeed` (formation clock — scaled `dt × Flight.Speed`) for the rest of the
formation, INCLUDING the final flight. It is the one rotation concept: the collapse has
no separate spin — it is the radial pull inside the frame Ω rotates.

**Nesting**: after a collapse, redeploy to `r·s` with `θ₀ += nestRotation` and repeat,
`m` times. Defaults follow the pentagram's own geometry: `s = 1/φ² ≈ 0.382`,
`nestRotation = π/n` — successive stars land exactly where the natural nesting puts
them. Per-repetition phase durations scale with radius (smaller stars draw faster — an
accelerating crescendo into the final collapse).

**Final flight (no flash, no carrier)**: after the last collapse the collapsed cluster
(all n vertices at `r≈0`, pen ON — they read as one bright comet) flies *with the center*
to the bar over `CarrierFlightDuration`, Ω still advancing (the point cluster spinning is
invisible). The endpoint is sampled **fresh** at flight start (`Target.RandomPosition()`,
z zeroed onto the formation plane); the launch-time drift sample is used ONLY as the drift
direction while drawing — reusing it here is what made the comet hit *below* the moving UI
bar. The final leg is smoothstepped so the handoff from drift is continuous. On arrival:
report `(LastScore, Points)` at the fresh target, release the vertices + anchor, unregister.

**Rigid in formation space**: ribbons record WORLD positions, so each tick — if the center
moved or Ω changed — every live vertex ribbon is re-framed by the same delta transform,
`p' = C_new + R(ΔΩ)·(p − C_old)` (`FlyingTrail.TransformRibbon`; pure translation is the
`ΔΩ == 0` fast path). This carries the drawn ink through the same translate+rotate the live
frame moved through, keeping the whole figure rigid while it glides and spins.

Implementation decisions:
- **Ticker, not tweens**: motion is analytic, so a formation ticker (`ILateTickable`,
  the `BalloonMotionTicker` pattern) evaluates `vᵢ(t)` directly — zero waypoint
  allocation, exact spirals; `DOPath` would approximate what we can compute.
- **Ribbon persistence is the aesthetic**: `TrailRenderer.time` must cover the whole
  repetition sequence so outer stars remain visible while inner ones draw (the nested
  look). Per-tier knob, restored on `OnDespawned`. `minVertexDistance` is the
  smoothness/vertex-budget knob.
- **Anchor pool**: `ShapeFormationTicker` owns a small `Stack<Transform>` of bare
  `ShapeFormationAnchor` GameObjects, activated per formation and deactivated on release,
  never destroyed — zero per-frame allocation.
- **Anchor placement**: centered on the pop origin, `r` scaled by tier and clamped inside
  `WallLimits` so a corner pop doesn't draw off-screen.

Per-tier config row: `n`, `k`, `repeats m`, `nestScale s` (default 1/φ²),
`nestRotation` (default π/n), `baseRadius`, per-phase durations, `ribbonTime`,
`rotationSpeed ω` (path spin once formed — nonzero for every tier), `driftToTarget`. The
triangle is just `{3/1}, m = 1` — one implementation covers every tier.

> **Transport-bridge contract (reworked 2026-07-18).** The formation's pause/snap/slow-mo interface is the
> **anchor's** `TrailFlight` handle, polled by `ShapeFormationTicker` every tick — this is *the* seam between
> the analytic formation and the cinematic/loss/reset machinery:
> - `Phase == Paused` → the cinematic froze the principal (it `Pause()`d the anchor flight in
>   `BeginCinematicWithTrail`); the ticker stops advancing the clock **and stops writing the anchor
>   transform**, so it never fights the cinematic's `AdvanceTrackedTrail` writes. Vertices hold position.
> - `Phase == Idle` → the pan-in completed the flight, or a `CompleteAll` (level-up/loss) did; the ticker
>   **snaps** in that tick: reports the whole `(LastScore, Points)` at the anchor's current position, then
>   fade-releases the live vertices (unscaled scale-out) and releases the anchor. A `Reported` guard + the
>   reporter's backstop prevent a double-fire against the final-flight arrival.
> - `flight.Speed` multiplies the formation `dt` (slow-mo). The formation clock is scaled time; the snap fade
>   is unscaled (freeze-safe). The anchor has no `FlyingTrail`, so the cinematic's `DisableMoveTween` call is
>   null-conditional — only tween-driven `DefaultScore` principals need it.
>
> **Design notes:**
> - Vertices are **full `FlyingTrail` trails with their `TrailRenderer` ribbon as the pen** — the ribbon *is*
>   how the star is drawn, so a per-tier `ribbonTime` (restored on `OnDespawned`) is essential.
> - There is no `VisualCap`/tier-glow escalation. A **tier is the star geometry** picked by group total
>   (`BigScoreTiers`, highest `MinPoints` cleared); vertex count = the tier's `n`, capped at a hard
>   `MaxVertexCount = 8`.
> - The snap on `CompleteAll` reports **in the ticker tick that observes `Idle`**, not synchronously inside
>   `CompleteAll`: the anchor has no tween during the formation, so `CompleteAll`'s `Complete()` cannot fire a
>   report itself. The one-frame settle is invisible — the level-up was already decided by `WillLevelUp()`
>   (projected), and the watermark confirm is order-safe bookkeeping.
> - `ShapeFormationTicker` owns **both** pool acquire and release for the anchor and vertices (single owner),
>   registering/unregistering the anchor flight itself; `BigScoreTrailBehaviour` only picks the tier, anchors
>   the centre, and calls `Launch`. Keeps the "consumer that `Get()`s returns" rule unambiguous.

> **Pause-through-ceremony (2026-07-19).** The level-up no longer `CompleteAll`s the survivors at the pan-in
> end — clearing the drawn constellations mid-flight read as harsh. Instead they are **frozen through the
> popup and resolved as outgoing-level content**. `LevelUpCinematic` calls `Flights.PauseAll()` the moment
> `ILevelProgress.Phase` becomes `Pending` (not at `BeginCinematic`: the non-tracked trails must keep flying
> until then so their arrivals *confirm* the level-up — freezing them earlier, with `CompleteAll` now gone
> from the pan-in end, would strand the popup unconfirmed and soft-lock, worst for a small `DefaultScore`
> final pop). Each frozen formation freezes + inflates its ribbons (`Paused` branch); a plain `DefaultScore`
> orb does the same via `TrailFlight.Pause → FlyingTrail.FreezeRibbon`. Resolution happens at the transition
> seam: `ScoreTrailService` implements `ITransitionOutgoingContent`, and `HoldOutgoing` (called mid-Ascent
> while the phase is still `Transitioning`) `CompleteAll`s them — arrivals bank their points (never banked
> while frozen, so total score stays correct) and the formations snap-fade under the descent that covers
> their exit. **Numbering invariant:** because this runs before `LevelTransitionCompletedMessage` flips the
> phase back to `Playing`, the arrivals (carrying the finished level's cumulative score) land in a
> non-`Playing` phase and are ignored by `LevelController`/`ColorProgressBar`, so an old-level survivor can
> never step the new bar's slider or watermark. Abort/loss (`AbortSession`) keeps the immediate `CompleteAll`.

### `BigScore` — 3D shape catalog + score decomposition (José, 2026-07-19)

**Denomination = vertex count, full 1:1 decomposition.** A shape's score value IS its vertex
count; every point is one orbiting pen trail. A group's total decomposes **greedily largest-first**
over the catalog ladder `{30, 20, 10, 8, 6, 5, 4, 3, 2}` (`ShapeCatalog.Denominations`), remainders
recurse, and a terminal remainder of 1 becomes one classic default-style trail. `13 = 10-sphere +
triangle`; `250 = 8×30-sphere + 10-sphere`; `7 = triangular-prism + 1`. Because 2 and 3 are both
denominations the remainder after the pass is always 0 or 1. There is **no visual cap** — trail
count scales with score by design (ticker-driven, zero tweens, one arrival per formation, so the old
per-arrival costs stay dead). Threshold `MinPoints` drops to **7** in the asset (2–6-point pops keep
classic trails).

> **12 is dropped from the ladder.** The design lists spheres at 10/12/20/30, but greedy over a
> 12-inclusive ladder splits `13` as `12+1`, contradicting the required `13 = 10+3` (and `7 = 6+1`
> pins pure greedy, so no single rule reconciles both once 12 is present). Spheres are 10/20/30 —
> three increasing accuracies. Re-add 12 to the ladder + catalog only if `13 = 12+1` becomes
> acceptable. `Decompose` is an internal static pure function on `BigScoreTrailBehaviour` (tested).

**The catalog (`ShapeCatalog`, `internal static`, built once, zero-alloc lookup).** Each shape is
local 3D vertices (normalized to unit bounding radius), a set of **closed walks** its pens orbit,
and a `RadiusScale`:
- `2` = line (a single edge as a back-and-forth shuttle), `3` = triangle (one 3-cycle), `4` =
  tetrahedron (4-cycle + two diagonal shuttles), `5` = square pyramid (a 5-cycle through the apex +
  3 shuttles), `6` = triangular prism (two triangle loops + 3 vertical shuttles along the wide
  axis), `8` = cube (a Hamiltonian 8-cycle + 4 shuttles).
- `10/20/30` = spheres as **latitude rings**: `10` = 2 rings of 5; `20` = 3 rings 6/8/6; `30` = 4
  rings 6/8/8/6 + 2 pole shuttles (meridian arcs, a hint of longitude). Ring segments are **arcs**
  (slerp); polyhedron edges are **chords** (lerp).

**Drawing = perpetual orbiting loops.** Pens are distributed across a shape's walks proportionally
to walk length (`PensPerWalk`, largest-remainder, summing to the denomination), spaced evenly along
each walk by **arc length**, and orbit continuously: the first lap draws the wireframe, later laps
re-ink it; `k` pens sharing a period-`P` walk cover it in `P/k`. Pen travel speed is authored in
**world units/second** (`PenSpeed`, the primary style knob) so ink density reads the same across
shape sizes; a **coverage** style dial (Range 0.2–2) sets each pen's ribbon time
`= (worldPerimeter / pensOnWalk / penSpeed) × coverage` at scale 1 — `≥1` solid wireframe, `<1`
chasing comet heads, `≪1` orbiting pearls.

**One curve-driven Travel phase** (the repo's "curve's last key is the duration" idiom, per
`TrackedTrailSettings`/PLAN-CinematicsArchitecture). A single `ScaleOverTravel` `AnimationCurve` on
the settings gives normalized shape scale over seconds and its last key time is the formation
duration `D`. The world position of a pen is `C(t) + Q(t)·(radius · scale(t) · localₚ(t))`:
- `C(t) = Lerp(origin, liveTarget, SmoothStep(t/D))` — the shape blooms at its sub-centre and
  travels to the bar. **Live target tracking** replaces all sampling policy: the random landing
  offset is sampled once at launch, then `liveTarget = Target.Center + offset` re-read every tick,
  so a drifting UI bar can never leave the landing stale (the `SetupFollow` principle).
- `scale(t)` — the curve: `0 → bloom → hold → 0`, so the shape grows from a point and tapers back to
  one at the bar. **Pens are pen-down from t = 0** (no deploy phase, no deploy spokes — the shape
  blooms from a point).
- `Q(t)` — a fixed random tilt spun from t = 0 (invisible while the shape is a point). The tumble
  **axis derives from the projectile hit direction** — `Cross(Vector3.back, hitDirection).normalized`
  — so the whole constellation rolls head-over-heels along the shot like momentum from the impact
  (all formations of a pop share the axis; a near-zero direction, e.g. item/laser/board pops, falls
  back to a per-shape random axis). Spin speed stays a global settings knob.

`TransformRibbon` re-frames the drawn ink by the tick's translate+tumble delta (now a
**`Quaternion`**), keeping the figure rigid in formation space. It is deliberately **not**
scale-corrected: the scale change is slow next to the loop speed and pens continuously re-ink at the
current scale, so slightly-larger old ink fading behind the shrinking shape reads as a natural
afterglow.

**Decomposition + simultaneity.** `BigScoreTrailBehaviour.Begin` decomposes `context.Points`, then
launches ALL resulting formations at once at spread sub-centres (golden-angle phyllotaxis around the
pop, each wall-clamped). The **largest** formation takes the **top** contiguous score range (so it
carries `LastScore` and is the principal — `GetPrincipalId` unchanged) and its sub-centre is the
pop; the rest take descending ranges. Each formation reports `(itsRangeLast, itsValue)` on landing;
a terminal remainder of 1 spawns one classic `context.Spawner.Spawn` trail reporting `(itsScore, 1)`.
Reports sum to the total by construction. Only the **principal's** anchor registers in `Flights`
(the cinematic tracks one); all formations of the group share that flight for the transport bridge,
so a pause/`Idle` fans out to every shape. The flight stays registered through the group's whole life
and is unregistered only once the last formation finishes, so a principal that lands first never
falsely snaps the others. `MaxVertexCount = 30`; per-formation state (30-slot pen arrays), groups,
and anchors are all pooled — zero per-frame allocation.

### Degenerate configs worth knowing exist

- *Formation flight* (keep N full trails, single valued arrival) = `BigScore` with
  `VisualCap = Points` and merge disabled — a fallback look if confluence doesn't land.
- Pure orb coalescing (earlier design discussion) = `BigScore` with `VisualCap` orbs and
  no carrier — strictly less spectacle, kept only for reference.

---

## What this deliberately does NOT change

- Streak notices (`StreakChangedMessage` path), the level-up ceremony/glow-trail drain,
  heart/shield trails (`HeartTrailController`, `ShieldTrailController` — separate
  spawner uses, untouched).
- `LevelController` progression semantics (watermark — constraint 3).
- `ProgressNotice`/`ProgressNoticePresenter` (the ticker rewrite stands; `Show(Points)` is
  already supported).
- The `TrailId`-based cinematic contract — it is *satisfied*, not redesigned.

---

## Implementation order (each step compiles + is committable)

1. **Message swap behind parity**: `ScorePointsGroupMessage` + valued
   `ScoreTrailArrivedMessage`; `ScoreController` publishes groups; `ScoreTrailService`
   inlines what becomes `DefaultScore` (no seam yet); cinematic subscription retargeted
   (`TrailId(msg)` from the group message); `+= Points` consumers. **Playtest gate:
   visually indistinguishable from today.**
2. **Extract the seam** ✅ *(landed — pure refactor, awaiting in-editor parity playtest)*:
   `IScoreTrailBehaviour` + `ScoreTrailContext` + `IScoreTrailReporter` + `ScoreTrailBehaviourConfiguration`
   + `ScoreTrailBehaviourResolver`; `DefaultScore` becomes the first handler. `ScoreTrailService` slims to
   endpoint/colour lookup → context → `resolver.Resolve(points).Begin(context)`, and `LevelUpCinematic`
   derives its tipping id from `resolver.PrincipalIdFor(msg)` so it can't diverge from what the handler
   registers. One deliberate divergence beyond parity: the service now implements `IRunResettable` and
   cancels+recreates a **group-scoped** `CancellationTokenSource` on run reset, so a group's spawn loop can
   no longer bleed trails into the next run (a pre-existing hole — the service `_cts` only cancelled on
   `Dispose`; prewarm still rides that lifetime `_cts`). Level-up does **not** cancel it — the ceremony
   relies on in-flight trails continuing. Config binding is a serialized field on `GameLifetimeScope` (scene
   inspector), so the resolver degrades to `DefaultScore` (one-time dev warning) when the asset is unwired.
   Second parity gate.
3. **`BigScore`** ✅ *(landed 2026-07-18 as star tiers; reworked 2026-07-19 to the 3D shape catalog +
   score decomposition model — awaiting in-editor playtest)*: `BigScoreTrailBehaviour` +
   `ShapeFormationTicker` + `ShapeCatalog`. A group's score **decomposes** over the catalog ladder into many
   3D shapes launched simultaneously at spread sub-centres; each shape's pens **orbit closed walks** through
   one **curve-driven Travel phase** (bloom → hold → taper to a point at the bar), tumbling about the
   **projectile hit direction**. `BigScoreFormationSettings` (a single global block: `BaseRadius`,
   `ScaleOverTravel` curve, `PenSpeed`, `Coverage`, `SpinSpeed`) replaces the tier table;
   `TrailSpawner.Acquire/Release`, `FlyingTrail` ribbon-time set/restore + quaternion `TransformRibbon`.
   Threshold `MinPoints 7`. See the 3D-catalog subsection + the transport-bridge contract note above.
   Playtest for feel; iterate knobs in-editor.
4. **`ScoreTrailPrewarmPerColor` stays at 128 — the step-4 prewarm reduction is CANCELLED.** Full 1:1
   decomposition keeps the peak trail demand (a 250-point pop is ~250 concurrent pens on one colour), so the
   worst case is not bounded the way the carrier/tributary design would have bounded it; the prewarm must
   still cover a large burst. Revisit the 0.02 s stagger for the default path only.

Verification: step 1/2 gates are in-editor playtests (pop bursts, a level-up mid-storm to
exercise the tipping path, a loss mid-storm for `CompleteAll`); step 3 adds the 5×
unbreakable profiler capture compared against José's pre-change capture
(`Canvas.SendWillRenderCanvases`, DOTween update, GC alloc). `dotnet build` + edit-mode
tests per commit as usual.

---

## Open questions for review

1. ~~**Discrimination inputs**~~ — RESOLVED: by group total ("big because cluster"); see
   the value/visual-decoupling subsection for why this creates no dividend/remainder
   problem and where a mixed-representation split would live if ever wanted.
2. ~~**Score label rhythm**~~ — RESOLVED: +N chunks. The notice shows the group value
   ("+100"), the total score steps by N per carrier arrival (zero-alloc `SetThousands`
   path either way). Two authoring follow-ups this creates: (a) the notice's scale /
   X-offset curves are keyed only to value 10 today — values beyond clamp to the last
   key, so re-author the curves for the BigScore value range during step-3 playtest;
   (b) decide the label format — today it renders the bare number, "+" prefix is a small
   formatting addition alongside the value change.
3. ~~**Tier authoring**~~ — RESOLVED: plain thresholds on the `BigScore` config entry.
   If other systems (popups, audio stingers) later want the same milestones, promote the
   thresholds to a shared asset then — not before (authoring affordances over
   infrastructure).
4. ~~**`CompleteAll` shape**~~ — RESOLVED: logic snaps, visuals fade. On `CancelAll`,
   every live group SYNCHRONOUSLY reports its remaining points (score/watermark settle
   immediately — the ceremony and loss flows depend on that), then each live trail plays
   a short fire-and-forget fade (unscaled — the level-up freeze zeroes timeScale) and
   returns to the pool at fade end. Cost is bounded by concurrent trails at cancellation
   (≤ ~25 under BigScore), one short tween each, once. `OnDespawned`'s tween-kill covers
   a force-return racing the fade.
