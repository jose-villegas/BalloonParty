using System;

namespace BalloonParty.Game.Upgrades
{
    /// <summary>A card won in play rather than at a level-up (the golden balloon's pop).</summary>
    internal interface IFreeCardGrant
    {
        /// <summary>
        ///     Deals one card the run can still take, holds it, and announces it with <c>UpgradeCardHeldMessage</c>,
        ///     which plays the card reveal. Won while a level-up's hand is on screen, it waits for the pick to be
        ///     held. A retry of the level it was won on starts without it, as with any card won after the level began.
        ///     Calls <paramref name="noCardLeft" /> instead when every card is at its limit, then or once the pick is
        ///     held, so the winner always gets something.
        /// </summary>
        void GrantFreeCard(Action noCardLeft);
    }
}
