namespace BalloonParty.Shared.Messages
{
    /// <summary>Published when the doomed moment ends — the shot died, or was reprieved (a moving
    /// balloon reopened a shield source in its path). Pairs one-to-one with
    /// <see cref="ProjectileDoomedStartedMessage"/> so reactors can release their claims.</summary>
    internal readonly struct ProjectileDoomedEndedMessage
    {
    }
}
