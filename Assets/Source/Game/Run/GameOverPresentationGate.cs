using System.Threading;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Holds the GameOver screen shut until the loss cinematic finishes. Starts closed and is only
    ///     opened by the cinematic producer, so the screen always waits for it — even on paths where no
    ///     beat plays, the producer still opens it, so the reveal never soft-locks. Deliberately a plain
    ///     class, not an <c>IReadyGate</c>: the producer drives it through <see cref="Arm" />/<see cref="Open" />
    ///     (not on that interface), and both sides inject it by concrete type.
    /// </summary>
    internal sealed class GameOverPresentationGate
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
