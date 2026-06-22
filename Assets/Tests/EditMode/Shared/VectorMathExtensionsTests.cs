using System.Collections.Generic;
using BalloonParty.Shared.Extensions;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Shared
{
    [TestFixture]
    public class VectorMathExtensionsTests
    {
        [Test]
        public void Centroid_ReturnsArithmeticMean()
        {
            var points = new List<Vector3>
            {
                new(0f, 0f, 0f),
                new(4f, 0f, 0f),
                new(0f, 6f, 0f)
            };

            var centroid = points.Centroid(3);

            Assert.AreEqual(4f / 3f, centroid.x, 0.001f);
            Assert.AreEqual(2f, centroid.y, 0.001f);
            Assert.AreEqual(0f, centroid.z, 0.001f);
        }

        [Test]
        public void BoundingRadius_ReturnsMaxDistance()
        {
            var center = new Vector3(1f, 1f, 0f);
            var points = new List<Vector3>
            {
                new(1f, 1f, 0f),
                new(4f, 1f, 0f),
                new(1f, 6f, 0f)
            };

            var radius = points.BoundingRadius(3, center);

            Assert.AreEqual(5f, radius, 0.001f);
        }

        [Test]
        public void BoundingRadius_AllSamePoint_ReturnsZero()
        {
            var center = new Vector3(2f, 3f, 0f);
            var points = new List<Vector3>
            {
                new(2f, 3f, 0f),
                new(2f, 3f, 0f)
            };

            var radius = points.BoundingRadius(2, center);

            Assert.AreEqual(0f, radius, 0.001f);
        }

        [Test]
        public void WithinRadius_InsideAndOutside()
        {
            var a = new Vector2(0f, 0f);

            Assert.IsTrue(a.WithinRadius(new Vector2(3f, 4f), 5f));
            Assert.IsFalse(a.WithinRadius(new Vector2(3f, 4f), 4.99f));
        }

        [Test]
        public void DirectionFromAngle_CardinalAngles()
        {
            var east = VectorMathExtensions.DirectionFromAngle(0f);
            Assert.AreEqual(1f, east.x, 0.001f);
            Assert.AreEqual(0f, east.y, 0.001f);

            var north = VectorMathExtensions.DirectionFromAngle(Mathf.PI / 2f);
            Assert.AreEqual(0f, north.x, 0.001f);
            Assert.AreEqual(1f, north.y, 0.001f);

            var west = VectorMathExtensions.DirectionFromAngle(Mathf.PI);
            Assert.AreEqual(-1f, west.x, 0.001f);
            Assert.AreEqual(0f, west.y, 0.001f);
        }

        [Test]
        public void DirectionFromAngle_IsUnitLength()
        {
            for (var a = 0f; a < Mathf.PI * 2f; a += 0.37f)
            {
                Assert.AreEqual(1f, VectorMathExtensions.DirectionFromAngle(a).magnitude, 0.001f);
            }
        }
    }
}
