namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     A request to end the current run, raised by a loss trigger (e.g. the player's HP
    ///     reaching zero). <c>RunController</c> subscribes and routes it through its
    ///     <c>EndRun</c> seam — which still no-ops outside <c>Game</c> / during a cinematic.
    ///     Using a message instead of a direct call keeps loss triggers out of the
    ///     <c>IRunResettable</c> dependency graph that <c>RunController</c> already owns.
    /// </summary>
    public readonly struct EndRunRequestedMessage
    {
    }
}
