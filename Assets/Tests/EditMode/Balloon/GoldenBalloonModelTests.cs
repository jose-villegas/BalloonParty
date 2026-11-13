using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.Extensions;
using BalloonParty.Slots.Capabilities;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     The golden reward balloon: only the shot's own pierce pops it; the shot's ordinary hit bounces off, and
    ///     no item (bomb, laser, lightning) touches it, however their damage is flagged.
    /// </summary>
    [TestFixture]
    public class GoldenBalloonModelTests
    {
        private GoldenBalloonModel _model;

        [SetUp]
        public void SetUp()
        {
            _model = new GoldenBalloonModel(new BalloonModelConfig(BalloonType.Golden, scoreValue: 0));
        }

        [Test]
        public void TheShotsOrdinaryHit_Deflects()
        {
            Assert.AreEqual(HitOutcome.Deflect, _model.EvaluateHit(new DamageContext(1, DamageFlags.DirectHit)));
        }

        [Test]
        public void TheShotsOwnPierce_Pops()
        {
            var pierce = new DamageContext(1, DamageFlags.Piercing | DamageFlags.DirectHit);

            Assert.AreEqual(HitOutcome.Pop, _model.EvaluateHit(pierce));
        }

        [Test]
        public void ARainbowPierce_Pops()
        {
            var flags = DamageFlags.Piercing | DamageFlags.DirectHit | DamageFlags.WildcardStreak;

            Assert.AreEqual(HitOutcome.Pop, _model.EvaluateHit(new DamageContext(1, flags)));
        }

        [TestCase(DamageFlags.Normal, TestName = "AnItemsOrdinaryDamage_DoesNothing")]
        [TestCase(DamageFlags.Piercing, TestName = "ABombsNeighbourOrALasersPiercingDamage_DoesNothing")]
        public void ItemDamage_DoesNothing(DamageFlags flags)
        {
            Assert.AreEqual(HitOutcome.None, _model.EvaluateHit(new DamageContext(99, flags)));
        }

        [Test]
        public void IsTough_SoAPiercingShotPlowsItAndShattersItAtTheWall()
        {
            Assert.IsTrue(_model.IsTough());
        }

        [Test]
        public void DeflectsShots_ResistsPaint_AndIsColourless()
        {
            Assert.IsTrue(_model is IDeflectsShots { DeflectsOrdinaryHit: true });
            Assert.IsTrue(_model is IResistsPaint);
            Assert.IsFalse(_model is IPaintable);
            Assert.AreEqual("", _model.GetColorId());
        }

        [Test]
        public void ScoresNothing_AndCarriesNoItem()
        {
            Assert.IsFalse(_model is IHasScore);
            Assert.IsFalse(_model is IHasScoreColor);
            Assert.IsFalse(_model is IHasItemSlot);
        }

        [Test]
        public void PopsAndImpactsInSparks()
        {
            Assert.AreEqual(GamePalette.SparksColorId, _model.GetImpactColorId());
            Assert.AreEqual(GamePalette.SparksColorId, _model.GetPopColorId());
        }

        [Test]
        public void TheFactory_BuildsAGoldenBalloonForItsEntry()
        {
            var entry = new BalloonPrefabEntry();
            typeof(BalloonPrefabEntry).GetField("_balloonType", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(entry, BalloonType.Golden);
            var palette = Substitute.For<IGamePalette>();
            palette.Colors.Returns(new List<PaletteEntry>());

            var model = BalloonModelFactory.Create(entry, palette);

            Assert.IsInstanceOf<GoldenBalloonModel>(model);
            Assert.AreEqual(BalloonType.Golden, model.TypeName);
        }
    }
}
