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

            _offers = new FailureOffers(_retryState, _staging, _config, _ads);
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

            _staging.Received(1).PrepareRetry(7);
            _ads.DidNotReceiveWithAnyArgs().ShowRewardedAsync(default, default);
        }

        [Test]
        public void TryRetry_GatedRetry_StagesItOnceTheAdIsWatched()
        {
            _retryState.RetriesUsed.Returns(2);

            Assert.IsTrue(TryRetry(7));

            _ads.Received(1).ShowRewardedAsync(AdPlacement.RetryGate, Arg.Any<CancellationToken>());
            _staging.Received(1).PrepareRetry(7);
        }

        [Test]
        public void TryRetry_AdSkipped_StagesNothing()
        {
            _retryState.RetriesUsed.Returns(2);
            AdEnds(AdResult.Skipped);

            Assert.IsFalse(TryRetry(7));
            _staging.DidNotReceiveWithAnyArgs().PrepareRetry(default);
        }

        [Test]
        public void TryRetry_AdUnavailable_GrantsTheRetry()
        {
            _retryState.RetriesUsed.Returns(2);
            AdEnds(AdResult.Unavailable);

            Assert.IsTrue(TryRetry(7), "no fill isn't the player's doing");
            _staging.Received(1).PrepareRetry(7);
        }

        [Test]
        public void TryRetry_AdServiceThrows_GrantsTheRetry()
        {
            _retryState.RetriesUsed.Returns(2);
            _ads.ShowRewardedAsync(Arg.Any<AdPlacement>(), Arg.Any<CancellationToken>())
                .Throws(new InvalidOperationException("sdk down"));

            Assert.IsTrue(TryRetry(7), "a broken SDK reads as no ad, not as a skip");
            _staging.Received(1).PrepareRetry(7);
        }

        [Test]
        public void TryRetry_Cancelled_StagesNothing()
        {
            _retryState.RetriesUsed.Returns(2);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var staged = _offers.TryRetryAsync(7, cts.Token).GetAwaiter().GetResult();

            Assert.IsFalse(staged, "a screen that moved on must not leave a retry staged for the next reset");
            _staging.DidNotReceiveWithAnyArgs().PrepareRetry(default);
        }

        [Test]
        public void TryRetry_NoRetryLeft_RefusesWithoutAnAd()
        {
            RunOutOfRetries();

            Assert.IsFalse(TryRetry(7));
            _ads.DidNotReceiveWithAnyArgs().ShowRewardedAsync(default, default);
            _staging.DidNotReceiveWithAnyArgs().PrepareRetry(default);
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
