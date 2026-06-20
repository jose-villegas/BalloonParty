using BalloonParty.Game.Danger;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class SpaceDangerTests
    {
        [Test]
        public void NoOverflow_IsSafe()
        {
            // The board can absorb the whole turn — no danger.
            Assert.AreEqual(0f, SpaceDanger.Evaluate(hearts: 5, availableSpace: 10, spawnPerTurn: 6));
        }

        [Test]
        public void PartialOverflow_ScalesByHearts()
        {
            // overflow = 12 - 9 = 3 would-be rejects against 5 hearts.
            Assert.AreEqual(0.6f, SpaceDanger.Evaluate(hearts: 5, availableSpace: 9, spawnPerTurn: 12), 1e-4f);
        }

        [Test]
        public void OverflowEqualToHearts_IsMaxDanger()
        {
            // overflow = 12 - 7 = 5 == hearts → last gradient value.
            Assert.AreEqual(1f, SpaceDanger.Evaluate(hearts: 5, availableSpace: 7, spawnPerTurn: 12));
        }

        [Test]
        public void OverflowBeyondHearts_ClampsToMax()
        {
            Assert.AreEqual(1f, SpaceDanger.Evaluate(hearts: 5, availableSpace: 0, spawnPerTurn: 30));
        }

        [Test]
        public void ZeroHearts_IsMaxDanger()
        {
            Assert.AreEqual(1f, SpaceDanger.Evaluate(hearts: 0, availableSpace: 50, spawnPerTurn: 6));
        }
    }
}
