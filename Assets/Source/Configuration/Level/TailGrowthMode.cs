namespace BalloonParty.Configuration.Level
{
    /// <summary>How <see cref="LevelScoringCurve"/> extrapolates beyond its last control point.</summary>
    internal enum TailGrowthMode
    {
        /// <summary>Each level's increment grows by a fixed multiplier (geometric series).
        /// Rate 1.0 = flat (constant increment); rate 1.1 = 10 % harder each level.</summary>
        Geometric,

        /// <summary>Each level's increment grows by a fixed addend (arithmetic series).
        /// Rate 0 = flat; rate 50 = +50 cumulative per level beyond last CP.</summary>
        Linear,
    }
}
