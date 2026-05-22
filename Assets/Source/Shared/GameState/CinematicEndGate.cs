using System.Threading;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Shared.GameState
{
    internal class CinematicEndGate : IReadyGate
    {
        public UniTask WaitAsync(CancellationToken ct) =>
            UniTask.WaitUntil(
                () => !Cinematic.IsPlaying,
                cancellationToken: ct);
    }
}

