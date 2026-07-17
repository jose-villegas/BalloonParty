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

            var found = TraceHitGeometry.TryFindSurfaceHit(_points, Vector3.one, 1f, out _, out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void TryFindSurfaceHit_NullList_ReturnsFalse()
        {
            var found = TraceHitGeometry.TryFindSurfaceHit(null, Vector3.zero, 1f, out _, out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void TryFindSurfaceHit_LineThroughCenter_HitsSurfaceOnEntrySide()
        {
            _points.Add(new Vector3(0f, 0f, 0f));
            _points.Add(new Vector3(10f, 0f, 0f));

            var found = TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(5f, 0f, 0f), 2f, out var hitPoint, out _);

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
                _points, new Vector3(10f, 0f, 0f), 5f, out var hitPoint, out _);

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
                _points, new Vector3(10f, 0f, 0f), 5f, out _, out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void TryFindSurfaceHit_CircleEntirelyBehindSegment_ReturnsFalse()
        {
            _points.Add(new Vector3(10f, 0f, 0f));
            _points.Add(new Vector3(20f, 0f, 0f));

            var found = TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(5f, 0f, 0f), 2f, out _, out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void TryFindSurfaceHit_CircleBeyondSegmentEnd_ReturnsFalse()
        {
            _points.Add(new Vector3(0f, 0f, 0f));
            _points.Add(new Vector3(10f, 0f, 0f));

            var found = TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(15f, 0f, 0f), 2f, out _, out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void TryFindSurfaceHit_EntryOnSecondSegment_WalksSegmentsInOrder()
        {
            _points.Add(new Vector3(0f, 0f, 0f));
            _points.Add(new Vector3(10f, 0f, 0f));
            _points.Add(new Vector3(10f, 10f, 0f));

            var found = TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(10f, 6f, 0f), 2f, out var hitPoint, out _);

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
                _points, new Vector3(5f, 1f, 0f), 3f, out var hitPoint, out _);

            Assert.IsTrue(found);
            Assert.AreEqual(new Vector3(5f, 0f, 0f), hitPoint);
        }

        [Test]
        public void TryFindSurfaceHit_DegenerateSegment_Skipped()
        {
            _points.Add(new Vector3(5f, 5f, 0f));
            _points.Add(new Vector3(5f, 5f, 0f));

            var found = TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(5f, 5f, 0f), 2f, out _, out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void TryFindSurfaceHit_LineThroughCenter_CentralityIsOne()
        {
            _points.Add(new Vector3(0f, 0f, 0f));
            _points.Add(new Vector3(10f, 0f, 0f));

            TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(5f, 0f, 0f), 2f, out _, out var centrality);

            Assert.AreEqual(1f, centrality, 0.0001f);
        }

        [Test]
        public void TryFindSurfaceHit_OffsetChord_CentralityScalesWithFootDistance()
        {
            _points.Add(new Vector3(0f, 3f, 0f));
            _points.Add(new Vector3(20f, 3f, 0f));

            TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(10f, 0f, 0f), 5f, out _, out var centrality);

            // Foot distance 3 on radius 5 → 1 - 3/5.
            Assert.AreEqual(0.4f, centrality, 0.0001f);
        }

        [Test]
        public void TryFindSurfaceHit_NearTangentGraze_CentralityApproachesZero()
        {
            _points.Add(new Vector3(0f, 4.999f, 0f));
            _points.Add(new Vector3(20f, 4.999f, 0f));

            var found = TraceHitGeometry.TryFindSurfaceHit(
                _points, new Vector3(10f, 0f, 0f), 5f, out _, out var centrality);

            Assert.IsTrue(found);
            Assert.Less(centrality, 0.001f);
        }

    }
}
