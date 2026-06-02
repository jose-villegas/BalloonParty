using BalloonParty.Configuration;
using BalloonParty.Slots.Actor.Cluster;
using BalloonParty.Slots.Grid;
using VContainer;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Puff-specific cluster view controller. Thin subclass that closes the generic
    /// type parameters and provides the cloud prefab from <see cref="IPuffCloudSettings"/>.
    /// </summary>
    internal class PuffCloudViewController
        : ClusterViewController<PuffObstacleModel, PuffCloudView, IPuffCloudSettings>
    {
        [Inject]
        internal PuffCloudViewController(
            PuffClusterRegistry registry,
            SlotGrid grid,
            IPuffCloudSettings settings,
            IObjectResolver resolver)
            : base(registry, grid, settings, resolver)
        {
        }

        protected override PuffCloudView GetPrefab(IPuffCloudSettings settings)
        {
            return settings.CloudPrefab;
        }
    }
}
