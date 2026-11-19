namespace BalloonParty.Slots.Actor
{
    /// <summary>
    ///     What the level being laid out says about where its mushrooms may stand, beyond the grid itself:
    ///     the seed its layout is rolled from, and the rows the balloon spawner keeps for itself.
    /// </summary>
    internal interface IMushroomPlacementSource
    {
        /// <summary>
        ///     The seed of the current level's layout: the same for a retry of the level, so the mushrooms stand
        ///     where they stood, and different from one run to the next.
        /// </summary>
        int LayoutSeed { get; }

        /// <summary>
        ///     Rows at the bottom of the grid the balloon spawner keeps free
        ///     (<c>IBalloonsConfiguration.PopSpawnFreeRows</c>). A mushroom stops rising balloons under it, as a
        ///     bush does, so one standing in these rows would close its column.
        /// </summary>
        int BottomRowsReserved { get; }
    }
}
