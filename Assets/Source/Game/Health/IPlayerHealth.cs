using UniRx;

namespace BalloonParty.Game.Health
{
    /// <summary>
    ///     Read-only view of the player's hit-point pool, for consumers that only observe the count
    ///     (the heart HUD, the danger signal) rather than mutating it.
    /// </summary>
    internal interface IPlayerHealth
    {
        IReadOnlyReactiveProperty<int> Current { get; }
    }
}
