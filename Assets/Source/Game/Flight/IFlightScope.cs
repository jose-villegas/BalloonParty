using UniRx;

namespace BalloonParty.Game.Flight
{
    /// <summary>
    ///     The per-shot boundary, owned in one place instead of re-derived by every consumer.
    ///     Two windows, because the systems that care want different ones: <see cref="IsLoaded" />
    ///     spans the whole shot including aiming, <see cref="IsAirborne" /> only the part where the
    ///     projectile is actually travelling.
    /// </summary>
    internal interface IFlightScope
    {
        /// <summary>Increments once per loaded projectile — a stable id for the shot in progress.</summary>
        int FlightIndex { get; }

        /// <summary>[Loaded, Destroyed). What per-flight counters are scoped to.</summary>
        IReadOnlyReactiveProperty<bool> IsLoaded { get; }

        /// <summary>
        ///     [Fired, Destroyed). What ducking and time-scale effects gate on — using
        ///     <see cref="IsLoaded" /> for those would hold the effect through aiming, i.e. permanently
        ///     between shots.
        /// </summary>
        IReadOnlyReactiveProperty<bool> IsAirborne { get; }
    }
}
