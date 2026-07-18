using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Game.Run;
using BalloonParty.Game.Score.Behaviours;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using BalloonParty.UI.Score;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Game.Score
{
    internal class ScoreTrailService : IStartable, IDisposable, IRunResettable
    {
        private readonly IPublisher<ScoreTrailArrivedMessage> _arrivedPublisher;
        private readonly Dictionary<string, Color> _colorLookup = new();
        private readonly IGameConfiguration _config;
        private readonly CancellationTokenSource _cts = new();
        private readonly TrailFlightRegistry<TrailId> _flights = new();
        private readonly PoolManager _poolManager;
        private readonly Stack<ScoreTrailReporter> _reporterPool = new();
        private readonly ScoreTrailBehaviourResolver _resolver;
        private readonly ISubscriber<ScorePointsGroupMessage> _scoredSubscriber;
        private readonly Dictionary<string, TrailSpawner> _spawners = new();
        private readonly TrailEndpointRegistry _endpoints;
        private readonly FlyingTrail _trailPrefab;

        private CancellationTokenSource _groupCts = new();
        private IDisposable _scoreSubscription;

        internal TrailFlightRegistry<TrailId> Flights => _flights;
        internal FlyingTrail TrailPrefab => _trailPrefab;

        // Cancelling stale group-spawn loops is independent of gameplay state, so it can run first.
        public int ResetOrder => RunResetOrder.Quiesce;

        [Inject]
        internal ScoreTrailService(
            IGameConfiguration config,
            ISubscriber<ScorePointsGroupMessage> scoredSubscriber,
            IPublisher<ScoreTrailArrivedMessage> arrivedPublisher,
            PoolManager poolManager,
            TrailEndpointRegistry endpoints,
            ScoreTrailBehaviourResolver resolver,
            FlyingTrail trailPrefab)
        {
            _config = config;
            _scoredSubscriber = scoredSubscriber;
            _arrivedPublisher = arrivedPublisher;
            _poolManager = poolManager;
            _endpoints = endpoints;
            _resolver = resolver;
            _trailPrefab = trailPrefab;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _groupCts.Cancel();
            _groupCts.Dispose();
            _scoreSubscription?.Dispose();
        }

        public void Start()
        {
            _scoreSubscription = _scoredSubscriber.Subscribe(OnScorePointsGroup);
        }

        // Stop stale group spawns from bleeding into the next run; already-airborne trails are left to
        // finish (the ceremony/loss paths own their completion). Prewarm stays on the service-lifetime _cts.
        public void ResetRun(int generation)
        {
            _groupCts.Cancel();
            _groupCts.Dispose();
            _groupCts = new CancellationTokenSource();
        }

        internal ITrailEndpoint GetTarget(string colorName)
        {
            return _endpoints.TryGet(colorName, out var endpoint) ? endpoint : null;
        }

        internal void RegisterTarget(string colorName, ITrailEndpoint target, Color color)
        {
            _endpoints.Register(colorName, target);
            _colorLookup[colorName] = color;

            // Guards against a level restart re-registering the same color: without it, a second
            // RegisterTarget would prewarm on top of an already-populated pool and grow it unboundedly.
            if (_spawners.ContainsKey(colorName))
            {
                return;
            }

            var spawner = new TrailSpawner(_poolManager, $"ScoreTrail_{colorName}", _trailPrefab);
            _spawners[colorName] = spawner;

            // Amortized over frames so registering a color at level setup never spikes into a hitch.
            spawner.PrewarmAsync(_config.ScoreTrailPrewarmPerColor, _cts.Token).Forget();
        }

        private void OnScorePointsGroup(ScorePointsGroupMessage msg)
        {
            if (!_endpoints.TryGet(msg.ColorName, out var endpoint))
            {
                Debug.LogWarning(
                    $"ScoreTrailService: no target provider registered for " +
                    $"color \"{msg.ColorName}\" — score trail skipped.");
                return;
            }

            var color = _colorLookup.TryGetValue(msg.ColorName, out var c) ? c : Color.white;
            var reporter = RentReporter(msg.ColorName, msg.Points);
            var context = new ScoreTrailContext(
                msg.ColorName,
                color,
                msg.WorldPosition,
                msg.Points,
                msg.FirstScore,
                msg.LastScore,
                endpoint,
                _spawners[msg.ColorName],
                _flights,
                reporter,
                _config,
                _groupCts.Token);

            _resolver.Resolve(msg.Points).Begin(context);
        }

        private ScoreTrailReporter RentReporter(string colorName, int total)
        {
            var reporter = _reporterPool.Count > 0
                ? _reporterPool.Pop()
                : new ScoreTrailReporter(_arrivedPublisher, ReturnReporter);
            reporter.Begin(colorName, total);
            return reporter;
        }

        private void ReturnReporter(ScoreTrailReporter reporter)
        {
            _reporterPool.Push(reporter);
        }

        // One per in-flight group; recycled once its reports sum to the group total. Publishes each arrival
        // and (dev builds) polices the handler contract with ORDER-INDEPENDENT invariants only: report
        // order is deliberately unconstrained because TrailFlightRegistry.CompleteAll (level-up/loss)
        // fires remaining arrivals in dictionary order, and the LevelController watermark is order-safe.
        private sealed class ScoreTrailReporter : IScoreTrailReporter
        {
            private readonly IPublisher<ScoreTrailArrivedMessage> _publisher;
            private readonly Action<ScoreTrailReporter> _recycle;

            private string _colorName;
            private int _total;
            private int _cumulative;

            internal ScoreTrailReporter(
                IPublisher<ScoreTrailArrivedMessage> publisher, Action<ScoreTrailReporter> recycle)
            {
                _publisher = publisher;
                _recycle = recycle;
            }

            internal void Begin(string colorName, int total)
            {
                _colorName = colorName;
                _total = total;
                _cumulative = 0;
            }

            public void ReportArrival(int score, int points, Vector3 at)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Assert(_cumulative + points <= _total,
                    $"ScoreTrail {_colorName}: reported points {_cumulative + points} exceed group total {_total}.");
#endif
                _cumulative += points;
                _publisher.Publish(new ScoreTrailArrivedMessage(_colorName, score, points, at));

                if (_cumulative < _total)
                {
                    return;
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Assert(_cumulative == _total,
                    $"ScoreTrail {_colorName}: final reports summed to {_cumulative}, expected {_total}.");
#endif
                _recycle(this);
            }
        }
    }
}
