using BalloonParty.Balloon.Spawner;
using NUnit.Framework;

namespace BalloonParty.Tests.Balloon
{
    // Where the initial fill deals the toughs when they walk as one snake: side by side on one line.
    [TestFixture]
    public class ToughRunPlanTests
    {
        [Test]
        public void TheRun_TakesTheDeepestLineASnakeWalks()
        {
            Assert.AreEqual(6, ToughRunPlan.For(4, 9, 6, 6, 0f).Line, "the snake's last row");
            Assert.AreEqual(4, ToughRunPlan.For(4, 5, 6, 6, 0f).Line, "a shorter fill's last line");
        }

        [Test]
        public void AShortRun_LiesSideBySide_AnywhereAlongTheLine()
        {
            var west = ToughRunPlan.For(4, 9, 6, 6, 0f);
            var east = ToughRunPlan.For(4, 9, 6, 6, 0.99f);

            Assert.AreEqual(4, west.Length);
            Assert.AreEqual(0, west.Start);
            Assert.AreEqual(2, east.Start, "flush to the east edge");
            CollectionAssert.AreEqual(new[] { 2, 3, 4, 5 },
                new[] { east.ColumnAt(6, 0, 6), east.ColumnAt(6, 1, 6), east.ColumnAt(6, 2, 6), east.ColumnAt(6, 3, 6) });
            Assert.AreEqual(0, east.CountOn(5));
        }

        [Test]
        public void ToughsPastAFullLine_BendRoundItsEastEnd_OnTheLineAbove()
        {
            var plan = ToughRunPlan.For(8, 9, 6, 6, 0.5f);

            Assert.AreEqual(6, plan.Length);
            Assert.AreEqual(0, plan.Start);
            Assert.AreEqual(2, plan.UpperLength);
            Assert.AreEqual(2, plan.CountOn(5));
            Assert.AreEqual(5, plan.ColumnAt(5, 0, 6), "from the east edge");
            Assert.AreEqual(4, plan.ColumnAt(5, 1, 6));
            Assert.AreEqual(-1, plan.ColumnAt(5, 2, 6));
        }

        [Test]
        public void NoToughs_OrNoLine_IsNoRun()
        {
            Assert.AreEqual(-1, ToughRunPlan.For(0, 9, 6, 6, 0f).Line);
            Assert.AreEqual(-1, ToughRunPlan.For(3, 9, 6, -1, 0f).Line);
        }
    }
}
