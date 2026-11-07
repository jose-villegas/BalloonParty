namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Whether a retry could be dealt a bonus card right now. <see cref="IFailureOffers" /> asks before
    ///     offering the rewarded bonus retry, so the ad is never sold for a card that can't be dealt.
    /// </summary>
    internal interface IBonusCardSource
    {
        bool CanDealBonus { get; }
    }
}
