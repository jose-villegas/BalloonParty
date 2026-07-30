using System;
using BalloonParty.Configuration;
using BalloonParty.Game.Health;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Grid;
using BalloonParty.UI.Score;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.Health
{
    /// <summary>
    ///     Spawns one heart trail per heart lost when a <see cref="WaveDamageMessage"/> arrives.
    ///     Each trail flies from the health UI to the overflow row area above the grid.
    /// </summary>
    internal sealed class HeartTrailController : IStartable, IDisposable
    {
        private const string TrailPoolKey = "HeartTrail";

        private readonly IOverflowSettings _settings;
        private readonly ISubscriber<WaveDamageMessage> _waveDamageSubscriber;
        private readonly PoolManager _poolManager;
        private readonly FlyingTrail _prefab;
        private readonly TrailEndpointRegistry _endpoints;
        private readonly HeartTrailTracker _tracker;
        private readonly SlotGrid _grid;

        private IDisposable _subscription;
        private TrailSpawner _spawner;

        [Inject]
        internal HeartTrailController(
            IOverflowSettings settings,
            ISubscriber<WaveDamageMessage> waveDamageSubscriber,
            PoolManager poolManager,
            FlyingTrail prefab,
            TrailEndpointRegistry endpoints,
            HeartTrailTracker tracker,
            SlotGrid grid)
        {
            _settings = settings;
            _waveDamageSubscriber = waveDamageSubscriber;
            _poolManager = poolManager;
            _prefab = prefab;
            _endpoints = endpoints;
            _tracker = tracker;
            _grid = grid;
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        public void Start()
        {
            _spawner = new TrailSpawner(_poolManager, TrailPoolKey, _prefab);
            _subscription = _waveDamageSubscriber.Subscribe(OnWaveDamage);
        }

        private void OnWaveDamage(WaveDamageMessage msg)
        {
            if (!_endpoints.TryGet(TrailEndpointKeys.Heart, out var source))
            {
                return;
            }

            var from = source.Center;
            var midCol = (_grid.Columns - 1) * 0.5f;

            for (var i = 0; i < msg.HeartsLost; i++)
            {
                // Target: the row just above the grid, staggered per heart so trails don't overlap.
                var targetRow = _grid.Rows + i;
                var target = _grid.IndexToWorldPosition(new Vector2Int(Mathf.RoundToInt(midCol), targetRow));

                Transform trail = null;
                trail = _spawner.Spawn(
                    from,
                    target,
                    _settings.HeartTrailDuration,
                    onArrived: () => _tracker.Remove(trail));
                _tracker.Add(trail);
            }
        }
    }
}
