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
    /// <summary>Score keeping only — level progression lives in <c>LevelController</c>.</summary>
    internal class ScoreController : IStartable, IDisposable, IRunResettable, IRunScore
    {
        private readonly ILevelProgress _levelProgress;
        private readonly IGamePalette _palette;
        private readonly Dictionary<string, int> _persistentScore = new();
        private readonly IPublisher<ScorePointsGroupMessage> _scoredPublisher;
        private readonly ColorStreakTracker _streakTracker;
        private readonly ReactiveProperty<int> _totalScore = new(0);
        private readonly ISubscriber<ScoreTrailArrivedMessage> _trailArrivedSubscriber;
        private readonly List<string> _colorKeys = new();
        private IDisposable _trailSubscription;

        public IReadOnlyReactiveProperty<int> TotalScore => _totalScore;

        // Resets after grid/gameplay state — no teardown dependencies.
        public int ResetOrder => RunResetOrder.Score;

        public ScoreController(
            ISubscriber<ScoreTrailArrivedMessage> trailArrivedSubscriber,
            IPublisher<ScorePointsGroupMessage> scoredPublisher,
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
            _colorKeys.AddRange(_palette.ProgressColorNames);

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

        // Invoked directly by HitPipeline, not bus-subscribed, so the streak tracker stays current.
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
            PublishAttributionGroup(attributions, msg.WorldPosition, msg.Context.Flags, msg.ProjectileDirection);
        }

        private void PublishAttributionGroup(
            IReadOnlyList<ScoreAttribution> attributions, Vector3 worldPosition, DamageFlags flags, Vector3 hitDirection)
        {
            if (attributions.Count == 0)
            {
                return;
            }

            using var resolvedPool =
                UnityEngine.Pool.ListPool<(string Color, int Points, int BaseProgress)>.Get(out var resolved);

            var multiplier = RecordStreakMultiplier(attributions, flags);
            ResolveAttributions(attributions, multiplier, resolved);

            PublishPoints(resolved, multiplier, worldPosition, hitDirection);
        }

        // A mixed group breaks the streak — unless exactly one entry is a wildcard's streak anchor
        // (e.g. the rainbow balloon), in which case the streak records against that colour instead.
        private int RecordStreakMultiplier(IReadOnlyList<ScoreAttribution> attributions, DamageFlags flags)
        {
            // A colour-agnostic (rainbow-buffed) projectile keeps the streak climbing on any pop.
            if (flags.HasFlag(DamageFlags.WildcardStreak))
            {
                return _streakTracker.RecordWildcard();
            }

            // A colourless projectile popping a rainbow — bank the pop for a later colour hit.
            if (flags.HasFlag(DamageFlags.DeferredStreak))
            {
                _streakTracker.RecordDeferred();
                return 1;
            }

            if (attributions.Count == 1)
            {
                return _streakTracker.Record(attributions[0].ColorId, attributions[0].BreaksStreak);
            }

            var primaryIndex = -1;
            for (var i = 0; i < attributions.Count; i++)
            {
                if (!attributions[i].IsPrimary)
                {
                    continue;
                }

                if (primaryIndex >= 0)
                {
                    // More than one primary is ambiguous — fall back to the break path.
                    primaryIndex = -1;
                    break;
                }

                primaryIndex = i;
            }

            if (primaryIndex >= 0)
            {
                var primary = attributions[primaryIndex];
                return _streakTracker.Record(primary.ColorId, primary.BreaksStreak);
            }

            _streakTracker.Record(null, true);
            return 1;
        }

        // Keeps only what was granted (capped at the level threshold) plus its base for numbering.
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

        // Points are capped at the level threshold, so every point belongs to the current level.
        private void PublishPoints(
            IReadOnlyList<(string Color, int Points, int BaseProgress)> resolved, int multiplier, Vector3 worldPosition,
            Vector3 hitDirection)
        {
            foreach (var (color, points, baseProgress) in resolved)
            {
                _scoredPublisher.Publish(new ScorePointsGroupMessage(
                    color,
                    worldPosition,
                    points,
                    baseProgress + points,
                    multiplier,
                    hitDirection));
            }
        }

        private void OnTrailArrived(ScoreTrailArrivedMessage msg)
        {
            if (!_persistentScore.ContainsKey(msg.ColorName))
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Level lock (BlockLevelUpCheat): the trail still flew to the counter, but the score doesn't tick up.
            if (BalloonParty.Cheats.CheatState.BlockLevelUp)
            {
                return;
            }
#endif

            _persistentScore[msg.ColorName] += msg.Points;
            _totalScore.Value += msg.Points;
        }
    }
}
