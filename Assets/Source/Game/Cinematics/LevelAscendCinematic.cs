using BalloonParty.Configuration;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.GameState;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     The level-transition camera beat: zooms out while <see cref="Game.Level.LevelTransitionController" />
    ///     repopulates the board off-frame, then snaps back in one frame once the reveal is ready. Drives
    ///     <see cref="CinematicCameraRig" /> directly rather than through <see cref="CameraRigCinematic" /> —
    ///     that runner's restore is always tweened, but the Ascent's swap must be instant and imperceptible
    ///     behind the zoomed-out framing.
    /// </summary>
    internal sealed class LevelAscendCinematic
    {
        private readonly CinematicDirector _director;
        private readonly CinematicCameraRig _rig;
        private readonly ICinematicsSettings _settings;

        private bool _began;

        internal LevelAscendCinematic(CinematicDirector director, CinematicCameraRig rig, ICinematicsSettings settings)
        {
            _director = director;
            _rig = rig;
            _settings = settings;
        }

        /// <summary>
        ///     Begins the zoom-out and returns its duration in seconds — 0 if another cinematic already
        ///     owns the director (the rig must not be touched while it's mid-use elsewhere).
        /// </summary>
        internal float BeginAscend()
        {
            _began = _director.TryBeginCinematic(CinematicState.LevelAscend);
            if (!_began)
            {
                return 0f;
            }

            var segment = _settings.EntryOf(CinematicState.LevelAscend).Rig;
            _rig.PreparePanIn(segment);
            return segment.TimeScaleCurve.Duration();
        }

        internal void EndAscend()
        {
            if (!_began)
            {
                return;
            }

            _began = false;
            _rig.Restore();
            _director.EndCinematic();
        }
    }
}
