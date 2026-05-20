using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Slots;
using UniRx;

namespace BalloonParty.Balloon.Model
{
    public interface IBalloonModel : IDynamicSlotActor, IHitable, IHasColor, IHasScore, IHasNudge
    {
        BalloonType TypeName { get; }
        IReadOnlyReactiveProperty<ItemType> Item { get; }
        bool CanHoldItem { get; }
    }
}
