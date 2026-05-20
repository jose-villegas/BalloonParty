using UniRx;

namespace BalloonParty.Slots
{
    public interface IDynamicSlotActor : ISlotActor
    {
        IReadOnlyReactiveProperty<bool> IsStable { get; }
    }
}
