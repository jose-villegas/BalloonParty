@page plan_nudge_layered_motion Nudge Layered Motion Refactor

# Nudge Layered Motion — impulse stack over a reconciled base

> Replaces the guard-based nudge ↔ balance coordination (see
> @ref plan_nudge_balance_interaction) with additive, stackable nudge impulses
> applied on top of a base position owned by the balance/spawn systems. The two
> motion systems become orthogonal: they never fight, never pause each other,
> and never exchange callbacks.

---

## 1. Motivation

The post-mortem in PLAN-NudgeBalanceInteraction documents 11 fix iterations, all
sharing one root cause: `BalloonMotionTicker` writes `transform.position`
**absolutely**. An absolute writer must know where the balloon *should* be, so
the slot position leaked into the nudge system — and for unstable actors
(mid-balance, mid-spawn) that answer is ambiguous. Every guard, pause, deferral,
and inheritance rule exists to paper over that ambiguity. The residual bug class
(rare teleports/stuck balloons under cascading pops) is unreachable by more
guards because the guards *are* the problem.

### Independent investigation findings

A fresh read of the post-fix code (ticker, view, balancer, service, tracker,
factory) confirms the post-mortem's fragility analysis and adds these findings:

1. **The writer asymmetry is the bug factory.** The spawn DOPath tweens the
   transform *directly* (killable by `transform.DOKill()`, invisible to
   `TweenTracker.IsPlaying`), while the balance DOPath lives *inside* a
   `TweenTracker` Sequence (immune to `transform.DOKill()`, invisible to
   `DOTween.IsTweening(transform)`), and the nudge lerp is ticker-driven
   (invisible to both). Three writers, three different kill/detect mechanisms,
   each blind to the other two — which is exactly why each fix iteration
   exposed the next bug instead of closing the class.
2. **`IsStable` is three flags in one.** It gates the animator's idle bob,
   admits/rejects nudges, and bookkeeps balance/spawn completion. Only the
   nudge-admission role causes fights; deleting it leaves the flag with a
   single owner (base motion) and a single meaning.
3. **Hot-path logging.** Every hit/nudge produces interpolated-string
   `Debug.Log` calls ([Nudge] tags) — a shockwave logs dozens per frame. On the
   120 Hz Android target this is GC-hitch territory independent of the bug
   itself; it exits with the refactor.
4. **The timing hazard is structural.** Nudges start in `FixedUpdate` (message
   handlers), the balancer cancels them in `Update`, deferred balances fire a
   frame later, and `ITickable` vs DOTween ordering is undefined — some nudges
   lived for zero rendered frames. The `ILateTickable` applier resolves this by
   fiat: it always runs after every base writer in the frame.
5. **Genuine fixes worth preserving.** Fix 2c (deflect physics uses the view's
   position for the reflection normal, the slot only for nudge direction) is
   correct independently of the orchestration mess and is kept. Fix 2b's
   path-pool snapshot becomes moot — with no deferral there is no closure to
   capture a pooled list.
6. **Alternatives considered and rejected** for the offset application:
   - *Child "nudge pivot" transform* — cleanest ownership on paper, but
     inserting a node above the visuals breaks Animator clip paths on rebind,
     `_swayPivot` deliberately excludes the baked-specular sprites (they must
     translate with a positional nudge even though they must not *rotate* with
     the sway), and it means prefab surgery across every balloon variant.
   - *Subtract-offset-before-Update / re-add-after* — restores a clean base for
     other writers each frame but depends on running before DOTween's update
     inside the same phase; VContainer's PlayerLoop hooks vs DOTween's update
     order is exactly the kind of implicit contract this refactor removes.
   - *Post-mortem §6.1/6.2/6.4/6.5* (state machine, home position, callback
     queue, ownership token) — all formalize coordination between the two
     systems. Layering removes the coordination itself, so there is nothing
     left for them to formalize.

### Rebuild base

The refactor is built on `6caa776d` (the last commit before the guard-fix
attempts), not on top of them — the guard iterations are preserved for
reference in branch `backup/nudge-guard-fixes`. Unrelated changes that rode
the fix commits (buff-configuration prefab wiring, pacing/scene tuning, the
`RainbowBalloonVariant` destroyed-object guard) are carried over.

