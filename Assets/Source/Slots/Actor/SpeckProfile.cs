using System;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>Which caller a speck spawn came from; selects the matching <see cref="SpeckProfile" /> — the
    /// speck-field analogue of the disturbance field's StampSource.</summary>
    [Flags]
    internal enum SpeckSource
    {
        BalloonPop = 1 << 0,
        UnbreakableBurst = 1 << 1,
        ToughWarning = 1 << 2,
    }

    /// <summary>A "spawn N specks here" preset — the speck analogue of a disturbance StampProfile. Resolved by
    /// <see cref="SpeckSource" /> and applied at a requested world position.</summary>
    [Serializable]
    internal struct SpeckProfile
    {
        [Tooltip("Which sources use this profile. Flag multiple to share settings.")]
        public SpeckSource Sources;

        [Tooltip("Specks this profile enables per request, clamped to the field's remaining ceiling room.")]
        public int Count;

        [Tooltip("World-space radius the enabled specks scatter within around the request point.")]
        public float Spread;
    }
}
