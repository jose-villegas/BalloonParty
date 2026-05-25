using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Pool;
using BalloonParty.UI.Score;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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
        private readonly PoolManager _poolManager;
        private readonly ISubscriber<ScorePointMessage> _scoredSubscriber;
        private readonly ISubscriber<PausedMessage> _pausedSubscriber;
        private readonly ISubscriber<ResumedMessage> _resumedSubscriber;
        private readonly Dictionary<string, TrailSpawner> _spawners = new();
        private readonly Dictionary<string, ITrailTarget> _targets = new();
        private readonly TrailTracker<TrailId> _tracker = new();
        private readonly FlyingTrail _trailPrefab;

        private IDisposable _scoreSubscription;
        private IDisposable _pauseSubscription;
        private IDisposable _resumeSubscription;

        internal TrailTracker<TrailId> Tracker => _tracker;

        internal ITrailTarget GetTarget(string colorName)
        {
            return _targets[colorName];
        }

        [Inject]
        internal ScoreTrailService(
            IGameConfiguration config,
            ISubscriber<ScorePointMessage> scoredSubscriber,
            ISubscriber<PausedMessage> pausedSubscriber,
            ISubscriber<ResumedMessage> resumedSubscriber,
            IPublisher<ScoreTrailArrivedMessage> arrivedPublisher,
            PoolManager poolManager,
            FlyingTrail trailPrefab)
        {
            _config = config;
            _scoredSubscriber = scoredSubscriber;
            _pausedSubscriber = pausedSubscriber;
            _resumedSubscriber = resumedSubscriber;
            _arrivedPublisher = arrivedPublisher;
            _poolManager = poolManager;
            _trailPrefab = trailPrefab;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _scoreSubscription?.Dispose();
            _pauseSubscription?.Dispose();
            _resumeSubscription?.Dispose();
        }

        public void Start()
        {
            _scoreSubscription = _scoredSubscriber.Subscribe(OnScorePoint);
            _pauseSubscription = _pausedSubscriber.Subscribe(OnPaused);
            _resumeSubscription = _resumedSubscriber.Subscribe(OnResumed);
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

            SpawnTrailAsync(msg.ColorName, center, origin, id, msg.NextLevel, msg.GroupIndex).Forget();
        }

        private void OnPaused(PausedMessage msg)
        {
            if (msg.Source == PauseSource.Cinematic)
            {
                _tracker.PauseWhere(_ => true);
            }
        }

        private void OnResumed(ResumedMessage msg)
        {
            if (msg.Source == PauseSource.Cinematic)
            {
                _tracker.ResumeAll();
            }
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
                    () => new ScoreTrailPoolChannel(_trailPrefab));
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
            var isTracked = _tracker.IsTracked(id, out var trackedCallback);
            var spawner = _spawners[colorName];
            var hasBurst = scatterOrigin != center;

            Transform transform;
            if (hasBurst)
            {
                transform = isTracked
                    ? spawner.SpawnBurst(center, scatterOrigin, target,
                        _config.ScorePointBurstDuration, _config.ScorePointTraceDuration,
                        color, () =>
                        {
                            _tracker.Unregister(id);
                            _arrivedPublisher.Publish(
                                new ScoreTrailArrivedMessage(colorName, id.Score, id.Level, target));
                        }, true)
                    : spawner.SpawnBurst(center, scatterOrigin, target,
                        _config.ScorePointBurstDuration, _config.ScorePointTraceDuration,
                        color, () =>
                        {
                            _tracker.Unregister(id);
                            _arrivedPublisher.Publish(
                                new ScoreTrailArrivedMessage(colorName, id.Score, id.Level, target));
                        });
            }
            else
            {
                transform = isTracked
                    ? spawner.SpawnUnscaled(scatterOrigin, target, _config.ScorePointTraceDuration,
                        color, () =>
                        {
                            _tracker.Unregister(id);
                            _arrivedPublisher.Publish(
                                new ScoreTrailArrivedMessage(colorName, id.Score, id.Level, target));
                        })
                    : spawner.Spawn(scatterOrigin, target, _config.ScorePointTraceDuration,
                        color, () =>
                        {
                            _tracker.Unregister(id);
                            _arrivedPublisher.Publish(
                                new ScoreTrailArrivedMessage(colorName, id.Score, id.Level, target));
                        });
            }

            _tracker.Register(id, transform);

            if (isTracked)
            {
                transform.DOPause();
                trackedCallback?.Invoke(transform);
            }
        }

        private async UniTaskVoid SpawnTrailAsync(
            string colorName,
            Vector3 center,
            Vector3 scatterOrigin,
            TrailId id,
            bool nextLevel,
            int groupIndex)
        {
            if (groupIndex > 0)
            {
                var delayMs = Mathf.RoundToInt(_config.ScorePointsScatterDelay * 1000f) * groupIndex;
                await UniTask.Delay(delayMs, cancellationToken: _cts.Token);
            }

            // Next-level trails must wait for the cinematic to finish.
            if (Cinematic.IsPlaying && nextLevel)
            {
                await UniTask.WaitWhile(() => Cinematic.IsPlaying, cancellationToken: _cts.Token);
            }

            SpawnTrail(colorName, center, scatterOrigin, id);
        }
    }
}
