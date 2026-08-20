using System.Collections.Generic;
using BalloonParty.Configuration.Items;
using BalloonParty.Item;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    // ChainMatches is the pure equality rule ItemSightTracker's per-frame grid walk relies on, and the
    // rule ItemSightSoundRouter's settle gate relies on in turn — factored out so it's edit-mode
    // testable without any SlotGrid/PredictionTraceProvider setup.
    [TestFixture]
    public class ItemSightTrackerTests
    {
        [Test]
        public void ChainMatches_BothEmpty_IsTrue()
        {
            var current = new List<ItemSightTracker.SightedItem>();
            var chain = new List<Vector2Int>();

            Assert.IsTrue(ItemSightTracker.ChainMatches(current, chain));
        }

        [Test]
        public void ChainMatches_SameSingleHostSameOrder_IsTrue()
        {
            var slot = new Vector2Int(1, 0);
            var current = new List<ItemSightTracker.SightedItem> { MakeItem(slot, Vector3.zero, 0f) };
            var chain = new List<Vector2Int> { slot };

            Assert.IsTrue(ItemSightTracker.ChainMatches(current, chain));
        }

        // The whole point of a "chain": the same first host with one more added further along the
        // path is a DIFFERENT chain, not an extension of the same one — a same-start check alone
        // would wrongly call this unchanged.
        [Test]
        public void ChainMatches_SameStartPlusOneMore_IsFalse()
        {
            var start = new Vector2Int(1, 0);
            var added = new Vector2Int(2, 0);
            var current = new List<ItemSightTracker.SightedItem>
            {
                MakeItem(start, new Vector3(1f, 0f, 0f), 0f),
                MakeItem(added, new Vector3(2f, 0f, 0f), 1f),
            };
            var chain = new List<Vector2Int> { start };

            Assert.IsFalse(ItemSightTracker.ChainMatches(current, chain));
        }

        [Test]
        public void ChainMatches_OneDroppedFromTheEnd_IsFalse()
        {
            var start = new Vector2Int(1, 0);
            var current = new List<ItemSightTracker.SightedItem> { MakeItem(start, Vector3.zero, 0f) };
            var chain = new List<Vector2Int> { start, new(2, 0) };

            Assert.IsFalse(ItemSightTracker.ChainMatches(current, chain));
        }

        // Same two hosts, but the trace now reaches them in the opposite order (e.g. the aim swept
        // back the other way) — still a different chain, since the chain plays in trace order.
        [Test]
        public void ChainMatches_SameHostsDifferentOrder_IsFalse()
        {
            var a = new Vector2Int(1, 0);
            var b = new Vector2Int(2, 0);
            var current = new List<ItemSightTracker.SightedItem>
            {
                MakeItem(a, Vector3.zero, 0f),
                MakeItem(b, Vector3.zero, 1f),
            };
            var chain = new List<Vector2Int> { b, a };

            Assert.IsFalse(ItemSightTracker.ChainMatches(current, chain));
        }

        private static ItemSightTracker.SightedItem MakeItem(Vector2Int slot, Vector3 position, float traceOffset)
        {
            return new ItemSightTracker.SightedItem(slot, position, traceOffset, ItemType.Bomb);
        }
    }
}
