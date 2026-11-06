using UniRx;

namespace BalloonParty.Game.Flight
{
    /// <summary>
    ///     The shot boundary, owned in one place instead of re-derived by every consumer. Per-ball
    ///     questions go to <c>IProjectileRegistry</c>; this answers the world-wide one.
    /// </summary>
    internal interface IFlightScope
    {
        /// <summary>
        ///     Some shot is in [Fired, Destroyed). What ducking and time-scale effects gate on. There is
        ///     no "some shot is loaded" window: the thrower holds a ball through all of play, so it would
        ///     always be true.
        /// </summary>
        IReadOnlyReactiveProperty<bool> IsAirborne { get; }
    }
}
