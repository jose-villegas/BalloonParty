using BalloonParty.Balloon.Spawner;
using NUnit.Framework;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class ReinforcementPlanTests
    {
        [Test]
        public void WithRoom_AddsEveryCall()
        {
            Assert.AreEqual(2, ReinforcementPlan.ToAdd(calls: 2, alive: 0, ceiling: 4, lineRoom: 7));
        }

        [Test]
        public void TheCeilingCountsTheOnesAlreadyOnTheBoard()
        {
            Assert.AreEqual(1, ReinforcementPlan.ToAdd(calls: 3, alive: 3, ceiling: 4, lineRoom: 7));
        }

        [Test]
        public void AtTheCeiling_AddsNone()
        {
            Assert.AreEqual(0, ReinforcementPlan.ToAdd(calls: 2, alive: 4, ceiling: 4, lineRoom: 7));
        }

        [Test]
        public void PastTheCeiling_AddsNoneRatherThanANegativeCount()
        {
            Assert.AreEqual(0, ReinforcementPlan.ToAdd(calls: 2, alive: 6, ceiling: 4, lineRoom: 7));
        }

        [Test]
        public void NeverMoreThanTheLineHasPlacesFor()
        {
            Assert.AreEqual(1, ReinforcementPlan.ToAdd(calls: 3, alive: 0, ceiling: 4, lineRoom: 1));
        }
    }
}
