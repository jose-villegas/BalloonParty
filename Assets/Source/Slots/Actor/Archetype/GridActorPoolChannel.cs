using BalloonParty.Shared.Pool;
using VContainer;

namespace BalloonParty.Slots.Actor.Archetype
{
    internal class GridActorPoolChannel : InjectingPoolChannel<GridActorView>
    {
        public GridActorPoolChannel(IObjectResolver resolver, GridActorView prefab)
            : base(resolver, prefab)
        {
        }
    }
}

