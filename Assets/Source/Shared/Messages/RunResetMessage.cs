namespace BalloonParty.Shared.Messages
{
    /// <summary>Broadcast after every <c>IRunResettable</c> has reset, for views that can't reset reactively (e.g. score progress bars, loaded projectile).</summary>
    public readonly struct RunResetMessage
    {
    }
}
