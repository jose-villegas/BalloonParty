using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Game.Score
{
    /// <summary>
    ///     Score keeping only: streak-multiplied attribution of pops into points, publishing the score
    ///     trails, and the lifetime/total tallies. Level progression (current level, per-colour progress,
    ///     the level-up ceremony) lives in <c>LevelController</c>; this feeds it the capped points via
    ///     <see cref="ILevelProgress.ClaimProgress" /> and otherwise stays out of levelling.
    /// </summary>
    internal class ScoreController : IStartable, IDisposable, IRunResettable, IRunScore
    {
        private readonly ILevelProgress _levelProgress;
        private readonly IGamePalette _palette;
        private readonly Dictionary<string, int> _persistentScore = new();
        private readonly IPublisher<ScorePointMessage> _scoredPublisher;
        private readonly ColorStreakTracker _streakTracker;
        private readonly ReactiveProperty<int> _totalScore = new(0);
        private readonly ISubscriber<ScoreTrailArrivedMessage> _trailArrivedSubscriber;
        private readonly List<string> _colorKeys = new();
        private IDisposable _trailSubscription;

        public IReadOnlyReactiveProperty<int> TotalScore => _totalScore;

        // Score state has no teardown dependencies, so it resets after grid/gameplay state.
        public int ResetOrder => RunResetOrder.Score;

        public ScoreController(
            ISubscriber<ScoreTrailArrivedMessage> trailArrivedSubscriber,
            IPublisher<ScorePointMessage> scoredPublisher,
            ILevelProgress levelProgress,
            IGamePalette palette,
            ColorStreakTracker streakTracker)
        {
            _trailArrivedSubscriber = trailArrivedSubscriber;
            _scoredPublisher = scoredPublisher;
            _levelProgress = levelProgress;
            _palette = palette;
            _streakTracker = streakTracker;
        }

        public void Dispose()
        {
            _trailSubscription?.Dispose();
        }

        public void Start()
        {
            _colorKeys.AddRange(_palette.ColorNames);

            ClearRunState();

            _trailSubscription = _trailArrivedSubscriber.Subscribe(OnTrailArrived);
        }

        public void ResetRun(int generation)
        {
            ClearRunState();
        }

        private void ClearRunState()
        {
            _totalScore.Value = 0;

            foreach (var key in _colorKeys)
            {
                _persistentScore[key] = 0;
            }
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

        // Claims each colour's streak-multiplied points against the level (which caps at the threshold
        // and advances projected progress), keeping only what was granted plus its base for numbering.
        private void ResolveAttributions(
            IReadOnlyList<ScoreAttribution> attributions, int multiplier,
            List<(string Color, int Points, int BaseProgress)> resolved)
        {
            foreach (var attribution in attributions)
            {
                var (baseProgress, granted) = _levelProgress.ClaimProgress(attribution.ColorId, attribution.Points * multiplier);
                if (granted <= 0)
                {
                    continue;
                }

                resolved.Add((attribution.ColorId, granted, baseProgress));
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

        // Emits one ScorePointMessage per point, carrying the group size/index so the bars can animate
        // the burst. Points are capped at the level threshold in ClaimProgress, so no point crosses into
        // the next level — every point belongs to the current level.
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
        }
    }
}
