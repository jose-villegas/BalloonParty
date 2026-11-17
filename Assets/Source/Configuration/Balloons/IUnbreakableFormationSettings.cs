namespace BalloonParty.Configuration.Balloons
{
    /// <summary>
    ///     How unbreakables gather into formations, hold them, pick the next shape, and call in more
    ///     unbreakables while left alone.
    /// </summary>
    public interface IUnbreakableFormationSettings
    {
        bool Enabled { get; }

        /// <summary>
        ///     The board's toughs walk as one snake of their own, ordered head to tail by hits left, through the
        ///     balloons in its way. <see cref="MinMembers" /> toughs on the board start it from the toughest, with every
        ///     other landed tough lined up behind it at once; a tough that lands later joins at its tail. Only while <see cref="Enabled" />.
        /// </summary>
        bool ToughSnakes { get; }

        /// <summary>Fewest unbreakables on the board that form up.</summary>
        int MinMembers { get; }

        /// <summary>Most unbreakables in one formation; the rest roam, or form a formation of their own.</summary>
        int MaxMembers { get; }

        /// <summary>Waves a formation holds, counting the wave it formed in, before it changes shape.</summary>
        int HoldWaves { get; }

        /// <summary>Bottom rows a formation never uses, so spawn lines always have room to enter.</summary>
        int BottomClearance { get; }

        /// <summary>
        ///     Bottom rows a horizontal line never uses, at least <see cref="BottomClearance" />, so a wall across
        ///     the board stands well above the cannon.
        /// </summary>
        int HorizontalBottomClearance { get; }

        /// <summary>
        ///     Most unbreakables in a horizontal line, so a row never spans the board; the rest are free to
        ///     form their own.
        /// </summary>
        int HorizontalMaxMembers { get; }

        /// <summary>Most unbreakables in a snake, the line that walks the board.</summary>
        int SnakeMaxMembers { get; }

        /// <summary>
        ///     Balancer pulses between an unbreakable snake's steps, so each step lands on the board's own beat.
        /// </summary>
        int SnakeStepPulses { get; }

        /// <summary>Seconds of play between the tough snake's steps, on a clock of its own.</summary>
        float ToughSnakeStepSeconds { get; }

        /// <summary>A formation picks at random among this many of its least-travel spots.</summary>
        int SpotChoices { get; }

        float HorizontalWeight { get; }

        /// <summary>Pick weight of each of the two diagonals.</summary>
        float DiagonalWeight { get; }

        /// <summary>Pick weight of ringing an item; only in the pick while an item is on the board.</summary>
        float ItemCoverWeight { get; }

        /// <summary>Pick weight of a snake.</summary>
        float SnakeWeight { get; }

        /// <summary>
        ///     Added to the snake's weight, as a share of <see cref="SnakeWeight" />, for each unbreakable on the
        ///     board beyond <see cref="MinMembers" />: the more unbreakables, the likelier a snake.
        /// </summary>
        float SnakeWeightPerExtraUnbreakable { get; }

        /// <summary>
        ///     Chance, at each wave start, that a formation which stood untouched through the last wave calls in
        ///     one more unbreakable.
        /// </summary>
        float ReinforcementChance { get; }

        /// <summary>Most called-in unbreakables on the board at once, on top of the level's own cap.</summary>
        int ReinforcementCeiling { get; }
    }
}
