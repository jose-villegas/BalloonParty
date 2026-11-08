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
        ///     rest are clear. Sky levels keep the full count.
        /// </summary>
        public float PuffCloudShare
        {
            get
            {
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

        public ScenarioStretch(ScenarioKind kind, int position, int length)
        {
            Kind = kind;
            Position = position;
            Length = length;
        }
    }
}
