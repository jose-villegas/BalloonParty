namespace BalloonParty.Game.HeadsUp
{
    /// <summary>Where routers send heads-ups. Whether one shows is the queue's call, by the style's rules.</summary>
    internal interface IHeadsUps
    {
        /// <summary>The line it shows on or rewrites, or <see cref="HeadsUpHandle.None" /> if it was dropped.</summary>
        HeadsUpHandle Request(in HeadsUpRequest request);

        /// <summary>Ends a held heads-up (the cannon's ready once it fires). A stale handle does nothing.</summary>
        void Release(HeadsUpHandle handle);
    }
}
