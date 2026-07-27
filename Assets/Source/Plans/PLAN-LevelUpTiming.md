@page plan_level_up_timing Level-Up Timing

# Level-Up Timing

Replace the level-up ceremony's *gate-before-fire* model with *flag-then-orchestrate*: the moment a
pop's score completes the level requirement, raise a flag, hold the other systems, let the shot play
out its whole flight, and present the popup where the reload would have gone.

**Status:** designed and reviewed (architect + reviewer + test strategist, 2026-07-27), **not
implemented**. Written to be executed by an implementer who does not have the design conversation —
every edit names its file, its insertion point and its code. Items marked **DECISION** are already
answered: do not re-derive them, and do not invent a type, message or controller that is not listed.

---

## 1. The bug this fixes

`LevelController.CheckLevelUp` refuses to fire on four conditions but is re-entered from only two places
(a score-trail arrival, and the pierce falling edge). Two conditions never re-check when they clear, so
a refusal is forgotten permanently:

| Gate | Purpose | Re-checked when it clears? |
|---|---|---|
| `_phase != Playing` | reentrancy | yes |
| `_pierce.IsPiercing` | don't interrupt a plow; don't strand `PendingPierceHits` | yes (falling edge) |
| `_navigation != Game` | don't fire off-screen | **no** |
| `_lossForecast.LossImminent` | don't celebrate mid-death | **no** |

**The compound path.** `LossImminent` is `PendingCharges >= health`; `PendingCharges` counts queued
*rejected* balloons — overflow produced by the spawn that runs at the **end of a shot**. The pierce gate
defers the check to the pierce falling edge, which fires when the shot dies, immediately *before* that
spawn. So: final pops land, trails launch; shot dies, pierce clears, `CheckLevelUp` runs but confirmed
progress isn't complete yet (trails still flying); spawn queues overflow; `LossImminent` goes true; the
last trail arrives, the check is refused, and nothing ever asks again. Reported twice (stuck before the
popup after completing level 12); unreproducible on a clean jump to level 12, which has an empty
overflow queue and full hearts.

**Second, independent candidate, same symptom:** a dropped trail (pool loss, "no target provider")
leaves projected progress complete but confirmed never done. Section 4 removes it by construction.

## 2. The model

```
pop scores -> requirement met (CLAIM time, projected progress)
   |
   +- Phase -> Completing
   +- Window A: exclusive time-scale claim (tipping beat) + camera follows the shot
   +- holds: no spawn wave, no loose pop-spawn, no reload
   |
   +- first wall hit (or death, whichever first) -> release Window A; camera back to normal
   |   framing; rest of the flight plays at NORMAL rules, including its own slow-mos
   |
   +- flight ends (ProjectileDestroyedMessage) -- or the cap fires
        |
        +- Window B: plain time-scale claim + PauseSource.Cinematic + camera restore
        +- Phase -> Pending, publish ScoreLevelUpMessage -> popup
             -> dismissal -> Transitioning (level advances, Ascent)
             -> LevelTransitionCompletedMessage -> Playing + reload
```

The presenting boundary is **end-of-flight**, which always arrives (and 6.3's cap covers the paths where
the shot leaves play another way). A recorded flag consumed at a boundary cannot be forgotten; a refused
check can. That is the entire fix.

**Why it is available:** `ClaimProgress` already caps progress at the requirement and banks the excess
run-scoped (`LevelController.cs:195-211`). Nothing scored after the tipping pop is lost, so nothing has
to be suppressed or hurried.

## 2.1 The signals, by name

| Beat | Signal | Site |
|---|---|---|
| Tipping claim | the `_projectedProgress[color] = baseProgress + granted;` write | `Game/Level/LevelController.cs:210` |
| Window A closes | `WallHitMessage` | published `Projectile/View/ProjectileView.cs:470` |
| End of flight | `ProjectileDestroyedMessage` | published `Projectile/View/ProjectileView.cs:391` |
| Run left gameplay | `INavigation.Current.Value != NavigationState.Game`; `GameOverMessage` | — |
| Ceremony bailed | `LevelUpAbortedMessage` | `LevelUpCinematic.AbortSession` |

Two orderings are load-bearing — **do not "tidy" them**:
- `WallHitMessage` (line 470) publishes BEFORE `ResolvePierceAtBounce` (line 508), so a discharge-tipped
  claim does not close its own Window A on the same bounce.
