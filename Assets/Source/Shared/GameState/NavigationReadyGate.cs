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

        public UniTask WaitAsync(CancellationToken ct)
        {
            // Short-circuit when already there — no needless frame delay, and lets callers await synchronously.
            if (_navigation.Current.Value == _targetState)
            {
                return UniTask.CompletedTask;
            }

            return UniTask.WaitUntil(
                () => _navigation.Current.Value == _targetState,
                cancellationToken: ct);
        }
    }
}
