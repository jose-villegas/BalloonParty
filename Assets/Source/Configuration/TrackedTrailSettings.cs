using System;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     Tuning for a trail the cinematic tracks/puppets during its flight — composed into
    ///     <see cref="CinematicStateEntry" /> so any trail-tracking cinematic can use it (the
    ///     level-up drives its tipping trail with it today; others may opt in).
    /// </summary>
    [Serializable]
    internal class TrackedTrailSettings
    {
        [Tooltip("Scale of the tracked trail over its normalized flight (can pulse above 1).")]
        [SerializeField] private AnimationCurve _scaleCurve = AnimationCurve.EaseInOut(0f, 2f, 1f, 1f);

        public TrackedTrailSettings()
        {
        }

        public TrackedTrailSettings(AnimationCurve scaleCurve)
        {
            _scaleCurve = scaleCurve;
        }

        public AnimationCurve ScaleCurve => _scaleCurve;
    }
}
