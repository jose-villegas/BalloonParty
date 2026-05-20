using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Slots;
using NUnit.Framework;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class ItemSlotTests
    {
        [Test]
        public void BalloonModel_ImplementsIHasItemSlot()
        {
            Assert.IsTrue(new BalloonModel() is IHasItemSlot);
        }

        [Test]
        public void BalloonModel_IHasItemSlot_AlsoImplementsIHasColor()
        {
            Assert.IsTrue(new BalloonModel() is IHasColor);
        }

        [Test]
        public void ToughBalloonModel_DoesNotImplementIHasItemSlot()
        {
            Assert.IsFalse(new ToughBalloonModel(new BalloonModelConfig()) is IHasItemSlot);
        }

        [Test]
        public void BalloonModel_Item_DefaultIsNone()
        {
            var model = new BalloonModel();
            Assert.AreEqual(ItemType.None, ((IHasItemSlot)model).Item.Value);
        }
    }
}



