using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Capabilities;
using NUnit.Framework;

namespace BalloonParty.Tests.Slots
{
    [TestFixture]
    public class StructuralActorTests
    {
        [Test]
        public void PuffObstacleModel_KindIsStatic()
        {
            var model = new PuffObstacleModel();

            Assert.AreEqual(SlotActorKind.Static, model.Kind);
        }

        [Test]
        public void PuffObstacleModel_IsIPassThrough()
        {
            var model = new PuffObstacleModel();

            Assert.IsTrue(model is IPassThrough);
        }

        [Test]
        public void PuffObstacleModel_IsNotIHitable()
        {
            var model = new PuffObstacleModel();

            Assert.IsFalse(model is IHitable);
        }

        [Test]
        public void BushObstacleModel_KindIsStatic()
        {
            var model = new BushObstacleModel();

            Assert.AreEqual(SlotActorKind.Static, model.Kind);
        }

        [Test]
        public void BushObstacleModel_IsNotIPassThrough()
        {
            var model = new BushObstacleModel();

            Assert.IsFalse(model is IPassThrough);
        }

        [Test]
        public void BushObstacleModel_IsNotIHitable()
        {
            var model = new BushObstacleModel();

            Assert.IsFalse(model is IHitable);
        }
    }
}
