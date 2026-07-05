using System;
using System.Threading;
using BalloonParty.Configuration;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.GameState;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     The level-transition camera beat: a literal vertical translate up and back down —
    ///     simulating a move from the current scenario's center to a new one on top of it — while
    ///     <see cref="Game.Level.LevelTransitionController" /> repopulates the board off-frame. Drives
    ///     <see cref="CinematicCameraRig" /> directly rather than through <see cref="CameraRigCinematic" />,
    ///     since this is a plain per-frame position sample off a curve, not a pan-toward-focus + tweened
    ///     restore.
    /// </summary>
    internal sealed class LevelAscendCinematic
    {
        private readonly CinematicDirector _director;
        private readonly CinematicCameraRig _rig;
        private readonly ICinematicsSettings _settings;

        internal LevelAscendCinematic(CinematicDirector director, CinematicCameraRig rig, ICinematicsSettings settings)
        {
            _director = director;
            _rig = rig;
            _settings = settings;
        }

        /// <summary>
        ///     Translates the camera up and back down along the segment's curve — the curve's VALUE is a
        ///     height fraction here, not a timeScale multiplier (gameplay is already paused via
        ///     <see cref="Shared.Pause.PauseSource.LevelTransition" />), and its <c>ZoomAmount</c> field
        ///     doubles as the ascend height in world units. <paramref name="onBalloonSpawnCue" /> fires
        ///     once, at the segment's <c>PanWeight</c> fraction of the total duration, so the new level's
        ///     balloons are already mid-spawn-animation by the time the camera settles back rather than
        ///     appearing only after arrival. No-ops (firing the cue immediately) if another cinematic
        ///     already owns the director — the rig must not be touched while it's mid-use elsewhere.
        /// </summary>
        internal async UniTask PlayAsync(Action onBalloonSpawnCue, CancellationToken ct)
        {
            if (!_director.TryBeginCinematic(CinematicState.LevelAscend))
            {
                onBalloonSpawnCue?.Invoke();
                return;
            }

            try
            {
                var segment = _settings.EntryOf(CinematicState.LevelAscend).Rig;
                var curve = segment.TimeScaleCurve;
                var duration = curve.Duration();
                var height = segment.ZoomAmount;
                var spawnCueTime = duration * Mathf.Clamp01(segment.PanWeight);
                var spawnCueFired = false;

                _rig.PrepareAscend();

                var elapsed = 0f;
                while (elapsed < duration)
                {
                    if (!spawnCueFired && elapsed >= spawnCueTime)
                    {
                        spawnCueFired = true;
                        onBalloonSpawnCue?.Invoke();
                    }

                    _rig.TranslateAscend(curve.Evaluate(elapsed) * height);
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    elapsed += Time.unscaledDeltaTime;
                }

                if (!spawnCueFired)
                {
                    onBalloonSpawnCue?.Invoke();
                }
            }
            finally
            {
                _rig.Restore();
                _director.EndCinematic();
            }
        }
    }
}
