@page plan_nudge_balance_interaction Nudge ↔ Balance Interaction Debugging

# Nudge ↔ Balance Interaction — Post-mortem & Architecture Review

> Documents the multi-iteration debugging of visual bugs caused by the nudge
> ticker and balance DOPath system fighting over `transform.position`, the fixes
> applied at each stage, why the system proved so fragile, and proposed
> architectural improvements to prevent similar regressions.

---

## 1. The Original Problem

When a projectile hit a tough balloon mid-balance-slide, the **deflect bounce
animation was silently dropped**. The balloon showed no visual reaction to the
impact. Neighbor balloons of popped targets also sometimes failed to nudge, or
nudged but teleported to a wrong position.

### Symptoms observed

| Symptom | Severity |
|---|---|
| Tough balloon hit mid-balance shows no bounce | High — breaks feedback |
| Balloon teleports to grid slot on nudge start (snaps from mid-slide pos) | High — visible glitch |
| Balloon stuck permanently offset from its slot after cascading pops | High — visual desync |
| Balloon overlaps neighbor after balance completes (view/model desync) | High — layout broken |
| Some balloons don't react to neighbor pops (appear frozen) | Low — cosmetic, brief |

### Root architecture

Three independent systems write `transform.position` for balloons:

- **BalloonMotionTicker** — ticker-driven out-and-back lerp (nudge). Runs in
  `Update` via `ITickable.Tick()`. Sets position every frame via
  `ApplyNudgePosition()`.
- **BalloonBalancer** — DOTween `DOPath` tween (balance slide). Lives inside
  `TweenTracker` (a `Sequence` wrapper). DOTween updates position in `Update`.
- **Direct position sets** — `DOScale`, spawn animations, snap-to-target on
  degenerate paths.

