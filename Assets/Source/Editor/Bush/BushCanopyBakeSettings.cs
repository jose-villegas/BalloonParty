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
    }
}
