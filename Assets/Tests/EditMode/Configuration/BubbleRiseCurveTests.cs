using BalloonParty.Configuration.Level;
using NUnit.Framework;

namespace BalloonParty.Tests.Configuration
{
    [TestFixture]
    public class BubbleRiseCurveTests
    {
        // Grace 6 s, then 15% climbing to 65% over 60 s.
        private static readonly BubbleRiseSettings Settings = new(6f, 0.15f, 0.65f, 60f);

        [Test]
        public void DuringTheGrace_NoBubbles([Values(0f, 3f, 5.99f)] float seconds)
        {
            Assert.AreEqual(0f, BubbleRiseCurve.ShareAt(Settings, seconds));
        }

        [Test]
        public void AsTheGraceEnds_TheStartShare()
        {
            Assert.AreEqual(0.15f, BubbleRiseCurve.ShareAt(Settings, 6f), 1e-5f);
        }

        [Test]
        public void HalfwayUpTheRamp_HalfwayBetweenStartAndMax()
        {
            Assert.AreEqual(0.4f, BubbleRiseCurve.ShareAt(Settings, 36f), 1e-5f);
        }

        [Test]
        public void AfterTheRamp_HoldsAtTheMax([Values(66f, 120f, 10000f)] float seconds)
        {
            Assert.AreEqual(0.65f, BubbleRiseCurve.ShareAt(Settings, seconds), 1e-5f);
        }

        [Test]
        public void TheLongerTheLevelRuns_TheMoreOfEachLineIsBubbles()
        {
            var previous = 0f;
            for (var seconds = 0f; seconds <= 90f; seconds += 0.5f)
            {
                var share = BubbleRiseCurve.ShareAt(Settings, seconds);
                Assert.That(share, Is.GreaterThanOrEqualTo(previous), $"at {seconds}s");
                previous = share;
            }

            Assert.That(previous, Is.GreaterThan(0.5f), "the ramp ends with more bubbles than balloons");
        }

        [Test]
        public void NoRamp_JumpsStraightToTheMax()
        {
            var settings = new BubbleRiseSettings(2f, 0.1f, 0.8f, 0f);

            Assert.AreEqual(0f, BubbleRiseCurve.ShareAt(settings, 1.9f));
            Assert.AreEqual(0.8f, BubbleRiseCurve.ShareAt(settings, 2f), 1e-5f);
        }

        [Test]
        public void ShippedDefaults_AreTheOwnersCurve()
        {
            var settings = new BubbleRiseSettings();

            Assert.AreEqual((6f, 0.15f, 0.65f, 60f), (settings.GraceSeconds, settings.StartShare, settings.MaxShare,
                settings.RampSeconds));
            Assert.AreEqual((12, 1), (settings.MaxBubbles, settings.StartBubblesForLevel(1)));
        }

        [Test]
        public void ShippedDefaults_LinesBringOneMoreBubbleEveryFifteenLevels()
        {
            var settings = new BubbleRiseSettings();

            Assert.AreEqual(1, settings.StartBubblesForLevel(15));
            Assert.AreEqual(2, settings.StartBubblesForLevel(16));
            Assert.AreEqual(3, settings.StartBubblesForLevel(36));
        }

        [Test]
        public void StartBubbles_NoExtraStep_HoldsAtEveryLevel()
        {
            var settings = new BubbleRiseSettings(6f, 0.15f, 0.65f, 60f, startBubbles: 2, extraBubbleEveryLevels: 0);

            Assert.AreEqual(2, settings.StartBubblesForLevel(100));
        }

        [Test]
        public void StartBubbles_LevelsZeroAndOne_AreTheStart()
        {
            var settings = new BubbleRiseSettings(6f, 0.15f, 0.65f, 60f, startBubbles: 2, extraBubbleEveryLevels: 15);

            Assert.AreEqual((2, 2), (settings.StartBubblesForLevel(0), settings.StartBubblesForLevel(1)));
        }

        [Test]
        public void StartBubbles_StartBelowOne_FloorsAtOne()
        {
            var settings = new BubbleRiseSettings(6f, 0.15f, 0.65f, 60f, startBubbles: 0, extraBubbleEveryLevels: 0);

            Assert.AreEqual(1, settings.StartBubblesForLevel(1));
        }

        [Test]
        public void StartBubbles_DebugPresetLevels_StartAsLevelOne()
        {
            var settings = new BubbleRiseSettings(6f, 0.15f, 0.65f, 60f, startBubbles: 1, extraBubbleEveryLevels: 5);

            Assert.AreEqual(1, settings.StartBubblesForLevel(-100));
        }
    }
}
