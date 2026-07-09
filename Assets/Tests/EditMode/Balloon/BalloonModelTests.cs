using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Palette;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Actor;
using NUnit.Framework;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class BalloonModelTests
    {
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
            _model.ResolveScoreAttribution(new DamageContext(1), results);

            Assert.AreEqual(1, results.Count);
            Assert.IsFalse(results[0].IsPrimary);
            Assert.AreEqual("Red", results[0].ColorId);
        }

        [Test]
        public void ResolveScoreAttribution_StillAlive_EmitsNothing()
        {
            _model.Color.Value = GamePalette.RainbowColorId;
            _model.HitsRemaining.Value = 3;

            var results = new List<ScoreAttribution>();
            _model.ResolveScoreAttribution(new DamageContext(1), results);

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
            _model.ResolveScoreAttribution(new DamageContext(1, DamageFlags.Normal, "Red"), results);

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
            model.ResolveScoreAttribution(new DamageContext(1, DamageFlags.Normal, "Purple"), results);

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
            model.ResolveScoreAttribution(new DamageContext(1, DamageFlags.Normal, "Red"), results);

            // Every allowed colour scores its full ScoreValue; exactly one anchors the streak.
            Assert.AreEqual(3, results.Count);
            foreach (var attribution in results)
            {
                Assert.AreEqual(4, attribution.Points);
            }

            Assert.AreEqual(1, results.FindAll(a => a.IsPrimary).Count);
            Assert.AreEqual("Red", results.Find(a => a.IsPrimary).ColorId);
        }
    }
}
