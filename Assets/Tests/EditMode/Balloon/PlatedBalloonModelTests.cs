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
    ///     The plated reward balloons, the golden balloon and the disco ball (PLAN-DiscoBalloon phase 1): they take
    ///     hits as an unbreakable does. Any piercing hit pops them (the shot's own pierce, a bomb's blast beside
    ///     them, a laser beam), the shot's ordinary hit bounces off, and an item's plain damage (lightning, paint)
    ///     does nothing. Each fixture runs once per plated type, so the two can't drift apart.
    /// </summary>
    [TestFixture(BalloonType.Golden)]
    [TestFixture(BalloonType.Disco)]
    public class PlatedBalloonModelTests
    {
        private readonly BalloonType _type;
        private IBalloonModel _model;

        public PlatedBalloonModelTests(BalloonType type)
        {
            _type = type;
        }

        [SetUp]
        public void SetUp()
        {
            _model = BalloonModelFactory.Create(Entry(_type), Palette());
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

        // Lightning and paint deal plain item damage.
        [Test]
        public void AnItemsOrdinaryDamage_DoesNothing()
        {
            Assert.AreEqual(HitOutcome.None, _model.EvaluateHit(new DamageContext(99, DamageFlags.Normal)));
        }

        // A bomb's blast on its neighbours carries Piercing and no DirectHit, and pops an unbreakable beside it.
        [Test]
        public void ABombsNeighboursPiercingDamage_Pops_AsItPopsAnUnbreakable()
        {
            var blast = new DamageContext(1, DamageFlags.Piercing);
            var unbreakable = new UnbreakableBalloonModel(new BalloonModelConfig());

            Assert.AreEqual(unbreakable.EvaluateHit(blast), _model.EvaluateHit(blast));
            Assert.AreEqual(HitOutcome.Pop, _model.EvaluateHit(blast));
        }

        // The laser's beam carries Piercing like a bomb's blast; Beam is what tells it apart.
        [TestCase(DamageFlags.Piercing | DamageFlags.Beam, TestName = "ALasersPiercingBeam_Pops")]
        [TestCase(DamageFlags.Beam, TestName = "ALaserBeamConfiguredWithoutPiercing_Pops")]
        public void ALaserBeam_Pops(DamageFlags flags)
        {
            Assert.AreEqual(HitOutcome.Pop, _model.EvaluateHit(new DamageContext(1, flags)));
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
        public void IsAReward_SoNoLevelMixBringsIt()
        {
            Assert.IsTrue(_type.IsReward());
        }

        [Test]
        public void TheFactory_BuildsItsOwnModelForItsEntry()
        {
            Assert.IsInstanceOf(_type == BalloonType.Golden ? typeof(GoldenBalloonModel) : typeof(DiscoBalloonModel),
                _model);
            Assert.AreEqual(_type, _model.TypeName);
        }

        private static BalloonPrefabEntry Entry(BalloonType type)
        {
            var entry = new BalloonPrefabEntry();
            typeof(BalloonPrefabEntry).GetField("_balloonType", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(entry, type);
            return entry;
        }

        private static IGamePalette Palette()
        {
            var palette = Substitute.For<IGamePalette>();
            palette.Colors.Returns(new List<PaletteEntry>());
            return palette;
        }
    }
}
