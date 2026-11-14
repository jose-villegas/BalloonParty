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
    ///     Draws a carbon pencil line along the straight run a Snipe lance is spent on. The run starts where
    ///     the lance armed (or at the last wall it carried the lance past). The pencil starts the moment the
    ///     run is sure to discharge, when the lance plows its first tough, and its tip runs behind the shot
    ///     at a steady speed and on to the wall, so the line is seen drawing across the board; then it holds
    ///     and is erased chunk by chunk. Cruise-earned pierces draw nothing: only a Snipe opens a run.
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
            _projectiles.Retired.Subscribe(OnRetired).AddTo(_subscriptions);
        }

        public void Tick()
        {
            if (HoldsStrokes(_pause))
            {
                return;
            }

            StartStrokesOnPlow();
            if (_strokes.Count == 0)
            {
                return;
            }

            // Unscaled: the discharge's slow-mo dip lands while a stroke is drawing, and a pencil that
            // crawls through it reads as lag, not drama. The tip never passes the shot, so it can't outrun it.
            var deltaTime = Time.unscaledDeltaTime;
            for (var i = _strokes.Count - 1; i >= 0; i--)
            {
                var stroke = _strokes[i];
                if (!stroke.Closed && TryGetInFlightPosition(stroke.Shot, out var shotPosition))
                {
                    stroke.End = shotPosition;
                }

                if (stroke.Progress.Advance(deltaTime, stroke.Length, stroke.Closed, stroke.Line.Timing, out var erased))
                {
                    stroke.Line.Render(stroke.End, stroke.Progress.Drawn, erased);
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

        // Only the cheat console holds a stroke, so a tester can study it. The level-up, the ascent and the
        // loss cinematic all hold a pause of their own for seconds on end, and a line frozen at full strength
        // through them hangs over a board that has already moved on.
        internal static bool HoldsStrokes(PauseService pause)
        {
            return pause.IsPaused(PauseSource.Cheat);
        }

        // A plowed tough is what makes the run's wall a discharge, so the pencil starts on it rather than
        // waiting for the wall.
        private void StartStrokesOnPlow()
        {
            if (_trace.OpenCount == 0 || _prefab == null)
            {
                return;
            }

            for (var i = 0; i < _projectiles.InFlightCount; i++)
            {
                var shot = _projectiles.InFlightAt(i);
                if (shot.Flight.PendingPierceHitCount == 0
                    || IndexOfDrawingStroke(shot) >= 0
                    || !_trace.TryGetRunStart(shot, out var from))
                {
                    continue;
                }

                BeginStroke(shot, from, _projectiles.InFlightPositionAt(i), closed: false);
            }
        }

        private void OnWallReached(PierceWallReachedMessage msg)
        {
            var shot = msg.Projectile;
            var draws = _trace.TryCloseRun(shot, msg.Position, shot.IsPiercing.Value, msg.Discharged, out var from);

            var drawing = IndexOfDrawingStroke(shot);
            if (drawing >= 0)
            {
                // Not drawing on means every plowed tough had already left the board, so nothing shattered
                // here: the pencil stops where it is and the line is erased.
                var stroke = _strokes[drawing];
                stroke.End = draws ? msg.Position : stroke.Tip;
                stroke.Closed = true;
                _strokes[drawing] = stroke;
                return;
            }

            // Toughs found only by the wall's own sweep: the run is known to discharge only now.
            if (draws && _prefab != null)
            {
                BeginStroke(shot, from, msg.Position, closed: true);
            }
        }

        // A shot that dies at a wall has already closed its run there; this catches one taken off the board
        // any other way, finishing the line where the shot was last seen.
        private void OnRetired(IProjectileModel shot)
        {
            var drawing = IndexOfDrawingStroke(shot);
            if (drawing >= 0)
            {
                var stroke = _strokes[drawing];
                stroke.Closed = true;
                _strokes[drawing] = stroke;
            }

            _trace.Drop(shot);
        }

        private void BeginStroke(IProjectileModel shot, Vector3 from, Vector3 end, bool closed)
        {
            var line = _poolManager.GetOrRegister(_poolKey, CreateChannel);
            line.Begin(from, _traceConfig.LineColor);
            _strokes.Add(new Stroke(line, shot, from, end, closed));
        }

        private int IndexOfDrawingStroke(IProjectileModel shot)
        {
            for (var i = 0; i < _strokes.Count; i++)
            {
                if (!_strokes[i].Closed && _strokes[i].Shot == shot)
                {
                    return i;
                }
            }

            return -1;
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
            internal readonly IProjectileModel Shot;
            internal readonly Vector3 From;
            internal Vector3 End;
            internal bool Closed;
            internal SnipeLanceStroke Progress;

            internal float Length => Vector3.Distance(From, End);

            internal Vector3 Tip
            {
                get
                {
                    var length = Length;
                    return length > 0f ? Vector3.Lerp(From, End, Progress.Drawn / length) : From;
                }
            }

            internal Stroke(SnipeLanceLineView line, IProjectileModel shot, Vector3 from, Vector3 end, bool closed)
            {
                Line = line;
                Shot = shot;
                From = from;
                End = end;
                Closed = closed;
                Progress = default;
            }
        }
    }
}
