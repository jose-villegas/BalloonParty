using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Health;
using BalloonParty.Game.Run;
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
    internal sealed class LevelController : IStartable, IDisposable, IRunResettable, ILevelProgress
    {
        private readonly IActiveLevelParameters _levelParams;
        private readonly ILevelThresholds _thresholds;
        private readonly IGamePalette _palette;
        private readonly INavigation _navigation;
        private readonly ILossForecast _lossForecast;
        private readonly PauseService _pauseService;
        private readonly IPublisher<ScoreLevelUpMessage> _levelUpPublisher;
        private readonly ISubscriber<ScoreTrailArrivedMessage> _trailArrivedSubscriber;
        private readonly ISubscriber<LevelUpDismissedMessage> _dismissedSubscriber;

        private readonly ReactiveProperty<int> _level = new(1);
        private readonly Dictionary<string, int> _levelProgress = new();
        private readonly Dictionary<string, int> _projectedProgress = new();
        private readonly List<string> _colorKeys = new();

        private IDisposable _trailSubscription;
        private IDisposable _dismissedSubscription;

        // Pending doubles as a semaphore: blocks a second level-up message until the ceremony resolves.
        private bool _pendingLevelUp;
        private int _pendingNewLevel;

        public LevelController(
            IActiveLevelParameters levelParams,
            ILevelThresholds thresholds,
            IGamePalette palette,
            INavigation navigation,
            ILossForecast lossForecast,
            PauseService pauseService,
            IPublisher<ScoreLevelUpMessage> levelUpPublisher,
            ISubscriber<ScoreTrailArrivedMessage> trailArrivedSubscriber,
            ISubscriber<LevelUpDismissedMessage> dismissedSubscriber)
        {
            _levelParams = levelParams;
            _thresholds = thresholds;
            _palette = palette;
            _navigation = navigation;
            _lossForecast = lossForecast;
            _pauseService = pauseService;
            _levelUpPublisher = levelUpPublisher;
            _trailArrivedSubscriber = trailArrivedSubscriber;
            _dismissedSubscriber = dismissedSubscriber;
        }

        public IReadOnlyReactiveProperty<int> Level => _level;

        // Re-resolves after grid/gameplay state clears; same stage as score reset.
        public int ResetOrder => RunResetOrder.Score;

        public void Start()
        {
            _colorKeys.AddRange(_palette.ColorNames);
            ClearRunState();

            _trailSubscription = _trailArrivedSubscriber.Subscribe(OnTrailArrived);

            // Level and progress advance only once the player dismisses the popup.
            _dismissedSubscription = _dismissedSubscriber.Subscribe(_ => OnLevelUpDismissed());
        }

        public void ResetRun(int generation)
        {
            ClearRunState();
        }

        public void Dispose()
        {
            _trailSubscription?.Dispose();
            _dismissedSubscription?.Dispose();
        }

        public int GetProgress(string colorName)
        {
            return _levelProgress.GetValueOrDefault(colorName);
        }

        public int GetRequiredPoints()
        {
            return _thresholds.PointsRequiredForLevel(_level.Value + 1);
        }

        public bool WillLevelUp()
        {
            var required = _thresholds.PointsRequiredForLevel(_level.Value + 1);

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

            // Cap one level-up per burst — excess is intentionally lost, not carried to the next level.
            var required = _thresholds.PointsRequiredForLevel(_level.Value + 1);
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
            _pendingLevelUp = false;
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

            // Pending level-up means any in-flight trail belongs to the finished level.
            if (_pendingLevelUp)
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
            // One level-up at a time: nothing new publishes until the current one is dismissed.
            if (_pendingLevelUp)
            {
                return;
            }

            // Suppressed on a lost run or mid-Ascent so a straggler can't trip an unearned level-up.
            if (_navigation.Current.Value != NavigationState.Game
                || _lossForecast.LossImminent
                || _pauseService.IsPaused(PauseSource.LevelTransition))
            {
                return;
            }

            var required = _thresholds.PointsRequiredForLevel(_level.Value + 1);
            if (!AllColorsConfirmed(required))
            {
                return;
            }

            // Snapshot before publishing — the resolver reacts to the same message and re-resolves AllowedColors.
            var completedColors = _levelParams.Current.AllowedColors;

            _pendingLevelUp = true;
            _pendingNewLevel = _level.Value + 1;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[LevelUp] detected {_level.Value}->{_pendingNewLevel} required={required} " +
                      $"allowed=[{string.Join(",", completedColors)}] progress={DescribeProgress()}");
#endif

            _levelUpPublisher.Publish(new ScoreLevelUpMessage(_pendingNewLevel, completedColors));
            _navigation.TransitionTo(NavigationState.LevelUp);
        }

        // Second phase: the player dismissed the popup, so the level and progress advance now.
        private void OnLevelUpDismissed()
        {
            if (!_pendingLevelUp)
            {
                return;
            }

            _level.Value = _pendingNewLevel;
            _pendingLevelUp = false;
            ResetColorProgress();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private string DescribeProgress()
        {
            var parts = new List<string>();
            foreach (var color in _levelParams.Current.AllowedColors)
            {
                parts.Add($"{color}={_levelProgress.GetValueOrDefault(color)}/{_projectedProgress.GetValueOrDefault(color)}");
            }

            return $"[{string.Join(" ", parts)}]";
        }
#endif
    }
}
