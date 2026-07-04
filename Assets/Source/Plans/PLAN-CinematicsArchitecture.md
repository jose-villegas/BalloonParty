@page plan_cinematics_architecture Cinematics Architecture

# Cinematics Architecture

> Restructures the cinematics system before it grows: per-cinematic behaviour moves from
> hand-maintained boolean flags into declared **traits**, tuning moves from scene
> MonoBehaviours into a **settings SO**, and the shared shape of the two existing
> cinematics is captured as a reusable **camera-rig cinematic** runner so the next
> cinematic (game-over loss) is a parameterization, not a fourth copy. No visual change —
> this is architecture only.

---

## Orientation — start here

**What this is:** a refactor plan for `Game/Cinematics/` + `Shared/GameState/` driven by
four observations (all verified against the code):

1. `CinematicStateService` answers per-trait questions (`BlocksLoss`, `BlocksShake`) with
   pattern-matches over states — every new cinematic means editing N switch expressions,
   and forgetting one silently defaults to `false`.
2. The two producers (`LevelUpTrailEffect`, `HeartTrailCinematicEffect`) are fat scene
   MonoBehaviours holding **tuning** (zoom/pan/follow, slow-mo curves) as serialized
   fields — the *fields* are duplicated between them (the authored *values* diverge, see
   the recovered table in Phase 2) and this violates the repo rule that config lives in
   SO assets behind read-only interfaces.
3. Both cinematics are the *same shape* — slow-mo ramp + camera pan-in + follow a focus +
   restore. "Cinematic" is the general concept; these two are **camera-rig cinematics**.
   That shape is the missing abstraction.
4. The camera shake wants to be a composable **camera effect** (it already became additive
   this session), not an ad-hoc service with a hard-coded cinematic gate.

