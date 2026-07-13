using UniRx;
using UnityEngine;

namespace BalloonParty.Shared.GameState
{
    /// <summary>Tracks whether a cinematic sequence is playing; static so any system can query without DI wiring.</summary>
    internal static class Cinematic
    {
        private static readonly ReactiveProperty<CinematicState> _state = new(CinematicState.None);

        public static IReadOnlyReactiveProperty<CinematicState> Current => _state;

        public static bool IsPlaying => _state.Value != CinematicState.None;

        public static void Begin(CinematicState state)
        {
            _state.Value = state;
        }

        public static void End()
        {
            _state.Value = CinematicState.None;
        }

        // Enter Play Mode Options can disable domain reload, which keeps this static ReactiveProperty (and its
        // last value) alive between play sessions — reset to the initial state on each play start.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _state.Value = CinematicState.None;
        }
    }
}
