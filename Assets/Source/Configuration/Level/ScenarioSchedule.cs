namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     Pure, seeded answer to "is this level in the sky or on the ground, and how far into that stretch?".
    ///     Sky until <see cref="FirstGroundLevel" />, rolled between
    ///     <see cref="ScenarioScheduleSettings.GroundFromLevel" /> and
    ///     <see cref="ScenarioScheduleSettings.GroundByLevel" />, then a ground stretch of a fixed
    ///     <see cref="ScenarioScheduleSettings.FirstGroundStretch" />, then alternating sky and ground stretches,
    ///     each lasting a rolled number of levels in its own range. Every roll hashes with the schedule's salt
    ///     mixed with the run's seed, so each run lays out its own ground and sky, and within a run the same
    ///     level always gets the same answer, so a retry keeps it. A run seed of 0 is the schedule with no run
    ///     mixed in, for the editor preview and tests.
    /// </summary>
    internal static class ScenarioSchedule
    {
        // Hashed for the first ground level's roll. Stretch lengths hash their first level, always 1 or more.
        private const int FirstGroundRollKey = 0;

        public static ScenarioKind ForLevel(ScenarioScheduleSettings settings, int level, int runSeed = 0)
        {
            return StretchAt(settings, level, runSeed).Kind;
        }

        public static ScenarioStretch StretchAt(ScenarioScheduleSettings settings, int level, int runSeed = 0)
        {
            var salt = settings.Salt ^ runSeed;
            var stretchStart = FirstGroundLevel(settings, runSeed);
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
                length = StretchLength(settings, kind, stretchStart, salt);
            }

            return new ScenarioStretch(kind, level - stretchStart, length);
        }

        /// <summary>The level the first ground stretch starts on: the opening sky's last level plus one.</summary>
        public static int FirstGroundLevel(ScenarioScheduleSettings settings, int runSeed = 0)
        {
            var from = settings.GroundFromLevel;
            var span = settings.GroundByLevel - from + 1;
            return from + LevelSeedHash.Hash(FirstGroundRollKey, settings.Salt ^ runSeed) % span;
        }

        // The rolled length of a stretch after the first ground one; salt is the schedule's, mixed with the run's.
        internal static int StretchLength(ScenarioScheduleSettings settings, ScenarioKind kind, int stretchStart,
            int salt)
        {
            var min = kind == ScenarioKind.Ground ? settings.MinGroundStretch : settings.MinSkyStretch;
            var max = kind == ScenarioKind.Ground ? settings.MaxGroundStretch : settings.MaxSkyStretch;
            return min + LevelSeedHash.Hash(stretchStart, salt) % (max - min + 1);
        }
    }
}
