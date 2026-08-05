using System;
using System.Collections.Generic;

namespace BalloonParty.Game.Telemetry
{
    // Timer-side counterpart to MetricCatalog, and the only home for TimerId's four wire names.
    // Without it the
    // serializer to hardcode four string literals into what are permanent, append-only wire names —
    // exactly what the counter catalog exists to prevent (R6, guardrail 14). Kept as its own static
    // table rather than folded into MetricCatalog: TimerId carries no FoldRule, Scope or MetricAxis —
    // Absorb never folds timers (every scope runs its own clocks; see MetricScope.Absorb) — so merging
    // the two tables would carry three unused columns on every timer row.
    internal static class TimerCatalog
    {
        private static readonly Entry[] Entries =
        {
            new(TimerId.Gameplay, "gameplay_seconds", "seconds"),
            new(TimerId.Ceremony, "ceremony_seconds", "seconds"),
            new(TimerId.Wall, "wall_seconds", "seconds"),
            new(TimerId.Hold, "hold_seconds", "seconds"),
        };

        private static readonly Dictionary<TimerId, Entry> ById;

        internal static readonly TimerId[] AllIds;

        static TimerCatalog()
        {
            var allTimerIds = (TimerId[])Enum.GetValues(typeof(TimerId));
            if (allTimerIds.Length != Entries.Length)
            {
                throw new InvalidOperationException(
                    $"TimerCatalog has {Entries.Length} rows but TimerId declares {allTimerIds.Length} " +
                    "members — every TimerId needs exactly one row.");
            }

            ById = BuildIndex();
            AllIds = BuildIds();
        }

        internal static string WireNameOf(TimerId id)
        {
            return ById[id].WireName;
        }

        internal static string UnitOf(TimerId id)
        {
            return ById[id].Unit;
        }

        private static Dictionary<TimerId, Entry> BuildIndex()
        {
            var map = new Dictionary<TimerId, Entry>(Entries.Length);
            foreach (var entry in Entries)
            {
                map.Add(entry.Id, entry);
            }

            return map;
        }

        // W2's serializer loops this rather than hardcoding four TimerId literals — the same
        // mechanical-loop discipline MetricCatalog.AllIds gives the counter side (R27).
        private static TimerId[] BuildIds()
        {
            var ids = new TimerId[Entries.Length];
            for (var i = 0; i < Entries.Length; i++)
            {
                ids[i] = Entries[i].Id;
            }

            return ids;
        }

        private readonly struct Entry
        {
            public readonly TimerId Id;
            public readonly string WireName;
            public readonly string Unit;

            public Entry(TimerId id, string wireName, string unit)
            {
                Id = id;
                WireName = wireName;
                Unit = unit;
            }
        }
    }
}
