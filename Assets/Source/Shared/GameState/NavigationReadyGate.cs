using System.Threading;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Shared.GameState
{
    internal class NavigationReadyGate : IReadyGate
    {
        private readonly INavigation _navigation;
        private readonly NavigationState _targetState;

        internal NavigationReadyGate(INavigation navigation, NavigationState targetState)
        {
            _navigation = navigation;
            _targetState = targetState;
        }

        // Always defers at least one frame (never completes synchronously) — the GridSpawnerCoordinator
        // relies on that so the initial spawn runs after every entry point has started (e.g. the pacing
        // resolver), not synchronously during its own Start.
        public UniTask WaitAsync(CancellationToken ct)
        {
            return UniTask.WaitUntil(
                () => _navigation.Current.Value == _targetState,
                cancellationToken: ct);
        }
    }
}
