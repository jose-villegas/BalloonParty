using UniRx;

namespace BalloonParty.Slots.Capabilities
{
    public interface IHasColor
    {
        IReadOnlyReactiveProperty<string> Color { get; }
    }
}
