using System;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using BalloonParty.UI.Score;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.Shields
{
    internal class ShieldTrailController : IStartable, IDisposable
    {
        private const string TrailPoolKey = "ShieldTrail";

        private readonly IGameConfiguration _config;
        private readonly FlyingTrail _prefab;
        private readonly PoolManager _poolManager;
        private readonly ISubscriber<ShieldGainedMessage> _shieldGainedSubscriber;
        private readonly ISubscriber<ShieldLostMessage> _shieldLostSubscriber;
        private readonly SlotGrid _slotGrid;
        private readonly TrailEndpointRegistry _endpoints;

        private IDisposable _gainedSubscription;
        private IDisposable _lostSubscription;
        private TrailSpawner _spawner;

        [Inject]
        internal ShieldTrailController(
            IGameConfiguration config,
            ISubscriber<ShieldGainedMessage> shieldGainedSubscriber,
            ISubscriber<ShieldLostMessage> shieldLostSubscriber,
            PoolManager poolManager,
            SlotGrid slotGrid,
            FlyingTrail prefab,
            TrailEndpointRegistry endpoints)
        {
            _config = config;
            _shieldGainedSubscriber = shieldGainedSubscriber;
            _shieldLostSubscriber = shieldLostSubscriber;
            _poolManager = poolManager;
            _slotGrid = slotGrid;
            _prefab = prefab;
            _endpoints = endpoints;
        }

        public void Dispose()
        {
            _gainedSubscription?.Dispose();
            _lostSubscription?.Dispose();
        }

        public void Start()
        {
            _spawner = new TrailSpawner(_poolManager, TrailPoolKey, _prefab);
            _gainedSubscription = _shieldGainedSubscriber.Subscribe(OnShieldGained);
            _lostSubscription = _shieldLostSubscriber.Subscribe(OnShieldLost);
        }

        // Shield gained: a trail flies from the balloon that granted it up to the shield HUD.
        private void OnShieldGained(ShieldGainedMessage msg)
        {
            if (!_endpoints.TryGet(TrailEndpointKeys.Shield, out var target))
            {
                return;
            }

            var fromWorldPosition = _slotGrid.IndexToWorldPosition(msg.SlotIndex);
            _spawner.Spawn(fromWorldPosition, target.Center, _config.ShieldTrailDuration);
        }

        // Shield lost: the reverse — a trail flies from the HUD down to the wall bounce that spent it.
        private void OnShieldLost(ShieldLostMessage msg)
        {
            if (!_endpoints.TryGet(TrailEndpointKeys.Shield, out var source))
            {
                return;
            }

            _spawner.Spawn(source.Center, msg.Position, _config.ShieldTrailDuration, motion: TrailMotion.Return);
        }
    }
}
