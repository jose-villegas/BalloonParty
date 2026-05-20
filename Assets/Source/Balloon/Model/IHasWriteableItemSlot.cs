using BalloonParty.Configuration;
using BalloonParty.Slots;
using UniRx;

namespace BalloonParty.Balloon.Model
{
    public interface IHasWriteableItemSlot : IHasItemSlot
    {
        new ReactiveProperty<ItemType> Item { get; }
    }
}

