using BalloonParty.Shared.Pool;
using VContainer;

namespace BalloonParty.Slots.StaticActor
{
    internal class StaticActorPoolChannel : InjectingPoolChannel<StaticActorView>
    {
        public StaticActorPoolChannel(IObjectResolver resolver, StaticActorView prefab)
            : base(resolver, prefab)
        {
        }
    }
}
