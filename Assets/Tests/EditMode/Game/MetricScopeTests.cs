using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Items;
using BalloonParty.Game.Telemetry;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class MetricScopeTests
    {
        private float _now;

        [SetUp]
        public void SetUp()
        {
            _now = 0f;
        }

        [Test]
        public void Absorb_SumMetric_AddsTheChildIntoTheParent()
        {
            var parent = BuildScope();
            var child = BuildScope();
            parent.Metrics.Add(MetricId.ShotsFired, 3);
            child.Metrics.Add(MetricId.ShotsFired, 2);

            parent.Absorb(child);

            Assert.AreEqual(5, parent.Metrics[MetricId.ShotsFired]);
        }

        [Test]
        public void Absorb_MaxMetric_TakesTheRunningMaximum()
        {
            var parent = BuildScope();
            var risingChild = BuildScope();
            parent.Metrics.RecordMax(MetricId.MaxStreak, 4);
            risingChild.Metrics.RecordMax(MetricId.MaxStreak, 7);

            parent.Absorb(risingChild);

            Assert.AreEqual(7, parent.Metrics[MetricId.MaxStreak]);

            // A smaller later child must not pull an already-higher parent maximum back down.
            var smallerChild = BuildScope();
            smallerChild.Metrics.RecordMax(MetricId.MaxStreak, 2);
            parent.Absorb(smallerChild);

            Assert.AreEqual(7, parent.Metrics[MetricId.MaxStreak]);
        }

        [Test]
        public void Absorb_MinMetric_TakesTheRunningMinimum()
        {
            var parent = BuildScope();
            var child = BuildScope();
            parent.Metrics.RecordMin(MetricId.MinHealth, 5);
            child.Metrics.RecordMin(MetricId.MinHealth, 2);

            parent.Absorb(child);

            Assert.AreEqual(2, parent.Metrics[MetricId.MinHealth]);
        }

        [Test]
        public void Absorb_LastMetric_TakesTheChildsValue()
        {
            var parent = BuildScope();
            var child = BuildScope();
            parent.Metrics.SetLast(MetricId.RetriesUsed, 1);
            child.Metrics.SetLast(MetricId.RetriesUsed, 3);

            parent.Absorb(child);

            Assert.AreEqual(3, parent.Metrics[MetricId.RetriesUsed]);
        }

        [Test]
        public void Absorb_ColorAndBalloonTypeAxes_SumElementWise()
        {
            var parent = BuildScope();
            var child = BuildScope();
            child.Metrics.IncrementColorAxis(0);
            child.Metrics.IncrementColorAxis(0);
            child.Metrics.IncrementBalloonTypeAxis(BalloonType.Tough);

            parent.Absorb(child);

            Assert.AreEqual(2, parent.Metrics.ColorAxis[0]);
            Assert.AreEqual(1, parent.Metrics.BalloonTypeAxis[(int)BalloonType.Tough]);
        }

        [Test]
        public void Absorb_ItemTypeAxis_SumsElementWise()
        {
            var parent = BuildScope();
            var child = BuildScope();
            child.Metrics.IncrementItemTypeAxis(ItemType.Shield);
            child.Metrics.IncrementItemTypeAxis(ItemType.Shield);

            parent.Absorb(child);

            Assert.AreEqual(2, parent.Metrics.ItemTypeAxis[(int)ItemType.Shield]);
        }

        [Test]
        public void Seal_Level_CarriesLevelIndexAndCompleted()
        {
            var scope = BuildScope();
            scope.Metrics.Add(MetricId.ShotsFired, 4);

            var snapshot = scope.Seal(3, true);

            Assert.AreEqual(3, snapshot.LevelIndex);
            Assert.IsTrue(snapshot.Completed);
            Assert.AreEqual(4, snapshot[MetricId.ShotsFired]);
        }

        [Test]
        public void Seal_Run_ReflectsCurrentCounters()
        {
            var scope = BuildScope();
            scope.Metrics.Add(MetricId.HeartsLost, 6);

            var snapshot = scope.Seal();

            Assert.AreEqual(6, snapshot[MetricId.HeartsLost]);
        }

        [Test]
        public void Seal_ThenReset_DoesNotMutateThePriorSnapshot()
        {
            var scope = BuildScope();
            scope.Metrics.Add(MetricId.ShotsFired, 5);
            scope.Metrics.IncrementColorAxis(0);

            var snapshot = scope.Seal(1, false);

            scope.Reset();
            scope.Metrics.Add(MetricId.ShotsFired, 99);
            scope.Metrics.IncrementColorAxis(0);

            Assert.AreEqual(5, snapshot[MetricId.ShotsFired],
                "the sealed snapshot must not see mutations that happen after Seal()");
            Assert.AreEqual(1, snapshot.PopsByColor[0].Count);
        }

        [Test]
        public void Reset_PausesEveryTimerAndZeroesElapsed()
        {
            var scope = BuildScope();
            scope.Timer(TimerId.Gameplay).Resume();
            _now = 5f;

            scope.Reset();

            Assert.IsFalse(scope.Timer(TimerId.Gameplay).IsRunning);
            Assert.AreEqual(0f, scope.Timer(TimerId.Gameplay).Elapsed);
        }

        private MetricScope BuildScope()
        {
            var metrics = new MetricSet(3);
            var timers = new TelemetryStopwatch[4];
            for (var i = 0; i < timers.Length; i++)
            {
                timers[i] = new TelemetryStopwatch(() => _now);
            }

            return new MetricScope(metrics, timers);
        }
    }
}
