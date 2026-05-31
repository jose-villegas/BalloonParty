@page plan_runtime_optimizations Runtime Optimizations

# Runtime Optimizations

> Audit of the runtime source code for allocation, caching, and algorithmic
> improvements. Each item includes the file, the problem, and the fix.

---

## Priority Legend

| Icon | Meaning |
|------|---------|
| 🔴 | **High** — per-frame or hot-path allocation / algorithmic issue |
| 🟡 | **Medium** — per-event allocation that is easy to remove |
| 🟢 | **Low** — infrequent or small allocation; easy win |

---

## 🔴 High Impact

### ~~H-1 — `SlotGrid.CalculateWeight` unbounded recursion~~ ✅ Done

**File:** `Slots/Grid/SlotGrid.cs` · `CalculateWeight(int, int)`

`CalculateWeight` recurses into adjacent rows without memoisation. For deep
grids this recalculates the same cells many times (exponential blowup). Called
from `OptimalNextEmptySlot` → `BalloonBalancer.Balance()` on every rebalance.

**Fix:** Add a `Dictionary<(int,int), int>` memo parameter (allocated once per
`Balance()` call) or convert to an iterative bottom-up pass.

---

### ~~H-2 — `Camera.main` in `ThrowerController.Tick()`~~ ✅ Done

**File:** `Thrower/ThrowerController.cs` · `UpdateDirection()`

`Camera.main` performs `FindGameObjectWithTag("MainCamera")` internally. Called
every frame inside `UpdateDirection()` → `Tick()`.

**Fix:** Cache the camera reference in `Start()` or on first use.

---

### ~~H-3 — `PredictionTraceView.SetTrace()` allocates every frame~~ ✅ Done

**File:** `Prediction/PredictionTraceView.cs` · `SetTrace(List<Vector3>)`

`points.ToArray()` allocates a new `Vector3[]` every frame while the player is
aiming. `LineRenderer.SetPositions` can accept an array with a count.

**Fix:** Keep a cached `Vector3[]` buffer; resize only when capacity is
insufficient. Use `LineRenderer.SetPositions(array)` after copying from the
list, or use the NativeArray overload.

---

### ~~H-4 — `ScoreController.OnTrailArrived()` LINQ `.Sum()` every arrival~~ ✅ Done

**File:** `Game/Score/ScoreController.cs` · `OnTrailArrived()`

`_persistentScore.Values.Sum()` iterates the entire dictionary and boxes each
`int` through the LINQ pipeline. Called on every single score trail arrival
(many per pop).

**Fix:** Maintain a running `_totalScoreAccumulator` field; increment by 1 on
each arrival, subtract and re-add on level-up resets. Remove the LINQ `Sum()`.

---

### ~~H-5 — `SlotGrid.HexNeighborIndices()` allocates `Vector2Int[6]` every call~~ ✅ Done

**File:** `Slots/Grid/SlotGrid.cs` · `HexNeighborIndices(int, int)`

Returns a freshly allocated `Vector2Int[6]` array. Called from
`NudgeService.OnActorHit`, `PuffClusterRegistry`, `BombItemHandler`,
`PaintItemHandler`, and the balancer — several times per hit event.

**Fix:** Add a `HexNeighborIndices(int, int, Vector2Int[] buffer)` overload
that fills a caller-supplied buffer. Callers use a `stackalloc` or
thread-local / field-level buffer.

---

### ~~H-6 — `SlotGrid.ComputePath()` list + `.ToArray()` per spawn~~ ✅ Done

**File:** `Slots/Grid/SlotGrid.cs` · `ComputePath(Vector2Int, Vector2Int)`

Every balloon spawn allocates a `List<Vector3>` and then `.ToArray()`. Called
once per balloon per spawn wave (6+ balloons at a time).

**Fix:** Accept a caller-supplied `List<Vector3>` that is cleared and filled.
Return count instead of a new array, or return `IReadOnlyList<Vector3>`.

---

## 🟡 Medium Impact

### M-1 — `ItemAssigner.OnItemCheck()` LINQ chains

**File:** `Item/ItemAssigner.cs` · `OnItemCheck()`

Two LINQ chains (`.Where().ToArray()` and `.OfType<>().ToList()`) allocate on
every turn.

**Fix:** Replace with `for` loops and a reusable `List<T>` field.

---

### M-2 — `ItemActivator.OnActorHit()` LINQ `.FirstOrDefault()`

**File:** `Item/ItemActivator.cs` · `OnActorHit()`

`_handlers.FirstOrDefault(h => h.Type == ...)` iterates the handler collection
(which may be a deferred `IEnumerable` from VContainer).

**Fix:** Cache a `Dictionary<ItemType, IBalloonItem>` in `Start()`.

---

### M-3 — `BombItemHandler.BlastBalloons()` `new HashSet<Vector2Int>`

**File:** `Item/Bomb/BombItemHandler.cs` · `BlastBalloons()`

Creates a `new HashSet<Vector2Int>(...)` on every bomb blast.

**Fix:** Use a reusable `HashSet<Vector2Int>` field; clear before each blast.

