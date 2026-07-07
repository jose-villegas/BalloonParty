using UniRx;

namespace BalloonParty.Slots.Capabilities
{
    /// <summary>An actor whose rainbow-wildcard mode can be switched on (e.g. by Paint conversion).</summary>
    public interface IHasWriteableRainbowMode : IHasRainbowMode
    {
        new ReactiveProperty<bool> IsRainbow { get; }
    }
}
