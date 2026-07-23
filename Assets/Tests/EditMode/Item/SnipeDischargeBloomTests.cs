using System;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Item.Snipe;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    [TestFixture]
    public class SnipeDischargeBloomTests
    {
        private SlotGrid _grid;
        private SnipeDischargeBloom _bloom;
        private IMessageHandler<PierceDischargedMessage> _dischargedHandler;

        [SetUp]
        public void SetUp()
        {
            var gameConfig = Substitute.For<ISlotGridConfig>();
            gameConfig.SlotsSize.Returns(new Vector2Int(6, 10));
            gameConfig.SlotSeparation.Returns(new Vector2(1f, 0.85f));
            gameConfig.SlotsOffset.Returns(new Vector2(2.5f, 4f));
            _grid = new SlotGrid(gameConfig, new BalancePathHolder());

            // Authored bloom tuning: radius = min(1 + charge*0.5, 4), charge = toughCount*1. Two plowed
            // toughs therefore bloom a radius of 2 (well under the cap).
            var itemConfig = Substitute.For<IItemConfiguration>();
            itemConfig[ItemType.Snipe].Returns(CreateSnipeSettings(
                chargePerToughHit: 1, bloomBaseRadius: 1f, bloomRadiusPerCharge: 0.5f, bloomRadiusCap: 4f));

            var dischargedSubscriber = Substitute.For<ISubscriber<PierceDischargedMessage>>();
            dischargedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<PierceDischargedMessage>>(h => _dischargedHandler = h),
                    Arg.Any<MessageHandlerFilter<PierceDischargedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            _bloom = new SnipeDischargeBloom(dischargedSubscriber, itemConfig, _grid);
            _bloom.Start();
        }

        [Test]
        public void OnDischarged_Rainbow_ConvertsPaintableWithinChargeScaledRadius()
        {
            // Centre is the midpoint of a two-tough plow at (2,4)/(2,6) — matches the resolver's centroid
            // math. Two toughs → charge 2 → radius 2 (radiusSq 4).
            var center = new Vector3(1f, -0.25f, 0f);

            // (2,5) sits 1.0² from the centre — inside even the base radius.
            var wellInside = PlaceBalloon(new Vector2Int(2, 5), "Red");
            // (2,7) sits 3.89² from the centre — outside the base radius (1²) but inside the charge-widened
            // radius (2²). It only blooms because the charge grew the radius past the base.
            var chargeWidenedInside = PlaceBalloon(new Vector2Int(2, 7), "Red");
            // (3,6) sits 4.72² from the centre — just beyond the radius; the charge doesn't reach it.
            var justOutside = PlaceBalloon(new Vector2Int(3, 6), "Blue");

            _dischargedHandler.Handle(new PierceDischargedMessage(center, toughCount: 2, isRainbow: true));

            Assert.AreEqual(GamePalette.RainbowColorId, wellInside.Color.Value);
            Assert.AreEqual(GamePalette.RainbowColorId, chargeWidenedInside.Color.Value);
            Assert.AreEqual("Blue", justOutside.Color.Value, "a balloon past the bloom radius is untouched");
        }

        [Test]
        public void OnDischarged_Rainbow_SkipsNonPaintableActorWithinRadius()
        {
            var center = new Vector3(1f, -0.25f, 0f);

            // Sitting right at the centre — it would bloom if it were paintable, but a tough isn't
            // IPaintable, so it must be skipped without interrupting the scan.
            var tough = new ToughBalloonModel(new BalloonModelConfig(hitsToPop: 2));
            _grid.Place(tough, null, new Vector2Int(2, 5));
            // Also within radius, further along the scan — proves the tough's skip doesn't short-circuit
            // conversion of the balloons after it.
            var stillConverted = PlaceBalloon(new Vector2Int(2, 4), "Red");

            Assert.DoesNotThrow(() =>
                _dischargedHandler.Handle(new PierceDischargedMessage(center, toughCount: 2, isRainbow: true)));

            Assert.AreEqual(GamePalette.RainbowColorId, stillConverted.Color.Value);
        }

        [Test]
        public void OnDischarged_NotRainbow_ConvertsNothing()
        {
            var center = new Vector3(1f, -0.25f, 0f);

            // Sitting right at the discharge centre — it would bloom under a rainbow lance, but a plain
            // piercing discharge never converts.
            var atCentre = PlaceBalloon(new Vector2Int(2, 5), "Red");

            _dischargedHandler.Handle(new PierceDischargedMessage(center, toughCount: 2, isRainbow: false));

            Assert.AreEqual("Red", atCentre.Color.Value);
        }

        private BalloonModel PlaceBalloon(Vector2Int slot, string color)
        {
            var model = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            model.Color.Value = color;
            _grid.Place(model, null, slot);
            return model;
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
