using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Balloon.Model;
using NUnit.Framework;

namespace BalloonParty.Tests.Slots
{
    [TestFixture]
    public class HitableActorTests
    {
        [Test]
        public void DeflectorActor_EvaluateHit_ReturnsDeflect()
        {
            var model = new DeflectorActorModel();

            Assert.AreEqual(HitOutcome.Deflect, model.EvaluateHit(new DamageContext(1)));
        }

        [Test]
        public void DeflectorActor_IsNotIHasDurability()
        {
            var model = new DeflectorActorModel();

            Assert.IsFalse(model is IHasDurability);
        }

        [Test]
        public void DeflectorActor_IsNotIBalloonModel()
        {
            var model = new DeflectorActorModel();

            Assert.IsFalse(model is IBalloonModel);
        }

        [Test]
        public void AbsorberActor_EvaluateHit_ReturnsAbsorb()
        {
            var model = new AbsorberActorModel();

            Assert.AreEqual(HitOutcome.Absorb, model.EvaluateHit(new DamageContext(1)));
        }

        [Test]
        public void AbsorberActor_IsNotIHasDurability()
        {
            var model = new AbsorberActorModel();

            Assert.IsFalse(model is IHasDurability);
        }
    }
}

