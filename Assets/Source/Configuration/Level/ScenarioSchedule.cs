namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     Pure, seeded answer to "is this level in the sky or on the ground, and how far into that stretch?".
    ///     Sky until <see cref="FirstGroundLevel" />, rolled between
    ///     <see cref="ScenarioScheduleSettings.GroundFromLevel" /> and
    ///     <see cref="ScenarioScheduleSettings.GroundByLevel" />, then a ground stretch of a fixed
    ///     <see cref="ScenarioScheduleSettings.FirstGroundStretch" />, then alternating sky and ground stretches,
    ///     each lasting a rolled number of levels in its own range. Every roll hashes with the schedule's salt,
    ///     so the same level always gets the same answer and a retry keeps it.
    /// </summary>
    internal static class ScenarioSchedule
    {
        // Hashed for the first ground level's roll. Stretch lengths hash their first level, always 1 or more.
        private const int FirstGroundRollKey = 0;

        public static ScenarioKind ForLevel(ScenarioScheduleSettings settings, int level)
        {
            return StretchAt(settings, level).Kind;
        }

        public static ScenarioStretch StretchAt(ScenarioScheduleSettings settings, int level)
        {
            var stretchStart = FirstGroundLevel(settings);
            if (level < stretchStart)
            {
                // The opening sky runs from level 1; debug presets (level < 1) count as its first level.
                return new ScenarioStretch(ScenarioKind.Sky, level > 1 ? level - 1 : 0,
                    stretchStart > 1 ? stretchStart - 1 : 1);
            }

            var kind = ScenarioKind.Ground;
            var length = settings.FirstGroundStretch;
            while (level >= stretchStart + length)
            {
                stretchStart += length;
                kind = kind == ScenarioKind.Ground ? ScenarioKind.Sky : ScenarioKind.Ground;
                length = StretchLength(settings, kind, stretchStart);
            }

            return new ScenarioStretch(kind, level - stretchStart, length);
        }

        /// <summary>The level the first ground stretch starts on: the opening sky's last level plus one.</summary>
        public static int FirstGroundLevel(ScenarioScheduleSettings settings)
        {
            var from = settings.GroundFromLevel;
            var span = settings.GroundByLevel - from + 1;
            return from + LevelSeedHash.Hash(FirstGroundRollKey, settings.Salt) % span;
        }

        // The rolled length of a stretch after the first ground one.
        internal static int StretchLength(ScenarioScheduleSettings settings, ScenarioKind kind, int stretchStart)
        {
            var min = kind == ScenarioKind.Ground ? settings.MinGroundStretch : settings.MinSkyStretch;
            var max = kind == ScenarioKind.Ground ? settings.MaxGroundStretch : settings.MaxSkyStretch;
            return min + LevelSeedHash.Hash(stretchStart, settings.Salt) % (max - min + 1);
        }
    }
}
