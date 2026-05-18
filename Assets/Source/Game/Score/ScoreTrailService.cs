using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
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
    internal class ScoreTrailService : IStartable, IDisposable, ICinematicAware
    {
        private readonly IPublisher<ScoreTrailArrivedMessage> _arrivedPublisher;
        private readonly HashSet<TrailId> _cinematicPausedTrails = new();
        private readonly Dictionary<string, Color> _colorLookup = new();
        private readonly IGameConfiguration _config;
        private readonly CancellationTokenSource _cts = new();
        private readonly Dictionary<TrailId, Transform> _inFlightTrails = new();
        private readonly Dictionary<string, string> _poolKeys = new();
        private readonly PoolManager _poolManager;
        private readonly ISubscriber<ScorePointMessage> _scoredSubscriber;
        private readonly Dictionary<string, Func<Vector3>> _targetProviders = new();
        private readonly Dictionary<TrailId, Action<Transform>> _trackedTrails = new();
        private readonly ScorePointTrail _trailPrefab;

        private IDisposable _subscription;

        [Inject]
        internal ScoreTrailService(
            IGameConfiguration config,
            ISubscriber<ScorePointMessage> scoredSubscriber,
            IPublisher<ScoreTrailArrivedMessage> arrivedPublisher,
            PoolManager poolManager,
            ScorePointTrail trailPrefab)
        {
            _config = config;
            _scoredSubscriber = scoredSubscriber;
            _arrivedPublisher = arrivedPublisher;
            _poolManager = poolManager;
            _trailPrefab = trailPrefab;
        }

        public void Dispose()
        {
            Cinematic.Unregister(this);
            _cts.Cancel();
            _cts.Dispose();
            _subscription?.Dispose();
        }

        public void Start()
        {
            _subscription = _scoredSubscriber.Subscribe(OnScorePoint);
            Cinematic.Register(this);
        }

        public void OnCinematicBegin(CinematicState state)
        {
        }

        public void OnCinematicEnd()
        {
            ResumeActiveTrails();
        }

        public void RegisterTarget(string colorName, Func<Vector3> targetProvider, Color color)
        {
            _targetProviders[colorName] = targetProvider;
            _colorLookup[colorName] = color;

            if (!_poolKeys.ContainsKey(colorName))
            {
                _poolKeys[colorName] = $"ScoreTrail_{colorName}";
            }
        }

        internal void ClearTrackedTrail(TrailId id)
        {
            _trackedTrails.Remove(id);
        }

        internal Transform GetTrailTransform(TrailId id)
        {
            return _inFlightTrails.TryGetValue(id, out var t) ? t : null;
        }

        internal void PauseTrailsAbove(TrailId threshold)
        {
            foreach (var kvp in _inFlightTrails)
            {
                if (kvp.Key.Level <= threshold.Level)
                {
                    continue;
                }

                if (kvp.Value != null)
                {
                    kvp.Value.DOPause();
                    _cinematicPausedTrails.Add(kvp.Key);
                }
            }
        }

        internal void ResumeTrail(TrailId id)
        {
            var t = GetTrailTransform(id);
            if (t != null)
            {
                t.DOPlay();
            }
        }

        internal void TrackTrail(TrailId id, Action<Transform> onSpawned)
        {
            if (_inFlightTrails.TryGetValue(id, out var existingTrail))
            {
                existingTrail.DOPause();

                // Trail was spawned before tracking — switch to unscaled time
                // so it runs during slow-motion.
                var tweens = DOTween.TweensByTarget(existingTrail);
                if (tweens != null)
                {
                    foreach (var tween in tweens)
                    {
                        tween.SetUpdate(true);
                    }
                }

                onSpawned?.Invoke(existingTrail);
                return;
            }

            _trackedTrails[id] = onSpawned;
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

        private void OnScorePoint(ScorePointMessage msg)
        {
            if (!_targetProviders.ContainsKey(msg.ColorName))
            {
                Debug.LogWarning(
                    $"ScoreTrailService: no target provider registered for " +
                    $"color \"{msg.ColorName}\" — score trail skipped.");
                return;
            }

            var id = new TrailId(msg);
            var origin = ComputeScatterOrigin(msg.WorldPosition, msg.GroupIndex, msg.GroupSize);

            SpawnTrailAsync(msg.ColorName, origin, id, msg.NextLevel, msg.GroupIndex).Forget();
        }

        private void ResumeActiveTrails()
        {
            foreach (var id in _cinematicPausedTrails)
            {
                if (_inFlightTrails.TryGetValue(id, out var trail) && trail != null)
                {
                    trail.DOPlay();
                }
            }

            _cinematicPausedTrails.Clear();
        }

        private void SpawnTrail(string colorName, string poolKey, Vector3 fromWorldPosition, TrailId id)
        {
            var target = _targetProviders[colorName]();
            var color = _colorLookup.TryGetValue(colorName, out var c) ? c : Color.white;

            var trail = _poolManager.GetOrRegister(poolKey,
                () => new ScoreTrailPoolChannel(_trailPrefab));

            trail.transform.position = fromWorldPosition;
            trail.transform.localScale = Vector3.one;

            _inFlightTrails[id] = trail.transform;

            var isTracked = _trackedTrails.TryGetValue(id, out var trackedCallback);

            trail.Setup(target,
                color,
                _config.ScorePointTraceDuration,
                () =>
                {
                    _inFlightTrails.Remove(id);
                    _cinematicPausedTrails.Remove(id);
                    _arrivedPublisher.Publish(
                        new ScoreTrailArrivedMessage(colorName, id.Score, id.Level, target));
                    _poolManager.Return(poolKey, trail);
                },
                isTracked);

            if (isTracked)
            {
                trail.transform.DOPause();
                trackedCallback?.Invoke(trail.transform);
            }
        }

        private async UniTaskVoid SpawnTrailAsync(
            string colorName,
            Vector3 origin,
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

            SpawnTrail(colorName, _poolKeys[colorName], origin, id);
        }
    }
}
