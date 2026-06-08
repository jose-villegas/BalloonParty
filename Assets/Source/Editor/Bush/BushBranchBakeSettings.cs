using System;
using UnityEngine;

namespace BalloonParty.Editor.Bush
{
    [Serializable]
    internal class BushBranchBakeSettings
    {
        [SerializeField] internal int Resolution = 256;
        [SerializeField] internal int Variants = 4;

        [SerializeField] internal int MaxDepth = 4;
        [SerializeField] internal int BranchesPerNode = 3;
        [SerializeField] internal Vector2 AngleSpread = new(25f, 55f);
        [SerializeField] internal Vector2 LengthRange = new(0.15f, 0.35f);
        [SerializeField] internal float LengthDecay = 0.7f;
        [SerializeField] internal float TrunkLength = 0.12f;
        [SerializeField] internal float BranchWidth = 0.02f;
        [SerializeField] internal float WidthDecay = 0.6f;
        [SerializeField] internal float TipTaper = 0.3f;

        [SerializeField] internal Color BranchColor = new(0.35f, 0.22f, 0.10f, 1f);
        [SerializeField] internal float ColorVariation = 0.08f;
        [SerializeField] internal Gradient BranchGradient = CreateDefaultBranchGradient();

        [SerializeField] internal float BushWorldSize = 0.9f;

        [SerializeField] internal float LeafDepthThreshold = 0.6f;
        [SerializeField] internal int MaxLeavesPerVariant = 16;
        [SerializeField] internal float LeafScale = 0.08f;
        [SerializeField] internal float LeafScaleVariation = 0.3f;
        [Range(0f, 1f)]
        [SerializeField] internal float LeafAttachmentBias = 0.85f;

        private static Gradient CreateDefaultBranchGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.25f, 0.15f, 0.08f), 0f),
                    new GradientColorKey(new Color(0.40f, 0.26f, 0.14f), 0.5f),
                    new GradientColorKey(new Color(0.25f, 0.15f, 0.08f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return g;
        }
    }
}
