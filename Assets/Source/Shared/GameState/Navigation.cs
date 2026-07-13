using UniRx;
using UnityEngine;

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

        // Enter Play Mode Options can disable domain reload, which keeps this static ReactiveProperty (and its
        // last value) alive between play sessions — reset to the initial state on each play start.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _state.Value = NavigationState.Launch;
        }
    }
}
