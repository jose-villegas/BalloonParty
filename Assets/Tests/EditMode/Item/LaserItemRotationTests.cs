using BalloonParty.Item;
using NUnit.Framework;

namespace BalloonParty.Tests.Item
{
    // IsDwelling is internal static and carries no MonoBehaviour/Time dependency, so it is exercised
    // directly here rather than through the component's Update loop — mirroring ItemPreviewTickerTests'
    // approach to ApproachDashCountForLength. It is also the single comparison DrawnAngle and
    // ISpinningItemVisual.IsSettled both key off, so this doubles as coverage for the settled signal
    // ItemRangePreviewController now re-blooms on.
    [TestFixture]
    public class LaserItemRotationTests
    {
        // TransitionFraction is 0.25f, so a 2s step dwells for the first 1.5s and eases over the last 0.5s.
        private const float StepSeconds = 2f;
        private const float DwellBoundary = StepSeconds * 0.75f;

        [TestCase(0f)]
        [TestCase(1f)]
        [TestCase(1.4999f)]
        public void IsDwelling_WithinDwellWindow_ReturnsTrue(float elapsedSeconds)
        {
            Assert.IsTrue(LaserItemRotation.IsDwelling(elapsedSeconds, StepSeconds));
        }

        [Test]
        public void IsDwelling_ExactlyAtDwellBoundary_ReturnsTrue()
        {
            // The boundary belongs to the dwell side (elapsed <= dwellDuration), not the transition side —
            // the frame the transition begins still reads as settled.
            Assert.IsTrue(LaserItemRotation.IsDwelling(DwellBoundary, StepSeconds));
        }

        [TestCase(1.50001f)]
        [TestCase(1.75f)]
        [TestCase(2f)]
        public void IsDwelling_PastDwellBoundary_ReturnsFalse(float elapsedSeconds)
        {
            Assert.IsFalse(LaserItemRotation.IsDwelling(elapsedSeconds, StepSeconds));
        }
    }
}