---

### M-4 — `LaserItemHandler.CastCross()` `new HashSet<IBalloonModel>`

**File:** `Item/Laser/LaserItemHandler.cs` · `CastCross()`

Creates a new `HashSet` per laser activation.

**Fix:** Use a reusable field; clear before use.

---

### M-5 — `SlotGrid.GetNeighbors()` allocates a `new List`

**File:** `Slots/Grid/SlotGrid.cs` · `GetNeighbors(int, int)`

Called from `NudgeService.OnActorHit` on every balloon hit that has nudge.

**Fix:** Accept a caller-supplied list or return via `Span<T>`.

---

### M-6 — `PuffCloudViewController.Reconfigure()` `new List<Vector4>` + `.ToArray()`

**File:** `Slots/Actor/Archetype/PuffCloudViewController.cs` · `Reconfigure()`

Allocates a new list and converts to array on every cluster change.

**Fix:** Use a pre-allocated `Vector4[]` buffer (max 16 slots already enforced
by the shader).

---

### M-7 — `LightningItemHandler.CollectSortedTargets()` allocations

**File:** `Item/Lightning/LightningItemHandler.cs` · `CollectSortedTargets()`

Allocates a new `List` and uses a closure-based `Sort` on every activation.

**Fix:** Cache the list as a field; use a static `IComparer<T>`.

---

### M-8 — `BalloonBalancer.AnimatePaths()` `path.ToArray()` per actor

**File:** `Balloon/Controller/BalloonBalancer.cs` · `AnimatePaths()`

`path.ToArray()` allocates per actor during balancing.

**Fix:** DOTween `DOPath` requires an array — consider pooling via
`ArrayPool<Vector3>` or `ListPool`.

---

### ~~M-9 — `ScoreController.CheckLevelUp()` `Keys.ToArray()`~~ ✅ Done

**File:** `Game/Score/ScoreController.cs` · `CheckLevelUp()`

`.Keys.ToArray()` allocates just to iterate safely during mutation.

**Fix:** Cache the list of color name strings at init time.

---

### M-10 — `TrailFlight.SetSpeed/SetUnscaledTime` `DOTween.TweensByTarget()`

**File:** `Shared/Pool/TrailFlight.cs` · `SetSpeed()`, `SetUnscaledTime()`

`DOTween.TweensByTarget()` returns a new `List<Tween>` each call.

**Fix:** Stash the tween references at creation time instead of querying
DOTween's internal map.

---

## 🟢 Low Impact

### L-1 — `BalloonSpawner.PublishItemCheck()` `.ToArray()`

**File:** `Balloon/Spawner/BalloonSpawner.cs` · `PublishItemCheck()`

`_newlySpawnedBalloons.ToArray()` allocates per wave.

**Fix:** Change `ItemCheckMessage` to accept `IReadOnlyList<IBalloonModel>`.

---

### L-2 — `ScoreCounterLabel.Bind()` `int.ToString("N0")`

**File:** `UI/Score/ScoreCounterLabel.cs` · `Bind()`

`s.ToString("N0")` allocates a string on every score change.

**Fix:** Use `ZString` or a fixed `StringBuilder` for allocation-free
int-to-string conversion. Low frequency but trivial to fix.

---

### L-3 — `ProjectileView.OnTriggerEnter2D()` `GetComponentInParent<BalloonView>()`

**File:** `Projectile/View/ProjectileView.cs` · `TryGetHitBalloon()`

Called on every physics trigger with a balloon. Layer filtering already limits
this, but `GetComponentInParent` still walks the hierarchy.

**Fix:** Attach a lightweight `BalloonViewRef` MonoBehaviour to the collider
root that caches the `BalloonView` reference.

---

### L-4 — `PuffCluster.AddSlot()` `List.Contains()` is O(n)

**File:** `Slots/Actor/Archetype/PuffCluster.cs` · `AddSlot()`

Linear scan membership test.

**Fix:** Use a `HashSet<Vector2Int>` alongside the list, or check the
`_slotToCluster` dictionary in the caller instead.

---

### L-5 — `StaticActorSpawner.SpawnStaticActors()` `List.Remove(slot)` is O(n)

**File:** `Slots/Actor/StaticActorSpawner.cs` · `SpawnStaticActors()`

Removes from the middle of a `List<Vector2Int>`, shifting elements. Runs once
at startup.

**Fix:** Use swap-and-remove-last, or build a `HashSet<Vector2Int>` of used
slots and filter at the end.

---

## ✅ Already Well-Optimised

These patterns are already in good shape — no action needed:

- Shader property IDs cached as `static readonly int`
- Layer masks cached as `static` / lazy-init fields
- Object pooling used consistently via `PoolManager`
- `CompositeDisposable` used properly for subscription cleanup
- `DOKill()` called in all `OnDespawned()` methods
- Pre-allocated batch arrays in `DisturbanceFieldService`
- `WeightedPickExtensions` uses a static candidate buffer
- `GetComponent` calls nearly always in `Awake()` only
- `FixedUpdate` used for physics-based movement (ProjectileView)


