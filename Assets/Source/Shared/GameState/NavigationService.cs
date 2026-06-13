using UniRx;

namespace BalloonParty.Shared.GameState
{
    internal class NavigationService : INavigation
    {
        public IReadOnlyReactiveProperty<NavigationState> Current => Navigation.Current;

        public void TransitionTo(NavigationState state)
        {
            Navigation.TransitionTo(state);
        }
    }
}
