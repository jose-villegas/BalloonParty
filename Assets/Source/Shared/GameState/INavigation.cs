using UniRx;

namespace BalloonParty.Shared.GameState
{
    /// <summary>Injectable seam over the static <see cref="Navigation"/>, testable without global state.</summary>
    internal interface INavigation
    {
        IReadOnlyReactiveProperty<NavigationState> Current { get; }

        void TransitionTo(NavigationState state);
    }
}