- The **death** wall takes the `Destroyed` branch and returns at line 463 *without* publishing
  `WallHitMessage`. Window A therefore closes on `WallHitMessage` **or** `ProjectileDestroyedMessage`,
  whichever lands first.

## 3. What this deletes

- The pierce gate and its falling-edge subscription (`LevelController` lines 99-106, the `_pierce`
  field/param). The flight always finishes, so `PendingPierceHits` can never be stranded.
- The loss and navigation gates as *races* (6.3 replaces them with one evaluation at the boundary).
- `AllColorsConfirmed` as a *firing* condition. **Keep the method** — the watchdog dumps it.
- `WillLevelUp()` — four sites: `Game/Level/ILevelProgress.cs:28`, `LevelController.cs:149-171`,
  `Game/Cinematics/LevelUpCinematic.cs:148`, and the doc comment at `Cheats/CheatState.cs:13-14`.
- `ThrowerController.ScaleAwayActiveProjectile` on `LevelUpDismissedMessage` (line 115) — the shot is
  already retired by then (6.5 is what makes this safe). Keep it for game-over.
- In `LevelUpCinematic`: the `PauseSource.Cinematic` pause, and the tipping-trail apparatus
  (`WaitForTippingTrailAsync`, `AdvanceTrackedTrail`, `PanInTimeoutFactor`,
  `TrailRegisterTimeoutFactor`). The camera no longer needs the trail to exist (6.7).

## 4. Detect on projected progress; keep tipping identity as presentation

Detection moves to claim time. The claim is authoritative the instant the pop happens; the trail is
presentation.

- The dropped-trail soft-lock **stops existing**. Worst case is a briefly short bar.
- **DECISION — keep the tipping-trail identity for highlights** (the scale-up, and future effects), but
  flow it FORWARD from the claim instead of reconstructing it backwards from arrivals.
  `ScoreController.cs:246` already receives `(baseProgress, granted)` from `ClaimProgress`, so the pop
  that tips the level is known before its trail exists — mark the trail at spawn. Deterministic by
  construction; if it is ever wrong you lose a highlight, not a run. This is the root of the historical
  bugginess here: one cosmetic signal was carrying correctness weight.
- **DECISION — keep BOTH progress dicts.** `_projectedProgress` (claim-time) and `_levelProgress`
  (confirmed) stop being a correctness mechanism, but the bars still fill against the confirmed value.
  Collapsing them would make bars jump to full at pop time and lose the fill the trails exist to sell.

## 5. The orchestrator IS the existing phase machine

**Do not add a second controller.** Two authorities over this ceremony is the shape of every historical
misfire here (`fe1f8c07`, the straggler misfire). `LevelController` already owns `LevelUpPhase`; add one
value, appended **last** in the enum (nothing serializes it; appending keeps existing values stable):

`Playing -> Completing -> Pending -> Transitioning -> Playing`

`LevelController` gains a `Tick` and ~5 dependencies. That god-class drift is accepted deliberately; if
it worsens later, extract *behind* the phase property, never beside it.

## 6. The edits

### 6.1 TimeScaleService — exclusivity

**New file** `Assets/Source/Shared/Pause/ITimeScaleClaims.cs` (+ `.meta`, mirror a sibling). It exists so
`LevelControllerTests` can substitute it: `TimeScaleService` is `internal sealed` (unfakeable), and a
fixture using the real one leaves the editor's `Time.timeScale` warped for the rest of the session — the
failure class fixed in commit `c4351c90`.

```csharp
namespace BalloonParty.Shared.Pause
{
    /// <summary>Write surface of the time-scale claim stack. Plain-C# controllers depend on this rather
    /// than on <see cref="TimeScaleService" /> itself, so an edit-mode fixture can substitute it instead
    /// of warping the editor's own clock for the rest of the session.</summary>
    internal interface ITimeScaleClaims
    {
        void Claim(TimeScaleSource source, float value);
        void Release(TimeScaleSource source);

        /// <summary>Takes sole ownership: <c>Apply</c> then uses ONLY this source's value, so other
        /// claimants keep recording and resume correctly on <see cref="ReleaseExclusive" />.</summary>
        void ClaimExclusive(TimeScaleSource source, float value);

        void ReleaseExclusive(TimeScaleSource source);
    }
}
```

`TimeScaleSource.cs` — append **last**:

```csharp
        /// <summary>The level-up ceremony's tipping beat and its hand-off to the popup. Claimed
        /// EXCLUSIVELY for the beat so a doomed shot's LastShield curve can't drag it slower than the
        /// ceremony asked for.</summary>
        LevelUpCeremony
```

