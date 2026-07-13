using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Health;
using BalloonParty.Game.Run;
using BalloonParty.Shared.GameState;
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
        private readonly ISubscriber<LevelUpDismissedMessage> _dismissedSubscriber;
        private readonly ISubscriber<LevelTransitionCompletedMessage> _transitionCompletedSubscriber;

        private readonly ReactiveProperty<int> _level = new(1);
        private readonly ReactiveProperty<LevelUpPhase> _phase = new(LevelUpPhase.Playing);
        private readonly Dictionary<string, int> _levelProgress = new();
        private readonly Dictionary<string, int> _projectedProgress = new();
        private readonly List<string> _colorKeys = new();

        private IDisposable _trailSubscription;
        private IDisposable _dismissedSubscription;
        private IDisposable _transitionCompletedSubscription;

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
            ISubscriber<LevelUpDismissedMessage> dismissedSubscriber,
            ISubscriber<LevelTransitionCompletedMessage> transitionCompletedSubscriber)
        {
            _levelParams = levelParams;
            _thresholds = thresholds;
            _palette = palette;
            _navigation = navigation;
            _lossForecast = lossForecast;
            _levelUpPublisher = levelUpPublisher;
            _trailArrivedSubscriber = trailArrivedSubscriber;
            _dismissedSubscriber = dismissedSubscriber;
            _transitionCompletedSubscriber = transitionCompletedSubscriber;
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

            // Level and progress advance only once the player dismisses the popup (Pending → Transitioning),
            // and scoring reopens only once the Ascent reports it has settled (Transitioning → Playing).
            _dismissedSubscription = _dismissedSubscriber.Subscribe(_ => OnLevelUpDismissed());
            _transitionCompletedSubscription = _transitionCompletedSubscriber.Subscribe(_ => OnTransitionCompleted());
        }

        public void ResetRun(int generation)
        {
            ClearRunState();
        }

        public void Dispose()
        {
            _trailSubscription?.Dispose();
            _dismissedSubscription?.Dispose();
            _transitionCompletedSubscription?.Dispose();
        }

        public int GetProgress(string colorName)
        {
            return _levelProgress.GetValueOrDefault(colorName);
        }

        public int GetRequiredPoints()
        {
            return _thresholds.PointsRequiredForLevel(_level.Value);
        }

        public bool WillLevelUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Dev cheat (BlockLevelUpCheat) — level lock: grant the points for the VISUAL (so score trails still
            // fly on a pop) but DON'T advance progress — no projected mutation here, and both OnTrailArrived
            // handlers skip their commit, so score, bars and level all stay put while the trails play.
            if (BalloonParty.Cheats.CheatState.BlockLevelUp)
            {
                return (baseProgress, points);
            }
#endif

            // Cap one level-up per burst — excess is intentionally lost, not carried to the next level.
            var required = _thresholds.PointsRequiredForLevel(_level.Value);
            var granted = Mathf.Min(points, Mathf.Max(0, required - baseProgress));
            if (granted <= 0)
            {
                return (baseProgress, 0);
            }

            _projectedProgress[color] = baseProgress + granted;
            return (baseProgress, granted);
        }

        private void ClearRunState()
        {
            _level.Value = 1;
            _phase.Value = LevelUpPhase.Playing;
            _pendingNewLevel = 0;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
        }

        // Transitioning → Playing: the Ascent has settled, so scoring reopens.
        private void OnTransitionCompleted()
        {
            if (_phase.Value != LevelUpPhase.Transitioning)
            {
                return;
            }

            _phase.Value = LevelUpPhase.Playing;
        }
    }
}
