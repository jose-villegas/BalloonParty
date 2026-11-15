# Cloth Ribbon

A ribbon of cloth with writing on it, hanging from a spring line: the launch screen's Continue balloon
carries one (`Game/LaunchContinueView`). The line and ribbon are simulated together as points joined by
springs, so they swing, stretch a little and flutter as whatever holds the line moves.

## How it moves

`RibbonCloth` is the simulation, plain C#. Points carry position and last position (position-based
dynamics, at fixed steps of `Steps Per Second`, at most eight a frame); each step adds the forces, then a few
solver passes pull every spring back toward its rest length and push points out of the obstacles.

- **The line** hangs from an anchor (the balloon's knot), a chain of springs. Its last point is the ribbon's
  **pivot**: the middle of the ribbon's head, in the swallowtail notch.
- **The ribbon** is three rows of points (top edge, middle, bottom edge) by `Columns`, joined along, across,
  diagonally (shear) and two columns apart (bend). Its points are light (`Ribbon Weight`): the breeze moves
  the ribbon without hauling the line off hanging down.
- **The breeze** streams the ribbon out behind the anchor's sideways travel, so a still or drifting balloon
  keeps it straight. Each column is held upright, top over bottom: the ribbon can droop and turn about the
  vertical, but can't roll over in the screen's plane.
- **Turning round.** When the anchor's travel reverses (faster than `Heading Speed`), the breeze turns round
  over `Turn Rate`, and while it turns the tail is pushed out of the screen (`Swing Kick`). So the ribbon swings
  round through depth to trail the other way, rather than flipping over; on the way, and once it trails the
  other way, its back faces the screen.
- **The wave** runs down the ribbon toward the tail, growing from nothing at the pivot: mostly a fold in depth,
  a little lift. With the springs it reads as cloth fluttering.
- **Obstacles** (`KeepOut`: a circle, or a capsule) push points out sideways, never in depth.

Space is the caller's: x right, y up, z toward the viewer. The ribbon's front faces the viewer while it
streams to +x. Drawing drops z.

## How it draws

| Class | What it draws |
|---|---|
| `SpringLineGraphic` | The line, a thin strip through its points |
| `ClothRibbonGraphic` | The ribbon's three rows as a notched strip, through `BalloonParty/Sprite/CelRibbon` (the card reveal's ribbon shader): each fold shaded by how its normal leans, as seen from the side facing the screen, plus the shader's drop-shadow copy |
| `ClothRibbonWriting` | The line of writing, on both faces: two `TMP_Text`s laid out flat, then every glyph corner carried onto the cloth where it sits. The back's copy is laid along the ribbon the other way (mirrored), so seen from behind, once the ribbon has swung round, it reads the right way. Each glyph shows on the face turned toward the screen where it sits and fades as that part turns edge on |

All three are simulated in their own local space, so they sit together at the same origin. `ClothRibbonWriting.Layout`
lays the writing out and returns the length of ribbon it needs, which the simulation is built with.

## Settings

`RibbonClothSettings` (serialized on the view that owns the ribbon) holds the line's and ribbon's shape, the
forces and the solver. Tooltips say what each does.
