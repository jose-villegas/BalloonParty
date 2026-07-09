# Danger

The **early-warning signal** for running out of space — a single 0→1 value the UI maps onto a colour
gradient so the player can feel the board getting dangerous before they actually lose a heart.

## Contents

| File | What it does |
|---|---|
| `SpaceDanger` | Plain C# entry point (`IStartable`, `IDisposable`). Exposes `IReadOnlyReactiveProperty<float> Level` and recomputes it whenever the grid (`SlotGrid.OnChanged`) or hit points (`IPlayerHealth.Current`) change. `Evaluate` is a pure, unit-tested function so the curve can be reasoned about in isolation |
| `IDangerLevel` | Read-only seam (`Level`) consumers bind against — registered alongside `SpaceDanger` in `GameLifetimeScope` |

## The danger curve

```
overflow      = max(0, spawnPerTurn − availableSpace)   // balloons a turn couldn't place = hearts it'd cost
Level         = hearts <= 0 ? 1 : clamp01(overflow / hearts)
```

- **0** — the board can still absorb the next turn's spawn; no warning.
- **rising** — as free space shrinks, the would-be overflow grows; `Level` is the fraction of the heart
  pool a single turn would burn.
- **1** — a single turn could empty the heart pool (`overflow ≥ hearts`), or there are no hearts left.
  The consumer shows the gradient's final colour.

Two inputs are deliberately simple heuristics and double as tuning knobs:

- **`availableSpace`** = the empty-slot count. Re-home + pressure balance fill nearly every empty, so
  this approximates "how many balloons the board can still take" without re-deriving reachability.
- **`spawnPerTurn`** = `IActiveLevelParameters.Current.SpawnLines × Columns`, the worst case a turn can throw at the board.

## Consumers

`UI/Danger/DangerGradientView` samples a `Gradient` at `Level` to tint sprites and grows the **top/bottom
gradient sprites** toward the centre — simulating the gradient creeping over the scenario. Each side has
its own height increase (`SpriteRenderer.size.y += increase × Level`, so those sprites need a Sliced/Tiled
draw mode) and is recentred by **half** the growth (centred pivot assumed) so its outer edge stays put:
the bottom expands up, the top expands down. On top of that it slides a top and a bottom container by a
**custom per-side Y offset** (`restY + offsetY × Level`, sign per the inspector value). It treats the bound
`Level` as a *target* and eases a current value toward it each frame (frame-rate-independent, `_lerpSpeed`),
so tint, growth and translation glide rather than snapping. `DangerUILifetimeScope` (a child scope)
binds every `DangerGradientView` under its hierarchy to `IDangerLevel.Level` at `Start` via the shared
`RegisterBoundViews` helper (`UI/Binding/`). Author the gradient, assign the target sprites, and
(optionally) set the container + Y offset in the inspector — nothing renders until those are wired.
