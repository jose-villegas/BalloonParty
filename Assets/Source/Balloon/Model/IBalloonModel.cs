using BalloonParty.Balloon.Type;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Actor;

namespace BalloonParty.Balloon.Model
{
    public interface IBalloonModel : IDynamicSlotActor, IHitable, IHasNudge
    {
        BalloonType TypeName { get; }

        /// <summary>
        ///     Opaque slot index into <c>BalloonControllerRegistry</c>, -1 while unregistered.
        ///     Written only by the registry; consumers must not interpret or store it.
        /// </summary>
        int RegistryHandle { get; }
    }
}
