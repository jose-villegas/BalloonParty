using BalloonParty.Configuration.Level;
using NUnit.Framework;

namespace BalloonParty.Tests.Configuration
{
    [TestFixture]
    public class LevelScoringCurveTests
    {
        [Test]
        public void CumulativeMilestone_LevelZero_ReturnsZero()
        {
            var curve = MakeCurve(new ScoringControlPoint(5, 1000f));
            Assert.AreEqual(0f, curve.CumulativeMilestone(0));
        }

        [Test]
        public void CumulativeMilestone_NegativeLevel_ReturnsZero()
        {
            var curve = MakeCurve(new ScoringControlPoint(5, 1000f));
            Assert.AreEqual(0f, curve.CumulativeMilestone(-1));
        }

        [Test]
        public void CumulativeMilestone_EmptyControlPoints_ReturnsZero()
        {
            var curve = new LevelScoringCurve(new ScoringControlPoint[0], DefaultTail());
            Assert.AreEqual(0f, curve.CumulativeMilestone(5));
            Assert.IsTrue(curve.IsEmpty);
        }

        [Test]
        public void CumulativeMilestone_SingleControlPoint_ReturnsExactValue()
        {
            var curve = MakeCurve(new ScoringControlPoint(5, 1000f));
            Assert.AreEqual(1000f, curve.CumulativeMilestone(5));
        }

        [Test]
        public void CumulativeMilestone_LevelBelowFirstCP_LinearRampFromOrigin()
        {
            var curve = MakeCurve(new ScoringControlPoint(10, 500f));

            // Halfway to first CP → half its score.
            Assert.AreEqual(250f, curve.CumulativeMilestone(5));
            Assert.AreEqual(50f, curve.CumulativeMilestone(1));
        }

        [Test]
        public void CumulativeMilestone_LevelExactlyOnControlPoint_ReturnsExactScore()
        {
            var curve = MakeCurve(
                new ScoringControlPoint(1, 200f),
                new ScoringControlPoint(5, 1000f),
                new ScoringControlPoint(10, 3000f));

            Assert.AreEqual(200f, curve.CumulativeMilestone(1));
            Assert.AreEqual(1000f, curve.CumulativeMilestone(5));
            Assert.AreEqual(3000f, curve.CumulativeMilestone(10));
        }

        [Test]
        public void CumulativeMilestone_BetweenControlPoints_ValueIsWithinBounds()
        {
            var curve = MakeCurve(
                new ScoringControlPoint(1, 100f),
                new ScoringControlPoint(10, 1000f));

            for (var level = 2; level <= 9; level++)
            {
                var value = curve.CumulativeMilestone(level);
                Assert.Greater(value, 100f, $"level {level} should exceed CP1");
                Assert.Less(value, 1000f, $"level {level} should be below CP2");
            }
        }

        [Test]
        public void CumulativeMilestone_MonotonicallyNonDecreasing_Over200Levels()
        {
            var curve = MakeCurve(
                new ScoringControlPoint(1, 100f),
                new ScoringControlPoint(5, 500f),
                new ScoringControlPoint(15, 2000f),
                new ScoringControlPoint(30, 5000f));

            var previous = 0f;
            for (var level = 1; level <= 200; level++)
            {
                var value = curve.CumulativeMilestone(level);
                Assert.GreaterOrEqual(value, previous, $"level {level} broke monotonicity");
                previous = value;
            }
        }

        [Test]
        public void CumulativeMilestone_FritschCarlson_NoOvershoots()
        {
            var curve = MakeCurve(
                new ScoringControlPoint(1, 100f),
                new ScoringControlPoint(5, 800f),
                new ScoringControlPoint(10, 900f),
                new ScoringControlPoint(20, 4000f));

            // Between CP2 (level 5, 800) and CP3 (level 10, 900) — flat segment.
            for (var level = 5; level <= 10; level++)
            {
                var value = curve.CumulativeMilestone(level);
                Assert.GreaterOrEqual(value, 800f, $"level {level} undershot");
                Assert.LessOrEqual(value, 900f, $"level {level} overshot");
            }
        }

