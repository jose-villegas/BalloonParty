@page plan_unbreakable_formations Unbreakable Formations

# Unbreakable Formations

> When three or more unbreakables are on the board, they stop roaming. They line up in a
> formation, hold it for a few turns, then switch to a different one. While they are in
> formation their red lights turn blue, and a blue glitter swirl spins behind each of them.
>
> **No changes until this plan is actioned.**

---

## Goals

1. **Formations.** With at least 3 unbreakables on the board, they gather into a shape:
   - a **horizontal line**,
   - a **diagonal** in either direction (`/` or `\`),
   - an **item cover**: a row of unbreakables right under a balloon that carries an item, so the
     player, who shoots from below, can't hit the item directly.
2. **Holding.** A formation stays in place for **N turns**. Its members do not roam, and they do not
   float up in the balance race. When the N turns are over, they change into a different formation.
3. **Blue while in formation.** Everything that is red on an unbreakable turns blue while it is in
   formation, and turns red again when the formation breaks.
4. **Glitter behind.** The spiral glitter from the night top bar renders behind each member, tinted
   blue.

A **turn** here is a spawn **wave**, three lines on the spawn clock. Unbreakables already jump once
per wave (`BalloonBalancer.Balance(atWaveStart: true)`), so a wave is the unit their movement is
counted in today.

---

## How unbreakables move today

- **Once per wave they roam.** `BalloonBalancer.RelocateRoamers` offers every `IPreBalanceRelocatable`
  the board's resting slots. These are empty slots that need no further settling.
  `UnbreakableBalloonModel.TryPickRelocation` jumps to the board's opposite side, preferring far
  slots.
- **Between waves they float up.** Like every balloon, an unbreakable takes part in the balance race
  (`BalancePlanner`). It rises while either slot above it is empty (`GridBalanceQuery.IsUnbalanced`).
  Its entry has unlimited steps, speed 2 and `DirectBalanceMotion`, so it glides straight to where
  it ends up.
- **A spawn shove relocates it.** In `PressurePropagation` its `PushResponse` is `RelocateFarthest`,
  so a shove from below makes it jump to the farthest free slot instead of pushing it.
- **The shot solver copies the race.** `ShotBoardDynamics` runs the real `BalancePlanner` on a
  headless grid to predict where balloons will be during the in-flight rebalance pulses. Every
  target that has a `BalanceProfile` is a mover there.

A formation has to switch off all four of these for its members. If it doesn't, the race floats a
horizontal line up as soon as there is a gap above it, and a spawn shove scatters it.

---

## What makes an unbreakable red

Found in `Assets/Prefabs/Balloon/Unbreakable.prefab` and the materials it uses:

| Source | Where | Today |
|---|---|---|
| Core glow | `Internal/Body (7)`: `circle.png`, `UnbreakableMatIN` | `SpriteRenderer.color` (0.88, 0.12, 0.12) |
| Core dust | `Internal/Body (8)`: `circle_dust.png`, `UnbreakableMatIN` | `SpriteRenderer.color` (0.88, 0.12, 0.12) |
| Inner rim | `UnbreakableMatIN` (`Body (7)`, `Body (8)`, `BodyIN`) | `_RimColor` (0.97, 0.20, 0.00) |
| Exhaust trail | `Trail` (`TrailRenderer`, `LR_Material_UnbreakablExhaust`) | gradient crimson → orange → cyan (fading out) |
| Motion light | `UnbreakableBalloonVariant._moveLight` | palette `Unbreakable` (1, 0, 0) |
| Burst stamp and specks | `UnbreakableBalloonVariant.EmitPulse` | the stamp is tagged with palette `Unbreakable`, and the specks take that colour from the tag |

`Body (7)` and `Body (8)` are not in the variant's `_renderers` or `_innerRenderers`. They get their
colour only from `SpriteRenderer.color`, so a tint on those two renderers is enough. The rest of the
sphere (`UnbreakableMat`, `UnbreakableMatIN2`) is chrome and blue-grey and stays as it is.

---

## The glitter effect

The night top-bar glitter is `ColorProgress.prefab` → `Slider/Shadow (1)`. It is a UI `Image` with
the `ColorBarNightGlitter` material, using the shader `BalloonParty/Sprite/GlitterSwirl`
(`Assets/Shaders/BalloonParty/Sprite/SpriteGlitterSwirl.shader`). Its `CanvasGroup` sits in
`ColorProgressBar._nightFadeExtras`, so it fades in with night.

The shader is a **sprite** shader, so it works on a world-space `SpriteRenderer` as well as on UI.
`LaserGlitter.mat` and `PickupInSightGlitter.mat` already use it in the world. It never draws the
sprite itself. The sprite's alpha, times the renderer's alpha, only masks where specks may appear,
and the renderer colour tints them. So:

- A soft disc sprite (`circle.png`) on a child renderer behind the shell confines the swirl to a halo
  around the balloon.
- `SpriteRenderer.color.a` fades the whole halo in and out.
- The blue comes from the palette (see below), through the renderer colour.

The bar material is tuned for a long bar (density 64, speck size 0.5, swirl speed 20). A halo about
the size of a balloon needs its own material, `UnbreakableFormationGlitter.mat`, with lower density.
The values get tuned in the Editor.

---

## Design

### Rules

- **Who joins.** Unbreakables on the board, up to `MaxMembers`. Each formation change picks the
  members closest to the formation's spot. Any extra unbreakables keep roaming as today.
- **When.** A formation is checked at each wave start, before the roam pre-pass:
  - **No formation and at least `MinMembers` (3) unbreakables:** pick a shape, find a spot for it,
    and move the members there.
  - **Holding, with turns left:** count one turn down. Nothing moves.
  - **Holding, no turns left:** pick a different shape and move into it. That counts as a new
    formation, and its own hold starts.
- **Where.** Every formation slot must be empty, or already held by one of the members. A formation
  never pushes other balloons out of the way. Slots in the bottom `BottomClearance` rows are never
  used, so spawn lines always have room to enter. If no spot is valid for the chosen shape, the next
  shape is tried. If none fits, there is no formation this wave and the unbreakables roam.
- **Which spot.** Among the valid spots for a shape, the one with the least total travel wins, with
  a small random pick among the near-best so it doesn't always form in the same place.
- **Breaking up.** If members are popped (a piercing hit) and fewer than `MinMembers` are left, the
  formation breaks at once. The rest turn red, are released into the balance race, and roam from the
  next wave. A new unbreakable that spawns while a formation holds keeps roaming, and joins at the
  next change.
- **Run reset or level-up:** the formation is cleared.

### Shapes on the hex grid

The grid is pointy-top, with odd rows shifted half a column right, and row 0 at the top
(`HexCoordinates`). For `k` members:

```
Horizontal            Diagonal "\"           Diagonal "/"          Item cover (k = 3)
row r:  U U U         row r:    U            row r:        U       row r:    [I]
                      row r+1:   U           row r+1:     U        row r+1: U U U
                      row r+2:    U          row r+2:    U
```

- **Horizontal:** `(c, r) … (c+k-1, r)`.
- **Diagonals:** step `row + 1` each time. The column follows the odd-row shift, the same neighbour
  rule as `HexCoordinates.HexNeighborIndices`.
- **Item cover:** the item host's two lower neighbours first (row `r+1`). The row then grows outward,
  alternating left and right, until there are `k` slots. If several balloons carry items, the one
  whose cover costs the least travel is picked.

Members **hover** in midair: they are anchored, not supported. Balloons below a formation rise until
they reach it, so a formation also works as a ceiling, and holes open above it. That is the danger
the formations add: a run of unbreakables the player has to shoot around. The score is not touched.

### Architecture

```
Balloon/Formation/                          (new folder, namespace BalloonParty.Balloon.Formation)
  FormationShape            enum: Horizontal, DiagonalDown, DiagonalUp, ItemCover
  FormationPlanner          pure: shape + grid + members + item hosts → target slots, or none
  FormationAssignment       pure: members → target slots, least total hex travel
  UnbreakableFormationService   controller: hold counter, shape order, break-up; IRunResettable
  README.md

Slots/Actor/IBalanceAnchor.cs               bool IsAnchored: the race, roam and pressure leave it alone
Balloon/Model/IFormationMember.cs           IReadOnlyReactiveProperty<bool> InFormation (for the view)
Configuration/Balloons/UnbreakableFormationSettings.cs   + IUnbreakableFormationSettings
```

- **Model.** `UnbreakableBalloonModel` implements `IBalanceAnchor` and `IFormationMember`. It gains
  an internal `SetInFormation(bool)` that only the service calls. While it is in formation:
  - `TryPickRelocation` returns false,
  - `IsAnchored` is true,
  - `PushResponse` is not used, because pressure treats an anchored actor as a static blocker.
- **Controller.** `UnbreakableFormationService` is a plain C# class registered in VContainer. At wave
  start, `BalloonBalancer.Balance(atWaveStart: true)` calls `_formations.PlanWaveStart(moves)` before
  `RelocateRoamers`. The service returns the moves only. The **balancer** carries them out the way it
  carries out roam moves (`Reserve`, `Remove`/`Place`, `IsStable = false`, `RecordPath`), so all grid
  changes and animation stay in one place. The service then sets each member's `InFormation`.
  Break-ups are found from the grid's change stream (`SlotGridChangedEvent`), so there is no polling
  every frame.
- **Balance seam.** `BalancePlanner.TryBalanceSlot`, `BalancePlanner.CollectPassCandidates`,
  `BalloonBalancer.HasPossibleMove` and `BalloonBalancer.RelocateRoamers` all skip an
  `IBalanceAnchor { IsAnchored: true }`. `PressurePropagation` treats it as immovable, the same as an
  actor without `IPressureMovable`.
- **Solver.** `ShotBoardGather` gives an anchored target no `BalanceProfile`, so `ShotBoardDynamics`
  places it as a static contact and the predicted pulses don't move it. This keeps the aim solver and
  the playtest bot honest about formations.
- **View.** `UnbreakableBalloonVariant` subscribes to `IFormationMember.InFormation` in `Bind` and
  eases a `0 → 1` blend over `ColorFadeDuration`. It is driven from its existing `Update`, only while
  the blend is changing:
  - `Body (7)` and `Body (8)`: `SpriteRenderer.color` lerps from its authored red to the palette's
    `UnbreakableFormation` blue, keeping the authored alpha. These go in a new serialized
    `_coreLights` array.
  - The inner rim: an MPB `_RimColor` override on the renderers that use `UnbreakableMatIN`, lerped
    the same way.
  - The trail: two `Gradient`s built once in `Awake`, the authored one and a blue copy with the same
    alpha keys. They are swapped at the midpoint of the fade. A per-frame gradient lerp would allocate.
  - The motion light and the burst stamp use the formation palette index while the blend is past 0.5,
    so the specks around a formation member puff blue too.
  - `FormationGlitter`: a new child `SpriteRenderer` behind the shell, with the `circle.png` sprite
    and `UnbreakableFormationGlitter.mat`. Its alpha follows the blend, and it is disabled at 0, so
    it costs no draw call out of formation.
  - `OnDespawned` / despawn resets the blend to 0 and restores the red, as pooling requires.
- **Palette.** A new `UnbreakableFormation` entry in `GamePalette.asset` (blue, `presentationOnly`),
  with a `GamePalette.UnbreakableFormationColorId` constant. All the blue comes from this one entry.

### Settings (`IUnbreakableFormationSettings`)

Nested in `BalloonsConfiguration` as `[SerializeField] UnbreakableFormationSettings _unbreakableFormation`,
so no new asset or scope registration is needed. The values below are defaults, to be tuned after a
playtest. The questions below may change them.

| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | true | Master switch, for A/B playtests |
| `MinMembers` | 3 | Fewest unbreakables that form |
| `MaxMembers` | 5 | Most that join one formation |
| `HoldWaves` | **N, open question** | Waves a formation holds |
| `BottomClearance` | 2 | Bottom rows a formation never uses |
| `ShapeWeights` | per shape | How often each shape is picked (see open questions) |
| `ColorFadeDuration` | 0.4 s | Red ↔ blue ease |

---

## Open questions

1. **N:** how many waves a formation holds.
2. **Walk or snap:** do members glide into formation (the existing direct glide at their speed) or
   snap there (vanish and reappear with a flash)?
3. **Order:** how the next shape is picked.
4. **Item cover against lines:** when it wins.

The answers are recorded under **Decisions** once given.

## Decisions

Answered by the user on 2026-11-10:

1. **N = 3 waves.** `HoldWaves` defaults to 3.
2. **Walk.** Members glide into formation with their existing direct glide (speed 2), and turn blue
   when they arrive. No new blink effect is needed.
3. **Random, no repeat.** A weighted random pick among the shapes that have a valid spot. The shape
   just held is excluded, unless it is the only one that fits.
4. **Item cover joins the rotation** only while a balloon on the board carries an item. Its weight is
   higher than each line's: defaults 1 for each line shape and 2 for the item cover.

---

## Phases

### Phase 1: Formation logic
- `IBalanceAnchor`, `IFormationMember`, the model changes, `FormationPlanner`, `FormationAssignment`,
  `UnbreakableFormationService`, and the settings.
- The balancer, planner, pressure and solver changes for anchored actors.
- **Tests (EditMode):**
  - `FormationPlanner`: line and diagonal slots on even and odd rows; occupied slots rejected;
    bottom clearance kept; the item cover takes the host's lower neighbours first; no spot gives none.
  - `FormationAssignment`: least total travel.
  - The service: under 3 never forms; 3 form and anchor; a hold of N waves moves nothing; a change
    after N picks a different shape; dropping below 3 breaks it and releases the rest; a newcomer
    during a hold roams; a reset clears the formation.
  - `BalancePlanner`: an anchored actor in an unbalanced slot stays, and balloons below rise to it.
  - `PressurePropagation`: an anchored actor blocks the push.
  - `ShotBoardGather`: an anchored target has no balance profile.
- Then run the architect, test-everything and reviewer agents, and curate their findings.

### Phase 2: Visuals
- The palette entry, the variant's blend (core lights, rim, trail, light and stamp colour), the
  `FormationGlitter` child and its material, and the prefab wiring.
- **Needs an in-Editor check:** the shader values on the new material, the halo size and sort order,
  and how the blue reads against each terrain. `dotnet build` does not check prefab or material
  edits.
- Run the reviewer and optimizer agents (the glitter's draw calls, and allocations from the blend).

### Phase 3: Docs and playtest
- `Balloon/Formation/README.md`; updates to `Balloon/README.md` (balance phase), `Balloon/Type/README.md`
  (the variant), `Slots/README.md` (`IBalanceAnchor`) and `Solver` docs.
- `Tools/playtest/playtest_bot.py` comparison with `Enabled` on and off: levels, shots, hearts. The
  bot is not a real player, so this only compares the two tunings.
- The user's in-Editor playtest and sign-off.

---

## Risks

- **A horizontal line can cut the board.** Balloons below it can't rise past it. With `MaxMembers` 5
  on a wider board, there is always a way around, and `BottomClearance` keeps spawns open. If the
  playtest shows lines blocking too much, cap the line length below the board width.
- **The roam pre-pass and the formation share the wave start.** The formation runs first, so roamers
  see the formation's slots as taken.
- **Prefab YAML edits.** Adding a child and serialized references by editing the YAML is fragile. Each
  edit is checked by reopening the prefab in the Editor, through the Unity CLI once the change is on
  main.
