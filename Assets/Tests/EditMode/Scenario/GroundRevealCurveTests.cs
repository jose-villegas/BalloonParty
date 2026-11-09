using BalloonParty.Configuration.Level;
using BalloonParty.Scenario;
using NUnit.Framework;

namespace BalloonParty.Tests.Scenario
{
    [TestFixture]
    public class GroundRevealCurveTests
    {
        private const float Share = 0.35f;
        private const float Closed = -1f;
        private const float Open = 12f;

        [Test]
        public void TheStart_IsSkyWithNoMist()
        {
            var frame = GroundRevealCurve.Evaluate(0f, Share, Closed, Open);

            Assert.AreEqual(0f, frame.MistDensity);
            Assert.AreEqual(Closed, frame.Radius);
        }

        [Test]
        public void TheMist_RollsInBeforeTheHoleOpens()
        {
            var midway = GroundRevealCurve.Evaluate(Share * 0.5f, Share, Closed, Open);
            var thick = GroundRevealCurve.Evaluate(Share, Share, Closed, Open);

            Assert.That(midway.MistDensity, Is.InRange(0.01f, 0.99f));
            Assert.AreEqual(Closed, midway.Radius);
            Assert.AreEqual(1f, thick.MistDensity);
            Assert.AreEqual(Closed, thick.Radius, 1e-4f);
        }

        [Test]
        public void TheHole_GrowsUntilTheGroundFillsTheView()
        {
            var previous = Closed;
            for (var i = 1; i <= 20; i++)
            {
                var opening = Share + (1f - Share) * i / 20f;
                var frame = GroundRevealCurve.Evaluate(opening, Share, Closed, Open);

                Assert.AreEqual(1f, frame.MistDensity, $"opening {opening}");
                Assert.That(frame.Radius, Is.GreaterThanOrEqualTo(previous), $"opening {opening}");
                previous = frame.Radius;
            }

            Assert.AreEqual(Open, previous, 1e-4f);
        }

        [Test]
        public void IntoTheSky_PlaysTheSameCurveBackwards([Values(0f, 0.2f, 0.5f, 0.8f, 1f)] float progress)
        {
            var toGround = GroundRevealCurve.Opening(ScenarioKind.Ground, progress);
            var toSky = GroundRevealCurve.Opening(ScenarioKind.Sky, 1f - progress);

            Assert.AreEqual(toGround, toSky, 1e-5f);
        }

        [Test]
        public void IntoTheSky_StartsOnTheMeadowAndEndsOnClearSky()
        {
            var first = GroundRevealCurve.Evaluate(
                GroundRevealCurve.Opening(ScenarioKind.Sky, 0f), Share, Closed, Open);
            var last = GroundRevealCurve.Evaluate(
                GroundRevealCurve.Opening(ScenarioKind.Sky, 1f), Share, Closed, Open);

            Assert.AreEqual(Open, first.Radius, 1e-4f);
            Assert.AreEqual(0f, last.MistDensity);
        }

