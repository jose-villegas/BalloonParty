namespace BalloonParty.Shared.GameState
{
    internal enum CinematicState
    {
        None,
        LevelUpPanIn,
        LevelUpRestore,

        // The overflow heart-drain beat. Unlike the level-up states it does NOT block loss — game-over
        // must be able to fire at 0 HP while it plays (it is the loss happening).
        HeartDrain
    }
}
