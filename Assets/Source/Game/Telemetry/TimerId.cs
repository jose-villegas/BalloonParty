namespace BalloonParty.Game.Telemetry
{
    // What each clock measures and why Ceremony and Wall are NOT pause-gated: see "What each timer
    // means" in this folder's README.
    internal enum TimerId
    {
        Gameplay,
        Ceremony,
        Wall,
        Hold
    }
}
