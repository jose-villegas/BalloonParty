using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Balloon.Controller
{
    public class BalloonBalancer : IStartable
    {
        private readonly IGameConfiguration _config;
        private readonly SlotGrid _grid;
        private readonly ISubscriber<BalanceBalloonsMessage> _subscriber;

        private bool _balanceRequested;

        [Inject]
        public BalloonBalancer(SlotGrid grid, IGameConfiguration config, ISubscriber<BalanceBalloonsMessage> subscriber)
        {
            _grid = grid;
            _config = config;
            _subscriber = subscriber;
        }

        public void Start()
        {
            _subscriber.Subscribe(_ => RequestBalance());
        }

        private void RequestBalance()
        {
            if (_balanceRequested) return;
            _balanceRequested = true;
            BalanceNextFrameAsync().Forget();
        }

        private async UniTaskVoid BalanceNextFrameAsync()
        {
            await UniTask.Yield();
            _balanceRequested = false;
            Balance();
        }

        private void Balance()
        {
            var paths = new Dictionary<BalloonModel, List<Vector3>>();
            var hasUnbalanced = true;

            while (hasUnbalanced)
            {
                hasUnbalanced = false;

                for (var col = 0; col < _grid.Columns; col++)
                for (var row = _grid.Rows - 1; row >= 0; row--)
                {
                    if (_grid.IsEmpty(col, row)) continue;
                    if (!_grid.IsUnbalanced(col, row)) continue;

                    var nextSlot = _grid.OptimalNextEmptySlot(col, row);
                    if (!nextSlot.HasValue) continue;

                    hasUnbalanced = true;

                    var balloon = _grid.At(new Vector2Int(col, row));
                    _grid.Remove(new Vector2Int(col, row));
                    _grid.Place(balloon, nextSlot.Value);
                    balloon.IsStable.Value = false;

                    var targetPosition = _grid.IndexToWorldPosition(nextSlot.Value);
                    if (paths.TryGetValue(balloon, out var path))
                        path.Add(targetPosition);
                    else
                        paths[balloon] = new List<Vector3> { targetPosition };
                }
            }

            AnimatePaths(paths);
        }

        private void AnimatePaths(Dictionary<BalloonModel, List<Vector3>> paths)
        {
            foreach (var (balloon, path) in paths)
            {
                var view = balloon.View;
                if (view == null) continue;

                view.TweenTracker.Kill();
                view.transform.DOKill();

                var currentScale = view.transform.localScale;
                var tween = view.transform
                    .DOPath(path.ToArray(), _config.TimeForBalloonsBalance, PathType.CatmullRom)
                    .OnComplete(() => balloon.IsStable.Value = true);

                view.TweenTracker.Append(tween);

                if (currentScale != Vector3.one)
                    view.transform.DOScale(Vector3.one, _config.TimeForBalloonsBalance);
            }
        }
    }
}