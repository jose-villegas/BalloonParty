using System;
using BalloonParty.Configuration.Palette;
using UnityEngine;

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

        [Tooltip("Per-speck scale multiplier range on Speck Size (min, max).")]
        [SerializeField] private Vector2 _scaleRange;

        [Tooltip("Per-speck scale-oscillation rate range (min, max).")]
        [SerializeField] private Vector2 _scalePulseSpeed;

        [Range(0f, 0.5f)] [SerializeField] private float _fadeIn;
        [Range(0f, 0.5f)] [SerializeField] private float _fadeOut;

        public int ColorMask => _colorMask;
        public float SpeckSize => _speckSize;
        public float TrailLength => _trailLength;
        public float TrailMax => _trailMax;
        public Vector2 ScaleRange => _scaleRange;
        public Vector2 ScalePulseSpeed => _scalePulseSpeed;
        public float FadeIn => _fadeIn;
        public float FadeOut => _fadeOut;
    }
}
