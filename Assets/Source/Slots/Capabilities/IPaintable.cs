using UniRx;

namespace BalloonParty.Slots.Capabilities
{
    /// <summary>An actor whose color identity can be overwritten.</summary>
    public interface IPaintable : IHasColor
    {
        new ReactiveProperty<string> Color { get; }
    }
}
