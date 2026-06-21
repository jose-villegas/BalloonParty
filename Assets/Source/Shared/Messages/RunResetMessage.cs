namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     Broadcast by <c>RunController</c> after every <c>IRunResettable</c> has reset, when a run
    ///     restarts. The signal for views that hold per-run visual state but can't reset reactively
    ///     (their data isn't a <c>ReactiveProperty</c>) or live outside the reset graph's scope —
    ///     e.g. the score progress bars and the thrower's loaded projectile.
    /// </summary>
    public readonly struct RunResetMessage
    {
    }
}
