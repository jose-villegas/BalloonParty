using System.Threading;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Shared.GameState
{
    internal class NavigationReadyGate : IReadyGate
    {
        private readonly NavigationState _targetState;

        internal NavigationReadyGate(NavigationState targetState)
        {
            _targetState = targetState;
        }

        public UniTask WaitAsync(CancellationToken ct) =>
            UniTask.WaitUntil(
                () => Navigation.Current.Value == _targetState,
                cancellationToken: ct);
    }
}

