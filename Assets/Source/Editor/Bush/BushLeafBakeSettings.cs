using System;
using UnityEngine;

namespace BalloonParty.Editor.Bush
{
    [Serializable]
    internal class BushLeafBakeSettings
    {
        [SerializeField] internal int Resolution = 64;
        [SerializeField] internal float LeafRadius = 0.4f;
        [SerializeField] internal float GielisM = 2f;
        [SerializeField] internal float GielisN1 = 1f;
        [SerializeField] internal float GielisN2 = 1.5f;
        [SerializeField] internal float GielisN3 = 1.5f;
        [SerializeField] internal float SSSStrength = 0.25f;
        [SerializeField] internal float SSSAbsorption = 3f;
        [SerializeField] internal Color SSSColor = new(0.6f, 0.8f, 0.2f, 1f);
        [SerializeField] internal float HueJitter = 10f;
        [SerializeField] internal float EdgeBrowningWidth = 0.15f;
        [SerializeField] internal int VeinSources = 150;
        [SerializeField] internal int LeafVariants = 8;
    }
}

