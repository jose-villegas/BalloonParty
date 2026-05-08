using System.Collections.Generic;
using DG.Tweening;
using MessagePipe;
using VContainer.Unity;
using UnityEngine;
using VContainer;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;

namespace BalloonParty.Balloon.Controller
{
    public class BalloonBalancer : IStartable
    {
        private readonly SlotGrid _grid;
        private readonly IGameConfiguration _config;
        private readonly ISubscriber<BalanceBalloonsMessage> _subscriber;

        [Inject]
        public BalloonBalancer(SlotGrid grid, IGameConfiguration config, ISubscriber<BalanceBalloonsMessage> subscriber)
        {
            _grid = grid;
            _config = config;
            _subscriber = subscriber;
        }

        public void Start()
        {
            _subscriber.Subscribe(_ => Balance());
        }

        private void Balance()
        {
            var paths = new Dictionary<BalloonModel, List<Vector3>>();
            var hasUnbalanced = true;

            while (hasUnbalanced)
            {
                hasUnbalanced = false;

                for (int col = 0; col < _grid.Columns; col++)
                {
                    for (int row = _grid.Rows - 1; row >= 0; row--)
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
            }

            AnimatePaths(paths);
        }

        private void AnimatePaths(Dictionary<BalloonModel, List<Vector3>> paths)
        {
            foreach (var (balloon, path) in paths)
            {
                var view = balloon.View;
                if (view == null) continue;

                // Kill any in-progress tween so the new path takes over cleanly.
                view.transform.DOKill();

                view.transform
                    .DOPath(path.ToArray(), _config.TimeForBalloonsBalance, PathType.CatmullRom)
                    .OnComplete(() => balloon.IsStable.Value = true);
            }
        }
    }
}

