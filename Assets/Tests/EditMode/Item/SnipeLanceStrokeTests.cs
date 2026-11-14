using BalloonParty.Item.Snipe;
using NUnit.Framework;

namespace BalloonParty.Tests.Item
{
    [TestFixture]
    public class SnipeLanceStrokeTests
    {
        // 10 units a second: a 0.1 s step draws one unit.
        private static readonly SnipeLanceStrokeTiming Timing = new(10f, 0.5f, 0.4f);

        [Test]
        public void Advance_TipRunsAtItsSpeed()
        {
            var stroke = new SnipeLanceStroke();

            Assert.IsTrue(stroke.Advance(0.1f, 5f, closed: true, Timing, out var alpha));
            Assert.AreEqual(1f, stroke.Drawn, 1e-5f);
            Assert.AreEqual(1f, alpha, 1e-5f);
        }

        // The end is the flying shot: the pencil follows it, never ahead of it.
        [Test]
        public void Advance_OpenRun_TipStopsAtTheShot()
        {
            var stroke = new SnipeLanceStroke();

            stroke.Advance(1f, 2f, closed: false, Timing, out _);

            Assert.AreEqual(2f, stroke.Drawn, 1e-5f);
        }

        [Test]
        public void Advance_OpenRun_CaughtUpWithTheShot_NeverFades()
        {
            var stroke = new SnipeLanceStroke();

            for (var i = 0; i < 20; i++)
            {
                stroke.Advance(0.5f, 2f, closed: false, Timing, out var alpha);
                Assert.AreEqual(1f, alpha, 1e-5f);
            }
        }

        [Test]
        public void Advance_RunGrowsAsTheShotFlies_TipCarriesOn()
        {
            var stroke = new SnipeLanceStroke();
            stroke.Advance(1f, 2f, closed: false, Timing, out _);

            stroke.Advance(0.1f, 6f, closed: false, Timing, out _);

            Assert.AreEqual(3f, stroke.Drawn, 1e-5f);
        }

        // The wall closes the run while the tip is still behind: it draws on to the wall before the hold.
        [Test]
        public void Advance_ClosedWhileBehind_DrawsOnToTheWallAtFullStrength()
        {
            var stroke = new SnipeLanceStroke();
            stroke.Advance(0.1f, 4f, closed: false, Timing, out _);

            Assert.IsTrue(stroke.Advance(0.2f, 4f, closed: true, Timing, out var alpha));
            Assert.AreEqual(3f, stroke.Drawn, 1e-5f);
            Assert.AreEqual(1f, alpha, 1e-5f);
        }

        [Test]
        public void Advance_ReachedTheWall_HoldsThenFades()
        {
            var stroke = new SnipeLanceStroke();
            stroke.Advance(0.1f, 1f, closed: true, Timing, out _);

            stroke.Advance(0.5f, 1f, closed: true, Timing, out var held);
            stroke.Advance(0.2f, 1f, closed: true, Timing, out var fading);

            Assert.AreEqual(1f, held, 1e-5f);
            Assert.AreEqual(0.5f, fading, 1e-4f);
        }

        [Test]
        public void Advance_PastTheFade_FinishedAndInvisible()
        {
            var stroke = new SnipeLanceStroke();
            stroke.Advance(0.1f, 1f, closed: true, Timing, out _);

            Assert.IsFalse(stroke.Advance(1f, 1f, closed: true, Timing, out var alpha));
            Assert.AreEqual(0f, alpha, 1e-5f);
        }

        [Test]
        public void Advance_NoTipSpeed_WholeRunAppearsAtOnce()
        {
            var stroke = new SnipeLanceStroke();

            stroke.Advance(0f, 5f, closed: false, new SnipeLanceStrokeTiming(0f, 0.5f, 0.4f), out _);

            Assert.AreEqual(5f, stroke.Drawn, 1e-5f);
        }
    }
}
