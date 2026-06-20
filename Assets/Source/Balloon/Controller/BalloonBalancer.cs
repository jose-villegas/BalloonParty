using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Game.Run;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Balloon.Controller
{
    internal class BalloonBalancer : IStartable, IRunResettable
    {
        private readonly BalancePathHolder _balancePathHolder;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly SlotGrid _grid;
        private readonly ISubscriber<BalanceBalloonsMessage> _subscriber;
        private readonly DisturbanceFieldService _disturbanceField;
        private readonly Dictionary<IWriteableDynamicSlotActor, List<Vector3>> _paths = new();

        private bool _balanceRequested;
        private int _generation;

        public int ResetOrder => RunResetOrder.Quiesce;

        // Exposed for tests: a balance scheduled in a prior generation is stale after a reset.
        internal int Generation => _generation;

        [Inject]
        internal BalloonBalancer(
            SlotGrid grid,
            IBalloonsConfiguration balloonsConfig,
            BalancePathHolder balancePathHolder,
            ISubscriber<BalanceBalloonsMessage> subscriber,
            DisturbanceFieldService disturbanceField)
        {
            _grid = grid;
            _balloonsConfig = balloonsConfig;
            _balancePathHolder = balancePathHolder;
            _subscriber = subscriber;
            _disturbanceField = disturbanceField;
        }

        public void Start()
        {
            _subscriber.Subscribe(_ => RequestBalance());
        }

        public void ResetRun(int generation)
        {
            // Adopt the new run's generation so any balance already scheduled this frame is dropped
            // when its continuation runs — it would otherwise animate actors the board-clear has
            // just returned to the pool, against an emptied grid.
            _generation = generation;
            _balanceRequested = false;
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
            // Reuse the path dictionary and pool the per-actor lists across turns; only
            // the DOPath waypoint arrays (built in AnimatePaths) must be freshly sized.
            ReleasePaths();
            var hasUnbalanced = true;

            while (hasUnbalanced)
            {
                hasUnbalanced = false;

                for (var col = 0; col < _grid.Columns; col++)
                {
                    for (var row = 1; row < _grid.Rows; row++)
                    {
                        if (_grid.IsEmpty(col, row))
                        {
                            continue;
                        }

                        if (!_grid.IsUnbalanced(col, row))
                        {
                            continue;
                        }

                        var currentSlot = new Vector2Int(col, row);
                        var actor = _grid.At(currentSlot);

                        if (actor is not IWriteableDynamicSlotActor dynamicActor)
                        {
                            continue;
                        }

                        var nextSlot = _grid.OptimalNextEmptySlot(col, row);
                        if (!nextSlot.HasValue)
                        {
                            continue;
                        }

                        hasUnbalanced = true;

                        _balancePathHolder.Reserve(dynamicActor, currentSlot);
                        _balancePathHolder.Reserve(dynamicActor, nextSlot.Value);

                        var actorView = _grid.ViewAt(currentSlot);
                        _grid.Remove(currentSlot);
                        _grid.Place(dynamicActor, actorView, nextSlot.Value);
                        dynamicActor.IsStable.Value = false;

                        var targetPosition = _grid.IndexToWorldPosition(nextSlot.Value);
                        if (_paths.TryGetValue(dynamicActor, out var path))
                        {
                            path.Add(targetPosition);
                        }
                        else
                        {
                            var newPath = ListPool<Vector3>.Get();
                            newPath.Add(targetPosition);
                            _paths[dynamicActor] = newPath;
                        }
                    }
                }
            }

            AnimatePaths(_paths);
            ReleasePaths();
        }

        // Pressure balance: when a column's entry can't accept a balloon, try to shove its bottom
        // occupant toward the nearest reachable gap so the new balloon can spawn instead of costing
        // HP. Returns whether room was opened; the caller re-checks the column afterwards.
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

                // Shift from the empty end backwards so every destination is vacant when placed.
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

        // Runs the deferred balance unless a reset has since bumped the generation. Returns
        // whether it actually balanced — the guard is the regression point for the reset race.
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
