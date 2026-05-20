using BalloonParty.Balloon.Model;
using BalloonParty.Slots;
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

            Assert.AreEqual(HitOutcome.PassThrough, _model.EvaluateHit(1));
        }

        [Test]
        public void EvaluateHit_ExactKill_ReturnsPop()
        {
            _model.HitsRemaining.Value = 1;

            Assert.AreEqual(HitOutcome.Pop, _model.EvaluateHit(1));
        }

        [Test]
        public void EvaluateHit_Overkill_ReturnsPop()
        {
            _model.HitsRemaining.Value = 1;

            Assert.AreEqual(HitOutcome.Pop, _model.EvaluateHit(5));
        }

        [Test]
        public void EvaluateHit_ExactKillHighDamage_ReturnsPop()
        {
            _model.HitsRemaining.Value = 3;

            Assert.AreEqual(HitOutcome.Pop, _model.EvaluateHit(3));
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

            var outcome = _model.EvaluateHit(1);

            Assert.AreEqual(HitOutcome.PassThrough, outcome);
            Assert.AreEqual(2, _model.HitsRemaining.Value);
        }

        [Test]
        public void BalloonModel_EvaluateHit_KillingBlow_ReturnsPop_AndHitsRemainingIsZero()
        {
            _model.HitsRemaining.Value = 1;

            var outcome = _model.EvaluateHit(1);

            Assert.AreEqual(HitOutcome.Pop, outcome);
            Assert.AreEqual(0, _model.HitsRemaining.Value);
        }
    }
}
