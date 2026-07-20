using System;
using UnityEngine;

namespace BalloonParty.Configuration.Cinematics
{
    /// <summary>
    ///     Tuning for the float-away board effect: balloons float up on their own while swaying on a sine
    ///     and tilting into the sway as they clear. Authored entirely in the editor.
    /// </summary>
    [Serializable]
    internal class BoardFloatAwaySettings
    {
        [Tooltip("Per-balloon float time in seconds.")]
        [SerializeField] private float _floatDuration;

        [Tooltip("Rise offset in world units (Y) over the float duration (X, in seconds up to Float Duration).")]
        [SerializeField] private AnimationCurve _riseCurve;

        [Tooltip("Random +/- fraction on each balloon's rise height (0 = uniform, 0.3 = up to +/-30%).")]
        [SerializeField] private float _riseVariance;

        [Tooltip("Peak horizontal sway of the sine zigzag, in world units.")]
        [SerializeField] private float _zigzagAmplitude;

        [Tooltip("Number of full side-to-side sways over the rise (sine cycles).")]
        [SerializeField] private float _zigzagFrequency;

        [Tooltip("Peak tilt in degrees, leaning into the sway direction (0 = upright).")]
        [SerializeField] private float _swayTiltAngle;

        public float FloatDuration => _floatDuration;
        public AnimationCurve RiseCurve => _riseCurve;
        public float RiseVariance => _riseVariance;
        public float ZigzagAmplitude => _zigzagAmplitude;
        public float ZigzagFrequency => _zigzagFrequency;
        public float SwayTiltAngle => _swayTiltAngle;
    }
}
