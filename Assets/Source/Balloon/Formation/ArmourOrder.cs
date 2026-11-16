using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using UnityEngine;

namespace BalloonParty.Balloon.Formation
{
    /// <summary>
    ///     The order of a tough snake, head to tail: the most armour (hits left) leads. Toughs with equal armour
    ///     keep the order they stand in, so a snake only re-orders when a hit changes someone's armour.
    /// </summary>
    internal sealed class ArmourOrder
    {
        private readonly List<IWriteableFormationMember> _members = new();
        private readonly List<Vector2Int> _slots = new();

        /// <summary>
        ///     Sorts <paramref name="members" /> most armour first. Stable: equal armour keeps its place, so a
        ///     snake with no hit since its last step keeps its body as it is.
        /// </summary>
        internal static void Sort(List<IWriteableFormationMember> members)
        {
            for (var i = 1; i < members.Count; i++)
            {
                var member = members[i];
                var armour = Of(member);
                var j = i - 1;
                while (j >= 0 && Of(members[j]) < armour)
                {
                    members[j + 1] = members[j];
                    j--;
                }

                members[j + 1] = member;
            }
        }

        internal static int Of(IWriteableFormationMember member)
        {
            return member is IArmouredFormationMember armoured ? armoured.Armour : 0;
        }

        /// <summary>
        ///     Re-pairs a snake's row (<paramref name="assignments" />, one slot per member) so the most armour takes
        ///     the slot at the leading end (east or west) and the rest follow by armour. Ties go to the one nearer
        ///     the leading end, so a row of equal armour already in place keeps its slots. Leaves the assignments
        ///     head first.
        /// </summary>
        internal void LineUp(List<FormationAssignment> assignments, bool east)
        {
            _members.Clear();
            _slots.Clear();
            for (var i = 0; i < assignments.Count; i++)
            {
                _members.Add(assignments[i].Member);
                _slots.Add(assignments[i].Target);
            }

            SortSlots(_slots, east);
            SortMembers(_members, east);
            for (var i = 0; i < assignments.Count; i++)
            {
                assignments[i] = new FormationAssignment(_members[i], _slots[i]);
            }
        }

        private static void SortSlots(List<Vector2Int> slots, bool east)
        {
            for (var i = 1; i < slots.Count; i++)
            {
                var slot = slots[i];
                var j = i - 1;
                while (j >= 0 && Leads(slot, slots[j], east))
                {
                    slots[j + 1] = slots[j];
                    j--;
                }

                slots[j + 1] = slot;
            }
        }

        private static void SortMembers(List<IWriteableFormationMember> members, bool east)
        {
            for (var i = 1; i < members.Count; i++)
            {
                var member = members[i];
                var armour = Of(member);
                var j = i - 1;
                while (j >= 0 && Precedes(armour, member.SlotIndex.Value, members[j], east))
                {
                    members[j + 1] = members[j];
                    j--;
                }

                members[j + 1] = member;
            }
        }

        private static bool Precedes(int armour, Vector2Int slot, IWriteableFormationMember other, bool east)
        {
            var otherArmour = Of(other);
            return armour != otherArmour ? armour > otherArmour : Leads(slot, other.SlotIndex.Value, east);
        }

        // Nearer the leading end: farther east for a snake heading east, farther west for one heading west; the
        // higher row breaks a tie.
        private static bool Leads(Vector2Int a, Vector2Int b, bool east)
        {
            if (a.x != b.x)
            {
                return east ? a.x > b.x : a.x < b.x;
            }

            return a.y < b.y;
        }
    }
}
