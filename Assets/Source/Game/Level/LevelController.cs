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
    ///     Owns the player's progression through levels: the current level, per-colour progress toward
    ///     the next-level threshold, and the level-up ceremony trigger. Progress is fed two ways —
    ///     projected (immediately, via <see cref="ClaimProgress" /> from <c>ScoreController</c> as points
    ///     are scored) and confirmed (as score trails arrive) — and a level-up fires once every allowed
    ///     colour is confirmed at threshold. Distinct from <c>LevelDifficultyResolver</c>, which answers
    ///     what a level's difficulty <em>is</em>; this tracks how far the player has gotten.
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

        private readonly ReactiveProperty<int> _level = new(1);
        private readonly Dictionary<string, int> _levelProgress = new();
        private readonly Dictionary<string, int> _projectedProgress = new();
        private readonly List<string> _colorKeys = new();

        private IDisposable _trailSubscription;
        private IDisposable _navigationSubscription;
        private bool _levelScored;

        public LevelController(
            IActiveLevelParameters levelParams,
            ILevelThresholds thresholds,
            IGamePalette palette,
            INavigation navigation,
            ILossForecast lossForecast,
            IPublisher<ScoreLevelUpMessage> levelUpPublisher,
            ISubscriber<ScoreTrailArrivedMessage> trailArrivedSubscriber)
        {
            _levelParams = levelParams;
            _thresholds = thresholds;
            _palette = palette;
            _navigation = navigation;
            _lossForecast = lossForecast;
            _levelUpPublisher = levelUpPublisher;
            _trailArrivedSubscriber = trailArrivedSubscriber;
        }

        public IReadOnlyReactiveProperty<int> Level => _level;

        // Re-resolves after grid/gameplay state clears; same stage as score reset.
        public int ResetOrder => RunResetOrder.Score;

        public void Start()
        {
            _colorKeys.AddRange(_palette.ColorNames);
            ClearRunState();

            _trailSubscription = _trailArrivedSubscriber.Subscribe(OnTrailArrived);

            // Re-open scoring when the next level begins (the transition has ended and the player can
            // score again) — by now every straggler from the finished level has long since landed.
            _navigationSubscription = _navigation.Current
                .Where(state => state == NavigationState.Game)
                .Subscribe(_ => _levelScored = false);
        }

        public void ResetRun(int generation)
        {
            ClearRunState();
        }

        public void Dispose()
        {
            _trailSubscription?.Dispose();
            _navigationSubscription?.Dispose();
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

            // Cap one level-up per burst: a colour's progress can't exceed the next-level threshold, so
            // a big/high-streak pop can't overfill and carry into the FOLLOWING level (which would
            // auto-complete it with no player throw and no cinematic). Excess is intentionally lost.
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
            _levelScored = false;

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

            // Once the level is scored it stays scored until the next one starts (the level-up is gated
            // by the transition, so every trail still in flight belongs to the level that just finished).
            // Folding a straggler in via Max would re-inflate the colour, so the scoring cap stops it
            // below the next threshold and the bar can never fill.
            if (_levelScored)
            {
                return;
            }

            // Progress is capped at the threshold at the scoring source (ClaimProgress), so no arriving
            // point exceeds it — a plain max to fold arrivals in is enough.
            var previous = _levelProgress[msg.ColorName];
            _levelProgress[msg.ColorName] = Math.Max(previous, msg.Score);
            _projectedProgress[msg.ColorName] = Math.Max(_projectedProgress[msg.ColorName], msg.Score);

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
            // No level-up on a lost run: the ceremony is suppressed once the loss is committed
            // (GameOver) or already certain (queued overflow charges cover the remaining HP) — a trail
            // arriving post-mortem must not yank navigation out of GameOver or show the popup.
            if (_navigation.Current.Value != NavigationState.Game || _lossForecast.LossImminent)
            {
                return;
            }

            var required = _thresholds.PointsRequiredForLevel(_level.Value + 1);
            if (!AllColorsConfirmed(required))
            {
                return;
            }

            // Snapshot before publishing — the resolver reacts to this same message and may re-resolve
            // AllowedColors to the new level before other subscribers read it.
            var completedColors = _levelParams.Current.AllowedColors;

            _level.Value++;
            _levelScored = true;

            foreach (var key in _colorKeys)
            {
                _levelProgress[key] = 0;
                _projectedProgress[key] = 0;
            }

            _levelUpPublisher.Publish(new ScoreLevelUpMessage(_level.Value, completedColors));
            _navigation.TransitionTo(NavigationState.LevelUp);
        }
    }
}
