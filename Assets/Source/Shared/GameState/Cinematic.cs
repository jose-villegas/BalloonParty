using System.Collections.Generic;
using UniRx;

namespace BalloonParty.Shared.GameState
{
    /// <summary>
    ///     Tracks whether a cinematic sequence is playing. Static so any
    ///     system can query or observe without DI wiring. Services that
    ///     implement <see cref="ICinematicAware"/> are notified automatically
    ///     when the state changes.
    /// </summary>
    internal static class Cinematic
    {
        private static readonly ReactiveProperty<CinematicState> _state = new(CinematicState.None);
        private static readonly List<ICinematicAware> _listeners = new();

        public static IReadOnlyReactiveProperty<CinematicState> Current => _state;

        public static bool IsPlaying => _state.Value != CinematicState.None;

        public static void Register(ICinematicAware listener)
        {
            _listeners.Add(listener);
        }

        public static void Unregister(ICinematicAware listener)
        {
            _listeners.Remove(listener);
        }

        public static void Begin(CinematicState state)
        {
            _state.Value = state;

            for (var i = _listeners.Count - 1; i >= 0; i--)
            {
                _listeners[i].OnCinematicBegin(state);
            }
        }

        public static void End()
        {
            _state.Value = CinematicState.None;

            for (var i = _listeners.Count - 1; i >= 0; i--)
            {
                _listeners[i].OnCinematicEnd();
            }
        }
    }
}

