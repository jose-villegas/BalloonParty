using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using UnityEngine;
using VContainer;

namespace BalloonParty.Game.Score.Behaviours
{
    /// <summary>
    ///     Carrier-takes-all confluence: one carrier trail carries the whole group value to the bar while n
    ///     vertex trails draw a star polygon {n/k} around the pop (nested per the tier), then flash out as the
    ///     carrier launches. The carrier is the principal and reports exactly once — one "+N" arrival, so a
    ///     5x-on-a-cluster award is one arrival instead of hundreds. The <see cref="ShapeFormationTicker"/>
    ///     owns the per-frame motion; this handler only picks the tier, anchors the formation, and launches it.
    /// </summary>
    internal sealed class BigScoreTrailBehaviour : IScoreTrailBehaviour
    {
        // Defensive: only used if the tier table is empty (the wired asset always has rows).
        private static readonly BigScoreTierConfig FallbackTier =
            new(0, 3, 1, 1, 0.381966f, 0f, 2f, 0.25f, 0.35f, 0.5f, 0.8f, 180f);

        private readonly ShapeFormationTicker _ticker;
        private readonly IScoreTrailBehaviourConfiguration _config;

        [Inject]
        internal BigScoreTrailBehaviour(ShapeFormationTicker ticker, IScoreTrailBehaviourConfiguration config)
        {
            _ticker = ticker;
            _config = config;
        }

        // The carrier spawns immediately at the formation centre, so it can nominate LastScore (unlike the
        // staggered default fan-out): the cinematic's bounded registry wait is timeout-safe from frame one.
        public TrailId GetPrincipalId(in ScorePointsGroupMessage msg)
        {
            return new TrailId(msg.ColorName, msg.LastScore);
        }

        public void Begin(in ScoreTrailContext context)
        {
            var tier = ResolveTier(context.Points);
            var center = ClampCenter(context.Origin, tier.BaseRadius, context.Config.LimitsClockwise);
            var carrierId = new TrailId(context.ColorName, context.LastScore);

            var request = new BigScoreFormationRequest(
                center,
                context.Origin,
                context.Color,
                context.Points,
                context.LastScore,
                carrierId,
                context.Target,
                context.Spawner,
                context.Flights,
                context.Reporter,
                tier,
                context.Config.ScorePointTraceDuration,
                context.CancellationToken);

            // Synchronous: registers the carrier in Flights before this returns (the cinematic depends on it).
            _ticker.Launch(in request);
        }

        // Highest MinPoints the group clears; falls back to the lowest authored tier (then the hardcoded one).
        internal static BigScoreTierConfig SelectTier(IReadOnlyList<BigScoreTierConfig> tiers, int points)
        {
            var best = tiers[0];
            var bestMin = -1;
            var lowest = tiers[0];
            for (var i = 0; i < tiers.Count; i++)
            {
                var tier = tiers[i];
                if (tier.MinPoints < lowest.MinPoints)
                {
                    lowest = tier;
                }

                if (tier.MinPoints <= points && tier.MinPoints > bestMin)
                {
                    best = tier;
                    bestMin = tier.MinPoints;
                }
            }

            return bestMin >= 0 ? best : lowest;
        }

        private BigScoreTierConfig ResolveTier(int points)
        {
            var tiers = _config?.BigScoreTiers;
            return tiers == null || tiers.Count == 0 ? FallbackTier : SelectTier(tiers, points);
        }

        // Shifts the centre inward so C +/- BaseRadius stays inside the walls rather than shrinking the star.
        private static Vector3 ClampCenter(Vector3 origin, float radius, Vector4 limitsClockwise)
        {
            var limits = new WallLimits(limitsClockwise);
            var center = origin;
            center.x = ClampAxis(origin.x, limits.Left + radius, limits.Right - radius);
            center.y = ClampAxis(origin.y, limits.Bottom + radius, limits.Top - radius);
            return center;
        }

        private static float ClampAxis(float value, float min, float max)
        {
            // A play area narrower than the star just centres it rather than clamping to a crossed bound.
            return min > max ? 0.5f * (min + max) : Mathf.Clamp(value, min, max);
        }
    }
}
