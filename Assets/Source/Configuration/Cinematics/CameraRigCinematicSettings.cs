using System;
using UnityEngine;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Configuration.Cinematics
{
    /// <summary>
    ///     One camera-rig segment — the uniform shape every cinematic state plays: a
    ///     <see cref="TimeScaleCurve" /> (whose last key is also the segment's duration) plus how the
    ///     camera frames it. A slow-mo pan-in and a restore are the same structure — the restore's curve
    ///     just ramps back to 1 with zoom/pan at 0 (target = base framing).
    /// </summary>
    [Serializable]
    internal class CameraRigCinematicSettings
    {
        [Tooltip("Time.timeScale over the segment's real time; the last key doubles as the segment duration.")]
        [SerializeField] private AnimationCurve _timeScaleCurve = AnimationCurve.EaseInOut(0f, 1f, 0.6f, 0.3f);

        [Tooltip("How much the orthographic size shrinks during the segment (0 = base framing).")]
        [SerializeField] private float _zoomAmount;

        [Tooltip("0 = camera stays at its base position, 1 = fully centres on the focus.")]
        [SerializeField] private float _panWeight;

        [Tooltip("Lerp sharpness of the camera easing toward the segment's target (higher = snappier).")]
        [SerializeField] private float _followSpeed = 5f;

        public CameraRigCinematicSettings()
        {
        }

        public CameraRigCinematicSettings(
            AnimationCurve timeScaleCurve,
            float zoomAmount,
            float panWeight,
            float followSpeed)
        {
            _timeScaleCurve = timeScaleCurve;
            _zoomAmount = zoomAmount;
            _panWeight = panWeight;
            _followSpeed = followSpeed;
        }

        public AnimationCurve TimeScaleCurve => _timeScaleCurve;
        public float ZoomAmount => _zoomAmount;
        public float PanWeight => _panWeight;
        public float FollowSpeed => _followSpeed;
    }
}
