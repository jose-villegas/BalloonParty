using System;
using UnityEngine;

namespace BalloonParty.Configuration.Cinematics
{
    /// <summary>
    ///     Tuning for the float-away board effect: balloons float up on their own while swaying on a sine
    ///     and scaling to zero as they clear. Authored entirely in the editor.
    /// </summary>
    [Serializable]
    internal class BoardFloatAwaySettings
    {
        [Tooltip("Per-balloon float time in seconds.")]
        [SerializeField] private float _floatDuration;

        [Tooltip("Delay before the float starts, so it kicks in a bit after the Ascent is already playing.")]
        [SerializeField] private float _startDelay;

        [Tooltip("How far each balloon floats up over the effect, in world units.")]
        [SerializeField] private float _riseHeight;

        [Tooltip("Peak horizontal sway of the sine zigzag, in world units.")]
        [SerializeField] private float _zigzagAmplitude;

        [Tooltip("Number of full side-to-side sways over the rise (sine cycles).")]
        [SerializeField] private float _zigzagFrequency;

        public float FloatDuration => _floatDuration;
        public float StartDelay => _startDelay;
        public float RiseHeight => _riseHeight;
        public float ZigzagAmplitude => _zigzagAmplitude;
        public float ZigzagFrequency => _zigzagFrequency;
    }
}
