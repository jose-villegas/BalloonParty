using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.Extensions;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Shared
{
    [TestFixture]
    public class BalloonPopColorTests
    {
        private IGamePalette _palette;

        [SetUp]
        public void SetUp()
        {
            _palette = Substitute.For<IGamePalette>();
            _palette.Colors.Returns(new List<PaletteEntry>());
        }

        [Test]
        public void GetPopColorId_ColoredBalloon_ReturnsOwnColor()
        {
            var model = new BalloonModel();
            model.Color.Value = "Red";

            Assert.AreEqual("Red", model.GetPopColorId());
        }

        [Test]
        public void GetPopColorId_Tough_ReturnsToughImpactColor()
        {
            var model = new ToughBalloonModel(
                new BalloonModelConfig(typeName: BalloonType.Tough, scoreValue: 7), _palette);

            Assert.AreEqual(GamePalette.ToughColorId, model.GetPopColorId());
        }

        [Test]
        public void GetPopColorId_Unbreakable_ReturnsSparksImpactColor()
        {
            var model = new UnbreakableBalloonModel(
                new BalloonModelConfig(typeName: BalloonType.Unbreakable));

            Assert.AreEqual(GamePalette.SparksColorId, model.GetPopColorId());
        }

        [Test]
        public void GetImpactColorId_Tough_IsToughEntry()
        {
            var model = new ToughBalloonModel(
                new BalloonModelConfig(typeName: BalloonType.Tough, scoreValue: 7), _palette);

            Assert.AreEqual(GamePalette.ToughColorId, model.GetImpactColorId());
        }

        [Test]
        public void GetImpactColorId_Unbreakable_IsSparksEntry()
        {
            var model = new UnbreakableBalloonModel(
                new BalloonModelConfig(typeName: BalloonType.Unbreakable));

            Assert.AreEqual(GamePalette.SparksColorId, model.GetImpactColorId());
        }
    }
}
