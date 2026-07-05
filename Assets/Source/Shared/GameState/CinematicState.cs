namespace BalloonParty.Shared.GameState
{
    internal enum CinematicState
    {
        None,
        LevelUpPanIn,
        LevelUpRestore,

        // The overflow heart-drain beat. Unlike the level-up states it does NOT block loss — game-over
        // must be able to fire at 0 HP while it plays (it is the loss happening).
        HeartDrain,

        // The heart-drain's return to normal speed — its own state (a restore is just another camera-rig
        // segment), appended so older serialized per-state arrays keep their indices.
        HeartDrainRestore,

        // The level-transition ascent: camera pans away from the board while it's re-populated for the
        // new level, then snaps back once the reveal is ready.
        LevelAscend
    }
}
