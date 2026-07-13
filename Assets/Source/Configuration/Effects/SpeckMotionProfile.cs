using System;
using BalloonParty.Configuration.Palette;
using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>A motion override for a set of palette colours. A speck showing one of the masked colours blends
    /// its movement from the base motion toward these values, scaled by its heat — the motion analogue of
    /// <see cref="SpeckLookProfile" />, applied in the compute sim.</summary>
    [Serializable]
    internal struct SpeckMotionProfile : IPaletteColorMasked
    {
        [Tooltip("Palette colours this motion applies to.")]
        [PaletteColorMask] [SerializeField] private int _colorMask;

        [SerializeField] private float _brownianStrength;
        [SerializeField] private float _drag;
        [SerializeField] private float _motionInfluence;
        [SerializeField] private float _disturbanceInfluence;

        [Tooltip("Extra velocity damping applied where the disturbance is active.")]
        [SerializeField] private float _disturbanceDamping;

        [Tooltip("Per-speck swirl angle range (degrees) rotating the disturbance push.")]
        [SerializeField] private Vector2 _swirlAngle;

        [Tooltip("Speed specks advance along the disturbance's own motion.")]
        [SerializeField] private float _flowInfluence;

        public int ColorMask => _colorMask;
        public float BrownianStrength => _brownianStrength;
        public float Drag => _drag;
        public float MotionInfluence => _motionInfluence;
        public float DisturbanceInfluence => _disturbanceInfluence;
        public float DisturbanceDamping => _disturbanceDamping;
        public Vector2 SwirlAngle => _swirlAngle;
        public float FlowInfluence => _flowInfluence;
    }
}
