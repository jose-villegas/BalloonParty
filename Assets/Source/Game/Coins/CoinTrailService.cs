using System;
using System.Threading;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Actor;
using BalloonParty.UI.Score;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Game.Coins
{
    /// <summary>
    ///     Flies each dropped coin from its pop to the coin counter and publishes <see cref="CoinLandedMessage" />
    ///     when it lands, which is when the wallet counts it. With no counter registered (a scene without the
    ///     HUD, the tests) a coin lands at once. Coins still flying when the run resets or the board changes
    ///     land at once too. They fly on real time: a coin is headed for the HUD, so it keeps moving through
    ///     the level-up popup's pause and the loss's slow motion. Only closing the game mid-flight loses one.
    /// </summary>
    internal sealed class CoinTrailService : IStartable, IDisposable, IRunResettable, ITransitionOutgoingContent
    {
        private const string TrailPoolKey = "CoinTrail";

        // Above the score trails (3300) and the level-up popup under them, so a coin landing while the popup is
        // up stays in view.
        private const int CoinTrailSortingOrder = 3400;

        private readonly ICoinSettings _settings;
        private readonly ISubscriber<CoinDroppedMessage> _droppedSubscriber;
        private readonly IPublisher<CoinLandedMessage> _landedPublisher;
        private readonly PoolManager _poolManager;
        private readonly TrailFlightRegistry<int> _flights = new();
        private readonly CancellationTokenSource _cts = new();

        private IDisposable _subscription;
        private ITrailEndpoint _target;
        private TrailSpawner _spawner;
        private int _nextFlightId;

        internal int FlyingCount => _flights.ActiveCount;

        // Lands the coins still flying before anything else resets, so they count toward the run they came from.
        public int ResetOrder => RunResetOrder.Quiesce;

        public CoinTrailService(
            IRunConfig config,
            ISubscriber<CoinDroppedMessage> droppedSubscriber,
            IPublisher<CoinLandedMessage> landedPublisher,
            PoolManager poolManager)
        {
            _settings = config.Coins;
            _droppedSubscriber = droppedSubscriber;
            _landedPublisher = landedPublisher;
            _poolManager = poolManager;
        }

        public void Start()
        {
            _subscription = _droppedSubscriber.Subscribe(OnCoinDropped);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _subscription?.Dispose();
        }

        public void ResetRun(int generation)
        {
            _flights.CompleteAll();
        }

        // The board is about to change (a level-up, or the loss's camera-down) and these coins came from the
        // old one: land them now rather than have them cross into the next level.
        public void HoldOutgoing(Transform outgoingRoot, float exitDrop)
        {
            _flights.CompleteAll();
        }

        public void ReleaseOutgoing()
        {
        }

        /// <summary>
        ///     Called by the counter's view: where coins fly to, and the trail they fly as. The pool is made from
        ///     the first registration's prefab; a later one only moves the target.
        /// </summary>
        internal void RegisterTarget(ITrailEndpoint target, FlyingTrail trailPrefab)
        {
            if (trailPrefab == null)
            {
                Log.Error("Coins", "The coin counter has no trail prefab; coins will land without flying.");
                return;
            }

            _target = target;
            if (_spawner != null)
            {
                return;
            }

            _spawner = new TrailSpawner(_poolManager, TrailPoolKey, trailPrefab, CoinTrailSortingOrder);
            _spawner.PrewarmAsync(_settings.TrailPrewarm, _cts.Token).Forget();
        }

        /// <summary>The counter is going away: later coins land at once, and those flying finish where they aimed.</summary>
        internal void UnregisterTarget(ITrailEndpoint target)
        {
            if (_target == target)
            {
                _target = null;
            }
        }

        private void OnCoinDropped(CoinDroppedMessage message)
        {
            if (_target == null)
            {
                _landedPublisher.Publish(new CoinLandedMessage(message.Amount, message.WorldPosition));
                return;
            }

            var id = _nextFlightId++;
            var amount = message.Amount;
            var to = _target.Center;

            // Lands where it was aimed, not at the target's live position: the counter may be gone by then.
            var trail = _spawner.Spawn(message.WorldPosition, to, _settings.TrailDuration, useUnscaledTime: true,
                onArrived: () =>
                {
                    _flights.Unregister(id);
                    _landedPublisher.Publish(new CoinLandedMessage(amount, to));
                });
            _flights.Register(id, trail, message.WorldPosition);
        }
    }
}
