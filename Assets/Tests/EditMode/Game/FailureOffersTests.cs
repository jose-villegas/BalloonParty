using System;
using System.Threading;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class FailureOffersTests
    {
        private IRetryState _retryState;
        private IRetryStaging _staging;
        private IRunConfig _config;
        private IAdService _ads;
        private IBonusCardSource _bonusCards;
        private FailureOffers _offers;

        [SetUp]
        public void SetUp()
        {
            _retryState = Substitute.For<IRetryState>();
            _retryState.IsUnlimited.Returns(true);
            _retryState.RetriesRemaining.Returns(int.MaxValue);
            _staging = Substitute.For<IRetryStaging>();

            _config = Substitute.For<IRunConfig>();
            _config.RetriesBeforeAd.Returns(2);

            _ads = Substitute.For<IAdService>();
            AdEnds(AdResult.Watched);

            _bonusCards = Substitute.For<IBonusCardSource>();
            _bonusCards.CanDealBonus.Returns(true);

            _offers = new FailureOffers(_retryState, _staging, _config, _ads, _bonusCards);
        }

        [Test]
        public void Current_WithinTheFreeRetries_OffersAPlainRetry()
        {
            _retryState.RetriesUsed.Returns(1);

            var offer = _offers.Current;

            Assert.IsTrue(offer.CanRetry);
            Assert.IsFalse(offer.RetryNeedsAd);
            Assert.AreEqual(-1, offer.RetriesLeft, "unlimited");
        }

        [Test]
        public void Current_FreeRetriesUsed_GatesTheRetryBehindAnAd()
        {
            _retryState.RetriesUsed.Returns(2);

            Assert.IsTrue(_offers.Current.RetryNeedsAd);
        }

        [Test]
        public void Current_NoFreeRetryLimit_NeverNeedsAnAd()
        {
            _config.RetriesBeforeAd.Returns(0);
            _retryState.RetriesUsed.Returns(100);

            Assert.IsFalse(_offers.Current.RetryNeedsAd);
        }

        [Test]
        public void Current_LimitedRetries_ReportsWhatIsLeft()
        {
            _retryState.IsUnlimited.Returns(false);
            _retryState.RetriesRemaining.Returns(2);

            Assert.AreEqual(2, _offers.Current.RetriesLeft);
        }

        [TestCase(0, 2)]
        [TestCase(1, 1)]
        [TestCase(2, 0)]
        [TestCase(5, 0)]
        public void Current_CountsTheFreeRetriesLeft(int used, int left)
        {
            _retryState.RetriesUsed.Returns(used);

            var offer = _offers.Current;

            Assert.AreEqual(2, offer.FreeRetries, "RetriesBeforeAd");
            Assert.AreEqual(left, offer.FreeRetriesLeft);
        }

        [Test]
        public void Current_NoAdGateAndNoCap_HasNoFreeRetriesToCount()
        {
            _config.RetriesBeforeAd.Returns(0);

            Assert.AreEqual(0, _offers.Current.FreeRetries);
        }

        [Test]
        public void Current_NoAdGate_CountsACappedRunsRetries()
        {
            _config.RetriesBeforeAd.Returns(0);
            _retryState.IsUnlimited.Returns(false);
            _retryState.RetriesUsed.Returns(1);
            _retryState.RetriesRemaining.Returns(2);

            var offer = _offers.Current;

            Assert.AreEqual(3, offer.FreeRetries);
            Assert.AreEqual(2, offer.FreeRetriesLeft);
        }

        [Test]
        public void Current_CapBelowTheFreeRetries_CountsOnlyTheCap()
        {
            _config.RetriesBeforeAd.Returns(3);
            _retryState.IsUnlimited.Returns(false);
            _retryState.RetriesRemaining.Returns(1);

            var offer = _offers.Current;

            Assert.AreEqual(1, offer.FreeRetries);
            Assert.AreEqual(1, offer.FreeRetriesLeft);
        }

        [Test]
        public void Current_LimitedRetriesRunOut_OffersNoRetry()
        {
            RunOutOfRetries();

            var offer = _offers.Current;

            Assert.IsFalse(offer.CanRetry);
            Assert.IsFalse(offer.RetryNeedsAd, "no ad sells a retry the run doesn't have");
        }

        [Test]
        public void TryRetry_PlainRetry_StagesItWithoutAnAd()
        {
            Assert.IsTrue(TryRetry(7));

            _staging.Received(1).PrepareRetry(7, false);
            _ads.DidNotReceiveWithAnyArgs().ShowRewardedAsync(default, default);
        }

        [Test]
        public void TryRetry_GatedRetry_StagesItOnceTheAdIsWatched()
        {
            _retryState.RetriesUsed.Returns(2);

            Assert.IsTrue(TryRetry(7));

            _ads.Received(1).ShowRewardedAsync(AdPlacement.RetryGate, Arg.Any<CancellationToken>());
            _staging.Received(1).PrepareRetry(7, false);
        }

        [Test]
        public void TryRetry_AdSkipped_StagesNothing()
        {
            _retryState.RetriesUsed.Returns(2);
            AdEnds(AdResult.Skipped);

            Assert.IsFalse(TryRetry(7));
            _staging.DidNotReceiveWithAnyArgs().PrepareRetry(default, default);
        }

        [Test]
        public void TryRetry_AdUnavailable_GrantsTheRetry()
        {
            _retryState.RetriesUsed.Returns(2);
            AdEnds(AdResult.Unavailable);

            Assert.IsTrue(TryRetry(7), "no fill isn't the player's doing");
            _staging.Received(1).PrepareRetry(7, false);
        }

        [Test]
        public void TryRetry_AdServiceThrows_GrantsTheRetry()
        {
            _retryState.RetriesUsed.Returns(2);
            _ads.ShowRewardedAsync(Arg.Any<AdPlacement>(), Arg.Any<CancellationToken>())
                .Throws(new InvalidOperationException("sdk down"));

            Assert.IsTrue(TryRetry(7), "a broken SDK reads as no ad, not as a skip");
            _staging.Received(1).PrepareRetry(7, false);
        }

        [Test]
        public void TryRetry_Cancelled_StagesNothing()
        {
            _retryState.RetriesUsed.Returns(2);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var staged = _offers.TryRetryAsync(7, cts.Token).GetAwaiter().GetResult();

            Assert.IsFalse(staged, "a screen that moved on must not leave a retry staged for the next reset");
            _staging.DidNotReceiveWithAnyArgs().PrepareRetry(default, default);
        }

        [Test]
        public void TryRetry_NoRetryLeft_RefusesWithoutAnAd()
        {
            RunOutOfRetries();

            Assert.IsFalse(TryRetry(7));
            _ads.DidNotReceiveWithAnyArgs().ShowRewardedAsync(default, default);
            _staging.DidNotReceiveWithAnyArgs().PrepareRetry(default, default);
        }

        [Test]
        public void Current_ABonusCardCanBeDealt_OffersTheBonusRetry()
        {
            Assert.IsTrue(_offers.Current.CanBonusRetry);
        }

        [Test]
        public void Current_NoBonusCardLeft_OffersNoBonusRetry()
        {
            _bonusCards.CanDealBonus.Returns(false);

            Assert.IsFalse(_offers.Current.CanBonusRetry, "the ad is never sold for a card that can't be dealt");
        }

        [Test]
        public void Current_NoRetryLeft_OffersNoBonusRetry()
        {
            RunOutOfRetries();

            Assert.IsFalse(_offers.Current.CanBonusRetry);
        }

        [Test]
        public void TryBonusRetry_AdWatched_StagesTheRetryWithABonusCard()
        {
            Assert.IsTrue(TryBonusRetry(7));

            _ads.Received(1).ShowRewardedAsync(AdPlacement.BonusRetry, Arg.Any<CancellationToken>());
            _staging.Received(1).PrepareRetry(7, true);
        }

        [Test]
        public void TryBonusRetry_RetryAlsoGated_OneAdPaysForBoth()
        {
            _retryState.RetriesUsed.Returns(2);

            Assert.IsTrue(TryBonusRetry(7));

            _ads.Received(1).ShowRewardedAsync(Arg.Any<AdPlacement>(), Arg.Any<CancellationToken>());
            _ads.DidNotReceive().ShowRewardedAsync(AdPlacement.RetryGate, Arg.Any<CancellationToken>());
            _staging.Received(1).PrepareRetry(7, true);
        }

        [Test]
        public void TryBonusRetry_AdSkipped_StagesNothing()
        {
            AdEnds(AdResult.Skipped);

            Assert.IsFalse(TryBonusRetry(7), "a reward needs its ad watched; the plain retry stays on offer");
            _staging.DidNotReceiveWithAnyArgs().PrepareRetry(default, default);
        }

        [Test]
        public void TryBonusRetry_AdUnavailable_StagesNothing()
        {
            AdEnds(AdResult.Unavailable);

            Assert.IsFalse(TryBonusRetry(7), "no ad shown, no reward: unlike the retry gate, nothing is owed");
            _staging.DidNotReceiveWithAnyArgs().PrepareRetry(default, default);
        }

        [Test]
        public void TryBonusRetry_AdServiceThrows_StagesNothing()
        {
            _ads.ShowRewardedAsync(Arg.Any<AdPlacement>(), Arg.Any<CancellationToken>())
                .Throws(new InvalidOperationException("sdk down"));

            Assert.IsFalse(TryBonusRetry(7));
            _staging.DidNotReceiveWithAnyArgs().PrepareRetry(default, default);
        }

        [Test]
        public void TryBonusRetry_NoBonusCardLeft_RefusesWithoutAnAd()
        {
            _bonusCards.CanDealBonus.Returns(false);

            Assert.IsFalse(TryBonusRetry(7));
            _ads.DidNotReceiveWithAnyArgs().ShowRewardedAsync(default, default);
        }

        [Test]
        public void TryRetry_AdCancelledMidFlight_StagesNothing()
        {
            _retryState.RetriesUsed.Returns(2);
            _ads.ShowRewardedAsync(Arg.Any<AdPlacement>(), Arg.Any<CancellationToken>())
                .Throws(new OperationCanceledException());

            Assert.IsFalse(TryRetry(7), "a cancelled ad reads as skipped, not as no fill");
            _staging.DidNotReceiveWithAnyArgs().PrepareRetry(default, default);
        }

        [Test]
        public void TryRetry_ScreenMovesOnWhileTheAdPlays_StagesNothing()
        {
            _retryState.RetriesUsed.Returns(2);
            using var cts = new CancellationTokenSource();
            _ads.ShowRewardedAsync(Arg.Any<AdPlacement>(), Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    cts.Cancel();
                    return UniTask.FromResult(AdResult.Watched);
                });

            var staged = _offers.TryRetryAsync(7, cts.Token).GetAwaiter().GetResult();

            Assert.IsFalse(staged, "a watched ad can't stage a retry for a screen that is gone");
            _staging.DidNotReceiveWithAnyArgs().PrepareRetry(default, default);
        }

        [Test]
        public void TryRetry_FreeRetryCancelledBeforeStaging_StagesNothing()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var staged = _offers.TryRetryAsync(7, cts.Token).GetAwaiter().GetResult();

            Assert.IsFalse(staged);
            _ads.DidNotReceiveWithAnyArgs().ShowRewardedAsync(default, default);
            _staging.DidNotReceiveWithAnyArgs().PrepareRetry(default, default);
        }

        [Test]
        public void TryBonusRetry_AdCancelledMidFlight_StagesNothing()
        {
            _ads.ShowRewardedAsync(Arg.Any<AdPlacement>(), Arg.Any<CancellationToken>())
                .Throws(new OperationCanceledException());

            Assert.IsFalse(TryBonusRetry(7));
            _staging.DidNotReceiveWithAnyArgs().PrepareRetry(default, default);
        }

        [Test]
        public void TryBonusRetry_ScreenMovesOnWhileTheAdPlays_StagesNothing()
        {
            using var cts = new CancellationTokenSource();
            _ads.ShowRewardedAsync(Arg.Any<AdPlacement>(), Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    cts.Cancel();
                    return UniTask.FromResult(AdResult.Watched);
                });

            var staged = _offers.TryBonusRetryAsync(7, cts.Token).GetAwaiter().GetResult();

            Assert.IsFalse(staged, "the reward must not be staged for a screen that is gone");
            _staging.DidNotReceiveWithAnyArgs().PrepareRetry(default, default);
        }

        [Test]
        public void TryBonusRetry_NoRetryLeft_RefusesWithoutAnAd()
        {
            RunOutOfRetries();

            Assert.IsFalse(TryBonusRetry(7));
            _ads.DidNotReceiveWithAnyArgs().ShowRewardedAsync(default, default);
            _staging.DidNotReceiveWithAnyArgs().PrepareRetry(default, default);
        }

        [Test]
        public void TryBonusRetry_Cancelled_StagesNothing()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var staged = _offers.TryBonusRetryAsync(7, cts.Token).GetAwaiter().GetResult();

            Assert.IsFalse(staged);
            _staging.DidNotReceiveWithAnyArgs().PrepareRetry(default, default);
        }

        [Test]
        public void TryBonusRetry_WithTheRealTracker_StagesTheBonusAndCountsTheRetry()
        {
            var config = Substitute.For<IRunConfig>();
            config.MaxRetries.Returns(0);
            config.RetriesBeforeAd.Returns(1);
            var tracker = new RetryTracker(config);
            var offers = new FailureOffers(tracker, tracker, config, _ads, _bonusCards);

            Assert.IsTrue(offers.TryBonusRetryAsync(6, CancellationToken.None).GetAwaiter().GetResult());
            Assert.AreEqual(6, tracker.RetryLevel);
            Assert.IsTrue(tracker.RetryWithBonusCard);

            tracker.ResetRun(0);

            Assert.IsFalse(tracker.RetryWithBonusCard);
            Assert.IsTrue(offers.Current.RetryNeedsAd, "the bonus retry used the free one");
            Assert.IsTrue(offers.TryRetryAsync(6, CancellationToken.None).GetAwaiter().GetResult());
            Assert.IsFalse(tracker.RetryWithBonusCard, "a plain retry after a bonus attempt carries no bonus");
        }

        [Test]
        public void NoAdService_CountsEveryAdAsWatched()
        {
            var result = new NoAdService().ShowRewardedAsync(AdPlacement.RetryGate, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.AreEqual(AdResult.Watched, result);
        }

        private bool TryRetry(int level)
        {
            return _offers.TryRetryAsync(level, CancellationToken.None).GetAwaiter().GetResult();
        }

        private bool TryBonusRetry(int level)
        {
            return _offers.TryBonusRetryAsync(level, CancellationToken.None).GetAwaiter().GetResult();
        }

        private void AdEnds(AdResult result)
        {
            _ads.ShowRewardedAsync(Arg.Any<AdPlacement>(), Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(result));
        }

        private void RunOutOfRetries()
        {
            _retryState.IsUnlimited.Returns(false);
            _retryState.RetriesRemaining.Returns(0);
            _retryState.RetriesUsed.Returns(3);
        }
    }
}
