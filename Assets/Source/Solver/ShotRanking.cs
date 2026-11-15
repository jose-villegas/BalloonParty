namespace BalloonParty.Solver
{
    /// <summary>
    ///     Orders simulated shots the way a player who knows the rewards would. A golden balloon's free card
    ///     outweighs any score, and a heart balloon outweighs score while its heart counts: up to the hearts
    ///     missing, or any number on a purple level, where each is an extra one. A disco ball's rainbow board
    ///     comes next: it pays the shots after this one, so a heart that counts now goes first. Score decides
    ///     the rest. No reward balloon scores, so ranking on score alone never aims at one.
    /// </summary>
    internal static class ShotRanking
    {
        /// <param name="heartsWanted">How many heart pops would add a heart right now (<see cref="HeartsWanted" />).</param>
        internal static bool IsBetter(in ShotSimulationResult candidate, in ShotSimulationResult best, int heartsWanted)
        {
            if (candidate.GoldenPops != best.GoldenPops)
            {
                return candidate.GoldenPops > best.GoldenPops;
            }

            var candidateHearts = candidate.HeartPops < heartsWanted ? candidate.HeartPops : heartsWanted;
            var bestHearts = best.HeartPops < heartsWanted ? best.HeartPops : heartsWanted;
            if (candidateHearts != bestHearts)
            {
                return candidateHearts > bestHearts;
            }

            if (candidate.DiscoPops != best.DiscoPops)
            {
                return candidate.DiscoPops > best.DiscoPops;
            }

            return candidate.RawScore > best.RawScore;
        }

        internal static int HeartsWanted(int hearts, int maxHearts, bool purpleLevel)
        {
            if (purpleLevel)
            {
                return int.MaxValue;
            }

            return hearts < maxHearts ? maxHearts - hearts : 0;
        }
    }
}
