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
