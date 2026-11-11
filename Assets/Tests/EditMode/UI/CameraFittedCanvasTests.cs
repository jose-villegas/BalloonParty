using BalloonParty.UI;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.UI
{
    [TestFixture]
    public class CameraFittedCanvasTests
    {
        private const float Tolerance = 1e-3f;

        private static readonly Vector2 Reference = new(225f, 400f);

        [Test]
        public void CanvasSize_ScreenOfTheReferenceShape_IsTheReference()
        {
            var size = CameraFittedCanvas.CanvasSize(new Vector2(1080f, 1920f), Reference, 0.5f);

            Assert.AreEqual(Reference.x, size.x, Tolerance);
            Assert.AreEqual(Reference.y, size.y, Tolerance);
        }

        [Test]
        public void CanvasSize_MatchingWidth_KeepsTheReferenceWidth()
        {
            var size = CameraFittedCanvas.CanvasSize(new Vector2(1080f, 2400f), Reference, 0f);

            Assert.AreEqual(225f, size.x, Tolerance);
            Assert.AreEqual(500f, size.y, Tolerance);
        }

        [Test]
        public void CanvasSize_MatchingHeight_KeepsTheReferenceHeight()
        {
            var size = CameraFittedCanvas.CanvasSize(new Vector2(1080f, 2400f), Reference, 1f);

            Assert.AreEqual(180f, size.x, Tolerance);
            Assert.AreEqual(400f, size.y, Tolerance);
        }

        [Test]
        public void CanvasSize_HalfwayMatch_ScalesByTheGeometricMeanOfBothFactors()
        {
            var size = CameraFittedCanvas.CanvasSize(new Vector2(1080f, 2400f), Reference, 0.5f);

            var factor = Mathf.Sqrt(1080f / 225f * (2400f / 400f));
            Assert.AreEqual(1080f / factor, size.x, Tolerance);
            Assert.AreEqual(2400f / factor, size.y, Tolerance);
        }
    }
}
