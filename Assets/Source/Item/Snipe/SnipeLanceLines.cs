using System;
using System.Collections.Generic;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Pool;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Item.Snipe
{
    /// <summary>
    ///     Draws a carbon pencil line along each straight run a Snipe lance plows, the moment the shot reaches
    ///     the wall that ends it. The run starts where the lance armed (or at the wall it carried the lance
    ///     past) and the line traces it start to wall, holds, then fades. Cruise-earned pierces draw nothing:
    ///     only a Snipe opens a run.
    /// </summary>
    internal sealed class SnipeLanceLines : ISnipeLanceArming, IStartable, ITickable, IDisposable
    {
        private readonly IProjectileRegistry _projectiles;
        private readonly ISubscriber<PierceWallReachedMessage> _wallReachedSubscriber;
        private readonly ISubscriber<ProjectileFiredMessage> _firedSubscriber;
        private readonly IPredictionTraceConfig _traceConfig;
        private readonly PauseService _pause;
        private readonly PoolManager _poolManager;
        private readonly SnipeLanceLineView _prefab;
        private readonly string _poolKey;
        private readonly SnipeLanceTrace _trace = new();
        private readonly List<Stroke> _strokes = new();
        private readonly CompositeDisposable _subscriptions = new();

        internal SnipeLanceLines(
            IProjectileRegistry projectiles,
            ISubscriber<PierceWallReachedMessage> wallReachedSubscriber,
            ISubscriber<ProjectileFiredMessage> firedSubscriber,
            IPredictionTraceConfig traceConfig,
            PauseService pause,
            PoolManager poolManager,
            SnipeLanceLinePrefab prefab)
        {
            _projectiles = projectiles;
            _wallReachedSubscriber = wallReachedSubscriber;
            _firedSubscriber = firedSubscriber;
            _traceConfig = traceConfig;
            _pause = pause;
            _poolManager = poolManager;
            _prefab = prefab.Prefab;
            _poolKey = _prefab != null ? _prefab.name : nameof(SnipeLanceLineView);
        }

        public void OnLanceArmed(IProjectileModel shot)
        {
            if (TryGetInFlightPosition(shot, out var position))
            {
                _trace.Open(shot, position);
                return;
            }

            // Still in the thrower: its first run starts at the muzzle, the moment it fires.
            _trace.OpenAtLaunch(shot);
        }

        public void OnLanceRearmed(IProjectileModel shot, Vector3 wall)
        {
            _trace.Open(shot, wall);
        }

        public void Start()
        {
            _wallReachedSubscriber.Subscribe(OnWallReached).AddTo(_subscriptions);
            _firedSubscriber.Subscribe(msg => _trace.Launched(msg.Projectile, msg.WorldPosition)).AddTo(_subscriptions);
            _projectiles.Retired.Subscribe(_trace.Drop).AddTo(_subscriptions);
        }

        public void Tick()
        {
            if (_strokes.Count == 0 || _pause.IsAnyPaused.Value)
            {
                return;
            }

            // Unscaled: the discharge's slow-mo dip lands exactly as a stroke starts, and a pencil that
            // crawls through it reads as lag, not drama.
            var deltaTime = Time.unscaledDeltaTime;
            for (var i = _strokes.Count - 1; i >= 0; i--)
            {
                var stroke = _strokes[i];
                stroke.Elapsed += deltaTime;
                if (SnipeLanceStroke.Evaluate(stroke.Elapsed, stroke.Line.Timing, out var drawn, out var alpha))
                {
                    stroke.Line.Render(drawn, alpha);
                    _strokes[i] = stroke;
                    continue;
                }

                _poolManager.Return(_poolKey, stroke.Line);
                _strokes.RemoveAt(i);
            }
        }

        // Lines still drawing are left to the scope teardown that destroys the pool, not handed back to it.
        public void Dispose()
        {
            _subscriptions.Dispose();
            _strokes.Clear();
            _trace.Clear();
        }

        private void OnWallReached(PierceWallReachedMessage msg)
        {
            var shot = msg.Projectile;
            if (!_trace.TryCloseRun(shot, msg.Position, shot.IsPiercing.Value, out var from) || _prefab == null)
            {
                return;
            }

            var line = _poolManager.GetOrRegister(_poolKey, CreateChannel);
            line.Begin(from, msg.Position, _traceConfig.LineColor);
            _strokes.Add(new Stroke(line));
        }

        private PoolChannel<SnipeLanceLineView> CreateChannel()
        {
            return new SimplePoolChannel<SnipeLanceLineView>(_prefab);
        }

        private bool TryGetInFlightPosition(IProjectileModel shot, out Vector3 position)
        {
            for (var i = 0; i < _projectiles.InFlightCount; i++)
            {
                if (_projectiles.InFlightAt(i) == shot)
                {
                    position = _projectiles.InFlightPositionAt(i);
                    return true;
                }
            }

            position = default;
            return false;
        }

        private struct Stroke
        {
            internal readonly SnipeLanceLineView Line;
            internal float Elapsed;

            internal Stroke(SnipeLanceLineView line)
            {
                Line = line;
                Elapsed = 0f;
            }
        }
    }
}
