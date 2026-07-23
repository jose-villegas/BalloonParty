using System.Threading;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using UnityEngine;

namespace BalloonParty.Game.Score.Behaviours
{
    /// <summary>
    ///     Everything a handler needs to choreograph one score group from spawn to arrival. Handlers own
    ///     their flight registrations in <see cref="Flights"/> and return every pooled instance they spawn.
    /// </summary>
    internal readonly struct ScoreTrailContext
    {
        internal readonly string ColorName;
        internal readonly Color Color;
        internal readonly Vector3 Origin;
        internal readonly Vector3 HitDirection;
        internal readonly int Points;
        internal readonly int FirstScore;
        internal readonly int LastScore;
        internal readonly ITrailEndpoint Target;
        internal readonly TrailSpawner Spawner;
        internal readonly TrailFlightRegistry<TrailId> Flights;
        internal readonly IScoreTrailReporter Reporter;
        internal readonly IScoreTrailConfig ScoreConfig;
        internal readonly ISlotGridConfig GridConfig;
        internal readonly IProjectileFlightConfig FlightConfig;
        internal readonly CancellationToken CancellationToken;

        internal ScoreTrailContext(
            string colorName,
            Color color,
            Vector3 origin,
            Vector3 hitDirection,
            int points,
            int firstScore,
            int lastScore,
            ITrailEndpoint target,
            TrailSpawner spawner,
            TrailFlightRegistry<TrailId> flights,
            IScoreTrailReporter reporter,
            IScoreTrailConfig scoreConfig,
            ISlotGridConfig gridConfig,
            IProjectileFlightConfig flightConfig,
            CancellationToken cancellationToken)
        {
            ColorName = colorName;
            Color = color;
            Origin = origin;
            HitDirection = hitDirection;
            Points = points;
            FirstScore = firstScore;
            LastScore = lastScore;
            Target = target;
            Spawner = spawner;
            Flights = flights;
            Reporter = reporter;
            ScoreConfig = scoreConfig;
            GridConfig = gridConfig;
            FlightConfig = flightConfig;
            CancellationToken = cancellationToken;
        }

        internal ScoreTrailContext(
            in ScorePointsGroupMessage msg,
            Color color,
            ITrailEndpoint target,
            TrailSpawner spawner,
            TrailFlightRegistry<TrailId> flights,
            IScoreTrailReporter reporter,
            IScoreTrailConfig scoreConfig,
            ISlotGridConfig gridConfig,
            IProjectileFlightConfig flightConfig,
            CancellationToken cancellationToken)
            : this(
                msg.ColorName, color, msg.WorldPosition, msg.HitDirection, msg.Points, msg.FirstScore,
                msg.LastScore, target, spawner, flights, reporter, scoreConfig, gridConfig, flightConfig,
                cancellationToken)
        {
        }
    }
}
