using BalloonParty.Slots.Actor.Cluster;
using BalloonParty.Slots.Grid;
using VContainer;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Subscribes to grid changes (not setup-only) because Puff slots can be added/removed at runtime.
    /// </summary>
    internal class PuffClusterRegistry : SlotClusterRegistry<PuffObstacleModel>
    {
        [Inject]
        internal PuffClusterRegistry(SlotGrid grid) : base(grid)
        {
        }
    }
}
