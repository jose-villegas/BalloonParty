using System.Collections.Generic;
using BalloonParty.Prediction;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Prediction
{
    [TestFixture]
    public class TraceHitGeometryTests
    {
        private List<Vector3> _points;

        [SetUp]
        public void SetUp()
        {
            _points = new List<Vector3>();
        }

        [Test]
        public void TryFindSurfaceHit_FewerThanTwoPoints_ReturnsFalse()
        {
            _points.Add(Vector3.zero);

            var found = TraceHitGeometry.TryFindSurfaceHit(_points, Vector3.one, 1f, out _, out _, out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void TryFindSurfaceHit_NullList_ReturnsFalse()
        {
            var found = TraceHitGeometry.TryFindSurfaceHit(null, Vector3.zero, 1f, out _, out _, out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void TryFindSurfaceHit_LineThroughCenter_HitsSurfaceOnEntrySide()
        {
            _points.Add(new Vector3(0f, 0f, 0f));
            _points.Add(new Vector3(10f, 0f, 0f));

            var found = TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(5f, 0f, 0f), 2f, out var hitPoint, out _, out _);

            Assert.IsTrue(found);
            // Travelling +X, the surface is first touched on the -X side of the circle.
            Assert.AreEqual(3f, hitPoint.x, 0.0001f);
            Assert.AreEqual(0f, hitPoint.y, 0.0001f);
        }

        [Test]
        public void TryFindSurfaceHit_OffsetChord_EntryMatchesCircleEquation()
        {
            _points.Add(new Vector3(0f, 3f, 0f));
            _points.Add(new Vector3(20f, 3f, 0f));

            var found = TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(10f, 0f, 0f), 5f, out var hitPoint, out _, out _);

            Assert.IsTrue(found);
            // Chord at y=3 on a radius-5 circle centred at (10,0): half-chord = 4, entry at x = 6.
            Assert.AreEqual(6f, hitPoint.x, 0.0001f);
            Assert.AreEqual(3f, hitPoint.y, 0.0001f);
            Assert.AreEqual(5f, (hitPoint - new Vector3(10f, 0f, 0f)).magnitude, 0.0001f);
        }

        [Test]
        public void TryFindSurfaceHit_LineMissesCircle_ReturnsFalse()
        {
            _points.Add(new Vector3(0f, 6f, 0f));
            _points.Add(new Vector3(20f, 6f, 0f));

            var found = TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(10f, 0f, 0f), 5f, out _, out _, out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void TryFindSurfaceHit_CircleEntirelyBehindSegment_ReturnsFalse()
        {
            _points.Add(new Vector3(10f, 0f, 0f));
            _points.Add(new Vector3(20f, 0f, 0f));

            var found = TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(5f, 0f, 0f), 2f, out _, out _, out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void TryFindSurfaceHit_CircleBeyondSegmentEnd_ReturnsFalse()
        {
            _points.Add(new Vector3(0f, 0f, 0f));
            _points.Add(new Vector3(10f, 0f, 0f));

            var found = TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(15f, 0f, 0f), 2f, out _, out _, out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void TryFindSurfaceHit_EntryOnSecondSegment_WalksSegmentsInOrder()
        {
            _points.Add(new Vector3(0f, 0f, 0f));
            _points.Add(new Vector3(10f, 0f, 0f));
            _points.Add(new Vector3(10f, 10f, 0f));

            var found = TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(10f, 6f, 0f), 2f, out var hitPoint, out _, out _);

            Assert.IsTrue(found);
            // Travelling +Y up the second segment, entry sits on the circle's lower surface.
            Assert.AreEqual(10f, hitPoint.x, 0.0001f);
            Assert.AreEqual(4f, hitPoint.y, 0.0001f);
        }

        [Test]
        public void TryFindSurfaceHit_SegmentStartsInsideCircle_HitsAtSegmentStart()
        {
            _points.Add(new Vector3(5f, 0f, 0f));
            _points.Add(new Vector3(20f, 0f, 0f));

            var found = TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(5f, 1f, 0f), 3f, out var hitPoint, out _, out _);

            Assert.IsTrue(found);
            Assert.AreEqual(new Vector3(5f, 0f, 0f), hitPoint);
        }

        [Test]
        public void TryFindSurfaceHit_DegenerateSegment_Skipped()
        {
            _points.Add(new Vector3(5f, 5f, 0f));
            _points.Add(new Vector3(5f, 5f, 0f));

            var found = TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(5f, 5f, 0f), 2f, out _, out _, out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void TryFindSurfaceHit_LineThroughCenter_CentralityIsOne()
        {
            _points.Add(new Vector3(0f, 0f, 0f));
            _points.Add(new Vector3(10f, 0f, 0f));

            TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(5f, 0f, 0f), 2f, out _, out var centrality, out _);

            Assert.AreEqual(1f, centrality, 0.0001f);
        }

        [Test]
        public void TryFindSurfaceHit_OffsetChord_CentralityScalesWithFootDistance()
        {
            _points.Add(new Vector3(0f, 3f, 0f));
            _points.Add(new Vector3(20f, 3f, 0f));

            TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(10f, 0f, 0f), 5f, out _, out var centrality, out _);

            // Foot distance 3 on radius 5 → 1 - 3/5.
            Assert.AreEqual(0.4f, centrality, 0.0001f);
        }

        [Test]
        public void TryFindSurfaceHit_NearTangentGraze_CentralityApproachesZero()
        {
            _points.Add(new Vector3(0f, 4.999f, 0f));
            _points.Add(new Vector3(20f, 4.999f, 0f));

            var found = TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(10f, 0f, 0f), 5f, out _, out var centrality, out _);

            Assert.IsTrue(found);
            Assert.Less(centrality, 0.001f);
        }

        [Test]
        public void TryFindSurfaceHit_ReportsHitSegmentTravelDirection()
        {
            _points.Add(new Vector3(0f, 0f, 0f));
            _points.Add(new Vector3(10f, 0f, 0f));
            _points.Add(new Vector3(10f, 10f, 0f));

            // Head-on hit on the first (+X) segment — direction is that segment's exact travel dir.
            TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(5f, 0f, 0f), 2f, out _, out _, out var straightDir);
            Assert.AreEqual(Vector2.right, straightDir);

            // Hit on the second (+Y) segment — direction must come from that segment, not the first.
            TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(10f, 6f, 0f), 2f, out _, out _, out var turnedDir);
            Assert.AreEqual(Vector2.up, turnedDir);
        }

        [Test]
        public void TryFindSurfaceHit_PrefersHighestCentrality_OverFirstHit()
        {
            // Simulates a wall bounce: segment 0 goes right, segment 1 goes up. A target near the
            // bounce point with a generous radius is grazed by segment 0 but struck dead-centre by
            // segment 1 — the dead-centre hit (highest centrality) should win.
            _points.Add(new Vector3(0f, 0f, 0f));
            _points.Add(new Vector3(10f, 0f, 0f));
            _points.Add(new Vector3(10f, 10f, 0f));

            // Centre at (10, 3): segment 0 grazes it (foot at x=10, foot distance=3, barely inside
            // radius 3.1), while segment 1 hits almost dead-on (foot at y=3, foot distance=0).
            var found = TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(10f, 3f, 0f), 3.1f, out _, out _, out var direction);

            Assert.IsTrue(found);
            // Should report segment 1's direction (up), not segment 0's direction (right).
            Assert.AreEqual(0f, direction.x, 0.01f);
            Assert.AreEqual(1f, direction.y, 0.01f);
        }

        [Test]
        public void TryFindSurfaceHit_EqualCentrality_PrefersFirstSegment()
        {
            // Two perpendicular segments both pass through the target's centre (centrality = 1.0).
            // The algorithm uses strict > so the first segment wins ties — this guards against an
            // accidental >= refactor flipping reported direction near symmetric bounce points.
            _points.Add(new Vector3(0f, 5f, 0f));
            _points.Add(new Vector3(10f, 5f, 0f));
            _points.Add(new Vector3(10f, 15f, 0f));

            // Centre at (5, 5): seg 0 travels +X through centre (centrality 1),
            // Centre also at (10, 10) for seg 1? No — need BOTH to pass through the SAME centre.
            // Place centre at (10, 5): seg 0's foot is at (10,5), foot distance = 0 → centrality 1.
            // seg 1's foot is at (10,5), foot distance = 0 → centrality 1.
            var found = TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(10f, 5f, 0f), 2f, out _, out var centrality, out var direction);

            Assert.IsTrue(found);
            Assert.AreEqual(1f, centrality, 0.0001f);
            // First segment (travelling +X) should win the tie.
            Assert.AreEqual(1f, direction.x, 0.01f);
            Assert.AreEqual(0f, direction.y, 0.01f);
        }

    }
}
