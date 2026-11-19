using System.Collections.Generic;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor.Archetype;
using UnityEngine;

namespace BalloonParty.Slots.Grid
{
    /// <summary>
    ///     The meadow's mushrooms as the ball meets them (@ref plan_meadow_mushrooms): one bumper circle for
    ///     every whole <see cref="MushroomBumperModel" /> on the grid, in the order of their ids, and the caps a
    ///     lance has cut, which are no bumpers until they grow back.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A circle's id is the cap's own and its wall the one the cap stands in, its centre the slot's world
    ///         position at rest, and its radius the cap's own plus the ball's (<see cref="BumperCircle.Radius" />
    ///         is where the
    ///         ball's centre is at the touch). The centre comes from the slot and not from a view: a mushroom
    ///         never drifts, and the shot solver and the aim line have to bounce off the same circles.
    ///     </para>
    ///     <para>
    ///         The circles are kept and rebuilt only when the grid has changed since they were read
    ///         (<see cref="SlotGrid.MutationVersion" />) or the ball's radius has (it reads as zero until the
    ///         thrower starts). Reading them on an unchanged board costs two comparisons and allocates nothing,
    ///         which is what lets the flight ask on every fixed step.
    ///     </para>
    ///     <para>
    ///         <b>Cut caps.</b> <see cref="Cut" /> takes a cap out of the circles there and then and remembers its
    ///         model with the beats it has to wait (<see cref="IMushroomSettings.RegrowBeats" />). The model stays
    ///         in its slot, so the slot stays taken, and a rebuild leaves a cut cap out. The cut caps are kept in
    ///         the order they were cut. The board keeps no clock:
    ///         it is told each beat (<see cref="CountBeat" />) and told to put a cap back
    ///         (<see cref="Regrow" />). A cut cap whose model has left the grid is forgotten at the next rebuild,
    ///         so a level's end, a retry and a reset leave nothing cut: the next level's caps are new models.
    ///     </para>
    /// </remarks>
    internal sealed class MushroomBoard : IBumperField, IMushroomRegrowth
    {
        // No layout holds more caps than this (IMushroomSettings.Placement.MaxCaps is 7), so cutting never grows
        // the list.
        private const int MostCapsExpected = 8;

        private readonly SlotGrid _grid;
        private readonly IMushroomSettings _settings;
        private readonly IShotOrigin _shotOrigin;
        private readonly List<BumperCircle> _bumpers = new();
        private readonly List<Vector2Int> _slots = new();
        private readonly List<CutCap> _cut = new(MostCapsExpected);

        private int _builtAtVersion = -1;
        private float _builtForContactRadius = -1f;

        public IReadOnlyList<BumperCircle> Bumpers
        {
            get
            {
                EnsureBuilt();
                return _bumpers;
            }
        }

        public int CutCount
        {
            get
            {
                if (_cut.Count == 0)
                {
                    return 0;
                }

                // A level that ended with caps cut took their models off the grid: the rebuild forgets them.
                EnsureBuilt();
                return _cut.Count;
            }
        }

        internal MushroomBoard(SlotGrid grid, IMushroomSettings settings, IShotOrigin shotOrigin)
        {
            _grid = grid;
            _settings = settings;
            _shotOrigin = shotOrigin;
        }

        public Vector2Int SlotAt(int index)
        {
            EnsureBuilt();
            return _slots[index];
        }

        public void Cut(int index)
        {
            EnsureBuilt();
            if (index < 0 || index >= _bumpers.Count || _grid.At(_slots[index]) is not MushroomBumperModel mushroom)
            {
                return;
            }

            _cut.Add(new CutCap(mushroom, Mathf.Max(1, _settings.RegrowBeats)));
            _bumpers.RemoveAt(index);
            _slots.RemoveAt(index);
        }

        public void CountBeat(int caps)
        {
            for (var i = 0; i < caps && i < _cut.Count; i++)
            {
                var cap = _cut[i];
                if (cap.BeatsLeft > 0)
                {
                    _cut[i] = new CutCap(cap.Model, cap.BeatsLeft - 1);
                }
            }
        }

        public bool IsDueAt(int index)
        {
            return _cut[index].BeatsLeft <= 0;
        }

        public BumperCircle CutCircleAt(int index)
        {
            return CircleOf(_cut[index].Model, _builtForContactRadius);
        }

        public Vector2Int CutSlotAt(int index)
        {
            return _cut[index].Model.SlotIndex;
        }

        public void Regrow(int index)
        {
            EnsureBuilt();
            if (index < 0 || index >= _cut.Count)
            {
                return;
            }

            var mushroom = _cut[index].Model;
            _cut.RemoveAt(index);
            Insert(CircleOf(mushroom, _builtForContactRadius), mushroom.SlotIndex);
        }

        private void EnsureBuilt()
        {
            var contactRadius = _settings.Radius + _shotOrigin.ProjectileContactRadius;
            if (_builtAtVersion == _grid.MutationVersion && _builtForContactRadius == contactRadius)
            {
                return;
            }

            _builtAtVersion = _grid.MutationVersion;
            _builtForContactRadius = contactRadius;
            _bumpers.Clear();
            _slots.Clear();
            var cutOnTheGrid = 0;
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var slot = new Vector2Int(col, row);
                    if (_grid.At(slot) is not MushroomBumperModel mushroom)
                    {
                        continue;
                    }

                    if (IsCut(mushroom))
                    {
                        cutOnTheGrid++;
                    }
                    else
                    {
                        Insert(CircleOf(mushroom, contactRadius), slot);
                    }
                }
            }

            if (cutOnTheGrid < _cut.Count)
            {
                ForgetCapsOffTheGrid();
            }
        }

        private BumperCircle CircleOf(MushroomBumperModel mushroom, float contactRadius)
        {
            return new BumperCircle(
                mushroom.ChainIndex, mushroom.Wall, _grid.IndexToWorldPosition(mushroom.SlotIndex), contactRadius);
        }

        private bool IsCut(MushroomBumperModel mushroom)
        {
            for (var i = 0; i < _cut.Count; i++)
            {
                if (ReferenceEquals(_cut[i].Model, mushroom))
                {
                    return true;
                }
            }

            return false;
        }

        private void ForgetCapsOffTheGrid()
        {
            for (var i = _cut.Count - 1; i >= 0; i--)
            {
                var mushroom = _cut[i].Model;
                if (!ReferenceEquals(_grid.At(mushroom.SlotIndex), mushroom))
                {
                    _cut.RemoveAt(i);
                }
            }
        }

        // Kept in the order of their ids as they are found: at most a handful, so an insertion is the whole sort.
        private void Insert(in BumperCircle bumper, Vector2Int slot)
        {
            var at = _bumpers.Count;
            while (at > 0 && _bumpers[at - 1].Id > bumper.Id)
            {
                at--;
            }

            _bumpers.Insert(at, bumper);
            _slots.Insert(at, slot);
        }

        private readonly struct CutCap
        {
            public readonly MushroomBumperModel Model;
            public readonly int BeatsLeft;

            public CutCap(MushroomBumperModel model, int beatsLeft)
            {
                Model = model;
                BeatsLeft = beatsLeft;
            }
        }
    }
}
