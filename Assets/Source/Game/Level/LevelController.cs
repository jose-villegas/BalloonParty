using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Cinematics;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Health;
using BalloonParty.Game.Run;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Game.Level
{
    /// <summary>
    ///     Owns the player's progression through levels — current level, per-colour progress, and the
    ///     level-up trigger.
    /// </summary>
    internal sealed class LevelController : IStartable, ITickable, IDisposable, IRunResettable, ILevelProgress
    {
        private const float CompletingCapSeconds = 8f;

        private readonly IActiveLevelParameters _levelParams;
        private readonly ILevelThresholds _thresholds;
        private readonly IGamePalette _palette;
        private readonly INavigation _navigation;
        private readonly ILossForecast _lossForecast;
        private readonly IRetryState _retryState;
        private readonly ITimeScaleClaims _timeScale;
        private readonly ICinematicsSettings _cinematics;
        private readonly IPublisher<ScoreLevelUpMessage> _levelUpPublisher;
        private readonly IPublisher<LevelUpAbandonedMessage> _abandonedPublisher;
        private readonly IPublisher<ForceDestroyProjectileMessage> _forceDestroyPublisher;
        private readonly ISubscriber<ScoreTrailArrivedMessage> _trailArrivedSubscriber;
        private readonly ISubscriber<LevelUpAbortedMessage> _abortedSubscriber;
        private readonly ISubscriber<LevelUpDismissedMessage> _dismissedSubscriber;
        private readonly ISubscriber<LevelTransitionCompletedMessage> _transitionCompletedSubscriber;
        private readonly ISubscriber<WallHitMessage> _wallHitSubscriber;
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private readonly ISubscriber<BoardDepletedMessage> _boardDepletedSubscriber;
        private readonly ISubscriber<GameOverMessage> _gameOverSubscriber;

        private readonly ReactiveProperty<int> _level = new(1);
        private readonly ReactiveProperty<LevelUpPhase> _phase = new(LevelUpPhase.Playing);
        private readonly Dictionary<string, int> _levelProgress = new();
        private readonly Dictionary<string, int> _projectedProgress = new();
        private readonly Dictionary<string, int> _bankedExcess = new();
        private readonly List<string> _colorKeys = new();

        private IDisposable _trailSubscription;
        private IDisposable _abortedSubscription;
        private IDisposable _dismissedSubscription;
        private IDisposable _transitionCompletedSubscription;
        private IDisposable _wallHitSubscription;
        private IDisposable _destroyedSubscription;
        private IDisposable _boardDepletedSubscription;
        private IDisposable _gameOverSubscription;
        private AnimationCurve _beatCurve;
        private float _completingElapsed;
        private bool _windowAOpen;

        // The target level for the deferred increment; applied when the popup is dismissed.
        private int _pendingNewLevel;

        public LevelController(
            IActiveLevelParameters levelParams,
            ILevelThresholds thresholds,
            IGamePalette palette,
            INavigation navigation,
            ILossForecast lossForecast,
            IRetryState retryState,
            ITimeScaleClaims timeScale,
            ICinematicsSettings cinematics,
            IPublisher<ScoreLevelUpMessage> levelUpPublisher,
            IPublisher<LevelUpAbandonedMessage> abandonedPublisher,
            IPublisher<ForceDestroyProjectileMessage> forceDestroyPublisher,
            ISubscriber<ScoreTrailArrivedMessage> trailArrivedSubscriber,
            ISubscriber<LevelUpAbortedMessage> abortedSubscriber,
            ISubscriber<LevelUpDismissedMessage> dismissedSubscriber,
            ISubscriber<LevelTransitionCompletedMessage> transitionCompletedSubscriber,
            ISubscriber<WallHitMessage> wallHitSubscriber,
            ISubscriber<ProjectileDestroyedMessage> destroyedSubscriber,
            ISubscriber<BoardDepletedMessage> boardDepletedSubscriber,
            ISubscriber<GameOverMessage> gameOverSubscriber)
        {
            _levelParams = levelParams;
            _thresholds = thresholds;
            _palette = palette;
            _navigation = navigation;
            _lossForecast = lossForecast;
            _retryState = retryState;
            _timeScale = timeScale;
            _cinematics = cinematics;
            _levelUpPublisher = levelUpPublisher;
            _abandonedPublisher = abandonedPublisher;
            _forceDestroyPublisher = forceDestroyPublisher;
            _trailArrivedSubscriber = trailArrivedSubscriber;
            _abortedSubscriber = abortedSubscriber;
            _dismissedSubscriber = dismissedSubscriber;
            _transitionCompletedSubscriber = transitionCompletedSubscriber;
            _wallHitSubscriber = wallHitSubscriber;
            _destroyedSubscriber = destroyedSubscriber;
            _boardDepletedSubscriber = boardDepletedSubscriber;
            _gameOverSubscriber = gameOverSubscriber;
        }

        public IReadOnlyReactiveProperty<int> Level => _level;
        public IReadOnlyReactiveProperty<LevelUpPhase> Phase => _phase;

        // Re-resolves after grid/gameplay state clears; same stage as score reset.
        public int ResetOrder => RunResetOrder.Score;

        public void Start()
        {
            _colorKeys.AddRange(_palette.ProgressColorNames);
            ClearRunState();

            _beatCurve = _cinematics.EntryOf(CinematicState.LevelUpPanIn).Rig.TimeScaleCurve;
            _trailSubscription = _trailArrivedSubscriber.Subscribe(OnTrailArrived);
            _abortedSubscription = _abortedSubscriber.Subscribe(_ => OnLevelUpAborted());

            // Level and progress advance only once the player dismisses the popup (Pending → Transitioning),
            // and scoring reopens only once the Ascent reports it has settled (Transitioning → Playing).
            _dismissedSubscription = _dismissedSubscriber.Subscribe(_ => OnLevelUpDismissed());
            _transitionCompletedSubscription = _transitionCompletedSubscriber.Subscribe(_ => OnTransitionCompleted());

            _wallHitSubscription = _wallHitSubscriber.Subscribe(_ => CloseWindowA());
            _destroyedSubscription = _destroyedSubscriber.Subscribe(_ => OnFlightEnded());
            _boardDepletedSubscription = _boardDepletedSubscriber.Subscribe(_ => OnBoardDepleted());
            _gameOverSubscription = _gameOverSubscriber.Subscribe(_ => AbandonCeremony("game over"));
        }

        public void ResetRun(int generation)
        {
            ClearRunState();
        }

        public void Dispose()
        {
            ReleaseCeremonyTimeScale();
            _trailSubscription?.Dispose();
            _abortedSubscription?.Dispose();
            _dismissedSubscription?.Dispose();
            _transitionCompletedSubscription?.Dispose();
            _wallHitSubscription?.Dispose();
            _destroyedSubscription?.Dispose();
            _boardDepletedSubscription?.Dispose();
            _gameOverSubscription?.Dispose();
        }

        public int GetProgress(string colorName)
        {
            return _levelProgress.GetValueOrDefault(colorName);
        }

        public int ExcessPoints(string color)
        {
            return _bankedExcess.GetValueOrDefault(color);
        }

        public int TotalExcessPoints()
        {
            var total = 0;
            foreach (var key in _colorKeys)
            {
                total += _bankedExcess.GetValueOrDefault(key);
            }

            return total;
        }

        public int GetRequiredPoints()
        {
            return _thresholds.PointsRequiredForLevel(_level.Value);
        }

        public bool WillLevelUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE
            // Dev cheat (BlockLevelUpCheat): report "not levelling up" so the projected level-up cinematic
            // (which gates on this) never starts — the earliest blocking state, before the ceremony.
            if (BalloonParty.Cheats.CheatState.BlockLevelUp)
            {
                return false;
            }
#endif

            var required = _thresholds.PointsRequiredForLevel(_level.Value);

            foreach (var color in _levelParams.Current.AllowedColors)
            {
                if (_projectedProgress.GetValueOrDefault(color) < required)
                {
                    return false;
                }
            }

            return true;
        }

        public (int baseProgress, int granted) ClaimProgress(string color, int points)
        {
            if (string.IsNullOrEmpty(color) || !_projectedProgress.ContainsKey(color))
            {
                return (0, 0);
            }

            var baseProgress = _projectedProgress[color];

#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE
            // Dev cheat (BlockLevelUpCheat) — level lock: grant the points for the VISUAL (so score trails still
            // fly on a pop) but DON'T advance progress — no projected mutation here, and both OnTrailArrived
            // handlers skip their commit, so score, bars and level all stay put while the trails play. Not
            // real progress, so nothing banks either.
            if (BalloonParty.Cheats.CheatState.BlockLevelUp)
            {
                return (baseProgress, points);
            }
#endif

            // Cap one level-up per burst — the excess past this level's requirement is dropped from progress
            // but banked run-scoped (see _bankedExcess) for a future per-level currency system to spend.
            var required = _thresholds.PointsRequiredForLevel(_level.Value);
            var granted = Mathf.Min(points, Mathf.Max(0, required - baseProgress));
            var overflow = points - granted;
            if (overflow > 0)
            {
                _bankedExcess[color] = _bankedExcess.GetValueOrDefault(color) + overflow;
                Log.Info("LevelController", $"Banked {overflow} excess {color} " +
                    $"(colour bank {_bankedExcess[color]}, run bank {TotalExcessPoints()})");
            }

            if (granted <= 0)
            {
                return (baseProgress, 0);
            }

            _projectedProgress[color] = baseProgress + granted;

            // Detect on PROJECTED progress: the claim is authoritative the instant the pop happens, so a
            // lost trail can no longer withhold the ceremony. The BlockLevelUp cheat returns at the guard
            // above, before this, so it still cannot complete a level.
            TryBeginCompleting();

            return (baseProgress, granted);
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
                // Ultimate failsafe: force-destroy the projectile through the canonical death path, then
                // fall through to OnFlightEnded (which is a no-op if the force-destroy already triggered
                // it via DestroyedMessage). If no projectile exists, the force-destroy is inert and the
                // direct OnFlightEnded presents the popup.
                Log.Warn("Level", $"Completing timed out after {CompletingCapSeconds:0.#}s — force-destroying");
                _forceDestroyPublisher.Publish(default);
                OnFlightEnded();
            }
        }

        private void OnBoardDepleted()
        {
            if (_phase.Value != LevelUpPhase.Completing)
            {
                return;
            }

            // Nothing left to hit — end the flight. If a projectile exists, the synchronous destroy
            // chain already calls OnFlightEnded via DestroyedMessage (making the direct call a no-op).
            // If none exists, the direct call is the only presenter.
            Log.Info("Level", "Board depleted during Completing — force-destroying projectile");
            _forceDestroyPublisher.Publish(default);
            OnFlightEnded();
        }

        private void ClearRunState()
        {
            var startLevel = _retryState.RetryLevel > 0 ? _retryState.RetryLevel : 1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE
            if (_retryState.RetryLevel <= 0)
            {
                startLevel = Mathf.Max(1, BalloonParty.Cheats.CheatState.StartLevel);
            }
#endif
            _level.Value = startLevel;
            _phase.Value = LevelUpPhase.Playing;
            _pendingNewLevel = 0;
            _windowAOpen = false;
            _completingElapsed = 0f;
            ReleaseCeremonyTimeScale();
            // The excess bank is run-scoped — cleared here (fresh run), but NOT at level-up, where it keeps
            // accumulating across the run.
            _bankedExcess.Clear();
            ResetColorProgress();
        }

        private void ResetColorProgress()
        {
            foreach (var key in _colorKeys)
            {
                _levelProgress[key] = 0;
                _projectedProgress[key] = 0;
            }
        }

        private void OnTrailArrived(ScoreTrailArrivedMessage msg)
        {
            if (!_levelProgress.ContainsKey(msg.ColorName))
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE
            // Level lock (BlockLevelUpCheat): the trail still arrived (and played), but don't confirm progress
            // or check for a level-up — the level stays where it was.
            if (BalloonParty.Cheats.CheatState.BlockLevelUp)
            {
                return;
            }
#endif

            // Allow confirming progress during Completing (bars must keep filling while the flight plays
            // out) but reject during Pending/Transitioning — those trails belong to the finished level.
            if (_phase.Value == LevelUpPhase.Pending || _phase.Value == LevelUpPhase.Transitioning)
            {
                return;
            }

            // Capped at this level's claim so a previous-level straggler can't re-inflate progress.
            var confirmable = Math.Min(msg.Score, _projectedProgress[msg.ColorName]);
            _levelProgress[msg.ColorName] = Math.Max(_levelProgress[msg.ColorName], confirmable);
        }

        private bool AllColorsConfirmed(int required)
        {
            foreach (var color in _levelParams.Current.AllowedColors)
            {
                if (_levelProgress.GetValueOrDefault(color) < required)
                {
                    return false;
                }
            }

            return true;
        }

        // Pending → Transitioning: the player dismissed the popup, so the level and progress advance now
        // and the Ascent (which watches Phase) kicks off.
        private void OnLevelUpDismissed()
        {
            if (_phase.Value != LevelUpPhase.Pending)
            {
                return;
            }

            ReleaseCeremonyTimeScale();
            _level.Value = _pendingNewLevel;
            ResetColorProgress();
            _phase.Value = LevelUpPhase.Transitioning;

            Log.Info("Level", $"Level {_level.Value} confirmed — progress reset, transitioning");
        }

        // Transitioning → Playing: the Ascent has settled, so scoring reopens.
        private void OnTransitionCompleted()
        {
            if (_phase.Value != LevelUpPhase.Transitioning)
            {
                return;
            }

            _phase.Value = LevelUpPhase.Playing;

            if (_navigation.Current.Value == NavigationState.LevelUp)
            {
                _navigation.TransitionTo(NavigationState.Game);
            }
        }

        private void OnLevelUpAborted()
        {
            if (_phase.Value == LevelUpPhase.Completing)
            {
                AbandonCeremony("cinematic aborted");
                return;
            }

            if (_phase.Value != LevelUpPhase.Pending)
            {
                return;
            }

            _phase.Value = LevelUpPhase.Playing;
            if (_navigation.Current.Value == NavigationState.LevelUp)
            {
                _navigation.TransitionTo(NavigationState.Game);
            }
        }

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
    }
}
