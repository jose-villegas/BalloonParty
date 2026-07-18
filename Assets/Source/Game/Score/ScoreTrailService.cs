using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
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
    internal class ScoreTrailService : IStartable, IDisposable
    {
        private readonly IPublisher<ScoreTrailArrivedMessage> _arrivedPublisher;
        private readonly Dictionary<string, Color> _colorLookup = new();
        private readonly IGameConfiguration _config;
        private readonly CancellationTokenSource _cts = new();
        private readonly TrailFlightRegistry<TrailId> _flights = new();
        private readonly PoolManager _poolManager;
        private readonly ISubscriber<ScorePointsGroupMessage> _scoredSubscriber;
        private readonly Dictionary<string, TrailSpawner> _spawners = new();
        private readonly TrailEndpointRegistry _endpoints;
        private readonly FlyingTrail _trailPrefab;

        private IDisposable _scoreSubscription;

        internal TrailFlightRegistry<TrailId> Flights => _flights;
        internal FlyingTrail TrailPrefab => _trailPrefab;

        [Inject]
        internal ScoreTrailService(
            IGameConfiguration config,
            ISubscriber<ScorePointsGroupMessage> scoredSubscriber,
            IPublisher<ScoreTrailArrivedMessage> arrivedPublisher,
            PoolManager poolManager,
            TrailEndpointRegistry endpoints,
            FlyingTrail trailPrefab)
        {
            _config = config;
            _scoredSubscriber = scoredSubscriber;
            _arrivedPublisher = arrivedPublisher;
            _poolManager = poolManager;
            _endpoints = endpoints;
            _trailPrefab = trailPrefab;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _scoreSubscription?.Dispose();
        }

        public void Start()
        {
            _scoreSubscription = _scoredSubscriber.Subscribe(OnScorePointsGroup);
        }

        internal ITrailEndpoint GetTarget(string colorName)
        {
            return _endpoints.TryGet(colorName, out var endpoint) ? endpoint : null;
        }

        private void OnScorePointsGroup(ScorePointsGroupMessage msg)
        {
            if (!_endpoints.TryGet(msg.ColorName, out _))
            {
                Debug.LogWarning(
                    $"ScoreTrailService: no target provider registered for " +
                    $"color \"{msg.ColorName}\" — score trail skipped.");
                return;
            }

            SpawnGroupAsync(msg).Forget();
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

        private Vector3 ComputeScatterOrigin(Vector3 center, int index, int count)
        {
            if (count <= 1)
            {
                return center;
            }

            var radius = Mathf.Min(_config.SlotSeparation.x, _config.SlotSeparation.y) * 1.5f;
            var angle = 2f * Mathf.PI * index / count;
            Vector3 direction = VectorMathExtensions.DirectionFromAngle(angle);
            return center + direction * radius;
        }

        private void SpawnTrail(string colorName, Vector3 center, Vector3 scatterOrigin, TrailId id)
        {
            var target = _endpoints.TryGet(colorName, out var endpoint) ? endpoint.RandomPosition() : Vector3.zero;
            var color = _colorLookup.TryGetValue(colorName, out var c) ? c : Color.white;
            var spawner = _spawners[colorName];
            var hasBurst = scatterOrigin != center;

            Action onArrived = () =>
            {
                _flights.Unregister(id);
                _arrivedPublisher.Publish(
                    new ScoreTrailArrivedMessage(colorName, id.Score, points: 1, target));
            };

            Transform transform;
            if (hasBurst)
            {
                transform = spawner.SpawnBurst(center,
                    scatterOrigin,
                    target,
                    _config.ScorePointBurstDuration,
                    _config.ScorePointTraceDuration,
                    color,
                    onArrived);
            }
            else
            {
                transform = spawner.Spawn(scatterOrigin,
                    target,
                    _config.ScorePointTraceDuration,
                    color,
                    onArrived);
            }

            _flights.Register(id, transform, center);
        }

        // One state machine per group reproduces today's per-point schedule (spawn i at 0.02 s × i):
        // the first spawn is immediate, then each iteration awaits until its shared-start target time.
        // Scheduling against t0 (scaled, like the delay) instead of chaining fixed waits keeps frame
        // rounding from accumulating per step — a chained 20 ms wait rounds up to a whole frame every
        // iteration, stretching a long group's tail by tens of percent at 60 Hz. A late frame simply
        // spawns the overdue points immediately, exactly like the old parallel per-point delays.
        private async UniTaskVoid SpawnGroupAsync(ScorePointsGroupMessage msg)
        {
            var center = msg.WorldPosition;
            var count = msg.Points;
            var delay = _config.ScorePointsScatterDelay;
            var start = Time.time;

            for (var i = 0; i < count; i++)
            {
                var remainingMs = Mathf.RoundToInt((start + delay * i - Time.time) * 1000f);
                if (remainingMs > 0)
                {
                    await UniTask.Delay(remainingMs, cancellationToken: _cts.Token);
                }

                var id = new TrailId(msg.ColorName, msg.FirstScore + i);
                var origin = ComputeScatterOrigin(center, i, count);
                SpawnTrail(msg.ColorName, center, origin, id);
            }
        }
    }
}
