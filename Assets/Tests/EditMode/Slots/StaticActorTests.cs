using BalloonParty.Slots;
using BalloonParty.Slots.StaticActor;
using NUnit.Framework;

namespace BalloonParty.Tests.Slots
{
    [TestFixture]
    public class StaticActorTests
    {
        [Test]
        public void StaticActorModel_KindIsStatic()
        {
            var model = new StaticActorModel(default);

            Assert.AreEqual(SlotActorKind.Static, model.Kind);
        }

        [Test]
        public void StaticActorModel_IsNotIDynamicSlotActor()
        {
            var model = new StaticActorModel(default);

            Assert.IsFalse(model is IDynamicSlotActor);
        }
    }
}

