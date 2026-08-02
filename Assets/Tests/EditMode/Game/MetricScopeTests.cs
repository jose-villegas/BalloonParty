using System;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Items;
using BalloonParty.Game.Telemetry;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class MetricScopeTests
    {
        private const int ColorAxisSize = 3;

        private float _now;

        [SetUp]
        public void SetUp()
        {
            _now = 0f;
        }

        [Test]
        public void Absorb_SumMetric_AddsTheChildIntoTheParent()
        {
            var parent = BuildScope(MetricScopeKind.Level);
            var child = BuildScope(MetricScopeKind.Flight);
            parent.Add(MetricId.WallBounces, 3);
            child.Add(MetricId.WallBounces, 2);

            parent.Absorb(child);

            Assert.AreEqual(5, parent.Metrics[MetricId.WallBounces]);
        }

        [Test]
        public void Absorb_MaxMetric_TakesTheRunningMaximum()
        {
            var parent = BuildScope(MetricScopeKind.Run);
            var risingChild = BuildScope(MetricScopeKind.Level);
            parent.RecordMax(MetricId.MaxWallBouncesInFlight, 4);
            risingChild.RecordMax(MetricId.MaxWallBouncesInFlight, 7);

            parent.Absorb(risingChild);

            Assert.AreEqual(7, parent.Metrics[MetricId.MaxWallBouncesInFlight]);

            // A smaller later child must not pull an already-higher parent maximum back down.
            var smallerChild = BuildScope(MetricScopeKind.Level);
            smallerChild.RecordMax(MetricId.MaxWallBouncesInFlight, 2);
            parent.Absorb(smallerChild);

            Assert.AreEqual(7, parent.Metrics[MetricId.MaxWallBouncesInFlight]);
        }

        [Test]
        public void Absorb_LastMetric_TakesTheChildsValue()
        {
            var parent = BuildScope(MetricScopeKind.Session);
            var child = BuildScope(MetricScopeKind.Run);
            parent.SetLast(MetricId.RetriesUsed, 1);
            child.SetLast(MetricId.RetriesUsed, 3);

            parent.Absorb(child);

            Assert.AreEqual(3, parent.Metrics[MetricId.RetriesUsed]);
        }

        // The gate the previous wave was missing entirely (see PLAN-GameplayTelemetry.md, "The
        // catalog"): RetriesUsed is a Run-scoped Last metric. A Level child never touches it, so
        // absorbing one must not overwrite the parent's real value with the child's untouched zero.
        [Test]
        public void Absorb_LastMetricAboveTheChildsScope_DoesNotOverwriteWithTheChildsZero()
        {
            var parent = BuildScope(MetricScopeKind.Run);
            var levelChild = BuildScope(MetricScopeKind.Level);
            parent.SetLast(MetricId.RetriesUsed, 3);

            parent.Absorb(levelChild);

            Assert.AreEqual(3, parent.Metrics[MetricId.RetriesUsed],
                "RetriesUsed is Run-scoped; a Level child's untouched zero must not fold into it");
        }

        [Test]
        public void Absorb_ColorAndBalloonTypeAxes_SumElementWise()
        {
            var parent = BuildScope(MetricScopeKind.Level);
            var child = BuildScope(MetricScopeKind.Flight);
            child.IncrementAxis(MetricId.Pops, MetricAxis.Color, 0);
            child.IncrementAxis(MetricId.Pops, MetricAxis.Color, 0);
            child.IncrementAxis(MetricId.Pops, MetricAxis.BalloonType, (int)BalloonType.Tough);

            parent.Absorb(child);

            Assert.AreEqual(2, parent.Metrics.AxisBucketsOf(MetricId.Pops, MetricAxis.Color)[0]);
            Assert.AreEqual(1, parent.Metrics.AxisBucketsOf(MetricId.Pops, MetricAxis.BalloonType)[(int)BalloonType.Tough]);
        }

        [Test]
        public void Absorb_ItemTypeAxis_SumsElementWise()
        {
            var parent = BuildScope(MetricScopeKind.Run);
            var child = BuildScope(MetricScopeKind.Level);
            child.IncrementAxis(MetricId.ItemsActivated, MetricAxis.ItemType, (int)ItemType.Shield);
            child.IncrementAxis(MetricId.ItemsActivated, MetricAxis.ItemType, (int)ItemType.Shield);

            parent.Absorb(child);

            Assert.AreEqual(2, parent.Metrics.AxisBucketsOf(MetricId.ItemsActivated, MetricAxis.ItemType)[(int)ItemType.Shield]);
        }

        // Pops and Deflects share the BalloonType axis kind but not the same AxisSlot (MetricSetTests
        // proves this at the MetricSet level; this proves the same isolation survives a fold).
        [Test]
        public void Absorb_PopsAndDeflectsShareBalloonTypeAxisKind_DoNotBleedIntoEachOther()
        {
            var parent = BuildScope(MetricScopeKind.Level);
            var child = BuildScope(MetricScopeKind.Flight);
            child.IncrementAxis(MetricId.Pops, MetricAxis.BalloonType, (int)BalloonType.Tough);
            child.IncrementAxis(MetricId.Pops, MetricAxis.BalloonType, (int)BalloonType.Tough);
            child.IncrementAxis(MetricId.Deflects, MetricAxis.BalloonType, (int)BalloonType.Tough);

            parent.Absorb(child);

            Assert.AreEqual(2, parent.Metrics.AxisBucketsOf(MetricId.Pops, MetricAxis.BalloonType)[(int)BalloonType.Tough]);
            Assert.AreEqual(1, parent.Metrics.AxisBucketsOf(MetricId.Deflects, MetricAxis.BalloonType)[(int)BalloonType.Tough]);
        }

        // Guards on the relationship Absorb assumes: the child must be exactly one scope below the
        // parent, or the fold silently skips or double-counts the scope in between.
        [Test]
        public void Absorb_TheScopeItself_Throws()
        {
            var scope = BuildScope(MetricScopeKind.Level);

            Assert.Throws<ArgumentException>(() => scope.Absorb(scope));
        }

        [Test]
        public void Absorb_ASameKindSiblingScope_Throws()
        {
            var parent = BuildScope(MetricScopeKind.Level);
            var sibling = BuildScope(MetricScopeKind.Level);

            Assert.Throws<ArgumentException>(() => parent.Absorb(sibling));
        }

        [Test]
        public void Absorb_ANonAdjacentScope_Throws()
        {
            var session = BuildScope(MetricScopeKind.Session);
            var level = BuildScope(MetricScopeKind.Level);

            Assert.Throws<ArgumentException>(() => session.Absorb(level));
        }

        [Test]
        public void Seal_Level_CarriesLevelIndexAndCompleted()
        {
            var scope = BuildScope(MetricScopeKind.Level);
            scope.Add(MetricId.ShotsFired, 4);

            var snapshot = scope.Seal(3, true);

            Assert.AreEqual(3, snapshot.LevelIndex);
            Assert.IsTrue(snapshot.Completed);
            Assert.AreEqual(4, snapshot[MetricId.ShotsFired]);
        }

        [Test]
        public void Seal_Run_ReflectsCurrentCounters()
        {
            var scope = BuildScope(MetricScopeKind.Run);
            scope.Add(MetricId.HeartsLost, 6);

            var snapshot = scope.Seal();

            Assert.AreEqual(6, snapshot[MetricId.HeartsLost]);
        }

        // Guards on the relationship Seal(int, bool) vs Seal() assumes: the overload picks the
        // snapshot type, so nothing else stops a Run scope producing a LevelMetricsSnapshot (or vice
        // versa) without this check.
        [Test]
        public void Seal_LevelOverload_OnANonLevelScope_Throws()
        {
            var scope = BuildScope(MetricScopeKind.Run);

            Assert.Throws<InvalidOperationException>(() => scope.Seal(1, true));
        }

        [Test]
        public void Seal_RunOverload_OnANonRunScope_Throws()
        {
            var scope = BuildScope(MetricScopeKind.Level);

            Assert.Throws<InvalidOperationException>(() => scope.Seal());
        }

        [Test]
        public void Seal_ThenReset_DoesNotMutateThePriorSnapshot()
        {
            var scope = BuildScope(MetricScopeKind.Level);
            scope.Add(MetricId.ShotsFired, 5);
            scope.IncrementAxis(MetricId.Pops, MetricAxis.Color, 0);

            var snapshot = scope.Seal(1, false);

            scope.Reset();
            scope.Add(MetricId.ShotsFired, 99);
            scope.IncrementAxis(MetricId.Pops, MetricAxis.Color, 0);

            Assert.AreEqual(5, snapshot[MetricId.ShotsFired],
                "the sealed snapshot must not see mutations that happen after Seal()");
            Assert.AreEqual(1, snapshot.PopsByColor[0].Count);
        }

        // Item 4 of the W1 rework: PointsByColor and DeflectsByBalloonType are new on both snapshots.
        [Test]
        public void Seal_ExposesPointsByColorAndDeflectsByBalloonType()
        {
            var scope = BuildScope(MetricScopeKind.Level);
            scope.AddAxis(MetricId.PointsBanked, MetricAxis.Color, 1, 50);
            scope.IncrementAxis(MetricId.Deflects, MetricAxis.BalloonType, (int)BalloonType.Unbreakable);

            var snapshot = scope.Seal(0, false);

            Assert.AreEqual(50, snapshot.PointsByColor[1].Count);
            Assert.AreEqual(1, snapshot.DeflectsByBalloonType[(int)BalloonType.Unbreakable].Count);
        }

        // Highest-value test gap the review flagged: a real exporter loops the catalog calling
        // AxisBucketsOf on whatever snapshot it is handed, and reads every TimerId through the
        // indexer — neither was exercised on a sealed snapshot anywhere before this. This also
        // exercises Wall and Hold, previously untouched by any test.
        [Test]
        public void Seal_Level_ExposesAxisBucketsOfAndEveryTimerThroughTheIndexer()
        {
            var scope = BuildScope(MetricScopeKind.Level);
            scope.IncrementAxis(MetricId.Pops, MetricAxis.Color, 1);
            scope.IncrementAxis(MetricId.Pops, MetricAxis.Color, 1);
            scope.Timer(TimerId.Gameplay).Resume();
            scope.Timer(TimerId.Ceremony).Resume();
            scope.Timer(TimerId.Wall).Resume();
            scope.Timer(TimerId.Hold).Resume();
            _now = 5f;

            var snapshot = scope.Seal(0, false);

            Assert.AreEqual(2, snapshot.AxisBucketsOf(MetricId.Pops, MetricAxis.Color)[1]);
            Assert.AreEqual(5f, snapshot[TimerId.Gameplay]);
            Assert.AreEqual(5f, snapshot[TimerId.Ceremony]);
            Assert.AreEqual(5f, snapshot[TimerId.Wall]);
            Assert.AreEqual(5f, snapshot[TimerId.Hold]);
        }

        [Test]
        public void Seal_Run_ExposesAxisBucketsOfAndEveryTimerThroughTheIndexer()
        {
            var scope = BuildScope(MetricScopeKind.Run);
            scope.IncrementAxis(MetricId.ItemsActivated, MetricAxis.ItemType, (int)ItemType.Bomb);
            scope.Timer(TimerId.Wall).Resume();
            scope.Timer(TimerId.Hold).Resume();
            _now = 3f;

            var snapshot = scope.Seal();

            Assert.AreEqual(1, snapshot.AxisBucketsOf(MetricId.ItemsActivated, MetricAxis.ItemType)[(int)ItemType.Bomb]);
            Assert.AreEqual(3f, snapshot[TimerId.Wall]);
            Assert.AreEqual(3f, snapshot[TimerId.Hold]);
        }

        // Seal_ThenReset_DoesNotMutateThePriorSnapshot above only covers Level — RunMetricsSnapshot has
        // its own axisSlots/counters copy and was never proven safe against a post-seal Reset().
        [Test]
        public void Seal_Run_ThenReset_DoesNotMutateThePriorSnapshot()
        {
            var scope = BuildScope(MetricScopeKind.Run);
            scope.Add(MetricId.HeartsLost, 5);
            scope.IncrementAxis(MetricId.ItemsActivated, MetricAxis.ItemType, (int)ItemType.Bomb);

            var snapshot = scope.Seal();

            scope.Reset();
            scope.Add(MetricId.HeartsLost, 99);
            scope.IncrementAxis(MetricId.ItemsActivated, MetricAxis.ItemType, (int)ItemType.Bomb);

            Assert.AreEqual(5, snapshot[MetricId.HeartsLost],
                "the sealed run snapshot must not see mutations that happen after Seal()");
            Assert.AreEqual(1, snapshot.ItemsActivated[(int)ItemType.Bomb].Count);
        }

        [Test]
        public void Reset_ZeroesElapsedButPreservesEachTimersRunningState()
        {
            var scope = BuildScope(MetricScopeKind.Level);
            scope.Timer(TimerId.Gameplay).Resume();
            _now = 5f;

            scope.Reset();

            Assert.IsTrue(scope.Timer(TimerId.Gameplay).IsRunning,
                "a running timer (e.g. Wall, which is never explicitly paused/resumed across a level flush) must keep running across Reset()");
            Assert.AreEqual(0f, scope.Timer(TimerId.Gameplay).Elapsed);
            Assert.IsFalse(scope.Timer(TimerId.Ceremony).IsRunning);
        }

        private MetricScope BuildScope(MetricScopeKind kind)
        {
            return MetricScope.Create(kind, ColorAxisSize, () => _now);
        }
    }
}
