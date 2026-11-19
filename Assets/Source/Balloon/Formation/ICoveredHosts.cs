using System.Collections.Generic;
using BalloonParty.Slots.Actor;
using UnityEngine;

namespace BalloonParty.Balloon.Formation
{
    /// <summary>A balloon an item cover stands round, and the formation that covers it.</summary>
    internal readonly struct CoveredHost
    {
        /// <summary>The covered balloon: the same object for as long as this cover stands round it.</summary>
        public readonly ISlotActor Actor;

        /// <summary>The covering formation's id, the one its members carry (<c>IFormationMember.FormationId</c>).</summary>
        public readonly int FormationId;

        /// <summary>World centre of the slot the balloon rests on, where its cover holds it.</summary>
        public readonly Vector2 Centre;

        public CoveredHost(ISlotActor actor, int formationId, Vector2 centre)
        {
            Actor = actor;
            FormationId = formationId;
            Centre = centre;
        }
    }

    /// <summary>
    ///     The balloons item covers stand round now, for views that draw a cover as one body with what it covers
    ///     (<c>Balloon/View/FormationGlowHosts</c>). A balloon is in from the cycle its cover is planned until the
    ///     cover changes shape or breaks, or the balloon pops or is taken off the board. It is held in its slot
    ///     all that time (<c>IWriteableCoverHost</c>), so nothing that balances the board moves it out.
    /// </summary>
    internal interface ICoveredHosts
    {
        IReadOnlyList<CoveredHost> CoveredHosts { get; }
    }
}
