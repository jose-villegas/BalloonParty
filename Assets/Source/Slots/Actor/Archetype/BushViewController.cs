using BalloonParty.Configuration;
using BalloonParty.Slots.Actor.Cluster;
using BalloonParty.Slots.Grid;
using VContainer;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Bush-specific cluster view controller. Thin subclass that closes the generic
    /// type parameters and provides the bush prefab from <see cref="IBushSettings"/>.
    /// </summary>
    internal class BushViewController
        : ClusterViewController<BushObstacleModel, BushView, IBushSettings>
    {
        [Inject]
        internal BushViewController(
            BushClusterRegistry registry,
            SlotGrid grid,
            IBushSettings settings,
            IObjectResolver resolver)
            : base(registry, grid, settings, resolver)
        {
        }

        protected override BushView GetPrefab(IBushSettings settings)
        {
            return settings.BushPrefab;
        }
    }
}

