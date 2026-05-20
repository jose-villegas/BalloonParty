using System;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots;
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
        private readonly SlotGrid _slotGrid;
        private readonly Func<Vector3> _targetProvider;

        private IDisposable _subscription;
        private TrailSpawner _spawner;

        [Inject]
        internal ShieldTrailController(
            IGameConfiguration config,
            ISubscriber<ShieldGainedMessage> shieldGainedSubscriber,
            PoolManager poolManager,
            SlotGrid slotGrid,
            FlyingTrail prefab,
            Func<Vector3> targetProvider)
        {
            _config = config;
            _shieldGainedSubscriber = shieldGainedSubscriber;
            _poolManager = poolManager;
            _slotGrid = slotGrid;
            _prefab = prefab;
            _targetProvider = targetProvider;
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        public void Start()
        {
            _spawner = new TrailSpawner(
                _poolManager,
                TrailPoolKey,
                () => new ShieldTrailPoolChannel(_prefab));

            _subscription = _shieldGainedSubscriber.Subscribe(OnShieldGained);
        }

        private void OnShieldGained(ShieldGainedMessage msg)
        {
            var fromWorldPosition = _slotGrid.IndexToWorldPosition(msg.SlotIndex);
            _spawner.Spawn(fromWorldPosition, _targetProvider(), _config.ShieldTrailDuration);
        }
    }
}
