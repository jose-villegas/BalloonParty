using BalloonParty.Shared;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Shared
{
    /// <summary>
    /// Tests <see cref="WallLimits.TryFindCrossing"/> — the ray-wall intersection used by
    /// the shield morph FSM to predict wall distance. Pure math with conditional branching
    /// (horizontal vs vertical wall, corner-hit summing, parallel/zero-direction guards).
    /// </summary>
    [TestFixture]
    public class WallLimitsTryFindCrossingTests
    {
        // A simple box: top=5, right=3, bottom=-5, left=-3
        private readonly WallLimits _walls = new(new Vector4(5f, 3f, -5f, -3f));

        [Test]
        public void TryFindCrossing_StraightUp_HitsTopWall()
        {
            var hit = _walls.TryFindCrossing(Vector3.zero, Vector3.up, out var crossing, out var normal);

            Assert.IsTrue(hit);
            Assert.AreEqual(0f, crossing.x, 0.001f);
            Assert.AreEqual(5f, crossing.y, 0.001f, "Should cross at the top wall");
            Assert.AreEqual(Vector3.down, normal, "Inward normal from top wall is down");
        }

        [Test]
        public void TryFindCrossing_StraightDown_HitsBottomWall()
        {
            var hit = _walls.TryFindCrossing(Vector3.zero, Vector3.down, out var crossing, out var normal);

            Assert.IsTrue(hit);
            Assert.AreEqual(0f, crossing.x, 0.001f);
            Assert.AreEqual(-5f, crossing.y, 0.001f, "Should cross at the bottom wall");
            Assert.AreEqual(Vector3.up, normal, "Inward normal from bottom wall is up");
        }

        [Test]
        public void TryFindCrossing_StraightRight_HitsRightWall()
        {
            var hit = _walls.TryFindCrossing(Vector3.zero, Vector3.right, out var crossing, out var normal);

            Assert.IsTrue(hit);
            Assert.AreEqual(3f, crossing.x, 0.001f, "Should cross at the right wall");
            Assert.AreEqual(0f, crossing.y, 0.001f);
            Assert.AreEqual(Vector3.left, normal, "Inward normal from right wall is left");
        }

        [Test]
        public void TryFindCrossing_StraightLeft_HitsLeftWall()
        {
            var hit = _walls.TryFindCrossing(Vector3.zero, Vector3.left, out var crossing, out var normal);

            Assert.IsTrue(hit);
            Assert.AreEqual(-3f, crossing.x, 0.001f, "Should cross at the left wall");
            Assert.AreEqual(0f, crossing.y, 0.001f);
            Assert.AreEqual(Vector3.right, normal, "Inward normal from left wall is right");
        }

        [Test]
        public void TryFindCrossing_Diagonal_HitsCloserWall()
        {
            // From origin going (1,1) normalized: right wall at x=3 (t=3), top at y=5 (t=5)
            // Right wall is closer.
            var dir = new Vector3(1f, 1f, 0f).normalized;
            var hit = _walls.TryFindCrossing(Vector3.zero, dir, out var crossing, out _);

            Assert.IsTrue(hit);
            Assert.AreEqual(3f, crossing.x, 0.01f, "Should hit right wall first");
        }

        [Test]
        public void TryFindCrossing_DiagonalHitsTopFirst_WhenCloserVertically()
        {
            // Walls: top=2, right=10. From origin going (1,1), top at t=2, right at t=10.
            var narrowTop = new WallLimits(new Vector4(2f, 10f, -10f, -10f));
            var dir = new Vector3(1f, 1f, 0f).normalized;
            var hit = narrowTop.TryFindCrossing(Vector3.zero, dir, out var crossing, out _);

            Assert.IsTrue(hit);
            Assert.AreEqual(2f, crossing.y, 0.01f, "Should hit top wall first");
        }

        [Test]
        public void TryFindCrossing_CornerHit_SumsBothNormals()
        {
            // Walls form a 1×1 box. From origin going (1,1): right at t=1, top at t=1.
            var square = new WallLimits(new Vector4(1f, 1f, -1f, -1f));
            var dir = new Vector3(1f, 1f, 0f).normalized;
            var hit = square.TryFindCrossing(Vector3.zero, dir, out var crossing, out var normal);

            Assert.IsTrue(hit);
            Assert.AreEqual(1f, crossing.x, 0.01f);
            Assert.AreEqual(1f, crossing.y, 0.01f);

            // Corner: both inward normals summed
            var expected = Vector3.left + Vector3.down;
            Assert.AreEqual(expected.x, normal.x, 0.01f, "Corner normal should sum left");
            Assert.AreEqual(expected.y, normal.y, 0.01f, "Corner normal should sum down");
        }

        [Test]
        public void TryFindCrossing_ZeroDirection_ReturnsFalse()
        {
            var hit = _walls.TryFindCrossing(Vector3.zero, Vector3.zero, out _, out _);

            Assert.IsFalse(hit, "Zero direction should not find any crossing");
        }

        [Test]
        public void TryFindCrossing_ParallelToWall_HitsPerpendicularWallOnly()
        {
            // Pointing purely right from y=2 — parallel to top/bottom, perpendicular to right.
            var hit = _walls.TryFindCrossing(new Vector3(0f, 2f, 0f), Vector3.right,
                out var crossing, out var normal);

            Assert.IsTrue(hit);
            Assert.AreEqual(3f, crossing.x, 0.001f, "Should hit right wall");
            Assert.AreEqual(2f, crossing.y, 0.001f, "Y unchanged — parallel to horizontal walls");
            Assert.AreEqual(Vector3.left, normal);
        }

        [Test]
        public void TryFindCrossing_OffsetPosition_DistanceReflectsProximity()
        {
            // From (2, 0) going right: distance to right wall (x=3) should be 1.
            var hit = _walls.TryFindCrossing(new Vector3(2f, 0f, 0f), Vector3.right,
                out var crossing, out _);

            Assert.IsTrue(hit);
            var distance = Vector3.Distance(new Vector3(2f, 0f, 0f), crossing);
            Assert.AreEqual(1f, distance, 0.001f, "Distance should reflect proximity to wall");
        }

        [Test]
        public void TryFindCrossing_PositionOnWall_ReturnsZeroDistance()
        {
            // Standing on the right wall, pointing right: t = 0 (the wall is at x=3).
            var pos = new Vector3(3f, 0f, 0f);
            var hit = _walls.TryFindCrossing(pos, Vector3.right, out var crossing, out _);

            Assert.IsTrue(hit);
            var distance = Vector3.Distance(pos, crossing);
            Assert.AreEqual(0f, distance, 0.001f, "Already on the wall — distance is zero");
        }
    }
}
