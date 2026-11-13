using System.Threading;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Game.Run
{
    /// <summary>What the game-over screen may offer right now.</summary>
    internal readonly struct FailureOffer
    {
        /// <summary>Retry at the level reached, with the same seeded mix.</summary>
        public readonly bool CanRetry;

        /// <summary>The retry costs a rewarded ad: the run has used its free retries.</summary>
        public readonly bool RetryNeedsAd;

        /// <summary>Retries left this run; -1 when they're unlimited.</summary>
        public readonly int RetriesLeft;

        /// <summary>
        ///     The retries this run gets without an ad: the free ones, or with no ad gate, every retry of a capped
        ///     run. 0 when there's nothing to count (no gate and no cap).
        /// </summary>
        public readonly int FreeRetries;

        /// <summary>How many of <see cref="FreeRetries" /> are still to use.</summary>
        public readonly int FreeRetriesLeft;

        /// <summary>
        ///     A rewarded ad can buy the retry with a bonus card for that attempt. Its ad also pays a retry
        ///     that <see cref="RetryNeedsAd" />, so one ad never costs two.
        /// </summary>
        public readonly bool CanBonusRetry;

        public FailureOffer(
            bool canRetry, bool retryNeedsAd, int retriesLeft, bool canBonusRetry, int freeRetries = 0,
            int freeRetriesLeft = 0)
        {
            CanRetry = canRetry;
            RetryNeedsAd = canRetry && retryNeedsAd;
            RetriesLeft = retriesLeft;
            CanBonusRetry = canRetry && canBonusRetry;
            FreeRetries = freeRetries < 0 ? 0 : freeRetries;
            FreeRetriesLeft = freeRetriesLeft < 0 ? 0 : freeRetriesLeft > FreeRetries ? FreeRetries : freeRetriesLeft;
        }
    }

    /// <summary>
    ///     Decides what a loss offers the player: a plain retry, one gated behind an ad, and a retry with a
    ///     bonus card bought with a rewarded ad. The one place the failure flow meets ads, so the screen
    ///     never talks to the ad SDK.
    /// </summary>
    internal interface IFailureOffers
    {
        FailureOffer Current { get; }

        /// <summary>
        ///     Settles the retry's cost (the ad, when <see cref="FailureOffer.RetryNeedsAd" />) and, once it's
        ///     paid, stages the retry at <paramref name="level" />. True when the retry is staged; nothing is
        ///     staged on false or when <paramref name="ct" /> is cancelled.
        /// </summary>
        UniTask<bool> TryRetryAsync(int level, CancellationToken ct);

        /// <summary>
        ///     Shows the rewarded ad and, once it's watched through, stages the retry at
        ///     <paramref name="level" /> with a bonus card. A reward needs the ad watched: a skip, no fill or an
        ///     SDK error stages nothing, and the plain retry is still on offer.
        /// </summary>
        UniTask<bool> TryBonusRetryAsync(int level, CancellationToken ct);
    }
}
