using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Game.Run;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using VContainer.Unity;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Balloon.Controller
{
    internal class BalloonBalancer : IStartable, ITickable, IRunResettable
    {
        private readonly BalancePathHolder _balancePathHolder;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly SlotGrid _grid;
        private readonly GridBalanceQuery _balanceQuery;
        private readonly ISubscriber<BalanceBalloonsMessage> _subscriber;
        private readonly ISubscriber<ProjectileLoadedMessage> _projectileLoadedSubscriber;
        private readonly ISubscriber<ProjectileDestroyedMessage> _projectileDestroyedSubscriber;
        private readonly PauseService _pauseService;
        private readonly DisturbanceFieldService _disturbanceField;
        private readonly BalloonMotionTicker _motionTicker;
        private readonly Dictionary<IWriteableDynamicSlotActor, List<Vector3>> _paths = new();

        private bool _balanceRequested;
        private int _generation;
        private IProjectileModel _activeProjectile;
        private float _flightRebalanceElapsed;

        public int ResetOrder => RunResetOrder.Quiesce;

        // Exposed for tests.
        internal int Generation => _generation;

        [Inject]
        internal BalloonBalancer(
            SlotGrid grid,
            GridBalanceQuery balanceQuery,
            IBalloonsConfiguration balloonsConfig,
            BalancePathHolder balancePathHolder,
            ISubscriber<BalanceBalloonsMessage> subscriber,
            ISubscriber<ProjectileLoadedMessage> projectileLoadedSubscriber,
            ISubscriber<ProjectileDestroyedMessage> projectileDestroyedSubscriber,
            PauseService pauseService,
            DisturbanceFieldService disturbanceField,
            BalloonMotionTicker motionTicker)
        {
            _grid = grid;
            _balanceQuery = balanceQuery;
            _balloonsConfig = balloonsConfig;
            _balancePathHolder = balancePathHolder;
            _subscriber = subscriber;
            _projectileLoadedSubscriber = projectileLoadedSubscriber;
            _projectileDestroyedSubscriber = projectileDestroyedSubscriber;
            _pauseService = pauseService;
            _disturbanceField = disturbanceField;
            _motionTicker = motionTicker;
        }

        public void Start()
        {
            _subscriber.Subscribe(_ => RequestBalance());
            _projectileLoadedSubscriber.Subscribe(msg => _activeProjectile = msg.Model);
            _projectileDestroyedSubscriber.Subscribe(_ => _activeProjectile = null);
        }

        // Pulses a rebalance at intervals while a projectile is airborne.
        public void Tick()
        {
            TickFlightRebalance(Time.deltaTime);
        }

        public void ResetRun(int generation)
        {
            // Bumping the generation drops any balance already scheduled this frame.
            _generation = generation;
            _balanceRequested = false;
            _activeProjectile = null;
            _flightRebalanceElapsed = 0f;
            ReleasePaths();
        }

        private void AnimatePaths(Dictionary<IWriteableDynamicSlotActor, List<Vector3>> paths)
        {
            foreach (var (actor, path) in paths)
            {
                var slot = actor.SlotIndex.Value;
                var view = _grid.ViewAt(slot);
                if (view == null)
                {
                    throw new InvalidOperationException(
                        $"BalloonBalancer.AnimatePaths: no view found at slot ({slot.x},{slot.y}) " +
                        "— model/view desync.");
                }

                view.TweenTracker.Kill();
                view.transform.DOKill();

                // A ticker-driven nudge escapes DOKill — cancel it explicitly.
                if (view is View.IBalloonMotionView motionView)
                {
                    _motionTicker.CancelNudge(motionView);
                }

                var currentScale = view.transform.localScale;
                var viewTransform = view.transform;

                var tween = viewTransform
                    .DOPath(path.ToArray(), _balloonsConfig.TimeForBalloonsBalance, PathType.CatmullRom)
                    .StampDisturbanceAlongPath(viewTransform, _disturbanceField, StampSource.BalloonPath)
                    .OnComplete(() =>
                    {
                        actor.IsStable.Value = true;
                        _balancePathHolder.Release(actor);
                    });

                view.TweenTracker.Append(tween);

                if (currentScale != Vector3.one)
                {
                    view.transform.DOScale(Vector3.one, _balloonsConfig.TimeForBalloonsBalance);
                }
            }
        }

        internal void Balance()
        {
            ReleasePaths();

            while (BalanceOnePass())
            {
            }

            AnimatePaths(_paths);
            ReleasePaths();
        }

        // One sweep over the grid; returns whether any actor was shifted.
        private bool BalanceOnePass()
        {
            var moved = false;
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 1; row < _grid.Rows; row++)
                {
                    moved |= TryBalanceSlot(col, row);
                }
            }

            return moved;
        }

        // Shifts the actor at (col, row) toward its optimal empty slot. Returns whether it moved.
        private bool TryBalanceSlot(int col, int row)
        {
            if (_grid.IsEmpty(col, row))
            {
                return false;
            }

            if (!_balanceQuery.IsUnbalanced(col, row))
            {
                return false;
            }

            var currentSlot = new Vector2Int(col, row);
            if (_grid.At(currentSlot) is not IWriteableDynamicSlotActor dynamicActor)
            {
                return false;
            }

            var nextSlot = _balanceQuery.OptimalNextEmptySlot(col, row);
            if (!nextSlot.HasValue)
            {
                return false;
            }

            _balancePathHolder.Reserve(dynamicActor, currentSlot);
            _balancePathHolder.Reserve(dynamicActor, nextSlot.Value);

            var actorView = _grid.ViewAt(currentSlot);
            _grid.Remove(currentSlot);
            _grid.Place(dynamicActor, actorView, nextSlot.Value);
            dynamicActor.IsStable.Value = false;

            RecordPath(dynamicActor, _grid.IndexToWorldPosition(nextSlot.Value));
            return true;
        }

        private void RecordPath(IWriteableDynamicSlotActor actor, Vector3 targetPosition)
        {
            if (_paths.TryGetValue(actor, out var path))
            {
                path.Add(targetPosition);
            }
            else
            {
                var newPath = ListPool<Vector3>.Get();
                newPath.Add(targetPosition);
                _paths[actor] = newPath;
            }
        }

        // Shoves a column's bottom occupant toward the nearest gap so a new balloon can spawn. Returns whether room was opened.
        internal bool TryRelievePressure(int column)
        {
            var chain = ListPool<Vector2Int>.Get();
            try
            {
                if (!PressureCascade.TryFindChain(_grid, column, chain))
                {
                    return false;
                }

                ReleasePaths();

                // Shift from the empty end backwards so every destination is vacant.
                for (var i = chain.Count - 2; i >= 0; i--)
                {
                    var from = chain[i];
                    var to = chain[i + 1];

                    if (_grid.At(from) is not IWriteableDynamicSlotActor actor)
                    {
                        continue;
                    }

                    var view = _grid.ViewAt(from);

                    _balancePathHolder.Reserve(actor, from);
                    _balancePathHolder.Reserve(actor, to);

                    _grid.Remove(from);
                    _grid.Place(actor, view, to);
                    actor.IsStable.Value = false;

                    var path = ListPool<Vector3>.Get();
                    path.Add(_grid.IndexToWorldPosition(to));
                    _paths[actor] = path;
                }

                AnimatePaths(_paths);
                ReleasePaths();
                return true;
            }
            finally
            {
                ListPool<Vector2Int>.Release(chain);
            }
        }

        private void ReleasePaths()
        {
            foreach (var path in _paths.Values)
            {
                ListPool<Vector3>.Release(path);
            }

            _paths.Clear();
        }

        private async UniTaskVoid BalanceNextFrameAsync(int generation)
        {
            await UniTask.Yield();
            RunScheduledBalance(generation);
        }

        // Runs the deferred balance unless a reset has since bumped the generation.
        internal bool RunScheduledBalance(int generation)
        {
            if (generation != _generation)
            {
                return false;
            }

            _balanceRequested = false;
            Balance();
            return true;
        }

        // Internal for tests — Time.deltaTime isn't injectable, so tests drive it with an explicit step.
        internal bool TickFlightRebalance(float deltaTime)
        {
            var interval = _balloonsConfig.FlightRebalanceInterval;
            if (interval <= 0f || _activeProjectile == null || !_activeProjectile.IsFree
                || _pauseService.IsAnyPaused.Value)
            {
                _flightRebalanceElapsed = 0f;
                return false;
            }

            _flightRebalanceElapsed += deltaTime;
            if (_flightRebalanceElapsed < interval)
            {
                return false;
            }

            _flightRebalanceElapsed = 0f;
            if (!HasPossibleMove())
            {
                return false;
            }

            RequestBalance();
            return true;
        }

        // True when some occupied dynamic actor could shift; read-only.
        internal bool HasPossibleMove()
        {
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 1; row < _grid.Rows; row++)
                {
                    if (_grid.IsEmpty(col, row) || !_balanceQuery.IsUnbalanced(col, row))
                    {
                        continue;
                    }

                    if (_grid.At(new Vector2Int(col, row)) is IWriteableDynamicSlotActor
                        && _balanceQuery.OptimalNextEmptySlot(col, row).HasValue)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void RequestBalance()
        {
            if (_balanceRequested)
            {
                return;
            }

            _balanceRequested = true;
            BalanceNextFrameAsync(_generation).Forget();
        }
    }
}
