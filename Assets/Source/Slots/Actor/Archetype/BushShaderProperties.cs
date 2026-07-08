using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>Leaf and branch shaders deliberately reuse the same shadow/sprite-scale prop names, so one id serves both.</summary>
    internal static class BushShaderProperties
    {
        internal const string RattleKeyword = "_RATTLE_ON";

        internal static readonly int LeafTint = Shader.PropertyToID("_LeafTint");
        internal static readonly int LeafColor = Shader.PropertyToID("_LeafColor");
        internal static readonly int BranchColor = Shader.PropertyToID("_BranchColor");
        internal static readonly int AOIntensity = Shader.PropertyToID("_AOIntensity");
        internal static readonly int UVRect = Shader.PropertyToID("_UVRect");
        internal static readonly int LeafWind = Shader.PropertyToID("_LeafWind");
        internal static readonly int ShadowColor = Shader.PropertyToID("_ShadowColor");
        internal static readonly int ShadowOffset = Shader.PropertyToID("_ShadowOffset");
        internal static readonly int ShadowSoftness = Shader.PropertyToID("_ShadowSoftness");
        internal static readonly int SpriteScale = Shader.PropertyToID("_SpriteScale");
        internal static readonly int WindFrequency = Shader.PropertyToID("_WindFrequency");
        internal static readonly int WindAmplitude = Shader.PropertyToID("_WindAmplitude");
        internal static readonly int WindNoiseAmplitude = Shader.PropertyToID("_WindNoiseAmplitude");
        internal static readonly int WindScalePulse = Shader.PropertyToID("_WindScalePulse");
        internal static readonly int PivotOffset = Shader.PropertyToID("_PivotOffset");
        internal static readonly int RattleAmplitude = Shader.PropertyToID("_RattleAmplitude");
        internal static readonly int RattleFrequency = Shader.PropertyToID("_RattleFrequency");
        internal static readonly int RattleDamping = Shader.PropertyToID("_RattleDamping");
        internal static readonly int BranchGradient = Shader.PropertyToID("_BranchGradient");
        internal static readonly int ShadowSpread = Shader.PropertyToID("_ShadowSpread");
        internal static readonly int AOColor = Shader.PropertyToID("_AOColor");
        internal static readonly int AORadius = Shader.PropertyToID("_AORadius");
        internal static readonly int AOSoftness = Shader.PropertyToID("_AOSoftness");
    }
}
