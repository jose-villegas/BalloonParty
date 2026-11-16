namespace BalloonParty.Balloon.Spawner
{
    /// <summary>
    ///     Starts the spawn clock's breather on demand, the one a big clear earns: the nuke's blast (PLAN-NukeBalloon)
    ///     grants it once the board is clear.
    /// </summary>
    internal interface IBreatherGrant
    {
        /// <summary>Starts the breather now, whatever the cooldown after the last; one already running starts over.</summary>
        void Grant();
    }
}
