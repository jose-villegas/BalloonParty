using BalloonParty.Balloon.Type;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Actor;

namespace BalloonParty.Balloon.Model
{
    public interface IBalloonModel : IDynamicSlotActor, IHitable, IHasScore, IHasNudge
    {
        BalloonType TypeName { get; }
    }
}
