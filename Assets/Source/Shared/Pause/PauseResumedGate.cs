using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using VContainer;

namespace BalloonParty.Shared.Pause
{
    /// <summary>
    ///     Gate that resolves as soon as a specific <see cref="PauseSource"/> transitions
    ///     from paused to resumed. Subscribes reactively to <see cref="ResumedMessage"/>
    ///     via MessagePipe — no per-frame polling, resolves in the same frame as
    ///     the matching <see cref="PauseService.Resume"/> call.
    ///     If the source is already resumed when <see cref="WaitAsync"/> is called,
    ///     the task completes immediately.
    /// </summary>
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

            // Box the subscription so the lambda can dispose it without a modified-capture warning.
            var holder = new IDisposable[1];
            holder[0] = _resumedSubscriber.Subscribe(msg =>
            {
                if (msg.Source != _awaitedSource)
                {
                    return;
                }

                holder[0].Dispose();
                tcs.TrySetResult();
            });

            ct.Register(() =>
            {
                holder[0].Dispose();
                tcs.TrySetCanceled();
            });

            return tcs.Task;
        }
    }
}



