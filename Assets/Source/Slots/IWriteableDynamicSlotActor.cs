using UniRx;

namespace BalloonParty.Slots
{
    public interface IWriteableDynamicSlotActor : IDynamicSlotActor, IWriteableSlotActor
    {
        new ReactiveProperty<bool> IsStable { get; }
    }
}
