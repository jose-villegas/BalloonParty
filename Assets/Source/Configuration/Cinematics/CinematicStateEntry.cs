using System;
using BalloonParty.Shared.GameState;
using UnityEngine;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Configuration.Cinematics
{
    /// <summary>
    ///     Everything one <see cref="CinematicState" /> declares, indexed by its ordinal in <see cref="CinematicsSettings" />.
    /// </summary>
    [Serializable]
    internal class CinematicStateEntry
    {
        [SerializeField] private CinematicTraits _traits;
        [SerializeField] private CameraRigCinematicSettings _rig;
        [SerializeField] private TrackedTrailSettings _trackedTrail;

        public CinematicTraits Traits => _traits;
        public CameraRigCinematicSettings Rig => _rig;
        public TrackedTrailSettings TrackedTrail => _trackedTrail;
    }
}
