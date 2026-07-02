using System;
using BalloonParty.Shared.GameState;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     Everything one <see cref="CinematicState" /> declares, composed from uniform blocks: its
    ///     behavioural <see cref="CinematicTraits" />, the camera-rig segment it plays
    ///     (<see cref="CameraRigCinematicSettings" /> — a restore is just another segment whose curve
    ///     ramps back to 1), and optional capability blocks (<see cref="TrackedTrailSettings" />).
    ///     Indexed by the state's ordinal in <see cref="CinematicsSettings" />.
    /// </summary>
    [Serializable]
    internal class CinematicStateEntry
    {
        [SerializeField] private CinematicTraits _traits = CinematicTraits.None;
        [SerializeField] private CameraRigCinematicSettings _rig = new();
        [SerializeField] private TrackedTrailSettings _trackedTrail = new();

        public CinematicStateEntry()
        {
        }

        public CinematicStateEntry(
            CinematicTraits traits,
            CameraRigCinematicSettings rig,
            TrackedTrailSettings trackedTrail)
        {
            _traits = traits;
            _rig = rig;
            _trackedTrail = trackedTrail;
        }

        public CinematicTraits Traits => _traits;
        public CameraRigCinematicSettings Rig => _rig;
        public TrackedTrailSettings TrackedTrail => _trackedTrail;
    }
}
