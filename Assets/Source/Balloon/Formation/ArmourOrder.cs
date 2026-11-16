using System.Collections.Generic;
using BalloonParty.Balloon.Model;

namespace BalloonParty.Balloon.Formation
{
    /// <summary>
    ///     The order of the tough snake, head to tail: the most armour (hits left) leads. Toughs with equal armour
    ///     keep the order they stand in. The snake moves toward that order one place per step, so a hit or a tough
    ///     joining at the tail never sends a member leaping along the body.
    /// </summary>
    internal static class ArmourOrder
    {
        /// <summary>
        ///     One step of <paramref name="members" /> toward most armour first: from the head down, each member with
        ///     more armour than the one ahead of it trades places with it. No member moves more than one place, so a
        ///     tough joined at the tail, or one hit at the head, works its way to its place a step at a time, and the
        ///     one it passes waits a beat beside it. Equal armour never trades. True when anyone moved.
        /// </summary>
        internal static bool StepTowardOrder(List<IWriteableFormationMember> members)
        {
            var moved = false;
            for (var i = 0; i < members.Count - 1; i++)
            {
                if (Of(members[i]) >= Of(members[i + 1]))
                {
                    continue;
                }

                (members[i], members[i + 1]) = (members[i + 1], members[i]);
                moved = true;

                // Past the pair just traded, so neither of them moves again this step.
                i++;
            }

            return moved;
        }

        internal static int Of(IWriteableFormationMember member)
        {
            return member is IArmouredFormationMember armoured ? armoured.Armour : 0;
        }
    }
}
