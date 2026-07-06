using System;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
using BalloonParty.Slots.Grid;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Game.Danger
{
    /// <summary>
    ///     A 0→1 "how close are we to dying" signal, recomputed whenever the board or hit points
    ///     change. Danger is how much the next turn's spawn would overflow the board relative to the
    ///     hearts left to absorb it: with <c>overflow = spawnPerTurn − availableSpace</c>, the level is
    ///     <c>overflow / hearts</c>, clamped to 1 once a single turn could empty the heart pool (and
    ///     1 outright at zero hearts). Consumers map it through a gradient — see
    ///     <c>UI/Danger/DangerGradientView</c>.
    ///
    ///     <c>availableSpace</c> is the empty-slot count, a heuristic for how many balloons the board
    ///     can still take (re-home + pressure fill nearly every empty), and <c>spawnPerTurn</c> is the
    ///     worst case <c>IActiveLevelParameters.SpawnLines × Columns</c> (the resolved, per-level
    ///     value — misreports on ramped levels if read from the catalog instead). Both are tuning knobs.
    /// </summary>
    internal class SpaceDanger : IStartable, IDisposable, IDangerLevel
    {
        private readonly SlotGrid _grid;
        private readonly IPlayerHealth _health;
        private readonly IActiveLevelParameters _levelParams;
        private readonly ReactiveProperty<float> _level = new(0f);
        private readonly CompositeDisposable _subscriptions = new();

        public SpaceDanger(SlotGrid grid, IPlayerHealth health, IActiveLevelParameters levelParams)
        {
            _grid = grid;
            _health = health;
            _levelParams = levelParams;
        }

        public IReadOnlyReactiveProperty<float> Level => _level;

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

        // Pure danger curve, exposed for unit testing. 0 = safe (the board can still absorb the turn),
        // 1 = a single turn could drain every heart, or no hearts remain.
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
