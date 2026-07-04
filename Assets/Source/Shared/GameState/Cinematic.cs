using UniRx;

namespace BalloonParty.Shared.GameState
{
    /// <summary>
    ///     Tracks whether a cinematic sequence is playing. Static so any
    ///     system can query or observe without DI wiring.
    /// </summary>
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
    }
}
