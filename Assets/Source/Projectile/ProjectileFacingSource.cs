using System.Collections.Generic;
using BalloonParty.Prediction;
using UnityEngine;
using VContainer;

namespace BalloonParty.Projectile
{
    /// <summary>
    ///     Aggregates the DI-only projectile providers (<see cref="IProjectileRegistry"/>,
    ///     <see cref="PredictionTraceProvider"/>) with the followed shot's live direction into one
    ///     read-only surface (<see cref="IProjectileFacingSource"/>) a pooled item visual can hold onto —
    ///     the pooled icon isn't DI-injected, so a host (e.g. BalloonView) resolves this singleton once
    ///     and hands it down, mirroring how ItemDisplayService threads SceneLightFieldService through to
    ///     LaserItemRotation.
    /// </summary>
    internal sealed class ProjectileFacingSource : IProjectileFacingSource
    {
        private readonly IProjectileRegistry _projectiles;
        private readonly PredictionTraceProvider _traceProvider;

        // Items face one shot: the newest in flight, else the one being aimed.
        public bool IsFlying => _projectiles.InFlightCount > 0;
        public bool IsAiming => _traceProvider.IsActive;

        public Vector3 ProjectilePosition =>
            _projectiles.TryGetLatestInFlight(out _, out var position) ? position : Vector3.zero;

        public Vector2 Direction
        {
            get
            {
                var followed = _projectiles.TryGetLatestInFlight(out var flying, out _) ? flying : _projectiles.Loaded;
                return followed != null ? (Vector2)followed.Direction : Vector2.zero;
            }
        }

        public IReadOnlyList<Vector3> PredictionPoints => _traceProvider.Points;
        public int PredictionVersion => _traceProvider.Version;

        [Inject]
        internal ProjectileFacingSource(IProjectileRegistry projectiles, PredictionTraceProvider traceProvider)
        {
            _projectiles = projectiles;
            _traceProvider = traceProvider;
        }
    }
}
