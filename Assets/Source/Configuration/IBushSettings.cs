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
        Gradient BranchGradient { get; }
        Material LeafMaterial { get; }
        float BushWorldSize { get; }
        float BranchSpriteScale { get; }
        Color BranchShadowColor { get; }
        Vector2 BranchShadowOffset { get; }
        float BranchShadowSpread { get; }
        float BranchShadowSoftness { get; }
        Color BranchAOColor { get; }
        float BranchAORadius { get; }
        float BranchAOSoftness { get; }
        Sprite[] LeafAtlasSprites { get; }
        Color LeafShadowColor { get; }
        Vector2 LeafShadowOffset { get; }
        float LeafShadowSoftness { get; }
        float LeafSpriteScale { get; }
        float LeafPivotOffset { get; }
        float LeafDepthSplit { get; }
        float WindAmplitude { get; }
        float WindPeriod { get; }
        float WindNoiseAmplitude { get; }
        float WindScalePulse { get; }
        bool RattleEnabled { get; }
        float RattleAmplitude { get; }
        float RattleFrequency { get; }
        float RattleDamping { get; }
    }
}
