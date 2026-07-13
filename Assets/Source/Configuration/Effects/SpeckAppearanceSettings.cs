using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Per-speck look — size, trail streak, scale pulse, fade, and the heat/colour tinting.</summary>
    internal interface ISpeckAppearanceSettings
    {
        float SpeckSize { get; }
        float TrailLength { get; }
        float TrailMax { get; }
        Vector2 LifetimeRange { get; }
        Vector2 ScaleRange { get; }
        Vector2 ScalePulseSpeed { get; }
        float FadeIn { get; }
        float FadeOut { get; }
        float HeatGain { get; }
        float HeatDecay { get; }
        float ColorLerpRate { get; }

        /// <summary>Per-palette-colour look overrides blended into by a speck's heat; colours without an entry
        /// stay on the base look above.</summary>
        IReadOnlyList<SpeckLookProfile> ColorProfiles { get; }
    }

    [Serializable]
    internal class SpeckAppearanceSettings : ISpeckAppearanceSettings
    {
        [SerializeField] private float _speckSize = 0.03f;

        [Tooltip("Stretches each speck into a streak along its motion, scaled by speed. 0 = round dots.")]
        [SerializeField] private float _trailLength;

        [Tooltip("Max streak length added by the trail (world units), so a fast ascend can't over-stretch.")]
        [SerializeField] private float _trailMax = 0.5f;

        [Tooltip("Per-speck lifetime range (seconds). Each speck fades in, lives, fades out, then respawns.")]
        [SerializeField] private Vector2 _lifetimeRange = new(2f, 6f);

        [Tooltip("Per-speck scale multiplier range on Speck Size, oscillated over time for size variety.")]
        [SerializeField] private Vector2 _scaleRange = new(0.5f, 1.5f);

        [Tooltip("Per-speck scale-oscillation rate range; each speck picks a random speed in it, so they " +
                 "pulse out of sync (fake toward/away drift).")]
        [SerializeField] private Vector2 _scalePulseSpeed = new(0.4f, 1f);

        [Tooltip("Fraction of life spent fading/scaling in.")]
        [Range(0f, 0.5f)] [SerializeField] private float _fadeIn = 0.15f;

        [Tooltip("Fraction of life spent fading/scaling out.")]
        [Range(0f, 0.5f)] [SerializeField] private float _fadeOut = 0.25f;

        [Tooltip("How fast a disturbed speck heats toward the material's Disturbed Tint, per unit agitation per second. 0 = no tinting.")]
        [SerializeField] private float _heatGain = 4f;

        [Tooltip("How fast the heat cools once the disturbance passes, per second — the return to the base color.")]
        [SerializeField] private float _heatDecay = 1.5f;

        [Tooltip("Per-second ramp of a speck's crossfade when its palette tag changes color (e.g. the rainbow cycling). Higher = snappier; ~4 crossfades in a quarter second.")]
        [SerializeField] private float _colorLerpRate = 4f;

        [Tooltip("Per-palette-colour look overrides. A speck showing a colour blends from the base look above " +
                 "toward the profile tagged with that colour, scaled by its heat; colours without a profile " +
                 "stay on the base look.")]
        [SerializeField] private SpeckLookProfile[] _colorProfiles = Array.Empty<SpeckLookProfile>();

        public float SpeckSize => _speckSize;
        public float TrailLength => _trailLength;
        public float TrailMax => _trailMax;
        public Vector2 LifetimeRange => _lifetimeRange;
        public Vector2 ScaleRange => _scaleRange;
        public Vector2 ScalePulseSpeed => _scalePulseSpeed;
        public float FadeIn => _fadeIn;
        public float FadeOut => _fadeOut;
        public float HeatGain => _heatGain;
        public float HeatDecay => _heatDecay;
        public float ColorLerpRate => _colorLerpRate;
        public IReadOnlyList<SpeckLookProfile> ColorProfiles => _colorProfiles;
    }
}
