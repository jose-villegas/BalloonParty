using System;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Configuration;
using BalloonParty.Game.Health;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using BalloonParty.UI.Score;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.Health
{
    /// <summary>
    ///     Flies a heart trail from the health UI to a ready overflow balloon on
    ///     <see cref="OverflowHeartRequestedMessage"/>, popping it on arrival via
    ///     <see cref="RejectedBalloonEffect.OnHeartArrived"/>.
    /// </summary>
    internal sealed class HeartTrailController : IStartable, IDisposable
    {
        private const string TrailPoolKey = "HeartTrail";

        private readonly IOverflowSettings _settings;
        private readonly ISubscriber<OverflowHeartRequestedMessage> _heartRequestedSubscriber;
        private readonly PoolManager _poolManager;
        private readonly FlyingTrail _prefab;
        private readonly TrailEndpointRegistry _endpoints;
        private readonly HeartTrailTracker _tracker;
        private readonly RejectedBalloonEffect _overflow;

        private IDisposable _subscription;
        private TrailSpawner _spawner;

        [Inject]
        internal HeartTrailController(
            IOverflowSettings settings,
            ISubscriber<OverflowHeartRequestedMessage> heartRequestedSubscriber,
            PoolManager poolManager,
            FlyingTrail prefab,
            TrailEndpointRegistry endpoints,
            HeartTrailTracker tracker,
            RejectedBalloonEffect overflow)
        {
            _settings = settings;
            _heartRequestedSubscriber = heartRequestedSubscriber;
            _poolManager = poolManager;
            _prefab = prefab;
            _endpoints = endpoints;
            _tracker = tracker;
            _overflow = overflow;
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        public void Start()
        {
            _spawner = new TrailSpawner(_poolManager, TrailPoolKey, _prefab);
            _subscription = _heartRequestedSubscriber.Subscribe(OnHeartRequested);
        }

        private void OnHeartRequested(OverflowHeartRequestedMessage msg)
        {
            if (!_endpoints.TryGet(TrailEndpointKeys.Heart, out var source))
            {
                return;
            }

            var from = source.Center;
            var requestId = msg.RequestId;
            var fallback = msg.TargetPosition;

            // Homes on the balloon's live position so it still lands as the pile compacts.
            Transform trail = null;
            trail = _spawner.SpawnFollow(
                from,
                () => _overflow.TryGetLivePosition(requestId, out var live) ? live : fallback,
                _settings.HeartTrailDuration,
                onArrived: () =>
                {
                    _overflow.OnHeartArrived(requestId);
                    _tracker.Remove(trail);
                });
            _tracker.Add(trail);
        }
    }
}
