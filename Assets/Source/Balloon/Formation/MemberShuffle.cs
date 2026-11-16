using System;
using System.Collections.Generic;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Balloon.Formation
{
    /// <summary>
    ///     Moves formation members onto their slots when one member's slot is another's target. Each member moves
    ///     once its target is empty, so a line follows its leader. Members that only trade places among
    ///     themselves (a tough snake re-ordered by armour) wait on each other in a ring, so one steps aside to
    ///     the nearest free slot, the ring moves, and it goes on to its own slot. That member's glide bends
    ///     through the slot it stepped aside to.
    /// </summary>
    internal sealed class MemberShuffle
    {
        private readonly List<FormationAssignment> _pending = new();

        /// <summary>
        ///     Moves every member in <paramref name="moves" /> to its target through <paramref name="relocate" />,
        ///     which removes it from its slot and places it on the target. With no free slot to step aside to, a
        ///     ring of members stays where it is.
        /// </summary>
        internal void Run(
            SlotGrid grid, IReadOnlyList<FormationAssignment> moves,
            Action<IWriteableDynamicSlotActor, Vector2Int> relocate)
        {
            _pending.Clear();
            for (var i = 0; i < moves.Count; i++)
            {
                if (moves[i].Member.SlotIndex.Value != moves[i].Target)
                {
                    _pending.Add(moves[i]);
                }
            }

            // A ring unwinds once one of it steps aside, so more asides than members means a target is held by
            // something no move will clear.
            var asides = 0;
            while (_pending.Count > 0)
            {
                var moved = false;
                for (var i = 0; i < _pending.Count; i++)
                {
                    var move = _pending[i];
                    if (grid.IsEmpty(move.Target.x, move.Target.y))
                    {
                        relocate(move.Member, move.Target);
                        _pending.RemoveAt(i);
                        i--;
                        moved = true;
                    }
                }

                if (moved)
                {
                    continue;
                }

                // Only rings are left: every target holds another member still to move.
                var aside = _pending[0].Member;
                if (asides >= moves.Count || !TryFindAside(grid, moves, aside.SlotIndex.Value, out var slot))
                {
                    return;
                }

                asides++;
                relocate(aside, slot);
            }
        }

        // The free slot nearest to from that no member is headed for.
        private static bool TryFindAside(
            SlotGrid grid, IReadOnlyList<FormationAssignment> moves, Vector2Int from, out Vector2Int aside)
        {
            aside = default;
            var best = int.MaxValue;
            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    var slot = new Vector2Int(col, row);
                    if (!grid.IsEmpty(col, row) || IsTarget(moves, slot))
                    {
                        continue;
                    }

                    var distance = HexCoordinates.Distance(from, slot);
                    if (distance < best)
                    {
                        best = distance;
                        aside = slot;
                    }
                }
            }

            return best != int.MaxValue;
        }

        private static bool IsTarget(IReadOnlyList<FormationAssignment> moves, Vector2Int slot)
        {
            for (var i = 0; i < moves.Count; i++)
            {
                if (moves[i].Target == slot)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
