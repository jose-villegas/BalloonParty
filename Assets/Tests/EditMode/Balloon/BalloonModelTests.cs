using BalloonParty.Balloon.Model;
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
        public void EvaluateHit_Unbreakable_ReturnsDeflect()
        {
            _model.HitsRemaining.Value = -1;

            Assert.AreEqual(HitOutcome.Deflect, _model.EvaluateHit(1));
        }

        [Test]
        public void EvaluateHit_UnbreakableHighDamage_StillDeflects()
        {
            _model.HitsRemaining.Value = -1;

            Assert.AreEqual(HitOutcome.Deflect, _model.EvaluateHit(99));
        }

        [Test]
        public void EvaluateHit_AbsorbsDamage_ReturnsDeflect()
        {
            _model.HitsRemaining.Value = 3;

            Assert.AreEqual(HitOutcome.Deflect, _model.EvaluateHit(1));
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
    }
}
