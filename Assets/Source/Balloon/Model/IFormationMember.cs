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
    }
}
