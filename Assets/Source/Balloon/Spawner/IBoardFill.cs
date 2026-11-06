namespace BalloonParty.Balloon.Spawner
{
    internal interface IBoardFill
    {
        /// <summary>
        ///     Balloons on the board against the level's starting fill: 0 = none left, 1 = at least as many
        ///     as the level started with.
        /// </summary>
        float StartingFill { get; }

        /// <summary>Share of the board's open slots (those no grid actor holds) that balloons fill, 0–1.</summary>
        float Occupancy { get; }
    }
}
