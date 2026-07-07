using UniRx;

namespace BalloonParty.Slots.Capabilities
{
    /// <summary>An actor that can act as a colour wildcard — scores into every allowed colour and carries the streak instead of breaking it.</summary>
    public interface IHasRainbowMode
    {
        IReadOnlyReactiveProperty<bool> IsRainbow { get; }
    }
}
