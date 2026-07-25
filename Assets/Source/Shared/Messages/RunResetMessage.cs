namespace BalloonParty.Shared.Messages
{
    /// <summary>Broadcast after every <c>IRunResettable</c> has reset, for views that can't reset reactively (e.g. score progress bars, loaded projectile).</summary>
    public readonly struct RunResetMessage
    {
        /// <summary>True when the board was reset in the same pass (an immediate restart); false when a
        /// transition cinematic owns the board swap and signals completion via <c>RunRestartCompletedMessage</c>.</summary>
        public readonly bool BoardReset;

        public RunResetMessage(bool boardReset)
        {
            BoardReset = boardReset;
        }
    }
}
