using System.Collections.Generic;
using System.Linq;
using MessagePipe;
using UniRx;
using VContainer;

namespace BalloonParty.Shared.Pause
{
    /// <summary>
    ///     Central coordinator for gameplay pause states. Callers broadcast intent
    ///     via <see cref="Pause"/> / <see cref="Resume"/>; systems react by subscribing
    ///     to <see cref="PausedMessage"/> / <see cref="ResumedMessage"/> through MessagePipe.
    ///     Reference-counted per source so nested pause calls don't prematurely resume.
    /// </summary>
    internal class PauseService
    {
        private readonly IPublisher<PausedMessage> _pausedPublisher;
        private readonly IPublisher<ResumedMessage> _resumedPublisher;
        private readonly Dictionary<PauseSource, int> _stack = new();
        private readonly ReactiveProperty<bool> _isAnyPaused = new(false);

        internal IReadOnlyReactiveProperty<bool> IsAnyPaused => _isAnyPaused;

        [Inject]
        internal PauseService(
            IPublisher<PausedMessage> pausedPublisher,
            IPublisher<ResumedMessage> resumedPublisher)
        {
            _pausedPublisher = pausedPublisher;
            _resumedPublisher = resumedPublisher;
        }

        internal bool IsPaused(PauseSource source)
        {
            return _stack.TryGetValue(source, out var count) && count > 0;
        }

        internal void Pause(PauseSource source)
        {
            _stack.TryGetValue(source, out var count);
            _stack[source] = count + 1;

            if (count == 0)
            {
                _isAnyPaused.Value = true;
                _pausedPublisher.Publish(new PausedMessage(source));
            }
        }

        internal void Resume(PauseSource source)
        {
            if (!_stack.TryGetValue(source, out var count) || count <= 0)
            {
                return;
            }

            _stack[source] = count - 1;

            if (count == 1)
            {
                _resumedPublisher.Publish(new ResumedMessage(source));
                _isAnyPaused.Value = _stack.Values.Any(v => v > 0);
            }
        }
    }
}
