using System;
using System.Collections.Generic;

namespace BalloonParty.Game.Telemetry
{
    // The single browsable table (R6): id -> wire name, unit, fold rule, dimension axes. Transcribed
    // verbatim from "The catalog" in PLAN-GameplayTelemetry.md — wire names are append-only once
    // shipped (guardrail 14), so this table is never reordered or edited in place, only extended.
    // Keyed by MetricId rather than indexed by ordinal so a row can never silently apply to the wrong
    // id if the enum and the table ever drift out of declaration order.
    internal static class MetricCatalog
    {
        private static readonly MetricAxis[] NoAxes = Array.Empty<MetricAxis>();
        private static readonly MetricAxis[] ColorAxisOnly = { MetricAxis.Color };
        private static readonly MetricAxis[] BalloonTypeAxisOnly = { MetricAxis.BalloonType };
        private static readonly MetricAxis[] ItemTypeAxisOnly = { MetricAxis.ItemType };
        private static readonly MetricAxis[] ColorAndBalloonTypeAxes = { MetricAxis.Color, MetricAxis.BalloonType };

        private static readonly Entry[] Entries =
        {
            new(MetricId.ShotsFired, "shots_fired", "count", FoldRule.Sum, NoAxes),
            new(MetricId.FlightsStarted, "flights_started", "count", FoldRule.Sum, NoAxes),
            new(MetricId.Pops, "pops", "count", FoldRule.Sum, ColorAndBalloonTypeAxes),
            new(MetricId.DirectHitPops, "direct_hit_pops", "count", FoldRule.Sum, NoAxes),
            new(MetricId.Deflects, "deflects", "count", FoldRule.Sum, BalloonTypeAxisOnly),
            new(MetricId.Absorbs, "absorbs", "count", FoldRule.Sum, NoAxes),
            new(MetricId.WallBounces, "wall_bounces", "count", FoldRule.Sum, NoAxes),
            new(MetricId.PierceDischarges, "pierce_discharges", "count", FoldRule.Sum, NoAxes),
            new(MetricId.PierceToughsCleared, "pierce_toughs_cleared", "count", FoldRule.Sum, NoAxes),
            new(MetricId.RainbowPierceDischarges, "rainbow_pierce_discharges", "count", FoldRule.Sum, NoAxes),
            new(MetricId.SpeedTapsMinted, "speed_taps_minted", "count", FoldRule.Sum, NoAxes),
            new(MetricId.MaxWallBouncesInFlight, "max_wall_bounces_in_flight", "count", FoldRule.Max, NoAxes),
            new(MetricId.MaxSpeedTapsInFlight, "max_speed_taps_in_flight", "count", FoldRule.Max, NoAxes),
            new(MetricId.HoldSpeedUpFlights, "hold_speed_up_flights", "count", FoldRule.Sum, NoAxes),
            new(MetricId.PointsBanked, "points_banked", "points", FoldRule.Sum, ColorAxisOnly),
            new(MetricId.PointsProjected, "points_projected", "points", FoldRule.Last, NoAxes),
            new(MetricId.MaxMultiplier, "max_multiplier", "multiplier", FoldRule.Max, NoAxes),
            new(MetricId.MaxStreak, "max_streak", "count", FoldRule.Max, NoAxes),
            new(MetricId.StreakBreaks, "streak_breaks", "count", FoldRule.Sum, NoAxes),
            new(MetricId.HeartsLost, "hearts_lost", "hearts", FoldRule.Sum, NoAxes),
            new(MetricId.MaxHeartsLostInWave, "max_hearts_lost_in_wave", "hearts", FoldRule.Max, NoAxes),
            new(MetricId.BlockedSlots, "blocked_slots", "count", FoldRule.Sum, NoAxes),
            new(MetricId.Strikethroughs, "strikethroughs", "count", FoldRule.Sum, NoAxes),
            new(MetricId.ShieldsGained, "shields_gained", "count", FoldRule.Sum, NoAxes),
            new(MetricId.ShieldsSpent, "shields_spent", "count", FoldRule.Sum, NoAxes),
            new(MetricId.ItemsActivated, "items_activated", "count", FoldRule.Sum, ItemTypeAxisOnly),
            new(MetricId.MinHealth, "min_health", "hearts", FoldRule.Min, NoAxes),
            new(MetricId.MaxDangerLevel, "max_danger_level", "level", FoldRule.Max, NoAxes),
            new(MetricId.BoardCleared, "board_cleared", "count", FoldRule.Max, NoAxes),
            new(MetricId.LevelsCompleted, "levels_completed", "count", FoldRule.Sum, NoAxes),
            new(MetricId.RetriesUsed, "retries_used", "count", FoldRule.Last, NoAxes),
        };

        private static readonly Dictionary<MetricId, Entry> ById = BuildIndex();

        internal static readonly IReadOnlyList<MetricId> AllIds = BuildIds();

        internal static string WireNameOf(MetricId id)
        {
            return ById[id].WireName;
        }

        internal static string UnitOf(MetricId id)
        {
            return ById[id].Unit;
        }

        internal static FoldRule FoldOf(MetricId id)
        {
            return ById[id].Fold;
        }

        internal static IReadOnlyList<MetricAxis> AxesOf(MetricId id)
        {
            return ById[id].Axes;
        }

        private static Dictionary<MetricId, Entry> BuildIndex()
        {
            var map = new Dictionary<MetricId, Entry>(Entries.Length);
            foreach (var entry in Entries)
            {
                map.Add(entry.Id, entry);
            }

            return map;
        }

        private static MetricId[] BuildIds()
        {
            var ids = new MetricId[Entries.Length];
            for (var i = 0; i < Entries.Length; i++)
            {
                ids[i] = Entries[i].Id;
            }

            return ids;
        }

        private readonly struct Entry
        {
            public readonly MetricId Id;
            public readonly string WireName;
            public readonly string Unit;
            public readonly FoldRule Fold;
            public readonly MetricAxis[] Axes;

            public Entry(MetricId id, string wireName, string unit, FoldRule fold, MetricAxis[] axes)
            {
                Id = id;
                WireName = wireName;
                Unit = unit;
                Fold = fold;
                Axes = axes;
            }
        }
    }
}
