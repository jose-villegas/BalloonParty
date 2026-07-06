using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Actor.Cluster;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Configuration.Effects
{
    internal interface IPuffCloudSettings : IClusterViewSettings
    {
        PuffCloudView CloudPrefab { get; }
    }
}
