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
        ///     The board's toughs walk as one snake of their own, through the balloons in its way, keeping its order.
        ///     <see cref="ToughSnakeMinMembers" /> toughs on the board start it, with every other tough lined up behind
        ///     it at once; a tough that arrives later joins at its tail. Only while <see cref="Enabled" />.
        /// </summary>
        bool ToughSnakes { get; }

        /// <summary>Fewest toughs (and toughers) on the board that start the tough snake.</summary>
        int ToughSnakeMinMembers { get; }

        /// <summary>Fewest unbreakables on the board that form up.</summary>
        int MinMembers { get; }

        /// <summary>Most unbreakables in one formation; the rest roam, or form a formation of their own.</summary>
        int MaxMembers { get; }

        /// <summary>
        ///     Seconds of play between the unbreakables' beats. An unbreakable snake steps
        ///     <see cref="SnakeStepsPerBeat" /> times a beat; every
        ///     <see cref="BeatsPerCycle" /> beats make a cycle, the clock formations form, hold and change shape on and
        ///     a lone unbreakable moves on.
        /// </summary>
        float BeatSeconds { get; }

        /// <summary>Beats in one cycle.</summary>
        int BeatsPerCycle { get; }

        /// <summary>
        ///     Steps an unbreakable snake takes each beat, evenly spaced: 2 steps on the beat and the off-beat.
        /// </summary>
        int SnakeStepsPerBeat { get; }

        /// <summary>Cycles a snake walks, counting the cycle it formed on, before it changes shape.</summary>
        int SnakeHoldCycles { get; }

        /// <summary>Cycles a horizontal or diagonal line holds, counting the cycle it formed on.</summary>
        int LineHoldCycles { get; }

        /// <summary>Cycles an item cover holds, counting the cycle it formed on.</summary>
        int ItemCoverHoldCycles { get; }

        /// <summary>
        ///     Cycles a lone unbreakable (in no formation) stays where it is before it may move again. Only one moves
        ///     alone per cycle.
        /// </summary>
        int LoneHoldCycles { get; }

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

        /// <summary>Seconds of play between the tough snake's steps, on a clock of its own apart from the beat.</summary>
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
        ///     Most unbreakable snakes on the board at once; a formation that would make one more picks another
        ///     shape. 0 sets no cap.
        /// </summary>
        int MaxUnbreakableSnakes { get; }

        /// <summary>
        ///     Chance that each member of a formation changing shape on its cycle leaves it and flies to an
        ///     unbreakable snake's tail instead, while that snake has room (<see cref="MaxMembers" />): snakes grow
        ///     from the other formations rather than new ones forming.
        /// </summary>
        float JoinSnakeChance { get; }

        /// <summary>
        ///     Chance, at each wave start, that a formation which stood untouched through the last wave calls in
        ///     one more unbreakable.
        /// </summary>
        float ReinforcementChance { get; }

        /// <summary>Most called-in unbreakables on the board at once, on top of the level's own cap.</summary>
        int ReinforcementCeiling { get; }
    }
}
