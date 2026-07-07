using System;
using UnityEngine;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Configuration.Cinematics
{
    /// <summary>Tuning for a trail the cinematic tracks/puppets during its flight.</summary>
    [Serializable]
    internal class TrackedTrailSettings
    {
        [Tooltip("Scale of the tracked trail over its normalized flight (can pulse above 1).")]
        [SerializeField] private AnimationCurve _scaleCurve;

        public AnimationCurve ScaleCurve => _scaleCurve;
    }
}
