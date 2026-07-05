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
    ///     The level-transition beat: descends <see cref="Game.Level.LevelTransitionController" />'s
    ///     staging root — which the new level's static actors are parented under, offset above the
    ///     board — down to <see cref="Vector3.zero" />, so the incoming scenario visibly slides down
    ///     into place instead of the camera moving. No camera work at all: an earlier version drove
    ///     <see cref="CinematicCameraRig" /> for a literal camera translate, but the abrupt up-then-down
    ///     jump read poorly — moving the (much cheaper, already-hidden-until-ready) content instead of
    ///     the camera is both cheaper and reads better.
    /// </summary>
    internal sealed class LevelAscendCinematic
    {
        private readonly CinematicDirector _director;
        private readonly ICinematicsSettings _settings;

        internal LevelAscendCinematic(CinematicDirector director, ICinematicsSettings settings)
        {
            _director = director;
            _settings = settings;
        }

        /// <summary>
        ///     Descends <paramref name="stagingRoot" /> from its current position to <see cref="Vector3.zero" />
        ///     along the segment's curve — the curve's VALUE is a height fraction here, not a timeScale
        ///     multiplier (gameplay is already paused via <see cref="Shared.Pause.PauseSource.LevelTransition" />),
        ///     and its <c>ZoomAmount</c> field doubles as the descend's starting height in world units.
        ///     <paramref name="onBalloonSpawnCue" /> fires once, at the segment's <c>PanWeight</c> fraction
        ///     of the total duration, so the new level's balloons are already mid-spawn-animation by the
        ///     time the scenario settles rather than appearing only after arrival. No-ops (firing the cue
        ///     immediately) if another cinematic already owns the director.
        /// </summary>
        internal async UniTask PlayAsync(Transform stagingRoot, Action onBalloonSpawnCue, CancellationToken ct)
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

                var elapsed = 0f;
                while (elapsed < duration)
                {
                    if (!spawnCueFired && elapsed >= spawnCueTime)
                    {
                        spawnCueFired = true;
                        onBalloonSpawnCue?.Invoke();
                    }

                    var position = stagingRoot.position;
                    position.y = curve.Evaluate(elapsed) * height;
                    stagingRoot.position = position;

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    elapsed += Time.unscaledDeltaTime;
                }

                if (!spawnCueFired)
                {
                    onBalloonSpawnCue?.Invoke();
                }

                stagingRoot.position = Vector3.zero;
            }
            finally
            {
                _director.EndCinematic();
            }
        }
    }
}
