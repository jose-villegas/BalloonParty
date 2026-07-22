using BalloonParty.EditorUI.Tables;
using NUnit.Framework;

namespace BalloonParty.EditorUI.Tests.Tables
{
    [TestFixture]
    public class TableDrawHelperTests
    {
        [Test]
        public void ComputeColumnX_ColumnZeroWithoutGap_ReturnsZero()
        {
            var x = TableDrawHelper.ComputeColumnX(0, new[] { 100f, 50f }, new int[0], 20f, 1f);

            Assert.That(x, Is.EqualTo(0f));
        }

        [Test]
        public void ComputeColumnX_ColumnZeroWithGap_ReturnsGapWidth()
        {
            var x = TableDrawHelper.ComputeColumnX(0, new[] { 100f, 50f }, new[] { 0 }, 20f, 1f);

            Assert.That(x, Is.EqualTo(20f));
        }

        [Test]
        public void ComputeColumnX_GapBeforeIntermediateColumn_AccumulatesWidthsSeparatorsAndGap()
        {
            var x = TableDrawHelper.ComputeColumnX(2, new[] { 100f, 50f, 25f }, new[] { 1 }, 20f, 1f);

            Assert.That(x, Is.EqualTo(172f));
        }

        [Test]
        public void ComputeColumnX_FullTraversalWithMultipleGaps_ReturnsExpectedSum()
        {
            var x = TableDrawHelper.ComputeColumnX(4, new[] { 30f, 20f, 10f, 5f }, new[] { 0, 2, 4 }, 10f, 2f);

            Assert.That(x, Is.EqualTo(103f));
        }

        [Test]
        public void HasGapBefore_ColumnListedInGapArray_ReturnsTrue()
        {
            var hasGap = TableDrawHelper.HasGapBefore(1, new[] { 1, 4, 6 });

            Assert.That(hasGap, Is.True);
        }

        [Test]
        public void HasGapBefore_ColumnNotListedInGapArray_ReturnsFalse()
        {
            var hasGap = TableDrawHelper.HasGapBefore(2, new[] { 1, 4, 6 });

            Assert.That(hasGap, Is.False);
        }

        [Test]
        public void ComputeColumnX_WithoutGaps_ReturnsWidthAndSeparatorSum()
        {
            var x = TableDrawHelper.ComputeColumnX(3, new[] { 10f, 20f, 30f }, new int[0], 20f, 2f);

            Assert.That(x, Is.EqualTo(66f));
        }
    }
}
