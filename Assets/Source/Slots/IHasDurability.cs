using UniRx;

namespace BalloonParty.Slots
{
    public interface IHasDurability : IHitable
    {
        IReadOnlyReactiveProperty<int> HitsRemaining { get; }
    }
}
