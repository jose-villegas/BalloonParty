namespace BalloonParty.Balloon.Spawner
{
    internal interface ISpawnClock
    {
        /// <summary>0 just after a line lands (or as a level's grace starts), rising to 1 as the next line arrives.</summary>
        float NextLineProgress { get; }
    }
}
