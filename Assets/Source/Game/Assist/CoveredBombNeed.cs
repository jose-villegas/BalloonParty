using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Items;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Game.Assist
{
    /// <summary>
    ///     Need from a bomb the player can't get at: a balloon carrying a bomb counts as covered while at least
    ///     <see cref="IAssistSettings.CoveredBombMinCover" /> of the slots next to it hold unbreakables in formation
    ///     (the item-cover formation closes round it from below). Its need grows from 0 as the cover closes to 1 once
    ///     it has stayed covered <see cref="IAssistSettings.CoveredBombSecondsForFull" />, times
    ///     <see cref="IAssistSettings.CoveredBombNeedWeight" />; the longest-covered bomb on the board is the one
    ///     read. A cover that opens, or a bomb that goes, starts from nothing again.
    /// </summary>
    internal sealed class CoveredBombNeed : ICoveredBombNeed, IStartable, IRunResettable, IDisposable
    {
        private const int NeighbourCount = 6;

        private readonly SlotGrid _grid;
        private readonly IAssistSettings _settings;
        // When each covered bomb's cover closed. A carrier moved between slots keeps its time while it stays covered.
        private readonly Dictionary<IWriteableSlotActor, float> _coveredSince = new();
        private readonly HashSet<IWriteableSlotActor> _covered = new();
        private readonly List<IWriteableSlotActor> _gone = new();
        private readonly Vector2Int[] _neighbours = new Vector2Int[NeighbourCount];

        private IDisposable _subscription;

        // Tests step the clock; game time, as the Snipe's cooldown, so a pause ages nothing.
        internal Func<float> Now { get; set; } = () => Time.time;
        public int ResetOrder => RunResetOrder.Counters;

        public float Need
        {
            get
            {
                var weight = Mathf.Clamp01(_settings.CoveredBombNeedWeight);
                if (weight <= 0f)
                {
                    return 0f;
                }

                var now = Now();
                Refresh(now);
                var longest = 0f;
                foreach (var since in _coveredSince.Values)
                {
                    longest = Mathf.Max(longest, now - since);
                }

                return Mathf.Clamp01(longest / Mathf.Max(0.01f, _settings.CoveredBombSecondsForFull)) * weight;
            }
        }

        internal CoveredBombNeed(SlotGrid grid, IAssistSettings settings)
        {
            _grid = grid;
            _settings = settings;
        }

        // Read on every change of the board as well as when a roll asks, so a cover's time starts when it closes and
        // not at the next ball loaded.
        public void Start()
        {
            _subscription = _grid.OnChanged.Subscribe(_ => Refresh(Now()));
        }

        public void ResetRun(int generation)
        {
            _coveredSince.Clear();
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        private void Refresh(float now)
        {
            _covered.Clear();
            var minCover = Mathf.Max(1, _settings.CoveredBombMinCover);
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var actor = _grid.At(new Vector2Int(col, row));
                    if (actor is not IHasItemSlot { Item: { Value: ItemType.Bomb } } || Cover(col, row) < minCover)
                    {
                        continue;
                    }

                    _covered.Add(actor);
                    if (!_coveredSince.ContainsKey(actor))
                    {
                        _coveredSince[actor] = now;
                    }
                }
            }

            _gone.Clear();
            foreach (var actor in _coveredSince.Keys)
            {
                if (!_covered.Contains(actor))
                {
                    _gone.Add(actor);
                }
            }

            foreach (var actor in _gone)
            {
                _coveredSince.Remove(actor);
            }
        }

        // How many of the slots next to this one hold an unbreakable in formation.
        private int Cover(int col, int row)
        {
            HexCoordinates.HexNeighborIndices(col, row, _neighbours);
            var cover = 0;
            foreach (var neighbour in _neighbours)
            {
                if (!_grid.IsEmpty(neighbour.x, neighbour.y)
                    && _grid.At(neighbour) is UnbreakableBalloonModel { InFormation: { Value: true } })
                {
                    cover++;
                }
            }

            return cover;
        }
    }
}
