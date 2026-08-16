using BalloonParty.Game.Score.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class FormationOverlapResolverTests
    {
        private const float NoPadding = 0f;
        private const float FullPush = 1f;

        [Test]
        public void Resolve_TwoOverlappingEqualCircles_PushApartSymmetricallyToExactlyTouching()
        {
            var centers = new[] { new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f) };
            var radii = new[] { 1.5f, 1.5f };
            var weights = new[] { 1f, 1f };
            var offsets = new Vector3[2];

            Resolve(centers, radii, weights, NoPadding, FullPush, 2, offsets);

            var newDistance = Vector3.Distance(centers[0] + offsets[0], centers[1] + offsets[1]);
            Assert.AreEqual(radii[0] + radii[1], newDistance, 1e-4f);
            Assert.AreEqual(offsets[0], -offsets[1], "an equal-weight pair splits the correction symmetrically");
        }

        [Test]
        public void Resolve_NonOverlappingPair_ProducesZeroOffsets()
        {
            var centers = new[] { Vector3.zero, new Vector3(10f, 0f, 0f) };
            var radii = new[] { 1f, 1f };
            var weights = new[] { 1f, 1f };
            var offsets = new Vector3[2];

            Resolve(centers, radii, weights, NoPadding, FullPush, 2, offsets);

            Assert.AreEqual(Vector3.zero, offsets[0]);
            Assert.AreEqual(Vector3.zero, offsets[1]);
        }

        [Test]
        public void Resolve_ExactlyTouchingPairWithNoPadding_ProducesZeroOffsets()
        {
            var centers = new[] { Vector3.zero, new Vector3(2f, 0f, 0f) };
            var radii = new[] { 1f, 1f };
            var weights = new[] { 1f, 1f };
            var offsets = new Vector3[2];

            Resolve(centers, radii, weights, NoPadding, FullPush, 2, offsets);

            Assert.AreEqual(Vector3.zero, offsets[0]);
            Assert.AreEqual(Vector3.zero, offsets[1]);
        }

        [Test]
        public void Resolve_PaddingInflatesEffectiveDistance_PushesAnExactlyTouchingPairApart()
        {
            var centers = new[] { Vector3.zero, new Vector3(2f, 0f, 0f) };
            var radii = new[] { 1f, 1f };
            var weights = new[] { 1f, 1f };
            var offsets = new Vector3[2];

            Resolve(centers, radii, weights, 0.2f, FullPush, 2, offsets);

            var newDistance = Vector3.Distance(centers[0] + offsets[0], centers[1] + offsets[1]);
            Assert.AreEqual(2.2f, newDistance, 1e-4f);
        }

        [Test]
        public void Resolve_HeterogeneousPadding_UsesTheLargerOfEachPairsPadding()
        {
            // Per-formation padding (not a single shared value — see BigScoreFormationSettings.OverlapPadding
            // being read per-Group.Settings): the effective gap for a pair is the LARGER of the two, so one
            // group authoring a bigger gap than its neighbour still gets that bigger gap honoured.
            var centers = new[] { Vector3.zero, new Vector3(2f, 0f, 0f) };
            var radii = new[] { 1f, 1f };
            var weights = new[] { 1f, 1f };
            var paddings = new[] { 0.1f, 0.5f };
            var offsets = new Vector3[2];

            FormationOverlapResolver.Resolve(
                centers, radii, weights, paddings, Uniform(FullPush, 2), 2, offsets);

            var newDistance = Vector3.Distance(centers[0] + offsets[0], centers[1] + offsets[1]);
            Assert.AreEqual(2.5f, newDistance, 1e-4f, "must use the larger padding (0.5), not the smaller (0.1)");
        }

        [Test]
        public void Resolve_HeterogeneousMaxPushFraction_ClampsEachFormationToItsOwnBudget()
        {
            // Deep, near-coincident overlap so the RAW correction would exceed both budgets, isolating the
            // clamp itself: each formation's clamp uses its OWN maxPushFraction, not its partner's or a
            // single shared value.
            var centers = new[] { Vector3.zero, new Vector3(0.01f, 0f, 0f) };
            var radii = new[] { 3f, 3f };
            var weights = new[] { 1f, 1f };
            var maxPushFractions = new[] { 0.2f, 0.6f };
            var offsets = new Vector3[2];

            FormationOverlapResolver.Resolve(
                centers, radii, weights, Uniform(NoPadding, 2), maxPushFractions, 2, offsets);

            Assert.LessOrEqual(offsets[0].magnitude, radii[0] * maxPushFractions[0] + 1e-4f);
            Assert.LessOrEqual(offsets[1].magnitude, radii[1] * maxPushFractions[1] + 1e-4f);
            Assert.Greater(offsets[1].magnitude, offsets[0].magnitude, "the larger budget must allow the larger push");
        }

        [Test]
        public void Resolve_NearCoincidentCenters_ProducesFiniteDeterministicOffsets()
        {
            var centers = new[] { new Vector3(5f, 5f, 0f), new Vector3(5f, 5f, 0f) };
            var radii = new[] { 1f, 1f };
            var weights = new[] { 1f, 1f };
            var firstRun = new Vector3[2];
            var secondRun = new Vector3[2];

            Resolve(centers, radii, weights, NoPadding, FullPush, 2, firstRun);
            Resolve(centers, radii, weights, NoPadding, FullPush, 2, secondRun);

            Assert.IsFalse(float.IsNaN(firstRun[0].x) || float.IsNaN(firstRun[0].y));
            Assert.AreNotEqual(Vector3.zero, firstRun[0], "a coincident pair must still separate");
            Assert.AreEqual(firstRun[0], secondRun[0], "no per-call randomness — repeat calls agree");
            Assert.AreEqual(firstRun[1], secondRun[1]);
        }

        [Test]
        public void Resolve_FormationsSeparatedOnlyInZ_StillTreatedAsOverlapping_OffsetsStayInXYPlane()
        {
            // The board reads as flat 2D under the orthographic camera, but a formation's travel curve
            // moves it through a large, camera-invisible Z distance toward the score-bar canvas. Two
            // formations at the SAME XY position but far apart in Z (as two formations launched a few
            // frames apart from each other would be) must still be resolved as fully overlapping — a 3D
            // distance check would see them as far apart and never push them apart on screen.
            var centers = new[] { new Vector3(3f, 3f, 0f), new Vector3(3f, 3f, 90f) };
            var radii = new[] { 1f, 1f };
            var weights = new[] { 1f, 1f };
            var offsets = new Vector3[2];

            Resolve(centers, radii, weights, NoPadding, FullPush, 2, offsets);

            Assert.AreNotEqual(Vector3.zero, offsets[0], "Z-only separation must still read as fully overlapping");
            Assert.AreNotEqual(Vector3.zero, offsets[1]);
            Assert.AreEqual(0f, offsets[0].z, "the correction must never touch Z — it would fight the travel curve");
            Assert.AreEqual(0f, offsets[1].z);
        }

        [Test]
        public void Resolve_ImmovableFormation_ReceivesZeroOffsetPartnerAbsorbsFullCorrection()
        {
            var centers = new[] { Vector3.zero, new Vector3(1f, 0f, 0f) };
            var radii = new[] { 1f, 1f };
            var weights = new[] { 0f, 1f };
            var offsets = new Vector3[2];

            Resolve(centers, radii, weights, NoPadding, FullPush, 2, offsets);

            Assert.AreEqual(Vector3.zero, offsets[0], "moveWeight 0 must not move");
            var newDistance = Vector3.Distance(centers[0], centers[1] + offsets[1]);
            Assert.AreEqual(radii[0] + radii[1], newDistance, 1e-4f, "the mover alone closes the whole gap");
        }

        [Test]
        public void Resolve_BothFormationsImmovable_NoOffsetNoThrow()
        {
            var centers = new[] { Vector3.zero, new Vector3(1f, 0f, 0f) };
            var radii = new[] { 1f, 1f };
            var weights = new[] { 0f, 0f };
            var offsets = new Vector3[2];

            Assert.DoesNotThrow(() => Resolve(centers, radii, weights, NoPadding, FullPush, 2, offsets));

            Assert.AreEqual(Vector3.zero, offsets[0]);
            Assert.AreEqual(Vector3.zero, offsets[1]);
        }

        [Test]
        public void Resolve_LargeOverlap_EachOffsetClampedToItsOwnMaxPushFraction()
        {
            var centers = new[] { Vector3.zero, new Vector3(0.01f, 0f, 0f) };
            var radii = new[] { 3f, 3f };
            var weights = new[] { 1f, 1f };
            const float maxPushFraction = 0.4f;
            var offsets = new Vector3[2];

            Resolve(centers, radii, weights, NoPadding, maxPushFraction, 2, offsets);

            Assert.LessOrEqual(offsets[0].magnitude, radii[0] * maxPushFraction + 1e-4f);
            Assert.LessOrEqual(offsets[1].magnitude, radii[1] * maxPushFraction + 1e-4f);
        }

        [Test]
        public void Resolve_ThreeMutuallyOverlappingCircles_MiddleOneAccumulatesBothPairPushes()
        {
            // Deliberately NOT coincident/colinear: FallbackDirection(i, j) depends only on (j - i), so
            // with all three centres exactly coincident at consecutive indices, FallbackDirection(0, 1)
            // and FallbackDirection(1, 2) resolve to the identical vector — the middle index's two pushes
            // would then exactly cancel to zero instead of summing, defeating this test.
            var centers = new[] { new Vector3(0f, 0f, 0f), new Vector3(0.2f, 0f, 0f), new Vector3(0.5f, 0.3f, 0f) };
            var radii = new[] { 1f, 1f, 1f };
            var weights = new[] { 1f, 1f, 1f };
            var offsets = new Vector3[3];

            Resolve(centers, radii, weights, NoPadding, FullPush, 3, offsets);

            // Isolate each pair's contribution to index 1 via a standalone 2-body call, so the 3-body
            // result can be checked against their SUM — proving index 1 accumulates both pushes rather
            // than one silently overwriting the other.
            var pairZeroOne = new Vector3[2];
            Resolve(
                new[] { centers[0], centers[1] }, new[] { radii[0], radii[1] }, new[] { weights[0], weights[1] },
                NoPadding, FullPush, 2, pairZeroOne);
            var pairOneTwo = new Vector3[2];
            Resolve(
                new[] { centers[1], centers[2] }, new[] { radii[1], radii[2] }, new[] { weights[1], weights[2] },
                NoPadding, FullPush, 2, pairOneTwo);

            var expectedMiddle = pairZeroOne[1] + pairOneTwo[0];
            Assert.AreEqual(expectedMiddle.x, offsets[1].x, 1e-4f);
            Assert.AreEqual(expectedMiddle.y, offsets[1].y, 1e-4f);
            Assert.AreNotEqual(Vector3.zero, offsets[1], "the shared middle circle must move from both pairs");
        }

        [Test]
        public void Resolve_CountZero_NoThrowAndNoOffsetsTouched()
        {
            var centers = new[] { Vector3.zero, Vector3.zero };
            var radii = new[] { 1f, 1f };
            var weights = new[] { 1f, 1f };
            var sentinel = new Vector3(9f, 9f, 9f);
            var offsets = new[] { sentinel, sentinel };

            Assert.DoesNotThrow(() => Resolve(centers, radii, weights, NoPadding, FullPush, 0, offsets));

            Assert.AreEqual(sentinel, offsets[0], "count 0 must not touch any entry");
            Assert.AreEqual(sentinel, offsets[1]);
        }

        [Test]
        public void Resolve_SingleFormation_NoSelfOverlapProducesZeroOffset()
        {
            // Guards the `j = i + 1` loop bound: a regression to `j = i` would compare a circle against
            // itself (distance 0, always inside its own radius), pushing a lone formation away from
            // itself for no reason.
            var centers = new[] { Vector3.zero };
            var radii = new[] { 1f };
            var weights = new[] { 1f };
            var offsets = new Vector3[1];

            Resolve(centers, radii, weights, NoPadding, FullPush, 1, offsets);

            Assert.AreEqual(Vector3.zero, offsets[0]);
        }

        [Test]
        public void Resolve_SmallerCountOnASubsequentCall_LeavesStaleEntriesPastCountUntouched()
        {
            // Documents the caller contract: Resolve only ever clears/writes indices below `count`. A
            // caller reusing the same offsets array across frames with a shrinking formation count must
            // not read past its OWN current count — this locks in that the resolver does not clear stale
            // entries for it.
            var centers = new[] { Vector3.zero, new Vector3(0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f) };
            var radii = new[] { 1f, 1f, 1f };
            var weights = new[] { 1f, 1f, 1f };
            var offsets = new Vector3[3];
            Resolve(centers, radii, weights, NoPadding, FullPush, 3, offsets);
            Assert.AreNotEqual(Vector3.zero, offsets[2], "sanity check: index 2 must be non-zero after the first call");
            var staleValue = offsets[2];

            var farApartCenters = new[] { Vector3.zero, new Vector3(100f, 0f, 0f) };
            Resolve(farApartCenters, radii, weights, NoPadding, FullPush, 2, offsets);

            Assert.AreEqual(Vector3.zero, offsets[0]);
            Assert.AreEqual(Vector3.zero, offsets[1]);
            Assert.AreEqual(staleValue, offsets[2], "index 2 is past the new count and must be left exactly as-is");
        }

        [Test]
        public void Resolve_MixedSet_OnlyTheOverlappingPairMovesIsolatedFormationsStayPut()
        {
            var centers = new[] { Vector3.zero, new Vector3(0.5f, 0f, 0f), new Vector3(50f, 50f, 0f) };
            var radii = new[] { 1f, 1f, 1f };
            var weights = new[] { 1f, 1f, 1f };
            var offsets = new Vector3[3];

            Resolve(centers, radii, weights, NoPadding, FullPush, 3, offsets);

            Assert.AreNotEqual(Vector3.zero, offsets[0], "index 0 overlaps index 1");
            Assert.AreNotEqual(Vector3.zero, offsets[1], "index 1 overlaps index 0");
            Assert.AreEqual(Vector3.zero, offsets[2], "the far-away formation is untouched");
        }

        // Most tests use the SAME padding/maxPushFraction for every formation in the call — Resolve takes
        // per-formation arrays (see Resolve_HeterogeneousPadding.../Resolve_HeterogeneousMaxPushFraction...
        // for the cases that don't), so this just fills a uniform array without repeating `new[] { x, x }`
        // at every call site.
        private static void Resolve(
            Vector3[] centers, float[] radii, float[] weights,
            float padding, float maxPushFraction, int count, Vector3[] offsets)
        {
            FormationOverlapResolver.Resolve(
                centers, radii, weights, Uniform(padding, count), Uniform(maxPushFraction, count), count, offsets);
        }

        private static float[] Uniform(float value, int count)
        {
            var array = new float[count];
            for (var i = 0; i < count; i++)
            {
                array[i] = value;
            }

            return array;
        }
    }
}
