using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Palette;
using BalloonParty.Item.Paint;
using BalloonParty.Slots.Actor;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Item
{
    /// <summary>
    ///     Who paint touches, shared by the Paint item and the disco ball's wave: a concrete balloon takes it, one
    ///     already that colour is skipped, and a rainbow balloon or a paint-immune one keeps its colour.
    /// </summary>
    [TestFixture]
    public class PaintTargetsTests
    {
        private IGamePalette _palette;

        [SetUp]
        public void SetUp()
        {
            _palette = Substitute.For<IGamePalette>();
            _palette.IsRainbow(GamePalette.RainbowColorId).Returns(true);
        }

        [Test]
        public void Classify_ConcreteBalloonOfAnotherColour_AcceptsAndHandsBackThePaintable()
        {
            var balloon = Balloon("Red");

            var verdict = PaintTargets.Classify(balloon, "Blue", _palette, out var paintable);

            Assert.AreEqual(PaintVerdict.Accept, verdict);
            Assert.AreSame(balloon, paintable);
        }

        [Test]
        public void Classify_BalloonAlreadyThePaintColour_IsIgnored()
        {
            var verdict = PaintTargets.Classify(Balloon("Red"), "Red", _palette, out var paintable);

            Assert.AreEqual(PaintVerdict.Ignore, verdict);
            Assert.IsNull(paintable);
        }

        [Test]
        public void Classify_RainbowBalloonUnderConcretePaint_Rejects()
        {
            var verdict = PaintTargets.Classify(
                Balloon(GamePalette.RainbowColorId), "Blue", _palette, out var paintable);

            Assert.AreEqual(PaintVerdict.Reject, verdict);
            Assert.IsNull(paintable);
        }

        [Test]
        public void Classify_RainbowBalloonUnderRainbowPaint_IsIgnored_NotRejected()
        {
            var verdict = PaintTargets.Classify(
                Balloon(GamePalette.RainbowColorId), GamePalette.RainbowColorId, _palette, out _);

            Assert.AreEqual(PaintVerdict.Ignore, verdict);
        }

        [Test]
        public void Classify_PaintImmuneBalloon_Rejects()
        {
            var tough = new ToughBalloonModel(new BalloonModelConfig(BalloonType.Tough, hitsToPop: 3));

            var verdict = PaintTargets.Classify(tough, "Blue", _palette, out var paintable);

            Assert.AreEqual(PaintVerdict.Reject, verdict);
            Assert.IsNull(paintable);
        }

        [Test]
        public void Classify_ActorThatIsNeitherPaintableNorImmune_IsIgnored()
        {
            var verdict = PaintTargets.Classify(Substitute.For<ISlotActor>(), "Blue", _palette, out var paintable);

            Assert.AreEqual(PaintVerdict.Ignore, verdict);
            Assert.IsNull(paintable);
        }

        private static BalloonModel Balloon(string color)
        {
            var model = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            model.Color.Value = color;
            return model;
        }
    }
}
