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
        [SerializeField] internal Color HighlightColor = new(0.55f, 0.8f, 0.35f, 0.45f);
        [SerializeField] internal float HighlightSize = 0.3f;
        [SerializeField] internal float HighlightOffset = 0.15f;

        [SerializeField] internal float VeinWidth = 0.06f;
        [SerializeField] internal float VeinDarken = 0.72f;
        [SerializeField] internal int LateralVeinCount = 6;
        [SerializeField] internal float LateralVeinAngle = 1.2f;
        [SerializeField] internal int VeinSources = 150;
        [SerializeField] internal float VeinTexStrength = 0.85f;

        [SerializeField] internal float SSSStrength = 0.25f;
        [SerializeField] internal float SSSAbsorption = 3f;
        [SerializeField] internal Color SSSColor = new(0.6f, 0.8f, 0.2f, 1f);

        [SerializeField] internal float HueJitter = 10f;
        [SerializeField] internal float EdgeBrowningWidth = 0.15f;
        [SerializeField] internal Color BrowningColor = new(0.4f, 0.28f, 0.12f, 1f);
    }
}
