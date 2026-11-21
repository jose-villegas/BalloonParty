using System.Collections.Generic;

namespace BalloonParty.Configuration.Balloons
{
    /// <summary>
    ///     When the reward balloons come (PLAN-RewardBalloons): on a purple level, both rolled every few lines with
    ///     each further one rarer; elsewhere, now and then a single golden one or disco ball, likelier the longer
    ///     since the last, and a heart one for a player missing hearts.
    /// </summary>
    public interface IRewardBalloonSettings
    {
        /// <summary>
        ///     The level's completion, 0 to 1, from which an ordinary level that won its golden balloon brings it
        ///     (<see cref="OrdinaryGoldenEveryLevels" />).
        /// </summary>
        float GoldenAtCompletion { get; }

        /// <summary>
        ///     Off a purple level, every this many levels from <see cref="SureGoldenLevel" /> on rolls once
        ///     (<see cref="OrdinaryGoldenChance" />) for a single golden balloon at <see cref="GoldenAtCompletion" />.
        ///     1 is every level, 0 is off.
        /// </summary>
        int OrdinaryGoldenEveryLevels { get; }

        /// <summary>
        ///     The ordinary roll's chance at the sure level, eased off over the run
        ///     (<see cref="OrdinaryChanceHalvedAfterLevels" />) and raised by each level without one
        ///     (<see cref="OrdinaryChanceGrowthPerLevel" />).
        /// </summary>
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

        /// <summary>
        ///     The disco ball's completion, 0 to 1, from which an ordinary level that won it brings it
        ///     (<see cref="OrdinaryDiscoEveryLevels" />). The golden balloon's rules, its own numbers (PLAN-DiscoBalloon).
        /// </summary>
        float DiscoAtCompletion { get; }

        /// <summary>
        ///     Off a purple level, every this many levels, from <see cref="SureDiscoLevel" /> on, rolls once
        ///     (<see cref="OrdinaryDiscoChance" />) for a single disco ball. 1 is every level, 0 is off.
        /// </summary>
        int OrdinaryDiscoEveryLevels { get; }

        /// <summary>
        ///     The ordinary roll's chance at the sure level, as <see cref="OrdinaryGoldenChance" />.
        /// </summary>
        float OrdinaryDiscoChance { get; }

        /// <summary>
        ///     How the golden balloon's and the disco ball's ordinary chances ease off over a run: halved this many
        ///     levels past each one's sure level, a third at twice that. 0 is never.
        /// </summary>
        int OrdinaryChanceHalvedAfterLevels { get; }

        /// <summary>
        ///     Soft pity for the golden balloon and the disco ball off purple: each level since the last one of that
        ///     kind was placed (from its sure level on) adds this to its ordinary chance, so an eased chance never
        ///     stays low.
        /// </summary>
        float OrdinaryChanceGrowthPerLevel { get; }

        /// <summary>
        ///     The disco ball's soft pity inside an ordinary level that lost its roll: each time this much more of the
        ///     level is done, it rolls again (<see cref="DiscoPityChancePerStep" />). 0 turns it off.
        /// </summary>
        float DiscoPityEveryCompletion { get; }

        /// <summary>
        ///     The chance of the first of those rolls; the second has twice it, the third three times, so a level
        ///     near its end is likelier to get its disco ball than one just begun.
        /// </summary>
        float DiscoPityChancePerStep { get; }

        /// <summary>A level that always brings a single disco ball, with no roll; 0 for none.</summary>
        int SureDiscoLevel { get; }

        float SureDiscoAtCompletion { get; }

        /// <summary>The level's completion, 0 to 1, from which a purple level rolls for disco balls.</summary>
        float PurpleDiscoAtCompletion { get; }

        /// <summary>
        ///     A purple level's disco balls: the chance each roll brings the next one, the first entry for the first
        ///     (1 makes it sure). Their count caps the level's disco balls.
        /// </summary>
        IReadOnlyList<float> PurpleDiscoChances { get; }

        /// <summary>Chance per spawn line of a heart balloon while one heart is missing, off a purple level.</summary>
        float HeartChanceMissingOne { get; }

        /// <summary>Chance per spawn line of a heart balloon while two or more hearts are missing.</summary>
        float HeartChanceMissingTwo { get; }

        /// <summary>Spawn lines after a heart balloon is placed before another can roll.</summary>
        int HeartCooldownLines { get; }

        /// <summary>
        ///     How every heart balloon's chance eases off over a run, a purple level's too: halved this many levels
        ///     in, a third at twice that. 0 is never.
        /// </summary>
        int HeartChanceHalvedAfterLevels { get; }
    }
}
