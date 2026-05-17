using BalloonParty.Item;

namespace BalloonParty.Shared.Messages
{
    public readonly struct TransformCapturedMessage
    {
        public readonly TransformSnapshot Snapshot;

        public TransformCapturedMessage(TransformSnapshot snapshot)
        {
            Snapshot = snapshot;
        }
    }
}
