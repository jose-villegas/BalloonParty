using BalloonParty.Slots;
using NUnit.Framework;

namespace BalloonParty.Tests.Slots
{
    [TestFixture]
    public class StaticActorTests
    {
        [Test]
        public void StaticActorModel_KindIsStatic()
        {
            var model = new StaticActorModel();

            Assert.AreEqual(SlotActorKind.Static, model.Kind);
        }
    }
}

