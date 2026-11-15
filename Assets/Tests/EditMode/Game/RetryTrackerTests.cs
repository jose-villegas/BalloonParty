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
            tracker.PrepareRetry(5, withBonusCard: false);
            Assert.AreEqual(5, tracker.RetryLevel, "read by the reset cascade before the tracker's own stage");

            tracker.ResetRun(0);

            Assert.AreEqual(0, tracker.RetryLevel);
            Assert.AreEqual(1, tracker.RetriesUsed);
        }

        [Test]
        public void BonusCard_IsStagedWithTheRetryAndClearedByTheReset()
        {
            var tracker = Tracker(0);
            tracker.PrepareRetry(5, withBonusCard: true);
            Assert.IsTrue(tracker.RetryWithBonusCard, "read by RunUpgrades during the reset cascade");

            tracker.ResetRun(0);

            Assert.IsFalse(tracker.RetryWithBonusCard, "a later plain retry must not inherit the bonus");
        }

        [Test]
        public void BonusRetry_CountsAsAUsedRetry()
        {
            var tracker = Tracker(3);

            tracker.PrepareRetry(5, withBonusCard: true);
            tracker.ResetRun(0);

            Assert.AreEqual(1, tracker.RetriesUsed, "an ad-bought retry still feeds the ad gate and the cap");
            Assert.AreEqual(2, tracker.RetriesRemaining);
        }

        [Test]
        public void PrepareRetry_StagedAgainWithoutABonus_DropsTheBonus()
        {
            var tracker = Tracker(0);
            tracker.PrepareRetry(5, withBonusCard: true);

            tracker.PrepareRetry(5, withBonusCard: false);

            Assert.IsFalse(tracker.RetryWithBonusCard);
        }

        [Test]
        public void FreshRunAfterABonusRetry_StartsWithoutTheBonus()
        {
            var tracker = Tracker(0);
            tracker.PrepareRetry(5, withBonusCard: true);
            tracker.ResetRun(0);

            tracker.ResetRun(0);

            Assert.IsFalse(tracker.RetryWithBonusCard);
            Assert.AreEqual(0, tracker.RetriesUsed);
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

            tracker.PrepareRetry(4, withBonusCard: false);

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

        [Test]
        public void PrepareResume_StagesTheSavedLevelForTheResetCascade()
        {
            var tracker = Tracker(0);
            var save = new RunSnapshot { Level = 8, Hearts = 2, RetriesUsed = 2 };

            tracker.PrepareResume(save);

            Assert.AreEqual(8, tracker.RetryLevel, "the retry path's readers set the level up as its retry would");
            Assert.AreSame(save, tracker.Resuming);
            Assert.IsFalse(tracker.RetryWithBonusCard);
        }

        [Test]
        public void Resume_SpendsNoRetry_AndKeepsTheSavedCount()
        {
            var tracker = Tracker(0);

            tracker.PrepareResume(new RunSnapshot { Level = 8, Hearts = 2, RetriesUsed = 3 });
            tracker.ResetRun(0);

            Assert.AreEqual(3, tracker.RetriesUsed, "not one more (a resume isn't a retry), not zero (the ad gate)");
            Assert.AreEqual(0, tracker.RetryLevel);
            Assert.IsNull(tracker.Resuming, "cleared with the retry level");
        }

        [Test]
        public void RetryAfterAResume_CountsOnFromTheSavedRetries()
        {
            var tracker = Tracker(5);
            tracker.PrepareResume(new RunSnapshot { Level = 8, Hearts = 2, RetriesUsed = 3 });
            tracker.ResetRun(0);

            Retry(tracker, 8);

            Assert.AreEqual(4, tracker.RetriesUsed);
            Assert.AreEqual(1, tracker.RetriesRemaining);
        }

        [Test]
        public void PrepareRetry_AfterAStagedResume_IsAPlainRetry()
        {
            var tracker = Tracker(0);
            tracker.PrepareResume(new RunSnapshot { Level = 8, Hearts = 2, RetriesUsed = 3 });

            tracker.PrepareRetry(8, withBonusCard: false);

            Assert.IsNull(tracker.Resuming);
        }

        private static RetryTracker Tracker(int maxRetries)
        {
            var config = Substitute.For<IRunConfig>();
            config.MaxRetries.Returns(maxRetries);
            return new RetryTracker(config);
        }

        private static void Retry(RetryTracker tracker, int level)
        {
            tracker.PrepareRetry(level, withBonusCard: false);
            tracker.ResetRun(0);
        }
    }
}
