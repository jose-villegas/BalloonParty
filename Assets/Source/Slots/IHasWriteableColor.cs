using UniRx;

namespace BalloonParty.Slots
{
    public interface IHasWriteableColor : IHasColor
    {
        new ReactiveProperty<string> Color { get; }
    }
}