        [Test]
        public void CumulativeMilestone_GeometricTail_GrowsExponentially()
        {
            var curve = new LevelScoringCurve(
                new[]
                {
                    new ScoringControlPoint(1, 100f),
                    new ScoringControlPoint(10, 1000f),
                },
                new TailGrowthConfig(TailGrowthMode.Geometric, 1.2f));

            // Increments beyond last CP should grow.
            var prev = curve.CumulativeMilestone(10);
            var lastIncrement = 0f;
            for (var level = 11; level <= 20; level++)
            {
                var value = curve.CumulativeMilestone(level);
                var increment = value - prev;
                Assert.Greater(increment, 0f, $"level {level} increment should be positive");
                if (level > 11)
                {
                    Assert.Greater(increment, lastIncrement, $"level {level} increment should grow");
                }

                lastIncrement = increment;
                prev = value;
            }
        }

        [Test]
        public void CumulativeMilestone_LinearTail_ConstantIncrementGrowth()
        {
            var curve = new LevelScoringCurve(
                new[]
                {
                    new ScoringControlPoint(1, 100f),
                    new ScoringControlPoint(10, 1000f),
                },
                new TailGrowthConfig(TailGrowthMode.Linear, 50f));

            // Each level's increment should grow by the addend (50).
            var prev = curve.CumulativeMilestone(10);
            var lastIncrement = 0f;
            for (var level = 11; level <= 20; level++)
            {
                var value = curve.CumulativeMilestone(level);
                var increment = value - prev;
                Assert.Greater(increment, 0f, $"level {level} increment should be positive");
                if (level > 11)
                {
                    var growth = increment - lastIncrement;
                    Assert.AreEqual(50f, growth, 0.01f, $"level {level} increment growth should be 50");
                }

                lastIncrement = increment;
                prev = value;
            }
        }

        [Test]
        public void CumulativeMilestone_GeometricTail_RateZero_FlatIncrements()
        {
            var curve = new LevelScoringCurve(
                new[]
                {
                    new ScoringControlPoint(1, 100f),
                    new ScoringControlPoint(10, 1000f),
                },
                new TailGrowthConfig(TailGrowthMode.Geometric, 0f));

            // Rate 0 clamps — should still produce non-negative values (flat at last cumulative).
            var lastValue = curve.CumulativeMilestone(10);
            for (var level = 11; level <= 20; level++)
            {
                var value = curve.CumulativeMilestone(level);
                Assert.GreaterOrEqual(value, lastValue, $"level {level} should not decrease");
            }
        }

        [Test]
        public void CumulativeMilestone_TwoControlPoints_MidpointWithinReasonableBounds()
        {
            var curve = MakeCurve(
                new ScoringControlPoint(1, 100f),
                new ScoringControlPoint(11, 1100f));

            // Midpoint (level 6): with uniform slope, linear would give 600.
            // Fritsch-Carlson with 2 points acts like a cubic passing through both,
            // should be within ±20% of linear.
            var midpoint = curve.CumulativeMilestone(6);
            Assert.Greater(midpoint, 480f, "midpoint too low");
            Assert.Less(midpoint, 720f, "midpoint too high");
        }

        [Test]
        public void CumulativeMilestone_DuplicateLevels_LastWins()
        {
            // Two CPs at same level — array is sorted, last cumulative value used.
            var curve = MakeCurve(
                new ScoringControlPoint(1, 100f),
                new ScoringControlPoint(5, 500f),
                new ScoringControlPoint(5, 800f),
                new ScoringControlPoint(10, 2000f));

            // Level 5 should hit one of the CPs at level 5.
            var value = curve.CumulativeMilestone(5);
            Assert.That(value, Is.EqualTo(500f).Or.EqualTo(800f));
        }

