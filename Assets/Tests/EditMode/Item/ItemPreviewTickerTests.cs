using BalloonParty.Item.Preview;
using NUnit.Framework;

namespace BalloonParty.Tests.Item
{
    // ApproachDashCountForLength is internal static and carries no MonoBehaviour/pool/Time dependency, so
    // it is exercised directly here rather than through the ticker's pen machinery — mirroring
    // ItemPreviewEntryTests' approach to ItemPreviewEntry's own pure geometry helper.
    [TestFixture]
    public class ItemPreviewTickerTests
    {
        // DashLength 0.2 + DashSpacing 0.15, the stride used throughout the bug report's own sweep.
        private const float Stride = 0.35f;

        [TestCase(0f)]
        [TestCase(0.05f)]
        [TestCase(0.12f)]
        [TestCase(0.2f)]
        [TestCase(0.34999f)]
        public void ApproachDashCountForLength_ShorterThanOneStride_ReturnsZero(float approachLength)
        {
            Assert.AreEqual(0, ItemPreviewTicker.ApproachDashCountForLength(approachLength, Stride));
        }

        [Test]
        public void ApproachDashCountForLength_ExactlyOneStride_ReturnsZero()
        {
            // An approach that can only just fit DashLength + DashSpacing still has no room left to paint
            // a dash once ComputePaintedLength's slot - DashSpacing is accounted for, so the boundary
            // itself belongs to the zero side, not the one-dash side.
            Assert.AreEqual(0, ItemPreviewTicker.ApproachDashCountForLength(Stride, Stride));
        }

        [Test]
        public void ApproachDashCountForLength_JustOverOneStride_ReturnsOne()
        {
            Assert.AreEqual(1, ItemPreviewTicker.ApproachDashCountForLength(0.4f, Stride));
        }

        // Bomb's own approach from the sweep in the bug report: 1.25 / 0.35 rounds to 4.
        [Test]
        public void ApproachDashCountForLength_BombLength_ReturnsFour()
        {
            Assert.AreEqual(4, ItemPreviewTicker.ApproachDashCountForLength(1.25f, Stride));
        }

        // Shield's own approach from the same sweep: 5.00 / 0.35 rounds to 14.
        [Test]
        public void ApproachDashCountForLength_ShieldLength_ReturnsFourteen()
        {
            Assert.AreEqual(14, ItemPreviewTicker.ApproachDashCountForLength(5f, Stride));
        }

        [Test]
        public void ApproachDashCountForLength_DegenerateStride_ReturnsOneUnlessLengthIsZero()
        {
            Assert.AreEqual(0, ItemPreviewTicker.ApproachDashCountForLength(0f, 0f));
            Assert.AreEqual(1, ItemPreviewTicker.ApproachDashCountForLength(1f, 0f));
        }
    }
}
