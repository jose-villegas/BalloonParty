namespace BalloonParty.Configuration.Balloons
{
    /// <summary>
    ///     How unbreakables gather into formations, hold them, pick the next shape, and call in more
    ///     unbreakables while left alone.
    /// </summary>
    public interface IUnbreakableFormationSettings
    {
        bool Enabled { get; }

        /// <summary>Fewest unbreakables on the board that form up.</summary>
        int MinMembers { get; }

        /// <summary>Most unbreakables in one formation; the rest roam, or form a formation of their own.</summary>
        int MaxMembers { get; }

        /// <summary>Waves a formation holds, counting the wave it formed in, before it changes shape.</summary>
        int HoldWaves { get; }

        /// <summary>Bottom rows a formation never uses, so spawn lines always have room to enter.</summary>
        int BottomClearance { get; }

        /// <summary>
        ///     Bottom rows a row formation (a horizontal line, an item cover) never uses, at least <see cref="BottomClearance" />, so a wall across
        ///     the board stands well above the cannon.
        /// </summary>
        int HorizontalBottomClearance { get; }

        /// <summary>A formation picks at random among this many of its least-travel spots.</summary>
        int SpotChoices { get; }

        float HorizontalWeight { get; }

        /// <summary>Pick weight of each of the two diagonals.</summary>
        float DiagonalWeight { get; }

        /// <summary>Pick weight of covering an item from below; only in the pick while an item is on the board.</summary>
        float ItemCoverWeight { get; }

        /// <summary>
        ///     Chance, at each wave start, that a formation which stood untouched through the last wave calls in
        ///     one more unbreakable.
        /// </summary>
        float ReinforcementChance { get; }

        /// <summary>Most called-in unbreakables on the board at once, on top of the level's own cap.</summary>
        int ReinforcementCeiling { get; }
    }
}
