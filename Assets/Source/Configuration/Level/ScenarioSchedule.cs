namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     Pure, seeded answer to "is this level in the sky or on the ground, and how far into that stretch?".
    ///     Sky until <see cref="ScenarioScheduleSettings.FirstGroundLevel" />, then a ground stretch of a fixed
    ///     <see cref="ScenarioScheduleSettings.FirstGroundStretch" />, then alternating sky and ground stretches,
    ///     each lasting a rolled number of levels in its own range. Every rolled length hashes the level its
    ///     stretch starts on, so the same level always gets the same answer and a retry keeps it.
    /// </summary>
    internal static class ScenarioSchedule
    {
        public static ScenarioKind ForLevel(ScenarioScheduleSettings settings, int level)
        {
            return StretchAt(settings, level).Kind;
        }

        public static ScenarioStretch StretchAt(ScenarioScheduleSettings settings, int level)
        {
            var stretchStart = settings.FirstGroundLevel;
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

        // The rolled length of a stretch after the first ground one.
        internal static int StretchLength(ScenarioScheduleSettings settings, ScenarioKind kind, int stretchStart)
        {
            var min = kind == ScenarioKind.Ground ? settings.MinGroundStretch : settings.MinSkyStretch;
            var max = kind == ScenarioKind.Ground ? settings.MaxGroundStretch : settings.MaxSkyStretch;
            return min + LevelSeedHash.Hash(stretchStart, settings.Salt) % (max - min + 1);
        }
    }
}
