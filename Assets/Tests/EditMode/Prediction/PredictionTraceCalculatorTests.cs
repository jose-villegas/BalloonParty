using System.Collections.Generic;
using BalloonParty.Prediction;
using BalloonParty.Shared;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Prediction
{
    [TestFixture]
    public class PredictionTraceCalculatorTests
    {
        private static readonly Vector4 DefaultLimits = new(5f, 3f, -5f, -3f);

        private PredictionTraceCalculator _calculator;
        private IPredictionTraceConfig _config;
        private IProjectileFlightConfig _flightConfig;
        private FakeDeflectorField _deflectors;
        private List<Vector3> _results;

        [SetUp]
        public void SetUp()
        {
            _config = Substitute.For<IPredictionTraceConfig>();
            _config.SegmentLength.Returns(0.5f);
            _config.MaxReflections.Returns(3);
            _config.MaxSegments.Returns(100);

            _flightConfig = Substitute.For<IProjectileFlightConfig>();
            _flightConfig.LimitsClockwise.Returns(DefaultLimits);

            _deflectors = new FakeDeflectorField();
            _calculator = new PredictionTraceCalculator(_config, _flightConfig, _deflectors);
            _results = new List<Vector3>();
        }

        [Test]
        public void Calculate_LeftWallBounce_ReflectsAtLeftLimit()
        {
            var origin = new Vector3(-2.5f, 0f, 0f);
            var direction = new Vector3(-1f, 1f, 0f).normalized;

            _calculator.Calculate(origin, direction, 0f, _results);

            Assert.GreaterOrEqual(_results.Count, 2);
            Assert.AreEqual(DefaultLimits.w, _results[1].x, 0.01f);
        }

        // The budget stops the NEXT turn, not the leg leaving this one — a reflection drawn with
        // nothing coming out of it tells the player where the shot stops being predicted, not where
        // it goes. So one reflection is three points: the start, the bounce, and where the outgoing
        // leg runs out.
        [Test]
        public void Calculate_OneWallReflection_DrawsTheOutgoingLegToTheNextLimit()
        {
            _config.MaxReflections.Returns(1);
            _config.MaxSegments.Returns(200);

            var origin = new Vector3(2.5f, 0f, 0f);
            var direction = new Vector3(1f, 1f, 0f).normalized;

            _calculator.Calculate(origin, direction, 0f, _results);

            Assert.AreEqual(3, _results.Count);
            Assert.AreEqual(DefaultLimits.y, _results[1].x, 0.01f, "y is the right wall");
            Assert.AreEqual(DefaultLimits.x, _results[2].y, 0.01f, "the leg ends on the top wall");
        }

        [Test]
        public void Calculate_MaxSteps_StopsBeforeReachingWall()
        {
            _config.MaxSegments.Returns(3);
            _config.MaxReflections.Returns(10);

            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _results);

            // Origin plus where it got to. It used to be the origin alone, which drew no line at all —
            // invisible before deflections existed, because every upward shot ends on a wall, but a
            // deflection sends the line downward where no wall stops it.
            Assert.AreEqual(2, _results.Count);
            Assert.AreEqual(3f * 0.5f, _results[1].y, 0.01f, "three steps of 0.5 travelled");
        }

        [Test]
        public void Calculate_RightWallBounce_ReflectsAtRightLimit()
        {
            var origin = new Vector3(2.5f, 0f, 0f);
            var direction = new Vector3(1f, 1f, 0f).normalized;

            _calculator.Calculate(origin, direction, 0f, _results);

            Assert.GreaterOrEqual(_results.Count, 2);
            Assert.AreEqual(DefaultLimits.y, _results[1].x, 0.01f);
        }

        [Test]
        public void Calculate_StraightUp_HitsTopWall()
        {
            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _results);

            var lastPoint = _results[_results.Count - 1];
            Assert.AreEqual(DefaultLimits.x, lastPoint.y, 0.01f);
        }

        [Test]
        public void Calculate_TopWallHit_TerminatesBouncing()
        {
            _config.MaxReflections.Returns(10);
            _config.MaxSegments.Returns(200);

            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _results);

            Assert.AreEqual(2, _results.Count);
        }

        [Test]
        public void Calculate_ZigZag_ProducesMultipleBouncePoints()
        {
            _config.MaxReflections.Returns(5);
            _config.MaxSegments.Returns(200);
            _config.SegmentLength.Returns(1f);

            var direction = new Vector3(1f, 0.3f, 0f).normalized;

            _calculator.Calculate(Vector3.zero, direction, 0f, _results);

            Assert.GreaterOrEqual(_results.Count, 3);
        }
    
        // The whole reason the trace deflects: a Tough dead ahead must send the line back, not let it
        // through. Head-on, so the reflection is exactly the reverse of the incoming direction.
        [Test]
        public void Calculate_DeflectorDeadAhead_ReversesTheLine()
        {
            _deflectors.Add(new DeflectorCircle(new Vector2(0f, 2f), 0.5f));

            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _results);

            Assert.GreaterOrEqual(_results.Count, 2);
            // Contact is the near surface, one radius below the centre.
            Assert.AreEqual(1.5f, _results[1].y, 0.05f);
            // Reversed: the line goes back down past where it started.
            Assert.Less(_results[_results.Count - 1].y, _results[1].y);
        }

        // Two on one step: only the closer is hit, or the line tunnels through a balloon the shot
        // would have bounced off.
        [Test]
        public void Calculate_TwoDeflectorsOnTheSameStep_TakesTheNearer()
        {
            _deflectors.Add(new DeflectorCircle(new Vector2(0f, 4f), 0.5f));
            _deflectors.Add(new DeflectorCircle(new Vector2(0f, 2f), 0.5f));

            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _results);

            Assert.AreEqual(1.5f, _results[1].y, 0.05f, "the nearer surface, not the far one");
        }

        [Test]
        public void Calculate_NoDeflectorsInTheWay_LineIsUnchanged()
        {
            var origin = new Vector3(-2.5f, 0f, 0f);
            var direction = new Vector3(-1f, 1f, 0f).normalized;
            _calculator.Calculate(origin, direction, 0f, _results);
            var withoutDeflectors = new List<Vector3>(_results);

            _deflectors.Add(new DeflectorCircle(new Vector2(4f, -2f), 0.5f)); // far behind the shot

            _calculator.Calculate(origin, direction, 0f, _results);

            CollectionAssert.AreEqual(withoutDeflectors, _results);
        }

        // A budget of zero draws the line up to the first thing it would hit and stops — it does not
        // pretend the balloon is not there and carry on through it.
        [Test]
        public void Calculate_NoReflectionBudget_EndsAtTheContact()
        {
            _config.MaxReflections.Returns(0);
            _deflectors.Add(new DeflectorCircle(new Vector2(0f, 2f), 0.5f));

            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _results);

            Assert.AreEqual(2, _results.Count);
            Assert.AreEqual(1.5f, _results[1].y, 0.05f, "stops on the balloon's surface");
        }

        // What a budget of 1 is FOR: the player sees the turn and the leg leaving it, then the line
        // ends where it would have turned again. Walls and deflections share the budget, so a shot
        // that deflects off a balloon has spent its one reflection and will not also show a wall
        // bounce after it.
        [Test]
        public void Calculate_OneReflection_DrawsTheOutgoingLegThenStops()
        {
            _config.MaxReflections.Returns(1);
            _deflectors.Add(new DeflectorCircle(new Vector2(0f, 2f), 0.5f));

            // Off-centre, so the deflection sends it sideways into a wall rather than straight back.
            _calculator.Calculate(new Vector3(-0.3f, 0f, 0f), Vector3.up, 0f, _results);

            Assert.AreEqual(3, _results.Count,
                "origin, the deflection, and where the outgoing leg ends");
            Assert.AreEqual(DefaultLimits.w, _results[2].x, 0.05f, "the leg runs to the left wall");
        }


        // Contact is the sum of both circles. With the shot treated as a point, a graze down the side
        // of a balloon is drawn sailing past when it would really clip and deflect — the error is
        // worst exactly at the sides, where the normal is most sideways.
        [Test]
        public void Calculate_GrazingShot_DeflectsOnceTheProjectileRadiusIsCounted()
        {
            // Passes 0.6 to the right of centre: outside the balloon's 0.5, inside 0.5 + 0.2.
            _deflectors.Add(new DeflectorCircle(new Vector2(0f, 2f), 0.5f));
            var origin = new Vector3(0.6f, 0f, 0f);

            _calculator.Calculate(origin, Vector3.up, 0f, _results);
            var asAPoint = _results.Count;

            _calculator.Calculate(origin, Vector3.up, 0.2f, _results);

            Assert.AreEqual(2, asAPoint, "as a point it misses entirely — straight to the top wall");
            Assert.Greater(_results.Count, asAPoint, "with its real radius it clips and turns");
            Assert.Less(_results[1].x, origin.x + 0.5f, "contact sits on the near side, not through it");
        }

        [Test]
        public void Calculate_HeadOnContact_SitsOnTheCombinedRadius()
        {
            _deflectors.Add(new DeflectorCircle(new Vector2(0f, 2f), 0.5f));

            _calculator.Calculate(Vector3.zero, Vector3.up, 0.25f, _results);

            // 2 - (0.5 + 0.25): the shot stops where the two circles touch, not at the balloon's skin.
            Assert.AreEqual(1.25f, _results[1].y, 0.05f);
        }

        private sealed class FakeDeflectorField : IDeflectorField
        {
            private readonly List<DeflectorCircle> _circles = new();

            internal void Add(DeflectorCircle circle)
            {
                _circles.Add(circle);
            }

            public void CollectDeflectors(List<DeflectorCircle> results)
            {
                results.Clear();
                results.AddRange(_circles);
            }
        }
}
}
