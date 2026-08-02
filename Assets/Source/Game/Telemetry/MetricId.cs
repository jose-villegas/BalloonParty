namespace BalloonParty.Game.Telemetry
{
    // Append-only once shipped (R6, guardrail 14) — a wire name is an external contract the moment it
    // reaches the warehouse. See MetricCatalog for wire names, units and fold rules; see
    // PLAN-GameplayTelemetry.md's "The catalog" for the source table.
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
        HoldSpeedUpFlights,
        PointsBanked,
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
        MinHealth,
        MaxDangerLevel,
        BoardCleared,
        LevelsCompleted,
        RetriesUsed
    }
}
