using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Health;
using BalloonParty.Game.Run;
using BalloonParty.Projectile.Controller;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Messages;
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
    internal sealed class LevelController : IStartable, IDisposable, IRunResettable, ILevelProgress
    {
        private readonly IActiveLevelParameters _levelParams;
        private readonly ILevelThresholds _thresholds;
        private readonly IGamePalette _palette;
        private readonly INavigation _navigation;
        private readonly ILossForecast _lossForecast;
        private readonly IPublisher<ScoreLevelUpMessage> _levelUpPublisher;
        private readonly ISubscriber<ScoreTrailArrivedMessage> _trailArrivedSubscriber;
        private readonly ISubscriber<LevelUpAbortedMessage> _abortedSubscriber;
        private readonly ISubscriber<LevelUpDismissedMessage> _dismissedSubscriber;
        private readonly ISubscriber<LevelTransitionCompletedMessage> _transitionCompletedSubscriber;
        private readonly IActiveProjectilePierce _pierce;

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
        private IDisposable _pierceEndedSubscription;

        // The target level for the deferred increment; applied when the popup is dismissed.
        private int _pendingNewLevel;

        public LevelController(
            IActiveLevelParameters levelParams,
            ILevelThresholds thresholds,
            IGamePalette palette,
            INavigation navigation,
            ILossForecast lossForecast,
            IPublisher<ScoreLevelUpMessage> levelUpPublisher,
            ISubscriber<ScoreTrailArrivedMessage> trailArrivedSubscriber,
            ISubscriber<LevelUpAbortedMessage> abortedSubscriber,
            ISubscriber<LevelUpDismissedMessage> dismissedSubscriber,
            ISubscriber<LevelTransitionCompletedMessage> transitionCompletedSubscriber,
            IActiveProjectilePierce pierce)
        {
            _levelParams = levelParams;
            _thresholds = thresholds;
            _palette = palette;
            _navigation = navigation;
            _lossForecast = lossForecast;
            _levelUpPublisher = levelUpPublisher;
            _trailArrivedSubscriber = trailArrivedSubscriber;
            _abortedSubscriber = abortedSubscriber;
            _dismissedSubscriber = dismissedSubscriber;
            _transitionCompletedSubscriber = transitionCompletedSubscriber;
            _pierce = pierce;
        }

        public IReadOnlyReactiveProperty<int> Level => _level;
        public IReadOnlyReactiveProperty<LevelUpPhase> Phase => _phase;

        // Re-resolves after grid/gameplay state clears; same stage as score reset.
        public int ResetOrder => RunResetOrder.Score;

        public void Start()
        {
            _colorKeys.AddRange(_palette.ProgressColorNames);
            ClearRunState();

            _trailSubscription = _trailArrivedSubscriber.Subscribe(OnTrailArrived);
            _abortedSubscription = _abortedSubscriber.Subscribe(_ => OnLevelUpAborted());

            // Level and progress advance only once the player dismisses the popup (Pending → Transitioning),
            // and scoring reopens only once the Ascent reports it has settled (Transitioning → Playing).
            _dismissedSubscription = _dismissedSubscriber.Subscribe(_ => OnLevelUpDismissed());
            _transitionCompletedSubscription = _transitionCompletedSubscriber.Subscribe(_ => OnTransitionCompleted());

            // A level-up is DETECTED mid-pierce (WillLevelUp stays true the moment we know), but the
            // COMMIT is held while a shot is piercing so the ceremony doesn't fire mid-flight — see
            // CheckLevelUp's guard. When the pierce ends (its discharge), re-check: the confirming trails
            // that arrived during the plow have already advanced progress, so this is where it commits.
            _pierceEndedSubscription = _pierce.IsPiercing
                .SkipLatestValueOnSubscribe()
                .Where(piercing => !piercing)
                .Subscribe(_ => CheckLevelUp());
        }

        public void ResetRun(int generation)
        {
            ClearRunState();
        }

        public void Dispose()
        {
            _trailSubscription?.Dispose();
            _abortedSubscription?.Dispose();
            _dismissedSubscription?.Dispose();
            _transitionCompletedSubscription?.Dispose();
            _pierceEndedSubscription?.Dispose();
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
            return (baseProgress, granted);
        }

        private void ClearRunState()
        {
            var startLevel = 1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE
            startLevel = Mathf.Max(1, BalloonParty.Cheats.CheatState.StartLevel);
#endif
            _level.Value = startLevel;
            _phase.Value = LevelUpPhase.Playing;
            _pendingNewLevel = 0;
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

            // Outside Playing (a ceremony or its Ascent is running), any in-flight trail belongs to the
            // finished level — ignore it.
            if (_phase.Value != LevelUpPhase.Playing)
            {
                return;
            }

            // Capped at this level's claim so a previous-level straggler can't re-inflate progress.
            var confirmable = Math.Min(msg.Score, _projectedProgress[msg.ColorName]);
            _levelProgress[msg.ColorName] = Math.Max(_levelProgress[msg.ColorName], confirmable);

            CheckLevelUp();
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

        private void CheckLevelUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE
            // Dev cheat (BlockLevelUpCheat): hold the current level — never complete it — while testing.
            if (BalloonParty.Cheats.CheatState.BlockLevelUp)
            {
                return;
            }
#endif

            // Only detect while Playing — Pending/Transitioning already own the ceremony, so this is the
            // single reentrancy guard (no second message until the current one resolves). nav/loss stay
            // to suppress on a run that's ending, which the phase doesn't model.
            if (_phase.Value != LevelUpPhase.Playing)
            {
                return;
            }

            // Hold the commit while a shot is piercing: it plows many balloons across one flight, so
            // firing the ceremony on a mid-flight confirming arrival would interrupt the shot. The
            // pierce-ended subscription re-runs this once the shot discharges.
            if (_pierce.IsPiercing.Value)
            {
                return;
            }

            if (_navigation.Current.Value != NavigationState.Game || _lossForecast.LossImminent)
            {
                return;
            }

            var required = _thresholds.PointsRequiredForLevel(_level.Value);
            if (!AllColorsConfirmed(required))
            {
                return;
            }

            // Snapshot before publishing — the resolver reacts to the same message and re-resolves AllowedColors.
            var completedColors = _levelParams.Current.AllowedColors;

            _phase.Value = LevelUpPhase.Pending;
            _pendingNewLevel = _level.Value + 1;

            Log.Info("Level", $"Level-up detected → pending level {_pendingNewLevel} " +
                $"(colors completed: {string.Join(", ", completedColors)})");

            _levelUpPublisher.Publish(new ScoreLevelUpMessage(_pendingNewLevel, completedColors));
            _navigation.TransitionTo(NavigationState.LevelUp);
        }

        // Pending → Transitioning: the player dismissed the popup, so the level and progress advance now
        // and the Ascent (which watches Phase) kicks off.
        private void OnLevelUpDismissed()
        {
            if (_phase.Value != LevelUpPhase.Pending)
            {
                return;
            }

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
    }
}
