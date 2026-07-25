namespace BalloonParty.Slots.Actor
{
    /// <summary>Which shared balance-bias formula an <see cref="IBalanceInfluence.WeightBias" /> override applies.</summary>
    internal enum BalanceBiasKind
    {
        None,
        ColorDiagonal,
        Line,
        Clump
    }
}
