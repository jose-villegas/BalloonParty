using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Shared.Diagnostics;
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
        private readonly ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        private readonly List<string> _colorKeys = new();

        // The score the run WILL show once every in-flight trail lands — summed at publish time (when points
        // are granted), so it leads _totalScore, which banks per arrival. A level-up freezes the survivors and
        // only banks them at CompleteAll, so the popup would show a low _totalScore; OnLevelUp snaps it to
        // _projectedTotal. _snapCredit records how much that snap pre-counted, so the survivors' later arrivals
        // are absorbed rather than double-added.
        private int _projectedTotal;
        private int _snapCredit;
        private IDisposable _trailSubscription;
        private IDisposable _levelUpSubscription;

        public IReadOnlyReactiveProperty<int> TotalScore => _totalScore;

        // Resets after grid/gameplay state — no teardown dependencies.
        public int ResetOrder => RunResetOrder.Score;

        public ScoreController(
            ISubscriber<ScoreTrailArrivedMessage> trailArrivedSubscriber,
            ISubscriber<ScoreLevelUpMessage> levelUpSubscriber,
            IPublisher<ScorePointsGroupMessage> scoredPublisher,
            ILevelProgress levelProgress,
            IGamePalette palette,
            ColorStreakTracker streakTracker)
        {
            _trailArrivedSubscriber = trailArrivedSubscriber;
            _levelUpSubscriber = levelUpSubscriber;
            _scoredPublisher = scoredPublisher;
            _levelProgress = levelProgress;
            _palette = palette;
            _streakTracker = streakTracker;
        }

        public void Dispose()
        {
            _trailSubscription?.Dispose();
            _levelUpSubscription?.Dispose();
        }

        public void Start()
        {
            _colorKeys.AddRange(_palette.ProgressColorNames);

            ClearRunState();

            _trailSubscription = _trailArrivedSubscriber.Subscribe(OnTrailArrived);
            _levelUpSubscription = _levelUpSubscriber.Subscribe(OnLevelUp);
        }

        public void ResetRun(int generation)
        {
            ClearRunState();
        }

        private void ClearRunState()
        {
            _totalScore.Value = 0;
            _projectedTotal = 0;
            _snapCredit = 0;

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
            using var incompletePool = UnityEngine.Pool.ListPool<string>.Get(out var incompleteColors);
            CollectIncompleteColors(incompleteColors);
            scoreColor.ResolveScoreAttribution(in msg.Context, incompleteColors, attributions);
            PublishAttributionGroup(attributions, msg.WorldPosition, msg.Context.Flags, msg.ProjectileDirection);
        }

        // Colours still short of the level's threshold — a scatter pop confines its split to these, so
        // completing a colour never wastes the points that would've landed on it (see ResolveScoreAttribution).
        private void CollectIncompleteColors(List<string> incompleteColors)
        {
            var required = _levelProgress.GetRequiredPoints();
            foreach (var color in _colorKeys)
            {
                if (_levelProgress.GetProgress(color) < required)
                {
                    incompleteColors.Add(color);
                }
            }
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

            if (resolved.Count > 0)
            {
                Log.Info("Score", multiplier > 1
                    ? $"Pop awarded {resolved.Count} color(s), ×{multiplier} streak"
                    : $"Pop awarded {resolved.Count} color(s)");
            }

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
        // The attribution contract is ONE entry per colour (see ScoreAttribution's doc): duplicates fan
        // a single pop into multiple same-colour score groups, which starves the shape decomposition —
        // aggregate at the source (ScoreAttributions.AddRandomPerColor) instead.
        private void ResolveAttributions(
            IReadOnlyList<ScoreAttribution> attributions, int multiplier,
            List<(string Color, int Points, int BaseProgress)> resolved)
        {
            for (var i = 0; i < attributions.Count; i++)
            {
                for (var j = i + 1; j < attributions.Count; j++)
                {
                    Log.Assert(attributions[i].ColorId != attributions[j].ColorId, "ScoreController",
                        $"Duplicate colour '{attributions[i].ColorId}' in one attribution group — " +
                        "aggregate per colour at the source.");
                }
            }
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // Level lock: the trail flies for show but never banks (see OnTrailArrived) — keep the
                // projected total in step so a later snap can't reveal the withheld points.
                if (BalloonParty.Cheats.CheatState.BlockLevelUp)
                {
                    continue;
                }
#endif
                _projectedTotal += points;
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

            // Absorb points a level-up snap already counted (the frozen survivors from the previous level,
            // landing now at CompleteAll) so they don't double-add; the rest ticks the total as usual.
            var points = msg.Points;
            if (_snapCredit > 0)
            {
                var absorbed = Mathf.Min(_snapCredit, points);
                _snapCredit -= absorbed;
                points -= absorbed;
            }

            _totalScore.Value += points;
        }

        // The level-up popup appears while the last trails are still frozen in flight, so _totalScore would
        // read low. The level is complete, so its points are all granted (in _projectedTotal) — snap to it, and
        // credit the gap so the survivors' arrivals at CompleteAll are absorbed instead of counted twice.
        private void OnLevelUp(ScoreLevelUpMessage msg)
        {
            _snapCredit += _projectedTotal - _totalScore.Value;
            _totalScore.Value = _projectedTotal;
        }
    }
}
