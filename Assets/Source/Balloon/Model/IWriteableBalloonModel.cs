using BalloonParty.Slots.Actor;

namespace BalloonParty.Balloon.Model
{
    public interface IWriteableBalloonModel : IWriteableDynamicSlotActor, IBalloonModel
    {
        new int RegistryHandle { get; set; }
    }
}
