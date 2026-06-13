using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Slots
{
    [TestFixture]
    public class BalancePathHolderTests
    {
        [Test]
        public void ResetRun_ClearsTransitSlots()
        {
            var holder = new BalancePathHolder();
            var actor = Substitute.For<IWriteableDynamicSlotActor>();
            var slot = new Vector2Int(1, 2);
            holder.Reserve(actor, slot);
            Assert.IsTrue(holder.IsInTransit(slot));

            holder.ResetRun(2);

            Assert.IsFalse(holder.IsInTransit(slot));
        }

        [Test]
        public void ResetRun_DropsPerActorSlotList()
        {
            var holder = new BalancePathHolder();
            var actor = Substitute.For<IWriteableDynamicSlotActor>();
            holder.Reserve(actor, new Vector2Int(0, 1));
            holder.ResetRun(2);

            // After a full clear the same actor reserves cleanly — no stale slot list lingers.
            holder.Reserve(actor, new Vector2Int(2, 3));

            Assert.IsTrue(holder.IsInTransit(new Vector2Int(2, 3)));
            Assert.IsFalse(holder.IsInTransit(new Vector2Int(0, 1)));
        }
    }
}
