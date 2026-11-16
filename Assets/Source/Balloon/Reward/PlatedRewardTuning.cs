using System.Collections.Generic;
using BalloonParty.Configuration.Balloons;

namespace BalloonParty.Balloon.Reward
{
    /// <summary>
    ///     One plated reward balloon's schedule, read from <see cref="IRewardBalloonSettings" /> each time it is asked,
    ///     so a value tuned in the inspector applies on the next line. The golden balloon and the disco ball share the
    ///     rules (<see cref="PlatedRewardSchedule" />) and differ only in these numbers.
    /// </summary>
    internal readonly struct PlatedRewardTuning
    {
        public readonly int OrdinaryEveryLevels;
        public readonly float OrdinaryChance;
        public readonly float AtCompletion;
        public readonly int SureLevel;
        public readonly float SureAtCompletion;
        public readonly float PurpleAtCompletion;
        public readonly IReadOnlyList<float> PurpleChances;
        public readonly int ChanceHalvedAfterLevels;
        public readonly float ChanceGrowthPerLevel;

        internal PlatedRewardTuning(
            int ordinaryEveryLevels, float ordinaryChance, float atCompletion, int sureLevel, float sureAtCompletion,
            float purpleAtCompletion, IReadOnlyList<float> purpleChances, int chanceHalvedAfterLevels = 0,
            float chanceGrowthPerLevel = 0f)
        {
            OrdinaryEveryLevels = ordinaryEveryLevels;
            OrdinaryChance = ordinaryChance;
            AtCompletion = atCompletion;
            SureLevel = sureLevel;
            SureAtCompletion = sureAtCompletion;
            PurpleAtCompletion = purpleAtCompletion;
            PurpleChances = purpleChances;
            ChanceHalvedAfterLevels = chanceHalvedAfterLevels;
            ChanceGrowthPerLevel = chanceGrowthPerLevel;
        }

        internal static PlatedRewardTuning Golden(IRewardBalloonSettings settings)
        {
            return new PlatedRewardTuning(
                settings.OrdinaryGoldenEveryLevels, settings.OrdinaryGoldenChance, settings.GoldenAtCompletion,
                settings.SureGoldenLevel, settings.SureGoldenAtCompletion, settings.PurpleGoldenAtCompletion,
                settings.PurpleGoldenChances, settings.OrdinaryChanceHalvedAfterLevels,
                settings.OrdinaryChanceGrowthPerLevel);
        }

        internal static PlatedRewardTuning Disco(IRewardBalloonSettings settings)
        {
            return new PlatedRewardTuning(
                settings.OrdinaryDiscoEveryLevels, settings.OrdinaryDiscoChance, settings.DiscoAtCompletion,
                settings.SureDiscoLevel, settings.SureDiscoAtCompletion, settings.PurpleDiscoAtCompletion,
                settings.PurpleDiscoChances, settings.OrdinaryChanceHalvedAfterLevels,
                settings.OrdinaryChanceGrowthPerLevel);
        }
    }
}
