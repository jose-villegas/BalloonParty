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

namespace BalloonParty.Game
{
    internal class ScoreTrailService : IStartable, IDisposable, ICinematicAware
    {
        private readonly IGameConfiguration _config;
        private readonly IPublisher<ScoreTrailArrivedMessage> _arrivedPublisher;
        private readonly ISubscriber<BalloonScoredMessage> _scoredSubscriber;
        private readonly PoolManager _poolManager;
        private readonly ScorePointTrail _trailPrefab;
        private readonly Dictionary<string, Color> _colorLookup = new();
        private readonly Dictionary<string, string> _poolKeys = new();
        private readonly Dictionary<string, Func<Vector3>> _targetProviders = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Dictionary<string, Transform> _lastSpawnedTrails = new();
        private readonly HashSet<Transform> _activeTrails = new();

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
            PauseActiveTrails();
        }

        public void OnCinematicEnd()
        {
            ResumeActiveTrails();
        }

        private void PauseActiveTrails()
        {
            foreach (var trail in _activeTrails)
            {
                if (trail != null)
                {
                    trail.DOPause();
                }
            }
        }

        private void ResumeActiveTrails()
        {
            foreach (var trail in _activeTrails)
            {
                if (trail != null)
                {
                    trail.DOPlay();
                }
            }
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

        internal Transform GetLastSpawnedTrail(string colorName)
        {
            return _lastSpawnedTrails.GetValueOrDefault(colorName);
        }


        /// <summary>
        ///     Resumes the tweens on a specific trail so the cinematic can
        ///     track it while all other trails stay paused.
        /// </summary>
        internal void ResumeTrail(Transform trail)
        {
            if (trail != null)
            {
                trail.DOPlay();
            }
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
            SpawnTrailsAsync(msg.ColorName, origins).Forget();
        }

        private async UniTaskVoid SpawnTrailsAsync(string colorName, Vector3[] origins)
        {
            var delayMs = Mathf.RoundToInt(_config.ScorePointsScatterDelay * 1000f);
            var poolKey = _poolKeys[colorName];

            for (var i = 0; i < origins.Length; i++)
            {
                if (Cinematic.IsPlaying)
                {
                    await UniTask.WaitUntil(
                        () => !Cinematic.IsPlaying,
                        cancellationToken: _cts.Token);
                }

                SpawnTrail(colorName, poolKey, origins[i]);
                if (i < origins.Length - 1)
                {
                    await UniTask.Delay(delayMs, cancellationToken: _cts.Token);
                }
            }
        }

        private void SpawnTrail(string colorName, string poolKey, Vector3 fromWorldPosition)
        {
            var target = _targetProviders[colorName]();
            var color = _colorLookup.TryGetValue(colorName, out var c) ? c : Color.white;

            var trail = _poolManager.GetOrRegister(poolKey,
                () => new ScoreTrailPoolChannel(_trailPrefab));

            trail.transform.position = fromWorldPosition;
            trail.transform.localScale = Vector3.one;

            _lastSpawnedTrails[colorName] = trail.transform;
            _activeTrails.Add(trail.transform);

            if (Cinematic.IsPlaying)
            {
                trail.transform.DOPause();
            }

            trail.Setup(target,
                color,
                _config.ScorePointTraceDuration,
                () =>
                {
                    _activeTrails.Remove(trail.transform);

                    // Only clear the entry if this trail is still the latest
                    // spawn — a newer trail may have overwritten it.
                    if (_lastSpawnedTrails.TryGetValue(colorName, out var last)
                        && last == trail.transform)
                    {
                        _lastSpawnedTrails.Remove(colorName);
                    }

                    _arrivedPublisher.Publish(new ScoreTrailArrivedMessage(colorName, target));
                    _poolManager.Return(poolKey, trail);
                });
        }
    }
}
