using BalloonParty.Slots.Actor.Cluster;
using BalloonParty.Slots.Grid;
using VContainer;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Puff-specific cluster registry. Thin subclass that closes the generic type
    /// parameter and preserves the existing DI registration name.
    /// Subscribes to grid changes (not setup-only) because Puff slots can be
    /// added and removed at runtime.
    /// </summary>
    internal class PuffClusterRegistry : SlotClusterRegistry<PuffObstacleModel>
    {
        [Inject]
        internal PuffClusterRegistry(SlotGrid grid) : base(grid)
        {
        }
    }
}
