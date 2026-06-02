using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Actor.Cluster;

namespace BalloonParty.Configuration
{
    internal interface IPuffCloudSettings : IClusterViewSettings
    {
        PuffCloudView CloudPrefab { get; }
    }
}
