using System.Collections.Generic;
using BalloonParty.Shared.Animation;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Shared
{
    [TestFixture]
    public class VectorMathHelperTests
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

            var centroid = VectorMathHelper.Centroid(points, 3);

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

            var radius = VectorMathHelper.BoundingRadius(points, 3, center);

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

            var radius = VectorMathHelper.BoundingRadius(points, 2, center);

            Assert.AreEqual(0f, radius, 0.001f);
        }
    }
}

