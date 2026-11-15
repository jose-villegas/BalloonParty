using UnityEngine;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     A bubble cluster's look as a merge plays out (<see cref="BubbleMergeView" />). The model's count changes at
    ///     once; while held, the cluster keeps showing the count it had and steps it one bubble at a time as bubbles
    ///     leave or land, then shows the model's count again when the last hold lets go.
    /// </summary>
    internal interface IBubbleMergeReactive
    {
        /// <summary>The cluster's renderer, which a hopping bubble copies its look from.</summary>
        SpriteRenderer BubbleRenderer { get; }

        /// <summary>A bubble's world size at rest, without a wobble in progress.</summary>
        Vector3 BubbleWorldScale { get; }

        /// <summary>
        ///     Changes whenever the holds are dropped with the binding (despawn, a level transition's freeze), so a merge
        ///     can tell its holds are gone and must not release holds taken since.
        /// </summary>
        int Binding { get; }

        /// <summary>Freezes the shown count where it is. Holds stack: each needs its own <see cref="ReleaseCount" />.</summary>
        void HoldCount();

        /// <summary>Moves the shown count by <paramref name="delta" /> bubbles while held.</summary>
        void StepCount(int delta);

        /// <summary>Lets go of one hold; the last shows the model's count again.</summary>
        void ReleaseCount();

        /// <summary>Punches the bubbles out by <paramref name="strength" /> of their size and lets them settle.</summary>
        void Wobble(float strength, float seconds);
    }
}
