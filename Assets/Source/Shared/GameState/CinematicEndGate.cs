using System.Threading;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Shared.GameState
{
    internal class CinematicEndGate : IReadyGate
    {
        private readonly CinematicState _awaitedState;

        internal CinematicEndGate(CinematicState awaitedState) => _awaitedState = awaitedState;

        public UniTask WaitAsync(CancellationToken ct) =>
            UniTask.WaitUntil(
                () => Cinematic.Current.Value != _awaitedState,
                cancellationToken: ct);
    }
}
