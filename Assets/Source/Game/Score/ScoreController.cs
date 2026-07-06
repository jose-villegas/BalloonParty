using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Game.Health;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer.Unity;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Game.Score
{
    internal class ScoreController : IStartable, IDisposable, IRunResettable, IRunScore, IScoreQuery
    {
        private readonly IActiveLevelParameters _levelParams;
        private readonly ReactiveProperty<int> _level = new(1);
        private readonly Dictionary<string, int> _levelProgress = new();
        private readonly IPublisher<ScoreLevelUpMessage> _levelUpPublisher;
        private readonly IGamePalette _palette;
        private readonly Dictionary<string, int> _persistentScore = new();
        private readonly Dictionary<string, int> _projectedProgress = new();
        private readonly IPublisher<ScorePointMessage> _scoredPublisher;
        private readonly INavigation _navigation;
        private readonly ILossForecast _lossForecast;
        private readonly ColorStreakTracker _streakTracker;
        private readonly ReactiveProperty<int> _totalScore = new(0);
        private readonly ISubscriber<ScoreTrailArrivedMessage> _trailArrivedSubscriber;
        private readonly List<string> _colorKeys = new();
        private IDisposable _trailSubscription;
        private IDisposable _navigationSubscription;
        private bool _levelScored;

        public IReadOnlyReactiveProperty<int> Level => _level;
        public IReadOnlyReactiveProperty<int> TotalScore => _totalScore;

        // Score state has no teardown dependencies, so it resets after grid/gameplay state.
        public int ResetOrder => RunResetOrder.Score;

        public ScoreController(
            ISubscriber<ScoreTrailArrivedMessage> trailArrivedSubscriber,
            IPublisher<ScorePointMessage> scoredPublisher,
            IPublisher<ScoreLevelUpMessage> levelUpPublisher,
            IActiveLevelParameters levelParams,
            IGamePalette palette,
            INavigation navigation,
            ILossForecast lossForecast,
            ColorStreakTracker streakTracker)
        {
            _trailArrivedSubscriber = trailArrivedSubscriber;
            _scoredPublisher = scoredPublisher;
            _levelUpPublisher = levelUpPublisher;
            _levelParams = levelParams;
            _palette = palette;
            _navigation = navigation;
            _lossForecast = lossForecast;
            _streakTracker = streakTracker;
        }

        public void Dispose()
        {
            _trailSubscription?.Dispose();
            _navigationSubscription?.Dispose();
        }

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

        public int GetProgress(string colorName)
        {
            return _levelProgress.GetValueOrDefault(colorName);
        }

        public int GetRequiredPoints()
        {
            return _levelParams.PointsRequiredForLevel(Level.Value + 1);
        }

        /// <summary>
        ///     Uses projected (not confirmed) progress so the cinematic can
        ///     register before in-flight trails from other colors arrive.
        /// </summary>
        public bool WillLevelUp()
        {
            var required = _levelParams.PointsRequiredForLevel(_level.Value + 1);

            foreach (var color in _levelParams.AllowedColors)
            {
                if (_projectedProgress.GetValueOrDefault(color) < required)
                {
                    return false;
                }
            }

            return true;
        }

        private void ClearRunState()
        {
            _level.Value = 1;
            _totalScore.Value = 0;
            _levelScored = false;

            foreach (var key in _colorKeys)
            {
                _persistentScore[key] = 0;
                _levelProgress[key] = 0;
                _projectedProgress[key] = 0;
            }
        }

        private bool AllColorsConfirmed(int required)
        {
            foreach (var color in _levelParams.AllowedColors)
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

            var required = _levelParams.PointsRequiredForLevel(_level.Value + 1);
            if (!AllColorsConfirmed(required))
            {
                return;
            }

            // Snapshot before publishing — the resolver reacts to this same message and may
            // re-resolve AllowedColors to the new level before other subscribers read it.
            var completedColors = _levelParams.AllowedColors;

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

        // Invoked by HitPipeline as the first dispatch stage (not bus-subscribed) so the streak
        // tracker is guaranteed current when Dispatch returns. Internal for direct test invocation.
        internal void OnActorHit(ActorHitMessage msg)
        {
            if (msg.Outcome != HitOutcome.Pop && msg.Outcome != HitOutcome.PassThrough)
            {
                return;
            }

            if (msg.Actor is not IHasScoreColor scoreColor)
            {
                return;
            }

            using var attributionPool = UnityEngine.Pool.ListPool<ScoreAttribution>.Get(out var attributions);
            scoreColor.ResolveScoreAttribution(in msg.Context, attributions);
            PublishAttributionGroup(attributions, msg.WorldPosition);
        }

        /// <summary>
        /// Publishes all attributions from a single <see cref="IHasScoreColor.ResolveScoreAttribution"/>
        /// call as one scatter group. Every message shares the same <c>GroupSize</c> so the UI can
        /// fan them out together regardless of how many colours are involved.
        /// </summary>
        private void PublishAttributionGroup(IReadOnlyList<ScoreAttribution> attributions, Vector3 worldPosition)
        {
            if (attributions.Count == 0)
            {
                return;
            }

            using var resolvedPool =
                UnityEngine.Pool.ListPool<(string Color, int Points, int BaseProgress)>.Get(out var resolved);

            var multiplier = RecordStreakMultiplier(attributions);
            ResolveAttributions(attributions, multiplier, resolved);

            var groupSize = SumPoints(resolved);
            if (groupSize <= 0)
            {
                return;
            }

            PublishPoints(resolved, groupSize, worldPosition);
        }

        // A single same-colour break extends the streak (and earns its multiplier); a mixed group
        // breaks it. Returns the points multiplier to apply.
        private int RecordStreakMultiplier(IReadOnlyList<ScoreAttribution> attributions)
        {
            if (attributions.Count == 1)
            {
                return _streakTracker.Record(attributions[0].ColorId, attributions[0].BreaksStreak);
            }

            _streakTracker.Record(null, true);
            return 1;
        }

        private void ResolveAttributions(
            IReadOnlyList<ScoreAttribution> attributions, int multiplier,
            List<(string Color, int Points, int BaseProgress)> resolved)
        {
            var required = _levelParams.PointsRequiredForLevel(_level.Value + 1);

            foreach (var attribution in attributions)
            {
                var color = attribution.ColorId;
                if (string.IsNullOrEmpty(color) || !_persistentScore.ContainsKey(color))
                {
                    continue;
                }

                var baseProgress = _projectedProgress.GetValueOrDefault(color);

                // Cap one level-up per burst: a color's progress can't exceed the next-level threshold,
                // so a big/high-streak pop can't overfill and carry into the FOLLOWING level. Without
                // this the next level arrived pre-completed and fired a second level-up with no player
                // throw and no cinematic — the "instant next level + transition, no popup" bug. Excess
                // is intentionally lost (no level-skipping).
                var pts = Mathf.Min(attribution.Points * multiplier, Mathf.Max(0, required - baseProgress));
                if (pts <= 0)
                {
                    continue;
                }

                _projectedProgress[color] = baseProgress + pts;
                resolved.Add((color, pts, baseProgress));
            }
        }

        private static int SumPoints(IReadOnlyList<(string Color, int Points, int BaseProgress)> resolved)
        {
            var total = 0;
            foreach (var (_, pts, _) in resolved)
            {
                total += pts;
            }

            return total;
        }

        // Emits one ScorePointMessage per point, carrying the group size/index so the bars can
        // animate the burst. Progress is capped at the threshold in ResolveAttributions, so no point
        // ever crosses into the next level — every point belongs to the current level.
        private void PublishPoints(
            IReadOnlyList<(string Color, int Points, int BaseProgress)> resolved, int groupSize, Vector3 worldPosition)
        {
            var groupIndex = 0;
            foreach (var (color, points, baseProgress) in resolved)
            {
                for (var i = 0; i < points; i++, groupIndex++)
                {
                    _scoredPublisher.Publish(new ScorePointMessage(
                        color,
                        worldPosition,
                        baseProgress + i + 1,
                        groupSize,
                        groupIndex));
                }
            }
        }

        private void OnTrailArrived(ScoreTrailArrivedMessage msg)
        {
            if (!_persistentScore.ContainsKey(msg.ColorName))
            {
                return;
            }

            _persistentScore[msg.ColorName]++;
            _totalScore.Value++;

            // Once the level is scored it stays scored until the next one starts (the level-up is gated
            // by the transition, so every trail still in flight belongs to the level that just finished).
            // Their late arrivals must not touch progress — folding a straggler in via Max would
            // re-inflate the color, so the scoring cap stops it below the next threshold and the bar can
            // never fill. Lifetime totals above still count it — the point was earned.
            if (_levelScored)
            {
                return;
            }

            // Progress is already capped at the threshold at the scoring source (ResolveAttributions),
            // so no arriving point exceeds it — a plain max to fold arrivals in is enough.
            var previous = _levelProgress[msg.ColorName];
            _levelProgress[msg.ColorName] = Math.Max(previous, msg.Score);
            _projectedProgress[msg.ColorName] = Math.Max(_projectedProgress[msg.ColorName], msg.Score);

            CheckLevelUp();
        }
    }
}
