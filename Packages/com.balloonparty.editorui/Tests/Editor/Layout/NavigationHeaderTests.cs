using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.EditorUI.Tests.Layout
{
    [TestFixture]
    public class NavigationHeaderTests
    {
        [Test]
        public void ClampValue_ValueBelowMinimum_ReturnsMinimum()
        {
            Assert.That(ClampLikeNavigationHeader(-3, 1), Is.EqualTo(1));
        }

        [Test]
        public void ClampValue_ValueAtMinimum_ReturnsOriginalValue()
        {
            Assert.That(ClampLikeNavigationHeader(4, 4), Is.EqualTo(4));
        }

        [Test]
        public void ClampValue_ValueAboveMinimum_ReturnsOriginalValue()
        {
            Assert.That(ClampLikeNavigationHeader(7, 3), Is.EqualTo(7));
        }

        private static int ClampLikeNavigationHeader(int value, int min)
        {
            return Mathf.Max(min, value);
        }
    }
}
