using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.Extensions;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     The nuke reward balloon (PLAN-NukeBalloon): plated like the golden balloon and the disco ball, and never
    ///     popped by a hit. The shot's ordinary hit dents it: it bounces off and costs a hit, and the last one arms it.
    ///     Piercing damage and a laser beam arm it at once. Armed, it deflects every direct hit, ignores the rest and
    ///     is anchored. An item's plain damage does nothing.
    /// </summary>
    [TestFixture]
    public class NukeBalloonModelTests
    {
        private static readonly DamageContext OrdinaryHit = new(1, DamageFlags.DirectHit);

        [Test]
        public void TheShotsOrdinaryHit_DeflectsAndCostsOneHit_WithoutArming()
        {
            var model = Nuke(3);

            Assert.AreEqual(HitOutcome.Deflect, model.EvaluateHit(OrdinaryHit));
            Assert.AreEqual(2, Hits(model));
            Assert.IsFalse(model.Armed.Value);
            Assert.IsFalse(model.IsAnchored);
        }

        [Test]
        public void TheThirdOrdinaryHit_ArmsIt_AndStillDeflects()
        {
            var model = Nuke(3);

            Assert.AreEqual(HitOutcome.Deflect, model.EvaluateHit(OrdinaryHit));
            Assert.AreEqual(HitOutcome.Deflect, model.EvaluateHit(OrdinaryHit));
            Assert.IsFalse(model.Armed.Value);
            Assert.AreEqual(HitOutcome.Deflect, model.EvaluateHit(OrdinaryHit));

            Assert.IsTrue(model.Armed.Value);
            Assert.AreEqual(0, Hits(model));
        }

        [Test]
        public void ADamageOfTwo_CostsTwoHits()
        {
            var model = Nuke(3);

            Assert.AreEqual(HitOutcome.Deflect, model.EvaluateHit(new DamageContext(2, DamageFlags.DirectHit)));
            Assert.AreEqual(1, Hits(model));
            Assert.IsFalse(model.Armed.Value);
        }

        [Test]
        public void ADamageBeyondTheHitsLeft_Arms()
        {
            var model = Nuke(3);

            Assert.AreEqual(HitOutcome.Deflect, model.EvaluateHit(new DamageContext(5, DamageFlags.DirectHit)));

            Assert.IsTrue(model.Armed.Value);
            Assert.AreEqual(0, Hits(model));
        }

        [Test]
        public void TheShotsOwnPierce_ArmsAtFullHits_AndBouncesOff()
        {
            var model = Nuke(3);

            var outcome = model.EvaluateHit(new DamageContext(1, DamageFlags.Piercing | DamageFlags.DirectHit));

            Assert.AreEqual(HitOutcome.Deflect, outcome);
            Assert.IsTrue(model.Armed.Value);
            Assert.AreEqual(0, Hits(model));
        }

        [TestCase(DamageFlags.Piercing | DamageFlags.Beam, TestName = "ALasersPiercingBeam_Arms")]
        [TestCase(DamageFlags.Beam, TestName = "ALaserBeamConfiguredWithoutPiercing_Arms")]
        public void ALaserBeam_Arms_AndIsNotAHitToBounceOff(DamageFlags flags)
        {
            var model = Nuke(3);

            Assert.AreEqual(HitOutcome.None, model.EvaluateHit(new DamageContext(1, flags)));
            Assert.IsTrue(model.Armed.Value);
            Assert.AreEqual(0, Hits(model));
        }

        // A bomb's blast on its neighbours carries Piercing and no DirectHit.
        [Test]
        public void ABombsNeighboursPiercingDamage_Arms_WithoutADeflection()
        {
            var model = Nuke(3);

            Assert.AreEqual(HitOutcome.None, model.EvaluateHit(new DamageContext(1, DamageFlags.Piercing)));
            Assert.IsTrue(model.Armed.Value);
        }

        // Lightning and paint deal plain item damage, which carries no DirectHit.
        [Test]
        public void AnItemsOrdinaryDamage_DoesNothing_AndCostsNoHit()
        {
            var model = Nuke(3);

            Assert.AreEqual(HitOutcome.None, model.EvaluateHit(new DamageContext(99, DamageFlags.Normal)));
            Assert.AreEqual(3, Hits(model));
            Assert.IsFalse(model.Armed.Value);
        }

        [Test]
        public void ArmingIt_AnchorsIt()
        {
            var model = Nuke(3);

            model.EvaluateHit(new DamageContext(1, DamageFlags.Piercing | DamageFlags.DirectHit));

            Assert.IsTrue(model is IBalanceAnchor);
            Assert.IsTrue(model.IsAnchored);
        }

        // Two reasons to hold its slot, and neither undoes the other.
        [Test]
        public void HeldByACover_ItIsAnchoredUnarmed_AndArmedItStaysAnchoredOnceLetGo()
        {
            var model = Nuke(3);
            Assert.IsFalse(model.IsAnchored);

            model.SetHeldByCover(true);
            Assert.IsTrue(model.IsAnchored, "held under a cover, unarmed");

            model.EvaluateHit(new DamageContext(1, DamageFlags.Piercing | DamageFlags.DirectHit));
            model.SetHeldByCover(false);
            Assert.IsTrue(model.IsAnchored, "armed, it holds its slot with no cover");
        }

        [TestCase(DamageFlags.DirectHit, TestName = "AnArmedNuke_DeflectsAnOrdinaryHit")]
        [TestCase(DamageFlags.Piercing | DamageFlags.DirectHit, TestName = "AnArmedNuke_DeflectsAPiercingHit")]
        public void AnArmedNuke_DeflectsEveryDirectHit_AndTakesNoDamage(DamageFlags flags)
        {
            var model = Armed();

            Assert.AreEqual(HitOutcome.Deflect, model.EvaluateHit(new DamageContext(3, flags)));
            Assert.AreEqual(0, Hits(model));
            Assert.IsTrue(model.Armed.Value);
        }

        [TestCase(DamageFlags.Normal, TestName = "AnArmedNuke_IgnoresItemDamage")]
        [TestCase(DamageFlags.Piercing, TestName = "AnArmedNuke_IgnoresABombsBlast")]
        [TestCase(DamageFlags.Beam, TestName = "AnArmedNuke_IgnoresALaserBeam")]
        public void AnArmedNuke_IgnoresEverythingThatIsNotADirectHit(DamageFlags flags)
        {
            var model = Armed();

            Assert.AreEqual(HitOutcome.None, model.EvaluateHit(new DamageContext(3, flags)));
            Assert.IsTrue(model.Armed.Value);
        }

        [Test]
        public void FuseSecondsLeft_StartsAtZero_AndNeverGoesBelowIt()
        {
            var model = Nuke(3);
            Assert.AreEqual(0, model.FuseSecondsLeft.Value);

            model.SetFuseSecondsLeft(2);
            Assert.AreEqual(2, model.FuseSecondsLeft.Value);

            model.SetFuseSecondsLeft(-4);
            Assert.AreEqual(0, model.FuseSecondsLeft.Value);
        }

        [TestCase(3, true)]
        [TestCase(2, true)]
        [TestCase(1, false)]
        public void DeflectsOrdinaryHit_OnlyWhileMoreThanOneHitIsLeft(int hits, bool deflects)
        {
            var model = Nuke(hits);

            Assert.AreEqual(deflects, ((IDeflectsShots)model).DeflectsOrdinaryHit);
        }

        [Test]
        public void DeflectsOrdinaryHit_TurnsOffAfterTheHitThatLeavesOne()
        {
            var model = Nuke(2);

            model.EvaluateHit(OrdinaryHit);

            Assert.IsFalse(((IDeflectsShots)model).DeflectsOrdinaryHit);
        }

        [TestCase(3, true)]
        [TestCase(2, true)]
        [TestCase(1, false)]
        public void IsTough_TracksTheHitsLeft(int hits, bool tough)
        {
            Assert.AreEqual(tough, Nuke(hits).IsTough());
        }

        [Test]
        public void IsAHasDurability_ReportingItsHits()
        {
            var model = Nuke(3);

            Assert.IsTrue(model is IHasDurability { MaxHitPoints: 3 });
            Assert.AreEqual(3, ((IHasDurability)model).HitsRemaining.Value);
        }

        [Test]
        public void ResistsPaint_AndIsColourless()
        {
            var model = Nuke(3);

            Assert.IsTrue(model is IResistsPaint);
            Assert.IsFalse(model is IPaintable);
            Assert.AreEqual("", model.GetColorId());
        }

        [Test]
        public void ScoresNothing_AndCarriesNoItem()
        {
            var model = Nuke(3);

            Assert.IsFalse(model is IHasScore);
            Assert.IsFalse(model is IHasScoreColor);
            Assert.IsFalse(model is IHasItemSlot);
        }

        [Test]
        public void PopsAndImpactsInSparks_ImpactHeavy()
        {
            var model = Nuke(3);

            Assert.AreEqual(GamePalette.SparksColorId, model.GetPopColorId());
            Assert.AreEqual(GamePalette.SparksColorId, model.GetImpactColorId());
        }

        [Test]
        public void IsAReward_SoNoLevelMixBringsIt()
        {
            Assert.IsTrue(BalloonType.Nuke.IsReward());
        }

        [Test]
        public void TheFactory_BuildsANukeModelForItsEntry()
        {
            var model = Nuke(3);

            Assert.IsInstanceOf<NukeBalloonModel>(model);
            Assert.AreEqual(BalloonType.Nuke, model.TypeName);
        }

        private static int Hits(IBalloonModel model)
        {
            return ((IHasDurability)model).HitsRemaining.Value;
        }

        private static NukeBalloonModel Armed()
        {
            var model = Nuke(3);
            model.EvaluateHit(new DamageContext(1, DamageFlags.Beam));
            return model;
        }

        private static NukeBalloonModel Nuke(int hitsToPop)
        {
            return (NukeBalloonModel)BalloonModelFactory.Create(Entry(hitsToPop), Palette());
        }

        private static BalloonPrefabEntry Entry(int hitsToPop)
        {
            var entry = new BalloonPrefabEntry();
            typeof(BalloonPrefabEntry).GetField("_balloonType", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(entry, BalloonType.Nuke);
            typeof(BalloonPrefabEntry).GetField("_hitsToPop", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(entry, hitsToPop);
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
