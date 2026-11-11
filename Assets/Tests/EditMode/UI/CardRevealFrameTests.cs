using BalloonParty.UI.CardReveal;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.UI
{
    [TestFixture]
    public class CardRevealFrameTests
    {
        private const float Tolerance = 1e-5f;

        [Test]
        public void FirstFrontFrame_IsTheSheetsTopLeftCell()
        {
            AssertRect(new Rect(0f, 7f / 8f, 1f / 6f, 1f / 8f), CardRevealView.FrameRect(0, true, 6, 4));
        }

        [Test]
        public void FirstBackFrame_IsTheTopLeftCellOfTheBottomHalf()
        {
            AssertRect(new Rect(0f, 3f / 8f, 1f / 6f, 1f / 8f), CardRevealView.FrameRect(0, false, 6, 4));
        }

        [Test]
        public void Frames_ReadLeftToRight_ThenTopToBottom()
        {
            AssertRect(new Rect(5f / 6f, 7f / 8f, 1f / 6f, 1f / 8f), CardRevealView.FrameRect(5, true, 6, 4));
            AssertRect(new Rect(0f, 6f / 8f, 1f / 6f, 1f / 8f), CardRevealView.FrameRect(6, true, 6, 4));
        }

        [Test]
        public void LastFrames_EndAtEachHalfsBottomRightCell()
        {
            AssertRect(new Rect(5f / 6f, 4f / 8f, 1f / 6f, 1f / 8f), CardRevealView.FrameRect(23, true, 6, 4));
            AssertRect(new Rect(5f / 6f, 0f, 1f / 6f, 1f / 8f), CardRevealView.FrameRect(23, false, 6, 4));
        }

        private static void AssertRect(Rect expected, Rect actual)
        {
            Assert.AreEqual(expected.x, actual.x, Tolerance, "x");
            Assert.AreEqual(expected.y, actual.y, Tolerance, "y");
            Assert.AreEqual(expected.width, actual.width, Tolerance, "width");
            Assert.AreEqual(expected.height, actual.height, Tolerance, "height");
        }
    }
}
