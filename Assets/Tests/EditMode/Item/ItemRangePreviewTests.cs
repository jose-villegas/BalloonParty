using BalloonParty.Configuration.Items;
using BalloonParty.Item.Preview;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    /// <summary>
    ///     Covers the figures whose geometry is self-contained. Lightning's needs a live
    ///     <c>SlotGrid</c>-backed board and is left to a board-level test.
    /// </summary>
    [TestFixture]
    public class ItemRangePreviewTests
    {
        private ItemPreviewShape _shape;

        [SetUp]
        public void SetUp()
        {
            _shape = new ItemPreviewShape();
        }

        // The four cast arms share two corridors, so the figure is two rectangles, not four.
        [Test]
        public void Laser_DrawsTwoClosedRectangles()
        {
            var preview = new LaserRangePreview(BuildItemConfig());

            var context = BuildContext(Vector2.zero, Vector2.up, 0f);
            preview.BuildShape(in context, _shape);

            Assert.AreEqual(2, _shape.Strokes.Count);
            foreach (var stroke in _shape.Strokes)
            {
                Assert.AreEqual(4, stroke.Count, "a rectangle is four corners");
                Assert.IsTrue(stroke.Closed);
            }
        }

        // Half-length is RaycastDistance and half-width CircleCastRadius, so the unrotated corridor spans
        // ±distance along one axis and ±radius across it.
        [Test]
        public void Laser_UnrotatedRectangleSpansCastDistanceAndRadius()
        {
            var preview = new LaserRangePreview(BuildItemConfig(castDistance: 4f, castRadius: 0.5f));

            var context = BuildContext(Vector2.zero, Vector2.up, 0f);
            preview.BuildShape(in context, _shape);

            var maxX = float.MinValue;
            var maxY = float.MinValue;
            var first = _shape.Strokes[0];
            for (var i = first.Start; i < first.Start + first.Count; i++)
            {
                maxX = Mathf.Max(maxX, Mathf.Abs(_shape.Points[i].x));
                maxY = Mathf.Max(maxY, Mathf.Abs(_shape.Points[i].y));
            }

            Assert.AreEqual(4f, maxX, 0.001f, "extends the cast distance along its axis");
            Assert.AreEqual(0.5f, maxY, 0.001f, "and the cast radius across it");
        }

        // The cross is cast along the icon's rotation — drawn axis-aligned while it spins, the telegraph
        // would point at balloons the beam misses.
        [Test]
        public void Laser_RotatesTheCrossWithTheItemSpin()
        {
            var preview = new LaserRangePreview(BuildItemConfig(castDistance: 4f, castRadius: 0.5f));

            var context = BuildContext(Vector2.zero, Vector2.up, 90f);
            preview.BuildShape(in context, _shape);

            var maxX = float.MinValue;
            var first = _shape.Strokes[0];
            for (var i = first.Start; i < first.Start + first.Count; i++)
            {
                maxX = Mathf.Max(maxX, Mathf.Abs(_shape.Points[i].x));
            }

            Assert.AreEqual(0.5f, maxX, 0.001f, "at 90 degrees the long axis is now vertical");
        }

        [Test]
        public void Paint_DrawsTheClosedSpreadTriangle()
        {
            var preview = new PaintRangePreview(BuildItemConfig());

            var context = BuildContext(Vector2.zero, Vector2.up, 0f);
            preview.BuildShape(in context, _shape);

            Assert.AreEqual(1, _shape.Strokes.Count);
            Assert.AreEqual(3, _shape.Strokes[0].Count);
            Assert.IsTrue(_shape.Strokes[0].Closed);
        }

        // The triangle points along the shot: aiming up puts its base above the apex.
        [Test]
        public void Paint_OrientsTheTriangleAlongTheAim()
        {
            var preview = new PaintRangePreview(BuildItemConfig());

            var context = BuildContext(Vector2.zero, Vector2.up, 0f);
            preview.BuildShape(in context, _shape);

            var apex = _shape.Points[0];
            var left = _shape.Points[1];
            var right = _shape.Points[2];

            Assert.Greater(left.y, apex.y, "the base sits ahead of the apex along the aim");
            Assert.AreEqual(left.y, right.y, 0.001f, "an isosceles base is square to the axis");
        }

        [Test]
        public void Shield_MarksTheAimTipWithTwoCrossedArms()
        {
            var preview = new ShieldRangePreview();
            var trace = new[] { Vector3.zero, new Vector3(1f, 2f, 0f) };

            var context = new ItemPreviewContext(Vector2.zero, Vector2Int.zero, Vector2.up, trace, null, 0f);
            preview.BuildShape(in context, _shape);

            Assert.AreEqual(2, _shape.Strokes.Count, "a plus is two arms");

            // Both arms are centred on the trace's LAST point, not the host.
            var centerX = (_shape.Points[0].x + _shape.Points[1].x) * 0.5f;
            var centerY = (_shape.Points[2].y + _shape.Points[3].y) * 0.5f;
            Assert.AreEqual(1f, centerX, 0.001f);
            Assert.AreEqual(2f, centerY, 0.001f);
        }

        [Test]
        public void Shield_WithNoTrace_DrawsNothing()
        {
            var preview = new ShieldRangePreview();

            var context = new ItemPreviewContext(Vector2.zero, Vector2Int.zero, Vector2.up, null, null, 0f);
            preview.BuildShape(in context, _shape);

            Assert.AreEqual(0, _shape.Strokes.Count);
        }

        [Test]
        public void Bomb_DrawsOneClosedCircleAtTheBlastRadius()
        {
            var preview = new BombRangePreview(BuildItemConfig(bombRadius: 1.5f));

            var context = BuildContext(new Vector2(2f, 1f), Vector2.up, 0f);
            preview.BuildShape(in context, _shape);

            Assert.AreEqual(1, _shape.Strokes.Count);
            Assert.IsTrue(_shape.Strokes[0].Closed);

            foreach (var point in _shape.Points)
            {
                var offset = new Vector2(point.x - 2f, point.y - 1f);
                Assert.AreEqual(1.5f, offset.magnitude, 0.001f, "every point sits on the blast radius");
            }
        }

        private static ItemPreviewContext BuildContext(Vector2 origin, Vector2 aim, float spinDegrees)
        {
            var trace = new[] { (Vector3)origin, (Vector3)(origin + (aim * 3f)) };
            return new ItemPreviewContext(origin, Vector2Int.zero, aim, trace, "Red", spinDegrees);
        }

        // ItemSettings' tuning fields are all [SerializeField]-private with no setters, so the values come
        // in the way Unity itself would deserialize them. FromJsonOverwrite merges, leaving every field
        // the json omits (Paint's spread, the curves) at its authored default.
        private static IItemConfiguration BuildItemConfig(
            float bombRadius = 1.25f, float castDistance = 20f, float castRadius = 0.065f)
        {
            var settings = new ItemSettings();
            JsonUtility.FromJsonOverwrite(
                "{\"_bomb\":{\"_bombRadius\":" + bombRadius + "}," +
                "\"_laser\":{\"_laserRaycastDistance\":" + castDistance +
                ",\"_laserCircleCastRadius\":" + castRadius + "}}",
                settings);

            var config = Substitute.For<IItemConfiguration>();
            foreach (ItemType type in System.Enum.GetValues(typeof(ItemType)))
            {
                config[type].Returns(settings);
            }

            return config;
        }
    }
}
