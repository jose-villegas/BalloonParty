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
    }
}
