#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE
namespace BalloonParty.Cheats.Autoplay
{
    /// <summary>
    ///     What the autoplay presses on the game-over screen, as the Editor playtest bot's <c>--on-loss</c>
    ///     (<c>Tools/playtest/README.md</c>).
    /// </summary>
    internal enum AutoplayOnLoss
    {
        /// <summary>Nothing: the session ends at the first game over.</summary>
        Stop,

        /// <summary>Restart: a new run from level 1.</summary>
        Restart,

        /// <summary>Retry while the run has one, the free ones and then the ones behind an ad; Restart after.</summary>
        Retry,

        /// <summary>The bonus retry (a rewarded ad that buys a card) while it is offered; otherwise as Retry.</summary>
        Bonus,

        /// <summary>Retry while the run has a free one; the session ends at the first loss that would need an ad.</summary>
        Free
    }
}
#endif
