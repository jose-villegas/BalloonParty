using System;
using System.Collections.Generic;
using System.Threading;
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
    internal class ScoreTrailService : IStartable, IDisposable
    {
        private readonly IPublisher<ScoreTrailArrivedMessage> _arrivedPublisher;
        private readonly Dictionary<string, Color> _colorLookup = new();
        private readonly IGameConfiguration _config;
        private readonly CancellationTokenSource _cts = new();
        private readonly TrailFlightRegistry<TrailId> _flights = new();
        private readonly PoolManager _poolManager;
        private readonly ISubscriber<ScorePointMessage> _scoredSubscriber;
        private readonly Dictionary<string, TrailSpawner> _spawners = new();
        private readonly Dictionary<string, ITrailTarget> _targets = new();
        private readonly FlyingTrail _trailPrefab;

        private IDisposable _scoreSubscription;

        internal TrailFlightRegistry<TrailId> Flights => _flights;
        internal FlyingTrail TrailPrefab => _trailPrefab;

        [Inject]
        internal ScoreTrailService(
            IGameConfiguration config,
            ISubscriber<ScorePointMessage> scoredSubscriber,
            IPublisher<ScoreTrailArrivedMessage> arrivedPublisher,
            PoolManager poolManager,
            FlyingTrail trailPrefab)
        {
            _config = config;
            _scoredSubscriber = scoredSubscriber;
            _arrivedPublisher = arrivedPublisher;
            _poolManager = poolManager;
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
            _scoreSubscription = _scoredSubscriber.Subscribe(OnScorePoint);
        }

        internal ITrailTarget GetTarget(string colorName)
        {
            return _targets[colorName];
        }

        private void OnScorePoint(ScorePointMessage msg)
        {
            if (!_targets.ContainsKey(msg.ColorName))
            {
                Debug.LogWarning(
                    $"ScoreTrailService: no target provider registered for " +
                    $"color \"{msg.ColorName}\" — score trail skipped.");
                return;
            }

            var id = new TrailId(msg);
            var center = msg.WorldPosition;
            var origin = ComputeScatterOrigin(center, msg.GroupIndex, msg.GroupSize);

            SpawnTrailAsync(msg.ColorName, center, origin, id, msg.GroupIndex).Forget();
        }

        internal void RegisterTarget(string colorName, ITrailTarget target, Color color)
        {
            _targets[colorName] = target;
            _colorLookup[colorName] = color;

            if (!_spawners.ContainsKey(colorName))
            {
                var poolKey = $"ScoreTrail_{colorName}";
                _spawners[colorName] = new TrailSpawner(
                    _poolManager,
                    poolKey,
                    () => new SimplePoolChannel<FlyingTrail>(_trailPrefab));
            }
        }

        private Vector3 ComputeScatterOrigin(Vector3 center, int index, int count)
        {
            if (count <= 1)
            {
                return center;
            }

            var radius = Mathf.Min(_config.SlotSeparation.x, _config.SlotSeparation.y) * 1.5f;
            var angle = 2f * Mathf.PI * index / count;
            return center + (new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
        }

        private void SpawnTrail(string colorName, Vector3 center, Vector3 scatterOrigin, TrailId id)
        {
            var target = _targets[colorName].RandomPosition();
            var color = _colorLookup.TryGetValue(colorName, out var c) ? c : Color.white;
            var spawner = _spawners[colorName];
            var hasBurst = scatterOrigin != center;

            Action onArrived = () =>
            {
                _flights.Unregister(id);
                _arrivedPublisher.Publish(
                    new ScoreTrailArrivedMessage(colorName, id.Score, id.Level, target));
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

        private async UniTaskVoid SpawnTrailAsync(
            string colorName,
            Vector3 center,
            Vector3 scatterOrigin,
            TrailId id,
            int groupIndex)
        {
            if (groupIndex > 0)
            {
                var delayMs = Mathf.RoundToInt(_config.ScorePointsScatterDelay * 1000f) * groupIndex;
                await UniTask.Delay(delayMs, cancellationToken: _cts.Token);
            }

            SpawnTrail(colorName, center, scatterOrigin, id);
        }
    }
}