### Design principles

1. **A nudge is a pure impulse.** \f$offset(t) = direction \times distance \times reach(t)\f$
   where \f$reach(0) = reach(1) = 0\f$. It carries no slot position, no return
   position, no completion callback. Return-to-origin is guaranteed by
   construction; drift is mathematically impossible.
2. **Impulses stack.** A balloon hit twice in quick succession runs two
   concurrent impulses whose offsets sum — it visibly bounces out, gets hit
   mid-return, and bounces again from wherever it is. This is the desired
   behavior, not a conflict to guard against.
3. **Base position has exactly one meaning.** Wherever the *rest of the game*
   put the balloon: spawn path tween, balance DOPath, pool teleport, idle. The
   nudge system reads it, never writes it.
4. **Single writer, last in the frame.** One `ILateTickable` applies
   \f$base + \sum impulses\f$ after all Update-phase base writers (DOTween Normal
   update, factory writes, balancer). Nothing else runs later, so there is no
   ordering fight to coordinate.

---

## 2. Architecture

### 2.1 The impulse model

```
reach(p) = p < 0.5 ? EaseOutQuad(p × 2)            // outbound half
                   : 1 − EaseOutQuad((p − 0.5) × 2) // return half
offset_i = Direction_i × Distance_i × reach(Elapsed_i / Duration_i)
totalOffset(view) = Σ offset_i over the view's active impulses
```

The reach curve is identical to today's (no visual retune needed). Each impulse
advances by `Time.deltaTime` and removes itself at \f$Elapsed \ge Duration\f$, at
which point its contribution is exactly zero.

### 2.2 Base reconciliation

The applier cannot know *when* another system moves the balloon, so it detects
it: it remembers the final position it wrote last frame (`LastWritten`). On the
next late tick:

```
if (view.Position ≉ LastWritten)   // someone else wrote the transform
    BasePosition = view.Position;  // adopt their value as the new base
view.Position = BasePosition + totalOffset;
LastWritten = view.Position;
```

- Comparison uses a small epsilon (\f$sqrMagnitude < 10^{-12}\f$) — guards against
  world↔local float round-trips, while real writes (DOTween moves ≥ µm/frame,
  pool teleports) always exceed it.
- A balance DOPath writes every frame → its value is adopted every frame → the
  balloon slides along the path *plus* the decaying bounce, and lands exactly
  on the slot because the impulse ends at zero.
- When a view's last impulse completes, the applier writes pure `BasePosition`
  once and releases the view's state back to the pool — zero steady-state cost
  and no lingering writes.

### 2.3 Ownership map (after)

| System | Writes | When |
|---|---|---|
| BalloonFactory / pool | `transform.position` (base) | Spawn teleport + spawn DOPath |
| BalloonBalancer / TweenTracker | `transform.position` (base) | Balance DOPath, Update phase |
| BalloonMotionTicker | `transform.position` (base + offsets) | `ILateTickable`, only while impulses are active |
| Everything else | — | never |

`IsStable` now means exactly one thing: *no base motion in progress*
(spawn/balance). Nudges no longer touch it. Behavioral note: the animator's
`IsStable` bool no longer flips during a nudge, so the idle bob keeps playing
under the bounce — verify in editor that this composes acceptably.

### 2.4 What gets deleted

| Mechanism | Why it existed | Why it can go |
|---|---|---|
| Stability guard in `BalloonView.Nudge` | ticker vs DOPath fight | systems are orthogonal now |
| Tracker-playing guard | same | same |
| `TweenTracker.Pause/Resume` + `_tweenPausedForNudge` | deflect mid-balance | balance keeps playing under the bounce |
| Balance deferral + path snapshot in `AnimatePaths` | nudge had to finish first | both run concurrently |
| `onComplete` through `INudgeable`/ticker + inheritance | deferred balance callback | no deferral exists |
| Return-position inheritance + blend formula | drift correction | offsets end at zero by construction |
| `_isNudging`, `IsNudging`, `OnNudgeCancelled` | boolean state machine | no cross-system state to track |
| `DOScale` compensation in `Nudge()` | nudge killed the spawn scale tween via `DOKill` | nudge kills nothing |
| Nudge writes to `IsStable` | shared "motion in progress" flag | nudge is not base motion |
| All `[Nudge]` `Debug.Log` calls | debugging the old system | hot-path logging; gone with the bugs |

