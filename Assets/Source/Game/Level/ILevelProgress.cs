using UniRx;

namespace BalloonParty.Game.Level
{
    /// <summary>
    ///     Read surface of the player's progression through levels — current level and per-colour
    ///     progress toward the next-level threshold — plus the scoring seam <see cref="ClaimProgress" />.
    ///     Owned by <c>LevelController</c>; consumers (HUD bars, the level-up cinematic, the run commit)
    ///     observe it, while <c>ScoreController</c> feeds it via <see cref="ClaimProgress" />.
    /// </summary>
    internal interface ILevelProgress
    {
        IReadOnlyReactiveProperty<int> Level { get; }

        int GetRequiredPoints();
        int GetProgress(string colorName);

        /// <summary>Projected (not confirmed) progress reaches the threshold for every allowed colour —
        /// lets the cinematic arm before in-flight trails from other colours confirm.</summary>
        bool WillLevelUp();

        /// <summary>
        ///     The scoring seam: caps <paramref name="points" /> at the colour's remaining room this
        ///     level (excess dropped — one level-up per burst), advances projected progress, and returns
        ///     the pre-claim base progress plus the granted amount so the caller can number the emitted
        ///     points. Returns <c>(0, 0)</c> for an unknown colour.
        /// </summary>
        (int baseProgress, int granted) ClaimProgress(string color, int points);
    }
}
