using System.Collections.Generic;
using BalloonParty.Item.Preview;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    [TestFixture]
    public class ItemPreviewEntryTests
    {
        private ItemPreviewShape _shape;

        [SetUp]
        public void SetUp()
        {
            _shape = new ItemPreviewShape();
        }

        // The single most important case: a trace through a closed circle must resolve to whichever
        // side it reaches FIRST, not merely whichever segment happens to sit earlier in the point list.
        [Test]
        public void TryFindEntryOffset_ClosedCircle_ReturnsNearSideCrossing()
        {
            _shape.AddCircle(Vector2.zero, 5f, 32);
            var arcTable = BuildArcTable(_shape, out _);

            // A horizontal trace offset off-centre (y = 0.3) so it can't land exactly on a polygon vertex,
            // travelling +X from well outside the circle on the -X side.
            var trace = new List<Vector3>
            {
                new(-20f, 0.3f, 0f),
                new(20f, 0.3f, 0f),
            };

            var found = ItemPreviewEntry.TryFindEntryOffset(_shape, 0, arcTable, trace, out var offset, out _);

            Assert.IsTrue(found);

            var stroke = _shape.Strokes[0];
            var samplePoint = SamplePointAtOffset(stroke, _shape.Points, arcTable, offset);

            // Entering from -X, the near-side crossing sits on the circle's left half; the far-side exit
            // (which a naive "first stroke segment found" walk could return instead) sits on the right.
            Assert.Less(samplePoint.x, 0f, "the near crossing must be on the -X side the trace enters from");
            Assert.AreEqual(5f, samplePoint.magnitude, 0.05f, "the sample should still sit on the circle");
        }

        // Round numbers throughout so the expected arc offset is obvious by hand: a 10-unit horizontal
        // stroke crossed by a vertical trace at x = 4 should read back exactly 4.
        [Test]
        public void TryFindEntryOffset_OpenSegmentCrossedPartWay_ReturnsExpectedArcOffset()
        {
            _shape.AddSegment(new Vector2(0f, 0f), new Vector2(10f, 0f));
            var arcTable = BuildArcTable(_shape, out _);

            var trace = new List<Vector3>
            {
                new(4f, -5f, 0f),
                new(4f, 5f, 0f),
            };

            var found = ItemPreviewEntry.TryFindEntryOffset(_shape, 0, arcTable, trace, out var offset, out var traceOffset);

            Assert.IsTrue(found);
            Assert.AreEqual(4f, offset, 0.0001f);
            // The trace runs straight from (4,-5) to (4,5); the crossing at (4,0) sits 5 units along it.
            Assert.AreEqual(5f, traceOffset, 0.0001f);
        }

        // The case the straight-line approach lerp got wrong: a trace that bends before it reaches the
        // stroke. The trace-side offset must be measured along the whole bent polyline (first leg in
        // full, plus the partial distance into the second leg to the crossing), not as if the trace ran
        // straight from its first point to the crossing.
        [Test]
        public void TryFindEntryOffset_BentMultiSegmentTrace_ReturnsTraceArcOffset()
        {
            _shape.AddSegment(new Vector2(0f, 0f), new Vector2(10f, 0f));
            var arcTable = BuildArcTable(_shape, out _);

            // Leg 1: (-10,10) -> (4,10), length 14, stays well above the stroke.
            // Leg 2: (4,10) -> (4,-10), length 20, crosses the stroke at (4,0) — 10 units into this leg.
            var trace = new List<Vector3>
            {
                new(-10f, 10f, 0f),
                new(4f, 10f, 0f),
                new(4f, -10f, 0f),
            };

            var found = ItemPreviewEntry.TryFindEntryOffset(_shape, 0, arcTable, trace, out var offset, out var traceOffset);

            Assert.IsTrue(found);
            Assert.AreEqual(4f, offset, 0.0001f, "stroke-side offset is unaffected by the trace's own shape");
            Assert.AreEqual(24f, traceOffset, 0.0001f, "14 (first leg) + 10 (partway into the second) = 24");
        }

        [Test]
        public void TryFindEntryOffset_TraceMissesStroke_ReturnsFalse()
        {
            _shape.AddSegment(new Vector2(0f, 0f), new Vector2(10f, 0f));
            var arcTable = BuildArcTable(_shape, out _);

            // Passes well above the segment — never comes near y = 0.
            var trace = new List<Vector3>
            {
                new(4f, 5f, 0f),
                new(4f, 6f, 0f),
            };

            var found = ItemPreviewEntry.TryFindEntryOffset(_shape, 0, arcTable, trace, out var offset, out var traceOffset);

            Assert.IsFalse(found);
            Assert.AreEqual(0f, offset);
            Assert.AreEqual(0f, traceOffset);
        }

        // A trace landing exactly on a shared vertex sees it from both adjacent segments (one at its own
        // t = 1, the other at t = 0) — this must resolve to one answer at that vertex's own arc length,
        // not a wrong offset from whichever segment's near-degenerate math happens to win.
        [Test]
        public void TryFindEntryOffset_TraceThroughVertex_ReturnsVertexArcLength()
        {
            _shape.BeginStroke();
            _shape.AddPoint(new Vector3(0f, 0f, 0f));
            _shape.AddPoint(new Vector3(5f, 0f, 0f));
            _shape.AddPoint(new Vector3(10f, 3f, 0f));
            _shape.EndStroke();

            var arcTable = BuildArcTable(_shape, out _);

            // Vertical line through (5, 0), the shared vertex between the two segments.
            var trace = new List<Vector3>
            {
                new(5f, -5f, 0f),
                new(5f, 5f, 0f),
            };

            var found = ItemPreviewEntry.TryFindEntryOffset(_shape, 0, arcTable, trace, out var offset, out var traceOffset);

            Assert.IsTrue(found);
            // The first segment alone spans (0,0) -> (5,0), a length-5 leg.
            Assert.AreEqual(5f, offset, 0.001f);
            // The trace runs straight from (5,-5) to (5,5); the vertex crossing at (5,0) sits 5 units in.
            Assert.AreEqual(5f, traceOffset, 0.001f);
        }

        // Straight polyline: the nearest point to an off-line target is the foot of the perpendicular,
        // and its arc-length offset is just the distance along the line to that foot.
        [Test]
        public void TryFindNearestPointOnPolyline_StraightPolyline_ReturnsPerpendicularFootAndOffset()
        {
            var polyline = new List<Vector3>
            {
                new(0f, 0f, 0f),
                new(10f, 0f, 0f),
            };

            var found = ItemPreviewEntry.TryFindNearestPointOnPolyline(
                polyline, new Vector3(4f, 3f, 0f), out var point, out var offset);

            Assert.IsTrue(found);
            Assert.AreEqual(new Vector3(4f, 0f, 0f), point);
            Assert.AreEqual(4f, offset, 0.0001f);
        }

        // The case that matters: a deflected aim bends the trace before the point nearest the host. The
        // offset must accumulate the full first leg plus the partial distance into the second, not treat
        // the polyline as if it ran straight from its own first point.
        [Test]
        public void TryFindNearestPointOnPolyline_BentMultiSegmentPolyline_ReturnsOffsetAlongBend()
        {
            // Leg 1: (0,0) -> (10,0), length 10.
            // Leg 2: (10,0) -> (10,10), length 10 — the target (10,4) projects onto this leg, 4 units in.
            var polyline = new List<Vector3>
            {
                new(0f, 0f, 0f),
                new(10f, 0f, 0f),
                new(10f, 10f, 0f),
            };

            var found = ItemPreviewEntry.TryFindNearestPointOnPolyline(
                polyline, new Vector3(10f, 4f, 0f), out var point, out var offset);

            Assert.IsTrue(found);
            Assert.AreEqual(new Vector3(10f, 4f, 0f), point);
            Assert.AreEqual(14f, offset, 0.0001f, "10 (first leg) + 4 (partway into the second) = 14");
        }

        [Test]
        public void TryFindNearestPointOnPolyline_FewerThanTwoPoints_ReturnsFalse()
        {
            var polyline = new List<Vector3> { new(4f, -5f, 0f) };

            var found = ItemPreviewEntry.TryFindNearestPointOnPolyline(
                polyline, new Vector3(0f, 0f, 0f), out var point, out var offset);

            Assert.IsFalse(found);
            Assert.AreEqual(default(Vector3), point);
            Assert.AreEqual(0f, offset);
        }

        [Test]
        public void TryFindEntryOffset_FewerThanTwoTracePoints_ReturnsFalse()
        {
            _shape.AddSegment(new Vector2(0f, 0f), new Vector2(10f, 0f));
            var arcTable = BuildArcTable(_shape, out _);

            var trace = new List<Vector3> { new(4f, -5f, 0f) };

            var found = ItemPreviewEntry.TryFindEntryOffset(_shape, 0, arcTable, trace, out var offset, out var traceOffset);

            Assert.IsFalse(found);
            Assert.AreEqual(0f, offset);
            Assert.AreEqual(0f, traceOffset);
        }

        [Test]
        public void TryFindEntryOffset_StrokeIndexOutOfRange_ReturnsFalse()
        {
            _shape.AddSegment(new Vector2(0f, 0f), new Vector2(10f, 0f));
            var arcTable = BuildArcTable(_shape, out _);

            var trace = new List<Vector3>
            {
                new(4f, -5f, 0f),
                new(4f, 5f, 0f),
            };

            var found = ItemPreviewEntry.TryFindEntryOffset(_shape, 1, arcTable, trace, out var offset, out var traceOffset);

            Assert.IsFalse(found);
            Assert.AreEqual(0f, offset);
            Assert.AreEqual(0f, traceOffset);
        }

        // Mirrors ItemPreviewTicker.BuildArcTable exactly, so the table this test feeds the helper matches
        // what production code actually hands it — a divergence here would test a fiction, not the contract.
        private static List<float> BuildArcTable(ItemPreviewShape shape, out List<float> strokeLengths)
        {
            var arcTable = new List<float>();
            var lengths = new List<float>();
            var points = shape.Points;

            for (var i = 0; i < points.Count; i++)
            {
                arcTable.Add(0f);
            }

            for (var s = 0; s < shape.Strokes.Count; s++)
            {
                var stroke = shape.Strokes[s];
                var total = 0f;
                arcTable[stroke.Start] = 0f;

                for (var i = 1; i < stroke.Count; i++)
                {
                    total += Vector3.Distance(points[stroke.Start + i - 1], points[stroke.Start + i]);
                    arcTable[stroke.Start + i] = total;
                }

                if (stroke.Closed)
                {
                    total += Vector3.Distance(points[stroke.Start + stroke.Count - 1], points[stroke.Start]);
                }

                lengths.Add(total);
            }

            strokeLengths = lengths;
            return arcTable;
        }

        // Direct (non-wrapping) arc-offset sampler, used only to verify a returned offset actually lands
        // where expected — deliberately independent of ItemPreviewTicker's own SampleStroke.
        private static Vector3 SamplePointAtOffset(
            ItemPreviewStroke stroke, IReadOnlyList<Vector3> points, IReadOnlyList<float> arcTable, float offset)
        {
            var lastIndex = stroke.Start + stroke.Count - 1;
            if (offset >= arcTable[lastIndex])
            {
                if (!stroke.Closed)
                {
                    return points[lastIndex];
                }

                var wrapLength = Vector3.Distance(points[lastIndex], points[stroke.Start]);
                var legT = wrapLength <= 1e-5f ? 0f : (offset - arcTable[lastIndex]) / wrapLength;
                return Vector3.Lerp(points[lastIndex], points[stroke.Start], legT);
            }

            for (var i = stroke.Start + 1; i <= lastIndex; i++)
            {
                if (offset > arcTable[i])
                {
                    continue;
                }

                var segmentLength = arcTable[i] - arcTable[i - 1];
                var segmentT = segmentLength <= 1e-5f ? 0f : (offset - arcTable[i - 1]) / segmentLength;
                return Vector3.Lerp(points[i - 1], points[i], segmentT);
            }

            return points[lastIndex];
        }
    }
}
