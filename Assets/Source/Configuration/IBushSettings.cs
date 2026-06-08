using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Actor.Cluster;
using UnityEngine;

namespace BalloonParty.Configuration
{
    internal interface IBushSettings : IClusterViewSettings
    {
        BushView BushPrefab { get; }
        BushVariantData[] BushVariants { get; }
        Shader BranchShader { get; }
        Shader LeafShader { get; }
        float BushWorldSize { get; }
        Sprite[] LeafAtlasSprites { get; }
        float WindAmplitude { get; }
        float WindPeriod { get; }
    }
}