`TimeScaleService.cs` — declaration becomes
`internal sealed class TimeScaleService : IStartable, IRunResettable, ITimeScaleClaims`, the four claim
methods become `public` (the class stays `internal`), and:

```csharp
        private TimeScaleSource? _exclusiveOwner;

        public void ResetRun(int generation)
        {
            // Dropped with the claims: a reset mid-ceremony would otherwise leave a dead ceremony owning
            // the clock for the rest of the run.
            _exclusiveOwner = null;
            _claims.Clear();
            Apply();
        }

        public void Release(TimeScaleSource source)
        {
            if (_exclusiveOwner == source)
            {
                _exclusiveOwner = null;
            }

            if (_claims.Remove(source))
            {
                Apply();
            }
        }

        // Last exclusive claimant wins rather than queueing: two at once would mean two ceremonies,
        // which the phase machine already forbids.
        public void ClaimExclusive(TimeScaleSource source, float value)
        {
            _exclusiveOwner = source;
            _claims[source] = Mathf.Max(0f, value);
            Apply();
        }

        public void ReleaseExclusive(TimeScaleSource source)
        {
            if (_exclusiveOwner != source)
            {
                return;
            }

            _exclusiveOwner = null;
            _claims.Remove(source);
            Apply();
        }

        private void Apply()
        {
            var scale = 1f;
            if (_exclusiveOwner.HasValue && _claims.TryGetValue(_exclusiveOwner.Value, out var owned))
            {
                // Owner-only: other sources keep their recorded claims (LastShield re-claims every frame
                // from a curve) and resume applying the moment exclusivity ends.
                scale = Mathf.Min(1f, owned);
            }
            else
            {
                foreach (var value in _claims.Values)
                {
                    scale = Mathf.Min(scale, value);
                }
            }

            Time.timeScale = scale;
        }
```

Registration, `Game/GameScopeRegistration.cs:124`:

```csharp
            builder.RegisterEntryPoint<TimeScaleService>().AsSelf().As<ITimeScaleClaims>().As<IRunResettable>();
```

**DECISION — suppress at `Apply`, never cancel other claims.** Cancelling was rejected on evidence:
`LastShield` (`ProjectileDoomedTimeScaleController.Tick`) and `Cinematic` (`CameraRigCinematic` lines
121/164/177) re-claim *every frame* from curves, so releasing them invites the claim back next frame.
`LastShield` is the collision that matters — a doomed shot completing the level on its last pop is a
likely pairing. Suppressing it is safe: the doomed drift eases over normalized GAME time
(`ProjectileMotionResolver.cs:50`), so muting the claim changes its wall-clock length, never its shape.

### 6.2 The two windows

| Window | Claim | Value | Ends on |
|---|---|---|---|
| **A — tipping beat** | `ClaimExclusive(LevelUpCeremony, v)`, re-issued each `Tick` | `ICinematicsSettings.EntryOf(CinematicState.LevelUpPanIn).Rig.TimeScaleCurve` sampled at `unscaledElapsed / curve.Duration()` — already authored for this beat, **no new config field** | first `WallHitMessage`, or `ProjectileDestroyedMessage` if the shot dies first |
| between | none | normal rules; `LastShield`/`PierceDischarge` apply again | — |
| **B — hand-off** | plain `Claim(LevelUpCeremony, v)` — deliberately **NOT** exclusive | that curve's END value | `LevelUpDismissedMessage` -> `Release` |

**DECISION — Window B must be plain.** `LevelUpPopUp` claims `LevelUpPopup = 0` only after
`await _gate.WaitAsync` (`UI/LevelUp/LevelUpPopUp.cs:95-99`). Exclusivity across that instant would
suppress the popup's own freeze (board moving behind the popup); releasing a frame early would snap the
world to full speed. `min(beat, 0) = 0` makes the hand-off automatic.

### 6.3 LevelController — the exact edits

It becomes `ITickable`; **no registration change** — `RegisterEntryPoint<LevelController>()`
(`GameScopeRegistration.cs:180`) picks up entry-point interfaces automatically.

**Delete:** `_pierceEndedSubscription` (field, `Start()` block lines 99-106, `Dispose()` line); the
`_pierce` field + ctor param; `WillLevelUp()`; the pierce/nav/loss guards inside `CheckLevelUp`
(lines 302-313) and `CheckLevelUp` itself. **Keep** `AllColorsConfirmed` (watchdog).

**Add** to the top field block, in the project's field order:

