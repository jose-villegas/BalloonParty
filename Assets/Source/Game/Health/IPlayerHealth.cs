using UniRx;

namespace BalloonParty.Game.Health
{
    /// <summary>Read-only view of the player's hit-point pool.</summary>
    internal interface IPlayerHealth
    {
        IReadOnlyReactiveProperty<int> Current { get; }
    }
}