---

## 3. Tasks

### Task 1 — Core refactor (implementation agent)

**`Balloon/Controller/BalloonMotionTicker.cs` — rewrite.**

- `ITickable` → `ILateTickable` (registration in `GameScopeRegistration`
  already uses `RegisterEntryPoint<BalloonMotionTicker>().AsSelf()`; VContainer
  picks up the new interface automatically).
- Data (all pooled, zero steady-state GC):
  ```csharp
  private struct NudgeImpulse { Vector3 Offset; float Duration; float Elapsed; }

  private sealed class NudgeState
  {
      public IBalloonMotionView View;
      public Vector3 BasePosition;
      public Vector3 LastWritten;
      public readonly List<NudgeImpulse> Impulses = new(MaxImpulsesPerView);
  }
  ```
  `List<NudgeState>` for iteration + `Dictionary<IBalloonMotionView, NudgeState>`
  for lookup + `Stack<NudgeState>` pool. `MaxImpulsesPerView = 8`; when full,
  overwrite the impulse closest to completion (silent — shockwave spam cap).
- API: `AddImpulse(IBalloonMotionView view, Vector3 offset, float duration)`,
  `CancelAll(IBalloonMotionView view)` (clears impulses, drops state WITHOUT a
  final write — the caller is despawning/teleporting the view).
- `LateTick()` per active state: advance impulses, swap-remove completed ones,
  reconcile base (§2.2), write \f$Base + \sum offsets\f$; on last impulse completed,
  write pure base and release the state.
- First-frame rule: when a state is created, initialize
  `BasePosition = view.Position` and `LastWritten = view.Position` so frame one
  never snaps.

**`Balloon/View/IBalloonMotionView.cs` — shrink.**

```csharp
internal interface IBalloonMotionView
{
    Vector3 Position { get; set; }   // transform.position passthrough
}
```
(A settable property keeps the fake trivially testable.)

**`Nudge/INudgeable.cs` — shrink.** `Nudge(Vector3 direction, float distance,
float duration)`. `NudgeType` stays a service/resolver concern; views no longer
branch on it. Drop `slotPosition` and `onComplete`.

**`Balloon/View/BalloonView.cs` — collapse `Nudge()` to a forward.**

```csharp
public void Nudge(Vector3 direction, float distance, float duration)
{
    _motionTicker.AddImpulse(this, direction.normalized * distance, duration);
}
```
Delete `CompleteNudge`, `OnNudgeCancelled`, `ApplyNudgePosition`, `_isNudging`,
`_tweenPausedForNudge`, both guards, the drift/accept logs, and the `DOScale`
block. Implement `Position => transform.position` get/set. `OnDespawned` keeps
`_motionTicker?.CancelAll(this)`.

**`Nudge/NudgeService.cs`** — keep override resolution and direction math
(`slotPos − origin`); call the new signature; delete diagnostic logs.

**`Balloon/Controller/BalloonBalancer.cs`** — `AnimatePaths` loses the entire
nudge branch (deferral, snapshot, `CancelNudge`, `OnNudgeCancelled`): kill
tracker + transform tweens, `StartBalanceTween`, done. Drop the now-unused
`BalloonMotionTicker` injection if nothing else uses it.

**`Shared/Animation/TweenTracker.cs`** — remove `Pause()`/`Resume()` (only the
deflect path used them). `IsPlaying` stays as-is.

**`Balloon/Controller/BalloonController.cs`** — deflect keeps publishing the
view position for physics; only the `Nudge` call-shape changes; delete
`[Nudge]` logs.

**Verify**: `dotnet build BalloonParty.Runtime.csproj` +
`python3 Tools/style_audit.py` on every touched file.

