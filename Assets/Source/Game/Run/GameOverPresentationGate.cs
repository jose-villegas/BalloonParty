using System.Threading;
using BalloonParty.Shared;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Holds the GameOver screen shut until the loss cinematic finishes. Starts closed and is only
    ///     opened by the cinematic producer, so the screen always waits for it — even on paths where no
    ///     beat plays, the producer still opens it, so the reveal never soft-locks.
    ///     Implements <see cref="IReadyGate" /> for consistency with the other gates, but is registered and
    ///     injected by concrete type: the producer drives it via <see cref="Arm" />/<see cref="Open" />
    ///     (not on the interface), and binding it <c>.As&lt;IReadyGate&gt;()</c> would collide with the
    ///     scope's <c>NavigationReadyGate(Game)</c>.
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
