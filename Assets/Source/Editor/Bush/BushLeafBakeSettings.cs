using System;
using UnityEngine;

namespace BalloonParty.Editor.Bush
{
    [Serializable]
    internal class BushLeafBakeSettings
    {
        [SerializeField] internal int Resolution = 64;
        [SerializeField] internal float LeafRadius = 0.4f;
        [SerializeField] internal int LeafVariants = 8;

        [SerializeField] internal float GielisM = 2f;
        [SerializeField] internal float GielisN1 = 1f;
        [SerializeField] internal float GielisN2 = 1.5f;
        [SerializeField] internal float GielisN3 = 1.5f;

        [SerializeField] internal Color BaseColor = new(0.25f, 0.55f, 0.15f, 1f);
        [SerializeField] internal float EdgeShade = 0.68f;
        [SerializeField] internal float HueJitter = 10f;

        [SerializeField] internal bool MidribEnabled = true;
        [SerializeField] internal float MidribWidth = 0.04f;
        [SerializeField] internal Gradient MidribGradient = DefaultMidribGradient();

        [SerializeField] internal int LateralCount = 4;
        [SerializeField] internal Vector2 LateralAngle = new(40f, 60f);
        [SerializeField] internal float LateralWidthRatio = 0.6f;
        [SerializeField] internal float LateralStart = -0.6f;
        [SerializeField] internal Vector2 LateralLength = new(0.4f, 0.8f);
        [SerializeField] internal int LateralSubCount = 2;
        [SerializeField] internal float LateralSubChance = 0.7f;
        [SerializeField] internal Vector2 LateralSubLength = new(0.15f, 0.4f);
        [SerializeField] internal float VeinCurvature = 0.3f;

        private static Gradient DefaultMidribGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.10f, 0.28f, 0.05f), 0f),
                    new GradientColorKey(new Color(0.18f, 0.42f, 0.10f), 0.35f),
                    new GradientColorKey(new Color(0.25f, 0.55f, 0.15f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.85f, 0f),
                    new GradientAlphaKey(0.4f, 0.3f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }
    }
}
