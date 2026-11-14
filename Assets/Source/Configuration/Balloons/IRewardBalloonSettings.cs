using System.Collections.Generic;

namespace BalloonParty.Configuration.Balloons
{
    /// <summary>
    ///     When the reward balloons come (PLAN-RewardBalloons): the golden one late in a purple level, the heart one
    ///     to a player missing hearts.
    /// </summary>
    public interface IRewardBalloonSettings
    {
        /// <summary>The level's completion, 0 to 1, from which a purple level's next line brings its golden balloon.</summary>
        float GoldenAtCompletion { get; }

        /// <summary>
        ///     The chance of each further golden balloon on a purple level, rolled once when the one before it pops:
        ///     the first entry for the second balloon, the next for the third. Their count caps the level's golden
        ///     balloons at one more than it.
        /// </summary>
        IReadOnlyList<float> GoldenRepeatChances { get; }

        /// <summary>Spawn lines after a golden balloon's pop before the further one it won comes.</summary>
        int GoldenRepeatDelayLines { get; }

        /// <summary>Chance per spawn line of a heart balloon while one heart is missing.</summary>
        float HeartChanceMissingOne { get; }

        /// <summary>Chance per spawn line of a heart balloon while two or more hearts are missing.</summary>
        float HeartChanceMissingTwo { get; }

        /// <summary>Added to the heart chance on a purple level, while a heart is missing.</summary>
        float HeartChanceOnPurple { get; }

        /// <summary>Spawn lines after a heart balloon is placed before another can roll.</summary>
        int HeartCooldownLines { get; }
    }
}
