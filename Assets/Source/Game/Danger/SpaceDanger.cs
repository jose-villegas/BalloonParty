using System;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
using BalloonParty.Slots.Grid;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Game.Danger
{
    /// <summary>0→1 "how close are we to dying" signal: next turn's spawn overflow relative to hearts left to absorb it.</summary>
    internal class SpaceDanger : IStartable, IDisposable, IDangerLevel
    {
        private readonly SlotGrid _grid;
        private readonly IPlayerHealth _health;
        private readonly IActiveLevelParameters _levelParams;
        private readonly ReactiveProperty<float> _level = new(0f);
        private readonly CompositeDisposable _subscriptions = new();

        public IReadOnlyReactiveProperty<float> Level => _level;

        public SpaceDanger(SlotGrid grid, IPlayerHealth health, IActiveLevelParameters levelParams)
        {
            _grid = grid;
            _health = health;
            _levelParams = levelParams;
        }

        public void Start()
        {
            _grid.OnChanged.Subscribe(_ => Recompute()).AddTo(_subscriptions);
            _health.Current.Subscribe(_ => Recompute()).AddTo(_subscriptions);
            Recompute();
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        // Pure danger curve, exposed for unit testing.
        internal static float Evaluate(int hearts, int availableSpace, int spawnPerTurn)
        {
            if (hearts <= 0)
            {
                return 1f;
            }

            var overflow = Mathf.Max(0, spawnPerTurn - availableSpace);
            return Mathf.Clamp01((float)overflow / hearts);
        }

        private void Recompute()
        {
            // Must read the resolved per-level value — the catalog misreports on ramped levels.
            var spawnPerTurn = _levelParams.Current.SpawnLines * _grid.Columns;
            _level.Value = Evaluate(_health.Current.Value, CountEmptySlots(), spawnPerTurn);
        }

        private int CountEmptySlots()
        {
            var count = 0;
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (_grid.IsEmpty(col, row))
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