```csharp
        private const float CompletingCapSeconds = 8f;

        private readonly ITimeScaleClaims _timeScale;
        private readonly ICinematicsSettings _cinematics;
        private readonly IPublisher<LevelUpAbandonedMessage> _abandonedPublisher;
        private readonly ISubscriber<WallHitMessage> _wallHitSubscriber;
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private readonly ISubscriber<GameOverMessage> _gameOverSubscriber;

        private IDisposable _wallHitSubscription;
        private IDisposable _destroyedSubscription;
        private IDisposable _gameOverSubscription;
        private AnimationCurve _beatCurve;
        private float _completingElapsed;
        private bool _windowAOpen;
```

**`Start()` additions:**

```csharp
            _beatCurve = _cinematics.EntryOf(CinematicState.LevelUpPanIn).Rig.TimeScaleCurve;
            _wallHitSubscription = _wallHitSubscriber.Subscribe(_ => CloseWindowA());
            _destroyedSubscription = _destroyedSubscriber.Subscribe(_ => OnFlightEnded());
            _gameOverSubscription = _gameOverSubscriber.Subscribe(_ => AbandonCeremony("game over"));
```

**Detection — inside `ClaimProgress`, replacing lines 210-211:**

```csharp
            _projectedProgress[color] = baseProgress + granted;

            // Detect on PROJECTED progress: the claim is authoritative the instant the pop happens, so a
            // lost trail can no longer withhold the ceremony. The BlockLevelUp cheat returns at line 187,
            // before this, so it still cannot complete a level.
            TryBeginCompleting();

            return (baseProgress, granted);
```

**New private methods**, at the bottom after `OnLevelUpAborted`:

```csharp
        private void TryBeginCompleting()
        {
            if (_phase.Value != LevelUpPhase.Playing)
            {
                return;
            }

            var required = _thresholds.PointsRequiredForLevel(_level.Value);
            foreach (var color in _levelParams.Current.AllowedColors)
            {
                if (_projectedProgress.GetValueOrDefault(color) < required)
                {
                    return;
                }
            }

            _phase.Value = LevelUpPhase.Completing;
            _completingElapsed = 0f;
            _windowAOpen = true;
            _timeScale.ClaimExclusive(TimeScaleSource.LevelUpCeremony, _beatCurve.Evaluate(0f));

            Log.Info("Level", $"Level {_level.Value} completed at claim time — holding for end of flight");
        }

        public void Tick()
        {
            if (_phase.Value != LevelUpPhase.Completing)
            {
                return;
            }

            // Unscaled: this beat is warping the very clock the ramp is measured against.
            _completingElapsed += Time.unscaledDeltaTime;

            if (_windowAOpen)
            {
                var duration = _beatCurve.Duration();
                var t = duration > 0f ? Mathf.Clamp01(_completingElapsed / duration) : 1f;
                _timeScale.ClaimExclusive(TimeScaleSource.LevelUpCeremony, _beatCurve.Evaluate(t));
            }

            if (_completingElapsed >= CompletingCapSeconds)
            {
                // The boundary never arrived: the shot left play without dying (game over, board clear),
                // or the tipping claim came from a delayed source (chain lightning, a cheat) with nothing
                // in flight. Present anyway rather than soft-lock the run.
                Log.Warn("Level", $"Completing timed out after {CompletingCapSeconds:0.#}s");
                OnFlightEnded();
            }
        }

        private void CloseWindowA()
        {
            if (_phase.Value != LevelUpPhase.Completing || !_windowAOpen)
            {
                return;
            }

            // Hand the clock back completely: the rest of the flight plays at normal rules, including its
            // own slow-mos (a pierce discharge dip, a doomed last breath).
            _windowAOpen = false;
            _timeScale.ReleaseExclusive(TimeScaleSource.LevelUpCeremony);
        }

        private void OnFlightEnded()
        {
            if (_phase.Value != LevelUpPhase.Completing)
            {
                return;
            }

            // The run leaving gameplay wins over the level it completed — evaluated ONCE, here, instead of
            // racing a forecast. LossImminent is deliberately NOT the test: it is a prediction, and gating
            // on it is the bug this plan removes.
            if (_navigation.Current.Value != NavigationState.Game)
            {
                AbandonCeremony("run left gameplay");
                return;
            }

            _windowAOpen = false;
            _timeScale.ReleaseExclusive(TimeScaleSource.LevelUpCeremony);
            // Plain, not exclusive: the popup's own freeze (0) must win by the minimum rule.
            _timeScale.Claim(TimeScaleSource.LevelUpCeremony, _beatCurve.Evaluate(_beatCurve.Duration()));

            var completedColors = _levelParams.Current.AllowedColors;
            _phase.Value = LevelUpPhase.Pending;
            _pendingNewLevel = _level.Value + 1;

            Log.Info("Level", $"Level-up presented → pending level {_pendingNewLevel} " +
                $"(colors completed: {string.Join(", ", completedColors)})");

            _levelUpPublisher.Publish(new ScoreLevelUpMessage(_pendingNewLevel, completedColors));
            _navigation.TransitionTo(NavigationState.LevelUp);
        }

        // Leaves Completing without a ceremony. Progress stays complete so nothing re-detects — fine,
        // because every path here means the run is ending or restarting.
        private void AbandonCeremony(string reason)
        {
            if (_phase.Value != LevelUpPhase.Completing)
            {
                return;
            }

            Log.Warn("Level", $"Level-up abandoned ({reason})");

            _windowAOpen = false;
            ReleaseCeremonyTimeScale();
            _phase.Value = LevelUpPhase.Playing;
            _abandonedPublisher.Publish(new LevelUpAbandonedMessage());
        }

        private void ReleaseCeremonyTimeScale()
        {
            _timeScale.ReleaseExclusive(TimeScaleSource.LevelUpCeremony);
            _timeScale.Release(TimeScaleSource.LevelUpCeremony);
        }
```

