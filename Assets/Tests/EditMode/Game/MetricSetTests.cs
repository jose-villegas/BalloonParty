using System;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Items;
using BalloonParty.Game.Telemetry;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class MetricSetTests
    {
        private const int ColorAxisSize = 3;

        private MetricSet _metrics;

        [SetUp]
        public void SetUp()
        {
            _metrics = new MetricSet(ColorAxisSize);
        }

        [Test]
        public void Increment_AddsOneToTheCounter()
        {
            _metrics.Increment(MetricId.ShotsFired);
            _metrics.Increment(MetricId.ShotsFired);

            Assert.AreEqual(2, _metrics[MetricId.ShotsFired]);
        }

        [Test]
        public void Add_AddsTheGivenValue()
        {
            _metrics.Add(MetricId.HeartsLost, 3);
            _metrics.Add(MetricId.HeartsLost, 2);

            Assert.AreEqual(5, _metrics[MetricId.HeartsLost]);
        }

        [Test]
        public void RecordMax_KeepsTheRunningMaximum()
        {
            _metrics.RecordMax(MetricId.MaxStreak, 3);
            _metrics.RecordMax(MetricId.MaxStreak, 7);
            _metrics.RecordMax(MetricId.MaxStreak, 5);

            Assert.AreEqual(7, _metrics[MetricId.MaxStreak]);
        }

        [Test]
        public void SetLast_OverwritesThePriorValue()
        {
            _metrics.SetLast(MetricId.RetriesUsed, 1);
            _metrics.SetLast(MetricId.RetriesUsed, 4);

            Assert.AreEqual(4, _metrics[MetricId.RetriesUsed]);
        }

        [Test]
        public void IncrementAxis_EveryColorBucket_DoesNotThrow()
        {
            for (var i = 0; i < ColorAxisSize; i++)
            {
                var bucket = i;
                Assert.DoesNotThrow(() => _metrics.IncrementAxis(MetricId.Pops, MetricAxis.Color, bucket));
            }

            Assert.AreEqual(1, _metrics.AxisBucketsOf(MetricId.Pops, MetricAxis.Color)[ColorAxisSize - 1]);
        }

        [Test]
        public void IncrementAxis_EveryBalloonTypeBucket_DoesNotThrow()
        {
            foreach (BalloonType type in Enum.GetValues(typeof(BalloonType)))
            {
                var captured = type;
                Assert.DoesNotThrow(() => _metrics.IncrementAxis(MetricId.Deflects, MetricAxis.BalloonType, (int)captured));
            }

            Assert.AreEqual(1, _metrics.AxisBucketsOf(MetricId.Deflects, MetricAxis.BalloonType)[(int)BalloonType.Tougher]);
        }

        [Test]
        public void IncrementAxis_EveryItemTypeBucket_DoesNotThrow()
        {
            foreach (ItemType type in Enum.GetValues(typeof(ItemType)))
            {
                var captured = type;
                Assert.DoesNotThrow(() => _metrics.IncrementAxis(MetricId.ItemsActivated, MetricAxis.ItemType, (int)captured));
            }

            Assert.AreEqual(1, _metrics.AxisBucketsOf(MetricId.ItemsActivated, MetricAxis.ItemType)[(int)ItemType.Snipe]);
        }

        // The largest change in this rework (see PLAN-GameplayTelemetry.md, "Axis storage"): storage is
        // keyed by the (MetricId, axis) pair, not by the axis kind alone. Pops and Deflects both declare
        // a BalloonType axis; without per-pair slots they would silently share one table.
        [Test]
        public void IncrementAxis_SameAxisKindDifferentMetric_DoesNotBleedBetweenMetrics()
        {
            _metrics.IncrementAxis(MetricId.Pops, MetricAxis.BalloonType, (int)BalloonType.Tough);
            _metrics.IncrementAxis(MetricId.Deflects, MetricAxis.BalloonType, (int)BalloonType.Tough);
            _metrics.IncrementAxis(MetricId.Pops, MetricAxis.BalloonType, (int)BalloonType.Tough);

            Assert.AreEqual(2, _metrics.AxisBucketsOf(MetricId.Pops, MetricAxis.BalloonType)[(int)BalloonType.Tough]);
            Assert.AreEqual(1, _metrics.AxisBucketsOf(MetricId.Deflects, MetricAxis.BalloonType)[(int)BalloonType.Tough]);
        }

        // Pops (a count) and PointsBanked (points) both declare a Color axis. Summing them into one
        // table would add two different units together.
        [Test]
        public void AddAxis_SameAxisKindDifferentMetric_DoesNotBleedBetweenMetrics()
        {
            _metrics.IncrementAxis(MetricId.Pops, MetricAxis.Color, 0);
            _metrics.AddAxis(MetricId.PointsBanked, MetricAxis.Color, 0, 50);

            Assert.AreEqual(1, _metrics.AxisBucketsOf(MetricId.Pops, MetricAxis.Color)[0]);
            Assert.AreEqual(50, _metrics.AxisBucketsOf(MetricId.PointsBanked, MetricAxis.Color)[0]);
        }

        [Test]
        public void Reset_ZeroesCountersAndEveryAxisSlot()
        {
            _metrics.Increment(MetricId.ShotsFired);
            _metrics.IncrementAxis(MetricId.Pops, MetricAxis.Color, 0);
            _metrics.IncrementAxis(MetricId.Deflects, MetricAxis.BalloonType, (int)BalloonType.Tough);
            _metrics.IncrementAxis(MetricId.ItemsActivated, MetricAxis.ItemType, (int)ItemType.Bomb);

            _metrics.Reset();

            Assert.AreEqual(0, _metrics[MetricId.ShotsFired]);
            Assert.AreEqual(0, _metrics.AxisBucketsOf(MetricId.Pops, MetricAxis.Color)[0]);
            Assert.AreEqual(0, _metrics.AxisBucketsOf(MetricId.Deflects, MetricAxis.BalloonType)[(int)BalloonType.Tough]);
            Assert.AreEqual(0, _metrics.AxisBucketsOf(MetricId.ItemsActivated, MetricAxis.ItemType)[(int)ItemType.Bomb]);
        }

        [Test]
        public void CopyCounters_ReturnsAnIndependentArray()
        {
            _metrics.Increment(MetricId.ShotsFired);

            var copy = _metrics.CopyCounters();
            _metrics.Increment(MetricId.ShotsFired);

            Assert.AreEqual(1, copy[(int)MetricId.ShotsFired]);
            Assert.AreEqual(2, _metrics[MetricId.ShotsFired]);
        }

        [Test]
        public void CopyAxis_ReturnsAnIndependentArray()
        {
            _metrics.IncrementAxis(MetricId.Pops, MetricAxis.Color, 0);

            var copy = _metrics.CopyAxis(MetricId.Pops, MetricAxis.Color);
            _metrics.IncrementAxis(MetricId.Pops, MetricAxis.Color, 0);

            Assert.AreEqual(1, copy[0]);
            Assert.AreEqual(2, _metrics.AxisBucketsOf(MetricId.Pops, MetricAxis.Color)[0]);
        }
    }
}
