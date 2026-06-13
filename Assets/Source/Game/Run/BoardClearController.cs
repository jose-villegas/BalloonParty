using BalloonParty.Shared.Messages;
using MessagePipe;

namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Drives the board-clear stage of a run reset: broadcasts <see cref="BoardClearMessage"/>
    ///     so every actor returns its pooled view and vacates its grid slot. MessagePipe publishes
    ///     synchronously, so the board is empty by the time <see cref="ResetRun"/> returns.
    /// </summary>
    internal class BoardClearController : IRunResettable
    {
        private readonly IPublisher<BoardClearMessage> _publisher;

        public BoardClearController(IPublisher<BoardClearMessage> publisher)
        {
            _publisher = publisher;
        }

        public int ResetOrder => RunResetOrder.Board;

        public void ResetRun(int generation)
        {
            _publisher.Publish(default);
        }
    }
}
