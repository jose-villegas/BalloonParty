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
    internal class SpaceDanger : IStartable, ILateTickable, IDisposable, IDangerLevel
    {
        private readonly SlotGrid _grid;
        private readonly IPlayerHealth _health;
        private readonly IActiveLevelParameters _levelParams;
        private readonly ReactiveProperty<float> _level = new(0f);
        private readonly ReactiveProperty<int> _heartsAtRisk = new(0);
        private readonly CompositeDisposable _subscriptions = new();
        private bool _dirty;

        public IReadOnlyReactiveProperty<float> Level => _level;
        public IReadOnlyReactiveProperty<int> HeartsAtRisk => _heartsAtRisk;

        public SpaceDanger(SlotGrid grid, IPlayerHealth health, IActiveLevelParameters levelParams)
        {
            _grid = grid;
            _health = health;
            _levelParams = levelParams;
        }

        public void Start()
        {
            // A bomb pop or balance sweep can fire OnChanged a dozen+ times in one frame; only the
            // settled end-of-frame state matters, so mark dirty here and recompute once in LateTick.
            _grid.OnChanged.Subscribe(_ => _dirty = true).AddTo(_subscriptions);
            _health.Current.Subscribe(_ => _dirty = true).AddTo(_subscriptions);
            Recompute();
        }

        public void LateTick()
        {
            if (_dirty)
            {
                Recompute();
            }
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        // Pure danger curve, exposed for unit testing. Uses line-based granularity:
        // only full lines of overflow count as hearts at risk.
        internal static float Evaluate(int hearts, int availableSpace, int spawnPerTurn, int columns)
        {
            if (hearts <= 0)
            {
                return 1f;
            }

            if (columns <= 0)
            {
                return 0f;
            }

            var overflow = Mathf.Max(0, spawnPerTurn - availableSpace);
            var heartsAtRisk = overflow / columns;
            return Mathf.Clamp01((float)heartsAtRisk / hearts);
        }

        private void Recompute()
        {
            _dirty = false;

            // Must read the resolved per-level value — the catalog misreports on ramped levels.
            var spawnPerTurn = _levelParams.Current.SpawnLines * _grid.Columns;
            var availableSpace = CountEmptySlots();
            var columns = _grid.Columns;

            _level.Value = Evaluate(_health.Current.Value, availableSpace, spawnPerTurn, columns);

            var overflow = Mathf.Max(0, spawnPerTurn - availableSpace);
            _heartsAtRisk.Value = columns > 0 ? overflow / columns : 0;
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
