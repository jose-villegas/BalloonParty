# Clearance

Every ball in flight carries a clear circle of one fixed radius, so the player can see what the ball is
doing however busy the board gets. Score shapes slide out of the circles, and the effect shaders read
them. This folder owns the circles; the features that keep out of them read them through
`IClearanceField` and never look at the balls themselves. Design: @ref plan_pen_clearance.

## Contents

| File | What it does |
|---|---|
| `ClearanceCircle` | One circle this frame: centre in the board's XY plane, effective radius (the settings' radius × strength), and strength 0..1 |
| `IClearanceField` | The read side: `Count` and `CircleAt(i)`. Formations read it; trails will too |
| `ClearanceField` | The circles themselves: `ClearanceCircle.MaxCircles` (8) fixed slots, keyed by the ball's model reference. Plain C#, EditMode-tested (`ClearanceFieldTests`) |
| `ClearanceFieldTicker` | `IStartable` + `ITickable` + `IDisposable` (game scope): publishes an empty field on start, then each frame tracks every ball in `IProjectileRegistry`'s in-flight list into the field, advances it, and publishes it to the shader globals. On dispose it zeroes `_ClearanceCount`, since globals outlive the scope |

Tuning lives in `IClearanceSettings` (`Configuration/Effects/ClearanceSettings`, the
`Assets/Configuration/ClearanceSettings.asset` wired on the `GameLifetimeScope` prefab; an unwired scope
falls back to the defaults).

## A circle's life

1. **Launch.** A ball that enters the registry's in-flight list takes a free slot, at strength 0. Its
   strength rises to 1 over `FadeIn` (0.15 s), so a circle never pops in.
2. **Flight.** The circle follows the ball every frame.
3. **Landing.** Once the ball leaves the in-flight list (it landed, or the run reset), its circle stays
   where the ball was last seen and fades to 0 over `FadeOut` (0.4 s). A ball's last pop is where most
   effects start, and a circle that vanished on the frame the ball did would let them close over that spot
   at once.
4. **Freed.** At strength 0 the slot is free again.

The strength a reader sees is smoothstepped, and the radius is already scaled by it, so everything that
keeps out of a circle lets go gently.

Eight slots cover the ball cap (3), every Extra Pen card (3) and a couple of circles still fading. A new
ball takes a free slot, or, when none is free, the weakest fading one; it can't run out, since at most six
balls fly at once. A ball's model can come back from the pool for
a new flight while its old circle still fades; the new flight gets a fresh slot, so the old circle doesn't
jump to the cannon.

The ball held in the thrower gets no circle: it sits below the board, where nothing draws over it.

## Who reads it

- **Score formations** (`Game/Score/Behaviours/ShapeFormationTicker`): after their own overlap packing,
  each moving formation is pushed out of every circle into its persistent `SeparationOffset`, at up to
  `FormationPushSpeed` world units a second. The circles never give way. See `Game/Score/README.md`,
  Overlap Resolution.
- **Effect shaders**: the ticker publishes `_ClearanceCircles` (8 × `float4`: centre xy, effective
  radius, strength), `_ClearanceCount`, `_ClearanceFloor` and `_ClearanceSoftness` every frame. With
  `FadeEffects` off it publishes a count of 0.

## Settings

| Setting | Default | Meaning |
|---|---|---|
| `Radius` | 0.6 | World radius of a full-strength circle (the ball's sprite is about 0.54 long; the board is 5.2 wide) |
| `RimSoftness` | 0.4 | Width of the smooth edge, as a share of the radius |
| `FadeIn` / `FadeOut` | 0.15 s / 0.4 s | How fast a circle eases in at launch and out after landing |
| `EffectFloorAlpha` | 0.3 | Alpha an effect keeps at a full-strength circle's centre |
| `FormationPushSpeed` | 14 | World units a second a formation slides out of a circle; faster than a ball (about 10) |
| `PushFormations` / `BendTrails` / `FadeEffects` | on | Switch each reader off on its own, to compare in a playtest |
