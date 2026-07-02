using System;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     The tuning block one camera-rig cinematic flies with (zoom/pan/follow + slow-mo ramp +
    ///     restore) — one instance per cinematic in <see cref="CinematicsSettings" />, so each stays
    ///     independently tunable while sharing the same shape. Level-up restores via
    ///     <see cref="RestoreCurve" /> (from the popup's frozen 0), the heart-drain via a plain tween
    ///     over <see cref="RestoreSeconds" /> — unification is the Part-C runner's business.
    /// </summary>
    [Serializable]
    internal class CameraRigCinematicSettings
    {
        [Tooltip("How much the orthographic size shrinks during the pan-in.")]
        [SerializeField] private float _zoomAmount = 0.5f;

        [Tooltip("0 = camera stays at its base position, 1 = fully centres on the focus.")]
        [SerializeField] private float _panWeight = 0.7f;

        [Tooltip("Lerp sharpness of the camera easing toward the pan target (higher = snappier).")]
        [SerializeField] private float _followSpeed = 5f;

        [Tooltip("Time.timeScale over the cinematic's real-time ramp (fast → slowest).")]
        [SerializeField] private AnimationCurve _slowDownCurve = AnimationCurve.EaseInOut(0f, 1f, 0.6f, 0.3f);

        [Tooltip("Time.timeScale over the restore ramp — used by cinematics that restore along a curve.")]
        [SerializeField] private AnimationCurve _restoreCurve = AnimationCurve.EaseInOut(0f, 0.3f, 0.4f, 1f);

        [Tooltip("Restore tween length — used by cinematics that tween timeScale back from wherever it is.")]
        [SerializeField] private float _restoreSeconds = 0.4f;

        public float ZoomAmount => _zoomAmount;
        public float PanWeight => _panWeight;
        public float FollowSpeed => _followSpeed;
        public AnimationCurve SlowDownCurve => _slowDownCurve;
        public AnimationCurve RestoreCurve => _restoreCurve;
        public float RestoreSeconds => _restoreSeconds;
    }
}
