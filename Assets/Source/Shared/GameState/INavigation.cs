using UniRx;

namespace BalloonParty.Shared.GameState
{
    /// <summary>
    ///     Injectable seam over the static <see cref="Navigation"/> so run-lifecycle
    ///     logic can be unit-tested without reaching into global state.
    /// </summary>
    internal interface INavigation
    {
        IReadOnlyReactiveProperty<NavigationState> Current { get; }

        void TransitionTo(NavigationState state);
    }
}
