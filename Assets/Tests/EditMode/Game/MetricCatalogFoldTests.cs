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

        [Test]
        public void MinHealth_FoldsAsMin()
        {
            Assert.AreEqual(FoldRule.Min, MetricCatalog.FoldOf(MetricId.MinHealth));
        }
    }
}
