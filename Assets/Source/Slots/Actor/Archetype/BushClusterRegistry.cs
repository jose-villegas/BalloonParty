using BalloonParty.Slots.Actor.Cluster;
using BalloonParty.Slots.Grid;
using VContainer;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Bush-specific cluster registry. Subscribes to grid changes so it can
    /// discover bush actors placed asynchronously by <c>GridSpawnerCoordinator</c>
    /// after <c>Start()</c>. The subscription is effectively dormant after initial
    /// placement because bush slots never change at runtime.
    /// </summary>
    internal class BushClusterRegistry : SlotClusterRegistry<BushObstacleModel>
    {
        [Inject]
        internal BushClusterRegistry(SlotGrid grid) : base(grid)
        {
        }
    }
}

