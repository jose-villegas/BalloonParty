using System;
using UnityEngine;

namespace BalloonParty.Editor.Bush
{
    [Serializable]
    internal class BushCanopyBakeSettings
    {
        [SerializeField] internal int Resolution = 256;
        [SerializeField] internal int SlotCount = 1;
        [SerializeField] internal int CanopyVariants = 4;

        [SerializeField] internal float SlotRadius = 0.4f;
        [SerializeField] internal float RadiusJitter = 0.06f;
        [SerializeField] internal float BranchSpread = 0.55f;
        [SerializeField] internal float SubCircleSize = 0.3f;
        [SerializeField] internal float SubCircleSizeVar = 0.3f;

        [SerializeField] internal float GielisM = 2f;
        [SerializeField] internal float GielisN1 = 1f;
        [SerializeField] internal float GielisN2 = 1.5f;
        [SerializeField] internal float GielisN3 = 1.5f;

        [SerializeField] internal Color BaseColor = new(0.14f, 0.4f, 0.1f, 1f);
        [SerializeField] internal Color TopColor = new(0.35f, 0.65f, 0.2f, 1f);
        [SerializeField] internal float EdgeShade = 0.68f;

        [SerializeField] internal float VeinWidth = 0.06f;
        [SerializeField] internal float VeinDarken = 0.72f;
        [SerializeField] internal int VeinDepth = 2;
        [SerializeField] internal int VeinCount = 6;

        [SerializeField] internal float SSSAbsorption = 3f;
        [SerializeField] internal float SSSStrength = 0.25f;
        [SerializeField] internal Color SSSColor = new(0.6f, 0.8f, 0.2f, 1f);

        [SerializeField] internal float LeafShadowStrength = 0.35f;
        [SerializeField] internal int ShadowSamples = 4;
        [SerializeField] internal float AOMul = 0.4f;

        [SerializeField] internal float HueJitter = 10f;
        [SerializeField] internal float EdgeBrowningWidth = 0.15f;
    }
}
