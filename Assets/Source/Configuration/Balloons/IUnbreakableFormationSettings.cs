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
        ///     <see cref="SnakeStepsPerBeat" /> times a beat. Every <see cref="BeatsPerCycle" /> beats make a cycle,
        ///     when a lone unbreakable may move; every <see cref="ReshapeEveryCycles" /> cycles the formations form
        ///     and change shape. It is the board's beat: bubble clusters merge on it too, a share of a beat after
        ///     each one (<see cref="IRipeBubbleSettings.MergeOffBeat" />).
        /// </summary>
        float BeatSeconds { get; }

        /// <summary>Beats in one cycle.</summary>
        int BeatsPerCycle { get; }

        /// <summary>
        ///     Cycles between the board's reshapes. On a reshape every formation changes shape at once and the free
        ///     unbreakables form up; in between every formation holds its shape, whatever it is.
        /// </summary>
        int ReshapeEveryCycles { get; }

        /// <summary>
        ///     Steps an unbreakable snake takes each beat, evenly spaced: 2 steps on the beat and the off-beat.
        /// </summary>
        int SnakeStepsPerBeat { get; }

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

        /// <summary>
        ///     The row the tough snake keeps to, counted from the top (0 is the top row). It climbs toward it, and on
        ///     it walks only left and right, so once its body is up the whole snake slides along it.
        /// </summary>
        int ToughSnakeRow { get; }

        /// <summary>A formation picks at random among this many of its least-travel spots.</summary>
        int SpotChoices { get; }

        /// <summary>
        ///     How strongly a cover prefers a gift balloon (golden, heart, disco ball, nuke) to a balloon carrying a
        ///     bomb, a snipe or a lightning, in slots of travel for each member. A spot's cost is the slots its
        ///     members travel to it, plus 3 for each balloon they move out of the way; a gift's spot counts as this
        ///     many slots cheaper for each member of the cover. So the gift is covered unless covering the item
        ///     carrier would save every member more than this many slots on average. 0: no preference, the cheaper
        ///     spot wins.
        /// </summary>
        float GiftCoverPreference { get; }

        float HorizontalWeight { get; }

        /// <summary>Pick weight of each of the two diagonals.</summary>
        float DiagonalWeight { get; }

        /// <summary>
        ///     Pick weight of a cover. It finds a spot only while a balloon carrying a bomb, a snipe or a lightning,
        ///     or a gift balloon, is on the board.
        /// </summary>
        float ItemCoverWeight { get; }

        /// <summary>Pick weight of a snake.</summary>
        float SnakeWeight { get; }

        /// <summary>Pick weight of a ring. It forms only from <see cref="RingMinMembers" /> unbreakables or more.</summary>
        float RingWeight { get; }

        /// <summary>
        ///     How many times likelier a ring is for a snake done walking: the snake coils into it, its head
        ///     leading. 1: no likelier than for any other formation.
        /// </summary>
        float RingWeightAfterSnake { get; }

        /// <summary>Fewest unbreakables a ring forms from.</summary>
        int RingMinMembers { get; }

        /// <summary>
        ///     Most unbreakables in a ring, five at most: of the six slots round its centre, one always stays open.
        /// </summary>
        int RingMaxMembers { get; }

        /// <summary>Cycles between the beats on which every ring and spinner turns the other way, all together. 0: never.</summary>
        int TurnReverseEveryCycles { get; }

        /// <summary>Pick weight of a spinner. It forms from three unbreakables or more.</summary>
        float SpinnerWeight { get; }

        /// <summary>
        ///     Most unbreakables in a spinner: 3 or 5, a line with as many on each side of the one it turns about.
        /// </summary>
        int SpinnerMaxMembers { get; }

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
