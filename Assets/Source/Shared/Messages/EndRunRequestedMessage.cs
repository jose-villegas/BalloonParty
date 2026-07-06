namespace BalloonParty.Shared.Messages
{
    /// <summary>Requests ending the current run; keeps loss triggers out of <c>RunController</c>'s <c>IRunResettable</c> dependency graph.</summary>
    public readonly struct EndRunRequestedMessage
    {
    }
}
