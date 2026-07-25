using System.Collections.Generic;
using BalloonParty.Item.Effects;
using BalloonParty.Shared;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Solver
{
    /// <summary>The hex-lattice geometry an item effect core needs, snapshotted once — no
    /// <c>SlotGrid</c>/dynamics reference, so it stays constructible in an EditMode test.
    /// <see cref="SlotPosition" /> reproduces <c>SlotGrid.IndexToWorldPosition</c>'s forward mapping
    /// bit-for-bit (both call the same <see cref="HexCoordinates.IndexToWorldPosition" />).</summary>
    internal readonly struct ShotSlotLattice
    {
        public readonly Vector2 Separation;
        public readonly Vector2 Offset;
        public readonly int Columns;
        public readonly int Rows;

        public ShotSlotLattice(Vector2 separation, Vector2 offset, int columns, int rows)
        {
            Separation = separation;
            Offset = offset;
            Columns = columns;
            Rows = rows;
        }

        public static ShotSlotLattice From(ISlotGridConfig config)
        {
            return new ShotSlotLattice(config.SlotSeparation, config.SlotsOffset, config.SlotsSize.x, config.SlotsSize.y);
        }

        public Vector2 SlotPosition(Vector2Int index)
        {
            return HexCoordinates.IndexToWorldPosition(index, Separation, Offset);
        }
    }

    /// <summary>Solver-side <see cref="IEffectBoard" /> — runs purely over the working set +
    /// <see cref="ShotSlotLattice" /> (@ref plan_shot_solver_accuracy Phase C: "runs on the working set
    /// + HexCoordinates only — NOT the dynamics grid"), so an item core selects identically whether or
    /// not <c>ShotBoardDynamics</c> is present. A handle is the occupant's stable <c>SlotIndex</c>
    /// (boxed) rather than a working-set array index — swap-remove reorders the array between
    /// activations, but a <see cref="ShotBalloonState.SlotIndex" /> survives it.</summary>
    internal sealed class ShotSimEffectBoard : IEffectBoard
    {
        private readonly ShotSlotLattice _lattice;
        private readonly List<EffectOccupant> _occupants = new();

        public IReadOnlyList<EffectOccupant> Occupants => _occupants;
        public int SearchRadius => Mathf.Max(_lattice.Columns, _lattice.Rows);

        public ShotSimEffectBoard(in ShotSlotLattice lattice)
        {
            _lattice = lattice;
        }

        public bool TryGetOccupantAt(Vector2Int slot, out EffectOccupant occupant)
        {
            for (var i = 0; i < _occupants.Count; i++)
            {
                if (_occupants[i].Slot == slot)
                {
                    occupant = _occupants[i];
                    return true;
                }
            }

            occupant = default;
            return false;
        }

        /// <summary>Rebuilds the occupant list from the live working set for one activation — every
        /// non-static active entry except <paramref name="excludeSlot" /> (the popped host).</summary>
        public void Bind(ShotBalloonState[] workingSet, int activeCount, Vector2Int? excludeSlot = null)
        {
            _occupants.Clear();

            for (var i = 0; i < activeCount; i++)
            {
                if (workingSet[i].IsStatic || workingSet[i].SlotIndex == excludeSlot)
                {
                    continue;
                }

                var slotPosition = _lattice.SlotPosition(workingSet[i].SlotIndex);
                var isPaintable = !workingSet[i].IsRainbow && !string.IsNullOrEmpty(workingSet[i].ColorId);

                _occupants.Add(new EffectOccupant(
                    workingSet[i].SlotIndex, workingSet[i].SlotIndex, workingSet[i].Position, slotPosition,
                    workingSet[i].Radius, workingSet[i].ColorId, isPaintable, resistsPaint: false));
            }
        }

        /// <summary>Unboxes an <see cref="EffectHit.Handle" /> this board produced back into the
        /// working-set slot it identifies.</summary>
        public Vector2Int SlotOf(object handle)
        {
            return (Vector2Int)handle;
        }
    }
}
