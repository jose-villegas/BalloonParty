using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Balloon.Controller
{
    internal class BalloonBalancer : IStartable
    {
        private readonly BalancePathHolder _balancePathHolder;
        private readonly BalloonsConfiguration _balloonsConfig;
        private readonly SlotGrid _grid;
        private readonly ISubscriber<BalanceBalloonsMessage> _subscriber;
        private readonly DisturbanceFieldService _disturbanceField;
        private readonly DisturbanceFieldSettings _disturbanceSettings;

        private bool _balanceRequested;

        [Inject]
        internal BalloonBalancer(
            SlotGrid grid,
            BalloonsConfiguration balloonsConfig,
            BalancePathHolder balancePathHolder,
            ISubscriber<BalanceBalloonsMessage> subscriber,
            DisturbanceFieldService disturbanceField,
            DisturbanceFieldSettings disturbanceSettings)
        {
            _grid = grid;
            _balloonsConfig = balloonsConfig;
            _balancePathHolder = balancePathHolder;
            _subscriber = subscriber;
            _disturbanceField = disturbanceField;
            _disturbanceSettings = disturbanceSettings;
        }

        public void Start()
        {
            _subscriber.Subscribe(_ => RequestBalance());
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
                var lastPos = viewTransform.position;
                var balanceStamp = _disturbanceSettings.GetProfile(StampSource.BalloonPath);

                var tween = viewTransform
                    .DOPath(path.ToArray(), _balloonsConfig.TimeForBalloonsBalance, PathType.CatmullRom)
                    .OnUpdate(() =>
                    {
                        var pos = viewTransform.position;
                        var delta = pos - lastPos;
                        var dir = new Vector2(delta.x, delta.y).normalized;
                        var rawScale = viewTransform.localScale.x;
                        var scale = rawScale * rawScale;
                        _disturbanceField.StampOverDuration(pos, balanceStamp.Radius * scale,
                            balanceStamp.Strength * scale, dir, balanceStamp.Duration);
                        lastPos = pos;
                    })
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
            var paths = new Dictionary<IWriteableDynamicSlotActor, List<Vector3>>();
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
                        if (paths.TryGetValue(dynamicActor, out var path))
                        {
                            path.Add(targetPosition);
                        }
                        else
                        {
                            paths[dynamicActor] = new List<Vector3> { targetPosition };
                        }
                    }
                }
            }

            AnimatePaths(paths);
        }

        private async UniTaskVoid BalanceNextFrameAsync()
        {
            await UniTask.Yield();
            _balanceRequested = false;
            Balance();
        }

        private void RequestBalance()
        {
            if (_balanceRequested)
            {
                return;
            }

            _balanceRequested = true;
            BalanceNextFrameAsync().Forget();
        }
    }
}
