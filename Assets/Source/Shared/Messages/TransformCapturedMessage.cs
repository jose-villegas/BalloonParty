using BalloonParty.Item;
using BalloonParty.Slots.Actor;

namespace BalloonParty.Shared.Messages
{
    public readonly struct TransformCapturedMessage
    {
        public readonly ISlotActor Source;
        public readonly TransformSnapshot Snapshot;

        public TransformCapturedMessage(ISlotActor source, TransformSnapshot snapshot)
        {
            Source = source;
            Snapshot = snapshot;
        }
    }
}