Also: `OnTrailArrived` no longer calls `CheckLevelUp`, and **must allow `Completing`** — change its guard
at line 259 from `!= Playing` to `== Pending || == Transitioning`, so confirmed progress keeps advancing
while the flight plays out (the bars depend on it, 6.6). `OnLevelUpDismissed` gains
`ReleaseCeremonyTimeScale();` as its first statement. `OnLevelUpAborted` accepts `Completing` too,
routing it to `AbandonCeremony("cinematic aborted")`. `ClearRunState` gains
`_windowAOpen = false; _completingElapsed = 0f; ReleaseCeremonyTimeScale();`. `Dispose` gains
`ReleaseCeremonyTimeScale();` plus the three new subscription disposals.

### 6.4 New message: LevelUpAbandonedMessage

Without it, an abandoned ceremony leaves the thrower with **no projectile and no path that will ever load
one** (the destroy-time load was held, and `LevelTransitionCompletedMessage` never comes) — a soft-lock
on exactly the loss path this plan is meant to make safe.

`Assets/Source/Shared/Messages/LevelUpAbandonedMessage.cs` (+ `.meta`):

```csharp
namespace BalloonParty.Shared.Messages
{
    /// <summary>The ceremony left Completing without presenting — the run is ending or restarting.
    /// Whoever held something for it (the thrower's reload) releases it here.</summary>
    public readonly struct LevelUpAbandonedMessage
    {
    }
}
```

Register beside the other level-up brokers (~`GameScopeRegistration.cs:79`):
`builder.RegisterMessageBroker<LevelUpAbandonedMessage>(options);`

### 6.5 The holds — predicate is `Phase != Playing`, NEVER `== Completing`

**DECISION, not optional.** `Completing -> Pending` happens *inside* the `ProjectileDestroyedMessage`
dispatch, and the subscribers live in two lifetime scopes (`BalloonSpawner` + `LevelController` in
`GameScopeRegistration`, `ThrowerController` in `ThrowerLifetimeScope`), so dispatch order cannot be
reasoned about. `== Completing` is a coin flip that spawns the cancelled wave or reloads the held shot.
Three independent reviewers converged on this.

| System | Exact edit |
|---|---|
| **Spawn wave** | `BalloonSpawner.OnProjectileDestroyed` (`Balloon/Spawner/BalloonSpawner.cs:186`) — first statement, BEFORE `_turnCount++`: `if (_levelProgress.Phase.Value != LevelUpPhase.Playing) { return; }`. The turn is cancelled, so it must not count toward `FirstSpawnTurn`. |
| **Loose pop-spawns** | `BalloonSpawner.OnActorHit` (~line 199) — same guard, first statement. |
| Injection | `BalloonSpawner` already has `using BalloonParty.Game.Level;`; add an `ILevelProgress _levelProgress` ctor param + field. |
| Balance pulses | allowed; the board is live while the shot flies. |
| Overflow/hearts | cancelled with the spawn. **A completed level wins over the overflow it would have caused**; a loss already committed before the tipping claim keeps priority via 6.3's nav check. |

