using UniRx;

namespace BalloonParty.Slots
{
    public interface IHasColor
    {
        IReadOnlyReactiveProperty<string> Color { get; }
    }
}
