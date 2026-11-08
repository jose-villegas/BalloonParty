namespace BalloonParty.Game.Telemetry
{
    // Append-only once shipped (R6, guardrail 14) — a wire name is an external contract the moment it
    // reaches the warehouse. See MetricCatalog for wire names, units and fold rules —
    // it is the authoritative table.
    internal enum MetricId
    {
        ShotsFired,
        FlightsStarted,
        Pops,
        DirectHitPops,
        Deflects,
        Absorbs,
        WallBounces,
        PierceDischarges,
        PierceToughsCleared,
        RainbowPierceDischarges,
        SpeedTapsMinted,
        MaxWallBouncesInFlight,
        MaxSpeedTapsInFlight,

        // 13 was hold_speed_up_flights (hold-to-speed-up is gone). MetricBinding serializes a MetricId
        // as its int in prefabs, so the members after it keep their numbers and 13 stays unused.
        PointsBanked = 14,
        PointsProjected,
        MaxMultiplier,
        MaxStreak,
        StreakBreaks,
        HeartsLost,
        MaxHeartsLostInWave,
        BlockedSlots,
        Strikethroughs,
        ShieldsGained,
        ShieldsSpent,
        ItemsActivated,
        MaxDangerLevel,
        BoardCleared,
        LevelsCompleted,
        RetriesUsed,
        TotalScore,
        CoinsEarned
    }
}
