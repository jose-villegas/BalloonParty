using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.Extensions;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Actor;
using NUnit.Framework;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class BalloonModelTests
    {
        private const string Red = "Red";
        private const string Blue = "Blue";

        private BalloonModel _model;

        [SetUp]
        public void SetUp()
        {
            _model = new BalloonModel();
        }

        [Test]
        public void EvaluateHit_Survives_ReturnsPassThrough()
        {
            _model.HitsRemaining.Value = 3;

            Assert.AreEqual(HitOutcome.PassThrough, _model.EvaluateHit(new DamageContext(1)));
        }

        [Test]
        public void EvaluateHit_ExactKill_ReturnsPop()
        {
            _model.HitsRemaining.Value = 1;

            Assert.AreEqual(HitOutcome.Pop, _model.EvaluateHit(new DamageContext(1)));
        }

        [Test]
        public void EvaluateHit_Overkill_ReturnsPop()
        {
            _model.HitsRemaining.Value = 1;

            Assert.AreEqual(HitOutcome.Pop, _model.EvaluateHit(new DamageContext(5)));
        }

        [Test]
        public void EvaluateHit_ExactKillHighDamage_ReturnsPop()
        {
            _model.HitsRemaining.Value = 3;

            Assert.AreEqual(HitOutcome.Pop, _model.EvaluateHit(new DamageContext(3)));
        }

        [Test]
        public void BalloonModel_ImplementsIDynamicSlotActor()
        {
            Assert.IsTrue(_model is IDynamicSlotActor);
        }

        [Test]
        public void BalloonModel_ImplementsIHitable()
        {
            Assert.IsTrue(_model is IHitable);
        }

        [Test]
        public void BalloonModel_ImplementsIHasDurability()
        {
            Assert.IsTrue(_model is IHasDurability);
        }

        [Test]
        public void BalloonModel_EvaluateHit_IntermediateHit_ReturnsPassThrough_AndDecrementsHits()
        {
            _model.HitsRemaining.Value = 3;

            var outcome = _model.EvaluateHit(new DamageContext(1));

            Assert.AreEqual(HitOutcome.PassThrough, outcome);
            Assert.AreEqual(2, _model.HitsRemaining.Value);
        }

        [Test]
        public void BalloonModel_EvaluateHit_KillingBlow_ReturnsPop_AndHitsRemainingIsZero()
        {
            _model.HitsRemaining.Value = 1;

            var outcome = _model.EvaluateHit(new DamageContext(1));

            Assert.AreEqual(HitOutcome.Pop, outcome);
            Assert.AreEqual(0, _model.HitsRemaining.Value);
        }

        [Test]
        public void BalloonModel_EvaluateHit_DamageContext_SurvivesWithPassThrough()
        {
            _model.HitsRemaining.Value = 3;

            var outcome = _model.EvaluateHit(new DamageContext(1));

            Assert.AreEqual(HitOutcome.PassThrough, outcome);
        }

        [Test]
        public void BalloonModel_EvaluateHit_PiercingFlag_PopsRegardlessOfHitsRemaining()
        {
            _model.HitsRemaining.Value = 5;

            var outcome = _model.EvaluateHit(new DamageContext(1, DamageFlags.Piercing));

            Assert.AreEqual(HitOutcome.Pop, outcome);
            Assert.AreEqual(0, _model.HitsRemaining.Value);
        }

        [Test]
        public void ResolveScoreAttribution_NotRainbow_EmitsSingleNonPrimaryAttribution()
        {
            _model.Color.Value = "Red";
            _model.HitsRemaining.Value = 0;

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), null, results);

            Assert.AreEqual(1, results.Count);
            Assert.IsFalse(results[0].IsPrimary);
            Assert.AreEqual("Red", results[0].ColorId);
        }

        [Test]
        public void ResolveScoreAttribution_NotRainbow_IgnoresIncompleteColors_StillScoresOwnColor()
        {
            // A completed-bar filter must not affect the single-colour path: a red balloon still scores
            // red even when "Red" is deliberately excluded from incompleteColors (it just banks as
            // overflow downstream — see ScoreController.CollectIncompleteColors).
            _model.Color.Value = "Red";
            _model.HitsRemaining.Value = 0;

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), new[] { "Blue" }, results);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Red", results[0].ColorId);
        }

        [Test]
        public void ResolveScoreAttribution_StillAlive_EmitsNothing()
        {
            _model.Color.Value = GamePalette.RainbowColorId;
            _model.HitsRemaining.Value = 3;

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), null, results);

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ResolveScoreAttribution_RainbowMode_NoColorSource_EmitsNothing()
        {
            // No palette/allowedColors were passed at construction — mirrors ToughBalloonModel's
            // no-level-context fallback edge case.
            _model.Color.Value = GamePalette.RainbowColorId;
            _model.HitsRemaining.Value = 0;

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1, DamageFlags.Normal, "Red"), null, results);

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ResolveScoreAttribution_RainbowMode_PrimaryColorNotAllowed_FallsBackToFirstAllowed()
        {
            var config = new BalloonModelConfig(scoreValue: 4);
            var model = new BalloonModel(config, allowedColors: new[] { "Red", "Blue" });
            model.Color.Value = GamePalette.RainbowColorId;
            model.HitsRemaining.Value = 0;

            var results = new List<ScoreAttribution>();
            model.ResolveScoreAttribution(new DamageContext(1, DamageFlags.Normal, "Purple"), null, results);

            var primary = results.Find(a => a.IsPrimary);
            Assert.AreEqual("Red", primary.ColorId);
        }

        [Test]
        public void ResolveScoreAttribution_RainbowMode_ScoresFullToEachAllowedColor()
        {
            var config = new BalloonModelConfig(scoreValue: 4);
            var model = new BalloonModel(config, allowedColors: new[] { "Red", "Blue", "Green" });
            model.Color.Value = GamePalette.RainbowColorId;
            model.HitsRemaining.Value = 0;

            var results = new List<ScoreAttribution>();
            model.ResolveScoreAttribution(new DamageContext(1, DamageFlags.Normal, "Red"), null, results);

            // Every allowed colour scores its full ScoreValue; exactly one anchors the streak.
            Assert.AreEqual(3, results.Count);
            foreach (var attribution in results)
            {
                Assert.AreEqual(4, attribution.Points);
            }

            Assert.AreEqual(1, results.FindAll(a => a.IsPrimary).Count);
            Assert.AreEqual("Red", results.Find(a => a.IsPrimary).ColorId);
        }

        [Test]
        public void Coat_AnOrdinaryHit_TakesItOff_TheBalloonSurvives_AndScoresNothing()
        {
            var model = Coated(Red);

            var outcome = model.EvaluateHit(new DamageContext(1, DamageFlags.DirectHit, Blue));

            Assert.AreEqual(HitOutcome.PassThrough, outcome);
            Assert.AreEqual(0, model.Coat.Value);
            Assert.AreEqual(1, model.HitsRemaining.Value);
            var results = new List<ScoreAttribution>();
            model.ResolveScoreAttribution(new DamageContext(1, DamageFlags.DirectHit, Blue), null, results);
            Assert.IsEmpty(results);
        }

        [Test]
        public void Coat_APiercingHit_PopsTheBalloon_CoatAndAll()
        {
            var model = Coated(Red);

            var outcome = model.EvaluateHit(new DamageContext(1, DamageFlags.Piercing | DamageFlags.DirectHit));

            Assert.AreEqual(HitOutcome.Pop, outcome);
            Assert.AreEqual(0, model.Coat.Value);
            Assert.AreEqual(0, model.HitsRemaining.Value);
        }

        [Test]
        public void Coat_ABeam_PopsTheBalloon()
        {
            var model = Coated(Red);

            Assert.AreEqual(HitOutcome.Pop, model.EvaluateHit(new DamageContext(1, DamageFlags.Beam)));
            Assert.AreEqual(0, model.Coat.Value);
        }

        // An item's blast on a neighbour: no DirectHit, one damage, not piercing.
        [Test]
        public void Coat_AnItemStyleHit_TakesItOff_TheBalloonSurvives()
        {
            var model = Coated(Red);

            Assert.AreEqual(HitOutcome.PassThrough, model.EvaluateHit(new DamageContext(1)));
            Assert.AreEqual(0, model.Coat.Value);
            Assert.AreEqual(1, model.HitsRemaining.Value);
        }

        // A laser beam carries both flags.
        [Test]
        public void Coat_APiercingBeam_PopsTheBalloon()
        {
            var model = Coated(Red);

            var outcome = model.EvaluateHit(new DamageContext(1, DamageFlags.Piercing | DamageFlags.Beam));

            Assert.AreEqual(HitOutcome.Pop, outcome);
            Assert.AreEqual(0, model.Coat.Value);
        }

        [Test]
        public void Coat_ARippleHit_TakesItOff()
        {
            var model = Coated(Red);

            Assert.AreEqual(HitOutcome.PassThrough, model.EvaluateHit(new DamageContext(1, DamageFlags.Ripple)));
            Assert.AreEqual(0, model.Coat.Value);
            Assert.AreEqual(1, model.HitsRemaining.Value);
        }

        [Test]
        public void Coat_TheSecondOrdinaryHit_PopsTheBalloon()
        {
            var model = Coated(Red);
            model.EvaluateHit(new DamageContext(1, DamageFlags.DirectHit));

            Assert.AreEqual(HitOutcome.Pop, model.EvaluateHit(new DamageContext(1, DamageFlags.DirectHit)));
            var results = new List<ScoreAttribution>();
            model.ResolveScoreAttribution(new DamageContext(1, DamageFlags.DirectHit), null, results);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(Red, results[0].ColorId);
        }

        [Test]
        public void Coat_AZeroDamageProbe_ChangesNothing()
        {
            var model = Coated(Red);

            model.EvaluateHit(new DamageContext(0));

            Assert.AreEqual(1, model.Coat.Value);
            Assert.AreEqual(1, model.HitsRemaining.Value);
        }

        [Test]
        public void TryCoat_StopsAtTheMax()
        {
            var model = new BalloonModel();

            Assert.IsTrue(model.TryCoat(2));
            Assert.IsTrue(model.TryCoat(2));
            Assert.IsFalse(model.TryCoat(2));
            Assert.AreEqual(2, model.Coat.Value);
        }

        [Test]
        public void Coat_TwoLayers_TakeTwoOrdinaryHits_ThenTheThirdPops()
        {
            var model = new BalloonModel();
            model.TryCoat(2);
            model.TryCoat(2);

            Assert.AreEqual(HitOutcome.PassThrough, model.EvaluateHit(new DamageContext(1)));
            Assert.AreEqual(HitOutcome.PassThrough, model.EvaluateHit(new DamageContext(1)));
            Assert.AreEqual(HitOutcome.Pop, model.EvaluateHit(new DamageContext(1)));
        }

        [Test]
        public void Uncoated_ADurableBalloonsSurvivingHit_IsUnchanged()
        {
            var model = new BalloonModel(new BalloonModelConfig(hitsToPop: 2));

            Assert.AreEqual(HitOutcome.PassThrough, model.EvaluateHit(new DamageContext(1)));
            Assert.AreEqual(1, model.HitsRemaining.Value);
            Assert.AreEqual(HitOutcome.Pop, model.EvaluateHit(new DamageContext(1)));
        }

        [Test]
        public void HitsToPop_CountsTheCoat_ButIsToughDoesNot()
        {
            var model = new BalloonModel();
            Assert.AreEqual(1, model.HitsToPop());
            Assert.IsFalse(model.IsTough());

            model.TryCoat(1);

            Assert.AreEqual(2, model.HitsToPop());
            Assert.IsFalse(model.IsTough(), "a piercing shot pops a coated balloon on contact, it does not plow it");
        }

        [Test]
        public void IsTough_ADurablePlainBalloon_StillIs_CoatedOrNot()
        {
            var model = new BalloonModel(new BalloonModelConfig(hitsToPop: 2));
            Assert.IsTrue(model.IsTough());

            model.TryCoat(1);

            Assert.AreEqual(3, model.HitsToPop());
            Assert.IsTrue(model.IsTough());
        }

        [Test]
        public void WashesProjectileColor_OnlyWhileCoated()
        {
            var model = new BalloonModel();
            Assert.IsFalse(model.WashesProjectileColor());

            model.TryCoat(1);
            Assert.IsTrue(model.WashesProjectileColor());

            model.EvaluateHit(new DamageContext(1));
            Assert.IsFalse(model.WashesProjectileColor());
        }

        private static BalloonModel Coated(string color)
        {
            var model = new BalloonModel();
            model.Color.Value = color;
            Assert.IsTrue(model.TryCoat(1));
            return model;
        }
    }
}