### Task 2 — Tests (test agent, after Task 1)

**New `Assets/Tests/EditMode/Nudge/BalloonMotionTickerTests.cs`** with a
`FakeMotionView` (`Position` auto-property). The ticker is plain C# — drive it
by calling `LateTick()` with a controllable delta (add an
`internal void Advance(float dt)` seam mirroring `TickFlightRebalance`'s
pattern, since `Time.deltaTime` isn't injectable).

| Test | Asserts |
|---|---|
| Single impulse round trip | position peaks mid-flight, returns *exactly* to base at completion; state released |
| No first-frame snap | first tick displacement \f$\le offset \times reach(dt/duration)\f$ |
| Stacked impulses sum | second impulse added mid-first; displacement = sum; each completes on its own timeline |
| Stack returns to base | after all impulses finish, position == original base |
| External base write adopted | move `Position` mid-impulse (simulated DOPath); final == new base |
| Continuous base writes | write a new base before every tick (balance slide); offsets ride the moving base; final == last base |
| CancelAll | impulses cleared, no further writes, state reusable |
| Impulse cap | 9th impulse replaces the most-completed one; never exceeds 8 |
| Degenerate duration | \f$duration \le 0\f$ clamps and completes without NaN |
| Pool reuse | start/complete cycles reuse pooled state (no dictionary/list growth) |

**Update existing tests**: audit `BalloonBalancerTests` and any test touching
the removed APIs; fix compile breaks in `BalloonParty.Tests.EditMode.csproj`.

**New play-mode regression `Assets/Tests/PlayMode/NudgeSettlePlayModeTests.cs`**
(mirror `BalanceSettlePlayModeTests` scaffolding): fire nudges at balloons
*during* an active balance, wait for settle, assert every view sits exactly on
`grid.IndexToWorldPosition(model.SlotIndex)` — the regression test for the
whole teleport/drift/stuck bug class. Flag that play-mode tests run in-editor.

### Task 3 — Documentation (docs agent, parallel with Task 2)

- Rewrite `Nudge/README.md`: replace *Stability tracking*, *Motion*, *Blend
  formula*, *Return-position inheritance*, and *Nudge ↔ balance interaction*
  sections with the impulse/base model, ownership map, and stacking semantics.
- Append a closing note to `PLAN-NudgeBalanceInteraction.md` pointing here as
  the implemented resolution.
- Register this plan in `Plans/Plans.md` (`@subpage plan_nudge_layered_motion`)
  and author `.meta` files mirroring a sibling's format.

### Task 4 — Adversarial review (review agent, after Tasks 1–3)

Review the full diff for gaps the implementation may have ignored, explicitly
checking:

- Pooling: no late-tick write can land on a despawned/reused view (CancelAll
  ordering vs `OnDespawned`); `CompositeDisposable` conventions.
- Reconciliation epsilon: parented balloons (board effects detach/reparent),
  float world↔local error, teleport adoption.
- Pause: ticker uses `Time.deltaTime` (timeScale-driven pause freezes it) —
  confirm parity with DOTween's pause behavior under `PauseService`.
- Float-away graduation / board effects / level cinematics taking over
  balloons mid-impulse.
- `IsStable` consumers — confirm nothing depended on nudges flipping it.
- Removed-API stragglers, dead config, stale comments/logs, README accuracy.
- Style audit + both csproj builds green.

---

## 4. Verification & rollout

1. `dotnet build BalloonParty.Runtime.csproj` and
   `BalloonParty.Tests.EditMode.csproj` — clean.
2. `python3 Tools/style_audit.py` — no `[ERROR]`.
3. Edit-mode suite green (in-editor run needed; no CI).
4. In-editor playtest checklist:
   - Rapid pops in a cluster — neighbors bounce, stack, and settle on-slot.
   - Deflect a tough balloon mid-balance — bounce plays *during* the slide;
     lands exactly on target.
   - Bomb shockwave during a cascade — no teleports, no stuck balloons.
   - Idle bob + nudge composition reads well (IsStable no longer flips).
   - Spawn wave + immediate neighbor pops — spawn scale/path unaffected.
