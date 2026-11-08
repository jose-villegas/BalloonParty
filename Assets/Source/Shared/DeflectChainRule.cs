namespace BalloonParty.Shared
{
    /// <summary>
    ///     When a run of deflects pays the ball a shield: a ball that banks off tough balloons again and again
    ///     without touching a wall earns a bounce every <see cref="IRunConfig.DeflectChainShieldEvery" />
    ///     deflects in a row.
    /// </summary>
    internal static class DeflectChainRule
    {
        /// <summary>The deflect count from which a chain is worth announcing.</summary>
        internal const int MinChain = 2;

        /// <summary>Whether the <paramref name="chain" />th deflect in a row pays a shield; 0 or less = never.</summary>
        internal static bool GrantsShield(int chain, int every)
        {
            return every > 0 && chain > 0 && chain % every == 0;
        }
    }
}
