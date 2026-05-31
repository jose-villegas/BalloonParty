using BalloonParty.Shared.Pool;
using VContainer;

namespace BalloonParty.Slots.Actor.Archetype
{
    internal class PuffCloudPoolChannel : InjectingPoolChannel<PuffCloudView>
    {
        public PuffCloudPoolChannel(IObjectResolver resolver, PuffCloudView prefab)
            : base(resolver, prefab)
        {
        }
    }
}
