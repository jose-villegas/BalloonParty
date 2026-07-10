namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Marks a resettable whose work is board population/teardown (clearing or respawning actors), as
    ///     opposed to run state (score, level, health). <see cref="RunController.RestartRun" /> can skip these
    ///     so a transition cinematic drives the board swap itself while the run state still resets.
    /// </summary>
    internal interface IBoardResettable : IRunResettable
    {
    }
}
