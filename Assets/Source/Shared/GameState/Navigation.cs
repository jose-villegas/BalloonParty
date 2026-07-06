using UniRx;

namespace BalloonParty.Shared.GameState
{
    /// <summary>App-wide navigation state; static so both scenes can access it without cross-scene DI wiring.</summary>
    internal static class Navigation
    {
        private static readonly ReactiveProperty<NavigationState> _state = new(NavigationState.Launch);

        public static IReadOnlyReactiveProperty<NavigationState> Current => _state;

        public static void TransitionTo(NavigationState state)
        {
            _state.Value = state;
        }
    }
}