**Reload.** `ThrowerController.Start` line 110 subscribes destroy -> `SwapActiveProjectile()`, which is
`ScaleAwayActiveProjectile()` **+** `LoadProjectile()` (lines 231-235). `ScaleAwayActiveProjectile` is the
ONLY path that retires a spent shot (`PlayDisappear` + `_poolManager.Return`), so skipping the whole swap
parks a dead projectile on the board through the ceremony and Ascent and leaks it from the pool. Replace
the subscription body with:

```csharp
            _destroyedSubscriber.Subscribe(_ =>
            {
                ScaleAwayActiveProjectile();

                // A level-up presents itself where this reload would have gone; the fresh shot arrives
                // with the new level, on LevelTransitionCompletedMessage (already wired below).
                if (_levelProgress.Phase.Value == LevelUpPhase.Playing)
                {
                    LoadProjectile();
                }
            }).AddTo(_subscriptions);

            // Completing was abandoned (run ending/restarting), so the skipped reload happens now or the
            // thrower stays empty forever.
            _levelUpAbandonedSubscriber.Subscribe(_ => LoadProjectile()).AddTo(_subscriptions);
```

Add `using BalloonParty.Game.Level;`, an `ILevelProgress` ctor param, and an
`ISubscriber<LevelUpAbandonedMessage>` ctor param. Leave `UnfireIfNeverFlown` (line 120) alone — dead
under the new model but harmless and guarded.

### 6.6 ColorProgressBar must keep drawing during Completing

`UI/Score/ColorProgressBar.cs:128` is
`private bool LevelUpInProgress => _levelProgress.Phase.Value != LevelUpPhase.Playing;` and
`OnTrailArrived` (~line 545) bails on it. Left as-is, every confirming trail that lands during the flight
is swallowed and the popup appears over visibly short bars. Change to:

```csharp
        private bool LevelUpInProgress => _levelProgress.Phase.Value == LevelUpPhase.Pending
                                          || _levelProgress.Phase.Value == LevelUpPhase.Transitioning;
```

### 6.7 The pan-in stops pausing; the camera follows the shot

**DECISION.** `LevelUpCinematic.BeginCinematicWithTrail` calls `_pauseService.Pause(PauseSource.Cinematic)`
(`Game/Cinematics/LevelUpCinematic.cs:213`) and `ProjectileView.FixedUpdate` returns early on
`IsAnyPaused` (`ProjectileView.cs:185`). Since the pan-in arms at the tipping claim, leaving that pause in
place means the shot **stops**, never reaches a wall, never dies — `ProjectileDestroyedMessage` never
arrives and **every level-up hard-locks**. Do this exactly:

1. **Delete** the `Pause(PauseSource.Cinematic)` at line 213 and its matching `Resume` in `OnDismissed`
   (line 258). Keep the defensive `Resume` in `OnDispose` (121-124) and `AbortSession` (337-340) —
   `PauseService.Resume` is a no-op at zero depth.
2. **The only pause is now at the boundary**, claimed alongside Window B in `OnFlightEnded` and released
   at dismissal. There the projectile is gone and spawn/reload are held, so its remaining job is keeping
   pause-gated systems (balancer, overflow effect) still behind the popup. One claim, one matched
   release — not an apparatus.
3. **Arm condition:** replace line 148's `if (!_levelProgress.WillLevelUp())` with
   `if (_levelProgress.Phase.Value != LevelUpPhase.Completing)`. Delete the nav/loss guard (line 135),
   the pierce guard (143), and the nav/loss re-check at 206 (keep `!Runner.TryBegin()`).
4. **Keep** `PanInTick`'s `LossImminent` abort (272-276): 6.3's `OnLevelUpAborted` now accepts
   `Completing`, so an abort during the beat is a clean abandon.
5. **Camera target is the PROJECTILE, not the tipping trail.** `ProjectilePositionProvider` (already
   maintained by `ThrowerController.Set/Clear`, lines 210/246) is the live handle — no puppeteering, no
   waiting for a trail to register. Follow it for **Window A only** (tipping claim -> first wall) so
   camera and clock share one boundary, then release to normal framing for the rest of the flight.
   **DECISION — do NOT pan to the score bars.** They fill during the flight, so there is nothing left to
   show there, and the camera is a world-space instrument while the bars are UI. The out-pan is simply the
   rig's existing restore, run at end-of-flight before the popup, gated by the `CinematicEndGate` that
   `LevelUpPopUp` already awaits (`LevelUpPopUp.cs:97`).
