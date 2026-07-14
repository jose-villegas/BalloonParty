using System;
using System.Collections.Generic;
using BalloonParty.Shared.SceneLight;
using UnityEngine;
using UnityEngine.Serialization;

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
        Vector2 ScalePulses { get; }
        Vector2 ScaleHold { get; }
        float FadeIn { get; }
        float FadeOut { get; }
        float HeatGain { get; }
        float HeatDecay { get; }
        float ColorLerpRate { get; }

        /// <summary>Base scene-light response for uncoloured specks (a colour look can override).</summary>
        float LightInfluence { get; }
        SceneLightMode LightMode { get; }

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

        [Tooltip("Number of scale pulses over a speck's lifetime (min, max) — each speck picks a random count " +
                 "in it, so they pulse out of sync. 0 = no pulse (a fixed random size, no animation). Driven " +
                 "by the speck's life fraction, not global time, so it never flickers.")]
        [FormerlySerializedAs("_scalePulseSpeed")]
        [SerializeField] private Vector2 _scalePulses = new(0.4f, 1f);

        [Tooltip("Fraction (0..1) of each pulse spent held at full scale before easing down and back " +
                 "(min, max), per speck. 0 = a smooth continuous pulse; higher = a plateau at full then a dip.")]
        [SerializeField] private Vector2 _scaleHold;

        [Tooltip("Fraction of life spent fading/scaling in.")]
        [Range(0f, 1f)] [SerializeField] private float _fadeIn = 0.15f;

        [Tooltip("Fraction of life spent fading/scaling out.")]
        [Range(0f, 1f)] [SerializeField] private float _fadeOut = 0.25f;

        [Tooltip("How fast a disturbed speck heats toward the material's Disturbed Tint, per unit agitation per second. 0 = no tinting.")]
        [SerializeField] private float _heatGain = 4f;

        [Tooltip("How fast the heat cools once the disturbance passes, per second — the return to the base color.")]
        [SerializeField] private float _heatDecay = 1.5f;

        [Tooltip("Per-second ramp of a speck's crossfade when its palette tag changes color (e.g. the rainbow cycling). Higher = snappier; ~4 crossfades in a quarter second.")]
        [SerializeField] private float _colorLerpRate = 4f;

        [Tooltip("Base scene-light response (0 = specks ignore light, stay emissive). Colour looks blend " +
                 "toward their own value by heat.")]
        [Range(0f, 1f)] [SerializeField] private float _lightInfluence;

        [SerializeField] private SceneLightMode _lightMode;

        [Tooltip("Per-palette-colour look overrides. A speck showing a colour blends from the base look above " +
                 "toward the profile tagged with that colour, scaled by its heat; colours without a profile " +
                 "stay on the base look.")]
        [SerializeField] private SpeckLookProfile[] _colorProfiles = Array.Empty<SpeckLookProfile>();

        public float SpeckSize => _speckSize;
        public float TrailLength => _trailLength;
        public float TrailMax => _trailMax;
        public Vector2 LifetimeRange => _lifetimeRange;
        public Vector2 ScaleRange => _scaleRange;
        public Vector2 ScalePulses => _scalePulses;
        public Vector2 ScaleHold => _scaleHold;
        public float FadeIn => _fadeIn;
        public float FadeOut => _fadeOut;
        public float HeatGain => _heatGain;
        public float HeatDecay => _heatDecay;
        public float ColorLerpRate => _colorLerpRate;
        public float LightInfluence => _lightInfluence;
        public SceneLightMode LightMode => _lightMode;
        public IReadOnlyList<SpeckLookProfile> ColorProfiles => _colorProfiles;
    }
}
