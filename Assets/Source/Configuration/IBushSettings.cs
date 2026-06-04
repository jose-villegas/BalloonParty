using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Actor.Cluster;
using UnityEngine;

namespace BalloonParty.Configuration
{
    internal interface IBushSettings : IClusterViewSettings
    {
        BushView BushPrefab { get; }
        Sprite[] CanopyVariants { get; }
        Sprite[] LeafAtlasSprites { get; }
        int RuffleLeafCount { get; }
        float RuffleRadius { get; }
    }
}
