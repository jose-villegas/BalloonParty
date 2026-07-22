using BalloonParty.EditorUI.Tables;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.EditorUI.Tests.Tables
{
    [TestFixture]
    public class RowColorResolverTests
    {
        private static readonly Color FocusedColor = new(0.9f, 0.1f, 0.1f, 1f);
        private static readonly Color ActiveColor = new(0.1f, 0.9f, 0.1f, 1f);
        private static readonly Color FallbackColor = new(0.1f, 0.1f, 0.9f, 1f);
        private static readonly Color OddColor = new(0.8f, 0.8f, 0.8f, 1f);
        private static readonly Color EvenColor = new(0.2f, 0.2f, 0.2f, 1f);

        [Test]
        public void Resolve_FocusedAndActive_ReturnsFocusedColor()
        {
            var color = RowColorResolver.Resolve(true, true, false, 0, FocusedColor, ActiveColor, FallbackColor, OddColor, EvenColor);

            Assert.That(color, Is.EqualTo(FocusedColor));
        }

        [Test]
        public void Resolve_ActiveAndFallback_ReturnsActiveColor()
        {
            var color = RowColorResolver.Resolve(false, true, true, 0, FocusedColor, ActiveColor, FallbackColor, OddColor, EvenColor);

            Assert.That(color, Is.EqualTo(ActiveColor));
        }

        [Test]
        public void Resolve_FallbackRow_ReturnsFallbackColor()
        {
            var color = RowColorResolver.Resolve(false, false, true, 0, FocusedColor, ActiveColor, FallbackColor, OddColor, EvenColor);

            Assert.That(color, Is.EqualTo(FallbackColor));
        }

        [Test]
        public void Resolve_EvenRowIndex_ReturnsEvenColor()
        {
            var color = RowColorResolver.Resolve(false, false, false, 2, FocusedColor, ActiveColor, FallbackColor, OddColor, EvenColor);

            Assert.That(color, Is.EqualTo(EvenColor));
        }

        [Test]
        public void Resolve_OddRowIndex_ReturnsOddColor()
        {
            var color = RowColorResolver.Resolve(false, false, false, 1, FocusedColor, ActiveColor, FallbackColor, OddColor, EvenColor);

            Assert.That(color, Is.EqualTo(OddColor));
        }

        [Test]
        public void IsInRange_SelectedLevelMatchesFromBoundary_ReturnsTrue()
        {
            var isInRange = RowColorResolver.IsInRange(3, 3, 5, false);

            Assert.That(isInRange, Is.True);
        }

        [Test]
        public void IsInRange_SelectedLevelMatchesToBoundary_ReturnsTrue()
        {
            var isInRange = RowColorResolver.IsInRange(5, 3, 5, false);

            Assert.That(isInRange, Is.True);
        }

        [Test]
        public void IsInRange_SelectedLevelOutsideRange_ReturnsFalse()
        {
            var isInRange = RowColorResolver.IsInRange(6, 3, 5, false);

            Assert.That(isInRange, Is.False);
        }

        [Test]
        public void IsInRange_FallbackRow_ReturnsFalse()
        {
            var isInRange = RowColorResolver.IsInRange(4, 3, 5, true);

            Assert.That(isInRange, Is.False);
        }
    }
}
