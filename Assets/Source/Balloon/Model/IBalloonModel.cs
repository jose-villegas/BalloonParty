using BalloonParty.Balloon.Type;
using BalloonParty.Slots;

namespace BalloonParty.Balloon.Model
{
    public interface IBalloonModel : IDynamicSlotActor, IHitable, IHasScore, IHasNudge
    {
        BalloonType TypeName { get; }
    }
}
