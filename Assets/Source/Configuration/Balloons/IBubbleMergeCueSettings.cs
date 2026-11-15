using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    /// <summary>
    ///     How a bubble merge looks: the bubbles a giver passes on while it stays hop one by one to the receiver, which wobbles
    ///     and splashes as each lands. A giver absorbed whole flows in (<see cref="IBalloonsConfiguration.BubbleMergeSeconds" />)
    ///     and the receiver takes it in the same way when it arrives.
    /// </summary>
    public interface IBubbleMergeCueSettings
    {
        /// <summary>Seconds one bubble takes to hop from the giver to the receiver.</summary>
        float HopSeconds { get; }

        /// <summary>Seconds between one bubble leaving the giver and the next.</summary>
        float HopStagger { get; }

        /// <summary>How far, in world units, a hop bows to the side of the straight line between the clusters.</summary>
        float ArcHeight { get; }

        /// <summary>A hopping bubble's size, as a share of a bubble in the cluster it left.</summary>
        float BubbleScale { get; }

        /// <summary>Sorting orders a hopping bubble draws above the higher of the two clusters.</summary>
        int SortingOrderOffset { get; }

        /// <summary>How hard the receiver's bubbles punch out as each one lands, as a share of its size.</summary>
        float ReceiverWobble { get; }

        /// <summary>How hard the giver's bubbles punch as each one leaves, as a share of its size.</summary>
        float GiverWobble { get; }

        /// <summary>Seconds a wobble takes to settle.</summary>
        float WobbleSeconds { get; }

        /// <summary>The splash where a bubble lands, as a share of a balloon pop's size. 0 turns it off.</summary>
        float SplashScale { get; }

        /// <summary>The splash's tint.</summary>
        Color SplashColor { get; }
    }
}
