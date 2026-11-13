using UniRx;

namespace BalloonParty.Game.Health
{
    /// <summary>Read-only view of the player's hit-point pool.</summary>
    internal interface IPlayerHealth
    {
        IReadOnlyReactiveProperty<int> Current { get; }

        /// <summary>The most hearts the player can hold; a heal never goes past it.</summary>
        int Max { get; }
    }
}
