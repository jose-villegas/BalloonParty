using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.Extensions;
using BalloonParty.Slots.Capabilities;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     The heart reward balloon: any hit pops it at once, the shot's or an item's, and it stays out of the
    ///     colour game (no colour, no score, no paint).
    /// </summary>
    [TestFixture]
    public class HeartBalloonModelTests
    {
        [TestCase(DamageFlags.DirectHit, TestName = "TheShotsOrdinaryHit_Pops")]
        [TestCase(DamageFlags.Piercing | DamageFlags.DirectHit, TestName = "TheShotsPierce_Pops")]
        [TestCase(DamageFlags.Normal, TestName = "AnItemsOrdinaryDamage_Pops")]
        [TestCase(DamageFlags.Piercing, TestName = "ABombsNeighbourOrALasersPiercingDamage_Pops")]
        public void AnyHit_Pops(DamageFlags flags)
        {
            var model = Heart();

            Assert.AreEqual(HitOutcome.Pop, model.EvaluateHit(new DamageContext(1, flags)));
            Assert.AreEqual(0, model.HitsRemaining.Value);
        }

        [Test]
        public void AnEntryAuthoredWithMoreHits_StillPopsToTheFirst()
        {
            var model = new HeartBalloonModel(new BalloonModelConfig(BalloonType.Heart, scoreValue: 0, hitsToPop: 3));

            Assert.AreEqual(HitOutcome.Pop, model.EvaluateHit(new DamageContext(1, DamageFlags.DirectHit)));
        }

        [Test]
        public void IsNotTough_SoAPiercingShotPopsItInPassing()
        {
            Assert.IsFalse(Heart().IsTough());
        }

        [Test]
        public void ResistsPaint_AndIsColourless()
        {
            var model = Heart();

            Assert.IsTrue(model is IResistsPaint);
            Assert.IsFalse(model is IPaintable);
            Assert.AreEqual("", model.GetColorId());
            Assert.AreEqual("", model.GetPopColorId());
        }

        [Test]
        public void ScoresNothing_AndCarriesNoItem()
        {
            var model = Heart();

            Assert.IsFalse(model is IHasScore);
            Assert.IsFalse(model is IHasScoreColor);
            Assert.IsFalse(model is IHasItemSlot);
        }

        [Test]
        public void TheFactory_BuildsAHeartBalloonForItsEntry()
        {
            var entry = new BalloonPrefabEntry();
            typeof(BalloonPrefabEntry).GetField("_balloonType", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(entry, BalloonType.Heart);
            var palette = Substitute.For<IGamePalette>();
            palette.Colors.Returns(new List<PaletteEntry>());

            var model = BalloonModelFactory.Create(entry, palette);

            Assert.IsInstanceOf<HeartBalloonModel>(model);
            Assert.AreEqual(BalloonType.Heart, model.TypeName);
        }

        private static HeartBalloonModel Heart()
        {
            return new HeartBalloonModel(new BalloonModelConfig(BalloonType.Heart, scoreValue: 0));
        }
    }
}
