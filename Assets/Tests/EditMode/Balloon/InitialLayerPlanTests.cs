using BalloonParty.Balloon.Spawner;
using NUnit.Framework;

namespace BalloonParty.Tests.Balloon
{
    // Covers the arithmetic behind initial-fill heavy layering: which lines form a layer, deepest-first
    // round-robin of an uneven heavy count, per-line capacity, and the fall-back (all-zero) cases.
    [TestFixture]
    public class InitialLayerPlanTests
    {
        [Test]
        public void HeavyPerLine_TwoRowsLightOneRowTough_BandsBottomOfEachSegment()
        {
            // spacing 3 over 6 lines → segments {0,1,2} and {3,4,5}; layers at the bottom line of each.
            var alloc = InitialLayerPlan.HeavyPerLine(heavyCount: 4, lineCount: 6, columns: 6, spacing: 3);

            Assert.That(alloc, Is.EqualTo(new[] { 0, 0, 2, 0, 0, 2 }));
        }

        [Test]
        public void HeavyPerLine_UnevenCount_SettlesDeeperLayerFirst()
        {
            var alloc = InitialLayerPlan.HeavyPerLine(heavyCount: 3, lineCount: 6, columns: 6, spacing: 3);

            // 3 heavies over layers {2, 5}: the deeper line (5) takes the extra.
            Assert.That(alloc, Is.EqualTo(new[] { 0, 0, 1, 0, 0, 2 }));
        }

        [Test]
        public void HeavyPerLine_ThreeSegments_RoundRobinsFromTheBottom()
        {
            var alloc = InitialLayerPlan.HeavyPerLine(heavyCount: 3, lineCount: 9, columns: 6, spacing: 3);

            // Layers at {2, 5, 8}; one each, awarded deepest-first.
            Assert.That(alloc, Is.EqualTo(new[] { 0, 0, 1, 0, 0, 1, 0, 0, 1 }));
        }

        [Test]
        public void HeavyPerLine_MoreHeaviesThanCapacity_CapsEachLayerAtColumns()
        {
            var alloc = InitialLayerPlan.HeavyPerLine(heavyCount: 20, lineCount: 6, columns: 6, spacing: 3);

            // Two layers × 6 columns = 12 banded; the remaining 8 are the caller's overflow, not here.
            Assert.That(alloc, Is.EqualTo(new[] { 0, 0, 6, 0, 0, 6 }));
            Assert.That(alloc[2] + alloc[5], Is.EqualTo(12));
        }

        [Test]
        public void HeavyPerLine_SingleFullSegment_BandsItsBottomLine()
        {
            var alloc = InitialLayerPlan.HeavyPerLine(heavyCount: 2, lineCount: 3, columns: 6, spacing: 3);

            Assert.That(alloc, Is.EqualTo(new[] { 0, 0, 2 }));
        }

        [Test]
        public void HeavyPerLine_BoardTooShortForAFullSegment_ReturnsAllZero()
        {
            var alloc = InitialLayerPlan.HeavyPerLine(heavyCount: 4, lineCount: 2, columns: 6, spacing: 3);

            Assert.That(alloc.Length, Is.EqualTo(2));
            Assert.That(alloc, Is.All.Zero);
        }

        [Test]
        public void HeavyPerLine_NoHeavies_ReturnsAllZero()
        {
            var alloc = InitialLayerPlan.HeavyPerLine(heavyCount: 0, lineCount: 6, columns: 6, spacing: 3);

            Assert.That(alloc, Is.All.Zero);
        }

        [Test]
        public void HeavyPerLine_SpacingBelowTwo_ReturnsAllZero()
        {
            Assert.That(
                InitialLayerPlan.HeavyPerLine(heavyCount: 4, lineCount: 6, columns: 6, spacing: 1),
                Is.All.Zero);
            Assert.That(
                InitialLayerPlan.HeavyPerLine(heavyCount: 4, lineCount: 6, columns: 6, spacing: 0),
                Is.All.Zero);
        }
    }
}
