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
        private readonly ISubscriber<BalloonScoredMessage> _scoredSubscriber;
        private readonly Dictionary<string, Func<Vector3>> _targetProviders = new();
        private readonly Dictionary<TrailId, Action<Transform>> _trackedTrails = new();
        private readonly ScorePointTrail _trailPrefab;

        private IDisposable _subscription;

        [Inject]
        internal ScoreTrailService(
            IGameConfiguration config,
            ISubscriber<BalloonScoredMessage> scoredSubscriber,
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
            _subscription = _scoredSubscriber.Subscribe(OnBalloonScored);
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

        /// <summary>
        ///     Pauses all in-flight trails of the same color whose score exceeds
        ///     the given threshold. These are post-tipping trails that belong to
        ///     the next level conceptually. Resumed automatically on cinematic end.
        /// </summary>
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

        /// <summary>
        ///     Registers a trail identity to watch for. When a trail with this
        ///     id is spawned, it is paused immediately and
        ///     <paramref name="onSpawned" /> is invoked with its transform.
        ///     If the trail is already in-flight, it is paused and the callback
        ///     fires immediately.
        /// </summary>
        internal void TrackTrail(TrailId id, Action<Transform> onSpawned)
        {
            if (_inFlightTrails.TryGetValue(id, out var existingTrail))
            {
                existingTrail.DOPause();
                onSpawned?.Invoke(existingTrail);
                return;
            }

            _trackedTrails[id] = onSpawned;
        }

        private Vector3[] ComputeOrigins(Vector3 center, int count)
        {
            if (count <= 0)
            {
                return Array.Empty<Vector3>();
            }

            var origins = new Vector3[count];

            if (count == 1)
            {
                origins[0] = center;
                return origins;
            }

            var radius = Mathf.Min(_config.SlotSeparation.x, _config.SlotSeparation.y) * 1.5f;
            for (var i = 0; i < count; i++)
            {
                var angle = 2f * Mathf.PI * i / count;
                origins[i] = center + (new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
            }

            return origins;
        }

        private void OnBalloonScored(BalloonScoredMessage msg)
        {
            if (msg.Points <= 0)
            {
                return;
            }

            if (!_targetProviders.ContainsKey(msg.ColorName))
            {
                Debug.LogWarning(
                    $"ScoreTrailService.OnBalloonScored: no target provider registered for " +
                    $"color \"{msg.ColorName}\" — score trail skipped.");
                return;
            }

            var origins = ComputeOrigins(msg.WorldPosition, msg.Points);
            var required = _config.PointsRequiredForLevel(msg.Level + 1);
            var ids = new TrailId[origins.Length];
            for (var i = 0; i < origins.Length; i++)
            {
                var rawScore = msg.CurrentProgress + i + 1;
                ids[i] = rawScore > required
                    ? new TrailId(msg.ColorName, rawScore - required, msg.Level + 1)
                    : new TrailId(msg.ColorName, rawScore, msg.Level);
            }

            SpawnTrailsAsync(msg.ColorName, origins, ids, msg.Level).Forget();
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

        private async UniTaskVoid SpawnTrailsAsync(
            string colorName,
            Vector3[] origins,
            TrailId[] ids,
            int baseLevel)
        {
            // Yield one frame so all BalloonScoredMessage handlers finish before
            // the first spawn. This lets LevelUpTrailEffect register TrackTrail
            // before the tipping trail is instantiated.
            await UniTask.Yield(cancellationToken: _cts.Token);

            var delayMs = Mathf.RoundToInt(_config.ScorePointsScatterDelay * 1000f);
            var poolKey = _poolKeys[colorName];

            for (var i = 0; i < origins.Length; i++)
            {
                // Only gate next-level trails — current-level trails from all
                // colors must keep arriving so CheckLevelUp can confirm progress.
                if (Cinematic.IsPlaying && ids[i].Level > baseLevel)
                {
                    await UniTask.WaitWhile(() => Cinematic.IsPlaying, cancellationToken: _cts.Token);
                }

                SpawnTrail(colorName, poolKey, origins[i], ids[i]);

                if (i < origins.Length - 1)
                {
                    await UniTask.Delay(delayMs, cancellationToken: _cts.Token);
                }
            }
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
    }
}
