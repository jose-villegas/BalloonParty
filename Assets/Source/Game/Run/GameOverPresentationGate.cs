using System.Threading;
using BalloonParty.Shared;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Holds the GameOver screen shut until the loss cinematic finishes. Starts closed and is only
    ///     opened by the cinematic producer, so the screen always waits for it — even on paths where no
    ///     beat plays, the producer still opens it, so the reveal never soft-locks.
    /// </summary>
    internal sealed class GameOverPresentationGate : IReadyGate
    {
        private bool _open;

        public void Arm()
        {
            _open = false;
        }

        public void Open()
        {
            _open = true;
        }

        public UniTask WaitAsync(CancellationToken ct)
        {
            return UniTask.WaitUntil(() => _open, cancellationToken: ct);
        }
    }
}
