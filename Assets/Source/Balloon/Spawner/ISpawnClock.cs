namespace BalloonParty.Balloon.Spawner
{
    internal interface ISpawnClock
    {
        /// <summary>0 just after a line lands (or as a level's grace starts), rising to 1 as the next line arrives.</summary>
        float NextLineProgress { get; }

        /// <summary>Seconds of play on the current level, counted under the clock's holds (Game screen, Playing, not
        /// paused); 0 again at each level start and run reset.</summary>
        float SecondsOnLevel { get; }

        /// <summary>Seconds left in the breather a big clear earned, 0 outside one: the release the clock gives
        /// before the pressure builds again.</summary>
        float BreatherSecondsLeft { get; }
    }
}
