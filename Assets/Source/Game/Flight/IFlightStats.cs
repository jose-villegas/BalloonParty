using BalloonParty.Balloon.Type;

namespace BalloonParty.Game.Flight
{
    /// <summary>
    ///     What happened during the flight in progress. Zeroed when the next projectile loads.
    ///     Read by audio (as pitch steps) and by metrics (as gameplay facts) — one owner, so the two
    ///     can never disagree about how many Toughs died this shot.
    /// </summary>
    internal interface IFlightStats
    {
        int PierceDischarges { get; }

        int WallBounces { get; }

        int PopsOf(BalloonType type);

        int DeflectsOf(BalloonType type);
    }
}
