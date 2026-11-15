using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    ///     A slot-actor view that can play the paint-splash drip reaction. Resolved off the grid via
    ///     <c>ActorViewAt&lt;IPaintReactive&gt;</c> so the Paint handler stays view-agnostic. The drip is
    ///     the same overlay for accept and reject — layering decides the read: an accepted balloon has
    ///     already committed the new colour underneath, a resisting one hasn't, so the incoming paint
    ///     either settles or slides off to reveal the unchanged body.
    /// </summary>
    public interface IPaintReactive : ISlotActorView
    {
        /// <param name="rainbow">Rainbow paint: the drip draws the level's colours in bands, not <paramref name="paintColor" />.</param>
        void PlayPaintDrip(Color paintColor, bool rainbow);

        /// <summary>
        ///     A small burst of the colour the balloon just lost, for a recolour that should read as a pop without
        ///     one (the disco ball's wave). <paramref name="scale" /> is a share of a pop's size.
        /// </summary>
        void PlayRecolourBurst(Color oldColor, float scale);
    }
}
