namespace BalloonParty.Game.Telemetry
{
    // See "Time tracking" in PLAN-GameplayTelemetry.md for the pause-gating rule per clock.
    internal enum TimerId
    {
        Gameplay,
        Ceremony,
        Wall,
        Hold
    }
}
