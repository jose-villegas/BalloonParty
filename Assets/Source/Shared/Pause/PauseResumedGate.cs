using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using VContainer;

namespace BalloonParty.Shared.Pause
{
    /// <summary>Resolves in the same frame as the matching <see cref="PauseService.Resume"/> call.</summary>
    internal sealed class PauseResumedGate : IReadyGate
    {
        private readonly PauseSource _awaitedSource;
        private readonly PauseService _pauseService;
        private readonly ISubscriber<ResumedMessage> _resumedSubscriber;

        [Inject]
        internal PauseResumedGate(
            PauseSource awaitedSource,
            PauseService pauseService,
            ISubscriber<ResumedMessage> resumedSubscriber)
        {
            _awaitedSource = awaitedSource;
            _pauseService = pauseService;
            _resumedSubscriber = resumedSubscriber;
        }

        public UniTask WaitAsync(CancellationToken ct)
        {
            if (!_pauseService.IsPaused(_awaitedSource))
            {
                return UniTask.CompletedTask;
            }

            var tcs = new UniTaskCompletionSource();

            // Boxed so the lambda can dispose it without a modified-capture warning.
            var holder = new IDisposable[1];
            holder[0] = _resumedSubscriber.Subscribe(msg =>
                OnResumed(msg, holder, tcs));

            ct.Register(() =>
            {
                holder[0].Dispose();
                tcs.TrySetCanceled();
            });

            return tcs.Task;
        }

        private void OnResumed(ResumedMessage msg, IDisposable[] holder, UniTaskCompletionSource tcs)
        {
            if (msg.Source != _awaitedSource)
            {
                return;
            }

            holder[0].Dispose();
            tcs.TrySetResult();
        }
    }
}
