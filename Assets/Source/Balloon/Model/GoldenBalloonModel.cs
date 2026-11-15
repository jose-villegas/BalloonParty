namespace BalloonParty.Balloon.Model
{
    /// <summary>
    ///     The golden reward balloon (PLAN-RewardBalloons): a <see cref="PlatedBalloonModel" /> whose pop pays a
    ///     free card (<c>RewardBalloonPayout</c>).
    /// </summary>
    internal sealed class GoldenBalloonModel : PlatedBalloonModel
    {
        internal GoldenBalloonModel(BalloonModelConfig config)
            : base(config)
        {
        }
    }
}
