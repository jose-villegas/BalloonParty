using UniRx;

namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     The launch screen's Continue, handed to the game scope. The two scopes don't share a message bus, so
    ///     the request waits here until the game takes it, even if the game finishes loading after the tap.
    /// </summary>
    internal interface IRunResumeRequest
    {
        IReadOnlyReactiveProperty<bool> Pending { get; }

        void Request();

        /// <summary>Takes the pending request; false when there was none.</summary>
        bool Consume();
    }
}
