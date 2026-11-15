using BalloonParty.Projectile.Clearance;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Projectile
{
    [TestFixture]
    public class ClearanceGeometryTests
    {
        private const float Radius = 2f;
        private const float Softness = 0.5f;
        private const float Band = Radius * (1f + Softness);
        private const float Tolerance = 1e-4f;

        private static readonly Vector2 Center = new Vector2(3f, -1f);

        [Test]
        public void Deflect_PointOutsideBand_Unchanged()
        {
            var point = Center + new Vector2(0f, Band + 1f);

            Assert.AreEqual(point, ClearanceGeometry.Deflect(point, Circle(), Softness));
        }

        [Test]
        public void Deflect_PointExactlyOnBandEdge_Unchanged()
        {
            var point = Center + new Vector2(Band, 0f);

            var result = ClearanceGeometry.Deflect(point, Circle(), Softness);

            Assert.AreEqual(point.x, result.x, Tolerance);
            Assert.AreEqual(point.y, result.y, Tolerance);
        }

        [Test]
        public void Deflect_PointInsideBand_LandsOnSameRayAtFormulaDistance()
        {
            var direction = new Vector2(3f, 4f).normalized;
            var point = Center + direction * 1.5f;
            var k = Band / (Band - Radius);
            var expected = Radius + (Band - Radius) * Mathf.Pow(1.5f / Band, k);

            var result = ClearanceGeometry.Deflect(point, Circle(), Softness);

            Assert.AreEqual(expected, (result - Center).magnitude, Tolerance);
            Assert.AreEqual(direction.x, (result - Center).normalized.x, Tolerance);
            Assert.AreEqual(direction.y, (result - Center).normalized.y, Tolerance);
        }

        [Test]
        public void Deflect_PointInsideCircle_NeverEndsInsideTheCircle()
        {
            for (var r = 0.01f; r < Band; r += 0.05f)
            {
                var result = ClearanceGeometry.Deflect(Center + new Vector2(r, 0f), Circle(), Softness);

                Assert.GreaterOrEqual((result - Center).magnitude, Radius - Tolerance, "r = " + r);
            }
        }

        [Test]
        public void Deflect_IncreasingDistance_NeverLandsNearer()
        {
            var previous = 0f;
            for (var r = 0f; r < Band + 1f; r += 0.02f)
            {
                var result = ClearanceGeometry.Deflect(Center + new Vector2(0f, r), Circle(), Softness);
                var landed = (result - Center).magnitude;

                Assert.GreaterOrEqual(landed, previous - Tolerance, "r = " + r);
                previous = landed;
            }
        }

        [Test]
        public void Deflect_JustInsideBandEdge_IsContinuousWithOutside()
        {
            var inside = ClearanceGeometry.Deflect(Center + new Vector2(Band - 1e-3f, 0f), Circle(), Softness);

            Assert.AreEqual(Band - 1e-3f, (inside - Center).magnitude, 1e-3f);
        }

        [Test]
        public void Deflect_PointOnCentre_GoesToRadiusAlongUp()
        {
            var result = ClearanceGeometry.Deflect(Center, Circle(), Softness);

            Assert.AreEqual(Center.x, result.x, Tolerance);
            Assert.AreEqual(Center.y + Radius, result.y, Tolerance);
        }

        [Test]
        public void Deflect_ZeroRadius_Unchanged()
        {
            var point = Center + new Vector2(0.3f, 0.1f);

            Assert.AreEqual(point, ClearanceGeometry.Deflect(point, new ClearanceCircle(Center, 0f, 1f), Softness));
        }

        [Test]
        public void Deflect_ZeroSoftness_Unchanged()
        {
            var point = Center + new Vector2(0.3f, 0.1f);

            Assert.AreEqual(point, ClearanceGeometry.Deflect(point, Circle(), 0f));
        }

        private static ClearanceCircle Circle()
        {
            return new ClearanceCircle(Center, Radius, 1f);
        }
    }
}
