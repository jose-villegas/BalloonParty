using BalloonParty.Balloon.Model;
using BalloonParty.Slots.Capabilities;
using NUnit.Framework;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class UnbreakableBalloonModelTests
    {
        private UnbreakableBalloonModel _model;

        [SetUp]
        public void SetUp()
        {
            _model = new UnbreakableBalloonModel(new BalloonModelConfig());
        }

        [Test]
        public void UnbreakableBalloonModel_IsIHitable()
        {
            Assert.IsTrue(_model is IHitable);
        }

        [Test]
        public void UnbreakableBalloonModel_IsNotIHasDurability()
        {
            Assert.IsFalse(_model is IHasDurability);
        }

        [Test]
        public void UnbreakableBalloonModel_EvaluateHit_NoFlags_ReturnsDeflect()
        {
            Assert.AreEqual(HitOutcome.Deflect, _model.EvaluateHit(new DamageContext(1)));
        }

        [Test]
        public void UnbreakableBalloonModel_EvaluateHit_HighDamage_NoFlags_ReturnsDeflect()
        {
            Assert.AreEqual(HitOutcome.Deflect, _model.EvaluateHit(new DamageContext(99)));
        }

        [Test]
        public void UnbreakableBalloonModel_EvaluateHit_PiercingFlag_ReturnsPop()
        {
            Assert.AreEqual(HitOutcome.Pop, _model.EvaluateHit(new DamageContext(1, DamageFlags.Piercing)));
        }

        [Test]
        public void UnbreakableBalloonModel_EvaluateHit_PiercingFlag_DoesNotMutateState()
        {
            var initialHits = _model.HitsRemaining.Value;

            _model.EvaluateHit(new DamageContext(1, DamageFlags.Piercing));

            Assert.AreEqual(initialHits, _model.HitsRemaining.Value);
        }
    }
}

