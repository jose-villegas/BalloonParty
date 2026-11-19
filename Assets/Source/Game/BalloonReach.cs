using BalloonParty.UI.ClothRibbon;
using UnityEngine;

namespace BalloonParty.Game
{
    /// <summary>
    ///     The room a drifting launch balloon takes up: its round body, and how far below the body's centre what
    ///     hangs from it reaches when it hangs still.
    /// </summary>
    internal readonly struct BalloonReach
    {
        internal readonly Vector2 Centre;
        internal readonly float Radius;
        internal readonly float Hang;

        internal BalloonReach(Vector2 centre, float radius, float hang)
        {
            Centre = centre;
            Radius = radius;
            Hang = hang;
        }

        /// <summary>
        ///     Where the centre of <paramref name="other" /> may not be, to keep <paramref name="gap" /> between the
        ///     two bodies with neither hanging its line and ribbon onto the other: below this one by less than this
        ///     one's hang, or above it by less than its own. The two balloons' keep-outs of each other say the same
        ///     thing, so neither can push the other about.
        /// </summary>
        internal KeepOut KeepOutFor(in BalloonReach other, float gap)
        {
            return new KeepOut(
                Centre + Vector2.down * Hang, Centre + Vector2.up * other.Hang, Radius + other.Radius + gap);
        }
    }
}
