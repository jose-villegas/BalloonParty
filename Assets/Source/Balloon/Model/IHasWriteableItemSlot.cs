using BalloonParty.Configuration;
using BalloonParty.Slots.Capabilities;
using UniRx;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Balloon.Model
{
    public interface IHasWriteableItemSlot : IHasItemSlot
    {
        new ReactiveProperty<ItemType> Item { get; }
    }
}
