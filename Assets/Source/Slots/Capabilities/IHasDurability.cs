using UniRx;

namespace BalloonParty.Slots.Capabilities
{
    public interface IHasDurability : IHitable
    {
        int MaxHitPoints { get; }
        IReadOnlyReactiveProperty<int> HitsRemaining { get; }
    }
}
