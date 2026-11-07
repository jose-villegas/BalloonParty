using BalloonParty.Game.Run;
using BalloonParty.Shared;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class RetryTrackerTests
    {
        [Test]
        public void MaxRetriesZero_IsUnlimited()
        {
            var tracker = Tracker(0);

            for (var i = 0; i < 50; i++)
            {
                Retry(tracker, 4);
            }

            Assert.IsTrue(tracker.IsUnlimited);
            Assert.AreEqual(int.MaxValue, tracker.RetriesRemaining, "unlimited never runs out");
            Assert.AreEqual(50, tracker.RetriesUsed, "still counted: the ad gate reads it");
        }

        [Test]
        public void LimitedRetries_CountDown()
        {
            var tracker = Tracker(3);

            Retry(tracker, 2);
            Retry(tracker, 2);

            Assert.IsFalse(tracker.IsUnlimited);
            Assert.AreEqual(1, tracker.RetriesRemaining);
            Assert.AreEqual(2, tracker.RetriesUsed);
        }

        [Test]
        public void FreshRun_ClearsTheRetriesUsed()
        {
            var tracker = Tracker(0);
            Retry(tracker, 3);
            Retry(tracker, 3);

            tracker.ResetRun(0);

            Assert.AreEqual(0, tracker.RetriesUsed);
            Assert.AreEqual(0, tracker.RetryLevel);
        }

        [Test]
        public void ResetRun_ConsumesTheStagedLevel()
        {
            var tracker = Tracker(0);
            tracker.PrepareRetry(5);
            Assert.AreEqual(5, tracker.RetryLevel, "read by the reset cascade before the tracker's own stage");

            tracker.ResetRun(0);

            Assert.AreEqual(0, tracker.RetryLevel);
            Assert.AreEqual(1, tracker.RetriesUsed);
        }

        [Test]
        public void NegativeMaxRetries_IsUnlimited()
        {
            var tracker = Tracker(-1);

            Assert.IsTrue(tracker.IsUnlimited);
            Assert.AreEqual(int.MaxValue, tracker.RetriesRemaining);
        }

        [Test]
        public void LimitedRetries_AllUsed_RemainingHitsZero()
        {
            var tracker = Tracker(2);

            Retry(tracker, 2);
            Retry(tracker, 2);

            Assert.AreEqual(0, tracker.RetriesRemaining);
        }

        [Test]
        public void PrepareRetry_WithoutReset_DoesNotConsumeARetry()
        {
            var tracker = Tracker(2);

            tracker.PrepareRetry(4);

            Assert.AreEqual(0, tracker.RetriesUsed);
            Assert.AreEqual(2, tracker.RetriesRemaining);
        }

        [Test]
        public void FreshRunAfterRetries_RestoresLimitedBudget()
        {
            var tracker = Tracker(2);
            Retry(tracker, 3);

            tracker.ResetRun(0);

            Assert.AreEqual(2, tracker.RetriesRemaining);
        }

        private static RetryTracker Tracker(int maxRetries)
        {
            var config = Substitute.For<IRunConfig>();
            config.MaxRetries.Returns(maxRetries);
            return new RetryTracker(config);
        }

        private static void Retry(RetryTracker tracker, int level)
        {
            tracker.PrepareRetry(level);
            tracker.ResetRun(0);
        }
    }
}