        [Test]
        public void DegenerateMistShare_IsClampedSoTheCurveNeverDividesByZero([Values(0f, 1f, -1f, 5f)] float share)
        {
            for (var i = 0; i <= 10; i++)
            {
                var frame = GroundRevealCurve.Evaluate(i / 10f, share, Closed, Open);

                Assert.IsFalse(float.IsNaN(frame.Radius) || float.IsNaN(frame.MistDensity), $"step {i}");
                Assert.That(frame.MistDensity, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void TheMist_ThickensMonotonicallyBeforeTheHoleOpens()
        {
            var previous = 0f;
            for (var i = 1; i <= 10; i++)
            {
                var frame = GroundRevealCurve.Evaluate(Share * i / 10f, Share, Closed, Open);

                Assert.That(frame.MistDensity, Is.GreaterThanOrEqualTo(previous), $"step {i}");
                previous = frame.MistDensity;
            }
        }

        [Test]
        public void Depth_IntoTheGround_StartsWithTheGroundShrunkAndEndsAtOne([Values(1.5f, 2f, 4f)] float zoom)
        {
            var start = GroundRevealCurve.Depth(ScenarioKind.Ground, 0f, zoom);
            var end = GroundRevealCurve.Depth(ScenarioKind.Ground, 1f, zoom);

            Assert.AreEqual(1f / zoom, start.Ground, 1e-5f);
            Assert.AreEqual(1f, start.Sky, 1e-5f);
            Assert.AreEqual(1f, end.Ground, 1e-5f);
            Assert.AreEqual(zoom, end.Sky, 1e-5f);
        }

        [Test]
        public void Depth_SkyIsAlwaysZoomTimesGround([Values(0f, 0.3f, 0.7f, 1f)] float opening, [Values(1.5f, 3f)] float zoom)
        {
            var depth = GroundRevealCurve.Depth(ScenarioKind.Ground, opening, zoom);

            Assert.AreEqual(depth.Ground * zoom, depth.Sky, 1e-5f);
        }

        [Test]
        public void Depth_IntoTheGround_IncomingIsTheGroundAndOutgoingTheSky()
        {
            var depth = GroundRevealCurve.Depth(ScenarioKind.Ground, 0.4f, 2f);

            Assert.AreEqual(depth.Ground, depth.Incoming);
            Assert.AreEqual(depth.Sky, depth.Outgoing);
        }

        [Test]
        public void Depth_IntoTheSky_IncomingIsTheSkyAndOutgoingTheGround()
        {
            var depth = GroundRevealCurve.Depth(ScenarioKind.Sky, 0.4f, 2f);

            Assert.AreEqual(depth.Sky, depth.Incoming);
            Assert.AreEqual(depth.Ground, depth.Outgoing);
        }

        [Test]
        public void Depth_AcrossAnyReveal_TheIncomingLayerLandsAtOneAndTheOutgoingStartsAtOne()
        {
            foreach (var target in new[] { ScenarioKind.Ground, ScenarioKind.Sky })
            {
                var first = GroundRevealCurve.Depth(target, GroundRevealCurve.Opening(target, 0f), 2.5f);
                var last = GroundRevealCurve.Depth(target, GroundRevealCurve.Opening(target, 1f), 2.5f);

                Assert.AreEqual(1f, first.Outgoing, 1e-5f, $"{target} start");
                Assert.AreEqual(1f, last.Incoming, 1e-5f, $"{target} end");
            }
        }

        [Test]
        public void Depth_GroundGrowsMonotonicallyAsTheGroundOpens()
        {
            var previous = 0f;
            for (var i = 0; i <= 10; i++)
            {
                var depth = GroundRevealCurve.Depth(ScenarioKind.Ground, i / 10f, 2f);

                Assert.That(depth.Ground, Is.GreaterThanOrEqualTo(previous), $"step {i}");
                previous = depth.Ground;
            }
        }

        [Test]
        public void Depth_ZoomOfOneOrLess_HasNoDepth([Values(1f, 0.5f, 0f, -3f)] float zoom)
        {
            var depth = GroundRevealCurve.Depth(ScenarioKind.Ground, 0.5f, zoom);

            Assert.AreEqual(1f, depth.Ground);
            Assert.AreEqual(1f, depth.Sky);
            Assert.AreEqual(1f, depth.Incoming);
            Assert.AreEqual(1f, depth.Outgoing);
        }

        [Test]
        public void Depth_OutOfRangeOpening_IsClamped()
        {
            var before = GroundRevealCurve.Depth(ScenarioKind.Ground, -5f, 2f);
            var after = GroundRevealCurve.Depth(ScenarioKind.Ground, 5f, 2f);

            Assert.AreEqual(0.5f, before.Ground, 1e-5f);
            Assert.AreEqual(1f, after.Ground, 1e-5f);
        }

        [Test]
        public void OutOfRangeInputs_StayOnTheCurve()
        {
            var before = GroundRevealCurve.Evaluate(-2f, Share, Closed, Open);
            var after = GroundRevealCurve.Evaluate(3f, Share, Closed, Open);

            Assert.AreEqual(0f, before.MistDensity);
            Assert.AreEqual(Open, after.Radius, 1e-4f);
            Assert.AreEqual(1f, GroundRevealCurve.Opening(ScenarioKind.Ground, 7f));
        }
    }
}
