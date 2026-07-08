using BalloonParty.Shared.Messages;
using MessagePipe;

namespace BalloonParty.Game.Run
{
    /// <summary>MessagePipe publishes synchronously, so the board is empty by the time <see cref="ResetRun"/> returns.</summary>
    internal class BoardClearController : IRunResettable
    {
        private readonly IPublisher<BoardClearMessage> _publisher;

        public int ResetOrder => RunResetOrder.Board;

        public BoardClearController(IPublisher<BoardClearMessage> publisher)
        {
            _publisher = publisher;
        }

        public void ResetRun(int generation)
        {
            _publisher.Publish(default);
        }
    }
}
