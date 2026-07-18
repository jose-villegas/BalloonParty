using System.Threading;
using BalloonParty.Shared;
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
        internal readonly IGameConfiguration Config;
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
            IGameConfiguration config,
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
            Config = config;
            CancellationToken = cancellationToken;
        }
    }
}
