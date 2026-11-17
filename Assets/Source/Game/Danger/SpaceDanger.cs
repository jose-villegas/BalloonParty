using System;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Game.Health;
using BalloonParty.Slots.Grid;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Game.Danger
{
    /// <summary>0→1 "how close are we to dying" signal: the next line's overflow relative to hearts left to absorb it.</summary>
    internal class SpaceDanger : IStartable, ILateTickable, IDisposable, IDangerLevel
    {
        private readonly SlotGrid _grid;
        private readonly IObjectResolver _resolver;
        private readonly IPlayerHealth _health;
        private readonly ReactiveProperty<float> _level = new(0f);
        private readonly ReactiveProperty<int> _heartsAtRisk = new(0);
        private readonly CompositeDisposable _subscriptions = new();
        private bool _dirty;
        private BalloonPlacementResolver _placement;

        public IReadOnlyReactiveProperty<float> Level => _level;
        public IReadOnlyReactiveProperty<int> HeartsAtRisk => _heartsAtRisk;

        public SpaceDanger(SlotGrid grid, IObjectResolver resolver, IPlayerHealth health)
        {
            _grid = grid;
            _resolver = resolver;
            _health = health;
        }

        public void Start()
        {
            // Resolved here, not taken in the constructor: the placement needs the balancer, which needs the
            // formations, whose threat ease reads the player's need, which reads this danger.
            _placement = _resolver.Resolve<BalloonPlacementResolver>();
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
        internal static float Evaluate(int hearts, int availableSpace, int spawnPerLine, int columns)
        {
            if (hearts <= 0)
            {
                return 1f;
            }

            if (columns <= 0)
            {
                return 0f;
            }

            var overflow = Mathf.Max(0, spawnPerLine - availableSpace);
            var heartsAtRisk = overflow / columns;
            return Mathf.Clamp01((float)heartsAtRisk / hearts);
        }

        private void Recompute()
        {
            _dirty = false;

            // The spawn clock brings one line per tick.
            var columns = _grid.Columns;
            // As the spawner counts it: empty slots sealed off from every line are no room.
            var availableSpace = _placement.LineHasRoom() ? _grid.CountEmpty() : 0;

            _level.Value = Evaluate(_health.Current.Value, availableSpace, columns, columns);
            _heartsAtRisk.Value = WaveDeficitCalculator.Calculate(availableSpace, columns, columns).HeartsLost;
        }
    }
}
