using UniRx;

namespace BalloonParty.Game.Level
{
    /// <summary>
    ///     Read surface of the player's progression through levels.
    /// </summary>
    internal interface ILevelProgress
    {
        IReadOnlyReactiveProperty<int> Level { get; }

        /// <summary>The level-up ceremony phase — the single guard everything reads instead of inferring
        /// from nav/pause. The Ascent watches this for its <see cref="LevelUpPhase.Transitioning" /> cue.</summary>
        IReadOnlyReactiveProperty<LevelUpPhase> Phase { get; }

        int GetRequiredPoints();
        int GetProgress(string colorName);

        /// <summary>Excess points banked for <paramref name="color" /> this run — the amount
        /// <see cref="ClaimProgress" /> couldn't grant because the level cap was already hit. Run-scoped and
        /// accumulating across levels; reserved for a future per-level currency system, unused in gameplay today.</summary>
        int ExcessPoints(string color);

        /// <summary>The run's total banked excess across every colour (see <see cref="ExcessPoints" />).</summary>
        int TotalExcessPoints();

        /// <summary>Projected (not confirmed) progress reaches the threshold for every allowed colour.</summary>
        bool WillLevelUp();

        /// <summary>Caps <paramref name="points" /> at the colour's remaining room this level (one level-up per burst);
        /// the remainder is banked as excess (see <see cref="ExcessPoints" />), not applied to progress.</summary>
        (int baseProgress, int granted) ClaimProgress(string color, int points);
    }
}
