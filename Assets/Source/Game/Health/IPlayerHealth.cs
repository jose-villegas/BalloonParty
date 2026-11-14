using UniRx;

namespace BalloonParty.Game.Health
{
    /// <summary>Read-only view of the player's hit-point pool.</summary>
    internal interface IPlayerHealth
    {
        IReadOnlyReactiveProperty<int> Current { get; }

        /// <summary>The cap a heal stops at; extra hearts (a purple level's heart balloons) take Current past it.</summary>
        int Max { get; }
    }
}
