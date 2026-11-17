using System.Collections.Generic;
using BalloonParty.Balloon.Model;

namespace BalloonParty.Balloon.Formation
{
    /// <summary>One standing formation: its members, its shape, and how long it holds that shape.</summary>
    internal sealed class StandingFormation
    {
        internal readonly List<IWriteableFormationMember> MemberList = new();

        internal IReadOnlyList<IWriteableFormationMember> Members => MemberList;
        internal FormationShape Shape { get; set; }

        /// <summary>Wave starts left to hold the current shape before it changes.</summary>
        internal int WavesLeft { get; set; }

        /// <summary>A member was pierced since it formed; a touched formation calls no reinforcements.</summary>
        internal bool Touched { get; set; }

        /// <summary>
        ///     A snake's walking direction (<see cref="Slots.Grid.HexCoordinates.Step" />); its members run head
        ///     first.
        /// </summary>
        internal int Heading { get; set; }

        /// <summary>The wave-start arrangement that last formed or re-planned it.</summary>
        internal int ArrangedOnPass { get; set; }

        /// <summary>
        ///     The snake of toughs (<see cref="IArmouredFormationMember" />), one at most: it takes every other tough
        ///     in at its tail, walks through the balloons in its way, keeping its order, until its last member pops,
        ///     and never changes shape or calls reinforcements.
        /// </summary>
        internal bool Armoured { get; set; }

        internal void Reset()
        {
            MemberList.Clear();
            Armoured = false;
            WavesLeft = 0;
            Touched = false;
            Heading = 0;
            ArrangedOnPass = 0;
        }
    }
}
