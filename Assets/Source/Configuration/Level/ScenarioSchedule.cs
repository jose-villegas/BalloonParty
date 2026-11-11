namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     Pure, seeded answer to "is this level in the sky, on the ground or over water, and how far into that
    ///     stretch?". Sky until <see cref="FirstGroundLevel" />, rolled between
    ///     <see cref="ScenarioScheduleSettings.GroundFromLevel" /> and
    ///     <see cref="ScenarioScheduleSettings.GroundByLevel" />, then a ground stretch of a fixed
    ///     <see cref="ScenarioScheduleSettings.FirstGroundStretch" />, then sky and surface stretches alternate,
    ///     each lasting a rolled number of levels in its own range. The surface stretch right after the first
    ///     meadow is water when <see cref="ScenarioScheduleSettings.WaterAfterFirstMeadow" /> is on; every later
    ///     one is water or a meadow by its own roll (<see cref="ScenarioScheduleSettings.WaterShare" />), made
    ///     after its length, so water changes which surface a stretch shows but never where the stretches fall.
    ///     Every roll hashes
    ///     with the schedule's salt mixed with the run's seed, so each run lays out its own surfaces and sky,
    ///     and within a run the same level always gets the same answer, so a retry keeps it. A run seed of 0 is
    ///     the schedule with no run mixed in, for the editor preview and tests.
    /// </summary>
    internal static class ScenarioSchedule
    {
        // Hashed for the first ground level's roll. Stretch lengths hash their first level, always 1 or more.
        private const int FirstGroundRollKey = 0;

        // Mixed into the salt for a surface stretch's water roll, so it is independent of the stretch's length,
        // which hashes the same first level.
        private const int WaterRollSalt = 0x5A7E2;

        // The water roll's resolution: shares are compared in steps of 1/WaterRollSteps.
        private const int WaterRollSteps = 10000;

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
            var laterSurfaces = 0;
            while (level >= stretchStart + length)
            {
                stretchStart += length;
                var surfaceNext = kind == ScenarioKind.Sky;
                length = StretchLength(settings, surfaceNext ? ScenarioKind.Ground : ScenarioKind.Sky,
                    stretchStart, salt);
                if (!surfaceNext)
                {
                    kind = ScenarioKind.Sky;
                }
                else
                {
                    var firstAfterMeadow = laterSurfaces++ == 0;
                    kind = firstAfterMeadow && settings.WaterAfterFirstMeadow
                        ? ScenarioKind.Water
                        : SurfaceKind(settings, stretchStart, salt);
                }
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
        // Every surface stretch rolls in the ground range, water included, so the water roll moves no stretch.
        internal static int StretchLength(ScenarioScheduleSettings settings, ScenarioKind kind, int stretchStart,
            int salt)
        {
            var surface = kind != ScenarioKind.Sky;
            var min = surface ? settings.MinGroundStretch : settings.MinSkyStretch;
            var max = surface ? settings.MaxGroundStretch : settings.MaxSkyStretch;
            return min + LevelSeedHash.Hash(stretchStart, salt) % (max - min + 1);
        }

        // Whether the surface stretch starting at stretchStart (never the first meadow) is water or a meadow.
        internal static ScenarioKind SurfaceKind(ScenarioScheduleSettings settings, int stretchStart, int salt)
        {
            var roll = LevelSeedHash.Hash(stretchStart, salt ^ WaterRollSalt) % WaterRollSteps;
            return roll < settings.WaterShare * WaterRollSteps ? ScenarioKind.Water : ScenarioKind.Ground;
        }
    }
}
