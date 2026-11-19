using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     A bomb's blast reached a meadow mushroom without taking its cap off (@ref plan_meadow_mushrooms): the
    ///     mushroom is not in one of the six slots next to the bomb, where caps are blown off, and its cap still
    ///     overlaps the blast. The cap is pushed away from the blast and springs back toward it, the same elastic
    ///     reaction a ball's hit gives. Published once for each such mushroom, in the frame the bomb goes off,
    ///     beside the <see cref="MushroomCutMessage" />s of the caps the same bomb blew off.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Nothing about the rule changes for a mushroom that swayed: its cap is whole, the ball bounces off
    ///         it and the chain counts it as before. Only whole caps sway: a bare stem (a cap already cut or
    ///         blown off) gets no message. A nuke sways nothing, since it blows every cap off.
    ///     </para>
    ///     <para>
    ///         <b>How strong to expect it.</b> With the shipped numbers (slots 0.75 apart, a kill radius of 1.25,
    ///         a cap radius of 0.3, a sway's share of 2.4) the sway reaches 3.3 from the bomb: the second ring of
    ///         slots, 1.30 or 1.50 away, gets about 0.78 or 0.71, the third (1.98, 2.25) about 0.52 or 0.41, and
    ///         the fourth (2.60 to 3.00) 0.27 down to 0.12. At a share of 1 only the second ring is reached, at
    ///         0.31 and 0.06, which a view cannot show (<c>IMushroomSettings.Blast</c>).
    ///     </para>
    /// </remarks>
    internal readonly struct MushroomSwayedMessage
    {
        /// <summary>The mushroom's grid slot, as <see cref="MushroomBouncedMessage.Slot" /> names it.</summary>
        public readonly Vector2Int Slot;

        /// <summary>The way the cap is pushed: the unit direction, in world space, from the blast's centre to the
        /// mushroom's. It leans that way first and swings back toward the blast.</summary>
        public readonly Vector2 Direction;

        /// <summary>
        ///     How hard, from 0 to 1: 1 at the distance of the slots next to the bomb (where the cap is blown
        ///     off instead, so no swaying mushroom stands nearer), falling in a straight line with distance to 0
        ///     at the blast's reach, its kill radius and the cap's own radius. Always above 0: a mushroom at or
        ///     past the reach gets no message.
        /// </summary>
        public readonly float Strength;

        public MushroomSwayedMessage(Vector2Int slot, Vector2 direction, float strength)
        {
            Slot = slot;
            Direction = direction;
            Strength = strength;
        }
    }
}
