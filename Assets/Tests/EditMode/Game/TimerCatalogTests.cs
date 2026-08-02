using System;
using System.Collections.Generic;
using BalloonParty.Game.Telemetry;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class TimerCatalogTests
    {
        [Test]
        public void EveryTimerId_HasAWireName()
        {
            foreach (TimerId id in Enum.GetValues(typeof(TimerId)))
            {
                var wireName = TimerCatalog.WireNameOf(id);
                Assert.IsFalse(string.IsNullOrEmpty(wireName), $"{id} has no wire name");
            }
        }

        [Test]
        public void EveryTimerId_HasAUnit()
        {
            foreach (TimerId id in Enum.GetValues(typeof(TimerId)))
            {
                var unit = TimerCatalog.UnitOf(id);
                Assert.IsFalse(string.IsNullOrEmpty(unit), $"{id} has no unit");
            }
        }

        [Test]
        public void WireNames_AreAllUnique()
        {
            var seen = new HashSet<string>();
            foreach (TimerId id in Enum.GetValues(typeof(TimerId)))
            {
                var wireName = TimerCatalog.WireNameOf(id);
                Assert.IsTrue(seen.Add(wireName), $"Duplicate wire name '{wireName}' for {id}");
            }
        }

        // The actual golden table, row for row against PLAN-GameplayTelemetry.md's "Timers" table
        // (#### Timers — `TimerId`), transcribed from the plan, not from TimerCatalog.cs, or this test
        // proves nothing. See MetricCatalogFoldTests.CatalogRow_MatchesThePlanTable for the same pattern
        // applied to counters.
        [TestCaseSource(nameof(GoldenRows))]
        public void CatalogRow_MatchesThePlanTable(GoldenRow row)
        {
            var id = (TimerId)Enum.Parse(typeof(TimerId), row.TimerIdName);

            Assert.AreEqual(row.WireName, TimerCatalog.WireNameOf(id), $"{row.TimerIdName} wire name");
            Assert.AreEqual(row.Unit, TimerCatalog.UnitOf(id), $"{row.TimerIdName} unit");
        }

        private static IEnumerable<GoldenRow> GoldenRows()
        {
            yield return new GoldenRow(nameof(TimerId.Gameplay), "gameplay_seconds", "seconds");
            yield return new GoldenRow(nameof(TimerId.Ceremony), "ceremony_seconds", "seconds");
            yield return new GoldenRow(nameof(TimerId.Wall), "wall_seconds", "seconds");
            yield return new GoldenRow(nameof(TimerId.Hold), "hold_seconds", "seconds");
        }

        public readonly struct GoldenRow
        {
            public readonly string TimerIdName;
            public readonly string WireName;
            public readonly string Unit;

            public GoldenRow(string timerIdName, string wireName, string unit)
            {
                TimerIdName = timerIdName;
                WireName = wireName;
                Unit = unit;
            }

            public override string ToString()
            {
                return TimerIdName;
            }
        }
    }
}
