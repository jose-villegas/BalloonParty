using BalloonParty.Game.Health;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class WaveDeficitCalculatorTests
    {
        // Surplus space — no damage.
        [TestCase(4, 6, 26, 0, 0, TestName = "Surplus_NoDamage")]

        // Exact fit — no damage, no unspawned.
        [TestCase(4, 6, 24, 0, 0, TestName = "ExactFit_NoDamage")]

        // Partial deficit (< 1 line) — no heart lost, those slots just don't spawn.
        [TestCase(4, 6, 21, 0, 3, TestName = "PartialDeficit_NoDamage")]

        // Exact one-line deficit.
        [TestCase(4, 6, 18, 1, 6, TestName = "ExactOneLine_OneHeart")]

        // Non-exact multi-slot deficit (remainder discarded for hearts).
        [TestCase(4, 6, 16, 1, 8, TestName = "OneLineWithRemainder_OneHeart")]

        // Two-line deficit.
        [TestCase(4, 6, 12, 2, 12, TestName = "TwoLines_TwoHearts")]

        // Near-total deficit.
        [TestCase(4, 6, 4, 3, 20, TestName = "NearTotal_ThreeHearts")]

        // Zero available — full deficit.
        [TestCase(4, 6, 0, 4, 24, TestName = "ZeroAvailable_AllLinesLost")]

        // Single spawn line, partial deficit — no heart lost.
        [TestCase(1, 6, 3, 0, 3, TestName = "SingleLine_PartialDeficit_NoDamage")]

        // Single spawn line, zero space — one heart.
        [TestCase(1, 6, 0, 1, 6, TestName = "SingleLine_ZeroSpace_OneHeart")]

        // Non-6-column grid (5 cols).
        [TestCase(4, 5, 10, 2, 10, TestName = "FiveColumns_TwoHearts")]

        // Non-6-column grid (7 cols).
        [TestCase(3, 7, 7, 2, 14, TestName = "SevenColumns_TwoHearts")]

        // Single column — every slot is a full line.
        [TestCase(3, 1, 0, 3, 3, TestName = "SingleColumn_AllLost")]

        // Zero spawn lines — nothing to spawn, no damage.
        [TestCase(0, 6, 0, 0, 0, TestName = "ZeroSpawnLines_NoDamage")]
        public void Calculate_ReturnsExpected(
            int spawnLines, int rowLength, int available, int expectedHearts, int expectedUnspawned)
        {
            var needed = spawnLines * rowLength;
            var result = WaveDeficitCalculator.Calculate(available, needed, rowLength);

            Assert.AreEqual(expectedHearts, result.HeartsLost, "HeartsLost");
            Assert.AreEqual(expectedUnspawned, result.UnspawnedSlots, "UnspawnedSlots");
        }

        [Test]
        public void Calculate_ZeroRowLength_ReturnsDefault()
        {
            var result = WaveDeficitCalculator.Calculate(0, 24, 0);

            Assert.AreEqual(0, result.HeartsLost);
            Assert.AreEqual(0, result.UnspawnedSlots);
        }
    }
}
