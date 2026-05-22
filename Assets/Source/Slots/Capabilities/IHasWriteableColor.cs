using UniRx;

namespace BalloonParty.Slots.Capabilities
{
    public interface IHasWriteableColor : IHasColor
    {
        new ReactiveProperty<string> Color { get; }
    }
}
