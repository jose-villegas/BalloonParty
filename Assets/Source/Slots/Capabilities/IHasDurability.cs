using UniRx;

namespace BalloonParty.Slots.Capabilities
{
    public interface IHasDurability : IHitable
    {
        IReadOnlyReactiveProperty<int> HitsRemaining { get; }
    }
}
