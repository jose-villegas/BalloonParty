using BalloonParty.Slots.Actor.Archetype;

namespace BalloonParty.Configuration
{
    internal interface IPuffCloudSettings
    {
        PuffCloudView CloudPrefab { get; }
        float AnimationSpeed { get; }
        float Padding { get; }
        int SortingLayerId { get; }
        int SortingOrderOffset { get; }
    }
}

