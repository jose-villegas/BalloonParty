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
        private FakePierceItemField _pierceItems;
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
            _pierceItems = new FakePierceItemField();
            _calculator = new PredictionTraceCalculator(_config, _flightConfig, _deflectors, _pierceItems);
            _results = new List<Vector3>();
        }

        [Test]
        public void Calculate_LeftWallBounce_ReflectsAtLeftLimit()
        {
            var origin = new Vector3(-2.5f, 0f, 0f);
            var direction = new Vector3(-1f, 1f, 0f).normalized;

            _calculator.Calculate(origin, direction, 0f, _results, out _);

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

            _calculator.Calculate(origin, direction, 0f, _results, out _);

            Assert.AreEqual(3, _results.Count);
            Assert.AreEqual(DefaultLimits.y, _results[1].x, 0.01f, "y is the right wall");
            Assert.AreEqual(DefaultLimits.x, _results[2].y, 0.01f, "the leg ends on the top wall");
        }

        [Test]
        public void Calculate_MaxSteps_StopsBeforeReachingWall()
        {
            _config.MaxSegments.Returns(3);
            _config.MaxReflections.Returns(10);

            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _results, out _);

            // Origin plus where it got to. It used to be the origin alone, which drew no line at all —
            // invisible before deflections existed, because every upward shot ends on a wall, and a
            // deflection can send the line anywhere the segment budget runs out first.
            Assert.AreEqual(2, _results.Count);
            Assert.AreEqual(3f * 0.5f, _results[1].y, 0.01f, "three steps of 0.5 travelled");
        }

        [Test]
        public void Calculate_RightWallBounce_ReflectsAtRightLimit()
        {
            var origin = new Vector3(2.5f, 0f, 0f);
            var direction = new Vector3(1f, 1f, 0f).normalized;

            _calculator.Calculate(origin, direction, 0f, _results, out _);

            Assert.GreaterOrEqual(_results.Count, 2);
            Assert.AreEqual(DefaultLimits.y, _results[1].x, 0.01f);
        }

        // The bottom wall is a real, visible wall the live shot bounces off (ProjectileMotionResolver
        // reflects off it the same as the other three) — a deflection sent downward must stop and
        // bounce there instead of running past the visible play area on the segment budget alone.
        [Test]
        public void Calculate_DeflectionSendsLineDownward_BouncesAtBottomLimit()
        {
            _config.MaxReflections.Returns(1);
            _deflectors.Add(new DeflectorCircle(new Vector2(0f, 2f), 0.5f));

            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _results, out var end);

            Assert.AreEqual(PredictionTraceEndKind.Wall, end.Kind);
            Assert.AreEqual(DefaultLimits.z, _results[_results.Count - 1].y, 0.05f,
                "the reflected line stops at the bottom wall, not off past the visible play area");
        }

        // Mirrors the bottom-limit test above but with the authored production numbers
        // (Assets/Configuration/PredictionTraceConfig.asset / ProjectileFlightConfig.asset) rather than
        // the test fixture's own round DefaultLimits/SegmentLength — a deflection sent back up toward
        // the top wall must still reach and terminate there under the real config, not just under
        // numbers chosen to land on convenient step boundaries.
        [Test]
        public void Calculate_DeflectionSendsLineUpward_ReachesTopLimitUnderProductionConfig()
        {
            var limits = new Vector4(3.625f, 2.25f, -4.5f, -2.25f);
            _flightConfig.LimitsClockwise.Returns(limits);
            _config.SegmentLength.Returns(1f);
            _config.MaxSegments.Returns(64);
            _config.MaxReflections.Returns(1);

            _deflectors.Add(new DeflectorCircle(new Vector2(0f, -2f), 0.5f));

            _calculator.Calculate(Vector3.zero, Vector3.down, 0f, _results, out var end);

            Assert.AreEqual(PredictionTraceEndKind.Wall, end.Kind);
            Assert.AreEqual(limits.x, _results[_results.Count - 1].y, 0.05f,
                "the leg reflected off the balloon reaches and stops at the top wall");
        }

        [Test]
        public void Calculate_StraightUp_HitsTopWall()
        {
            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _results, out _);

            var lastPoint = _results[_results.Count - 1];
            Assert.AreEqual(DefaultLimits.x, lastPoint.y, 0.01f);
        }

        [Test]
        public void Calculate_TopWallHit_TerminatesBouncing()
        {
            _config.MaxReflections.Returns(10);
            _config.MaxSegments.Returns(200);

            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _results, out _);

            Assert.AreEqual(2, _results.Count);
        }

        [Test]
        public void Calculate_ZigZag_ProducesMultipleBouncePoints()
        {
            _config.MaxReflections.Returns(5);
            _config.MaxSegments.Returns(200);
            _config.SegmentLength.Returns(1f);

            var direction = new Vector3(1f, 0.3f, 0f).normalized;

            _calculator.Calculate(Vector3.zero, direction, 0f, _results, out _);

            Assert.GreaterOrEqual(_results.Count, 3);
        }
    
        // The whole reason the trace deflects: a Tough dead ahead must send the line back, not let it
        // through. Head-on, so the reflection is exactly the reverse of the incoming direction.
        [Test]
        public void Calculate_DeflectorDeadAhead_ReversesTheLine()
        {
            _deflectors.Add(new DeflectorCircle(new Vector2(0f, 2f), 0.5f));

            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _results, out _);

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

            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _results, out _);

            Assert.AreEqual(1.5f, _results[1].y, 0.05f, "the nearer surface, not the far one");
        }

        [Test]
        public void Calculate_NoDeflectorsInTheWay_LineIsUnchanged()
        {
            var origin = new Vector3(-2.5f, 0f, 0f);
            var direction = new Vector3(-1f, 1f, 0f).normalized;
            _calculator.Calculate(origin, direction, 0f, _results, out _);
            var withoutDeflectors = new List<Vector3>(_results);

            _deflectors.Add(new DeflectorCircle(new Vector2(4f, -2f), 0.5f)); // far behind the shot

            _calculator.Calculate(origin, direction, 0f, _results, out _);

            CollectionAssert.AreEqual(withoutDeflectors, _results);
        }

        // A budget of zero draws the line up to the first thing it would hit and stops — it does not
        // pretend the balloon is not there and carry on through it.
        [Test]
        public void Calculate_NoReflectionBudget_EndsAtTheContact()
        {
            _config.MaxReflections.Returns(0);
            _deflectors.Add(new DeflectorCircle(new Vector2(0f, 2f), 0.5f));

            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _results, out _);

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
            _calculator.Calculate(new Vector3(-0.3f, 0f, 0f), Vector3.up, 0f, _results, out _);

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

            _calculator.Calculate(origin, Vector3.up, 0f, _results, out _);
            var asAPoint = _results.Count;

            _calculator.Calculate(origin, Vector3.up, 0.2f, _results, out _);

            Assert.AreEqual(2, asAPoint, "as a point it misses entirely — straight to the top wall");
            Assert.Greater(_results.Count, asAPoint, "with its real radius it clips and turns");
            Assert.Less(_results[1].x, origin.x + 0.5f, "contact sits on the near side, not through it");
        }

        // The shot's CENTRE turns where the two circles touch, but the drawn corner goes on the
        // balloon's skin: a line one shot-radius short of the balloon reads as a bend that happened
        // too early. Detection still uses the combined radius — Calculate_GrazingShot covers that,
        // and would fail if this drew the skin contact instead of merely painting it there.
        [Test]
        public void Calculate_HeadOnContact_IsDrawnOnTheBalloonSkin()
        {
            _deflectors.Add(new DeflectorCircle(new Vector2(0f, 2f), 0.5f));

            _calculator.Calculate(Vector3.zero, Vector3.up, 0.25f, _results, out _);

            // 2 - 0.5, not 2 - (0.5 + 0.25).
            Assert.AreEqual(1.5f, _results[1].y, 0.05f);
        }

        // No deflector on the path at all — the only thing a pierce-item host should do here is drop
        // a vertex on an otherwise-straight run and report where, not bend or shorten the line.
        [Test]
        public void Calculate_PierceItemWithNoDeflectorInTheWay_InsertsVertexAndReportsPierceStartIndex()
        {
            _pierceItems.Add(new PierceItemCircle(new Vector2(0f, 2f), 0.5f));

            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _results, out var end);

            Assert.AreEqual(3, _results.Count, "origin, the pierce mark, and the top wall — unchanged otherwise");
            Assert.AreEqual(1, end.PierceStartIndex);
            Assert.AreEqual(1.5f, _results[end.PierceStartIndex].y, 0.05f,
                "the item's own contact, same math as a deflector's");
            Assert.AreEqual(DefaultLimits.x, _results[2].y, 0.05f, "still runs straight up to the top wall");
        }

        // Pierce-start index must stay -1 for a trace that never crosses a pierce-item host — the
        // signal a view relies on to know whether to style a trailing run at all.
        [Test]
        public void Calculate_NoPierceItemsInTheWay_PierceStartIndexStaysNegativeOne()
        {
            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _results, out var end);

            Assert.AreEqual(-1, end.PierceStartIndex);
        }

        // The whole point of pierce-awareness: a tough beyond the pierce-item host must not still
        // draw a bounce — the real shot would already be piercing by the time it gets there.
        [Test]
        public void Calculate_ToughBeyondPierceHost_IsPassedThroughInsteadOfDeflecting()
        {
            _pierceItems.Add(new PierceItemCircle(new Vector2(0f, 2f), 0.5f));
            _deflectors.Add(new DeflectorCircle(new Vector2(0f, 3.5f), 0.5f));

            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _results, out var end);

            Assert.AreEqual(1, end.PierceStartIndex);
            Assert.AreEqual(3, _results.Count, "no extra bend vertex for the tough beyond the pierce host");
            Assert.AreEqual(DefaultLimits.x, _results[2].y, 0.05f,
                "reached the top wall instead of stopping/reversing at the tough");
        }

        // A tough that itself hosts the pierce item still deflects at that exact contact — the item's
        // activation lands a frame after the bounce it was hit on — but a SECOND tough further along
        // the reflected path no longer deflects once piercing has armed.
        [Test]
        public void Calculate_ToughHostingPierceItem_StillDeflectsButLaterToughDoesNot()
        {
            _config.MaxSegments.Returns(20);
            _deflectors.Add(new DeflectorCircle(new Vector2(0f, 2f), 0.5f));
            _pierceItems.Add(new PierceItemCircle(new Vector2(0f, 2f), 0.5f));
            _deflectors.Add(new DeflectorCircle(new Vector2(0f, -2f), 0.5f));

            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _results, out var end);

            Assert.AreEqual(1, end.PierceStartIndex, "arms at the same contact that still deflects");
            Assert.AreEqual(1.5f, _results[1].y, 0.05f, "the pierce mark sits at the shared contact");
            Assert.AreEqual(1.5f, _results[2].y, 0.05f, "the deflection at that same contact still happens");
            var lastPoint = _results[_results.Count - 1];
            Assert.Less(lastPoint.y, -1.5f,
                "kept travelling straight down past the second tough instead of bouncing off it");
        }

        // The scenario a same-step comparison must get right: a DIFFERENT, nearer pierce-item host must
        // pre-empt a farther deflector found within the SAME step — the deflector must not still bounce
        // the line just because both were discovered while resolving the same iteration. Both circles
        // sit well inside one 0.5-unit segment on purpose.
        [Test]
        public void Calculate_PierceItemNearerThanDeflectorOnTheSameStep_ArmsInsteadOfDeflecting()
        {
            _pierceItems.Add(new PierceItemCircle(new Vector2(0f, 0.2f), 0.05f));
            _deflectors.Add(new DeflectorCircle(new Vector2(0f, 0.4f), 0.05f));

            _calculator.Calculate(Vector3.zero, Vector3.up, 0f, _results, out var end);

            Assert.AreEqual(1, end.PierceStartIndex);
            Assert.AreEqual(0.15f, _results[end.PierceStartIndex].y, 0.01f,
                "arms at the nearer pierce item's own contact, not the farther deflector's");
            Assert.AreEqual(DefaultLimits.x, _results[_results.Count - 1].y, 0.05f,
                "kept travelling straight to the top wall — the farther deflector never bounced it");
        }

        // Mirrors the "deflector beyond a wall can't steal a bounce" rule: the pierce test is bounded
        // by the same wall-clipped step, so a host sitting past the wall cannot arm piercing on it.
        [Test]
        public void Calculate_PierceItemBeyondAWall_DoesNotArmOnThatStep()
        {
            _pierceItems.Add(new PierceItemCircle(new Vector2(3.2f, 0f), 0.05f));
            var origin = new Vector3(2.9f, 0f, 0f);

            _calculator.Calculate(origin, Vector3.right, 0f, _results, out var end);

            Assert.AreEqual(-1, end.PierceStartIndex, "the wall-clipped step ends before reaching the item");
            Assert.AreEqual(DefaultLimits.y, _results[1].x, 0.05f, "still bounces normally off the right wall");
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

        private sealed class FakePierceItemField : IPierceItemField
        {
            private readonly List<PierceItemCircle> _circles = new();

            internal void Add(PierceItemCircle circle)
            {
                _circles.Add(circle);
            }

            public void CollectPierceItems(List<PierceItemCircle> results)
            {
                results.Clear();
                results.AddRange(_circles);
            }
        }
    }
}
