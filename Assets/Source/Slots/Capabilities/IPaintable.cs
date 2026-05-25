using UniRx;

namespace BalloonParty.Slots.Capabilities
{
    /// <summary>
    /// Declares that an actor's color identity can be overwritten.
    /// </summary>
    public interface IPaintable : IHasColor
    {
        new ReactiveProperty<string> Color { get; }
    }
}

