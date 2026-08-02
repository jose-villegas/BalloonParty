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
        public void RecordMin_KeepsTheRunningMinimum()
        {
            _metrics.RecordMin(MetricId.MinHealth, 5);
            _metrics.RecordMin(MetricId.MinHealth, 1);
            _metrics.RecordMin(MetricId.MinHealth, 3);

            Assert.AreEqual(1, _metrics[MetricId.MinHealth]);
        }

        [Test]
        public void SetLast_OverwritesThePriorValue()
        {
            _metrics.SetLast(MetricId.RetriesUsed, 1);
            _metrics.SetLast(MetricId.RetriesUsed, 4);

            Assert.AreEqual(4, _metrics[MetricId.RetriesUsed]);
        }

        [Test]
        public void IncrementColorAxis_EveryValidIndex_DoesNotThrow()
        {
            for (var i = 0; i < ColorAxisSize; i++)
            {
                var index = i;
                Assert.DoesNotThrow(() => _metrics.IncrementColorAxis(index));
            }

            Assert.AreEqual(1, _metrics.ColorAxis[ColorAxisSize - 1]);
        }

        [Test]
        public void IncrementBalloonTypeAxis_EveryEnumValue_DoesNotThrow()
        {
            foreach (BalloonType type in Enum.GetValues(typeof(BalloonType)))
            {
                var captured = type;
                Assert.DoesNotThrow(() => _metrics.IncrementBalloonTypeAxis(captured));
            }

            Assert.AreEqual(1, _metrics.BalloonTypeAxis[(int)BalloonType.Tougher]);
        }

        [Test]
        public void IncrementItemTypeAxis_EveryEnumValue_DoesNotThrow()
        {
            foreach (ItemType type in Enum.GetValues(typeof(ItemType)))
            {
                var captured = type;
                Assert.DoesNotThrow(() => _metrics.IncrementItemTypeAxis(captured));
            }

            Assert.AreEqual(1, _metrics.ItemTypeAxis[(int)ItemType.Snipe]);
        }

        [Test]
        public void Reset_ZeroesCountersAndEveryAxis()
        {
            _metrics.Increment(MetricId.ShotsFired);
            _metrics.IncrementColorAxis(0);
            _metrics.IncrementBalloonTypeAxis(BalloonType.Tough);
            _metrics.IncrementItemTypeAxis(ItemType.Bomb);

            _metrics.Reset();

            Assert.AreEqual(0, _metrics[MetricId.ShotsFired]);
            Assert.AreEqual(0, _metrics.ColorAxis[0]);
            Assert.AreEqual(0, _metrics.BalloonTypeAxis[(int)BalloonType.Tough]);
            Assert.AreEqual(0, _metrics.ItemTypeAxis[(int)ItemType.Bomb]);
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
        public void CopyColorAxis_ReturnsAnIndependentArray()
        {
            _metrics.IncrementColorAxis(0);

            var copy = _metrics.CopyColorAxis();
            _metrics.IncrementColorAxis(0);

            Assert.AreEqual(1, copy[0]);
            Assert.AreEqual(2, _metrics.ColorAxis[0]);
        }
    }
}
