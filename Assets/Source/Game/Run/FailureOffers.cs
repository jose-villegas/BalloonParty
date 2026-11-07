using System;
using System.Threading;
using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Reads the run's retry state against <see cref="IRunConfig.RetriesBeforeAd" />: the first retries
    ///     are free, every one after needs a rewarded ad. An ad that can't be shown never costs the player
    ///     the retry; only skipping one does.
    /// </summary>
    internal sealed class FailureOffers : IFailureOffers
    {
        private readonly IRetryState _retryState;
        private readonly IRetryStaging _staging;
        private readonly IRunConfig _config;
        private readonly IAdService _ads;

        public FailureOffer Current
        {
            get
            {
                var unlimited = _retryState.IsUnlimited;
                var canRetry = unlimited || _retryState.RetriesRemaining > 0;
                var freeRetries = _config.RetriesBeforeAd;
                var needsAd = freeRetries > 0 && _retryState.RetriesUsed >= freeRetries;
                return new FailureOffer(canRetry, needsAd, unlimited ? -1 : _retryState.RetriesRemaining);
            }
        }

        public FailureOffers(IRetryState retryState, IRetryStaging staging, IRunConfig config, IAdService ads)
        {
            _retryState = retryState;
            _staging = staging;
            _config = config;
            _ads = ads;
        }

        public async UniTask<bool> TryRetryAsync(int level, CancellationToken ct)
        {
            var offer = Current;
            if (!offer.CanRetry)
            {
                return false;
            }

            if (offer.RetryNeedsAd && !await RetryAdPaidAsync(ct))
            {
                return false;
            }

            // The screen moved on while the ad played: a stale retry would hijack the next reset.
            if (ct.IsCancellationRequested)
            {
                return false;
            }

            _staging.PrepareRetry(level);
            return true;
        }

        private async UniTask<bool> RetryAdPaidAsync(CancellationToken ct)
        {
            AdResult result;
            try
            {
                result = await _ads.ShowRewardedAsync(AdPlacement.RetryGate, ct);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception e)
            {
                Log.Warn("FailureOffers", $"Retry ad failed, granting the retry: {e.Message}");
                result = AdResult.Unavailable;
            }

            return result != AdResult.Skipped;
        }
    }
}
