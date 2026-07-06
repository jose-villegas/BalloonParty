namespace BalloonParty.Shared.GameState
{
    internal enum CinematicState
    {
        None,
        LevelUpPanIn,
        LevelUpRestore,

        // Unlike level-up states, does NOT block loss — game-over must fire at 0 HP while this plays.
        HeartDrain,

        // Appended (not inserted) so older serialized per-state arrays keep their indices.
        HeartDrainRestore,

        // Camera pans off-board during level repopulation, then snaps back once the reveal is ready.
        LevelAscend
    }
}
