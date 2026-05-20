using BalloonParty.Configuration;
using BalloonParty.Slots;
using UniRx;

namespace BalloonParty.Balloon.Model
{
    public interface IWriteableBalloonModel : IWriteableDynamicSlotActor, IBalloonModel
    {
        new ReactiveProperty<ItemType> Item { get; }
    }
}
