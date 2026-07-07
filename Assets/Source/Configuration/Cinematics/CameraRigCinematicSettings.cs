using System;
using UnityEngine;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Configuration.Cinematics
{
    /// <summary>One camera-rig segment: a <see cref="TimeScaleCurve" /> plus how the camera frames it.</summary>
    [Serializable]
    internal class CameraRigCinematicSettings
    {
        [Tooltip("Time.timeScale over the segment's real time; the last key doubles as the segment duration.")]
        [SerializeField] private AnimationCurve _timeScaleCurve;

        [Tooltip("How much the orthographic size shrinks during the segment (0 = base framing).")]
        [SerializeField] private float _zoomAmount;

        [Tooltip("0 = camera stays at its base position, 1 = fully centres on the focus.")]
        [SerializeField] private float _panWeight;

        [Tooltip("Lerp sharpness of the camera easing toward the segment's target (higher = snappier).")]
        [SerializeField] private float _followSpeed;

        public AnimationCurve TimeScaleCurve => _timeScaleCurve;
        public float ZoomAmount => _zoomAmount;
        public float PanWeight => _panWeight;
        public float FollowSpeed => _followSpeed;
    }
}
