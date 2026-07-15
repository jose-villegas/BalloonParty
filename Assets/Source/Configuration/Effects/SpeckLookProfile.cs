using System;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.SceneLight;
using UnityEngine;
using UnityEngine.Serialization;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>A render-look override for a set of palette colours. A speck showing one of the masked colours
    /// blends from the base appearance toward these values, scaled by its heat — so colour and look shift
    /// together.</summary>
    [Serializable]
    internal struct SpeckLookProfile : IPaletteColorMasked
    {
        [Tooltip("Palette colours this look applies to.")]
        [PaletteColorMask] [SerializeField] private int _colorMask;

        [SerializeField] private float _speckSize;

        [Tooltip("Stretches each speck into a streak along its motion, scaled by speed. 0 = round dots.")]
        [SerializeField] private float _trailLength;

        [Tooltip("Max streak length added by the trail (world units).")]
        [SerializeField] private float _trailMax;

        [Tooltip("Per-speck lifetime range (seconds) for this colour. Replaces the base lifetime the moment a " +
                 "speck adopts the colour, so coloured specks can live longer/shorter than the base.")]
        [SerializeField] private Vector2 _lifetimeRange;

        [Tooltip("Per-speck scale multiplier range on Speck Size (min, max).")]
        [SerializeField] private Vector2 _scaleRange;

        [Tooltip("Number of scale pulses over lifetime (min, max). 0 = no pulse (fixed random size).")]
        [FormerlySerializedAs("_scalePulseSpeed")]
        [SerializeField] private Vector2 _scalePulses;

        [Tooltip("Fraction (0..1) of each pulse held at full scale before dipping (min, max).")]
        [SerializeField] private Vector2 _scaleHold;

        [Range(0f, 1f)] [SerializeField] private float _fadeIn;
        [Range(0f, 1f)] [SerializeField] private float _fadeOut;

        [Tooltip("How much the scene light tints this colour's specks (0 = ignore light, emissive; " +
                 "1 = fully lit, local color replaces at full boost; >1 = picks up local light color " +
                 "more eagerly). Blends from the base look's value toward this by heat.")]
        [Range(0f, 4f)] [SerializeField] private float _lightInfluence;

        [Tooltip("Which light these specks read: Full (ambient + local), Ambient (global only), or Local " +
                 "(only nearby field lights, neutral otherwise).")]
        [SerializeField] private SceneLightMode _lightMode;

        public int ColorMask => _colorMask;
        public float SpeckSize => _speckSize;
        public float TrailLength => _trailLength;
        public float TrailMax => _trailMax;
        public Vector2 LifetimeRange => _lifetimeRange;
        public Vector2 ScaleRange => _scaleRange;
        public Vector2 ScalePulses => _scalePulses;
        public Vector2 ScaleHold => _scaleHold;
        public float FadeIn => _fadeIn;
        public float FadeOut => _fadeOut;
        public float LightInfluence => _lightInfluence;
        public SceneLightMode LightMode => _lightMode;
    }
}
