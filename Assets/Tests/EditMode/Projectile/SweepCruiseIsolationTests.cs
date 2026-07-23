using System;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Score;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Projectile.View;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Projectile
{
    /// <summary>Verifies the mutual-exclusivity invariant: Sweep taps and Cruise taps never
    /// double-contribute. An empty flight never triggers Sweep; a segment with pops never triggers
    /// Cruise. These tests bridge the resolver (cruise-bounce counting) and the view-level sweep
    /// guard to confirm the two speed-tap sources remain isolated.</summary>
    [TestFixture]
    public class SweepCruiseIsolationTests
    {
        private static readonly Vector4 Walls = new(5f, 5f, -5f, -5f);

        private IHitDispatcher _hitDispatcher;
        private ProjectileHitResolver _hitResolver;
        private ProjectileMotionResolver _motionResolver;
        private ProjectileModel _projectile;
        private readonly System.Collections.Generic.List<GameObject> _gameObjectsToDestroy = new();

        [SetUp]
        public void SetUp()
        {
            _hitDispatcher = Substitute.For<IHitDispatcher>();
            var shieldGainedPublisher = Substitute.For<IPublisher<ShieldGainedMessage>>();
            var dischargedPublisher = Substitute.For<IPublisher<PierceDischargedMessage>>();

            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(6, 10));
            gridConfig.SlotSeparation.Returns(new Vector2(1f, 0.85f));
            gridConfig.SlotsOffset.Returns(new Vector2(2.5f, 4f));

            var flightConfig = Substitute.For<IProjectileFlightConfig>();
            flightConfig.LimitsClockwise.Returns(Walls);
            flightConfig.CruiseSpeedPerShield.Returns(0.5f);
            flightConfig.CruiseTapEaseDuration.Returns(0f);
            flightConfig.CruisePiercingTapThreshold.Returns(0);
            flightConfig.CruiseTapCurve.Returns(AnimationCurve.Linear(0f, 0f, 1f, 1f));

            var grid = new SlotGrid(gridConfig, new BalancePathHolder());

            var levelUpSubscriber = Substitute.For<ISubscriber<ScoreLevelUpMessage>>();
            levelUpSubscriber
                .Subscribe(
                    Arg.Any<IMessageHandler<ScoreLevelUpMessage>>(),
                    Arg.Any<MessageHandlerFilter<ScoreLevelUpMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            var projectileLoadedSubscriber = Substitute.For<ISubscriber<ProjectileLoadedMessage>>();
            projectileLoadedSubscriber
                .Subscribe(
                    Arg.Any<IMessageHandler<ProjectileLoadedMessage>>(),
                    Arg.Any<MessageHandlerFilter<ProjectileLoadedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            var streakTracker = new ColorStreakTracker(
                Substitute.For<IPublisher<StreakChangedMessage>>(), levelUpSubscriber, projectileLoadedSubscriber);

            _hitResolver = new ProjectileHitResolver(
                _hitDispatcher, shieldGainedPublisher, dischargedPublisher, streakTracker, grid);
            _motionResolver = new ProjectileMotionResolver(flightConfig);
            _projectile = new ProjectileModel { IsFree = true };
            _projectile.ShieldsRemaining.Value = 10;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _gameObjectsToDestroy)
            {
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
            }

            _gameObjectsToDestroy.Clear();
        }

        // ──────────────────────────────────────────────────────────────────────────────────────
        // 1. Empty segment (no pops) → Sweep does NOT trigger, Cruise CAN trigger
        // ──────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void TryAwardSweepTap_EmptySegmentNoPops_DoesNotIncrementTotalSweeps()
        {
            var view = CreateSweepView();
            _projectile.Flight.SegmentPopCount = 0;
            _projectile.Flight.LastBouncePosition = Vector3.zero;

            AwardSweepTap(view, new Vector3(3f, 0f, 0f), Vector3.right);

            Assert.AreEqual(0, _projectile.Flight.TotalSweeps,
                "SegmentPopCount == 0 → the sweep guard bails, TotalSweeps stays unchanged");
        }

        [Test]
        public void TryAwardSweepTap_EmptySegmentNoPops_DoesNotIncrementTotalCruiseTaps()
        {
            var view = CreateSweepView();
            _projectile.Flight.SegmentPopCount = 0;
            _projectile.Flight.LastBouncePosition = Vector3.zero;

            AwardSweepTap(view, new Vector3(3f, 0f, 0f), Vector3.right);

            Assert.AreEqual(0, _projectile.Flight.TotalCruiseTaps,
                "no pops on the segment means no shared speed tap is awarded");
        }

        [Test]
        public void Step_EmptySegmentCruising_AccumulatesCruiseTaps()
        {
            // A cruising shot on an empty corridor (SegmentPopCount remains 0) earns cruise taps
            // on each wall bounce — confirming cruise CAN trigger on empty segments.
            _projectile.Direction = Vector2.up;
            _projectile.Speed = 1f;
            _projectile.IsCruising.Value = true;

            _motionResolver.Step(_projectile, new Vector3(0f, 4.5f, 0f), 1f);

            Assert.AreEqual(0, _projectile.Flight.SegmentPopCount,
                "no balloon contact — the segment remains empty");
            Assert.AreEqual(1, _projectile.Flight.TotalCruiseTaps,
                "cruise taps accumulate on empty-corridor bounces");
        }

        // ──────────────────────────────────────────────────────────────────────────────────────
        // 2. Segment with pops → Sweep CAN trigger, Cruise bounce counter resets
        // ──────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void TryAwardSweepTap_SegmentWithPops_IncrementsTotalSweeps()
        {
            var view = CreateSweepView();
            var balloon = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            balloon.Color.Value = "Red";
            _projectile.Flight.LastBouncePosition = Vector3.zero;

            _hitResolver.Resolve(_projectile, balloon, Vector3.zero);

            Assert.Greater(_projectile.Flight.SegmentPopCount, 0, "sanity: pop was counted");

            AwardSweepTap(view, new Vector3(3f, 0f, 0f), Vector3.right);

            Assert.AreEqual(1, _projectile.Flight.TotalSweeps,
                "pops on the segment allow a sweep to be counted");
        }

        [Test]
        public void Resolve_BalloonContact_ResetsCruiseBounceCounter()
        {
            _projectile.Flight.ConsecutiveWallBounces = 5;

            var balloon = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            balloon.Color.Value = "Red";
            _hitResolver.Resolve(_projectile, balloon, Vector3.zero);

            Assert.AreEqual(0, _projectile.Flight.ConsecutiveWallBounces,
                "any balloon contact resets the consecutive bounce counter, preventing cruise entry");
        }

        [Test]
        public void Resolve_BalloonContact_EndsCruise()
        {
            _projectile.Flight.ConsecutiveWallBounces = 5;
            _projectile.IsCruising.Value = true;

            var balloon = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            balloon.Color.Value = "Red";
            _hitResolver.Resolve(_projectile, balloon, Vector3.zero);

            Assert.IsFalse(_projectile.IsCruising.Value,
                "a segment with pops exits cruise — cruise and sweep never overlap");
        }

        // ──────────────────────────────────────────────────────────────────────────────────────
        // 3. Segment with pops + path NOT clear → Neither triggers
        // ──────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void TryAwardSweepTap_SegmentSweepInvalid_NeitherSweepNorCruise()
        {
            var view = CreateSweepView();
            // A multi-hit contact invalidates the sweep even though pops happened.
            _projectile.Flight.SegmentPopCount = 2;
            _projectile.Flight.SegmentSweepValid = false;
            _projectile.Flight.LastBouncePosition = Vector3.zero;

            AwardSweepTap(view, new Vector3(3f, 0f, 0f), Vector3.right);

            Assert.AreEqual(0, _projectile.Flight.TotalSweeps,
                "SegmentSweepValid == false prevents any sweep count");
            Assert.AreEqual(0, _projectile.Flight.TotalCruiseTaps,
                "no shared speed tap when the path wasn't a clean corridor clear");
            Assert.IsFalse(_projectile.IsCruising.Value,
                "the segment had pops, so ConsecutiveWallBounces was already reset — no cruise either");
        }

        // ──────────────────────────────────────────────────────────────────────────────────────
        // 4. Below SweepTapThreshold → sweep counted but no speed, Cruise still prevented
        // ──────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void TryAwardSweepTap_BelowThreshold_CountsButNoSpeed()
        {
            var view = CreateSweepView(sweepTapThreshold: 3);
            _projectile.Flight.SegmentPopCount = 1;
            _projectile.Flight.SegmentSweepValid = true;
            _projectile.Flight.LastBouncePosition = Vector3.zero;

            AwardSweepTap(view, new Vector3(3f, 0f, 0f), Vector3.right);

            Assert.AreEqual(1, _projectile.Flight.TotalSweeps,
                "sweep is counted (toward threshold progress)");
            Assert.AreEqual(0, _projectile.Flight.TotalCruiseTaps,
                "below-threshold sweeps don't contribute to the shared cruise-tap counter");
        }

        [Test]
        public void TryAwardSweepTap_BelowThreshold_CruiseStillPrevented()
        {
            // Even though the sweep didn't award speed (below threshold), the segment had pops —
            // so the cruise counter was already reset by the hit resolver. Cruise cannot trigger.
            _projectile.Flight.ConsecutiveWallBounces = 10;
            var balloon = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            balloon.Color.Value = "Red";
            _hitResolver.Resolve(_projectile, balloon, Vector3.zero);

            Assert.AreEqual(0, _projectile.Flight.ConsecutiveWallBounces,
                "pops reset the bounce counter regardless of sweep threshold");
            Assert.IsFalse(_projectile.IsCruising.Value,
                "cruise is killed by balloon contact even if sweep didn't award speed");
        }

        // ──────────────────────────────────────────────────────────────────────────────────────
        // 5. No double speed accumulation — alternating segments sum correctly
        // ──────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void AlternatingSegments_TotalSpeedMatchesSumOfIndividualSources()
        {
            // Scenario: two cruise bounces then a sweep tap. The shared TotalCruiseTaps counter
            // should land at exactly 3 with no double-counting.
            _projectile.Direction = Vector2.up;
            _projectile.Speed = 1f;
            _projectile.IsCruising.Value = true;

            // Two cruise bounces on an empty segment.
            _motionResolver.Step(_projectile, new Vector3(0f, 4.5f, 0f), 1f);
            _projectile.Direction = Vector2.up;
            _motionResolver.Step(_projectile, new Vector3(0f, 4.5f, 0f), 1f);

            Assert.AreEqual(2, _projectile.Flight.TotalCruiseTaps, "two cruise-bounce taps");

            // Now a balloon contact breaks the cruise and resets the counter.
            var balloon = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            balloon.Color.Value = "Red";
            _hitResolver.Resolve(_projectile, balloon, Vector3.zero);

            Assert.IsFalse(_projectile.IsCruising.Value, "balloon contact ended cruise");
            Assert.AreEqual(0, _projectile.Flight.ConsecutiveWallBounces, "counter reset");

            // Award a sweep on the segment that had the pop.
            var view = CreateSweepView();
            _projectile.Flight.LastBouncePosition = Vector3.zero;
            AwardSweepTap(view, new Vector3(3f, 0f, 0f), Vector3.right);

            // The sweep contributes its own tap to the shared counter.
            Assert.AreEqual(3, _projectile.Flight.TotalCruiseTaps,
                "shared counter = 2 (cruise) + 1 (sweep) — no double-count");
        }

        [Test]
        public void SweepTap_NeverSetsIsCruising()
        {
            var view = CreateSweepView();
            _projectile.Flight.SegmentPopCount = 1;
            _projectile.Flight.SegmentSweepValid = true;
            _projectile.Flight.LastBouncePosition = Vector3.zero;

            AwardSweepTap(view, new Vector3(3f, 0f, 0f), Vector3.right);

            Assert.IsFalse(_projectile.IsCruising.Value,
                "sweep taps contribute speed outside cruise — they never flip IsCruising on");
        }

        // ──────────────────────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────────────────────

        private ProjectileView CreateSweepView(int sweepTapThreshold = 0)
        {
            var gameObject = new GameObject(nameof(ProjectileView));
            _gameObjectsToDestroy.Add(gameObject);

            var projectileView = gameObject.AddComponent<ProjectileView>();
            var config = Substitute.For<IProjectileFlightConfig>();
            config.SweepEnabled.Returns(true);
            config.CruiseSpeedPerShield.Returns(0.5f);
            config.CruisePiercingTapThreshold.Returns(0);
            config.SweepTapThreshold.Returns(sweepTapThreshold);

            var visual = Substitute.For<IProjectileVisualConfig>();

            SetField(projectileView, "_flightConfig", config);
            SetField(projectileView, "_visual", visual);
            SetField(projectileView, "_model", _projectile);
            SetField(projectileView, "_contactRadius", 0.1f);
            SetStaticField(typeof(ProjectileView), "BalloonsLayer", 0);
            return projectileView;
        }

        private static void AwardSweepTap(ProjectileView projectileView, Vector3 wallHitPosition, Vector3 travelDirection)
        {
            typeof(ProjectileView)
                .GetMethod("TryAwardSweepTap", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(projectileView, new object[] { wallHitPosition, travelDirection });
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }

        private static void SetStaticField(Type targetType, string fieldName, object value)
        {
            targetType
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)!
                .SetValue(null, value);
        }
    }
}