6. This removes the camera-park problem: `CameraRigCinematic.EndPanIn` currently leaves the camera zoomed
   for a later restore, which would have played the whole remaining flight framed away from the shot. With
   the follow model the camera's last act *is* the restore.

## 7. Tests

Only what is actually writable. `BalloonSpawner` and `ThrowerController` have **no** EditMode fixture and
need `SlotGrid`/pools/a `ThrowerView` MonoBehaviour — do not build scaffolding for them. Verify those
holds by reading plus the in-editor playtest, and say so rather than claiming coverage.

**`Assets/Tests/EditMode/Shared/TimeScaleServiceTests.cs`** (existing; keep its `Time.timeScale = 1f`
`TearDown`):
- an exclusive claim ignores a lower competing claim (0.6 wins over a 0.25 `LastShield`);
- `ReleaseExclusive` restores the minimum of the still-recorded claims, not `1f`;
- a `Claim` arriving *while* exclusivity holds is still recorded, and applies after release;
- `Release` on the exclusive owner clears the owner too;
- `ResetRun` clears the owner — a later plain claim applies normally.

**`Assets/Tests/EditMode/Game/LevelControllerTests.cs`** — `BuildController` (~line 110) gains
`Substitute.For<ITimeScaleClaims>()`, `Substitute.For<ICinematicsSettings>()` (its entry returning
`Rig.TimeScaleCurve = AnimationCurve.Constant(0f, 0.3f, 0.5f)`), a publisher substitute for
`LevelUpAbandonedMessage`, and three handler captures (`WallHitMessage`, `ProjectileDestroyedMessage`,
`GameOverMessage`) built exactly like the four existing ones. Add a `FireFlightEnded()` helper. **Do not**
hand it a real `TimeScaleService` (see 6.1).
- tipping claim -> `Completing`, `ClaimExclusive` called, nothing published yet;
- `WallHitMessage` during `Completing` -> `ReleaseExclusive`, phase unchanged;
- `ProjectileDestroyedMessage` -> `Pending`, `ScoreLevelUpMessage` published **exactly once**;
- a second `ProjectileDestroyedMessage` publishes nothing more;
- `ProjectileDestroyedMessage` while merely `Playing` does nothing (every ordinary shot fires it — this is
  the single most important new guard);
- a trail arriving during `Completing` neither re-fires nor changes the phase;
- **dropped trail:** claims with no arrivals still reach `Pending` at end-of-flight;
- **THE PINNED REGRESSION:** `LossImminent` false at the tipping claim, then true before end-of-flight ->
  still reaches `Pending` and publishes. This is the reported bug;
- nav already `GameOver` at end-of-flight -> back to `Playing`, `LevelUpAbandonedMessage` published, no
  `ScoreLevelUpMessage`;
- the cap: `Completing` entered, no flight-end, `Tick` past `CompletingCapSeconds` -> presents;
- stray `Dismiss`/`Abort` while `Completing` behave per 6.3.

**Existing tests needing a mechanical change:** every test that scores to completion then asserts on
dismissal/publication needs `FireFlightEnded();` inserted after the last `ScoreColor(...)` and before the
first assertion — including `LevelUpDismissed_AdvancesLevel`, `LevelUp_PublishesCompletedColorsSnapshot`,
`Detection_WhileTransitioning_DoesNotFire`, the `LevelUpDismissed_Resets*` group,
`ExcessBank_AccumulatesAcrossLevels`, the nav-state group, and
`Abort_WhilePending_ResetsPhaseAndNavToGame`. `Phase_CyclesThroughTheCeremony` needs a behavioural rewrite
to insert the `Completing` stage.

**Delete outright** (they assert the pierce gate, a mechanism this plan removes — there is no behaviour to
redirect them to; leave a one-line comment saying why): `CheckLevelUp_WhilePiercing_HoldsCommit`,
`PierceEnded_WithRequirementMet_PublishesOnceAndGoesPending`,
`NotPiercing_ConfirmingArrival_FiresImmediately`, `PierceEnded_RequirementNotMet_DoesNotPublish`,
`MultipleArrivalsDuringPierce_ThenPierceEnds_PublishesOnce`, `ClaimProgress_WhilePiercing_StillBanksExcess`,
`PierceEnded_WhenLossImminent_DoesNotPublish`, `PierceEnded_WhenNotInGame_DoesNotPublish`,
`PierceEnded_WhileAlreadyPending_DoesNotRePublish`, `PierceEnded_AfterDispose_DoesNotFireOrThrow`.

