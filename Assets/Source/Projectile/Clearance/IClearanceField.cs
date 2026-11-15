using UnityEngine;

namespace BalloonParty.Projectile.Clearance
{
    /// <summary>
    ///     The clear circles around the balls in flight, plus the fading ones balls left behind. What
    ///     formations, trails and effect shaders keep out of; none of them reads the balls themselves.
    ///     An index is good for one frame only: the circles are repacked every frame, so
    ///     <c>CircleAt(i)</c> can be a different ball's circle next frame.
    /// </summary>
    internal interface IClearanceField
    {
        int Count { get; }

        ClearanceCircle CircleAt(int index);

        /// <summary>
        ///     <paramref name="point" /> bent out of every circle in turn (<see cref="ClearanceGeometry.Deflect" />),
        ///     XY only; Z is kept.
        /// </summary>
        Vector3 Deflect(Vector3 point);
    }
}
