using BalloonParty.Item.Preview;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    [TestFixture]
    public class ItemPreviewShapeTests
    {
        private ItemPreviewShape _shape;

        [SetUp]
        public void SetUp()
        {
            _shape = new ItemPreviewShape();
        }

        [Test]
        public void AddSegment_RecordsOneOpenStrokeOfTwoPoints()
        {
            _shape.AddSegment(Vector2.zero, new Vector2(0f, 3f));

            Assert.AreEqual(1, _shape.Strokes.Count);
            Assert.AreEqual(0, _shape.Strokes[0].Start);
            Assert.AreEqual(2, _shape.Strokes[0].Count);
            Assert.IsFalse(_shape.Strokes[0].Closed);
            Assert.AreEqual(3f, _shape.Points[1].y, 0.001f);
        }

        [Test]
        public void AddCircle_RecordsOneClosedStrokeWithoutRepeatingTheFirstPoint()
        {
            _shape.AddCircle(Vector2.zero, 2f, 8);

            Assert.AreEqual(1, _shape.Strokes.Count);
            Assert.IsTrue(_shape.Strokes[0].Closed, "the wrap leg is implied by Closed, not a duplicate point");
            Assert.AreEqual(8, _shape.Strokes[0].Count, "eight segments means eight points, not nine");

            foreach (var point in _shape.Points)
            {
                Assert.AreEqual(2f, new Vector2(point.x, point.y).magnitude, 0.001f, "every point sits on the radius");
            }
        }

        // Two figures in one shape must not overlap: the ticker indexes points by Start + i, so a wrong
        // Start would have a pen tracing another stroke's points.
        [Test]
        public void MultipleStrokes_EachIndexesItsOwnPointRange()
        {
            _shape.AddSegment(Vector2.left, Vector2.right);
            _shape.AddSegment(Vector2.down, Vector2.up);

            Assert.AreEqual(2, _shape.Strokes.Count);
            Assert.AreEqual(0, _shape.Strokes[0].Start);
            Assert.AreEqual(2, _shape.Strokes[1].Start);
            Assert.AreEqual(4, _shape.Points.Count);
        }

        // A one-point stroke has no length for a pen to travel and would divide by a zero arc-length in
        // the ticker — it must never reach the stroke list.
        [Test]
        public void EndStroke_DiscardsAStrokeWithFewerThanTwoPoints()
        {
            _shape.BeginStroke();
            _shape.AddPoint(Vector3.zero);
            _shape.EndStroke();

            Assert.AreEqual(0, _shape.Strokes.Count);
            Assert.AreEqual(0, _shape.Points.Count, "the orphaned point is rolled back, not left dangling");
        }

        [Test]
        public void AddCircle_WithDegenerateInput_RecordsNothing()
        {
            _shape.AddCircle(Vector2.zero, 0f, 16);
            _shape.AddCircle(Vector2.zero, 1f, 2);

            Assert.AreEqual(0, _shape.Strokes.Count);
        }

        [Test]
        public void Clear_ResetsPointsAndStrokes()
        {
            _shape.AddCircle(Vector2.zero, 1f, 8);
            _shape.Clear();

            Assert.AreEqual(0, _shape.Strokes.Count);
            Assert.AreEqual(0, _shape.Points.Count);
        }

        // Reuse across rebuilds is the whole point of the buffer — a second build must not inherit the first.
        [Test]
        public void Rebuild_AfterClear_ProducesTheSameShapeAsAFreshBuffer()
        {
            _shape.AddSegment(Vector2.zero, Vector2.up);
            _shape.Clear();
            _shape.AddCircle(Vector2.zero, 1f, 6);

            var fresh = new ItemPreviewShape();
            fresh.AddCircle(Vector2.zero, 1f, 6);

            Assert.AreEqual(fresh.Points.Count, _shape.Points.Count);
            Assert.AreEqual(fresh.Strokes.Count, _shape.Strokes.Count);
            Assert.AreEqual(fresh.Strokes[0].Start, _shape.Strokes[0].Start);
        }
    }
}
