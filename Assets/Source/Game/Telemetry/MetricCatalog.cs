using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Game.Telemetry
{
    // The single browsable table (R6): id -> wire name, unit, fold rule, scope, dimension axes.
    // Transcribed verbatim from "The catalog" in PLAN-GameplayTelemetry.md — wire names are
    // append-only once shipped (guardrail 14), so this table is never reordered or edited in place,
    // only extended. Keyed by MetricId rather than indexed by ordinal so a row can never silently
    // apply to the wrong id if the enum and the table ever drift out of declaration order.
    internal static class MetricCatalog
    {
        private static readonly MetricAxis[] NoAxes = Array.Empty<MetricAxis>();
        private static readonly MetricAxis[] ColorAxisOnly = { MetricAxis.Color };
        private static readonly MetricAxis[] BalloonTypeAxisOnly = { MetricAxis.BalloonType };
        private static readonly MetricAxis[] ItemTypeAxisOnly = { MetricAxis.ItemType };
        private static readonly MetricAxis[] ColorAndBalloonTypeAxes = { MetricAxis.Color, MetricAxis.BalloonType };

        private static readonly int BalloonTypeAxisSize = Enum.GetValues(typeof(BalloonType)).Length;
        private static readonly int ItemTypeAxisSize = Enum.GetValues(typeof(ItemType)).Length;

        private static readonly Entry[] Entries =
        {
            new(MetricId.ShotsFired, "shots_fired", "count", FoldRule.Sum, MetricScopeKind.Level, NoAxes),
            new(MetricId.FlightsStarted, "flights_started", "count", FoldRule.Sum, MetricScopeKind.Level, NoAxes),
            new(MetricId.Pops, "pops", "count", FoldRule.Sum, MetricScopeKind.Flight, ColorAndBalloonTypeAxes),
            new(MetricId.DirectHitPops, "direct_hit_pops", "count", FoldRule.Sum, MetricScopeKind.Flight, NoAxes),
            new(MetricId.Deflects, "deflects", "count", FoldRule.Sum, MetricScopeKind.Flight, BalloonTypeAxisOnly),
            new(MetricId.Absorbs, "absorbs", "count", FoldRule.Sum, MetricScopeKind.Flight, NoAxes),
            new(MetricId.WallBounces, "wall_bounces", "count", FoldRule.Sum, MetricScopeKind.Flight, NoAxes),
            new(MetricId.PierceDischarges, "pierce_discharges", "count", FoldRule.Sum, MetricScopeKind.Flight, NoAxes),
            new(MetricId.PierceToughsCleared, "pierce_toughs_cleared", "count", FoldRule.Sum, MetricScopeKind.Flight, NoAxes),
            new(MetricId.RainbowPierceDischarges, "rainbow_pierce_discharges", "count", FoldRule.Sum, MetricScopeKind.Flight, NoAxes),
            new(MetricId.SpeedTapsMinted, "speed_taps_minted", "count", FoldRule.Sum, MetricScopeKind.Flight, NoAxes),
            new(MetricId.MaxWallBouncesInFlight, "max_wall_bounces_in_flight", "count", FoldRule.Max, MetricScopeKind.Level, NoAxes),
            new(MetricId.MaxSpeedTapsInFlight, "max_speed_taps_in_flight", "count", FoldRule.Max, MetricScopeKind.Level, NoAxes),
            new(MetricId.HoldSpeedUpFlights, "hold_speed_up_flights", "count", FoldRule.Sum, MetricScopeKind.Level, NoAxes),
            new(MetricId.PointsBanked, "points_banked", "points", FoldRule.Sum, MetricScopeKind.Level, ColorAxisOnly),
            new(MetricId.PointsProjected, "points_projected", "points", FoldRule.Last, MetricScopeKind.Level, NoAxes),
            new(MetricId.MaxMultiplier, "max_multiplier", "multiplier", FoldRule.Max, MetricScopeKind.Level, NoAxes),
            new(MetricId.MaxStreak, "max_streak", "count", FoldRule.Max, MetricScopeKind.Level, NoAxes),
            new(MetricId.StreakBreaks, "streak_breaks", "count", FoldRule.Sum, MetricScopeKind.Level, NoAxes),
            new(MetricId.HeartsLost, "hearts_lost", "hearts", FoldRule.Sum, MetricScopeKind.Level, NoAxes),
            new(MetricId.MaxHeartsLostInWave, "max_hearts_lost_in_wave", "hearts", FoldRule.Max, MetricScopeKind.Level, NoAxes),
            new(MetricId.BlockedSlots, "blocked_slots", "count", FoldRule.Sum, MetricScopeKind.Level, NoAxes),
            new(MetricId.Strikethroughs, "strikethroughs", "count", FoldRule.Sum, MetricScopeKind.Level, NoAxes),
            new(MetricId.ShieldsGained, "shields_gained", "count", FoldRule.Sum, MetricScopeKind.Level, NoAxes),
            new(MetricId.ShieldsSpent, "shields_spent", "count", FoldRule.Sum, MetricScopeKind.Level, NoAxes),
            new(MetricId.ItemsActivated, "items_activated", "count", FoldRule.Sum, MetricScopeKind.Level, ItemTypeAxisOnly),
            new(MetricId.MaxDangerLevel, "max_danger_level", "level_hundredths", FoldRule.Max, MetricScopeKind.Level, NoAxes),
            new(MetricId.BoardCleared, "board_cleared", "count", FoldRule.Max, MetricScopeKind.Level, NoAxes),
            new(MetricId.LevelsCompleted, "levels_completed", "count", FoldRule.Sum, MetricScopeKind.Run, NoAxes),
            new(MetricId.RetriesUsed, "retries_used", "count", FoldRule.Last, MetricScopeKind.Run, NoAxes),
            new(MetricId.TotalScore, "total_score", "points", FoldRule.Last, MetricScopeKind.Run, NoAxes),
        };

        private static readonly Dictionary<MetricId, Entry> ById;
        private static readonly FoldRule[] FoldByOrdinal;
        private static readonly int[,] SlotIndexByIdAndAxis;
        private static readonly AxisSlotInfo[] Slots;

        internal static readonly MetricId[] AllIds;

        internal static IReadOnlyList<AxisSlotInfo> AllSlots => Slots;

        static MetricCatalog()
        {
            var allMetricIds = (MetricId[])Enum.GetValues(typeof(MetricId));
            if (allMetricIds.Length != Entries.Length)
            {
                throw new InvalidOperationException(
                    $"MetricCatalog has {Entries.Length} rows but MetricId declares {allMetricIds.Length} " +
                    "members — every MetricId needs exactly one row.");
            }

            ById = BuildIndex();
            AllIds = BuildIds();
            FoldByOrdinal = BuildFoldByOrdinal();
            (SlotIndexByIdAndAxis, Slots) = BuildSlots();

            // Neither enum is owned by this feature (R12/R5) — the axis arrays are sized from their
            // length and indexed by ordinal, so a future explicit re-numbering elsewhere in the
            // codebase would silently under-size a table here instead of failing loudly.
            AssertContiguousFromZero(Enum.GetValues(typeof(BalloonType)), nameof(BalloonType));
            AssertContiguousFromZero(Enum.GetValues(typeof(ItemType)), nameof(ItemType));

            // MetricAxis is indexed the same way (SlotIndexByIdAndAxis, the axisCount-wide dimension in
            // BuildSlots) — an out-of-order or gapped MetricAxis would silently under-size that table
            // too.
            AssertContiguousFromZero(Enum.GetValues(typeof(MetricAxis)), nameof(MetricAxis));

            // MetricScope.Absorb's whole fold gate — "is the child exactly one scope below me" — is an
            // ordinal comparison over MetricScopeKind. Nothing else pins Flight < Level < Run < Session;
            // reordering the enum would silently invert or break that gate.
            AssertScopeOrdering();
        }

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
            return FoldByOrdinal[(int)id];
        }

        internal static MetricScopeKind ScopeOf(MetricId id)
        {
            return ById[id].Scope;
        }

        internal static IReadOnlyList<MetricAxis> AxesOf(MetricId id)
        {
            return ById[id].Axes;
        }

        internal static AxisSlot SlotOf(MetricId id, MetricAxis axis)
        {
            var index = SlotIndexByIdAndAxis[(int)id, (int)axis];
            if (index < 0)
            {
                throw new ArgumentException($"{id} does not declare a {axis} axis.");
            }

            return new AxisSlot(index);
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

        private static FoldRule[] BuildFoldByOrdinal()
        {
            var byOrdinal = new FoldRule[Entries.Length];
            foreach (var entry in Entries)
            {
                byOrdinal[(int)entry.Id] = entry.Fold;
            }

            return byOrdinal;
        }

        // Dense (MetricId, MetricAxis) -> AxisSlot lookup as a 2D array rather than a Dictionary, so
        // the allocation-free increment path (MetricSet.IncrementAxis, R29) never pays a hash lookup.
        // -1 marks a pair the catalog does not declare.
        private static (int[,] SlotIndexByIdAndAxis, AxisSlotInfo[] Slots) BuildSlots()
        {
            var axisCount = Enum.GetValues(typeof(MetricAxis)).Length;
            var slotIndexByIdAndAxis = new int[Entries.Length, axisCount];
            for (var i = 0; i < Entries.Length; i++)
            {
                for (var a = 0; a < axisCount; a++)
                {
                    slotIndexByIdAndAxis[i, a] = -1;
                }
            }

            var slots = new List<AxisSlotInfo>();
            foreach (var entry in Entries)
            {
                foreach (var axis in entry.Axes)
                {
                    var bucketCount = axis switch
                    {
                        MetricAxis.Color => AxisSlotInfo.RuntimeSuppliedBucketCount,
                        MetricAxis.BalloonType => BalloonTypeAxisSize,
                        MetricAxis.ItemType => ItemTypeAxisSize,
                        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
                    };

                    var slotIndex = slots.Count;
                    slots.Add(new AxisSlotInfo(new AxisSlot(slotIndex), entry.Id, axis, bucketCount));
                    slotIndexByIdAndAxis[(int)entry.Id, (int)axis] = slotIndex;
                }
            }

            return (slotIndexByIdAndAxis, slots.ToArray());
        }

        private static void AssertContiguousFromZero(Array enumValues, string enumName)
        {
            var max = -1;
            foreach (int value in enumValues)
            {
                if (value > max)
                {
                    max = value;
                }
            }

            if (max != enumValues.Length - 1)
            {
                throw new InvalidOperationException(
                    $"{enumName} must be contiguous from 0 (max value {max}, {enumValues.Length} members).");
            }
        }

        private static void AssertScopeOrdering()
        {
            if ((int)MetricScopeKind.Flight != 0
                || (int)MetricScopeKind.Level != 1
                || (int)MetricScopeKind.Run != 2
                || (int)MetricScopeKind.Session != 3)
            {
                throw new InvalidOperationException(
                    "MetricScopeKind must stay ordered Flight < Level < Run < Session — " +
                    "MetricScope.Absorb's adjacency check and its per-metric Scope gate both depend on " +
                    "the ordinal comparison.");
            }
        }

        private readonly struct Entry
        {
            public readonly MetricId Id;
            public readonly string WireName;
            public readonly string Unit;
            public readonly FoldRule Fold;
            public readonly MetricScopeKind Scope;
            public readonly MetricAxis[] Axes;

            public Entry(MetricId id, string wireName, string unit, FoldRule fold, MetricScopeKind scope, MetricAxis[] axes)
            {
                Id = id;
                WireName = wireName;
                Unit = unit;
                Fold = fold;
                Scope = scope;
                Axes = axes;
            }
        }
    }
}
