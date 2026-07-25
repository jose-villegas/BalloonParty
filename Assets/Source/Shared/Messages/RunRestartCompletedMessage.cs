namespace BalloonParty.Shared.Messages
{
    /// <summary>The loss→restart transition cinematic has settled and the new board is ready to play, so a
    /// deferred-board restart (a <c>RunResetMessage</c> with <c>BoardReset</c> false) can load its
    /// projectile now rather than at the transition's start.</summary>
    public readonly struct RunRestartCompletedMessage
    {
    }
}
