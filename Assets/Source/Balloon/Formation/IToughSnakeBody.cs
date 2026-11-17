using System.Collections.Generic;
using BalloonParty.Slots.Actor;

namespace BalloonParty.Balloon.Formation
{
    /// <summary>The tough snake as it stands, for views that draw it as one body.</summary>
    internal interface IToughSnakeBody
    {
        /// <summary>Its members, head first; empty while no tough snake walks the board.</summary>
        IReadOnlyList<ISlotActor> ToughSnakeMembers { get; }
    }
}
