using System.Collections.Generic;
using BalloonParty.Shared.Animation;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Shared
{
    [TestFixture]
    public class PathHelperTests
    {
        [Test]
        public void CatmullRomPath_StartsAtFirstWaypoint()
        {
            var waypoints = new List<Vector3>
            {
                new(0f, 0f, 0f),
                new(1f, 0f, 0f),
                new(2f, 0f, 0f)
            };

            var path = PathHelper.CatmullRomPath(waypoints, 3);

            AssertVectorsEqual(waypoints[0], path[0]);
        }

        [Test]
        public void CatmullRomPath_EndsAtLastWaypoint()
        {
            var waypoints = new List<Vector3>
            {
                new(0f, 0f, 0f),
                new(1f, 0f, 0f),
                new(2f, 0f, 0f)
            };

            var path = PathHelper.CatmullRomPath(waypoints, 3);

            AssertVectorsEqual(waypoints[2], path[^1]);
        }

        [Test]
        public void CatmullRomPath_SinglePoint_ReturnsSinglePoint()
        {
            var waypoints = new List<Vector3> { new(5f, 3f, 0f) };

            var path = PathHelper.CatmullRomPath(waypoints, 1);

            Assert.AreEqual(1, path.Length);
            AssertVectorsEqual(waypoints[0], path[0]);
        }

        [Test]
        public void CatmullRomPath_TwoPoints_ReturnsCorrectSubdivisionCount()
        {
            var waypoints = new List<Vector3>
            {
                new(0f, 0f, 0f),
                new(1f, 0f, 0f)
            };

            const int subdivisions = 6;
            var path = PathHelper.CatmullRomPath(waypoints, 2, subdivisions);

            // (pointCount - 1) * subdivisions + 1
            Assert.AreEqual(((2 - 1) * subdivisions) + 1, path.Length);
        }

        [Test]
        public void CatmullRomLoop_EndsAtFirstWaypoint()
        {
            var waypoints = new List<Vector3>
            {
                new(0f, 0f, 0f),
                new(1f, 1f, 0f),
                new(2f, 0f, 0f)
            };

            var path = PathHelper.CatmullRomLoop(waypoints, 3);

            AssertVectorsEqual(waypoints[0], path[^1]);
        }

        [Test]
        public void SampleAt_IntegerIndex_ReturnsExactValue()
        {
            var array = new[] { Vector3.zero, Vector3.one, Vector3.up };

            var result = PathHelper.SampleAt(array, 1f);

            AssertVectorsEqual(Vector3.one, result);
        }

        [Test]
        public void SampleAt_FractionalIndex_Interpolates()
        {
            var array = new[] { new Vector3(0f, 0f, 0f), new Vector3(2f, 0f, 0f) };

            var result = PathHelper.SampleAt(array, 0.5f);

            AssertVectorsEqual(new Vector3(1f, 0f, 0f), result);
        }

        [Test]
        public void SampleAt_BeyondBounds_Clamps()
        {
            var array = new[] { Vector3.zero, Vector3.one };

            var result = PathHelper.SampleAt(array, 5f);

            AssertVectorsEqual(Vector3.one, result);
        }

        [Test]
        public void PrefixSum_CorrectCumulativeValues()
        {
            var sizes = new[] { 3, 5, 2 };

            var result = PathHelper.PrefixSum(sizes);

            Assert.AreEqual(new[] { 0, 3, 8, 10 }, result);
        }

        [Test]
        public void PrefixSum_FirstElementIsZero()
        {
            var sizes = new[] { 7 };

            var result = PathHelper.PrefixSum(sizes);

            Assert.AreEqual(0, result[0]);
        }

        [Test]
        public void MidpointDisplacement_PreservesEndpoints()
        {
            var start = new Vector3(0f, 0f, 0f);
            var end = new Vector3(10f, 0f, 0f);
            var buffer = new Vector3[8];

            PathHelper.MidpointDisplacement(start, end, 1f, 0.5f, buffer, 0, 8);

            AssertVectorsEqual(start, buffer[0]);
            AssertVectorsEqual(end, buffer[7]);
        }

        [Test]
        public void MidpointDisplacement_CountTwoOrLess_OnlyEndpoints()
        {
            var start = new Vector3(0f, 0f, 0f);
            var end = new Vector3(5f, 0f, 0f);
            var buffer = new Vector3[2];

            PathHelper.MidpointDisplacement(start, end, 1f, 0.5f, buffer, 0, 2);

            AssertVectorsEqual(start, buffer[0]);
            AssertVectorsEqual(end, buffer[1]);
        }

        private static void AssertVectorsEqual(Vector3 expected, Vector3 actual, float tolerance = 0.001f)
        {
            Assert.AreEqual(expected.x, actual.x, tolerance, $"X mismatch: expected {expected}, got {actual}");
            Assert.AreEqual(expected.y, actual.y, tolerance, $"Y mismatch: expected {expected}, got {actual}");
            Assert.AreEqual(expected.z, actual.z, tolerance, $"Z mismatch: expected {expected}, got {actual}");
        }
    }
}

