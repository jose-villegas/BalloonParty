using System.Collections.Generic;
using BalloonParty.Shared.Extensions;
using NUnit.Framework;

namespace BalloonParty.Tests.Shared
{
    [TestFixture]
    public class ListExtensionsTests
    {
        [Test]
        public void SwapRemoveAt_MovesLastIntoSlot_DropsCount()
        {
            var list = new List<int> { 10, 20, 30, 40 };

            list.SwapRemoveAt(1);

            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(40, list[1], "last element should fill the removed slot");
            CollectionAssert.AreEquivalent(new[] { 10, 40, 30 }, list);
        }

        [Test]
        public void SwapRemoveAt_LastIndex_JustRemoves()
        {
            var list = new List<int> { 10, 20, 30 };

            list.SwapRemoveAt(2);

            CollectionAssert.AreEqual(new[] { 10, 20 }, list);
        }

        [Test]
        public void SwapRemoveAt_SingleElement_Empties()
        {
            var list = new List<int> { 99 };

            list.SwapRemoveAt(0);

            Assert.IsEmpty(list);
        }
    }
}
