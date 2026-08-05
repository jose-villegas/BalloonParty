using BalloonParty.Configuration.Items;
using BalloonParty.Item.Shield;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    // The bias only ever nudges: it must stay small next to support and pressure, and it must be
    // silent for everything that is not carrying a shield.
    [TestFixture]
    public class ShieldSlotPreferenceTests
    {
        [Test]
        public void WeightFor_ActorWithoutAnItemSlot_IsZero()
        {
            var preference = new ShieldSlotPreference(null);

            Assert.AreEqual(0, preference.WeightFor(Substitute.For<ISlotActor>(), Vector2Int.zero));
        }

        [Test]
        public void WeightFor_ActorCarryingAnotherItem_IsZero()
        {
            var preference = new ShieldSlotPreference(null);

            Assert.AreEqual(0, preference.WeightFor(Host(ItemType.Bomb), Vector2Int.zero));
        }

        private static ISlotActor Host(ItemType item)
        {
            var actor = Substitute.For<ISlotActor, IHasItemSlot>();
            ((IHasItemSlot)actor).Item.Returns(new ReactiveProperty<ItemType>(item));
            return actor;
        }
    }
}
