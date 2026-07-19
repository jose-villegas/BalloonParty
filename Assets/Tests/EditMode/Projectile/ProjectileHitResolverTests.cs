using System;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Score;
using BalloonParty.Projectile.Buffs;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
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
        private ColorStreakTracker _streakTracker;
        private SlotGrid _grid;
        private IItemConfiguration _itemConfig;
        private ProjectileHitResolver _resolver;
        private ProjectileModel _projectile;

        [SetUp]
        public void SetUp()
        {
            _hitDispatcher = Substitute.For<IHitDispatcher>();
            _shieldGainedPublisher = Substitute.For<IPublisher<ShieldGainedMessage>>();

            var gameConfig = Substitute.For<IGameConfiguration>();
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

            // Authored bloom tuning: radius = min(1 + charge*0.5, 4), charge = toughCount*1. Two plowed
            // toughs therefore bloom a radius of 2 (well under the cap). The non-bloom tests never reach
            // BloomConvert (no rainbow buff), so these values only bind the discharge-bloom cases.
            _itemConfig = Substitute.For<IItemConfiguration>();
            _itemConfig[ItemType.Snipe].Returns(CreateSnipeSettings(
                chargePerToughHit: 1, bloomBaseRadius: 1f, bloomRadiusPerCharge: 0.5f, bloomRadiusCap: 4f));

            _resolver = new ProjectileHitResolver(
                _hitDispatcher, _shieldGainedPublisher, _streakTracker, _grid, _itemConfig);
            _projectile = new ProjectileModel { IsFree = true };
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
            Assert.IsTrue(_projectile.Flight.DischargeArmed, "the plow arms the discharge debounce");
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
        public void DischargePending_BloomsFromRainbowCapturedAtPlow_EvenAfterBuffGone()
        {
            // The real discharge ends the pierce, dropping the RainbowShield buff BEFORE it resolves, so
            // the bloom must key off the rainbow captured when the tough was plowed — not the live buff.
            _projectile.IsPiercing.Value = true;
            var rainbow = ApplyRainbowBuff();
            PlaceAndRecordTough(new Vector2Int(2, 4));
            var target = PlaceBalloon(new Vector2Int(2, 5), "Red");

            _projectile.RemoveBuff(rainbow);
            Assert.IsFalse(_projectile.HasBuff(ProjectileBuffId.RainbowShield), "buff gone, as at a real discharge");

            _resolver.DischargePending(_projectile);

            Assert.AreEqual(GamePalette.RainbowColorId, target.Color.Value,
                "bloom uses the rainbow captured at plow time, not HasBuff at discharge");
        }

        [Test]
        public void DischargePending_RainbowBuff_BloomsPaintableWithinChargeScaledRadius()
        {
            _projectile.IsPiercing.Value = true;
            ApplyRainbowBuff();

            // Two plowed toughs, recorded at their own slot positions, so the bloom centres on (1.0, -0.25)
            // — the midpoint of the plowed line. Two toughs → charge 2 → radius 2 (radiusSq 4).
            PlaceAndRecordTough(new Vector2Int(2, 4));
            PlaceAndRecordTough(new Vector2Int(2, 6));
            Assert.AreEqual(2, _projectile.Flight.PendingPierceHits.Count);

            // (2,5) sits 1.0² from the centre — inside even the base radius.
            var wellInside = PlaceBalloon(new Vector2Int(2, 5), "Red");
            // (2,7) sits 3.89² from the centre — outside the base radius (1²) but inside the charge-widened
            // radius (2²). It only blooms because the charge grew the radius past the base.
            var chargeWidenedInside = PlaceBalloon(new Vector2Int(2, 7), "Red");
            // (3,6) sits 4.72² from the centre — just beyond the radius; the charge doesn't reach it.
            var justOutside = PlaceBalloon(new Vector2Int(3, 6), "Blue");

            _resolver.DischargePending(_projectile);

            Assert.AreEqual(GamePalette.RainbowColorId, wellInside.Color.Value);
            Assert.AreEqual(GamePalette.RainbowColorId, chargeWidenedInside.Color.Value);
            Assert.AreEqual("Blue", justOutside.Color.Value, "a balloon past the bloom radius is untouched");
        }

        [Test]
        public void DischargePending_NoRainbowBuff_DoesNotBloom()
        {
            _projectile.IsPiercing.Value = true;

            PlaceAndRecordTough(new Vector2Int(2, 4));
            PlaceAndRecordTough(new Vector2Int(2, 6));

            // Sitting right at the plow centre — it would bloom under a rainbow lance, but a plain
            // piercing discharge only shatters the recorded toughs, it never converts.
            var atCentre = PlaceBalloon(new Vector2Int(2, 5), "Red");

            _resolver.DischargePending(_projectile);

            Assert.AreEqual("Red", atCentre.Color.Value);
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

        private static IBalloonModel AbsorbingBalloon()
        {
            var balloon = Substitute.For<IBalloonModel>();
            balloon.EvaluateHit(Arg.Any<DamageContext>()).Returns(HitOutcome.Absorb);
            return balloon;
        }

        private static ItemSettings CreateSnipeSettings(
            int chargePerToughHit, float bloomBaseRadius, float bloomRadiusPerCharge, float bloomRadiusCap)
        {
            var settings = new ItemSettings();
            SetField(settings, "_type", ItemType.Snipe);

            var snipe = new SnipeSettings();
            SetField(snipe, "_snipeChargePerToughHit", chargePerToughHit);
            SetField(snipe, "_bloomBaseRadius", bloomBaseRadius);
            SetField(snipe, "_bloomRadiusPerCharge", bloomRadiusPerCharge);
            SetField(snipe, "_bloomRadiusCap", bloomRadiusCap);
            SetField(settings, "_snipe", snipe);
            return settings;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }
    }
}
