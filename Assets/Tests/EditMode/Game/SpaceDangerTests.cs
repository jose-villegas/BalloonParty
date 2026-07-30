using BalloonParty.Game.Danger;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class SpaceDangerTests
    {
        private const int Cols = 6;

        [Test]
        public void NoOverflow_IsSafe()
        {
            // The board can absorb the whole turn — no danger.
            Assert.AreEqual(0f, SpaceDanger.Evaluate(hearts: 3, availableSpace: 30, spawnPerTurn: 18, columns: Cols));
        }

        [Test]
        public void PartialOverflow_BelowOneLine_NoDanger()
        {
            // overflow = 18 - 15 = 3, heartsAtRisk = floor(3/6) = 0 — sub-line deficit is forgiven.
            Assert.AreEqual(0f, SpaceDanger.Evaluate(hearts: 3, availableSpace: 15, spawnPerTurn: 18, columns: Cols));
        }

        [Test]
        public void OneLineOverflow_ScalesByHearts()
        {
            // overflow = 18 - 12 = 6, heartsAtRisk = 1, danger = 1/3.
            Assert.AreEqual(1f / 3f, SpaceDanger.Evaluate(hearts: 3, availableSpace: 12, spawnPerTurn: 18, columns: Cols), 1e-4f);
        }

        [Test]
        public void OverflowEqualToHearts_IsMaxDanger()
        {
            // overflow = 18 - 0 = 18, heartsAtRisk = 3 == hearts → max danger.
            Assert.AreEqual(1f, SpaceDanger.Evaluate(hearts: 3, availableSpace: 0, spawnPerTurn: 18, columns: Cols));
        }

        [Test]
        public void OverflowBeyondHearts_ClampsToMax()
        {
            // overflow = 30, heartsAtRisk = 5 > 3 hearts → clamped to 1.
            Assert.AreEqual(1f, SpaceDanger.Evaluate(hearts: 3, availableSpace: 0, spawnPerTurn: 30, columns: Cols));
        }

        [Test]
        public void ZeroHearts_IsMaxDanger()
        {
            Assert.AreEqual(1f, SpaceDanger.Evaluate(hearts: 0, availableSpace: 50, spawnPerTurn: 18, columns: Cols));
        }

        [Test]
        public void ZeroColumns_ReturnsSafe()
        {
            // Guard clause: division by zero must not blow up — returns 0 (safe).
            Assert.AreEqual(0f, SpaceDanger.Evaluate(hearts: 3, availableSpace: 0, spawnPerTurn: 18, columns: 0));
        }

        [Test]
        public void TwoLineOverflow_ScalesByHearts()
        {
            // overflow = 18 - 6 = 12, heartsAtRisk = 12/6 = 2, danger = 2/3.
            Assert.AreEqual(2f / 3f, SpaceDanger.Evaluate(hearts: 3, availableSpace: 6, spawnPerTurn: 18, columns: Cols), 1e-4f);
        }
    }
}