**Status:** Phases 1 + 2 **implemented 2026-07-02**, revised three times in review to the
final shape: (a) traits live in the settings SO, not a code table; (b) tuning is
enum-indexed, not named fields; (c) **fully per-state uniform segments** — one
`[EnumIndexed(typeof(CinematicState))] CinematicStateEntry[]` where each entry composes
`Traits` + a uniform `CameraRigCinematicSettings` segment (TimeScaleCurve/Zoom/Pan/Follow;
the curve's last key is the duration) + `TrackedTrailSettings`. Restores are ordinary
segments (`RestoreCurve`/`RestoreSeconds` deleted); `CinematicState.HeartDrainRestore`
added and the heart-drain producer flips to it. `ICinematicState.Has(trait)` reads
through the SO; `RunController`/`CameraShakeService` migrated; both producers stripped of
serialized tuning (they keep only the `Camera` ref); the field initializers AND the asset
carry the recovered authored values, so a fresh instance equals the shipped asset
(asserted by `CinematicsSettingsTests`). `GameLifetimeScope` wiring done; Phases 1+2
playtested green.

**Phase 3a implemented 2026-07-02:** one DI-owned `CinematicCameraRig`
(`Register<CinematicCameraRig>(Lifetime.Singleton)`) fed by a thin `CinematicCameraView`
(`RegisterComponentInHierarchy`; lazy `Camera.main` fallback since the rig is constructed
at container build, before Awake). The rig's ctor tuning is gone — `PreparePanIn(segment)`
and the unified `Frame(focus, segment, dt)` take the active state's
`CameraRigCinematicSettings` per call; `FollowTrail`/`FollowPoints` collapsed into `Frame`
over `ICinematicFocus` (`PointFocus` = level-up tipping trail, hard-clamp; `HeartTrailFocus`
= tracker centroid+bounds, pre-clamp — clamp order auto-selected by min==max). Producers
lost their serialized `Camera` refs. **In-editor pending (blocking): add a
`CinematicCameraView` component to the Game scene (on the camera) and wire `_camera`** —
`RegisterComponentInHierarchy` fails at container build without it; then playtest both
cinematics. — **DONE, playtested.**

**Phase 3b + 4 implemented 2026-07-02:** `CameraRigCinematic` runner (plain C#) — pan-in
segment (timeScale drive + `Frame`) → end condition → restore segment (tween-from-current
to 1 + rig restore); owns begin/end pairing and `Abort()` teardown.
`CinematicDirector.TryBeginCinematic` centralizes the drop-while-busy policy.
`HeartTrailCinematicEffect` (MonoBehaviour) is **deleted**, replaced by
`HeartDrainCinematic` — a plain C# `IStartable` entry point that is just trigger + focus +
end condition over the runner. The missing component was removed from `Cinema.prefab` and the heart-drain playtested. —
**DONE.**

**Phase 3c implemented 2026-07-02:** the runner grew a `CameraRigCinematicConfig`
(`DrivesTimeScale`, `RestoreEvaluatesCurve`, `OnPanInTick(dt, curveValue)` hook, `OnEnded`,
nullable `EndCondition`) and the split-phase API (`TryBegin` … `EndPanIn` … external gate …
`TryBeginRestore`; `IsPanInRunning`). `LevelUpTrailEffect` (MonoBehaviour) is **deleted**,
replaced by plain-C# `LevelUpCinematic` (`RegisterEntryPoint`): the tipping-trail
intercept/puppeteering ported verbatim into the tick hook (pan-in leaves timeScale alone —
the curve modulates the trail; gameplay paused), restore samples its curve from the
popup's frozen 0, `OnEnded` hands back `NavigationState.Game`. Both cinematics are now
runner parameterizations; no cinematic MonoBehaviours remain except the camera view.
The component was removed from `Cinema.prefab` and the full level-up playtested. — **DONE.**

**Phase 5 implemented 2026-07-02:** `TimeScaleService` (+ `TimeScaleSource`, in
`Shared/Pause/` beside `PauseService`): claim/release per source, lowest active claim
wins, no claims = 1, `IRunResettable` clears. Writers migrated: `CameraRigCinematic`
claims `Cinematic` (pan-in ramp + both restore forms, released on end/abort/EndPanIn) and
`LevelUpPopUp` claims `LevelUpPopup` = 0 (released on dismiss *after* publishing the
dismissed message, so the restore's claim is already active — no full-speed flash).
**Enforcement live:** `style_audit.py` rule `timescale-writes` (`[ERROR]`) bans direct
`Time.timeScale` writes outside `TimeScaleService` (reads/comparisons allowed), with four
`test_style_audit.py` cases. **In-editor pending:** playtest level-up (popup freeze →
restore ramp) + heart-drain slow-mo. Remaining: Phase 6 (camera-effect composer,
deferred until a second camera effect exists).

**Decisions already locked** (from the loss/pacing work — don't re-litigate):
- Heart-drain is non-loss-blocking (game-over fires through it) and non-shake-blocking.
- HP + shake charge at heart *launch*; the pop is purely visual.
- The shake is additive (offset delta in `LateUpdate`) and unscaled.

---

## Current state (inventory)

| Piece | Type | Problem |
|---|---|---|
| `Shared/GameState/Cinematic.cs` | static reactive state (the `ICinematicAware` listener list was deleted 2026-07-04 — zero implementers) | fine — keep |
| `Shared/GameState/CinematicState.cs` | enum `None/LevelUpPanIn/LevelUpRestore/HeartDrain` | conflates *identity* with *behaviour* — behaviour lives elsewhere as flags |
| `Shared/GameState/ICinematicState.cs` + `CinematicStateService` | seam: `IsPlaying`, `BlocksLoss`, `BlocksShake` | one boolean property **per trait**, each a central pattern-match; scales as traits × states |
| `Game/Cinematics/CinematicDirector.cs` | plain C# ticker | `BeginCinematic`/`EndCinematic` pairing is manual; `BeginCinematic` while active silently stomps; both producers carry `OnDestroy` repair code |
| `Game/Cinematics/CinematicCameraRig.cs` | plain C# camera driver | good core; `FollowTrail`/`FollowPoints` are two entry points for one concept (a focus); constructed per-producer with per-producer tuning |
| `Game/Cinematics/LevelUpTrailEffect.cs` | MonoBehaviour, ~310 lines | trigger + trail tracking + slow-mo + camera + restore + gameplay pause, all in one scene object; tuning serialized |
| `Game/Cinematics/HeartTrailCinematicEffect.cs` | MonoBehaviour, ~165 lines | same shape duplicated (curve, zoom/pan/follow fields, timeScale ramp, restore) |
| `Display/CameraShakeService.cs` | MonoBehaviour | additive offset (good); gate on `BlocksShake` hard-codes the cinematic relationship |
| `Time.timeScale` writers | 3, uncoordinated | `LevelUpTrailEffect`, `HeartTrailCinematicEffect`, `LevelUpPopUp` (sets 0) — overlap is last-writer-wins, each `OnDestroy` hand-restores `1f` |

Consumers of the flags today: `RunController.EndRun` (`BlocksLoss`), `CameraShakeService`
(`BlocksShake`), `CinematicEndGate` (per-state `IReadyGate`), `CameraShakeService`-style
`IsPlaying` checks in producers' trigger guards.

---

## Part A — Traits instead of per-trait booleans

The insight: `BlocksLoss`/`BlocksShake` are not properties of "the cinematic system";
they're properties of **each cinematic**. Declare them once per state, query by trait.

```csharp
[Flags]
internal enum CinematicTraits
{
    None        = 0,
    BlocksLoss  = 1 << 0,   // 0-HP game-over must wait (level-up)
    BlocksShake = 1 << 1,   // cinematic hard-owns the camera; shake stands down
    // future: PausesGameplay, OwnsTimeScale, GatesUI...
}
```

**Declared in the settings SO** (revised in review from an initial code-table design): the
Part-B `CinematicsSettings` asset is where every cinematic already gets an entry, so it is
also where traits are declared — one registry, not two. Per-state storage is a
`[EnumIndexed(typeof(CinematicState))] CinematicTraits[]` array (O(1) by ordinal, drawn
with enum names by the existing `EnumIndexedDrawer`), whose **field initializers are the
canonical declarations** — a fresh instance carries them (testable without
`AssetDatabase`), and the asset starts from them.

- `ICinematicState` becomes `bool IsPlaying { get; }` + `bool Has(CinematicTraits trait)`.
  `CinematicStateService` injects `ICinematicsSettings`;
  `Has` = `(settings.TraitsOf(Cinematic.Current.Value) & trait) != 0`.
- `RunController`: `_cinematic.Has(CinematicTraits.BlocksLoss)`.
  `CameraShakeService`: `_cinematic.Has(CinematicTraits.BlocksShake)`.
- Adding a cinematic = one enum value + one initializer line (+ its settings block).
  `TraitsOf` throws on an unmapped state; `CinematicsSettingsTests` walks the enum on a
  fresh instance, so a forgotten declaration fails CI instead of silently defaulting.
  `OnValidate` keeps an older asset's array in lock-step with the enum (new entries
  default to `None`).
- `RunControllerTests` swap `BlocksLoss.Returns(true)` for `Has(BlocksLoss).Returns(true)`.
- Traits are correctness-critical data in an asset now: the safety net is the defaults
  test + the throw; a designer flipping `BlocksLoss` in the inspector is a deliberate,
  visible act.

## Part B — Settings out of the scene

Repo-standard config: one `CinematicsSettings` ScriptableObject + `ICinematicsSettings`
read-only interface, registered in `GameLifetimeScope` like the other nine configs.

```csharp
CinematicsSettings : ScriptableObject, ICinematicsSettings
{
    [EnumIndexed(typeof(CinematicState))] CinematicStateEntry[] _states;   // ONE registry, per state
}

CinematicStateEntry            // everything one state declares, composed from uniform blocks
{
    CinematicTraits Traits;                      // behaviour (BlocksLoss/BlocksShake)
    CameraRigCinematicSettings Rig;              // the uniform SEGMENT: TimeScaleCurve (its last key IS
                                                 // the segment duration) + ZoomAmount + PanWeight
                                                 // + FollowSpeed
    TrackedTrailSettings TrackedTrail;           // capability block: any trail-tracking segment can use
                                                 // it (level-up's tipping-trail 4× pulse today)
}
```

**The generalization (third review revision, final):** a *restore is not special-cased* —
it's just another segment whose curve ramps timeScale back to 1 with zoom/pan at 0 (target
= base framing). So `RestoreCurve`/`RestoreSeconds` dissolved; `LevelUpRestore` is simply
another entry, and the heart-drain's hidden restore got its own state
(`CinematicState.HeartDrainRestore`, appended to preserve serialized indices — the
producer now flips to it via `BeginCinematic` when the drain ends). Every state has the
same structure; durations are consistent because they all come from the curve's last key
(`FollowSpeed` stays a *speed* — tracking responsiveness has no endpoint, so no duration
field is needed anywhere). Consumers read `EntryOf(state)`; adding a cinematic = enum
value(s) + entry initializer(s).

Nuance carried forward to the Part-C runner: the heart-drain restore tweens timeScale
**from wherever it currently is** to 1 over the segment's duration (sampling the curve
directly would snap speed down first when the drain ends early, e.g. game-over during the
first ramp). The runner's restore segments should ease from-current toward the curve's end
value rather than sample absolutely.

Repeating *values* across entries is fine (explicitly accepted) — consistency comes from
the shared **type**, and each cinematic stays independently tunable. Scene objects keep
only scene references (the `Camera`).

### Value recovery — authored values ARE NOT the code defaults

The live tuning lives on `Assets/Prefabs/Game/Cinema.prefab` (not the code defaults, which
drifted far behind). Recovered 2026-07-02 — this table is the **source of truth for the SO
asset**; migration is only done when the asset matches it:

| Field | LevelUp (authored) | HeartDrain (authored) |
|---|---|---|
| ZoomAmount | **2** | **0.15** |
| PanWeight | **0.6** | **0.1** |
| FollowSpeed | **0.7** | **2** |
| SlowDownCurve | keys (0, 1) → (1.5, 0.5) → (3, 1) — dips to half speed and *recovers by itself* over 3 s | keys (0, 1) → (0.6, 0.3) |
| Restore | `_restoreCurve` keys (0, 0) → (3, 1) — from frozen (popup set 0) to full over 3 s | `_restoreSeconds` = **0.85** |
| TrackedTrailScaleCurve | keys (0, 1) → (0.5, 4) → (1, 1) — pulses to 4× mid-flight | — |
| `_camera` | `{fileID: 0}` in the prefab — wired per scene instance as an override | same |

Notes the recovery surfaced:
- The authored values diverge per cinematic (zoom 2 vs 0.15) — confirming per-cinematic
  SO entries over shared values.
- Both `_camera` refs are scene-instance overrides on the prefab — exactly the awkwardness
  the single `CinematicCameraView` (Part C) removes.
- The SO asset can be authored headlessly from this table; the in-editor step reduces to
  wiring the `GameLifetimeScope` field and verifying the inspector shows these values.

## Part C — The camera-rig cinematic abstraction

Both producers reduce to the same skeleton. Extract it once:

```
CameraRigCinematic (plain C#, one instance per cinematic kind)
├─ state + traits            (Part A)
├─ CameraRigCinematicSettings (Part B)
├─ ICinematicFocus            what the camera frames this tick
│    bool TryGetFocus(out Vector3 center, out Bounds bounds)
│    — single-trail focus (level-up) and trail-set focus (heart tracker) both implement it;
│      FollowTrail/FollowPoints collapse into one rig Frame(focus, dt) call
├─ Func<bool> endCondition    (trail arrived / pile drained / game-over)
└─ phases: PanIn → [external gate] → Restore
```

- **Phases are the split the level-up needs**: heart-drain runs PanIn→Restore as one
  continuous cinematic; level-up runs PanIn as one cinematic, ends it (popup gate opens),
  and later runs Restore as a second cinematic. The runner exposes both compositions —
  "the return to play is different" is a *phase wiring* difference, not a reason for two
  implementations.
- **One rig, one camera view.** A single thin `CinematicCameraView` MonoBehaviour in the
  scene (`RegisterComponentInHierarchy`) holds the `Camera` reference; the rig is built
  once in DI from it + `OrthogonalSizeCameraController`. Producers stop owning rigs.
- **Producers become plain C# controllers** (repo MVC rule: thin View + plain controller):
  - `HeartDrainCinematic` (rename of `HeartTrailCinematicEffect`): subscribes
    `OverflowHeartRequestedMessage`, focus = `HeartTrailTracker` positions, end =
    game-over ∨ (overflow idle ∧ no trails). Converts **first** — it has no gameplay
    pause, no trail puppeteering, no gate.
  - `LevelUpCinematic` (rename of `LevelUpTrailEffect`): keeps its level-up-specific parts
    (tipping-trail wait/pause/manual advance, `PauseSource.Cinematic`, dismissed-message
    restore trigger) but delegates all camera/slow-mo/restore mechanics to the runner.
    Converts **last** — the trail path is the known-fragile area.
- Trigger guards (`Cinematic.IsPlaying` checks in each producer) centralize as
  `director.TryBegin(state)` → false while busy (drop policy, documented). Producers stop
  re-implementing it.

## Part D — Director lifecycle hardening

Small, orthogonal to A–C:

- `TryBegin(CinematicState)` (concurrency policy in one place, replaces per-producer guards).
- A `CinematicHandle : IDisposable` returned by the director; disposing guarantees
  `EndCinematic` + scene teardown. The `OnDestroy` repair blocks in both producers (end
  cinematic, resume pause, reset timeScale, re-enable ortho) collapse into the runner's
  dispose path — one implementation instead of two drifting copies.
- Keep `CinematicScene`/`CompleteScene` as-is under the hood; the runner is the only
  remaining direct client, so the subtle `CompleteScene` vs `EndCinematic` distinction
  stops leaking into feature code.

## Part E — Camera effects (deferred, name the seam now)

The shake is a **camera effect**, not a cinematic effect — it fires in normal gameplay too
(every heart launch). This session already made it additive (offset delta in `LateUpdate`),
which *is* the composition contract. When a second effect appears (zoom punch, recoil,
vignette), formalize:

- `Display/CameraEffects/` module; each effect contributes an additive offset; a single
  `CameraEffectComposer` applies the sum after all camera writers.
- The `BlocksShake` trait consult stays — with additive composition it's an *aesthetic*
  choice (level-up wants stillness), no longer a technical necessity.

Do **not** build the composer for one effect; this part is a naming/placement decision now
(rename/move when Part C touches `Display/`), machinery later.

## Part F — Time-scale ownership (REQUIRED — usage is enforced)

Three writers race on `Time.timeScale` today. Mirror `PauseService`'s source model:

```
TimeScaleService (plain C#, Lifetime.Singleton)
  Claim(TimeScaleSource source, float value)   // popup freeze = 0, cinematic ramp = curve sample
  Release(TimeScaleSource source)              // value falls back to next claim or 1
  resolution: lowest active claim wins (freeze beats slow-mo beats normal)
```

Cinematics write their per-tick curve sample through a claim; `LevelUpPopUp`'s hard 0 is a
claim; releases replace the three hand-written `Time.timeScale = 1f` restores. This kills
the whole "who forgot to restore" bug class. `IRunResettable` clears all claims on restart.

**Enforcement (decided):** direct `Time.timeScale` writes are banned outside
`TimeScaleService` itself. A new `Tools/style_audit.py` rule flags
`Time.timeScale` assignments (`Time.timeScale =`, `+=`, tween setters like
`x => Time.timeScale = x`) in any other file as `[ERROR]` — same mechanism that enforces
the rest of the conventions, so the pre-commit hook and CI block regressions
automatically. The rule lands **in the same commit** that migrates the three writers
(otherwise the audit blocks every intermediate commit), with a `Tools/test_style_audit.py`
case covering it.

---

## Phasing (each phase ships green: build + audit + tests + playtest)

| Phase | Scope | Size | Risk |
|---|---|---|---|
| 1 | Traits: enum + table + `ICinematicState.Has` + migrate `RunController`/`CameraShakeService` + exhaustiveness test | S | low |
| 2 | `CinematicsSettings` SO + interface + strip tuning from both MonoBehaviours (**editor**: create asset, copy values, wire scope) | S | low — values copied 1:1 |
| 3a | `CinematicCameraView` + DI-owned rig + `ICinematicFocus` (unify Follow APIs) | M | low |
| 3b | `CameraRigCinematic` runner + convert **heart-drain** to plain C# controller | M | medium — playtest the drain |
| 3c | Convert **level-up** onto the runner (split phases; keep trail puppeteering local) | M | **high — fragile trail path; full level-up playtest** |
| 4 | Director `TryBegin` + handle/dispose cleanup | S | low |
| 5 | `TimeScaleService` + migrate 3 writers + **style-audit ban on direct `Time.timeScale` writes** (same commit) | M | medium — popup freeze interplay |
| 6 | (later, on second effect) camera-effect composer | — | deferred |

Order matters: 1 and 2 are independent and immediately useful; 3a→3b→3c builds the
abstraction against the simple case before touching the fragile one; 4 rides along with 3b.
5 can happen any time after 3c (it is **required**, not optional — the audit rule makes
the service the only legal writer). Each phase updates `Game/Cinematics/README.md`.

## Verification

- EditMode: traits table exhaustiveness; `RunControllerTests` migration; runner phase
  logic (plain C# — testable with a substituted rig seam if the rig gets an interface).
- The rig/camera/slow-mo feel **cannot be verified headless** — every phase touching 3x/5
  ends with an in-editor playtest (level-up: full cycle incl. popup + glow trails +
  restore; heart-drain: multi-heart overflow with shake).
- `dotnet build` both csprojs + `style_audit.py` per phase, as usual.

## Open questions (decide before the relevant phase)

1. ~~**Restore spec**~~ **RESOLVED (Part B, third revision):** restores are ordinary
   per-state segments — one `TimeScaleCurve` everywhere, duration = its last key. The
   heart-drain restore reads only the curve's duration/end for now (tween-from-current,
   see the nuance note in Part B); the Part-C runner standardizes ease-from-current.
2. **`CinematicEndGate`** (Phase 1): keep keyed by state (works today) or re-key by trait
   (`GatesUI`)? Leaning: keep by state until a second gated UI exists.
3. **Static `Cinematic` vs DI** (any phase): the static reactive state stays (it predates
   this plan and works); traits table lives beside it. Revisit only if domain-reload-off
   enters the picture. (The `ICinematicAware` listener API was deleted 2026-07-04 —
   nothing ever implemented it; consumers observe `Cinematic.Current`.)