**Invert, do not delete:** `LevelUp_WhenLossImminent_DoesNotLevelUp` — its current contract *is* the bug.
Flip the assertion with a comment explaining why it inverted.

**Not worth testing:** the watchdog log; `LevelUpCinematic`'s own gate changes (already Deferred per
`Assets/Tests/README.md`; its PlayMode smoke test stays); the `ScaleAwayActiveProjectile` deletion
(provably inert); `PendingPierceHits` discharge (untouched, already covered by
`ProjectileMotionResolverTests`/`ProjectileHitResolverTests`).

## 8. Sequencing

1. **Time-scale exclusivity** — `ITimeScaleClaims`, the new `TimeScaleSource` member, the `Apply` rewrite,
   registration, `TimeScaleServiceTests`. Nothing observes it yet; safe alone.
2. **`Completing`, end to end, in ONE commit** — the phase value, claim-time detection, the boundary, the
   abandon path + `LevelUpAbandonedMessage`, the `CompletingCapSeconds` cap, the four gates removed, **and
   6.7's pan-in de-pausing**. Splitting any of these leaves the game soft-locked between commits: the
   phase alone freezes every shot; the boundary alone has no fallback.
3. **The holds** — spawner, thrower, `ColorProgressBar`, `LevelController.OnTrailArrived`.
4. **The camera follow** (6.7 item 5) — feel work, isolated, playtest-driven.
5. **Delete the dead paths** — `WillLevelUp` (all four sites), the pierce subscription,
   `ScaleAwayActiveProjectile` on dismissal, the tipping-trail apparatus.
6. **Docs** — `Game/Level/README.md` (it documents the model being replaced),
   `Game/Cinematics/README.md`, and the `Cheats/CheatState.cs` doc comment.

After every step: `dotnet build BalloonParty.Runtime.csproj -nologo -clp:ErrorsOnly` (plus
`BalloonParty.Tests.EditMode.csproj` from step 1, and `BalloonParty.Tests.PlayMode.csproj` whenever a
runtime interface gains a member) and `python3 Tools/style_audit.py`. **The implementer cannot run the
tests** — there is no headless Unity runner in that environment. Stop after each numbered step and hand
back for an in-editor run; a green build proves very little about this change. Do not report a step as
verified on a compile alone.

## 9. Risks — playtest, not compile

- **The played-out flight scores nothing visible.** A capped claim returns `granted == 0` and
  `ScoreController.ResolveAttributions` skips those (`ScoreController.cs:247`) — no trail, no point
  notice, no score tick. Points still bank, but the player pops balloons for *seconds* with no reward.
  Today that window is milliseconds. Watch for this specifically; it is a different complaint from "dead
  air", and it may argue for letting capped pops still fly a (non-progress) trail.
- **A long armed flight delays the popup** by its whole duration (taps accrue all flight and banked Snipe
  charges re-arm the lance). If it reads as dead air, lower `CompletingCapSeconds`; do not go back to
  interrupting.
- **Cancelling the tipping shot's spawn wave** is a balance change: one fewer wave per level-up.
- **The follow camera** on a shot crossing the box in ~0.2s and bouncing several times a second may be
  unpleasant. Window A's slow-mo covers exactly that span, which is why the two match. If the whole flight
  is wanted instead, ease the framing out after the wall rather than tracking tightly.

## 10. Known residual (scoped, cosmetic)

**Trail-to-bar mismatch under a moving camera.** Score trails fly world-space toward UI bars; with the
camera now moving during the flight, a trail that baked its target at launch will drift or overshoot.
Previously hidden because the world was frozen and the trail was puppeteered. First thing to check:
whether `ScoreTrailService`'s `TrailSpawner` homes on a **Transform** (may already be correct, just
needing damping) or a **cached Vector3** (needs per-frame re-projection). Fix on its own schedule — it is
cosmetic by construction now, because progression is decided at claim time. That is the point of the split
in section 4: the same misbehaving trail used to be able to withhold a ceremony or strand a run.

## 11. Keep the watchdog regardless

Dev-only. When progress is complete and the phase has not moved within ~2s, `Log.Warn` the phase,
navigation, `LossImminent`, `AllColorsConfirmed` per colour, and whether a projectile is in flight. This
bug class is rare, unreproducible on demand, and cost a full session to diagnose from a description. The
next occurrence should name its own cause.
