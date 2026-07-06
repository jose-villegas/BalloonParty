using BalloonParty.Slots.Actor.Cluster;
using BalloonParty.Slots.Grid;
using VContainer;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>Bush slots never change at runtime, so the grid subscription is dormant after initial placement.</summary>
    internal class BushClusterRegistry : SlotClusterRegistry<BushObstacleModel>
    {
        [Inject]
        internal BushClusterRegistry(SlotGrid grid) : base(grid)
        {
        }
    }
}
