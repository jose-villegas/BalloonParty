using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Slots.Actor;
using UnityEngine;

namespace BalloonParty.Balloon.Formation
{
    /// <summary>One standing formation: its members, its shape, and how long it holds that shape.</summary>
    internal sealed class StandingFormation
    {
        internal readonly List<IWriteableFormationMember> MemberList = new();

        internal IReadOnlyList<IWriteableFormationMember> Members => MemberList;
        internal FormationShape Shape { get; set; }

        /// <summary>
        ///     Which formation this is, unlike any other standing with it; each member carries it
        ///     (<see cref="IFormationMember.FormationId" />). A new one each time the object is taken up again.
        /// </summary>
        internal int Id { get; set; }

        /// <summary>Cycles left to hold the current shape before it changes.</summary>
        internal int CyclesLeft { get; set; }

        /// <summary>The wave it formed in, which rolls for no reinforcement.</summary>
        internal int FormedOnWave { get; set; }

        /// <summary>A member was pierced since it formed; a touched formation calls no reinforcements.</summary>
        internal bool Touched { get; set; }

        /// <summary>
        ///     A snake's walking direction (<see cref="Slots.Grid.HexCoordinates.Step" />); its members run head
        ///     first.
        /// </summary>
        internal int Heading { get; set; }

        /// <summary>The cycle's arrangement that last formed or re-planned it.</summary>
        internal int ArrangedOnPass { get; set; }

        /// <summary>
        ///     The snake of toughs (<see cref="IArmouredFormationMember" />), one at most: it takes every other tough
        ///     in at its tail, walks through the balloons in its way, keeping its order, until its last member pops,
        ///     and never changes shape or calls reinforcements.
        /// </summary>
        internal bool Armoured { get; set; }

        /// <summary>
        ///     The balloon an item cover stands round, held on <see cref="HostSlot" /> for as long as it is the host
        ///     (<see cref="IWriteableCoverHost" />); null for any other shape, and once that balloon has popped or
        ///     been taken off the board (the cover then stands round nothing until it changes).
        /// </summary>
        internal IWriteableSlotActor Host { get; set; }

        /// <summary>Where <see cref="Host" /> stood when the cover was planned.</summary>
        internal Vector2Int HostSlot { get; set; }

        internal void Reset()
        {
            MemberList.Clear();
            Host = null;
            Armoured = false;
            CyclesLeft = 0;
            FormedOnWave = 0;
            Touched = false;
            Heading = 0;
            ArrangedOnPass = 0;
        }
    }
}
