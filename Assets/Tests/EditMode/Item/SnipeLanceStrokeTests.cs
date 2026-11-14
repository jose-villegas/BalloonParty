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

            Assert.IsTrue(stroke.Advance(0.1f, 5f, closed: true, Timing, out var erased));
            Assert.AreEqual(1f, stroke.Drawn, 1e-5f);
            Assert.AreEqual(0f, erased, 1e-5f);
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
        public void Advance_OpenRun_CaughtUpWithTheShot_NeverErased()
        {
            var stroke = new SnipeLanceStroke();

            for (var i = 0; i < 20; i++)
            {
                stroke.Advance(0.5f, 2f, closed: false, Timing, out var erased);
                Assert.AreEqual(0f, erased, 1e-5f);
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
        public void Advance_ClosedWhileBehind_DrawsOnToTheWallUntouched()
        {
            var stroke = new SnipeLanceStroke();
            stroke.Advance(0.1f, 4f, closed: false, Timing, out _);

            Assert.IsTrue(stroke.Advance(0.2f, 4f, closed: true, Timing, out var erased));
            Assert.AreEqual(3f, stroke.Drawn, 1e-5f);
            Assert.AreEqual(0f, erased, 1e-5f);
        }

        [Test]
        public void Advance_ReachedTheWall_HoldsThenErases()
        {
            var stroke = new SnipeLanceStroke();
            stroke.Advance(0.1f, 1f, closed: true, Timing, out _);

            stroke.Advance(0.5f, 1f, closed: true, Timing, out var holding);
            stroke.Advance(0.2f, 1f, closed: true, Timing, out var erasing);

            Assert.AreEqual(0f, holding, 1e-5f);
            Assert.AreEqual(0.5f, erasing, 1e-4f);
        }

        [Test]
        public void Advance_PastTheErase_FinishedAndGone()
        {
            var stroke = new SnipeLanceStroke();
            stroke.Advance(0.1f, 1f, closed: true, Timing, out _);

            Assert.IsFalse(stroke.Advance(1f, 1f, closed: true, Timing, out var erased));
            Assert.AreEqual(1f, erased, 1e-5f);
        }

        [Test]
        public void Advance_NoTipSpeed_WholeRunAppearsAtOnce()
        {
            var stroke = new SnipeLanceStroke();

            stroke.Advance(0f, 5f, closed: false, new SnipeLanceStrokeTiming(0f, 0.5f, 0.4f), out _);

            Assert.AreEqual(5f, stroke.Drawn, 1e-5f);
        }

        [Test]
        public void ChunkErased_FirstTurn_WipesAsTheEraseStarts()
        {
            Assert.Greater(SnipeLanceErase.ChunkErased(0.1f, 0, 5, 0.3f), 0f);
        }

        [Test]
        public void ChunkErased_LastTurn_UntouchedUntilLateThenGoneAtTheEnd()
        {
            Assert.AreEqual(0f, SnipeLanceErase.ChunkErased(0.6f, 4, 5, 0.3f), 1e-5f);
            Assert.AreEqual(1f, SnipeLanceErase.ChunkErased(1f, 4, 5, 0.3f), 1e-5f);
        }

        // Piece by piece: midway, earlier turns are gone while later ones are still whole.
        [Test]
        public void ChunkErased_Midway_EarlierTurnsGoneLaterOnesWhole()
        {
            Assert.AreEqual(1f, SnipeLanceErase.ChunkErased(0.5f, 0, 5, 0.3f), 1e-5f);
            Assert.AreEqual(0f, SnipeLanceErase.ChunkErased(0.5f, 3, 5, 0.3f), 1e-5f);
        }

        [Test]
        public void ChunkErased_OneChunk_FollowsTheWholeErase()
        {
            Assert.AreEqual(0.4f, SnipeLanceErase.ChunkErased(0.4f, 0, 1, 0.3f), 1e-5f);
        }

        // Rubbed out: one side, then back across to the other, and still again by the time it's gone.
        [Test]
        public void Smear_AcrossTheWipe_OutToOneSideThenTheOther()
        {
            Assert.AreEqual(0f, SnipeLanceErase.Smear(0f), 1e-5f);
            Assert.AreEqual(1f, SnipeLanceErase.Smear(0.25f), 1e-5f);
            Assert.AreEqual(-1f, SnipeLanceErase.Smear(0.75f), 1e-5f);
            Assert.AreEqual(0f, SnipeLanceErase.Smear(1f), 1e-5f);
        }

        [Test]
        public void Spread_PeaksMidRub_NoneUntouchedOrGone()
        {
            Assert.AreEqual(0f, SnipeLanceErase.Spread(0f), 1e-5f);
            Assert.AreEqual(1f, SnipeLanceErase.Spread(0.5f), 1e-5f);
            Assert.AreEqual(0f, SnipeLanceErase.Spread(1f), 1e-5f);
        }
    }
}
