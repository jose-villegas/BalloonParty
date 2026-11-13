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
    ///     the retry; only skipping one does. The bonus retry is a reward, so it needs its ad watched.
    /// </summary>
    internal sealed class FailureOffers : IFailureOffers
    {
        private readonly IRetryState _retryState;
        private readonly IRetryStaging _staging;
        private readonly IRunConfig _config;
        private readonly IAdService _ads;
        private readonly IBonusCardSource _bonusCards;

        public FailureOffer Current
        {
            get
            {
                var unlimited = _retryState.IsUnlimited;
                var canRetry = unlimited || _retryState.RetriesRemaining > 0;
                var freeRetries = _config.RetriesBeforeAd;
                var used = _retryState.RetriesUsed;
                var needsAd = freeRetries > 0 && used >= freeRetries;
                var free = FreeRetries(freeRetries, unlimited, used);
                return new FailureOffer(canRetry, needsAd, unlimited ? -1 : _retryState.RetriesRemaining,
                    _bonusCards.CanDealBonus, free, free - used);
            }
        }

        public FailureOffers(IRetryState retryState, IRetryStaging staging, IRunConfig config, IAdService ads,
            IBonusCardSource bonusCards)
        {
            _retryState = retryState;
            _staging = staging;
            _config = config;
            _ads = ads;
            _bonusCards = bonusCards;
        }

        public async UniTask<bool> TryRetryAsync(int level, CancellationToken ct)
        {
            var offer = Current;
            if (!offer.CanRetry)
            {
                return false;
            }

            if (offer.RetryNeedsAd && await ShowAdAsync(AdPlacement.RetryGate, ct) == AdResult.Skipped)
            {
                return false;
            }

            return Stage(level, withBonusCard: false, ct);
        }

        public async UniTask<bool> TryBonusRetryAsync(int level, CancellationToken ct)
        {
            if (!Current.CanBonusRetry || await ShowAdAsync(AdPlacement.BonusRetry, ct) != AdResult.Watched)
            {
                return false;
            }

            return Stage(level, withBonusCard: true, ct);
        }

        // A capped run never gets more free retries than it has retries at all.
        private int FreeRetries(int beforeAd, bool unlimited, int used)
        {
            var cap = unlimited ? int.MaxValue : used + _retryState.RetriesRemaining;
            if (beforeAd > 0)
            {
                return Math.Min(beforeAd, cap);
            }

            return unlimited ? 0 : cap;
        }

        private bool Stage(int level, bool withBonusCard, CancellationToken ct)
        {
            // The screen moved on while the ad played: a stale retry would hijack the next reset.
            if (ct.IsCancellationRequested)
            {
                return false;
            }

            _staging.PrepareRetry(level, withBonusCard);
            return true;
        }

        // A cancelled ad reads as skipped (nothing is staged after it anyway); a broken SDK reads as no ad.
        private async UniTask<AdResult> ShowAdAsync(AdPlacement placement, CancellationToken ct)
        {
            try
            {
                return await _ads.ShowRewardedAsync(placement, ct);
            }
            catch (OperationCanceledException)
            {
                return AdResult.Skipped;
            }
            catch (Exception e)
            {
                Log.Warn("FailureOffers", $"{placement} ad failed: {e.Message}");
                return AdResult.Unavailable;
            }
        }
    }
}
