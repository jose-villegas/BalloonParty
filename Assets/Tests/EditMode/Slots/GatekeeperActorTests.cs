using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Capabilities;
using NUnit.Framework;

namespace BalloonParty.Tests.Slots
{
    [TestFixture]
    public class GatekeeperActorTests
    {
        [Test]
        public void GatekeeperActor_EvaluateHit_Survives_ReturnsDeflect_AndDecrementsHits()
        {
            var model = new GatekeeperActorModel(hitsToPop: 3);

            var outcome = model.EvaluateHit(new DamageContext(1));

            Assert.AreEqual(HitOutcome.Deflect, outcome);
            Assert.AreEqual(2, model.HitsRemaining.Value);
        }

        [Test]
        public void GatekeeperActor_EvaluateHit_KillingBlow_ReturnsPop_AndHitsRemainingIsZero()
        {
            var model = new GatekeeperActorModel(hitsToPop: 1);

            var outcome = model.EvaluateHit(new DamageContext(1));

            Assert.AreEqual(HitOutcome.Pop, outcome);
            Assert.AreEqual(0, model.HitsRemaining.Value);
        }
    }
}

