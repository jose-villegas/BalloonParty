using System.Threading;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Game.Run
{
    /// <summary>Where in the game an ad is asked for, so an SDK can report and cap them per placement.</summary>
    internal enum AdPlacement
    {
        /// <summary>A retry past the run's free ones (<see cref="Shared.IRunConfig.RetriesBeforeAd" />).</summary>
        RetryGate
    }

    /// <summary>How a rewarded ad ended.</summary>
    internal enum AdResult
    {
        /// <summary>The player watched it through: the reward is earned.</summary>
        Watched,

        /// <summary>The player closed it early: no reward.</summary>
        Skipped,

        /// <summary>No ad could be shown (no fill, not loaded, SDK error). Not the player's doing.</summary>
        Unavailable
    }

    /// <summary>
    ///     The seam an ad SDK plugs into. The game shows ads only on failure, so the only call is a
    ///     rewarded ad the player chose to watch. It reports what happened; what that costs the player is
    ///     <see cref="IFailureOffers" />'s call. <see cref="NoAdService" /> stands in until an SDK lands.
    /// </summary>
    internal interface IAdService
    {
        UniTask<AdResult> ShowRewardedAsync(AdPlacement placement, CancellationToken ct);
    }
}
