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

        // The loop's own defining property: whatever vertices land in between, the first and last must be
        // the identical point — not merely close within tolerance, since the last one re-uses the first
        // outright rather than recomputing it from angle + 2π (see AppendApproachLoop's own remarks on
        // why: trig round-off must not leave a gap for the standing no-discontinuity rule to catch).
        [Test]
        public void AppendApproachLoop_ClosesExactlyOnItsStartPoint()
        {
            var buffer = new List<Vector3>();

            ItemPreviewEntry.AppendApproachLoop(
                buffer, Vector3.zero, new Vector3(1f, 0f, 0f), Vector2.up, 2f, 12);

            Assert.AreEqual(buffer[0], buffer[^1]);
        }

        // The returned length must be the discrete polygon's own perimeter — the same consecutive-point
        // summation ItemPreviewTicker's own arc tables use everywhere else — not the ideal 2*pi*r a
        // caller could otherwise assume. A regular n-gon inscribed in radius r has a closed-form
        // perimeter (2*n*r*sin(pi/n)); comparing against that formula, rather than re-summing the
        // buffer's own points, is what keeps this an independent check.
        [Test]
        public void AppendApproachLoop_ReturnsRegularPolygonPerimeter()
        {
            var buffer = new List<Vector3>();
            const float radius = 3f;
            const int segments = 16;

            var length = ItemPreviewEntry.AppendApproachLoop(
                buffer, Vector3.zero, new Vector3(radius, 0f, 0f), Vector2.up, radius, segments);

            var expected = 2f * segments * radius * Mathf.Sin(Mathf.PI / segments);
            Assert.AreEqual(expected, length, 0.001f);
            Assert.AreEqual(segments + 1, buffer.Count, "n segments plus the closing duplicate of the first point");
        }

        // Every appended vertex, including the closing duplicate, must sit on the authored radius — a
        // regression here (an off-by-one angle step, say) would otherwise still "close" trivially without
        // actually being a circle.
        [Test]
        public void AppendApproachLoop_EveryPointSitsOnTheRadius()
        {
            var buffer = new List<Vector3>();
            var center = new Vector3(5f, -2f, 0f);
            const float radius = 1.5f;

            ItemPreviewEntry.AppendApproachLoop(
                buffer, center, center + new Vector3(0f, radius, 0f), Vector2.right, radius, 20);

            foreach (var point in buffer)
            {
                Assert.AreEqual(radius, Vector3.Distance(center, point), 0.001f);
            }
        }

        // Winding is counter-clockwise (increasing angle), matching ItemPreviewShape.AddCircle: starting
        // due +X of the centre, the very next vertex must have a positive Y — a clockwise loop would swing
        // negative instead.
        [Test]
        public void AppendApproachLoop_WindsCounterClockwise()
        {
            var buffer = new List<Vector3>();

            ItemPreviewEntry.AppendApproachLoop(
                buffer, Vector3.zero, new Vector3(1f, 0f, 0f), Vector2.up, 1f, 12);

            Assert.Greater(buffer[1].y, 0f);
        }

        // towardPoint coinciding with the centre (Laser, Paint, Lightning's first arc — the entry sits
        // right on the host) leaves no leg direction to sight the loop's start off; it must fall back to
        // the caller-supplied direction rather than produce a NaN from normalizing a zero vector.
        [Test]
        public void AppendApproachLoop_TowardPointOnCentre_FallsBackToSuppliedDirection()
        {
            var buffer = new List<Vector3>();

            ItemPreviewEntry.AppendApproachLoop(
                buffer, Vector3.zero, Vector3.zero, new Vector3(0f, 1f, 0f), 2f, 12);

            Assert.AreEqual(new Vector3(0f, 2f, 0f), buffer[0], "starts on the +Y ray, the supplied fallback direction");
            Assert.IsFalse(float.IsNaN(buffer[0].x) || float.IsNaN(buffer[0].y), "must never NaN from a zero-length direction");
        }

        // A non-positive radius (missing config, or a genuinely zero balloon radius) must degrade to no
        // loop at all — nothing appended, zero length — rather than throwing or drawing a degenerate point.
        [TestCase(0f)]
        [TestCase(-1f)]
        public void AppendApproachLoop_NonPositiveRadius_AppendsNothing(float radius)
        {
            var buffer = new List<Vector3>();

            var length = ItemPreviewEntry.AppendApproachLoop(
                buffer, Vector3.zero, new Vector3(1f, 0f, 0f), Vector2.up, radius, 12);

            Assert.AreEqual(0f, length);
            Assert.AreEqual(0, buffer.Count);
        }

        // Straight trace through the centre (perpendicular offset d = 0), so the two crossings sit exactly
        // one radius either side of the host's own projection — the simplest case, and the one a naive
        // "always one radius along the trace" implementation would also get right, so it mainly pins the
        // forward/backward split itself.
        [Test]
        public void TryFindCircleCrossing_StraightTraceThroughCentre_ForwardAndBackwardSplitAroundHost()
        {
            var trace = new List<Vector3> { new(-10f, 0f, 0f), new(10f, 0f, 0f) };
            var arcTable = BuildTraceArcTable(trace);

            // The host's own projection sits at (0,0,0), 10 units into the trace.
            var foundForward = ItemPreviewEntry.TryFindCircleCrossing(
                trace, arcTable, Vector3.zero, 5f, 10f, true, out var forwardPoint, out var forwardOffset);
            var foundBackward = ItemPreviewEntry.TryFindCircleCrossing(
                trace, arcTable, Vector3.zero, 5f, 10f, false, out var backwardPoint, out var backwardOffset);

            Assert.IsTrue(foundForward);
            Assert.AreEqual(new Vector3(5f, 0f, 0f), forwardPoint, "forward exit sits one radius ahead");
            Assert.AreEqual(15f, forwardOffset, 0.0001f);

            Assert.IsTrue(foundBackward);
            Assert.AreEqual(new Vector3(-5f, 0f, 0f), backwardPoint, "backward exit sits one radius behind");
            Assert.AreEqual(5f, backwardOffset, 0.0001f);
        }

        // The case a "one radius along the trace" shortcut gets wrong: the host projects off-centre (d = 3
        // on a radius-5 circle), so the true half-chord is sqrt(5^2 - 3^2) = 4, not 5.
        [Test]
        public void TryFindCircleCrossing_OffCentreProjection_UsesHalfChordNotRadius()
        {
            var trace = new List<Vector3> { new(-20f, 0f, 0f), new(20f, 0f, 0f) };
            var arcTable = BuildTraceArcTable(trace);
            var center = new Vector3(0f, 3f, 0f);

            // The host's own projection sits at (0,0,0), 20 units into the trace.
            var found = ItemPreviewEntry.TryFindCircleCrossing(
                trace, arcTable, center, 5f, 20f, true, out var point, out var offset);

            Assert.IsTrue(found);
            Assert.AreEqual(0f, Vector3.Distance(new Vector3(4f, 0f, 0f), point), 0.0001f);
            Assert.AreEqual(24f, offset, 0.0001f, "20 (to the projection) + 4 (the true half-chord)");
        }

        // The case the whole helper exists for: the projection sits exactly on the segment nearest it, but
        // the actual exit crossing (walking further in the requested direction) only happens on a LATER
        // segment once the trace has already turned a corner — so the walk must cross a segment boundary,
        // not just solve the one segment the projection landed on.
        [Test]
        public void TryFindCircleCrossing_BentTrace_CrossesOnALaterSegment()
        {
            // Leg 1: (-10,10) -> (4,10), length 14.
            // Leg 2: (4,10) -> (4,-10), length 20 — the host projects onto this leg, at (4,-9).
            // Leg 3: (4,-10) -> (14,-10), length 10 — the forward exit actually lands here.
            var trace = new List<Vector3>
            {
                new(-10f, 10f, 0f),
                new(4f, 10f, 0f),
                new(4f, -10f, 0f),
                new(14f, -10f, 0f),
            };
            var arcTable = BuildTraceArcTable(trace);
            var center = new Vector3(4f, -9f, 0f);

            // Host projection (4,-9) sits 14 + 19 = 33 units in.
            var foundForward = ItemPreviewEntry.TryFindCircleCrossing(
                trace, arcTable, center, 3f, 33f, true, out var forwardPoint, out var forwardOffset);

            Assert.IsTrue(foundForward);
            Assert.AreEqual(-10f, forwardPoint.y, 0.001f, "the forward exit has already turned onto leg 3");
            Assert.Greater(forwardPoint.x, 4f, "past the corner, heading toward (14,-10)");
            Assert.AreEqual(3f, Vector3.Distance(center, forwardPoint), 0.001f, "still exactly on the circle");
            Assert.Greater(forwardOffset, 34f, "past the corner's own arc offset (14 + 20 = 34)");

            // The backward exit, by contrast, never leaves leg 2 — a plain sanity check that the two
            // directions aren't accidentally sharing one answer.
            var foundBackward = ItemPreviewEntry.TryFindCircleCrossing(
                trace, arcTable, center, 3f, 33f, false, out var backwardPoint, out var backwardOffset);

            Assert.IsTrue(foundBackward);
            Assert.AreEqual(0f, Vector3.Distance(new Vector3(4f, -6f, 0f), backwardPoint), 0.001f);
            Assert.AreEqual(30f, backwardOffset, 0.001f);
        }

        // The host centre sits nowhere near the trace at all (perpendicular distance far exceeds the
        // radius) — there is no crossing to find in either direction, and the walk must say so rather than
        // return whatever segment happened to look closest.
        [Test]
        public void TryFindCircleCrossing_HostFarOffTrace_ReturnsFalse()
        {
            var trace = new List<Vector3> { new(0f, 0f, 0f), new(10f, 0f, 0f) };
            var arcTable = BuildTraceArcTable(trace);
            var center = new Vector3(5f, 10f, 0f);

            var found = ItemPreviewEntry.TryFindCircleCrossing(
                trace, arcTable, center, 2f, 5f, true, out var point, out var offset);

            Assert.IsFalse(found);
            Assert.AreEqual(default(Vector3), point);
            Assert.AreEqual(0f, offset);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void TryFindCircleCrossing_NonPositiveRadius_ReturnsFalse(float radius)
        {
            var trace = new List<Vector3> { new(-10f, 0f, 0f), new(10f, 0f, 0f) };
            var arcTable = BuildTraceArcTable(trace);

            var found = ItemPreviewEntry.TryFindCircleCrossing(
                trace, arcTable, Vector3.zero, radius, 10f, true, out var point, out var offset);

            Assert.IsFalse(found);
            Assert.AreEqual(default(Vector3), point);
            Assert.AreEqual(0f, offset);
        }

        [Test]
        public void TryFindCircleCrossing_FewerThanTwoTracePoints_ReturnsFalse()
        {
            var trace = new List<Vector3> { new(4f, -5f, 0f) };
            var arcTable = BuildTraceArcTable(trace);

            var found = ItemPreviewEntry.TryFindCircleCrossing(
                trace, arcTable, Vector3.zero, 5f, 0f, true, out var point, out var offset);

            Assert.IsFalse(found);
            Assert.AreEqual(default(Vector3), point);
            Assert.AreEqual(0f, offset);
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

        // Mirrors ItemPreviewTicker.BuildTraceBuffers' own cumulative-arc table for a flat trace polyline
        // (as opposed to BuildArcTable above, which tables a shape's own strokes) — what feeds
        // TryFindCircleCrossing's arcTable parameter in these tests, matching what production code hands it.
        private static List<float> BuildTraceArcTable(IReadOnlyList<Vector3> trace)
        {
            var arcTable = new List<float> { 0f };
            var total = 0f;

            for (var i = 1; i < trace.Count; i++)
            {
                total += Vector3.Distance(trace[i - 1], trace[i]);
                arcTable.Add(total);
            }

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
