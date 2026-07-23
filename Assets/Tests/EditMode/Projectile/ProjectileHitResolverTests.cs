using System;
using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Score;
using BalloonParty.Projectile.Buffs;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Projectile.View;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Projectile
{
    [TestFixture]
    public class ProjectileHitResolverTests
    {
        private IHitDispatcher _hitDispatcher;
        private IPublisher<ShieldGainedMessage> _shieldGainedPublisher;
        private IPublisher<PierceDischargedMessage> _dischargedPublisher;
        private ColorStreakTracker _streakTracker;
        private SlotGrid _grid;
        private ProjectileHitResolver _resolver;
        private ProjectileModel _projectile;
        private readonly List<GameObject> _gameObjectsToDestroy = new();

        [SetUp]
        public void SetUp()
        {
            _hitDispatcher = Substitute.For<IHitDispatcher>();
            _shieldGainedPublisher = Substitute.For<IPublisher<ShieldGainedMessage>>();
            _dischargedPublisher = Substitute.For<IPublisher<PierceDischargedMessage>>();

            var gameConfig = Substitute.For<ISlotGridConfig>();
            gameConfig.SlotsSize.Returns(new Vector2Int(6, 10));
            gameConfig.SlotSeparation.Returns(new Vector2(1f, 0.85f));
            gameConfig.SlotsOffset.Returns(new Vector2(2.5f, 4f));
            _grid = new SlotGrid(gameConfig, new BalancePathHolder());

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

            _streakTracker = new ColorStreakTracker(
                Substitute.For<IPublisher<StreakChangedMessage>>(), levelUpSubscriber, projectileLoadedSubscriber);

            _resolver = new ProjectileHitResolver(
                _hitDispatcher, _shieldGainedPublisher, _dischargedPublisher, _streakTracker, _grid);
            _projectile = new ProjectileModel { IsFree = true };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in _gameObjectsToDestroy)
            {
                if (gameObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(gameObject);
                }
            }

            _gameObjectsToDestroy.Clear();
        }

        [Test]
        public void Resolve_AbsorbingBalloon_ReturnsDestroyed()
        {
            var result = _resolver.Resolve(_projectile, AbsorbingBalloon(), Vector3.zero);

            Assert.AreEqual(ProjectileHitVisual.Destroyed, result);
        }

        [Test]
        public void Resolve_AbsorbingBalloon_SetsProjectileNotFree()
        {
            _resolver.Resolve(_projectile, AbsorbingBalloon(), Vector3.zero);

            Assert.IsFalse(_projectile.IsFree);
        }

        [Test]
        public void Resolve_AbsorbingBalloon_DispatchesActorHitWithAbsorbOutcome()
        {
            var balloon = AbsorbingBalloon();

            _resolver.Resolve(_projectile, balloon, Vector3.zero);

            _hitDispatcher.Received(1).Dispatch(Arg.Is<ActorHitMessage>(m =>
                m.Actor == balloon && m.Outcome == HitOutcome.Absorb));
        }

        [Test]
        public void Resolve_AnyBalloonContact_EndsCruiseAndResetsBounceCount()
        {
            _projectile.Flight.ConsecutiveWallBounces = 4;
            _projectile.IsCruising.Value = true;

            _resolver.Resolve(_projectile, new BalloonModel(new BalloonModelConfig(hitsToPop: 1)), Vector3.zero);

            Assert.IsFalse(_projectile.IsCruising.Value, "balloon contact ends the cruise");
            Assert.AreEqual(0, _projectile.Flight.ConsecutiveWallBounces, "counter restarts on any contact");
        }

        [Test]
        public void Resolve_PiercingBuff_KeepsCruisingThroughContact()
        {
            _projectile.IsPiercing.Value = true;
            _projectile.Flight.ConsecutiveWallBounces = 4;
            _projectile.IsCruising.Value = true;

            _resolver.Resolve(_projectile, new BalloonModel(new BalloonModelConfig(hitsToPop: 1)), Vector3.zero);

            Assert.IsTrue(_projectile.IsCruising.Value, "piercing rides the cruise through the pop");
            Assert.AreEqual(4, _projectile.Flight.ConsecutiveWallBounces, "bounce counter isn't reset while piercing");
        }

        [Test]
        public void Resolve_PiercingTough_RecordsWithoutPopping()
        {
            _projectile.IsPiercing.Value = true;

            // A 2-hit tough is a >1-hit actor — a piercing shot plows through it, recording the strike
            // for the discharge instead of popping it on contact.
            var result = _resolver.Resolve(
                _projectile, new BalloonModel(new BalloonModelConfig(hitsToPop: 2)), new Vector3(1f, 2f, 0f));

            Assert.AreEqual(ProjectileHitVisual.None, result);
            Assert.AreEqual(1, _projectile.Flight.PendingPierceHits.Count, "the plowed tough is recorded");
            Assert.AreEqual(new Vector3(1f, 2f, 0f), _projectile.Flight.PendingPierceHits[0].Position);
            _hitDispatcher.DidNotReceive().Dispatch(Arg.Any<ActorHitMessage>());
        }

        [Test]
        public void Resolve_PiercingNormal_PopsOnContactWithoutRecording()
        {
            _projectile.IsPiercing.Value = true;
            var balloon = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            balloon.Color.Value = "Red";

            _resolver.Resolve(_projectile, balloon, Vector3.zero);

            Assert.AreEqual(0, _projectile.Flight.PendingPierceHits.Count, "a normal balloon pops, it isn't recorded");
            _hitDispatcher.Received(1).Dispatch(Arg.Is<ActorHitMessage>(m =>
                m.Actor == balloon && m.Outcome == HitOutcome.Pop));
        }

        [Test]
        public void Resolve_Pop_IncrementsSegmentPopCount()
        {
            var balloon = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            balloon.Color.Value = "Red";

            _resolver.Resolve(_projectile, balloon, Vector3.zero);

            Assert.AreEqual(1, _projectile.Flight.SegmentPopCount);
            Assert.IsTrue(_projectile.Flight.SegmentSweepValid,
                "a 1HP one-shot pop keeps the segment sweep-eligible");
        }

        [Test]
        public void Resolve_AbsorbingBalloon_DoesNotIncrementSegmentPopCount()
        {
            // An absorb kills the projectile on contact but is NOT a pop — it must not
            // increment SegmentPopCount, or a Sweep tap would be falsely awarded at the next wall.
            _resolver.Resolve(_projectile, AbsorbingBalloon(), Vector3.zero);

            Assert.AreEqual(0, _projectile.Flight.SegmentPopCount);
        }

        [Test]
        public void Resolve_PiercingTough_DoesNotIncrementSegmentPopCount()
        {
            // A piercing shot plows through a tough: the tough is recorded for the discharge
            // but NOT popped on contact. SegmentPopCount must stay 0 — the tough is still live
            // in the corridor at the moment of the wall hit, so no Sweep tap should be awarded.
            _projectile.IsPiercing.Value = true;
            var tough = new BalloonModel(new BalloonModelConfig(hitsToPop: 2));

            _resolver.Resolve(_projectile, tough, Vector3.zero);

            Assert.AreEqual(0, _projectile.Flight.SegmentPopCount,
                "plowed toughs are recorded for discharge, not counted as segment pops");
            Assert.IsFalse(_projectile.Flight.SegmentSweepValid,
                "a >1HP contact invalidates Sweep even if the pierce later discharges it");
        }

        [Test]
        public void Resolve_AllOneHitContactsOnSegment_AwardsSweepTapUsingCruiseSpeed()
        {
            var projectileView = CreateSweepView(cruiseSpeedPerShield: 0.25f);
            _projectile.Flight.LastBouncePosition = Vector3.zero;
            _projectile.Flight.CruiseTapElapsed = 99f;

            _resolver.Resolve(_projectile, CreateBalloon(hitsToPop: 1, "Red"), Vector3.zero);
            _resolver.Resolve(_projectile, CreateBalloon(hitsToPop: 1, "Blue"), Vector3.zero);

            AwardSweepTap(projectileView, new Vector3(2f, 0f, 0f), Vector3.right);

            Assert.AreEqual(1, _projectile.Flight.TotalCruiseTaps,
                "a valid sweep should bank one shared cruise tap");
            Assert.AreEqual(0f, _projectile.Flight.CruiseTapElapsed,
                "a sweep tap should replay the same tap-beat ease from t=0");
        }

        [Test]
        public void Resolve_MultiHitContactOnSegment_DoesNotAwardSweepTap()
        {
            var projectileView = CreateSweepView(cruiseSpeedPerShield: 0.25f);
            _projectile.Flight.LastBouncePosition = Vector3.zero;
            _projectile.Flight.CruiseTapElapsed = 99f;

            // The first contact struck a 2-HP balloon, so this segment is NOT a sweep even if a later
            // 1-HP balloon pops and the corridor is otherwise clear at the wall.
            _resolver.Resolve(_projectile, CreateBalloon(hitsToPop: 2, "Red"), Vector3.zero);
            _resolver.Resolve(_projectile, CreateBalloon(hitsToPop: 1, "Blue"), Vector3.zero);

            AwardSweepTap(projectileView, new Vector3(2f, 0f, 0f), Vector3.right);

            Assert.AreEqual(0, _projectile.Flight.TotalCruiseTaps,
                "any >1-HP contact on the segment should invalidate the sweep tap");
            Assert.AreEqual(99f, _projectile.Flight.CruiseTapElapsed, 1e-4f,
                "no sweep awarded means the shared tap-beat should not restart");
        }

        [Test]
        public void DischargePending_PopsRecordedToughs()
        {
            _projectile.IsPiercing.Value = true;
            var tough = new BalloonModel(new BalloonModelConfig(hitsToPop: 2));
            _grid.Place(tough, Substitute.For<ISlotActorView>(), new Vector2Int(2, 3));

            _resolver.Resolve(_projectile, tough, new Vector3(1f, 2f, 0f));
            Assert.AreEqual(1, _projectile.Flight.PendingPierceHits.Count);

            _resolver.DischargePending(_projectile);

            _hitDispatcher.Received(1).Dispatch(Arg.Is<ActorHitMessage>(m =>
                m.Actor == tough && m.Outcome == HitOutcome.Pop));
            Assert.AreEqual(0, _projectile.Flight.PendingPierceHits.Count, "the discharge clears the pending set");
        }

        [Test]
        public void DischargePending_SkipsToughThatLeftTheBoard()
        {
            _projectile.IsPiercing.Value = true;
            // Recorded but never placed in the grid — it "left the board" before the discharge.
            var tough = new BalloonModel(new BalloonModelConfig(hitsToPop: 2));
            tough.SlotIndex.Value = new Vector2Int(2, 3);
            _resolver.Resolve(_projectile, tough, Vector3.zero);

            _resolver.DischargePending(_projectile);

            _hitDispatcher.DidNotReceive().Dispatch(Arg.Any<ActorHitMessage>());
        }

        [Test]
        public void DischargePending_RainbowCapturedAtPlow_MessageIsRainbowEvenAfterBuffGone()
        {
            // The real discharge ends the pierce, dropping the RainbowShield buff BEFORE it resolves, so
            // the published IsRainbow must key off the rainbow captured when the tough was plowed — not
            // the live buff (the bloom listener downstream depends on this).
            _projectile.IsPiercing.Value = true;
            var rainbow = ApplyRainbowBuff();
            PlaceAndRecordTough(new Vector2Int(2, 4));

            _projectile.RemoveBuff(rainbow);
            Assert.IsFalse(_projectile.HasBuff(ProjectileBuffId.RainbowShield), "buff gone, as at a real discharge");

            _resolver.DischargePending(_projectile);

            // IsRainbow must reflect the rainbow captured at plow time, not HasBuff at discharge.
            _dischargedPublisher.Received(1).Publish(Arg.Is<PierceDischargedMessage>(m => m.IsRainbow));
        }

        [Test]
        public void DischargePending_RainbowBuff_PublishesMessageWithPlowCentroidAndToughCount()
        {
            _projectile.IsPiercing.Value = true;
            ApplyRainbowBuff();

            // Two plowed toughs, recorded at their own slot positions, so the plow centres on (1.0, -0.25)
            // — the midpoint of the line.
            PlaceAndRecordTough(new Vector2Int(2, 4));
            PlaceAndRecordTough(new Vector2Int(2, 6));
            Assert.AreEqual(2, _projectile.Flight.PendingPierceHits.Count);

            _resolver.DischargePending(_projectile);

            _dischargedPublisher.Received(1).Publish(Arg.Is<PierceDischargedMessage>(m =>
                m.Center == new Vector3(1f, -0.25f, 0f) && m.ToughCount == 2 && m.IsRainbow));
        }

        [Test]
        public void DischargePending_NoRainbowBuff_PublishesMessageWithIsRainbowFalse()
        {
            _projectile.IsPiercing.Value = true;

            PlaceAndRecordTough(new Vector2Int(2, 4));
            PlaceAndRecordTough(new Vector2Int(2, 6));

            _resolver.DischargePending(_projectile);

            _dischargedPublisher.Received(1).Publish(Arg.Is<PierceDischargedMessage>(m =>
                !m.IsRainbow && m.ToughCount == 2));
        }

        [Test]
        public void Resolve_PopNormalBalloon_StealsColour()
        {
            var balloon = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            balloon.Color.Value = "Red";
            _projectile.ColorName.Value = "Blue";

            var result = _resolver.Resolve(_projectile, balloon, Vector3.zero);

            Assert.AreEqual("Red", _projectile.ColorName.Value);
            Assert.AreEqual(ProjectileHitVisual.Recolored, result);
        }

        [Test]
        public void Resolve_HitSoapBubble_WashesProjectileColourToNone()
        {
            var soap = new BubbleClusterModel(new BalloonModelConfig(hitsToPop: 1), Substitute.For<IGamePalette>());
            _projectile.ColorName.Value = "Red";

            _resolver.Resolve(_projectile, soap, Vector3.zero);

            Assert.IsTrue(string.IsNullOrEmpty(_projectile.ColorName.Value));
        }

        [Test]
        public void Resolve_PopRainbowBalloon_DoesNotStealColour()
        {
            var balloon = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            balloon.Color.Value = GamePalette.RainbowColorId;
            _projectile.ColorName.Value = "Blue";

            var result = _resolver.Resolve(_projectile, balloon, Vector3.zero);

            Assert.AreEqual("Blue", _projectile.ColorName.Value);
            Assert.AreEqual(ProjectileHitVisual.None, result);
        }

        [Test]
        public void Resolve_RainbowPop_StreakAtTwo_GrantsShield()
        {
            // Seeds the tracker as if a prior pop already ran through ScoreController's HitPipeline
            // stage — the shield check reads the tracker directly, same as production (see Resolve's
            // "Dispatch runs the streak stage synchronously" comment); _hitDispatcher is a substitute.
            _streakTracker.Record("Blue", false);
            _streakTracker.Record("Blue", false);

            var balloon = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            balloon.Color.Value = GamePalette.RainbowColorId;
            _projectile.ColorName.Value = "Blue";

            _resolver.Resolve(_projectile, balloon, Vector3.zero);

            Assert.AreEqual(1, _projectile.ShieldsRemaining.Value);
            Assert.AreEqual("Blue", _projectile.ColorName.Value); // unchanged — no steal
        }

        [Test]
        public void Resolve_RainbowBuffPop_DispatchesWildcardStreakAndPiercingFlags()
        {
            ApplyRainbowBuff();
            var balloon = PlaceBalloon(new Vector2Int(2, 2), "Red");

            _resolver.Resolve(_projectile, balloon, Vector3.zero);

            _hitDispatcher.Received().Dispatch(Arg.Is<ActorHitMessage>(m =>
                m.Outcome == HitOutcome.Pop
                && m.Context.Flags.HasFlag(DamageFlags.WildcardStreak)
                && m.Context.Flags.HasFlag(DamageFlags.Piercing)));
        }

        [Test]
        public void Resolve_RainbowBuff_PiercesMultiHitBalloon()
        {
            ApplyRainbowBuff();
            var tough = new BalloonModel(new BalloonModelConfig(hitsToPop: 3));
            tough.Color.Value = "Red";
            _grid.Place(tough, null, new Vector2Int(2, 2));

            _resolver.Resolve(_projectile, tough, Vector3.zero);

            // Without the buff a 3-hit balloon would survive one hit; piercing one-shots it.
            _hitDispatcher.Received().Dispatch(Arg.Is<ActorHitMessage>(m =>
                m.Actor == tough && m.Outcome == HitOutcome.Pop));
        }

        [Test]
        public void Resolve_RainbowBuffPop_ConvertsNeighboursToRainbow()
        {
            ApplyRainbowBuff();
            var hit = PlaceBalloon(new Vector2Int(2, 2), "Red");
            var neighbour = PlaceBalloon(new Vector2Int(1, 2), "Blue"); // a hex neighbour of (2,2)

            _resolver.Resolve(_projectile, hit, Vector3.zero);

            Assert.AreEqual(GamePalette.RainbowColorId, neighbour.Color.Value);
        }

        [Test]
        public void Resolve_NoBuff_LeavesNeighboursUnchanged()
        {
            var hit = PlaceBalloon(new Vector2Int(2, 2), "Red");
            var neighbour = PlaceBalloon(new Vector2Int(1, 2), "Blue");

            _resolver.Resolve(_projectile, hit, Vector3.zero);

            Assert.AreEqual("Blue", neighbour.Color.Value);
        }

        private ProjectileBuff ApplyRainbowBuff()
        {
            var wallBounces = Substitute.For<ISubscriber<ShieldLostMessage>>();
            wallBounces
                .Subscribe(
                    Arg.Any<IMessageHandler<ShieldLostMessage>>(),
                    Arg.Any<MessageHandlerFilter<ShieldLostMessage>[]>())
                .Returns(Substitute.For<IDisposable>());
            var buff = new ProjectileBuff(
                ProjectileBuffId.RainbowShield, 0f, BuffModifierOp.Flat,
                new WallBounceEndCondition(wallBounces));
            _projectile.AddBuff(buff);
            return buff;
        }

        private ProjectileView CreateSweepView(float cruiseSpeedPerShield)
        {
            var gameObject = new GameObject(nameof(ProjectileView));
            _gameObjectsToDestroy.Add(gameObject);

            var projectileView = gameObject.AddComponent<ProjectileView>();
            var config = Substitute.For<IProjectileFlightConfig>();
            config.SweepEnabled.Returns(true);
            config.CruiseSpeedPerShield.Returns(cruiseSpeedPerShield);
            config.CruisePiercingTapThreshold.Returns(0);

            SetField(projectileView, "_flightConfig", config);
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

        private BalloonModel PlaceBalloon(Vector2Int slot, string color)
        {
            var model = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            model.Color.Value = color;
            _grid.Place(model, null, slot);
            return model;
        }

        // Places a 2-hit tough at the slot and plows it with a piercing shot, recording the strike at the
        // slot's own world position so the discharge centroid is the geometric midpoint of the plowed line.
        private void PlaceAndRecordTough(Vector2Int slot)
        {
            var tough = new BalloonModel(new BalloonModelConfig(hitsToPop: 2));
            _grid.Place(tough, null, slot);
            _resolver.Resolve(_projectile, tough, _grid.IndexToWorldPosition(slot));
        }

        private static BalloonModel CreateBalloon(int hitsToPop, string color)
        {
            var balloon = new BalloonModel(new BalloonModelConfig(hitsToPop: hitsToPop));
            balloon.Color.Value = color;
            return balloon;
        }

        private static IBalloonModel AbsorbingBalloon()
        {
            var balloon = Substitute.For<IBalloonModel>();
            balloon.EvaluateHit(Arg.Any<DamageContext>()).Returns(HitOutcome.Absorb);
            return balloon;
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