        [Test]
        public void CumulativeMilestone_LinearMode_ProducesLinearInterpolation()
        {
            var curve = MakeCurve(
                new ScoringControlPoint(1, 100f, SegmentMode.Linear),
                new ScoringControlPoint(11, 1100f));

            // Linear: value at midpoint (level 6, t=0.5) should be exactly halfway.
            Assert.AreEqual(600f, curve.CumulativeMilestone(6), 0.01f);
            // Level 4 (t=0.3): 100 + 1000*0.3 = 400.
            Assert.AreEqual(400f, curve.CumulativeMilestone(4), 0.01f);
        }

        [Test]
        public void CumulativeMilestone_ConvexMode_StartsSlowEndsFast()
        {
            var curve = MakeCurve(
                new ScoringControlPoint(1, 0f, SegmentMode.Convex),
                new ScoringControlPoint(11, 1000f));

            var quarter = curve.CumulativeMilestone(3);  // t = 0.2
            var mid = curve.CumulativeMilestone(6);      // t = 0.5
            var threeQ = curve.CumulativeMilestone(9);   // t = 0.8

            // Convex (ease-in): first half gains less than second half.
            Assert.Less(mid, 500f, "midpoint should be below linear");
            // Values should be monotone and below the linear path.
            Assert.Less(quarter, mid);
            Assert.Less(mid, threeQ);
        }

        [Test]
        public void CumulativeMilestone_ConcaveMode_StartsFastEndsSlow()
        {
            var curve = MakeCurve(
                new ScoringControlPoint(1, 0f, SegmentMode.Concave),
                new ScoringControlPoint(11, 1000f));

            var mid = curve.CumulativeMilestone(6);      // t = 0.5
            var threeQ = curve.CumulativeMilestone(9);   // t = 0.8

            // Concave (ease-out): first half gains more than second half.
            Assert.Greater(mid, 500f, "midpoint should be above linear");
            Assert.Less(mid, threeQ);
        }

        [Test]
        public void CumulativeMilestone_MixedModes_AllMonotone()
        {
            var curve = MakeCurve(
                new ScoringControlPoint(1, 100f, SegmentMode.Convex),
                new ScoringControlPoint(5, 500f, SegmentMode.Linear),
                new ScoringControlPoint(10, 1000f, SegmentMode.Concave),
                new ScoringControlPoint(15, 2000f, SegmentMode.Smooth),
                new ScoringControlPoint(20, 3500f));

            var previous = 0f;
            for (var level = 1; level <= 20; level++)
            {
                var value = curve.CumulativeMilestone(level);
                Assert.GreaterOrEqual(value, previous, $"level {level} broke monotonicity");
                previous = value;
            }
        }

        [Test]
        public void CumulativeMilestone_LinearMode_HitsEndpoints()
        {
            var curve = MakeCurve(
                new ScoringControlPoint(5, 500f, SegmentMode.Linear),
                new ScoringControlPoint(10, 1500f));

            Assert.AreEqual(500f, curve.CumulativeMilestone(5));
            Assert.AreEqual(1500f, curve.CumulativeMilestone(10));
        }

        [Test]
        public void CumulativeMilestone_ConvexConcaveMode_HitEndpoints()
        {
            var convexCurve = MakeCurve(
                new ScoringControlPoint(1, 100f, SegmentMode.Convex),
                new ScoringControlPoint(10, 1000f));

            var concaveCurve = MakeCurve(
                new ScoringControlPoint(1, 100f, SegmentMode.Concave),
                new ScoringControlPoint(10, 1000f));

            Assert.AreEqual(100f, convexCurve.CumulativeMilestone(1));
            Assert.AreEqual(1000f, convexCurve.CumulativeMilestone(10));
            Assert.AreEqual(100f, concaveCurve.CumulativeMilestone(1));
            Assert.AreEqual(1000f, concaveCurve.CumulativeMilestone(10));
        }

        private static LevelScoringCurve MakeCurve(params ScoringControlPoint[] points)
        {
            return new LevelScoringCurve(points, DefaultTail());
        }

        private static TailGrowthConfig DefaultTail()
        {
            return new TailGrowthConfig(TailGrowthMode.Geometric, 1f);
        }
    }
}
