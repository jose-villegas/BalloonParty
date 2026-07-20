using System.Collections.Generic;
using BalloonParty.Prediction;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Projectile
{
    /// <summary>
    ///     Aggregates the DI-only projectile providers (<see cref="ProjectilePositionProvider"/>,
    ///     <see cref="PredictionTraceProvider"/>) with the loaded model's live direction into one
    ///     read-only surface (<see cref="IProjectileFacingSource"/>) a pooled item visual can hold onto —
    ///     the pooled icon isn't DI-injected, so a host (e.g. BalloonView) resolves this singleton once
    ///     and hands it down, mirroring how ItemDisplayService threads SceneLightFieldService through to
    ///     LaserItemRotation.
    /// </summary>
    internal sealed class ProjectileFacingSource : IProjectileFacingSource, IStartable
    {
        private readonly ProjectilePositionProvider _positionProvider;
        private readonly PredictionTraceProvider _traceProvider;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;

        private IProjectileModel _model;

        public bool IsFlying => _positionProvider.IsActive;
        public bool IsAiming => _traceProvider.IsActive;
        public Vector3 ProjectilePosition => _positionProvider.Position;
        public Vector2 Direction => _model != null ? (Vector2)_model.Direction : Vector2.zero;
        public IReadOnlyList<Vector3> PredictionPoints => _traceProvider.Points;
        public int PredictionVersion => _traceProvider.Version;

        [Inject]
        internal ProjectileFacingSource(
            ProjectilePositionProvider positionProvider,
            PredictionTraceProvider traceProvider,
            ISubscriber<ProjectileLoadedMessage> loadedSubscriber,
            ISubscriber<ProjectileDestroyedMessage> destroyedSubscriber)
        {
            _positionProvider = positionProvider;
            _traceProvider = traceProvider;
            _loadedSubscriber = loadedSubscriber;
            _destroyedSubscriber = destroyedSubscriber;
        }

        public void Start()
        {
            _loadedSubscriber.Subscribe(message => _model = message.Model);
            _destroyedSubscriber.Subscribe(_ => _model = null);
        }
    }
}
