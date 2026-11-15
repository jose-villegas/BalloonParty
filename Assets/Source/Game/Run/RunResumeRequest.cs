using UniRx;

namespace BalloonParty.Game.Run
{
    internal sealed class RunResumeRequest : IRunResumeRequest
    {
        private readonly ReactiveProperty<bool> _pending = new(false);

        public IReadOnlyReactiveProperty<bool> Pending => _pending;

        public void Request()
        {
            _pending.Value = true;
        }

        public bool Consume()
        {
            if (!_pending.Value)
            {
                return false;
            }

            _pending.Value = false;
            return true;
        }
    }
}
