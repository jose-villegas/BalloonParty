using BalloonParty.Shared.Extensions;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Shared
{
    /// <summary>
    ///     Guards two properties of the disturbance path gate. First, frame-rate independence: the number of
    ///     stamps must depend on DISTANCE travelled, not on how many rendered frames it took. Second,
    ///     back-fill: a frame that covers several steps must deposit all of them, because the stamp falloffs
    ///     only sum to a flat ridge when every step is placed — a scattered trail leaves isolated steep bumps
    ///     that the speck field reads as a high-gain gradient and flings specks off.
    /// </summary>
    [TestFixture]
    public class DisturbanceTweenExtensionsTests
    {
        [Test]
        public void GateStampSteps_DeltaBelowSpacing_ReturnsZero()
        {
            var anchor = Vector3.zero;
            var current = new Vector3(0.4f, 0f, 0f);

            var steps = DisturbanceTweenExtensions.GateStampSteps(
                current, anchor, spacing: 1f, out var newAnchor, out _);

            Assert.AreEqual(0, steps);
            Assert.AreEqual(anchor, newAnchor, "Anchor must not advance when the gate rejects the step.");
        }

        [Test]
        public void GateStampSteps_DeltaAtExactSpacing_ReturnsOneStep()
        {
            var anchor = Vector3.zero;
            var current = new Vector3(1f, 0f, 0f);

            var steps = DisturbanceTweenExtensions.GateStampSteps(
                current, anchor, spacing: 1f, out var newAnchor, out _);

            Assert.AreEqual(1, steps, "Boundary case (delta == spacing) must stamp, not be gated out.");
            Assert.Less(Vector3.Distance(current, newAnchor), 1e-4f);
        }

        [Test]
        public void GateStampSteps_ZeroOrNegativeSpacing_ReturnsZero()
        {
            var anchor = Vector3.zero;
            var current = new Vector3(100f, 0f, 0f);

            Assert.AreEqual(0, DisturbanceTweenExtensions.GateStampSteps(current, anchor, 0f, out _, out _));
            Assert.AreEqual(0, DisturbanceTweenExtensions.GateStampSteps(current, anchor, -1f, out _, out _));
        }

        [Test]
        public void GateStampSteps_ReturnsNormalizedDirectionTowardCurrentPos()
        {
            var anchor = Vector3.zero;
            var current = new Vector3(3f, 4f, 0f);

            DisturbanceTweenExtensions.GateStampSteps(current, anchor, spacing: 1f, out _, out var direction);

            Assert.AreEqual(1f, direction.magnitude, 0.0001f);
            Assert.AreEqual(new Vector2(0.6f, 0.8f), direction);
        }

        // The bug this replaced: one stamp per frame however far the frame travelled, with the anchor snapped
        // to the current position. An eased path outruns a step while it is fast, so the trail came out
        // scattered at the start and solid only once the balloon slowed.
        [Test]
        public void GateStampSteps_FrameOutrunsSpacing_BackFillsEveryWholeStep()
        {
            var anchor = Vector3.zero;
            var current = new Vector3(3.5f, 0f, 0f);

            var steps = DisturbanceTweenExtensions.GateStampSteps(
                current, anchor, spacing: 1f, out var newAnchor, out _);

            Assert.AreEqual(3, steps, "A frame covering 3.5 steps must deposit all three whole steps.");
            Assert.Less(Vector3.Distance(new Vector3(3f, 0f, 0f), newAnchor), 1e-4f,
                "The anchor advances by whole steps; the 0.5 remainder carries to the next frame.");
        }

        [Test]
        public void GateStampSteps_AccumulatesAcrossSkippedFrames_CarriesRemainder()
        {
            var anchor = Vector3.zero;

            // Three sub-threshold "skipped frames" — none should stamp individually.
            Assert.AreEqual(0, DisturbanceTweenExtensions.GateStampSteps(
                new Vector3(0.3f, 0f, 0f), anchor, spacing: 1f, out anchor, out _));
            Assert.AreEqual(0, DisturbanceTweenExtensions.GateStampSteps(
                new Vector3(0.6f, 0f, 0f), anchor, spacing: 1f, out anchor, out _));
            Assert.AreEqual(0, DisturbanceTweenExtensions.GateStampSteps(
                new Vector3(0.9f, 0f, 0f), anchor, spacing: 1f, out anchor, out _));

            var steps = DisturbanceTweenExtensions.GateStampSteps(
                new Vector3(1.2f, 0f, 0f), anchor, spacing: 1f, out anchor, out _);

            Assert.AreEqual(1, steps, "Delta accumulated across skipped frames must still clear the gate.");
            Assert.Less(Vector3.Distance(new Vector3(1f, 0f, 0f), anchor), 1e-4f,
                "The anchor lands on the step, not on the current position — the 0.2 remainder carries.");
        }

        [Test]
        public void GateStampSteps_RunawayTravel_IsCappedButKeepsAnchorConsistent()
        {
            var anchor = Vector3.zero;
            var current = new Vector3(100f, 0f, 0f);
            const float spacing = 1f;

            var steps = DisturbanceTweenExtensions.GateStampSteps(
                current, anchor, spacing, out var newAnchor, out _);

            Assert.Less(steps, (int)(100f / spacing), "A runaway frame must not monopolise the stamp batch.");
            Assert.Greater(steps, 0);
            Assert.Less(Vector3.Distance(new Vector3(steps * spacing, 0f, 0f), newAnchor), 1e-4f,
                "However many steps were emitted, the anchor must sit on exactly that many.");
        }

        [Test]
        public void GateStampSteps_SameTotalDistance_CoarseAndFineCadence_ProduceSameStampCount()
        {
            const float spacing = 1f;
            const float totalDistance = 10f;

            // Coarse cadence: 10 calls of exactly 1 unit each — a "60Hz-like" per-frame step size.
            var coarseAnchor = Vector3.zero;
            var coarseStamps = 0;

            for (var i = 1; i <= 10; i++)
            {
                var current = new Vector3(i * 1f, 0f, 0f);
                coarseStamps += DisturbanceTweenExtensions.GateStampSteps(
                    current, coarseAnchor, spacing, out coarseAnchor, out _);
            }

            // Fine cadence: 1000 calls of 0.01 units each — a "120Hz-like" (or higher) per-frame step size
            // covering the exact same total distance. Frame-rate independence means this must produce the
            // SAME stamp count as the coarse cadence above, not ~2x or ~100x as many.
            var fineAnchor = Vector3.zero;
            var fineStamps = 0;

            for (var i = 1; i <= 1000; i++)
            {
                var current = new Vector3(i * 0.01f, 0f, 0f);
                fineStamps += DisturbanceTweenExtensions.GateStampSteps(
                    current, fineAnchor, spacing, out fineAnchor, out _);
            }

            Assert.AreEqual(coarseStamps, fineStamps, "Stamp count must track distance, not call/frame count.");
            Assert.AreEqual((int)(totalDistance / spacing), coarseStamps);
            Assert.Less(Vector3.Distance(coarseAnchor, fineAnchor), 1e-3f,
                "Both cadences must land on the same final anchor.");
        }
    }
}
