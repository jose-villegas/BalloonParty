using System;
using System.Collections.Generic;
using BalloonParty.Game.Telemetry;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class MetricCatalogFoldTests
    {
        [Test]
        public void EveryMetricId_HasAFoldRule()
        {
            foreach (MetricId id in Enum.GetValues(typeof(MetricId)))
            {
                var current = id;
                Assert.DoesNotThrow(() => MetricCatalog.FoldOf(current), $"{id} has no catalog row");
            }
        }

        [Test]
        public void EveryMetricId_HasAWireName()
        {
            foreach (MetricId id in Enum.GetValues(typeof(MetricId)))
            {
                var wireName = MetricCatalog.WireNameOf(id);
                Assert.IsFalse(string.IsNullOrEmpty(wireName), $"{id} has no wire name");
            }
        }

        [Test]
        public void EveryMetricId_HasAUnit()
        {
            foreach (MetricId id in Enum.GetValues(typeof(MetricId)))
            {
                var unit = MetricCatalog.UnitOf(id);
                Assert.IsFalse(string.IsNullOrEmpty(unit), $"{id} has no unit");
            }
        }

        [Test]
        public void WireNames_AreAllUnique()
        {
            var seen = new HashSet<string>();
            foreach (MetricId id in Enum.GetValues(typeof(MetricId)))
            {
                var wireName = MetricCatalog.WireNameOf(id);
                Assert.IsTrue(seen.Add(wireName), $"Duplicate wire name '{wireName}' for {id}");
            }
        }

        [Test]
        public void Pops_DeclaresBothColorAndBalloonTypeAxes()
        {
            var axes = MetricCatalog.AxesOf(MetricId.Pops);

            CollectionAssert.Contains(axes, MetricAxis.Color);
            CollectionAssert.Contains(axes, MetricAxis.BalloonType);
        }

        [Test]
        public void ItemsActivated_DeclaresTheItemTypeAxis()
        {
            var axes = MetricCatalog.AxesOf(MetricId.ItemsActivated);

            CollectionAssert.Contains(axes, MetricAxis.ItemType);
        }

        [Test]
        public void ShotsFired_HasNoAxis()
        {
            var axes = MetricCatalog.AxesOf(MetricId.ShotsFired);

            Assert.AreEqual(0, axes.Count);
        }

        [Test]
        public void MaxStreak_FoldsAsMax()
        {
            Assert.AreEqual(FoldRule.Max, MetricCatalog.FoldOf(MetricId.MaxStreak));
        }

        [Test]
        public void PointsProjected_FoldsAsLast()
        {
            Assert.AreEqual(FoldRule.Last, MetricCatalog.FoldOf(MetricId.PointsProjected));
        }

        // SlotOf on an undeclared (id, axis) pair used to return an AxisSlot with Index == -1, which
        // surfaced later as a bare IndexOutOfRangeException naming neither the metric nor the axis.
        [Test]
        public void SlotOf_AnUndeclaredPair_ThrowsNamingTheMetricAndTheAxis()
        {
            var ex = Assert.Throws<ArgumentException>(() => MetricCatalog.SlotOf(MetricId.ShotsFired, MetricAxis.Color));

            StringAssert.Contains(nameof(MetricId.ShotsFired), ex.Message);
            StringAssert.Contains(nameof(MetricAxis.Color), ex.Message);
        }

        // The presence/uniqueness checks above pass on a mistranscribed wire name, unit, fold rule or
        // scope as long as it is non-empty and non-duplicate — this is the actual golden table, row for
        // row against PLAN-GameplayTelemetry.md's "The catalog" (transcribed from the plan, not from
        // MetricCatalog.cs, or this test proves nothing). MinHealth/Min is dropped deliberately per the
        // plan's "Dropped deliberately" note and is not in this table.
        //
        // GoldenRow holds enum member names as strings, not the enums themselves: MetricId/FoldRule/
        // MetricScopeKind/MetricAxis are internal (CLAUDE.md's "every type internal"), and a public
        // TestCaseSource-driven test method cannot declare a parameter of an internal type (CS0051).
        // The nameof() calls below are still compile-time checked against the real enums.
        [TestCaseSource(nameof(GoldenRows))]
        public void CatalogRow_MatchesThePlanTable(GoldenRow row)
        {
            var id = (MetricId)Enum.Parse(typeof(MetricId), row.MetricIdName);
            var fold = (FoldRule)Enum.Parse(typeof(FoldRule), row.FoldName);
            var scope = (MetricScopeKind)Enum.Parse(typeof(MetricScopeKind), row.ScopeName);
            var axes = Array.ConvertAll(row.AxisNames, name => (MetricAxis)Enum.Parse(typeof(MetricAxis), name));

            Assert.AreEqual(row.WireName, MetricCatalog.WireNameOf(id), $"{row.MetricIdName} wire name");
            Assert.AreEqual(row.Unit, MetricCatalog.UnitOf(id), $"{row.MetricIdName} unit");
            Assert.AreEqual(fold, MetricCatalog.FoldOf(id), $"{row.MetricIdName} fold rule");
            Assert.AreEqual(scope, MetricCatalog.ScopeOf(id), $"{row.MetricIdName} scope");
            CollectionAssert.AreEquivalent(axes, MetricCatalog.AxesOf(id), $"{row.MetricIdName} axes");
        }

        private static IEnumerable<GoldenRow> GoldenRows()
        {
            yield return new GoldenRow(nameof(MetricId.ShotsFired), "shots_fired", "count", nameof(FoldRule.Sum), nameof(MetricScopeKind.Level));
            yield return new GoldenRow(nameof(MetricId.FlightsStarted), "flights_started", "count", nameof(FoldRule.Sum), nameof(MetricScopeKind.Level));
            yield return new GoldenRow(nameof(MetricId.Pops), "pops", "count", nameof(FoldRule.Sum), nameof(MetricScopeKind.Flight), nameof(MetricAxis.Color), nameof(MetricAxis.BalloonType));
            yield return new GoldenRow(nameof(MetricId.DirectHitPops), "direct_hit_pops", "count", nameof(FoldRule.Sum), nameof(MetricScopeKind.Flight));
            yield return new GoldenRow(nameof(MetricId.Deflects), "deflects", "count", nameof(FoldRule.Sum), nameof(MetricScopeKind.Flight), nameof(MetricAxis.BalloonType));
            yield return new GoldenRow(nameof(MetricId.Absorbs), "absorbs", "count", nameof(FoldRule.Sum), nameof(MetricScopeKind.Flight));
            yield return new GoldenRow(nameof(MetricId.WallBounces), "wall_bounces", "count", nameof(FoldRule.Sum), nameof(MetricScopeKind.Flight));
            yield return new GoldenRow(nameof(MetricId.PierceDischarges), "pierce_discharges", "count", nameof(FoldRule.Sum), nameof(MetricScopeKind.Flight));
            yield return new GoldenRow(nameof(MetricId.PierceToughsCleared), "pierce_toughs_cleared", "count", nameof(FoldRule.Sum), nameof(MetricScopeKind.Flight));
            yield return new GoldenRow(nameof(MetricId.RainbowPierceDischarges), "rainbow_pierce_discharges", "count", nameof(FoldRule.Sum), nameof(MetricScopeKind.Flight));
            yield return new GoldenRow(nameof(MetricId.SpeedTapsMinted), "speed_taps_minted", "count", nameof(FoldRule.Sum), nameof(MetricScopeKind.Flight));
            yield return new GoldenRow(nameof(MetricId.MaxWallBouncesInFlight), "max_wall_bounces_in_flight", "count", nameof(FoldRule.Max), nameof(MetricScopeKind.Level));
            yield return new GoldenRow(nameof(MetricId.MaxSpeedTapsInFlight), "max_speed_taps_in_flight", "count", nameof(FoldRule.Max), nameof(MetricScopeKind.Level));
            yield return new GoldenRow(nameof(MetricId.HoldSpeedUpFlights), "hold_speed_up_flights", "count", nameof(FoldRule.Sum), nameof(MetricScopeKind.Level));
            yield return new GoldenRow(nameof(MetricId.PointsBanked), "points_banked", "points", nameof(FoldRule.Sum), nameof(MetricScopeKind.Level), nameof(MetricAxis.Color));
            yield return new GoldenRow(nameof(MetricId.PointsProjected), "points_projected", "points", nameof(FoldRule.Last), nameof(MetricScopeKind.Level));
            yield return new GoldenRow(nameof(MetricId.MaxMultiplier), "max_multiplier", "multiplier", nameof(FoldRule.Max), nameof(MetricScopeKind.Level));
            yield return new GoldenRow(nameof(MetricId.MaxStreak), "max_streak", "count", nameof(FoldRule.Max), nameof(MetricScopeKind.Level));
            yield return new GoldenRow(nameof(MetricId.StreakBreaks), "streak_breaks", "count", nameof(FoldRule.Sum), nameof(MetricScopeKind.Level));
            yield return new GoldenRow(nameof(MetricId.HeartsLost), "hearts_lost", "hearts", nameof(FoldRule.Sum), nameof(MetricScopeKind.Level));
            yield return new GoldenRow(nameof(MetricId.MaxHeartsLostInWave), "max_hearts_lost_in_wave", "hearts", nameof(FoldRule.Max), nameof(MetricScopeKind.Level));
            yield return new GoldenRow(nameof(MetricId.BlockedSlots), "blocked_slots", "count", nameof(FoldRule.Sum), nameof(MetricScopeKind.Level));
            yield return new GoldenRow(nameof(MetricId.Strikethroughs), "strikethroughs", "count", nameof(FoldRule.Sum), nameof(MetricScopeKind.Level));
            yield return new GoldenRow(nameof(MetricId.ShieldsGained), "shields_gained", "count", nameof(FoldRule.Sum), nameof(MetricScopeKind.Level));
            yield return new GoldenRow(nameof(MetricId.ShieldsSpent), "shields_spent", "count", nameof(FoldRule.Sum), nameof(MetricScopeKind.Level));
            yield return new GoldenRow(nameof(MetricId.ItemsActivated), "items_activated", "count", nameof(FoldRule.Sum), nameof(MetricScopeKind.Level), nameof(MetricAxis.ItemType));
            yield return new GoldenRow(nameof(MetricId.MaxDangerLevel), "max_danger_level", "level", nameof(FoldRule.Max), nameof(MetricScopeKind.Level));
            yield return new GoldenRow(nameof(MetricId.BoardCleared), "board_cleared", "count", nameof(FoldRule.Max), nameof(MetricScopeKind.Level));
            yield return new GoldenRow(nameof(MetricId.LevelsCompleted), "levels_completed", "count", nameof(FoldRule.Sum), nameof(MetricScopeKind.Run));
            yield return new GoldenRow(nameof(MetricId.RetriesUsed), "retries_used", "count", nameof(FoldRule.Last), nameof(MetricScopeKind.Run));
        }

        public readonly struct GoldenRow
        {
            public readonly string MetricIdName;
            public readonly string WireName;
            public readonly string Unit;
            public readonly string FoldName;
            public readonly string ScopeName;
            public readonly string[] AxisNames;

            public GoldenRow(string metricIdName, string wireName, string unit, string foldName, string scopeName, params string[] axisNames)
            {
                MetricIdName = metricIdName;
                WireName = wireName;
                Unit = unit;
                FoldName = foldName;
                ScopeName = scopeName;
                AxisNames = axisNames;
            }

            public override string ToString()
            {
                return MetricIdName;
            }
        }
    }
}
