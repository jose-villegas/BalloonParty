using BalloonParty.Slots.Actor.Cluster;
using BalloonParty.Slots.Grid;
using VContainer;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Provides the cloud prefab from <see cref="IPuffCloudSettings"/>.
    /// </summary>
    internal class PuffCloudViewController
        : ClusterViewController<PuffObstacleModel, PuffCloudView, IPuffCloudSettings>
    {
        private static readonly string LowQualityKeyword = "_LOW_QUALITY_CLOUD";

        [Inject]
        internal PuffCloudViewController(
            PuffClusterRegistry registry,
            SlotGrid grid,
            IPuffCloudSettings settings,
            IObjectResolver resolver,
            ScenarioContentRoot scenarioRoot)
            : base(registry, grid, settings, resolver, scenarioRoot)
        {
        }

        protected override PuffCloudView GetPrefab(IPuffCloudSettings settings)
        {
            return settings.CloudPrefab;
        }

        protected override void OnViewCreated(PuffCloudView view)
        {
#if !UNITY_EDITOR
            if (Application.isMobilePlatform && view.Renderer != null)
            {
                view.Renderer.sharedMaterial.EnableKeyword(LowQualityKeyword);
            }
#endif
        }
    }
}
