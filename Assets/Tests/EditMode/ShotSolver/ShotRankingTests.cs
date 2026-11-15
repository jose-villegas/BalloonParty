using BalloonParty.Solver;
using NUnit.Framework;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>
    ///     How the Fire Best Shot cheat, and the bots that aim with it, order shots: a golden balloon over any
    ///     score, a heart that adds one over score, a disco ball over score, then score.
    /// </summary>
    [TestFixture]
    public class ShotRankingTests
    {
        private static ShotSimulationResult Shot(int score, int golden = 0, int heart = 0, int disco = 0)
        {
            return new ShotSimulationResult(score, 0, 0, false, 0, false, false, false, golden, heart, disco);
        }

        [Test]
        public void IsBetter_AGoldenPop_BeatsAnyScore()
        {
            Assert.IsTrue(ShotRanking.IsBetter(Shot(0, golden: 1), Shot(100000), heartsWanted: 0));
            Assert.IsFalse(ShotRanking.IsBetter(Shot(100000), Shot(0, golden: 1), heartsWanted: 0));
        }

        [Test]
        public void IsBetter_AGoldenPop_BeatsAHeartPop()
        {
            Assert.IsTrue(ShotRanking.IsBetter(Shot(0, golden: 1), Shot(0, heart: 1), heartsWanted: 1));
        }

        [Test]
        public void IsBetter_AHeartThatCounts_BeatsAnyScore()
        {
            Assert.IsTrue(ShotRanking.IsBetter(Shot(0, heart: 1), Shot(100000), heartsWanted: 1));
        }

        // At full hearts off a purple level the heart adds nothing, so it's worth only its (nil) score.
        [Test]
        public void IsBetter_AHeartThatDoesntCount_LosesToScore()
        {
            Assert.IsFalse(ShotRanking.IsBetter(Shot(0, heart: 1), Shot(10), heartsWanted: 0));
            Assert.IsTrue(ShotRanking.IsBetter(Shot(10), Shot(0, heart: 1), heartsWanted: 0));
        }

        [Test]
        public void IsBetter_SameRewards_ScoreDecides()
        {
            Assert.IsTrue(ShotRanking.IsBetter(Shot(20, golden: 1), Shot(10, golden: 1), heartsWanted: 1));
            Assert.IsFalse(ShotRanking.IsBetter(Shot(10), Shot(10), heartsWanted: 1), "a tie keeps the first");
        }

        // One heart missing: a second heart pop adds nothing, so score decides between one heart and two.
        [Test]
        public void IsBetter_MoreHeartsThanMissing_ScoreDecides()
        {
            Assert.IsTrue(ShotRanking.IsBetter(Shot(50, heart: 1), Shot(0, heart: 2), heartsWanted: 1));
            Assert.IsTrue(ShotRanking.IsBetter(Shot(0, heart: 2), Shot(50, heart: 1), heartsWanted: 2));
        }

        [Test]
        public void IsBetter_ADiscoPop_BeatsAnyScore()
        {
            Assert.IsTrue(ShotRanking.IsBetter(Shot(0, disco: 1), Shot(100000), heartsWanted: 0));
            Assert.IsFalse(ShotRanking.IsBetter(Shot(100000), Shot(0, disco: 1), heartsWanted: 0));
        }

        // The disco ball pays the shots after this one; a golden card or a heart that counts pays now.
        [Test]
        public void IsBetter_AGoldenPopOrAHeartThatCounts_BeatsADiscoPop()
        {
            Assert.IsTrue(ShotRanking.IsBetter(Shot(0, golden: 1), Shot(0, disco: 1), heartsWanted: 0));
            Assert.IsTrue(ShotRanking.IsBetter(Shot(0, heart: 1), Shot(0, disco: 1), heartsWanted: 1));
            Assert.IsTrue(ShotRanking.IsBetter(Shot(0, disco: 1), Shot(0, heart: 1), heartsWanted: 0));
        }

        [TestCase(1, 3, false, 2)]
        [TestCase(3, 3, false, 0)]
        [TestCase(3, 3, true, int.MaxValue)]
        [TestCase(4, 3, true, int.MaxValue)]
        public void HeartsWanted_TheMissingOnes_OrAnyOnAPurpleLevel(int hearts, int max, bool purple, int wanted)
        {
            Assert.AreEqual(wanted, ShotRanking.HeartsWanted(hearts, max, purple));
        }
    }
}
