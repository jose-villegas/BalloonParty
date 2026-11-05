using BalloonParty.Balloon.Type;

namespace BalloonParty.Game.Flight
{
    /// <summary>
    ///     What happened during one shot's flight, from its load until its death. Read by audio (as pitch
    ///     steps) and by metrics (as gameplay facts) — one owner, so the two can never disagree about how
    ///     many Toughs died this shot. Get one through <see cref="IFlightStatsSource" />.
    /// </summary>
    internal interface IFlightStats
    {
        int PierceDischarges { get; }

        int WallBounces { get; }

        int PopsOf(BalloonType type);

        int DeflectsOf(BalloonType type);
    }
}
