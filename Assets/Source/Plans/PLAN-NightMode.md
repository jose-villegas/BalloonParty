@page plan_night_mode Night Mode / Time of Day

# Night Mode / Time of Day

Give each level a **time of day** and let the level-up transition sweep the sun to the next
one — the light direction walks a circle, and everything that reads the scene light (colour
gradient, intensity, GI shadows) follows. Purely atmospheric: no gameplay rule changes, and
the colour-matching read must stay intact.

Builds on the shipped direction→colour gradient (see @ref plan_lighting and
`Configuration/Effects/SceneLightFieldSettings`): `LightColor` samples a full-circle gradient
indexed by the light direction (`Vector2.Angle01`, `t = angle / 360`).

---

## Decisions (locked)

- **Authoring — procedural per-level.** A start angle + degrees-per-level, wrapping the circle.
  Endless and cyclic; a full day spans `360 / degreesPerLevel` levels. No per-level authoring.
- **Drives — colour + intensity + GI shadow strength.** The angle feeds the colour gradient
  (done), an intensity-over-angle curve (midnight genuinely dimmer, not just bluer), and a
  shadow-strength-over-angle curve (longer/stronger shadows at low sun).
- **Scope — cosmetic only.** Difficulty stays owned by `LevelDifficultyResolver`. Time-of-day
  correlates with the climb but never changes rules.
- **Sweep — always-forward.** Time only advances; the sweep continues *through* the
  midnight→dawn wrap rather than reversing to the shortest arc.
- **Readability guardrail.** The ambient tint multiplies into every consumer, and this is a
  colour-matching game — so the **balloons stay largely tint-exempt** (low `_LightInfluence`,
  the existing per-material opt-in) and **scenario / background / clouds** carry the mood.

## The problem it solves

Today the ambient direction/colour/intensity are *static* config: `SceneLightFieldSettings`
resolves `LightColor` from its own `_lightDirection`, and `SceneLightFieldService` pushes the
globals once at `Start` (per-tick only under `UNITY_EDITOR`). Nothing animates the direction at
runtime. Night mode needs a runtime owner of the *current* angle that the sweep can drive and
that everything reads from.

## Architecture

```
LevelController ── Phase → Transitioning ──▶ TimeOfDayService (new, plain-C# IStartable)
                                                    │  owns CurrentAngle (ReactiveProperty)
                                                    │  snap on run start/reset; sweep on level-up
                                                    ▼
                              pushes _SceneLightDir + gradient _SceneLightColor + _SceneLightIntensity
                                                    │
                     ISceneLightRuntime ◀───────────┘ (CurrentDirection/Colour/Intensity/ShadowStrength)
                          ▲                    ▲
        SceneLightFieldService          ScreenSpaceLightService
        (ambient read for the field)    (GI: reads intensity + shadow strength per capture)
```

`TimeOfDayService` becomes the **single writer** of the ambient shader globals and the single
source of the *current* light state, exposed read-only as `ISceneLightRuntime`. When night mode
is **off** it holds the static authored angle and reproduces today's look bit-for-bit — the same
acceptance bar the field used (field-off is bit-identical).

- Gradient evaluation moves **out** of `SceneLightFieldSettings.LightColor` (which becomes the
  static/rest fallback) and **into** the runtime owner, which samples the gradient at
  `CurrentAngle` each change.
- `SceneLightFieldService` stops pushing the ambient globals; it reads the ambient it needs for
  the local field from `ISceneLightRuntime` instead of `ISceneLightSettings`.
- `ScreenSpaceLightService` reads its magnitude-ref (intensity) and shadow strength from
  `ISceneLightRuntime`, falling back to `IScreenSpaceLightSettings` when night mode is off.

## Phases

### Phase 1 — Runtime direction ownership (refactor, no behaviour change)
Introduce `TimeOfDayService` + `ISceneLightRuntime`. Move the `_SceneLightDir`/`_SceneLightColor`/
`_SceneLightIntensity` push and the gradient evaluation to the service. Default `CurrentAngle` =
the authored `_lightDirection`'s angle, static — **look is identical to today**. Rewire
`SceneLightFieldService` and `ScreenSpaceLightService` onto `ISceneLightRuntime`. Register in the
game scope; keep the editor live-tuning affordance (per-tick re-push under `UNITY_EDITOR`).
*Acceptance:* night mode off ⇒ pixel-identical; the `[UnitCircle]` + angle field still relight live.

