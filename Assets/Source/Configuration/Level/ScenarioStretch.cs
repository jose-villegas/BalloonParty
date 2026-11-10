namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     Where a level sits in the run's scenario schedule: the stretch's scenario, the level's place in it
    ///     (0 = the stretch's first level) and how many levels the stretch lasts.
    /// </summary>
    internal readonly struct ScenarioStretch
    {
        public readonly ScenarioKind Kind;
        public readonly int Position;
        public readonly int Length;

        /// <summary>
        ///     The share of its puff-cloud count a level keeps. A meadow arrives under clouds and clears halfway:
        ///     its first ⌊n/2⌋ levels (at least one) have puff clouds, the last of them at half the count, and the
        ///     rest are clear. Sky levels keep the full count; water levels have none (the water surface has no
        ///     clouds).
        /// </summary>
        public float PuffCloudShare
        {
            get
            {
                if (Kind == ScenarioKind.Water)
                {
                    return 0f;
                }

                if (Kind != ScenarioKind.Ground)
                {
                    return 1f;
                }

                var cloudyLevels = Length / 2 > 1 ? Length / 2 : 1;
                if (Position < cloudyLevels - 1)
                {
                    return 1f;
                }

                return Position == cloudyLevels - 1 ? 0.5f : 0f;
            }
        }

        /// <summary>
        ///     The share of its bush count a level keeps: the meadow trades its puff clouds for bushes, so bushes
        ///     start at half under full clouds and reach their full count once the clouds are gone. Sky levels keep
        ///     the full count (the generator leaves bushes out of the sky anyway); water levels have none.
        /// </summary>
        public float BushShare => Kind switch
        {
            ScenarioKind.Ground => 1f - PuffCloudShare * 0.5f,
            ScenarioKind.Water => 0f,
            _ => 1f,
        };

        /// <summary>Whether this is a meadow level that has started trading its puff clouds for bushes.</summary>
        public bool IsClearingMeadow => Kind == ScenarioKind.Ground && PuffCloudShare < 1f;

        public ScenarioStretch(ScenarioKind kind, int position, int length)
        {
            Kind = kind;
            Position = position;
            Length = length;
        }
    }
}
