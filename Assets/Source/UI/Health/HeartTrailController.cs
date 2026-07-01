using System;
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
    ///     Flies a heart trail from the health UI to each overflow pop — the visual of a hit point
    ///     being spent on a balloon that couldn't fit. Triggered by <see cref="SpawnBlockedMessage"/>,
    ///     the same signal that charges the HP, whose <c>Position</c> is the pop's world point. Mirrors
    ///     <c>ShieldTrailController</c> (reversed: UI → world). The heart-trail cinematic follows these.
    /// </summary>
    internal sealed class HeartTrailController : IStartable, IDisposable
    {
        private const string TrailPoolKey = "HeartTrail";

        private readonly IOverflowSettings _settings;
        private readonly ISubscriber<SpawnBlockedMessage> _blockedSubscriber;
        private readonly PoolManager _poolManager;
        private readonly FlyingTrail _prefab;
        private readonly TrailEndpointRegistry _endpoints;
        private readonly HeartTrailTracker _tracker;

        private IDisposable _subscription;
        private TrailSpawner _spawner;

        [Inject]
        internal HeartTrailController(
            IOverflowSettings settings,
            ISubscriber<SpawnBlockedMessage> blockedSubscriber,
            PoolManager poolManager,
            FlyingTrail prefab,
            TrailEndpointRegistry endpoints,
            HeartTrailTracker tracker)
        {
            _settings = settings;
            _blockedSubscriber = blockedSubscriber;
            _poolManager = poolManager;
            _prefab = prefab;
            _endpoints = endpoints;
            _tracker = tracker;
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        public void Start()
        {
            _spawner = new TrailSpawner(_poolManager, TrailPoolKey, _prefab);

            _subscription = _blockedSubscriber.Subscribe(OnBlocked);
        }

        private void OnBlocked(SpawnBlockedMessage msg)
        {
            if (!_endpoints.TryGet(TrailEndpointKeys.Heart, out var source))
            {
                return;
            }

            Transform trail = null;
            trail = _spawner.Spawn(
                source.Center,
                msg.Position,
                _settings.HeartTrailDuration,
                onArrived: () => _tracker.Remove(trail));
            _tracker.Add(trail);
        }
    }
}
