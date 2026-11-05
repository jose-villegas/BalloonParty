using UniRx;

namespace BalloonParty.Game.Flight
{
    /// <summary>
    ///     The shot boundary, owned in one place instead of re-derived by every consumer.
    ///     Two windows, because the systems that care want different ones: <see cref="IsLoaded" />
    ///     spans whole shots including aiming, <see cref="IsAirborne" /> only the part where a
    ///     projectile is actually travelling. Both are true while ANY shot is in that window.
    /// </summary>
    internal interface IFlightScope
    {
        /// <summary>Increments once per loaded projectile — the id of the most recently loaded shot.</summary>
        int FlightIndex { get; }

        /// <summary>Some shot is in [Loaded, Destroyed). What per-flight counters are scoped to.</summary>
        IReadOnlyReactiveProperty<bool> IsLoaded { get; }

        /// <summary>
        ///     Some shot is in [Fired, Destroyed). What ducking and time-scale effects gate on — using
        ///     <see cref="IsLoaded" /> for those would hold the effect through aiming, i.e. permanently
        ///     between shots.
        /// </summary>
        IReadOnlyReactiveProperty<bool> IsAirborne { get; }
    }
}
