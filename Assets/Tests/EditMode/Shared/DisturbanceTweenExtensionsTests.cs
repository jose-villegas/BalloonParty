using BalloonParty.Shared.Extensions;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Shared
{
    /// <summary>
    ///     Regression guard for the frame-rate-independence fix: the number of disturbance stamps must
    ///     depend on DISTANCE travelled, not on how many OnUpdate calls (rendered frames) it took to
    ///     travel it. Exercises the pure gating decision extracted from
    ///     <c>DisturbanceTweenExtensions.StampStep</c> (see the seam note below) — no Transform or
    ///     DisturbanceFieldService involved, since the decision itself needs neither.
    /// </summary>
    /// <remarks>
    ///     SEAM REQUIRED: as of writing, this decision lives inline inside the private static
    ///     <c>StampStep(Transform, DisturbanceFieldService, StampProfile, Vector3)</c>, which is not
    ///     unit-testable (both dependencies need a live scene/GPU resources to construct meaningfully).
    ///     Extract the pure part into an internal static method on the same class, e.g.:
    ///     <code>
    ///     internal static bool TryGateStamp(
    ///         Vector3 currentPos, Vector3 lastStampPos, float spacing, out Vector3 newAnchor, out Vector2 direction)
    ///     {
    ///         var delta = currentPos - lastStampPos;
    ///         if (spacing &lt;= 0f || delta.sqrMagnitude &lt; spacing * spacing)
    ///         {
    ///             newAnchor = lastStampPos;
    ///             direction = Vector2.zero;
    ///             return false;
    ///         }
    ///         newAnchor = currentPos;
    ///         direction = new Vector2(delta.x, delta.y).normalized;
    ///         return true;
    ///     }
    ///     </code>
    ///     and have <c>StampStep</c> call it, only touching <c>field.Stamp</c> when it returns true. Same
    ///     math, same branch, no behavior change — just a seam. This test file will not compile until
    ///     that extraction lands.
    /// </remarks>
    [TestFixture]
    public class DisturbanceTweenExtensionsTests
    {
        [Test]
        public void TryGateStamp_DeltaBelowSpacing_DoesNotStamp()
        {
            var anchor = Vector3.zero;
            var current = new Vector3(0.4f, 0f, 0f);

            var stamped = DisturbanceTweenExtensions.TryGateStamp(
                current, anchor, spacing: 1f, out var newAnchor, out _);

            Assert.IsFalse(stamped);
            Assert.AreEqual(anchor, newAnchor, "Anchor must not advance when the gate rejects the step.");
        }

        [Test]
        public void TryGateStamp_DeltaAtExactSpacing_Stamps()
        {
            var anchor = Vector3.zero;
            var current = new Vector3(1f, 0f, 0f);

            var stamped = DisturbanceTweenExtensions.TryGateStamp(
                current, anchor, spacing: 1f, out var newAnchor, out _);

            Assert.IsTrue(stamped, "Boundary case (delta == spacing) must stamp, not be gated out.");
            Assert.AreEqual(current, newAnchor);
        }

        [Test]
        public void TryGateStamp_ZeroOrNegativeSpacing_NeverStamps()
        {
            var anchor = Vector3.zero;
            var current = new Vector3(100f, 0f, 0f);

            Assert.IsFalse(DisturbanceTweenExtensions.TryGateStamp(current, anchor, 0f, out _, out _));
            Assert.IsFalse(DisturbanceTweenExtensions.TryGateStamp(current, anchor, -1f, out _, out _));
        }

        [Test]
        public void TryGateStamp_ReturnsNormalizedDirectionTowardCurrentPos()
        {
            var anchor = Vector3.zero;
            var current = new Vector3(3f, 4f, 0f);

            DisturbanceTweenExtensions.TryGateStamp(current, anchor, spacing: 1f, out _, out var direction);

            Assert.AreEqual(1f, direction.magnitude, 0.0001f);
            Assert.AreEqual(new Vector2(0.6f, 0.8f), direction);
        }

        [Test]
        public void TryGateStamp_AccumulatesAcrossSkippedFrames_StampsOnlyOnceThresholdCleared()
        {
            var anchor = Vector3.zero;

            // Three sub-threshold "skipped frames" — none should stamp individually.
            Assert.IsFalse(DisturbanceTweenExtensions.TryGateStamp(
                new Vector3(0.3f, 0f, 0f), anchor, spacing: 1f, out anchor, out _));
            Assert.IsFalse(DisturbanceTweenExtensions.TryGateStamp(
                new Vector3(0.6f, 0f, 0f), anchor, spacing: 1f, out anchor, out _));
            Assert.IsFalse(DisturbanceTweenExtensions.TryGateStamp(
                new Vector3(0.9f, 0f, 0f), anchor, spacing: 1f, out anchor, out _));

            // The accumulated delta (0.9 -> 1.2) finally clears spacing on this call.
            var stamped = DisturbanceTweenExtensions.TryGateStamp(
                new Vector3(1.2f, 0f, 0f), anchor, spacing: 1f, out anchor, out _);

            Assert.IsTrue(stamped, "Delta accumulated across skipped frames must still clear the gate.");
            Assert.AreEqual(new Vector3(1.2f, 0f, 0f), anchor);
        }

        [Test]
        public void TryGateStamp_SameTotalDistance_CoarseAndFineStepCadence_ProduceSameStampCount()
        {
            const float spacing = 1f;
            const float totalDistance = 10f;

            // Coarse cadence: 10 calls of exactly 1 unit each — a "60Hz-like" per-frame step size.
            var coarseAnchor = Vector3.zero;
            var coarseStamps = 0;

            for (var i = 1; i <= 10; i++)
            {
                var current = new Vector3(i * 1f, 0f, 0f);

                if (DisturbanceTweenExtensions.TryGateStamp(current, coarseAnchor, spacing, out coarseAnchor, out _))
                {
                    coarseStamps++;
                }
            }

            // Fine cadence: 1000 calls of 0.01 units each — a "120Hz-like" (or higher) per-frame step
            // size covering the exact same total distance. Frame-rate independence means this must
            // produce the SAME stamp count as the coarse cadence above, not ~2x or ~100x as many.
            var fineAnchor = Vector3.zero;
            var fineStamps = 0;

            for (var i = 1; i <= 1000; i++)
            {
                var current = new Vector3(i * 0.01f, 0f, 0f);

                if (DisturbanceTweenExtensions.TryGateStamp(current, fineAnchor, spacing, out fineAnchor, out _))
                {
                    fineStamps++;
                }
            }

            Assert.AreEqual(coarseStamps, fineStamps, "Stamp count must track distance, not call/frame count.");
            Assert.AreEqual((int)(totalDistance / spacing), coarseStamps);
            Assert.AreEqual(coarseAnchor, fineAnchor, "Both cadences must land on the same final anchor.");
        }
    }
}
