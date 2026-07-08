using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Actor.Cluster;
using UnityEngine;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Configuration.Effects
{
    internal interface IBushSettings : IClusterViewSettings
    {
        BushView BushPrefab { get; }
        BushVariantData[] BushVariants { get; }
        Shader BranchShader { get; }
        Gradient BranchGradient { get; }
        Color BranchColor { get; }
        Material LeafMaterial { get; }
        Color LeafTint { get; }
        float BushWorldSize { get; }
        float BranchSpriteScale { get; }
        Color BranchShadowColor { get; }
        Vector2 BranchShadowOffset { get; }
        float BranchShadowSpread { get; }
        float BranchShadowSoftness { get; }
        Color BranchAOColor { get; }
        float BranchAORadius { get; }
        float BranchAOSoftness { get; }
        float BranchAOIntensity { get; }
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
        ParticleSystem BushRustleVfx { get; }
        float RustleProximityRadius { get; }

#if UNITY_EDITOR
        /// <summary>Editor-only: when on, bushes rebuild whenever <see cref="Revision" /> changes so tweaks preview live.</summary>
        bool LiveTuning { get; }

        /// <summary>Editor-only: bumped on every inspector edit; views poll it to detect settings changes.</summary>
        int Revision { get; }
#endif
    }
}
