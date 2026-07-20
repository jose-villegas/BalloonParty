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
        ProjectileFire = 1 << 3,

        /// <summary>The projectile's cruise-triggered burst — one request per bounce while cruising
        /// (see ProjectileView.MoveAndBounce), scaled by <see cref="SpeckProfile.CruiseVelocityCurve" />.</summary>
        ProjectileCruise = 1 << 4,
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

        [Tooltip("ProjectileCruise only: when authored (non-empty), each request's spawn count is " +
                 "round(Count * (CruiseVelocityCurve.Evaluate(t) + 1)) instead of Count directly — X is the " +
                 "shot's NORMALIZED velocity t in [0,1] (0 at base speed, 1 at the cruise ramp's max), Y is " +
                 "added to 1 as the multiplier on Count. Author y≈0 at t=0 so a base-speed bounce spawns the " +
                 "base Count. Empty/unauthored = Count is used unscaled (×1). NEEDS AUTHORING.")]
        public AnimationCurve CruiseVelocityCurve;
    }
}
