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

        internal void Reset()
        {
            MemberList.Clear();
            WavesLeft = 0;
            Touched = false;
            Heading = 0;
            ArrangedOnPass = 0;
        }
    }
}