There was no coordination mechanism between these systems. The ticker-driven
lerp escapes `transform.DOKill()` (it doesn't use DOTween), and
`DOTween.IsTweening(transform)` doesn't detect tweens wrapped inside a
`TweenTracker` Sequence.

---

## 2. Fix History — Chronological

Each fix addressed one layer of the problem. Testing after each fix revealed the
next layer, because the systems interact in ways that only manifest under
specific timing conditions (cascading pops, rapid nudge replacement, deflect
during mid-balance).

### Commit 1 — `0afbea6a` — Pass NudgeType through to BalloonView

**Problem**: `BalloonView.Nudge()` had a stability guard that skipped the nudge
when `IsStable = false` and `_isNudging = false`. During a balance move,
`IsStable` is false, so ALL nudges — including deflect bounces — were dropped.

**Fix**: Added `NudgeType source` parameter to `INudgeable.Nudge()`. The
stability guard now bypasses for `NudgeType.Deflect`:

```csharp
// Before: all nudges blocked when unstable
if (!stableChecker.IsStable.Value && !_isNudging) return;

// After: deflect always proceeds
if (source != NudgeType.Deflect
    && !stableChecker.IsStable.Value
    && !_isNudging) return;
```

**Files changed**: `INudgeable.cs`, `NudgeService.cs` (pass source through),
`BalloonView.cs` (guard bypass).

### Commit 2 — `ae55a415` — The compound fix (3 root causes)

This commit (amended repeatedly) addressed three interacting bugs discovered
through iterative log-driven debugging.

#### Fix 2a — TweenTracker.IsPlaying detection

**Problem**: `DOTween.IsTweening(transform)` returns false for tweens wrapped in
a TweenTracker `Sequence`. The nudge code couldn't detect an active balance
tween, so it started nudging a balloon that was simultaneously being moved by
DOPath. The two systems fought over `transform.position`, causing visual jitter.

**Fix**: Added `Pause()` and `Resume()` to `TweenTracker`. Changed `IsPlaying`
from `IsActive() && !IsComplete()` to `IsActive() && IsPlaying()` (the DOTween
method returns false when paused). Added tracker-playing guard in
`BalloonView.Nudge()`:

```csharp
// New guard: don't nudge while balance DOPath is running
if (source != NudgeType.Deflect && _tweenTracker.IsPlaying) return;
```

For deflect nudges, the tracker is **paused** instead of ignored, and resumed on
`CompleteNudge()`.

#### Fix 2b — Path pool reuse (deferred balance gets empty path)

**Problem**: When a balloon was mid-nudge and a balance fired,
`AnimatePaths` deferred the balance by capturing a closure over the pooled
`List<Vector3> path`. But `ReleasePaths()` ran before the deferred callback
fired, returning the list to the pool and clearing it. The callback received an
empty path → degenerate-move fast-path → balloon snapped to wrong position →
`IsStable = true` at wrong visual position → view/model desync and overlap.

**Fix**: Snapshot the path before deferring:

```csharp
// Before: captured reference to pooled list
_motionTicker.ReplaceOnComplete(nudgingView, () =>
    StartBalanceTween(actor, path, view));

// After: snapshot — the pooled list will be released independently
var pathSnapshot = new List<Vector3>(path);
_motionTicker.ReplaceOnComplete(nudgingView, () =>
    StartBalanceTween(actor, pathSnapshot, view));
```

#### Fix 2c — Deflect physics position

**Problem**: When a tough balloon was mid-balance, its **model** slot index
pointed at the target position, but its **view** was still at the origin. The
deflect code used `grid.IndexToWorldPosition(model.SlotIndex)` for the
`BalloonDeflectedMessage` surface normal calculation — giving the projectile a
wrong reflection direction.

**Fix**: Use `view.transform.position` (the visual position the projectile
actually hit) for the deflected message and disturbance stamp. Keep
`grid.IndexToWorldPosition` only for computing nudge direction.

### Commit 2 (continued amendments) — Additional fixes discovered through testing

#### Fix 2d — IsStable set true before deferred balance

**Problem**: `CompleteNudge()` unconditionally set `IsStable = true` and then
invoked `onComplete`. When `onComplete` was a deferred `StartBalanceTween`, it
expected `IsStable` to already be false (set by `TryBalanceSlot`). The momentary
`true` broke guards.

**Fix**: `CompleteNudge()` only sets `IsStable = true` when there is no deferred
callback (`onComplete == null`) and no paused tracker to resume:

```csharp
if (_tweenPausedForNudge)
{
    // Resume or kill the paused tracker — it manages IsStable
}
else if (onComplete == null)
{
    w.IsStable.Value = true;  // nudge was the only motion
}
// When onComplete != null: the deferred balance's OnComplete will restore it.
```

#### Fix 2e — Stale `_isNudging` after balance cancellation

**Problem**: When `BalloonBalancer.AnimatePaths` cancelled a nudge via
`CancelNudge()`, it didn't reset `_isNudging` on the view. Subsequent neighbor
nudges arrived while `_isNudging = true` but the ticker had no entry for this
view, causing the nudge to silently pass the guard but create a fresh entry with
unexpected state.

**Fix**: Added `OnNudgeCancelled()` to `IBalloonMotionView`. The balancer calls
it after `CancelNudge()`:

```csharp
_motionTicker.CancelNudge(motionView);
motionView.OnNudgeCancelled();  // _isNudging = false, _tweenPausedForNudge = false
```

#### Fix 2f — Double deflect guard

**Problem**: A second deflect arriving while the first was still playing found
the tracker already paused. Calling `Pause()` on an already-paused sequence is
harmless, but on `CompleteNudge` the code would try to `Resume()` a tracker
whose internal progress was invalid (the balloon had moved since the pause
point).

**Fix**: If `_tweenPausedForNudge` is already true when a new deflect arrives,
kill the paused tracker outright (a future balance creates a fresh path):

```csharp
if (_tweenPausedForNudge)
{
    _tweenTracker.Kill();
    _tweenPausedForNudge = false;
}
else if (trackerPlaying)
{
    _tweenTracker.Pause();
    _tweenPausedForNudge = true;
}
```

#### Fix 2g — Lost deferred callback on nudge replacement

**Problem**: When nudge B replaced nudge A via `StartNudge → CancelNudge`, A's
`onComplete` (the deferred balance callback) was destroyed — `CancelNudge`
recycled the entry without preserving the callback. The deferred balance never
fired; the balloon stayed at the nudge's return position with `IsStable = false`,
permanently stuck.

**Fix**: `CancelNudge` returns the old callback. `StartNudge` inherits it:

```csharp
var previousComplete = CancelNudge(view);
onComplete ??= previousComplete;  // Inherit deferred balance if present
```

#### Fix 2h — First-frame teleport (visual vs grid position)

**Problem**: `NudgeService` passed `grid.IndexToWorldPosition(slot)` as
`slotPosition`. For a balloon mid-balance, the model's slot was the **target**
(far away), but the view was at the **origin**. The first frame of the nudge set
\f$transform.position = slotPosition + offset \cdot reach(tiny) \approx slotPosition\f$ →
the balloon teleported.

**Fix**: Override `slotPosition` with `transform.position` (the visual position)
so the nudge always starts from where the balloon currently IS.

### Uncommitted — Drift accumulation fix

**Problem**: The fix from 2h (always use visual position) solved the teleport
but introduced drift accumulation. When nudges were rapidly replaced mid-flight
(common during cascading pops), each new nudge captured the mid-outbound visual
position as its return target. At completion (reach = 0), the balloon returned
to the mid-outbound position instead of the correct grid slot. Each replacement
compounded the error:

```
Slot (3,0): grid=(0.19, 3.25)
  Nudge 1: visual=(0.22, 3.41)  — 0.16 drift
  Nudge 2: visual=(0.25, 3.46)  — 0.22 drift
  Nudge 3: visual=(0.18, 3.58)  — 0.33 drift
  Nudge 4: visual=(0.25, 3.71)  — 0.46 drift ← permanently offset
```

**Root cause**: The nudge motion formula was symmetric —
\f$position = center + offset \times reach\f$ — with center = visual position. It always
returned to wherever it started. With the visual-position override, "wherever it
started" was a drifting point.

**Fix** (two parts):

1. **Blend formula** — separate `startPosition` (visual, prevents snap) from
   `returnPosition` (grid slot, ensures correct landing):
   ```
   basePos  = Lerp(startPosition, returnPosition, progress)
   position = basePos + offset × reach
   ```
   At progress 0: \f$position = startPosition\f$ (no snap).
   At progress 1: \f$position = returnPosition\f$ (correct grid slot).

2. **Return-position inheritance** — when a nudge replaces another mid-flight,
   `CancelNudgeInternal` returns the cancelled entry's `SlotPosition`. The new
   entry inherits it as its `returnPosition`:
   ```csharp
   var (previousComplete, previousReturn) = CancelNudgeInternal(view);
   if (previousReturn.HasValue) returnPosition = previousReturn.Value;
   ```
   This preserves the original grid position across any number of rapid
   replacements.

3. **Selective visual override** — `BalloonView.Nudge` only uses visual pos as
   return target for Deflect + mid-balance (where the grid target is genuinely
   far away). All other cases use the grid position:
   ```csharp
   var returnPosition = slotPosition;  // grid pos
   if (source == NudgeType.Deflect && trackerPlaying)
       returnPosition = visualPos;     // symmetric bounce, balance resumes after
   ```

---

## 3. Files Modified (Summary)

| File | Changes |
|---|---|
| `Nudge/INudgeable.cs` | Added `NudgeType source` parameter |
| `Nudge/NudgeService.cs` | Passes source through; diagnostic logs |
| `Balloon/View/BalloonView.cs` | Complete rewrite of `Nudge()`, `CompleteNudge()`; new `OnNudgeCancelled()`; tracker-playing guard; double-deflect guard; drift-correcting blend |
| `Balloon/View/IBalloonMotionView.cs` | Added `IsNudging`, `OnNudgeCancelled()` |
| `Balloon/Controller/BalloonMotionTicker.cs` | `StartNudge` split into start/return positions; `CancelNudgeInternal` returns callback + position; blend formula; `ReplaceOnComplete`; diagnostic logs |
| `Balloon/Controller/BalloonBalancer.cs` | Extracted `StartBalanceTween()`; mid-nudge deferral with path snapshot; calls `OnNudgeCancelled()` on cancel |
| `Balloon/Controller/BalloonController.cs` | Deflect uses view position for physics, slot position for nudge direction |
| `Shared/Animation/TweenTracker.cs` | `IsPlaying` uses `_active.IsPlaying()` (paused = false); added `Pause()`, `Resume()` |
| `Nudge/README.md` | Expanded with blend formula, interaction table, architecture notes |

---

## 4. Why the System Was Fragile

The debugging required 11 iterations across 3 interacting systems. This fragility
stems from several architectural patterns.

### 4.1 Multiple writers, no ownership protocol

Three systems write `transform.position`:

- `BalloonMotionTicker.TickNudges()` — every frame during a nudge
- `TweenTracker` (DOTween) — every frame during a balance
- Direct sets — spawn snaps, degenerate-path fast-path

There is no locking, no priority queue, no "who owns the transform right now?"
check. Each system assumes it has exclusive control. Conflicts are resolved by
ad-hoc guards (`if trackerPlaying return`, `if _isNudging defer`) that grew
organically through the debugging. The guards prevent simultaneous writes but
can't express priority or composition.

### 4.2 Implicit state machine (boolean soup)

The balloon's motion state is encoded across three booleans:

| Field | Owner | Meaning |
|---|---|---|
| `_isNudging` | `BalloonView` | A nudge ticker entry exists for this view |
| `IsStable.Value` | Model (reactive) | No motion system is driving the balloon |
| `_tweenTracker.IsPlaying` | `TweenTracker` | A DOPath balance tween is running |

This creates \f$2^3 = 8\f$ possible states, but only ~4 are valid:

| `_isNudging` | `IsStable` | `trackerPlaying` | Valid? |
|---|---|---|---|
| false | true | false | ✅ Idle |
| true | false | false | ✅ Nudging |
| false | false | true | ✅ Balancing |
| true | false | true | ⚠️ Deflect mid-balance (tracker paused) |
| false | false | false | ⚠️ Window between cancel and balance start |
| true | true | * | ❌ Should never happen |
| false | true | true | ❌ Should never happen |

The invalid/transient states are where bugs hide. Fix 2d (IsStable set true
before deferred), Fix 2e (stale _isNudging), and the "barely moving" skips all
stem from balloons in the `(false, false, false)` transient window.

### 4.3 Lifecycle coupling without explicit contracts

- The nudge ticker owns a `NudgeEntry` that references the view, but the view
  doesn't know the entry's internal state (start position, elapsed progress,
  return target). When the view needs to make decisions based on nudge state, it
  can only read `_isNudging` — a single boolean that loses all context.
- The balancer schedules deferred callbacks into the ticker's completion slot,
  but the ticker doesn't know what the callback does. When a new nudge replaces
  the entry, the callback is silently destroyed unless the caller explicitly
  inherits it — a fragile protocol maintained by convention, not by contract.
- `CompleteNudge()` must decide what to do with `IsStable` based on whether a
  deferred callback exists, whether the tracker was paused, and whether the
  callback will start a new tween. This decision tree grew through bugs 2d/2f/2g.

### 4.4 Position semantics ambiguity

The term "slotPosition" was used for three different concepts:

1. **Grid position** — `grid.IndexToWorldPosition(slot)` — where the model says
   the balloon IS (after balance resolves the new slot).
2. **Visual position** — `transform.position` — where the view currently renders.
3. **Return target** — where the nudge should end up when it completes.

These three can differ significantly during a balance move. The original code
used (1) for all contexts. Fix 2h changed everything to (2). The drift fix
finally separated (2) from (3) by splitting `StartNudge` into `startPosition` +
`returnPosition`.

### 4.5 Timing-dependent interactions

| Phase | System | What happens |
|---|---|---|
| `FixedUpdate` | Physics | Hits detected → `ActorHitMessage` published |
| `FixedUpdate` | NudgeService | Synchronous handler → `BalloonView.Nudge()` → `StartNudge()` |
| `Update` | BalloonMotionTicker | `Tick()` — first frame of new nudges |
| `Update` | DOTween | Balance tweens update position |
| `Update` (deferred) | BalloonBalancer | `AnimatePaths()` — cancels nudges, starts balance |
| `Update + 1 frame` | BalloonBalancer | Deferred balance (via `UniTask.Yield()`) |

A nudge started in FixedUpdate can be cancelled by the balancer in the same
frame's Update. The balloon nudges for 0 rendered frames. The visual effect
depends on whether the ticker or DOTween updates first — an ordering Unity
doesn't guarantee between `ITickable` and DOTween's update mode.

---

## 5. Known Remaining Behavior

### "Barely moving" / "Not reacting" balloons

After the drift fix, some balloons still appear unresponsive to nearby pops
during cascading chains. Two causes:

1. **Correctly skipped** — balloons whose nudge was just cancelled by the
   balancer are `IsStable=false, _isNudging=false`. The stability guard rejects
   new neighbor nudges. A balance DOPath is running (or about to start). This
   is intentional — the ticker and DOPath would fight over `transform.position`
   if both ran simultaneously.

2. **Tiny nudge distance** — tough balloons have a per-type override of
   `distance=0.020` (≈2 pixels). The visual motion is near-invisible by design.

Neither is a bug. The window where nudges are rejected lasts only as long as
the balance tween (~0.3–0.5s). After the tween completes and `IsStable`
restores, the balloon accepts nudges normally.

---

## 6. Proposed Architectural Improvements

### 6.1 Formal motion state machine

Replace the boolean soup with an explicit enum and transition rules:

```csharp
enum MotionState { Idle, Nudging, Balancing, Spawning }
```

Each state defines who owns `transform.position` and which transitions are
valid. The state machine rejects invalid transitions with an assertion instead
of silently proceeding. Guards become declarative:

```csharp
// Instead of: if (!stable && !nudging && source != Deflect) return;
if (State != MotionState.Idle && source != NudgeType.Deflect) return;
```

### 6.2 Canonical home position

Add a `HomePosition` field to the model, updated by balance on completion and
by slot assignment on spawn. The nudge ticker always returns to `HomePosition`
— no need to inherit return positions, no drift possible by construction:

```csharp
entry.ReturnPosition = view.HomePosition;  // always correct
```

### 6.3 Layered / additive motion

Instead of "one owner at a time" for `transform.position`, separate motion
into layers that compose:

```
finalPosition = BasePosition + NudgeOffset
```

- **BasePosition** — owned by balance DOPath (or idle slot position when no
  tween is active). Updated by DOTween or static assignment.
- **NudgeOffset** — owned by the nudge ticker. A transient additive offset
  that rises and falls. Applied in `LateUpdate` after DOTween.

This eliminates all position-fighting, all stability guards, and all
nudge-vs-balance coordination. Both systems can run simultaneously. A balloon
mid-balance-slide can visually react to a neighbor pop with a small bounce
without interrupting the slide.

Implementation sketch:

```csharp
// In BalloonView:
private Vector3 _nudgeOffset;

void LateUpdate()
{
    transform.position += _nudgeOffset;
}

// In BalloonMotionTicker:
void ApplyNudgeOffset(Vector3 offset) => _nudgeOffset = offset;

// In CompleteNudge:
_nudgeOffset = Vector3.zero;
```

The DOPath writes `transform.position` in Update; `LateUpdate` adds the nudge
offset. No fighting, no guards.

### 6.4 Callback queue instead of single-slot inheritance

Replace the fragile `onComplete ??= previousComplete` pattern with a small
queue:

```csharp
class NudgeEntry
{
    public readonly Queue<Action> PendingCallbacks = new();
}
```

When a nudge is replaced, all pending callbacks transfer to the new entry.
When the final nudge completes, all queued callbacks fire in order. This
prevents lost callbacks regardless of how many replacements occur.

### 6.5 Motion ownership token

A lightweight token that makes ownership explicit:

```csharp
readonly struct MotionToken : IDisposable
{
    // Only the holder can write transform.position.
    // Disposing releases ownership.
}
```

Systems acquire the token before writing. If the token is held, the request
is either queued (for nudge) or forcibly claimed (for balance, which has
higher priority). This formalizes the ad-hoc guard system.

### 6.6 Position reconciliation on balance start

Instead of guarding against all nudge-during-balance scenarios, let the
balancer reconcile position when it starts:

```csharp
// In StartBalanceTween:
// Don't care where the balloon currently is — the DOPath starts from
// viewTransform.position (wherever it ended up after nudge/drift/etc.)
var waypoints = WaypointBuffer(viewTransform.position, path);
```

This already happens today, but if combined with layered motion (6.3), the
balancer only owns `BasePosition` and never needs to cancel nudges or check
`_isNudging` at all.

---

## 7. Recommendation

The highest-value improvement is **6.3 (layered motion)** because it eliminates
the entire class of nudge-vs-balance bugs by making the two systems orthogonal.
The existing PLAN-UnifiedHexMotion already contemplates a unified weight model;
layered motion is a complementary view-side change that makes the nudge ticker
independent of the balance system's tween ownership.

If layered motion is too large a refactor for now, **6.1 + 6.2** (state machine
+ home position) provide the most defensive value by catching invalid state
transitions early and removing the drift vector entirely.

---

## 8. Resolution

The layered-motion proposal (§6.3) was implemented as @ref plan_nudge_layered_motion. This replaced the guard-based coordination with an additive impulse model on top of a reconciled base position, eliminating the entire class of nudge-vs-balance fighting bugs. The nudge and balance systems are now orthogonal and can run simultaneously without coordination.