### Phase 2 — Procedural time-of-day + level sweep — SHIPPED (pending in-editor playtest)
Config (`ITimeOfDaySettings`, new night-mode section on the settings asset): `NightModeEnabled`,
`DegreesPerLevel`, `SweepDuration`, `SweepEase`. There is **no** start-angle knob — level 1 sits at
the already-authored `ISceneLightSettings.LightDirection` (the UnitCircle + Angle° field), so
`angle(level) = base + (level - 1) × DegreesPerLevel` (continuous, never wrapped = always-forward).
The Shared-layer `TimeOfDayService` stays a passive holder/pusher; the Game-layer `TimeOfDayCycle`
(`Game/Level/`) owns the policy — snap on Start/`ResetRun` (at `RunResetOrder.Respawn`, after
`LevelController`'s `Score` reset re-applies the `StartLevel` cheat), sweep on `Phase → Transitioning`
over `SweepDuration` on unscaled time so it plays through the transition pause.
*Acceptance:* climbing levels visibly walks the day; wrap is continuous; a run reset snaps; night mode
off is a true no-op. Follow-up: a multi-tick test of an interrupting level-up mid-sweep (behaviour
verified by review; not yet regression-pinned).

### Phase 3 — GI shadow over angle DONE; intensity PARKED (2026-07-24)
**Intensity: PARKED.** Stays at 1 — the colour gradient alone carries day/night and looks good, so
per-angle intensity isn't worth wiring. Reserved as a future hook for **weather / special drama** (a
storm dimming the scene), not the routine cycle. This is *why* the alpha-follows-light feature keys off
the colour-vector magnitude, not the scalar intensity — with intensity flat, colour is the only
day/night signal. If revived: an intensity-over-angle curve in `TimeOfDayService` → `_SceneLightIntensity`.

**GI shadow over angle: DONE.** `ITimeOfDaySettings.ShadowStrengthOverAngle` (an `Angle01`-indexed
curve, matched endpoints, default flat 1) → `TimeOfDayService.ShadowStrengthScale` (1 when night mode
off or unauthored) on `ISceneLightRuntime` → `ScreenSpaceLightService` multiplies the authored
`ShadowStrength` by it (clamped 0..1). So shadows deepen/lighten as the sun sweeps, no intensity needed.
All C# (no shader edit); bit-identical when off. Follow-up if wanted: also drive length (`SmearDistance`)
/ softness (`ShadowMipSpread`) — deferred (more smear worsens the known GI border halo).

### Phase 4 — Readability + atmosphere polish
Verify balloons stay legible across a full cycle (audit `_LightInfluence` on balloon materials vs
scenario/background/clouds); tune the default day arc; confirm local lights (pops, laser,
projectile) read strongly against night. Full-cycle in-editor playtest.
*Acceptance:* red-vs-orange (and every colour pair) stays distinguishable at every time of day.

## Risks / notes

- **Colour-matching legibility is the hard constraint.** If any time of day makes two balloon
  colours ambiguous, that's a gameplay bug, not a look nit — Phase 4 gates on it.
- **Single-writer discipline.** Two services pushing the ambient globals would race; Phase 1's
  whole point is to consolidate the writer. Grep for stray `_SceneLightDir`/`_SceneLightColor`
  pushes after the refactor.
- **`dotnet build` can't validate the look** — every phase needs an in-editor playtest (relight,
  sweep timing, night dimming, legibility). GI shadow changes especially.
- The launcher previews the ambient light too: Phase 1 registers `TimeOfDayService` +
  `ISceneLightSettings` in `LaunchLifetimeScope` so it pushes the ambient globals there. No field or
  GI in the launcher (no `SceneLightFieldService`), so `_SceneLightFieldOn` stays 0 and shaders take
  the flat ambient path — enough to keep launch→game seamless. Only launcher materials that opt into
  the scene light change; flat art is unaffected.

## Out of scope (later)

Mechanical coupling (glow balloons, night-only rules), authored per-range "chapter" moods, and
tying the danger/loss state to a region of the circle — deferred; see @ref plan_future_ideas.
