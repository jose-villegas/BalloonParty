using UniRx;
using UnityEngine;

namespace BalloonParty.Balloon.Model
{
    /// <summary>A balloon that can join an unbreakable formation; the view reads it to swap its lights.</summary>
    internal interface IFormationMember
    {
        IReadOnlyReactiveProperty<bool> InFormation { get; }

        /// <summary>
        ///     The world direction the member's formation runs, shared by every member of it. It keeps its
        ///     last value after the formation breaks, so the look fades out without turning.
        /// </summary>
        Vector2 FormationHeading { get; }

        /// <summary>
        ///     Which formation it is in: the same for every member of one, and unlike any other formation's. It
        ///     keeps its last value after the formation breaks, as the heading does, so what a member leaves
        ///     behind (its ring) still goes with its fellows'.
        /// </summary>
        int FormationId { get; }

        /// <summary>
        ///     Its formation walks (a snake): it keeps the formation's look while it glides from slot to slot.
        ///     Cleared when it leaves its formation.
        /// </summary>
        bool Walking { get; }
    }
}
