using System.Collections.Generic;

namespace BalloonParty.Configuration.Balloons
{
    /// <summary>
    ///     When the reward balloons come (PLAN-RewardBalloons): on a purple level, both rolled every few lines with
    ///     each further one rarer; elsewhere, now and then a single golden one, and a heart one for a player missing
    ///     hearts.
    /// </summary>
    public interface IRewardBalloonSettings
    {
        /// <summary>
        ///     The level's completion, 0 to 1, from which an ordinary level that won its golden balloon brings it
        ///     (<see cref="OrdinaryGoldenEveryLevels" />).
        /// </summary>
        float GoldenAtCompletion { get; }

        /// <summary>
        ///     Off a purple level, every this many levels rolls once (<see cref="OrdinaryGoldenChance" />) for a single
        ///     golden balloon at <see cref="GoldenAtCompletion" />. 0 is off.
        /// </summary>
        int OrdinaryGoldenEveryLevels { get; }

        float OrdinaryGoldenChance { get; }

        /// <summary>A level that always brings a single golden balloon, with no roll; 0 for none.</summary>
        int SureGoldenLevel { get; }

        /// <summary>The completion the sure level's golden balloon comes at, earlier than the others'.</summary>
        float SureGoldenAtCompletion { get; }

        /// <summary>
        ///     Spawn lines between a purple level's reward rolls, golden and heart each on its own count. The first roll
        ///     of each comes on the first line it can; lines while one of that kind is on the board don't count.
        /// </summary>
        int PurpleRollEveryLines { get; }

        /// <summary>The level's completion, 0 to 1, from which a purple level rolls for golden balloons.</summary>
        float PurpleGoldenAtCompletion { get; }

        /// <summary>
        ///     A purple level's golden balloons: the chance each roll brings the next one, the first entry for the
        ///     first. Their count caps the level's golden balloons.
        /// </summary>
        IReadOnlyList<float> PurpleGoldenChances { get; }

        /// <summary>
        ///     A purple level's heart balloons, rolled from the level's start, at full hearts too: the chance each roll
        ///     brings the next one, the first entry for the first. Their count caps the level's heart balloons; the
        ///     roll while hurt (and its cooldown) is off there.
        /// </summary>
        IReadOnlyList<float> PurpleHeartChances { get; }

        /// <summary>Chance per spawn line of a heart balloon while one heart is missing, off a purple level.</summary>
        float HeartChanceMissingOne { get; }

        /// <summary>Chance per spawn line of a heart balloon while two or more hearts are missing.</summary>
        float HeartChanceMissingTwo { get; }

        /// <summary>Spawn lines after a heart balloon is placed before another can roll.</summary>
        int HeartCooldownLines { get; }
    }
}
