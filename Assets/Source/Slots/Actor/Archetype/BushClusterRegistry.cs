using BalloonParty.Slots.Actor.Cluster;
using BalloonParty.Slots.Grid;
using VContainer;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Bush-specific cluster registry. Uses <c>setupOnly: true</c> because bush
    /// slots never change after initial placement — the registry builds once at
    /// startup and does no further work.
    /// </summary>
    internal class BushClusterRegistry : SlotClusterRegistry<BushObstacleModel>
    {
        [Inject]
        internal BushClusterRegistry(SlotGrid grid) : base(grid, setupOnly: true)
        {
        }
    }
}

