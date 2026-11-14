using System;
using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    /// <summary>A pop that swells and then shrinks away instead of vanishing at once: the heart balloon's beat.</summary>
    [Serializable]
    public class PopPulse
    {
        [Tooltip("The scale the balloon swells to before it shrinks away. 1 or less = no pulse: it vanishes when it pops.")]
        [SerializeField] [Min(0f)] private float _peakScale;

        [SerializeField] [Min(0f)] private float _swellSeconds = 0.08f;

        [SerializeField] [Min(0f)] private float _shrinkSeconds = 0.14f;

        public float PeakScale => _peakScale;
        public float SwellSeconds => _swellSeconds;
        public float ShrinkSeconds => _shrinkSeconds;

        public bool Enabled => _peakScale > 1f;

        public PopPulse()
        {
        }

        internal PopPulse(float peakScale, float swellSeconds, float shrinkSeconds)
        {
            _peakScale = peakScale;
            _swellSeconds = swellSeconds;
            _shrinkSeconds = shrinkSeconds;
        }
    }
}
