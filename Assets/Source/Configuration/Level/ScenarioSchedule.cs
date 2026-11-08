namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     Pure, seeded answer to "is this level in the sky or on the ground?". Sky until
    ///     <see cref="ScenarioScheduleSettings.FirstGroundLevel" />, then alternating stretches of ground and
    ///     sky, each lasting a rolled number of levels. Every stretch length hashes the level it starts on, so
    ///     the same level always gets the same scenario and a retry keeps it.
    /// </summary>
    internal static class ScenarioSchedule
    {
        public static ScenarioKind ForLevel(ScenarioScheduleSettings settings, int level)
        {
            var stretchStart = settings.FirstGroundLevel;
            if (level < stretchStart)
            {
                return ScenarioKind.Sky;
            }

            // Walks the stretches from the first ground level up to this one: a few dozen steps at most for any
            // level a run reaches, and no state to keep in step with the run.
            var kind = ScenarioKind.Ground;
            while (true)
            {
                var stretchEnd = stretchStart + StretchLength(settings, stretchStart);
                if (level < stretchEnd)
                {
                    return kind;
                }

                stretchStart = stretchEnd;
                kind = kind == ScenarioKind.Ground ? ScenarioKind.Sky : ScenarioKind.Ground;
            }
        }

        internal static int StretchLength(ScenarioScheduleSettings settings, int stretchStart)
        {
            var span = settings.MaxStretch - settings.MinStretch + 1;
            return settings.MinStretch + LevelSeedHash.Hash(stretchStart, settings.Salt) % span;
        }
    }
}
