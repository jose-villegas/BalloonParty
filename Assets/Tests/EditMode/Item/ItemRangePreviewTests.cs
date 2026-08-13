using BalloonParty.Configuration;
using BalloonParty.Configuration.Items;
using BalloonParty.Item.Preview;
using BalloonParty.Prediction;
using BalloonParty.Shared;
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

        // The four cast arms share two corridors, so the figure is two open axis lines, not four —
        // and not closed, since a beam has no interior to fill.
        [Test]
        public void Laser_DrawsTwoOpenLines()
        {
            var preview = new LaserRangePreview(BuildItemConfig(), BuildFlightConfig());

            var context = BuildContext(Vector2.zero, Vector2.up, 0f);
            preview.BuildShape(in context, _shape);

            Assert.AreEqual(2, _shape.Strokes.Count);
            foreach (var stroke in _shape.Strokes)
            {
                Assert.AreEqual(2, stroke.Count, "a line is two endpoints");
                Assert.IsFalse(stroke.Closed);
            }
        }

        // With the walls far enough away not to clip, each axis spans the full ±RaycastDistance either
        // side of the host — CircleCastRadius no longer contributes a width, since the rectangle it used
        // to draw was 0.13 units wide on a board 4.5 units across. Unrotated, the first stroke is the
        // right axis (x) and the second is the up axis (y).
        [Test]
        public void Laser_UnclippedLinesSpanCastDistance()
        {
            var preview = new LaserRangePreview(
                BuildItemConfig(castDistance: 4f), BuildFlightConfig(top: 100f, right: 100f, bottom: -100f, left: -100f));

            var context = BuildContext(Vector2.zero, Vector2.up, 0f);
            preview.BuildShape(in context, _shape);

            var rightAxis = _shape.Strokes[0];
            Assert.AreEqual(-4f, _shape.Points[rightAxis.Start].x, 0.001f, "the minus arm reaches the cast distance");
            Assert.AreEqual(4f, _shape.Points[rightAxis.Start + 1].x, 0.001f, "the plus arm reaches the cast distance");
            Assert.AreEqual(0f, _shape.Points[rightAxis.Start].y, 0.001f, "the right axis carries no y component");

            var upAxis = _shape.Strokes[1];
            Assert.AreEqual(-4f, _shape.Points[upAxis.Start].y, 0.001f, "the minus arm reaches the cast distance");
            Assert.AreEqual(4f, _shape.Points[upAxis.Start + 1].y, 0.001f, "the plus arm reaches the cast distance");
        }

        // The cross is cast along the icon's rotation — drawn axis-aligned while it spins, the telegraph
        // would point at balloons the beam misses. At 90 degrees, the axis that ran along x at spin 0
        // now runs along y.
        [Test]
        public void Laser_RotatesTheAxesWithTheItemSpin()
        {
            var preview = new LaserRangePreview(
                BuildItemConfig(castDistance: 4f), BuildFlightConfig(top: 100f, right: 100f, bottom: -100f, left: -100f));

            var context = BuildContext(Vector2.zero, Vector2.up, 90f);
            preview.BuildShape(in context, _shape);

            var first = _shape.Strokes[0];
            var maxX = Mathf.Max(
                Mathf.Abs(_shape.Points[first.Start].x), Mathf.Abs(_shape.Points[first.Start + 1].x));
            var maxY = Mathf.Max(
                Mathf.Abs(_shape.Points[first.Start].y), Mathf.Abs(_shape.Points[first.Start + 1].y));

            Assert.AreEqual(0f, maxX, 0.001f, "the axis that ran along x at spin 0 now carries no x component");
            Assert.AreEqual(4f, maxY, 0.001f, "and reaches the same cast distance, now along y");
        }

        // A wall closer than RaycastDistance clips that half-arm to the wall crossing rather than the
        // full cast distance — the arm near the wall is short, the opposite arm is unaffected. The top
        // wall only bounds the vertical (up) axis, the second stroke when unrotated.
        [Test]
        public void Laser_ClipsAnArmAtTheNearerWall()
        {
            var preview = new LaserRangePreview(
                BuildItemConfig(castDistance: 20f), BuildFlightConfig(top: 3f, right: 100f, bottom: -100f, left: -100f));

            var context = BuildContext(Vector2.zero, Vector2.up, 0f);
            preview.BuildShape(in context, _shape);

            var upAxis = _shape.Strokes[1];
            var a = _shape.Points[upAxis.Start];
            var b = _shape.Points[upAxis.Start + 1];

            Assert.AreEqual(3f, b.y, 0.001f, "the plus arm stops at the top wall rather than the cast distance");
            Assert.AreEqual(-20f, a.y, 0.001f, "the minus arm is unaffected and keeps the full cast distance");
        }

        [Test]
        public void Paint_DrawsTheClosedSpreadTriangle()
        {
            var preview = new PaintRangePreview(BuildItemConfig(), BuildPreviewConfig(stubLength: 0f));

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
            var preview = new PaintRangePreview(BuildItemConfig(), BuildPreviewConfig(stubLength: 0f));

            var context = BuildContext(Vector2.zero, Vector2.up, 0f);
            preview.BuildShape(in context, _shape);

            var apex = _shape.Points[0];
            var left = _shape.Points[1];
            var right = _shape.Points[2];

            Assert.Greater(left.y, apex.y, "the base sits ahead of the apex along the aim");
            Assert.AreEqual(left.y, right.y, 0.001f, "an isosceles base is square to the axis");
        }

        // A display-only multiplier on top of the true spread — proves the scale actually reaches the
        // drawn triangle rather than only existing on the config, AND that it scales about the figure's
        // own axis midpoint rather than its apex. With the default SpreadLength of 3 and SpreadOffset of
        // 0, aiming up: a 0.5 scale must halve the length (apex to base) while leaving the midpoint where
        // it was unscaled — the property that was broken when Scale shrank the triangle toward the host.
        [Test]
        public void Paint_ScaleShrinksTheTriangleAboutItsMidpoint()
        {
            var preview = new PaintRangePreview(
                BuildItemConfig(), BuildPreviewConfig(stubLength: 0f, paintScale: 0.5f));

            var context = BuildContext(Vector2.zero, Vector2.up, 0f);
            preview.BuildShape(in context, _shape);

            var apex = _shape.Points[0];
            var left = _shape.Points[1];
            var midpoint = (apex.y + left.y) * 0.5f;

            Assert.AreEqual(0.75f, apex.y, 0.001f, "the apex moved in by half the spread length lost");
            Assert.AreEqual(2.25f, left.y, 0.001f, "the base moved in by half the spread length lost");
            Assert.AreEqual(1.5f, midpoint, 0.001f, "the axis midpoint stayed put, scaled or not");
        }

        // Shield shows the bounce it buys, so the stub must start at the aim line's end and leave along
        // the REFLECTED heading — straight up into the top wall comes back straight down.
        [Test]
        public void Shield_DrawsTheReflectedStubOffTheWallTheAimEndsOn()
        {
            var preview = new ShieldRangePreview(BuildPreviewConfig(stubLength: 2f));
            var trace = new[] { new Vector3(0f, 1f, 0f), new Vector3(0f, 5f, 0f) };
            var end = new PredictionTraceEnd(-1, PredictionTraceEndKind.Wall, Vector2.down);

            var context = BuildShieldContext(trace, end);
            preview.BuildShape(in context, _shape);

            Assert.AreEqual(1, _shape.Strokes.Count);
            Assert.AreEqual(2, _shape.Strokes[0].Count, "a stub is one segment");
            Assert.AreEqual(0f, _shape.Points[0].x, 0.001f, "starts at the aim line's end");
            Assert.AreEqual(5f, _shape.Points[0].y, 0.001f);
            Assert.AreEqual(3f, _shape.Points[1].y, 0.001f, "heads back down for the configured length of 2");
        }

        // Walls only, on purpose: a shield is spent on a wall and nothing else (ProjectileMotionResolver
        // decrements ShieldsRemaining on a wall reflection, its Deflect never does), so a bounce off a
        // balloon costs the shot nothing and Shield has no consequence to advertise there.
        [Test]
        public void Shield_WhenTheAimEndsOnADeflector_DrawsNothing()
        {
            var preview = new ShieldRangePreview(BuildPreviewConfig(stubLength: 2f));
            var trace = new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 1.5f, 0f) };
            var end = new PredictionTraceEnd(-1, PredictionTraceEndKind.Deflector, Vector2.down);

            var context = BuildShieldContext(trace, end);
            preview.BuildShape(in context, _shape);

            Assert.AreEqual(0, _shape.Strokes.Count, "no shield is spent deflecting off a balloon");
        }

        // The stub's reach is config, not a constant — a tuning change has to move it.
        [Test]
        public void Shield_StubLengthComesFromConfig()
        {
            var preview = new ShieldRangePreview(BuildPreviewConfig(stubLength: 0.5f));
            var trace = new[] { new Vector3(0f, 1f, 0f), new Vector3(0f, 5f, 0f) };
            var end = new PredictionTraceEnd(-1, PredictionTraceEndKind.Wall, Vector2.down);

            var context = BuildShieldContext(trace, end);
            preview.BuildShape(in context, _shape);

            Assert.AreEqual(4.5f, _shape.Points[1].y, 0.001f);
        }

        // A side wall reflects the horizontal component only — the stub keeps climbing while it turns back.
        [Test]
        public void Shield_ReflectsOffASideWallAcrossTheHorizontal()
        {
            var preview = new ShieldRangePreview(BuildPreviewConfig(stubLength: 2f));
            var trace = new[] { new Vector3(1f, 0f, 0f), new Vector3(3f, 2f, 0f) };
            var end = new PredictionTraceEnd(-1, PredictionTraceEndKind.Wall, Vector2.left);

            var context = BuildShieldContext(trace, end);
            preview.BuildShape(in context, _shape);

            Assert.AreEqual(1, _shape.Strokes.Count);
            Assert.Less(_shape.Points[1].x, _shape.Points[0].x, "turned back off the right wall");
            Assert.Greater(_shape.Points[1].y, _shape.Points[0].y, "but keeps its upward travel");
        }

        // The aim line can run out of segments in open air; there is no contact, so there is no bounce to
        // promise, and inventing one would be a lie.
        [Test]
        public void Shield_WhenTheAimEndsInOpenAir_DrawsNothing()
        {
            var preview = new ShieldRangePreview(BuildPreviewConfig(stubLength: 2f));
            var trace = new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 1f, 0f) };

            var context = BuildShieldContext(trace, PredictionTraceEnd.OpenAir(-1));
            preview.BuildShape(in context, _shape);

            Assert.AreEqual(0, _shape.Strokes.Count);
        }

        [Test]
        public void Shield_WithNoTrace_DrawsNothing()
        {
            var preview = new ShieldRangePreview(BuildPreviewConfig(stubLength: 2f));
            var end = new PredictionTraceEnd(-1, PredictionTraceEndKind.Wall, Vector2.down);

            var context = BuildShieldContext(null, end);
            preview.BuildShape(in context, _shape);

            Assert.AreEqual(0, _shape.Strokes.Count);
        }

        [Test]
        public void Bomb_DrawsOneClosedCircleAtTheBlastRadius()
        {
            var preview = new BombRangePreview(BuildItemConfig(bombRadius: 1.5f), BuildPreviewConfig(stubLength: 0f));

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

        // A display-only nudge on top of the true radius — proves the offset actually reaches the drawn
        // circle rather than only existing on the config.
        [Test]
        public void Bomb_RadiusOffsetMovesTheDrawnRadius()
        {
            var preview = new BombRangePreview(
                BuildItemConfig(bombRadius: 1.5f), BuildPreviewConfig(stubLength: 0f, radiusOffset: 0.5f));

            var context = BuildContext(new Vector2(2f, 1f), Vector2.up, 0f);
            preview.BuildShape(in context, _shape);

            foreach (var point in _shape.Points)
            {
                var offset = new Vector2(point.x - 2f, point.y - 1f);
                Assert.AreEqual(2.0f, offset.magnitude, 0.001f, "the offset shifts every point outward");
            }
        }

        private static IItemPreviewConfig BuildPreviewConfig(
            float stubLength, float radiusOffset = 0f, float paintScale = 1f)
        {
            var shield = Substitute.For<IShieldPreviewSettings>();
            shield.StubLength.Returns(stubLength);

            var bomb = Substitute.For<IBombPreviewSettings>();
            bomb.RadiusOffset.Returns(radiusOffset);

            var paint = Substitute.For<IPaintPreviewSettings>();
            paint.Scale.Returns(paintScale);

            var config = Substitute.For<IItemPreviewConfig>();
            config.Shield.Returns(shield);
            config.Bomb.Returns(bomb);
            config.Paint.Returns(paint);
            return config;
        }

        private static ItemPreviewContext BuildShieldContext(Vector3[] trace, PredictionTraceEnd end)
        {
            return new ItemPreviewContext(
                Vector2.zero, Vector2Int.zero, Vector2.up, trace, null, 0f, end);
        }

        private static ItemPreviewContext BuildContext(Vector2 origin, Vector2 aim, float spinDegrees)
        {
            var trace = new[] { (Vector3)origin, (Vector3)(origin + (aim * 3f)) };
            return new ItemPreviewContext(origin, Vector2Int.zero, aim, trace, "Red", spinDegrees);
        }

        // ItemSettings' tuning fields are all [SerializeField]-private with no setters, so the values come
        // in the way Unity itself would deserialize them. FromJsonOverwrite merges, leaving every field
        // the json omits (Paint's spread, the curves) at its authored default.
        private static IItemConfiguration BuildItemConfig(float bombRadius = 1.25f, float castDistance = 20f)
        {
            var settings = new ItemSettings();
            JsonUtility.FromJsonOverwrite(
                "{\"_bomb\":{\"_bombRadius\":" + bombRadius + "}," +
                "\"_laser\":{\"_laserRaycastDistance\":" + castDistance + "}}",
                settings);

            var config = Substitute.For<IItemConfiguration>();
            foreach (ItemType type in System.Enum.GetValues(typeof(ItemType)))
            {
                config[type].Returns(settings);
            }

            return config;
        }

        // Defaults mirror the real board (IProjectileFlightConfig.LimitsClockwise): Top 3.625 / Right
        // 2.25 / Bottom -4.5 / Left -2.25. Individual tests override whichever wall needs to be near or
        // far to make the clipping behaviour under test unambiguous.
        private static IProjectileFlightConfig BuildFlightConfig(
            float top = 3.625f, float right = 2.25f, float bottom = -4.5f, float left = -2.25f)
        {
            var flightConfig = Substitute.For<IProjectileFlightConfig>();
            flightConfig.LimitsClockwise.Returns(new Vector4(top, right, bottom, left));
            return flightConfig;
        }
    }
}
